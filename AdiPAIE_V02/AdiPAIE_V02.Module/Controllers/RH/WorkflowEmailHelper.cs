using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Helper partagé pour l'envoi d'emails dans tous les workflow controllers GRH.
    ///
    /// Règle fondamentale :
    ///   - INonSecuredObjectSpaceFactory et IEmailSender doivent être obtenus
    ///     sur le thread UI (avant Task.Run).
    ///   - Task.Run ne reçoit QUE des primitives (string) — zéro accès XPO.
    ///
    /// Pourquoi INonSecuredObjectSpaceFactory ?
    ///   Les utilisateurs salariés n'ont pas accès à ParametresPaie via
    ///   l'ObjectSpace sécurisé → la config SMTP/Graph est invisible.
    ///   La factory contourne ce filtre de sécurité (usage légitime, données
    ///   de configuration non sensibles pour l'utilisateur).
    /// </summary>
    internal static class WorkflowEmailHelper
    {
        // ── API publique ─────────────────────────────────────────────────

        /// <summary>
        /// Envoie l'email correspondant à une NotificationSalarie.
        /// Doit être appelé sur le thread UI, APRÈS CommitChanges().
        /// </summary>
        public static void EnvoyerNotifAsync(XafApplication app, NotificationSalarie notif)
        {
            if (notif?.Salarie == null) return;

            var dest = notif.Salarie.Email?.Trim();
            if (string.IsNullOrWhiteSpace(dest)) return;

            var sender = ExtraireSender(app);
            if (sender == null) return;

            var sujet = $"[AdiPAIE] {notif.Titre}";
            var body = BuildHtmlNotif(notif);

            EnvoyerAsync(sender, dest, sujet, body);
        }

        /// <summary>
        /// Envoie un email libre à une liste de destinataires RH/DAF/Comptable.
        /// Doit être appelé sur le thread UI.
        /// </summary>
        public static void EnvoyerEmailsAsync(
            XafApplication app,
            IEnumerable<string> destinataires,
            string sujet,
            string body)
        {
            var sender = ExtraireSender(app);
            if (sender == null) return;

            foreach (var dest in destinataires.Where(d => !string.IsNullOrWhiteSpace(d)))
                EnvoyerAsync(sender, dest, sujet, body);
        }

        /// <summary>
        /// Lit les emails RH depuis ParametresPaie (bypass sécurité).
        /// </summary>
        public static List<string> ExtraireEmailsRH(XafApplication app)
            => ExtraireEmails(app, prm => prm?.EmailsRHAlertes);

        /// <summary>
        /// Construit un corps HTML pour les notifications RH (emails directs sans notif in-app).
        /// </summary>
        public static string HtmlTableau(string titre, string message,
            IEnumerable<(string Label, string Valeur)> lignes)
        {
            var rows = string.Concat(lignes.Select((l, i) =>
                $"<tr{(i % 2 == 1 ? " style='background:#EBF3FB;'" : "")}>" +
                $"<td style='padding:6px 12px;'><b>{l.Label}</b></td>" +
                $"<td style='padding:6px 12px;'>{l.Valeur}</td></tr>"));

            return $@"<html><body style='font-family:Segoe UI,Arial;font-size:14px;color:#333;'>
<h2 style='color:#1F4E79;'>{titre}</h2>
<table style='border-collapse:collapse;'>{rows}</table>
<p style='margin-top:16px;'>{message}</p>
<hr style='border:none;border-top:1px solid #BDD7EE;margin:16px 0;'/>
<p style='color:#888;font-size:12px;'>Ce message a été envoyé automatiquement par AdiPAIE.</p>
</body></html>";
        }

        // ── Implémentation interne ───────────────────────────────────────

        internal static IEmailSender ExtraireSender(XafApplication app)
        {
            try
            {
                var factory = app.ServiceProvider
                    .GetRequiredService<INonSecuredObjectSpaceFactory>();
                using var os = factory.CreateNonSecuredObjectSpace(typeof(ParametresPaie));
                var prm = os.GetObjectsQuery<ParametresPaie>().FirstOrDefault();
                if (prm == null)
                {
                    Tracing.Tracer.LogWarning("WorkflowEmailHelper : ParametresPaie introuvable.");
                    return null;
                }
                if (!prm.EmailActif)
                {
                    Tracing.Tracer.LogWarning("WorkflowEmailHelper : EmailActif = false.");
                    return null;
                }
                return prm.CreateEmailSender();
            }
            catch (Exception ex)
            {
                Tracing.Tracer.LogError(ex);
                return null;
            }
        }

        private static List<string> ExtraireEmails(
            XafApplication app, Func<ParametresPaie, string> selector)
        {
            try
            {
                var factory = app.ServiceProvider
                    .GetRequiredService<INonSecuredObjectSpaceFactory>();
                using var os = factory.CreateNonSecuredObjectSpace(typeof(ParametresPaie));
                var prm = os.GetObjectsQuery<ParametresPaie>().FirstOrDefault();
                return ParseEmails(selector(prm) ?? "");
            }
            catch (Exception ex) { Tracing.Tracer.LogError(ex); return new List<string>(); }
        }

        internal static void EnvoyerAsync(IEmailSender sender, string dest, string sujet, string body)
        {
            if (sender == null || string.IsNullOrWhiteSpace(dest)) return;
            Task.Run(() => sender.Send(dest, sujet, body))
                .ContinueWith(t =>
                {
                    if (t.Exception != null)
                        Tracing.Tracer.LogError(
                            $"WorkflowEmailHelper ÉCHEC → {dest} : " +
                            t.Exception.GetBaseException().Message);
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private static List<string> ParseEmails(string raw)
            => (raw ?? "")
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Contains('@'))
                .ToList();

        // ── HTML notification salarié ────────────────────────────────────

        private static string BuildHtmlNotif(NotificationSalarie notif)
        {
            var couleur = (notif.Priorite ?? NotificationPriorite.Info) switch
            {
                NotificationPriorite.Urgent => "#C00000",
                NotificationPriorite.Important => "#7D4E00",
                _ => "#1F4E79"
            };
            var label = (notif.Priorite ?? NotificationPriorite.Info) switch
            {
                NotificationPriorite.Urgent => "URGENT",
                NotificationPriorite.Important => "Important",
                _ => "Information"
            };
            var nom = notif.Salarie?.FullName ?? "Collaborateur";
            var corps = notif.Corps?.Replace("\r\n", "<br>").Replace("\n", "<br>") ?? "";
            var cat = string.IsNullOrWhiteSpace(notif.Categorie) ? "" :
                $"<span style='background:#E8F3FB;color:#1F4E79;padding:2px 8px;" +
                $"border-radius:10px;font-size:12px;'>{notif.Categorie}</span>&nbsp;";
            var date = notif.DateEnvoi.ToString("dd MMMM yyyy à HH:mm",
                            new System.Globalization.CultureInfo("fr-FR"));

            return $@"<!DOCTYPE html><html>
<body style='margin:0;padding:0;background:#F4F6F9;font-family:Segoe UI,Arial,sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0' style='background:#F4F6F9;padding:30px 0;'>
<tr><td align='center'>
<table width='600' cellpadding='0' cellspacing='0'
  style='background:#fff;border-radius:8px;overflow:hidden;
         box-shadow:0 2px 8px rgba(0,0,0,0.08);'>
  <tr><td style='background:{couleur};padding:24px 32px;'>
    <p style='margin:0;color:#fff;font-size:11px;letter-spacing:1px;
              text-transform:uppercase;'>{label}</p>
    <h1 style='margin:8px 0 0;color:#fff;font-size:20px;font-weight:600;
               line-height:1.3;'>{notif.Titre}</h1>
  </td></tr>
  <tr><td style='padding:32px;'>
    <p style='margin:0 0 16px;color:#555;font-size:15px;'>
      Bonjour <strong>{nom}</strong>,</p>
    <div style='background:#F8FAFC;border-left:4px solid {couleur};
                padding:16px 20px;border-radius:4px;margin:0 0 24px;
                color:#333;font-size:14px;line-height:1.7;'>{corps}</div>
    <p style='margin:0;font-size:13px;color:#888;'>{cat}Envoyé le {date}</p>
  </td></tr>
  <tr><td style='background:#F4F6F9;padding:16px 32px;
                 border-top:1px solid #E5E9EF;'>
    <p style='margin:0;font-size:11px;color:#AAA;text-align:center;'>
      Ce message est généré automatiquement par AdiPAIE. Ne pas répondre.</p>
  </td></tr>
</table>
</td></tr></table>
</body></html>";
        }
    }
}
