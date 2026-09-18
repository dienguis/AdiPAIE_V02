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
using System.Linq;
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
                ToolTip = "Valide le(s) bulletin(s) sélectionné(s) sans les envoyer.",
                // V1.8.6 - Passage en multi-sélection pour permettre la
                // validation en masse (avant : un seul bulletin à la fois).
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects,
                ConfirmationMessage = "Valider le(s) bulletin(s) sélectionné(s) ?"
            };
            _valider.Execute += OnValider;

            _validerEtEnvoyer = new SimpleAction(this, "ValiderEtEnvoyer", PredefinedCategory.Edit)
            {
                Caption = "Valider + envoyer",
                ImageName = "BO_Mail",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Valide le bulletin et envoie le PDF au salarié par e-mail.",
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects,
                ConfirmationMessage = "Valider et envoyer le(s) bulletin(s) sélectionné(s) ?"
            };
            _validerEtEnvoyer.Execute += OnValiderEtEnvoyerAsync;

            _renvoyer = new SimpleAction(this, "RenvoyerBulletin", PredefinedCategory.Edit)
            {
                Caption = "Renvoyer PDF",
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

            // V1.4.3 - ValiderEtEnvoyer et RenvoyerBulletin sont OBSOLÈTES.
            // Le nouveau workflow : Valider -> Publier -> (Re-notifier).
            // L'envoi de PDF chiffré par email est remplacé par notification +
            // accès Espace Salarié (cf. BulletinPublishController).
            // On force ces actions à inactives pour les masquer en attendant
            // de les supprimer définitivement après stabilisation.
            _validerEtEnvoyer.Active.SetItemValue("V143_Obsolete", false);
            _renvoyer.Active.SetItemValue("V143_Obsolete", false);

            if (View is DetailView)
            {
                _renvoyer.SelectionDependencyType = SelectionDependencyType.RequireSingleObject;
            }
            else
            {
                _renvoyer.SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects;
            }
        }

        private void OnValider(object sender, SimpleActionExecuteEventArgs e)
        {
            // V1.8.6 - Boucle sur les objets sélectionnés (validation en masse).
            // Avant : ne traitait que View.CurrentObject -> un seul bulletin.
            var bulletins = new System.Collections.Generic.List<Bulletin>();
            if (e.SelectedObjects != null)
            {
                foreach (var obj in e.SelectedObjects)
                    if (obj is Bulletin b0) bulletins.Add(b0);
            }
            // Fallback DetailView : si aucune sélection multiple, on prend le courant.
            if (bulletins.Count == 0 && View.CurrentObject is Bulletin bCur)
                bulletins.Add(bCur);

            if (bulletins.Count == 0)
                throw new UserFriendlyException("Aucun bulletin sélectionné.");

            int nbValides = 0, nbIgnores = 0;
            var messagesIgnoreParStatut = new System.Collections.Generic.Dictionary<BulletinStatut, int>();

            foreach (var b in bulletins)
            {
                if (b.Statut != BulletinStatut.Brouillon)
                {
                    nbIgnores++;
                    if (!messagesIgnoreParStatut.ContainsKey(b.Statut))
                        messagesIgnoreParStatut[b.Statut] = 0;
                    messagesIgnoreParStatut[b.Statut]++;
                    continue;
                }

                var ancienStatut = b.Statut;

                // 1. Passer en Valide
                b.Statut = BulletinStatut.Valide;

                // 2. Marquer les échéances du mois comme Prélevées
                b.ValiderRemboursementsPrets();

                ObjectSpace.SetModified(b);
                nbValides++;

                SvcAudit.Enregistrer(Application, "Bulletin", "Valider",
                    b.Oid.ToString(), b.DisplayName,
                    $"Net à payer : {b.NetAPayer:N0} FCFA",
                    ancienStatut: ancienStatut.ToString(), nouveauStatut: "Validé");
            }

            ObjectSpace.CommitChanges();
            View.ObjectSpace.Refresh();

            var msg = nbValides > 0
                ? $"{nbValides} bulletin(s) validé(s) - remboursements prêts enregistrés."
                : "Aucun bulletin validé.";
            if (nbIgnores > 0)
            {
                var detailsIgnores = string.Join(", ",
                    messagesIgnoreParStatut.Select(kv => $"{kv.Value} en {kv.Key}"));
                msg += $" {nbIgnores} ignoré(s) (déjà validé(s)/clôturé(s) : {detailsIgnores}).";
            }

            Application.ShowViewStrategy.ShowMessage(
                msg,
                nbValides > 0 ? InformationType.Success : InformationType.Info,
                4500, InformationPosition.Top);
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

                var subject = $"Bulletin de paie - {bReloaded.Periode}";
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

                var subject = $"[RENVOI] Bulletin de paie - {bReloaded.Periode}";
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
