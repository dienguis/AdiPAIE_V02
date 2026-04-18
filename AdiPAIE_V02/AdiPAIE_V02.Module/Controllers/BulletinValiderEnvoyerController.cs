// AdiPAIE_V02.Module/Controllers/BulletinValiderEnvoyerController.cs
// Visible sur DetailView ET ListView via Active["ViewType"]
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.Services;
using SvcAudit = AdiPAIE_V02.Module.Services.AuditService;
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
        : ObjectViewController<ObjectView, Bulletin>
    {
        private readonly SimpleAction _validerEtEnvoyer;
        private readonly SimpleAction _renvoyer;

        private readonly SimpleAction _valider;

        public BulletinValiderEnvoyerController()
        {
            _valider = new SimpleAction(this, "ValiderBulletin", PredefinedCategory.Edit)
            {
                Caption = "Valider",
                ImageName = "btn_valider",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Valide le bulletin sans l'envoyer.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                ConfirmationMessage = "Valider ce bulletin ?"
            };
            _valider.Execute += OnValider;

            _validerEtEnvoyer = new SimpleAction(this, "ValiderEtEnvoyer", PredefinedCategory.Edit)
            {
                Caption = "Valider et envoyer",
                ImageName = "BO_Mail",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Valide le bulletin et envoie le PDF au salarié par e-mail.",
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects,
                ConfirmationMessage = "Valider et envoyer le(s) bulletin(s) sélectionné(s) ?"
            };
            _validerEtEnvoyer.Execute += OnValiderEtEnvoyerAsync;

            _renvoyer = new SimpleAction(this, "RenvoyerBulletin", PredefinedCategory.Edit)
            {
                Caption = "Renvoyer (PDF archivé)",
                ImageName = "btn_renvoyer",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Renvoie le PDF archivé au salarié.",
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects
            };
            _renvoyer.Execute += OnRenvoyerAsync;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            if (View is DetailView)
            {
                // DetailView : uniquement "Renvoyer" — envoi géré par l'aperçu en lot
                _validerEtEnvoyer.Active["UsesBatchSend"] = false;
                _renvoyer.SelectionDependencyType = SelectionDependencyType.RequireSingleObject;
            }
            else
            {
                // ListView : les deux masqués — envoi via ApercuEnvoiBulletinsMois
                _validerEtEnvoyer.Active["UsesBatchSend"] = false;
                _renvoyer.SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects;
            }
        }

        private void OnValider(object sender, SimpleActionExecuteEventArgs e)
        {
            var b = View.CurrentObject as Bulletin
                    ?? throw new UserFriendlyException("Aucun bulletin en contexte.");

            if (b.Statut != DomainEnums.BulletinStatut.Brouillon)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Le bulletin est déjà en statut {b.Statut}.",
                    InformationType.Info, 3000, InformationPosition.Top);
                return;
            }

            // 1. Passer en Valide
            b.Statut = DomainEnums.BulletinStatut.Valide;

            // 2. Marquer automatiquement les échéances du mois comme Prélevées
            b.ValiderRemboursementsPrets();

            ObjectSpace.SetModified(b);
            ObjectSpace.CommitChanges();
            View.ObjectSpace.Refresh();

            SvcAudit.Enregistrer(Application, "Bulletin", "Valider",
                b.Oid.ToString(), b.DisplayName,
                $"Net à payer : {b.NetAPayer:N0} FCFA",
                ancienStatut: "Brouillon", nouveauStatut: "Validé");

            Application.ShowViewStrategy.ShowMessage(
                "Bulletin validé — remboursements prêts enregistrés.",
                InformationType.Success, 2500, InformationPosition.Top);
        }

        private async void OnValiderEtEnvoyerAsync(object sender, SimpleActionExecuteEventArgs e)
        {
            _validerEtEnvoyer.Active["Busy"] = false;
            try
            {
                var b = (View is DetailView
                            ? View.CurrentObject as Bulletin
                            : e.SelectedObjects.Count > 0
                                ? e.SelectedObjects[0] as Bulletin
                                : null)
                        ?? throw new UserFriendlyException("Aucun bulletin sélectionné.");

                if (string.IsNullOrWhiteSpace(b.Salarie?.Email))
                    throw new UserFriendlyException("Le salarié n'a pas d'adresse e-mail.");

                if (b.Statut == BulletinStatut.Brouillon)
                    b.Statut = BulletinStatut.Valide;
                ObjectSpace.SetModified(b);
                ObjectSpace.CommitChanges();

                using var osRead = Application.CreateObjectSpace(typeof(Bulletin));
                var bReloaded = osRead.GetObjectByKey<Bulletin>(b.Oid)
                    ?? throw new UserFriendlyException("Bulletin introuvable après mise à jour.");

                var keyEnc = bReloaded.Salarie?.PayslipKeyEnc;
                if (string.IsNullOrWhiteSpace(keyEnc))
                    throw new UserFriendlyException("Clé PDF absente : générez d'abord la clé du salarié.");

                var userPwd = LocalSecretProtector.Unprotect(keyEnc);
                var pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(osRead, bReloaded.Oid, userPwd);
                var fileName = $"Bulletin_{bReloaded.Periode}_{bReloaded.Salarie?.Matricule}.pdf";

                if (b.PdfArchive == null)
                    b.PdfArchive = ObjectSpace.CreateObject<FileData>();
                using (var pdfForArchive = new MemoryStream(pdfBytes))
                    b.PdfArchive.LoadFromStream(fileName, pdfForArchive);
                ObjectSpace.CommitChanges();

                var p = ParametresPaie.TryGet(ObjectSpace)
                        ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
                var senderSvc = p.CreateEmailSender()
                             ?? throw new UserFriendlyException("Service d'envoi d'e-mails indisponible.");

                var subject = $"Bulletin de paie – {bReloaded.Periode}";
                var bodyHtml = $@"<p>Bonjour {bReloaded.Salarie?.FullName},</p>
<p>Veuillez trouver ci-joint votre bulletin de paie pour <b>{bReloaded.Periode}</b>.</p>
<p><i>Ce document est protégé par votre clé personnelle.</i></p>
<p>Cordialement,<br/>{p.MailFromDisplayName}</p>";

                using var msAttach = new MemoryStream(pdfBytes);
                using var att = new Attachment(msAttach, fileName, MediaTypeNames.Application.Pdf);
                await senderSvc.SendAsync(bReloaded.Salarie.Email, subject, bodyHtml, att);

                b.Statut = BulletinStatut.Envoye;
                ObjectSpace.CommitChanges();
                if (View?.ObjectSpace != null) View.ObjectSpace.Refresh();

                Application.ShowViewStrategy.ShowMessage(
                    $"Bulletin envoyé à {bReloaded.Salarie?.Email}.",
                    InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (UserFriendlyException) { throw; }
            catch (Exception ex) { throw new UserFriendlyException($"Échec envoi : {ex.Message}"); }
            finally { _validerEtEnvoyer.Active.RemoveItem("Busy"); }
        }

        private async void OnRenvoyerAsync(object sender, SimpleActionExecuteEventArgs e)
        {
            _renvoyer.Active["Busy"] = false;
            try
            {
                var b = (View is DetailView
                            ? View.CurrentObject as Bulletin
                            : e.SelectedObjects.Count > 0
                                ? e.SelectedObjects[0] as Bulletin
                                : null)
                        ?? throw new UserFriendlyException("Aucun bulletin sélectionné.");

                if (string.IsNullOrWhiteSpace(b.Salarie?.Email))
                    throw new UserFriendlyException("Le salarié n'a pas d'adresse e-mail.");

                using var osRead = Application.CreateObjectSpace(typeof(Bulletin));
                var bReloaded = osRead.GetObjectByKey<Bulletin>(b.Oid)
                    ?? throw new UserFriendlyException("Bulletin introuvable.");

                var keyEnc = bReloaded.Salarie?.PayslipKeyEnc;
                if (string.IsNullOrWhiteSpace(keyEnc))
                    throw new UserFriendlyException("Clé PDF absente.");

                var userPwd = LocalSecretProtector.Unprotect(keyEnc);
                var pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(osRead, bReloaded.Oid, userPwd);
                var fileName = $"Bulletin_{bReloaded.Periode}_{bReloaded.Salarie?.Matricule}.pdf";

                var p = ParametresPaie.TryGet(ObjectSpace)
                        ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
                var senderSvc = p.CreateEmailSender()
                             ?? throw new UserFriendlyException("Service d'envoi indisponible.");

                var subject = $"[RENVOI] Bulletin de paie – {bReloaded.Periode}";
                var bodyHtml = $@"<p>Bonjour {bReloaded.Salarie?.FullName},</p>
<p>Je vous renvoie votre bulletin de paie pour <b>{bReloaded.Periode}</b> en pièce jointe.</p>
<p>Cordialement,<br/>{p.MailFromDisplayName}</p>";

                using var msAttach = new MemoryStream(pdfBytes);
                using var att = new Attachment(msAttach, fileName, MediaTypeNames.Application.Pdf);
                await senderSvc.SendAsync(bReloaded.Salarie.Email, subject, bodyHtml, att);

                Application.ShowViewStrategy.ShowMessage(
                    "Bulletin renvoyé.", InformationType.Success, 2500, InformationPosition.Top);
            }
            catch (UserFriendlyException) { throw; }
            catch (Exception ex) { throw new UserFriendlyException($"Échec renvoi : {ex.Message}"); }
            finally { _renvoyer.Active.RemoveItem("Busy"); }
        }
    }
}
