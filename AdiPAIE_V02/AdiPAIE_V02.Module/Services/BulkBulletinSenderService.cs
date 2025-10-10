// Services/BulkBulletinSenderService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Net.Mail;
using System.Threading.Tasks;
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.Persistent.BaseImpl;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public record BulkSendResult(Guid BulletinOid, string Matricule, string To, bool Ok, string Error = null);

    /// <summary>
    /// Envoi en masse sécurisé (1 itération = 1 bulletin) pour éviter tout mélange pièce jointe/destinataire.
    /// </summary>
    public static class BulkBulletinSenderService
    {
        /// <summary>
        /// Envoie une sélection précise de bulletins (par Oids).
        /// </summary>
        public static async Task<List<BulkSendResult>> SendSpecificAsync(
            XafApplication app,
            IEnumerable<Guid> bulletinOids,
            bool dryRun = false,
            string overrideEmail = null,
            Action<int, int, string> progress = null)
        {
            var results = new List<BulkSendResult>();
            var ids = bulletinOids?.Distinct().ToList() ?? new List<Guid>();
            int total = ids.Count, sent = 0;

            foreach (var id in ids)
            {
                using var os = app.CreateObjectSpace(typeof(Bulletin));
                var b = os.GetObjectByKey<Bulletin>(id);
                if (b == null)
                {
                    results.Add(new BulkSendResult(id, "", "", false, "Introuvable"));
                    continue;
                }

                var matricule = b.Salarie?.Matricule ?? "";
                var to = b.Salarie?.Email;
                progress?.Invoke(sent, total, $"{b.Periode} / {matricule}");

                // Garde-fous RGPD / données minimales
                if (string.IsNullOrWhiteSpace(to))
                {
                    results.Add(new BulkSendResult(b.Oid, matricule, to, false, "Email manquant"));
                    continue;
                }
                if (string.IsNullOrEmpty(b.Salarie?.PayslipKeyEnc))
                {
                    results.Add(new BulkSendResult(b.Oid, matricule, to, false, "Clé RGPD absente"));
                    continue;
                }

                try
                {
                    // Clé PDF (décryptée localement)
                    var key = LocalSecretProtector.Unprotect(b.Salarie.PayslipKeyEnc);

                    // Préparer le PDF (archivé ? sinon générer et archiver)
                    byte[] pdfBytes; string fileName;
                    if (b.PdfArchive != null && b.PdfArchive.Size > 0)
                    {
                        using var ms = new MemoryStream();
                        b.PdfArchive.SaveToStream(ms);
                        pdfBytes = ms.ToArray();
                        fileName = string.IsNullOrWhiteSpace(b.PdfArchive.FileName)
                            ? $"Bulletin_{b.Periode}_{matricule}.pdf"
                            : b.PdfArchive.FileName;
                    }
                    else
                    {
                        pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(os, b.Oid, key);
                        fileName = $"Bulletin_{b.Periode}_{matricule}.pdf";

                        if (b.PdfArchive == null)
                            b.PdfArchive = os.CreateObject<FileData>();
                        using (var msArc = new MemoryStream(pdfBytes))
                            b.PdfArchive.LoadFromStream(fileName, msArc);

                        os.CommitChanges();
                    }

                    // Préparer l'e-mail
                    var p = ParametresPaie.TryGet(os) ?? throw new UserFriendlyException("Paramètres paie introuvables.");
                    var sender = p.CreateEmailSender();

                    var subject = $"Bulletin de paie – {b.Periode}";
                    var bodyHtml = $@"<p>Bonjour {b.Salarie?.FullName},</p>
<p>Veuillez trouver ci-joint votre bulletin de paie pour <b>{b.Periode}</b>.</p>
<p>Cordialement,<br/>{p.MailFromDisplayName}</p>";

                    string finalTo = dryRun && !string.IsNullOrWhiteSpace(overrideEmail) ? overrideEmail : to;

                    using var msAttach = new MemoryStream(pdfBytes);
                    using var att = new Attachment(msAttach, fileName, MediaTypeNames.Application.Pdf);

                    // Envoi
                    await sender.SendAsync(finalTo, subject, bodyHtml, att);

                    // Statut
                    if (!dryRun)
                    {
                        b.Statut = BulletinStatut.Envoye;
                        os.CommitChanges();
                    }

                    results.Add(new BulkSendResult(b.Oid, matricule, finalTo, true));
                    sent++;
                    progress?.Invoke(sent, total, $"{b.Periode} / {matricule}");
                }
                catch (Exception ex)
                {
                    results.Add(new BulkSendResult(b.Oid, matricule, to, false, ex.Message));
                }
            }
            return results;
        }

        /// <summary>
        /// Envoie tous les bulletins d'une période (optionnellement seulement les "Validé").
        /// Wrapper qui prépare la sélection puis appelle SendSpecificAsync(...).
        /// </summary>
        public static async Task<List<BulkSendResult>> SendAllForPeriodAsync(
            XafApplication app,
            Guid periodeOid,
            bool onlyValidated = true,
            bool dryRun = false,
            string overrideEmail = null,
            Action<int, int, string> progress = null)
        {
            using var os = app.CreateObjectSpace(typeof(PeriodePaie));
            var p = os.GetObjectByKey<PeriodePaie>(periodeOid)
                    ?? throw new UserFriendlyException("Période introuvable.");

            // Critère selon ton modèle :
            // Variante 1 : Company directement sur Bulletin
            CriteriaOperator crit = new GroupOperator(GroupOperatorType.And,
                new BinaryOperator("Company", p.Company),
                new BinaryOperator("Annee", p.Annee),
                new BinaryOperator("Mois", p.Mois));

            // // Variante 2 (si Company n'est pas sur Bulletin mais sur Salarie) :
            // crit = CriteriaOperator.Parse("Salarie.Company = ? AND Annee = ? AND Mois = ?", p.Company, p.Annee, p.Mois);

            if (onlyValidated)
                crit = new GroupOperator(GroupOperatorType.And, crit,
                    new BinaryOperator("Statut", BulletinStatut.Valide));

            var bulletins = os.GetObjects<Bulletin>(crit);
            var oids = bulletins.Select(b => b.Oid).ToList();

            return await SendSpecificAsync(app, oids, dryRun, overrideEmail, progress);
        }
    }
}
