using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using DevExpress.ExpressApp.Notifications;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Service d'envoi d'email lors de la création d'une NotificationSalarie.
    ///
    /// Appelé depuis les controllers après CommitChanges().
    /// Envoie un email au salarié si :
    ///   - Le salarié a une adresse email renseignée
    ///   - EmailActif = true dans ParametresPaie
    ///   - La notification n'a pas déjà été envoyée par email (EmailEnvoye = false)
    ///
    /// Usage :
    ///   NotificationEmailService.Envoyer(notification, objectSpace, logger);
    /// </summary>
    public static class NotificationEmailService
    {
        public static void Envoyer(
            NotificationSalarie notification,
            IObjectSpace os,
            ILogger logger = null)
        {
            if (notification == null) return;
            if (notification.EmailEnvoye) return; // idempotent

            try
            {
                // ── Email du salarié ───────────────────────────────────
                var emailSalarie = notification.Salarie?.Email?.Trim();
                if (string.IsNullOrWhiteSpace(emailSalarie))
                {
                    logger?.LogDebug(
                        "NotificationEmailService : salarié {0} sans email — email ignoré.",
                        notification.Salarie?.FullName);
                    return;
                }

                // ── Config SMTP ────────────────────────────────────────
                var prm = ParametresPaie.TryGet(os);
                if (prm == null || !prm.EmailActif)
                {
                    logger?.LogDebug("NotificationEmailService : email désactivé dans ParametresPaie.");
                    return;
                }

                IEmailSender sender = prm.CreateEmailSender();

                // ── Construction du message ────────────────────────────
                var sujet = $"[AdiPAIE] {notification.Titre}";
                var body = BuildEmailHtml(notification);

                sender.Send(emailSalarie, sujet, body);

                // Marque l'envoi pour éviter les doublons
                notification.EmailEnvoye = true;
                os.CommitChanges();

                logger?.LogInformation(
                    "Email notification envoyé à {0} ({1}).",
                    notification.Salarie?.FullName, emailSalarie);
            }
            catch (Exception ex)
            {
                // On logge mais on ne propage pas — l'échec email
                // ne doit pas bloquer le workflow métier
                logger?.LogError(ex,
                    "Échec de l'envoi email de notification pour {0}.",
                    notification.Salarie?.FullName);
            }
        }

        // ── HTML du message ──────────────────────────────────────

        private static string BuildEmailHtml(NotificationSalarie notif)
        {
            var prioriteCouleur = (notif.Priorite ?? NotificationPriorite.Info) switch
            {
                NotificationPriorite.Urgent => "#C00000",
                NotificationPriorite.Important => "#7D4E00",
                _ => "#1F4E79"
            };

            var prioriteLabel = (notif.Priorite ?? NotificationPriorite.Info) switch
            {
                NotificationPriorite.Urgent => "URGENT",
                NotificationPriorite.Important => "Important",
                _ => "Information"
            };

            var nom = notif.Salarie?.FullName ?? "Collaborateur";
            var corps = notif.Corps?.Replace("\r\n", "<br>").Replace("\n", "<br>")
                          ?? string.Empty;
            var categorie = string.IsNullOrWhiteSpace(notif.Categorie)
                ? string.Empty
                : $"<span style='background:#E8F3FB;color:#1F4E79;padding:2px 8px;border-radius:10px;font-size:12px;'>{notif.Categorie}</span>&nbsp;";
            var dateEnvoi = notif.DateEnvoi.ToString("dd MMMM yyyy à HH:mm");

            return $@"<!DOCTYPE html>
<html>
<body style='margin:0;padding:0;background:#F4F6F9;font-family:Segoe UI,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#F4F6F9;padding:30px 0;'>
    <tr><td align='center'>
      <table width='600' cellpadding='0' cellspacing='0' style='background:#FFFFFF;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);'>

        <!-- En-tête coloré -->
        <tr><td style='background:{prioriteCouleur};padding:24px 32px;'>
          <p style='margin:0;color:#FFFFFF;font-size:11px;letter-spacing:1px;text-transform:uppercase;'>{prioriteLabel}</p>
          <h1 style='margin:8px 0 0;color:#FFFFFF;font-size:20px;font-weight:600;line-height:1.3;'>{notif.Titre}</h1>
        </td></tr>

        <!-- Corps -->
        <tr><td style='padding:32px;'>
          <p style='margin:0 0 16px;color:#555;font-size:15px;'>Bonjour <strong>{nom}</strong>,</p>
          <div style='background:#F8FAFC;border-left:4px solid {prioriteCouleur};padding:16px 20px;border-radius:4px;margin:0 0 24px;color:#333;font-size:14px;line-height:1.7;'>
            {corps}
          </div>
          <p style='margin:0;font-size:13px;color:#888;'>{categorie}Envoyé le {dateEnvoi}</p>
        </td></tr>

        <!-- Pied -->
        <tr><td style='background:#F4F6F9;padding:16px 32px;border-top:1px solid #E5E9EF;'>
          <p style='margin:0;font-size:11px;color:#AAA;text-align:center;'>
            Ce message est généré automatiquement par AdiPAIE. Ne pas répondre directement.
          </p>
        </td></tr>

      </table>
    </td></tr>
  </table>
</body>
</html>";
        }
    }
}
