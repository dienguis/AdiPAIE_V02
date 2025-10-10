using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using System;
using System.IO;
using System.Net.Mime;                       // MediaTypeNames.Application.Pdf
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using Attachment = System.Net.Mail.Attachment; // évite l’ambiguïté avec Graph

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class BulletinValiderEnvoyerController
       : ObjectViewController<DetailView, Bulletin>
    {
        private readonly SimpleAction _validerEtEnvoyer;
        private readonly SimpleAction _renvoyer;

        public BulletinValiderEnvoyerController()
        {
            _validerEtEnvoyer = new SimpleAction(this, "ValiderEtEnvoyer", PredefinedCategory.RecordEdit)
            {
                Caption = "Valider et envoyer",
                ImageName = "BO_Mail",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ConfirmationMessage = "Valider ce bulletin et l'envoyer par email ?"
            };
            _validerEtEnvoyer.Execute += OnValiderEtEnvoyerAsync;

            _renvoyer = new SimpleAction(this, "RenvoyerBulletin", PredefinedCategory.RecordEdit)
            {
                Caption = "Renvoyer (PDF archivé)",
                ImageName = "MailSend",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            _renvoyer.Execute += OnRenvoyerAsync;
        }

        // Handler async autorisé (événement XAF)
        private async void OnValiderEtEnvoyerAsync(object sender, SimpleActionExecuteEventArgs e)
        {
            var b = View.CurrentObject as Bulletin;
            if (b == null) return;

            if (string.IsNullOrWhiteSpace(b.Salarie?.Email))
                throw new UserFriendlyException("Le salarié n'a pas d'adresse e-mail.");

            try
            {
                // 1) Valider si nécessaire puis commit pour figer les données
                if (b.Statut == BulletinStatut.Brouillon)
                    b.Statut = BulletinStatut.Valide;

                ObjectSpace.CommitChanges();

                // 2) OS séparé pour l'export (évite tout blocage/re-entrance)
                using var osRead = Application.CreateObjectSpace(typeof(Bulletin));
                var bReloaded = osRead.GetObjectByKey<Bulletin>(b.Oid)
                    ?? throw new UserFriendlyException("Bulletin introuvable après mise à jour.");

                // 3) Générer le PDF depuis le report UI (ReportsV2)
                var pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(osRead, bReloaded.Oid);
                var fileName = $"Bulletin_{bReloaded.Periode}_{bReloaded.Salarie?.Matricule}.pdf";

                // 4) Archiver dans FileData (gèle la version envoyée)
                if (b.PdfArchive == null)
                    b.PdfArchive = ObjectSpace.CreateObject<FileData>();
                // On charge depuis un nouveau stream (ne pas réutiliser celui de l’attachment)
                using (var pdfForArchive = new MemoryStream(pdfBytes))
                    b.PdfArchive.LoadFromStream(fileName, pdfForArchive);

                // 5) Envoyer le mail avec pièce jointe (async)
                var p = ParametresPaie.TryGet(ObjectSpace)
                        ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
                var senderSvc = p.CreateEmailSender();

                using var msAttach = new MemoryStream(pdfBytes);
                using var att = new Attachment(msAttach, fileName, MediaTypeNames.Application.Pdf);

                var subject = $"Bulletin de paie – {bReloaded.Periode}";
                var bodyHtml = $@"
<p>Bonjour {bReloaded.Salarie?.FullName},</p>
<p>Veuillez trouver ci-joint votre bulletin de paie pour <b>{bReloaded.Periode}</b>.</p>
<p>Cordialement,<br/>{p.MailFromDisplayName}</p>";

                await senderSvc.SendAsync(bReloaded.Salarie.Email, subject, bodyHtml, att);

                // 6) Statut final + commit
                b.Statut = BulletinStatut.Envoye;
                ObjectSpace.CommitChanges();

                Application.ShowViewStrategy.ShowMessage(
                    "Bulletin validé, archivé et envoyé.", InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (UserFriendlyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException($"Échec de l'envoi : {ex.Message}");
            }
        }

        private async void OnRenvoyerAsync(object sender, SimpleActionExecuteEventArgs e)
        {
            var b = View.CurrentObject as Bulletin;
            if (b == null) return;

            if (string.IsNullOrWhiteSpace(b.Salarie?.Email))
                throw new UserFriendlyException("Le salarié n'a pas d'adresse e-mail.");

            try
            {
                byte[] pdfBytes;
                string fileName;

                if (b.PdfArchive != null && b.PdfArchive.Size > 0)
                {
                    // Renvoyer l’archive existante
                    fileName = string.IsNullOrWhiteSpace(b.PdfArchive.FileName)
                        ? $"Bulletin_{b.Periode}_{b.Salarie?.Matricule}.pdf"
                        : b.PdfArchive.FileName;

                    using var msRead = new MemoryStream();
                    b.PdfArchive.SaveToStream(msRead);
                    pdfBytes = msRead.ToArray();
                }
                else
                {
                    // Pas d’archive ? On régénère à la volée.
                    using var osRead = Application.CreateObjectSpace(typeof(Bulletin));
                    var bReloaded = osRead.GetObjectByKey<Bulletin>(b.Oid)
                        ?? throw new UserFriendlyException("Bulletin introuvable.");
                    pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(osRead, bReloaded.Oid);
                    fileName = $"Bulletin_{bReloaded.Periode}_{bReloaded.Salarie?.Matricule}.pdf";
                }

                var p = ParametresPaie.TryGet(ObjectSpace)
                        ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
                var senderSvc = p.CreateEmailSender();

                using var msAttach = new MemoryStream(pdfBytes);
                using var att = new Attachment(msAttach, fileName, MediaTypeNames.Application.Pdf);

                var subject = $"[RENVOI] Bulletin de paie – {b.Periode}";
                var bodyHtml = $@"
<p>Bonjour {b.Salarie?.FullName},</p>
<p>Je vous renvoie votre bulletin de paie pour <b>{b.Periode}</b> en pièce jointe.</p>
<p>Cordialement,<br/>{p.MailFromDisplayName}</p>";

                await senderSvc.SendAsync(b.Salarie.Email, subject, bodyHtml, att);

                Application.ShowViewStrategy.ShowMessage(
                    "Bulletin renvoyé.", InformationType.Success, 2500, InformationPosition.Top);
            }
            catch (UserFriendlyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException($"Échec du renvoi : {ex.Message}");
            }
        }
    }
}