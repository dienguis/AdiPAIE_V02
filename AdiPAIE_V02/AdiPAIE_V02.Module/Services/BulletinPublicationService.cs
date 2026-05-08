// =============================================================================
//  BulletinPublicationService.cs — V1.4.3
//
//  Orchestration de la publication d'un bulletin de paie dans l'Espace Salarié.
//
//  Workflow :
//    1. Le RH valide le bulletin   → Statut = Valide   (BulletinValiderEnvoyer)
//    2. Le RH le publie           → Statut = Envoye   (CE service)
//    3. Le RH peut le clôturer    → Statut = Cloture  (BulletinCloturePret)
//
//  Au moment de la publication :
//    - Le PDF est généré (BulletinPdfService) sans mot de passe (l'auth de
//      l'Espace Salarié remplace la clé email).
//    - Le PDF est archivé dans Bulletin.PdfArchive (FileData).
//    - DatePublication et PublieParUser sont renseignés (audit).
//    - Un email de notification est envoyé au salarié (texte simple, sans PJ).
//
//  Le DG a confirmé en CODIR mai 2026 : plus d'envoi de PDF chiffré par email
//  — le salarié télécharge depuis son Espace Salarié authentifié.
// =============================================================================
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class BulletinPublicationService
    {
        /// <summary>
        /// Publie un bulletin : génère le PDF, l'archive, met le statut à Envoye,
        /// renseigne les métadonnées d'audit et envoie l'email de notification.
        /// Idempotent : si déjà publié, ne fait rien (sauf si <paramref name="forceRegenererPdf"/>).
        /// </summary>
        /// <returns>true si une publication a eu lieu, false si bulletin déjà publié et pas de force.</returns>
        public static async Task<bool> PublierAsync(
            Bulletin bulletin,
            IObjectSpace os,
            string currentUserName,
            bool forceRegenererPdf = false,
            ILogger logger = null)
        {
            if (bulletin == null) throw new ArgumentNullException(nameof(bulletin));
            if (os == null) throw new ArgumentNullException(nameof(os));

            // Pré-conditions ──────────────────────────────────────────────
            if (bulletin.Statut < BulletinStatut.Valide)
                throw new UserFriendlyException(
                    "Le bulletin doit être validé avant publication. Validez-le d'abord.");

            if (bulletin.Statut > BulletinStatut.Envoye && !forceRegenererPdf)
                throw new UserFriendlyException(
                    $"Le bulletin est déjà au statut {bulletin.Statut} — utilisez Re-notifier au lieu de Publier.");

            // Idempotence
            if (bulletin.Statut == BulletinStatut.Envoye && bulletin.PdfArchive != null && !forceRegenererPdf)
            {
                logger?.LogInformation(
                    "Bulletin {Oid} déjà publié — saut de la republication.", bulletin.Oid);
                return false;
            }

            // ── 1) Génération du PDF (sans mot de passe) ─────────────────
            byte[] pdfBytes;
            try
            {
                pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(os, bulletin.Oid);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex,
                    "Échec génération PDF du bulletin {Oid} — publication annulée.", bulletin.Oid);
                throw new UserFriendlyException(
                    $"Impossible de générer le PDF du bulletin : {ex.Message}");
            }

            // ── 2) Archivage du PDF ──────────────────────────────────────
            var fileName = $"Bulletin_{bulletin.Salarie?.Matricule}_{bulletin.Annee:0000}_{bulletin.Mois:00}.pdf";
            var pdfArchive = bulletin.PdfArchive ?? new FileData(((XPObjectSpace)os).Session);
            pdfArchive.FileName = fileName;
            pdfArchive.LoadFromStream(fileName, new System.IO.MemoryStream(pdfBytes));
            bulletin.PdfArchive = pdfArchive;

            // ── 3) Mise à jour des métadonnées ───────────────────────────
            bulletin.Statut = BulletinStatut.Envoye;
            bulletin.DatePublication = DateTime.Now;
            bulletin.PublieParUser = currentUserName ?? "system";

            os.CommitChanges();

            // ── 4) Audit ─────────────────────────────────────────────────
            try
            {
                AuditService.EnregistrerDansSession(
                    session: ((XPObjectSpace)os).Session,
                    nomEntite: nameof(Bulletin),
                    action: "Publier",
                    objectId: bulletin.Oid.ToString(),
                    objectLabel: $"Bulletin {bulletin.Mois:00}/{bulletin.Annee} — {bulletin.Salarie?.FullName}",
                    details: $"Publié par {currentUserName} | PDF {pdfBytes.Length / 1024} Ko");
            }
            catch (Exception auditEx)
            {
                logger?.LogWarning(auditEx, "Échec écriture audit pour publication bulletin {Oid}.", bulletin.Oid);
                // non bloquant
            }

            // ── 5) Notification email (best-effort, non bloquant) ────────
            try
            {
                await EnvoyerNotificationAsync(bulletin, os, logger);
            }
            catch (Exception emailEx)
            {
                logger?.LogWarning(emailEx,
                    "Échec envoi email de notification pour bulletin {Oid} — publication conservée.",
                    bulletin.Oid);
            }

            return true;
        }

        /// <summary>
        /// Dépublie un bulletin : repasse au statut Valide. Conserve l'archive PDF
        /// pour traçabilité, mais le filtre Espace Salarié (Statut >= Envoye)
        /// ne le rendra plus accessible côté salarié.
        /// </summary>
        public static void Depublier(
            Bulletin bulletin,
            IObjectSpace os,
            string currentUserName,
            ILogger logger = null)
        {
            if (bulletin == null) throw new ArgumentNullException(nameof(bulletin));
            if (os == null) throw new ArgumentNullException(nameof(os));

            if (bulletin.Statut != BulletinStatut.Envoye)
                throw new UserFriendlyException(
                    $"Seul un bulletin au statut Envoye (publié) peut être dépublié. Statut actuel : {bulletin.Statut}.");

            bulletin.Statut = BulletinStatut.Valide;
            // On garde DatePublication et PublieParUser comme historique.
            // On garde PdfArchive (peut être réutilisé à la republication).

            os.CommitChanges();

            try
            {
                AuditService.EnregistrerDansSession(
                    session: ((XPObjectSpace)os).Session,
                    nomEntite: nameof(Bulletin),
                    action: "Depublier",
                    objectId: bulletin.Oid.ToString(),
                    objectLabel: $"Bulletin {bulletin.Mois:00}/{bulletin.Annee} — {bulletin.Salarie?.FullName}",
                    details: $"Dépublié par {currentUserName}");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Échec écriture audit pour dépublication bulletin {Oid}.", bulletin.Oid);
            }
        }

        /// <summary>
        /// Envoie (ou ré-envoie) l'email de notification au salarié sans
        /// regénérer le PDF. Utile si le salarié a perdu le mail ou si le
        /// SMTP était KO au moment de la publication initiale.
        /// Async pour ne pas bloquer le thread UI Blazor (l'envoi SMTP
        /// synchrone pourrait prendre plusieurs secondes).
        /// </summary>
        public static async Task<bool> EnvoyerNotificationAsync(
            Bulletin bulletin,
            IObjectSpace os,
            ILogger logger = null)
        {
            if (bulletin == null) throw new ArgumentNullException(nameof(bulletin));
            if (os == null) throw new ArgumentNullException(nameof(os));

            if (bulletin.Statut < BulletinStatut.Envoye)
                throw new UserFriendlyException(
                    "Seul un bulletin publié peut faire l'objet d'une notification.");

            var emailSalarie = bulletin.Salarie?.Email?.Trim();
            if (string.IsNullOrWhiteSpace(emailSalarie))
            {
                logger?.LogInformation(
                    "Bulletin {Oid} : salarié {Salarie} sans email — notification ignorée.",
                    bulletin.Oid, bulletin.Salarie?.FullName);
                return false;
            }

            var prm = ParametresPaie.TryGet(os);
            if (prm == null || !prm.EmailActif)
            {
                logger?.LogInformation(
                    "Bulletin {Oid} : EmailActif=false dans ParametresPaie — notification ignorée.",
                    bulletin.Oid);
                return false;
            }

            IEmailSender sender;
            try
            {
                sender = prm.CreateEmailSender();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Bulletin {Oid} : impossible de créer EmailSender.", bulletin.Oid);
                return false;
            }

            var moisLabel = MoisLabel(bulletin.Mois);
            var sujet = $"[SunuPaie] Votre bulletin de paie {moisLabel} {bulletin.Annee} est disponible";
            var body = BuildEmailHtml(bulletin, moisLabel);

            try
            {
                // SendAsync = Task.Run(Send) — délègue au threadpool, libère l'UI
                await sender.SendAsync(emailSalarie, sujet, body, attachment: null);
                logger?.LogInformation(
                    "Bulletin {Oid} : email de notification envoyé à {Email}.",
                    bulletin.Oid, emailSalarie);
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex,
                    "Bulletin {Oid} : échec envoi email à {Email}.", bulletin.Oid, emailSalarie);
                throw; // remonte pour gestion par appelant
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────

        private static string BuildEmailHtml(Bulletin b, string moisLabel)
        {
            var prenom = b.Salarie?.FirstName ?? b.Salarie?.FullName ?? "";
            // V1.5 — Décision DG (mai 2026) : ne PAS afficher le Net à payer
            // dans l'email de notification (confidentialité — l'info reste
            // visible uniquement dans le PDF téléchargé depuis l'Espace Salarié).
            return $@"
<html>
<body style='font-family:Calibri,sans-serif; font-size:14px; color:#222;'>
<p>Bonjour <strong>{System.Net.WebUtility.HtmlEncode(prenom)}</strong>,</p>

<p>Votre bulletin de paie du mois de <strong>{moisLabel} {b.Annee}</strong>
est désormais disponible dans votre Espace Salarié SunuPaie.</p>

<p>Pour le consulter et le télécharger en PDF :</p>
<ol>
  <li>Connectez-vous à votre Espace Salarié SunuPaie</li>
  <li>Cliquez sur le menu <em>Mes bulletins de paie</em></li>
  <li>Cliquez sur <em>Télécharger</em> en face du bulletin du mois</li>
</ol>

<p style='color:#666; font-size:12px;'>
Pour toute question, contactez le service Paie / RH.<br/>
— Service Paie ELTON Oil Company
</p>
</body>
</html>";
        }

        private static string MoisLabel(int mois) => mois switch
        {
            1 => "Janvier",
            2 => "Février",
            3 => "Mars",
            4 => "Avril",
            5 => "Mai",
            6 => "Juin",
            7 => "Juillet",
            8 => "Août",
            9 => "Septembre",
            10 => "Octobre",
            11 => "Novembre",
            12 => "Décembre",
            _ => mois.ToString()
        };
    }
}
