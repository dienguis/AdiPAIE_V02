using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using DevExpress.XtraPrinting.Preview;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Blazor.Server.Services
{
    /// <summary>
    /// Service d'arrière-plan qui tourne toutes les heures.
    /// Chaque jour à l'heure configurée dans ParametresPaie.HeureEnvoiAlertes :
    ///
    ///   1. Cherche les DemandeAttestation en statut Soumise depuis plus de N jours
    ///      (N = ParametresPaie.DelaiAlertAttestationJours)
    ///   2. Crée une NotificationSalarie pour chaque RH concerné
    ///   3. Envoie un email récapitulatif aux adresses ParametresPaie.EmailsRHAlertes
    ///
    /// Enregistrement dans Startup.cs :
    ///   services.AddHostedService<AttestationRappelService>();
    /// </summary>
    public class AttestationRappelService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AttestationRappelService> _log;

        // Mémorise la date du dernier envoi pour éviter les doublons dans la même journée
        private DateTime _dernierEnvoi = DateTime.MinValue;

        public AttestationRappelService(
            IServiceProvider services,
            ILogger<AttestationRappelService> log)
        {
            _services = services;
            _log = log;
        }

        // ── Boucle principale ────────────────────────────────────
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("AttestationRappelService démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TryRunIfDue();
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Erreur dans AttestationRappelService.");
                }

                // Vérification toutes les heures
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private async Task TryRunIfDue()
        {
            var now = DateTime.Now;

            // Lit l'heure configurée
            int heureConfigured = await GetHeureEnvoi();

            // Déclenche une seule fois par jour à l'heure configurée
            bool estHeure = now.Hour == heureConfigured;
            bool dejaTourne = _dernierEnvoi.Date == now.Date;

            if (!estHeure || dejaTourne) return;

            _dernierEnvoi = now;
            _log.LogInformation("Lancement du contrôle des attestations en retard ({0:HH:mm}).", now);

            await Task.Run(() => TraiterDemandesEnRetard());
        }

        // ── Logique métier ───────────────────────────────────────
        private void TraiterDemandesEnRetard()
        {
            using var scope = _services.CreateScope();

            // Résout IObjectSpaceProvider via le conteneur DI
            var ospFactory = scope.ServiceProvider
                .GetRequiredService<IObjectSpaceFactory>();

            using var os = ospFactory.CreateObjectSpace(typeof(DemandeAttestation));
            // ── Lecture de la config ───────────────────────────────
            var prm = ParametresPaie.TryGet(os);

            if (prm == null)
            {
                _log.LogWarning("ParametresPaie introuvable — alertes ignorées.");
                return;
            }

            int delai = prm.DelaiAlertAttestationJours;
            if (delai <= 0)
            {
                _log.LogDebug("Alertes attestation désactivées (DelaiAlertAttestationJours = 0).");
                return;
            }

            var dateLimit = DateTime.Today.AddDays(-delai);

            // ── Demandes en retard ─────────────────────────────────
            var demandesEnRetard = os.GetObjects<DemandeAttestation>()
                .Where(d => d.Statut == DemandeStatut.Soumise
                         && d.DateDemande.Date <= dateLimit)
                .OrderBy(d => d.DateDemande)
                .ToList();

            if (!demandesEnRetard.Any())
            {
                _log.LogDebug("Aucune demande en retard.");
                return;
            }

            _log.LogInformation("{0} demande(s) en retard détectée(s).", demandesEnRetard.Count);

            // ── Création des notifications in-app pour les RH ──────
            CreerNotificationsRH(os, demandesEnRetard, delai);

            // ── Envoi email récapitulatif ──────────────────────────
            EnvoyerEmailRecap(prm, demandesEnRetard, delai);

            os.CommitChanges();
        }

        // ── Notifications in-app ─────────────────────────────────

        private void CreerNotificationsRH(
            IObjectSpace os,
            List<DemandeAttestation> demandes,
            int delai)
        {
            // Récupère tous les utilisateurs RH (pas de fiche salarié = RH/admin)
            var utilisateursRH = os.GetObjects<ApplicationUser>()
                .Where(u => u.IsActive && u.Salarie == null)
                .ToList();

            if (!utilisateursRH.Any())
            {
                _log.LogWarning("Aucun utilisateur RH trouvé (ApplicationUser sans fiche Salarie).");
                return;
            }

            var corps = BuildCorpsNotification(demandes, delai);

            foreach (var rh in utilisateursRH)
            {
                // Évite le doublon si une notification non lue existe déjà aujourd'hui
                bool dejaNotifie = os.GetObjects<NotificationSalarie>()
                    .Any(n => n.EnvoyePar == "SYSTEME"
                           && n.DateEnvoi.Date == DateTime.Today
                           && n.Titre.Contains("attestation"));

                if (dejaNotifie) continue;

                // NotificationSalarie est destinée aux salariés —
                // pour les RH on crée une notification système sans destinataire salarié
                // en utilisant le champ Salarie = null (permis car non-[RuleRequired] côté système)
                var notif = os.CreateObject<NotificationSalarie>();
                notif.Titre = $"Rappel : {demandes.Count} demande(s) d'attestation en attente";
                notif.Corps = corps;
                notif.Categorie = "Attestation";
                notif.Priorite = NotificationPriorite.Urgent;
                notif.EnvoyePar = "SYSTEME";
                // Salarie = null → notification visible uniquement côté RH (pas de filtre salarié)
            }

            _log.LogInformation("Notifications in-app créées pour {0} RH.", utilisateursRH.Count);
        }

        // ── Email récapitulatif ───────────────────────────────────

        private void EnvoyerEmailRecap(
            ParametresPaie prm,
            List<DemandeAttestation> demandes,
            int delai)
        {
            if (!prm.EmailActif)
            {
                _log.LogDebug("Email désactivé — pas d'envoi de rappel.");
                return;
            }

            var destinataires = ParseEmails(prm.EmailsRHAlertes);
            if (!destinataires.Any())
            {
                _log.LogWarning("Aucune adresse email RH configurée dans EmailsRHAlertes.");
                return;
            }

            try
            {
                IEmailSender sender = prm.CreateEmailSender();
                var sujet = $"[AdiPAIE] {demandes.Count} demande(s) d'attestation non traitée(s)";
                var body = BuildEmailHtml(demandes, delai);

                foreach (var dest in destinataires)
                {
                    sender.Send(dest, sujet, body);
                    _log.LogInformation("Email d'alerte envoyé à {0}.", dest);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Échec de l'envoi de l'email de rappel.");
            }
        }

        // ── Helpers texte ────────────────────────────────────────

        private static string BuildCorpsNotification(List<DemandeAttestation> demandes, int delai)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Les demandes suivantes sont en attente depuis plus de {delai} jour(s) :");
            sb.AppendLine();
            foreach (var d in demandes)
            {
                var age = (DateTime.Today - d.DateDemande.Date).Days;
                sb.AppendLine($"• {d.Salarie?.FullName ?? "—"} — {d.Nature} — soumise il y a {age} jour(s)");
            }
            return sb.ToString();
        }

        private static string BuildEmailHtml(List<DemandeAttestation> demandes, int delai)
        {
            var sb = new StringBuilder();
            sb.Append(@"<!DOCTYPE html><html><body style='font-family:Segoe UI,Arial;font-size:14px;color:#333;'>
<h2 style='color:#1F4E79;'>Rappel — Demandes d'attestation en attente</h2>");
            sb.Append($"<p>Les demandes suivantes sont en statut <strong>Soumise</strong> depuis plus de <strong>{delai} jour(s)</strong> sans prise en charge :</p>");
            sb.Append(@"<table style='border-collapse:collapse;width:100%;max-width:700px;'>
<thead><tr style='background:#2E75B6;color:#fff;'>
  <th style='padding:8px 12px;text-align:left;'>Salarié</th>
  <th style='padding:8px 12px;text-align:left;'>Nature</th>
  <th style='padding:8px 12px;text-align:left;'>Date demande</th>
  <th style='padding:8px 12px;text-align:left;'>En attente depuis</th>
</tr></thead><tbody>");

            bool alt = false;
            foreach (var d in demandes)
            {
                var age = (DateTime.Today - d.DateDemande.Date).Days;
                var bg = alt ? "#EBF3FB" : "#FFFFFF";
                sb.Append($@"<tr style='background:{bg};'>
  <td style='padding:7px 12px;border-bottom:1px solid #dde;'>{d.Salarie?.FullName ?? "—"}</td>
  <td style='padding:7px 12px;border-bottom:1px solid #dde;'>{d.Nature}</td>
  <td style='padding:7px 12px;border-bottom:1px solid #dde;'>{d.DateDemande:dd/MM/yyyy}</td>
  <td style='padding:7px 12px;border-bottom:1px solid #dde;color:{(age > 5 ? "#C00000" : "#7D4E00")};font-weight:bold;'>{age} jour(s)</td>
</tr>");
                alt = !alt;
            }

            sb.Append("</tbody></table>");
            sb.Append("<br><p style='color:#666;font-size:12px;'>Ce message est généré automatiquement par AdiPAIE. Ne pas répondre directement.</p>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static List<string> ParseEmails(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            return raw.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(e => e.Trim())
                      .Where(e => e.Contains('@'))
                      .ToList();
        }

        // ── Config async ─────────────────────────────────────────
        private async Task<int> GetHeureEnvoi()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var ospFactory = scope.ServiceProvider
                        .GetRequiredService<IObjectSpaceFactory>();
                    using var os = ospFactory.CreateObjectSpace(typeof(ParametresPaie));
                    return ParametresPaie.TryGet(os)?.HeureEnvoiAlertes ?? 8;
                }
                catch { return 8; }
            });
        }
    }
}
