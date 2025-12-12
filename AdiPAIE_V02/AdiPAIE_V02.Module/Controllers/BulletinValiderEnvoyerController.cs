
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using System;
using System.IO;
using System.Net.Mime;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using Attachment = System.Net.Mail.Attachment;

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
                ConfirmationMessage = "Valider ce bulletin et l'envoyer par e-mail ?"
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

        private async void OnValiderEtEnvoyerAsync(object sender, SimpleActionExecuteEventArgs e)
        {
            _validerEtEnvoyer.Active["Busy"] = false;
            try
            {
                var b = View.CurrentObject as Bulletin ?? throw new UserFriendlyException("Aucun bulletin en contexte.");
                if (string.IsNullOrWhiteSpace(b.Salarie?.Email))
                    throw new UserFriendlyException("Le salarié n'a pas d'adresse e-mail.");

                // 1) Valider si nécessaire + commit
                if (b.Statut == BulletinStatut.Brouillon)
                    b.Statut = BulletinStatut.Valide;
                ObjectSpace.SetModified(b);
                ObjectSpace.CommitChanges();

                // 2) OS de lecture pour l’export (isolation)
                using var osRead = Application.CreateObjectSpace(typeof(Bulletin));
                var bReloaded = osRead.GetObjectByKey<Bulletin>(b.Oid)
                    ?? throw new UserFriendlyException("Bulletin introuvable après mise à jour.");

                // 3) Clé PDF
                var keyEnc = bReloaded.Salarie?.PayslipKeyEnc;
                if (string.IsNullOrWhiteSpace(keyEnc))
                    throw new UserFriendlyException("Clé PDF absente : générez d’abord la clé du salarié.");
                var userPwd = LocalSecretProtector.Unprotect(keyEnc);

                // 4) Export PDF protégé
                var pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(osRead, bReloaded.Oid, userPwd);
                var fileName = $"Bulletin_{bReloaded.Periode}_{bReloaded.Salarie?.Matricule}.pdf";

                // 5) Archiver avant envoi
                if (b.PdfArchive == null)
                    b.PdfArchive = ObjectSpace.CreateObject<FileData>();
                using (var pdfForArchive = new MemoryStream(pdfBytes, writable: false))
                    b.PdfArchive.LoadFromStream(fileName, pdfForArchive);
                ObjectSpace.CommitChanges();

                // 6) Envoi ASYNC (sans ConfigureAwait(false) !)
                var p = ParametresPaie.TryGet(ObjectSpace)
                        ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
                var senderSvc = p.CreateEmailSender()
                             ?? throw new UserFriendlyException("Service d'envoi d'e-mails indisponible.");

                using (var msAttach = new MemoryStream(pdfBytes, writable: false))
                using (var att = new Attachment(msAttach, fileName, MediaTypeNames.Application.Pdf))
                {
                    var subject = $"Bulletin de paie – {bReloaded.Periode}";
                    var bodyHtml = $@"
<p>Bonjour {bReloaded.Salarie?.FullName},</p>
<p>Veuillez trouver ci-joint votre bulletin de paie pour <b>{bReloaded.Periode}</b>.</p>
<p><i>Le document est protégé par mot de passe.</i></p>
<p>Cordialement,<br/>{p.MailFromDisplayName}</p>";

                    await senderSvc.SendAsync(bReloaded.Salarie.Email, subject, bodyHtml, att);
                }

                // 7) Ces lignes s’exécutent maintenant sur le thread UI
                b.Statut = BulletinStatut.Envoye;
                ObjectSpace.CommitChanges();

                Application.ShowViewStrategy.ShowMessage(
                    "Bulletin validé, archivé et envoyé (PDF protégé).",
                    InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (UserFriendlyException) { throw; }
            catch (Exception ex)
            {
               // DevExpress.ExpressApp.Utils.Tracing.Tracer.LogError(ex);
                throw new UserFriendlyException($"Échec de l'envoi : {ex.Message}");
            }
            finally
            {
                _validerEtEnvoyer.Active.RemoveItem("Busy");
            }
        }

        private async void OnRenvoyerAsync(object sender, SimpleActionExecuteEventArgs e)
        {
            _renvoyer.Active["Busy"] = false;
            try
            {
                var b = View.CurrentObject as Bulletin ?? throw new UserFriendlyException("Aucun bulletin en contexte.");
                if (string.IsNullOrWhiteSpace(b.Salarie?.Email))
                    throw new UserFriendlyException("Le salarié n'a pas d'adresse e-mail.");

                using var osRead = Application.CreateObjectSpace(typeof(Bulletin));
                var bReloaded = osRead.GetObjectByKey<Bulletin>(b.Oid)
                    ?? throw new UserFriendlyException("Bulletin introuvable.");

                var keyEnc = bReloaded.Salarie?.PayslipKeyEnc;
                if (string.IsNullOrWhiteSpace(keyEnc))
                    throw new UserFriendlyException("Clé PDF absente : générez d’abord la clé du salarié.");

                var userPwd = LocalSecretProtector.Unprotect(keyEnc);
                var pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(osRead, bReloaded.Oid, userPwd);
                var fileName = $"Bulletin_{bReloaded.Periode}_{bReloaded.Salarie?.Matricule}.pdf";

                // Optionnel : remettre l’archive à jour
                if (b.PdfArchive == null)
                    b.PdfArchive = ObjectSpace.CreateObject<FileData>();
                using (var pdfForArchive = new MemoryStream(pdfBytes, writable: false))
                    b.PdfArchive.LoadFromStream(fileName, pdfForArchive);
                ObjectSpace.CommitChanges();

                var p = ParametresPaie.TryGet(ObjectSpace)
                        ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
                var senderSvc = p.CreateEmailSender()
                             ?? throw new UserFriendlyException("Service d'envoi d'e-mails indisponible.");

                using (var msAttach = new MemoryStream(pdfBytes, writable: false))
                using (var att = new Attachment(msAttach, fileName, MediaTypeNames.Application.Pdf))
                {
                    var subject = $"[RENVOI] Bulletin de paie – {bReloaded.Periode}";
                    var bodyHtml = $@"
<p>Bonjour {bReloaded.Salarie?.FullName},</p>
<p>Je vous renvoie votre bulletin de paie pour <b>{bReloaded.Periode}</b> en pièce jointe.</p>
<p><i>Le document est protégé par mot de passe.</i></p>
<p>Cordialement,<br/>{p.MailFromDisplayName}</p>";

                    await senderSvc.SendAsync(bReloaded.Salarie.Email, subject, bodyHtml, att);
                }

                Application.ShowViewStrategy.ShowMessage(
                    "Bulletin renvoyé (PDF protégé).",
                    InformationType.Success, 2500, InformationPosition.Top);
            }
            catch (UserFriendlyException) { throw; }
            catch (Exception ex)
            {
                //DevExpress.ExpressApp.Utils.Tracing.Tracer.LogError(ex);
                throw new UserFriendlyException($"Échec du renvoi : {ex.Message}");
            }
            finally
            {
                _renvoyer.Active.RemoveItem("Busy");
            }
        }
    }
}
