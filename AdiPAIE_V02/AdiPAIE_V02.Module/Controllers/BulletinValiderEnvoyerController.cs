using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
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

        public BulletinValiderEnvoyerController()
        {
            _validerEtEnvoyer = new SimpleAction(this, "ValiderEtEnvoyer", PredefinedCategory.RecordEdit)
            {
                Caption = "Valider et envoyer",
                ImageName = "BO_Mail",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ConfirmationMessage = "Valider ce bulletin et l'envoyer par email ?"
            };
            _validerEtEnvoyer.Execute += OnExecuteAsync;
        }

        // IMPORTANT: async void autorisé ici (handler d'événement XAF)
        private async void OnExecuteAsync(object sender, SimpleActionExecuteEventArgs e)
        {
            var b = View.CurrentObject as Bulletin;
            if (b == null) return;

            if (string.IsNullOrWhiteSpace(b.Salarie?.Email))
                throw new UserFriendlyException("Le salarié n'a pas d'adresse e-mail.");

            try
            {
                // 1) Valider si nécessaire puis COMMIT (on fige l’état)
                if (b.Statut == BulletinStatut.Brouillon)
                    b.Statut = BulletinStatut.Valide;

                ObjectSpace.CommitChanges(); // évite que l’export lise un état instable

                // 2) Ouvrir un ObjectSpace séparé (lecture seule) pour fabriquer le PDF
                using var osReadOnly = Application.CreateObjectSpace(typeof(Bulletin));
                var bReloaded = osReadOnly.GetObjectByKey<Bulletin>(b.Oid);
                if (bReloaded == null)
                    throw new UserFriendlyException("Bulletin introuvable après mise à jour.");

                var pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(osReadOnly, bReloaded.Oid);
                var fileName = $"Bulletin_{bReloaded.Periode}_{bReloaded.Salarie?.Matricule}.pdf";

                // 3) Préparer l’email (async)
                var p = ParametresPaie.TryGet(ObjectSpace)
                        ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
                var senderSvc = p.CreateEmailSender();

                using var ms = new MemoryStream(pdfBytes);
                using var att = new Attachment(ms, fileName, MediaTypeNames.Application.Pdf);

                var subject = $"Bulletin de paie – {bReloaded.Periode}";
                var bodyHtml = $@"
<p>Bonjour {bReloaded.Salarie?.FullName},</p>
<p>Veuillez trouver ci-joint votre bulletin de paie pour <b>{bReloaded.Periode}</b>.</p>
<p>Cordialement,<br/>{p.MailFromDisplayName}</p>";

                // On privilégie un sender async; si ton service ne l'a pas,
                // ajoute une méthode async ou utilise SmtpClient.SendMailAsync ici.
                await senderSvc.SendAsync(bReloaded.Salarie.Email, subject, bodyHtml, att);

                // 4) Repasser sur l’OS d’origine pour mettre à jour le statut
                b.Statut = BulletinStatut.Envoye;
                ObjectSpace.CommitChanges();

                Application.ShowViewStrategy.ShowMessage(
                    "Bulletin validé et envoyé.", InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (UserFriendlyException)
            {
                throw; // XAF affichera proprement
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException($"Échec de l'envoi : {ex.Message}");
            }
        }

    }
}