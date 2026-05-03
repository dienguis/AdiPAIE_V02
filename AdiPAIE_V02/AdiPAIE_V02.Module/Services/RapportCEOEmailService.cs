// AdiPAIE_V02.Module/Services/RapportCEOEmailService.cs
// Envoi du rapport CEO par email au destinataire configuré.
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using System;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Services
{
    public static class RapportCEOEmailService
    {
        /// <summary>
        /// Envoie le rapport CEO (PDF et/ou Excel) par email.
        /// </summary>
        public static async Task EnvoyerAsync(
            XafApplication app, RapportCEOData data,
            byte[] pdfBytes, byte[] xlsxBytes)
        {
            using var os = app.CreateObjectSpace(typeof(ParametresPaie));
            var param = ParametresPaie.TryGet(os);
            if (param == null)
                throw new UserFriendlyException("Paramètres de paie introuvables.");

            var emailCEO = param.EmailCEO;
            if (string.IsNullOrWhiteSpace(emailCEO))
                throw new UserFriendlyException(
                    "L'adresse email du CEO n'est pas configurée dans les paramètres de paie "
                    + "(onglet « GRH - Rapport CEO »).");

            if (!param.EmailActif)
                throw new UserFriendlyException("L'envoi d'email est désactivé dans les paramètres.");

            var sender = param.CreateEmailSender();

            string sujet = $"[SunuPaie] Rapport Exécutif — {NomMois(data.Mois)} {data.Annee}";
            string corps = BuildCorpsEmail(data);

            // Construire un MailMessage avec pièces jointes multiples
            using var message = new MailMessage();
            message.To.Add(emailCEO);
            message.Subject = sujet;
            message.Body = corps;
            message.IsBodyHtml = true;

            if (pdfBytes != null && pdfBytes.Length > 0)
            {
                var pdfStream = new MemoryStream(pdfBytes);
                var pdfAtt = new Attachment(pdfStream,
                    $"Rapport_CEO_{data.Annee}_{data.Mois:D2}.pdf",
                    MediaTypeNames.Application.Pdf);
                message.Attachments.Add(pdfAtt);
            }

            if (xlsxBytes != null && xlsxBytes.Length > 0)
            {
                var xlsxStream = new MemoryStream(xlsxBytes);
                var xlsxAtt = new Attachment(xlsxStream,
                    $"Rapport_CEO_{data.Annee}_{data.Mois:D2}.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                message.Attachments.Add(xlsxAtt);
            }

            // Envoi via la première pièce jointe (l'API existante gère 1 attachment)
            // On envoie donc avec l'extension SendAsync pour compatibilité
            Attachment firstAtt = message.Attachments.Count > 0 ? message.Attachments[0] : null;
            await sender.SendAsync(emailCEO, sujet, corps, firstAtt);

            // Audit
            AuditService.Enregistrer(app,
                "RapportCEO", "Email envoyé",
                "-", $"Rapport CEO {data.Mois:D2}/{data.Annee}",
                $"Envoyé à {emailCEO}");
        }

        private static string BuildCorpsEmail(RapportCEOData d)
        {
            return $@"
<div style=""font-family:Arial,sans-serif;font-size:14px;color:#1a1a2e"">
    <h2 style=""color:#0d1b4a"">Rapport Exécutif — {NomMois(d.Mois)} {d.Annee}</h2>
    <p>Bonjour,</p>
    <p>Veuillez trouver ci-joint le rapport exécutif mensuel de <strong>{d.EntrepriseNom}</strong>.</p>

    <table style=""border-collapse:collapse;font-size:13px;margin:16px 0"">
        <tr><td style=""padding:4px 12px;color:#666"">Effectif actif</td><td style=""padding:4px 12px;font-weight:bold"">{d.EffectifActif}</td></tr>
        <tr><td style=""padding:4px 12px;color:#666"">Masse salariale brute</td><td style=""padding:4px 12px;font-weight:bold"">{d.MasseSalarialeBrute:N0} FCFA</td></tr>
        <tr><td style=""padding:4px 12px;color:#666"">Coût total employeur</td><td style=""padding:4px 12px;font-weight:bold"">{d.CoutTotalEmployeur:N0} FCFA</td></tr>
        <tr><td style=""padding:4px 12px;color:#666"">Turnover mensuel</td><td style=""padding:4px 12px;font-weight:bold"">{d.TauxTurnover:F1}%</td></tr>
        <tr><td style=""padding:4px 12px;color:#666"">Absentéisme</td><td style=""padding:4px 12px;font-weight:bold"">{d.TauxAbsenteisme:F1}%</td></tr>
    </table>

    {(d.Alertes.Count > 0
        ? $"<p style=\"color:#e76f51;font-weight:bold\">⚠ {d.Alertes.Count} alerte(s) à consulter dans le rapport joint.</p>"
        : "<p style=\"color:#2d6a4f\">✓ Tous les indicateurs sont dans les seuils normaux.</p>")}

    <p style=""font-size:12px;color:#888;margin-top:20px"">
        Ce rapport a été généré automatiquement par SunuPaie le {d.DateGeneration:dd/MM/yyyy à HH:mm}.<br>
        Document confidentiel — ne pas transférer.
    </p>
</div>";
        }

        private static string NomMois(int mois) => mois switch
        {
            1 => "Janvier", 2 => "Février", 3 => "Mars", 4 => "Avril",
            5 => "Mai", 6 => "Juin", 7 => "Juillet", 8 => "Août",
            9 => "Septembre", 10 => "Octobre", 11 => "Novembre", 12 => "Décembre",
            _ => mois.ToString()
        };
    }
}
