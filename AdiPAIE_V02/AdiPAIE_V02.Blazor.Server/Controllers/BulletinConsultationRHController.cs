// AdiPAIE_V02.Blazor.Server/Controllers/BulletinConsultationRHController.cs
// ──────────────────────────────────────────────────────────────────────
// Consultation des bulletins côté RH :
//   1. Télécharger PDF (depuis ListView OU DetailView)
//   2. Envoyer au salarié (depuis ListView OU DetailView)
//   3. Filtres rapides prédéfinis (ListView uniquement)
// ──────────────────────────────────────────────────────────────────────
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;
using System.IO;
using System.Net.Mime;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using Attachment = System.Net.Mail.Attachment;

namespace AdiPAIE_V02.Blazor.Server.Controllers
{
    /// <summary>
    /// Actions de consultation RH sur les bulletins.
    /// Fonctionne sur ListView ET DetailView (ObjectView).
    /// Masqué pour les salariés (qui ont leur propre BulletinTelechargerController).
    /// </summary>
    public class BulletinConsultationRHController
        : ObjectViewController<ObjectView, Bulletin>
    {
        private readonly SimpleAction _telechargerPdf;
        private readonly SimpleAction _envoyerEmail;
        private readonly SingleChoiceAction _filtreRapide;

        public BulletinConsultationRHController()
        {
            // ── 1. Télécharger PDF ───────────────────────────────
            _telechargerPdf = new SimpleAction(this,
                "BulletinRH_TelechargerPdf", PredefinedCategory.View)
            {
                Caption = "Télécharger PDF",
                ImageName = "Action_Export",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Télécharge le bulletin sélectionné en PDF.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            _telechargerPdf.Execute += OnTelechargerPdf;

            // ── 2. Envoyer au salarié ────────────────────────────
            _envoyerEmail = new SimpleAction(this,
                "BulletinRH_EnvoyerEmail", PredefinedCategory.View)
            {
                Caption = "Envoyer au salarié",
                ImageName = "BO_Mail",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Envoie le bulletin PDF par email au salarié concerné.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                ConfirmationMessage = "Envoyer le bulletin par email au salarié ?"
            };
            _envoyerEmail.Execute += OnEnvoyerEmail;

            // ── 3. Filtres rapides (ListView uniquement) ─────────
            _filtreRapide = new SingleChoiceAction(this,
                "BulletinRH_FiltreRapide", PredefinedCategory.Filters)
            {
                Caption = "Filtre rapide",
                ImageName = "Action_Filter",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ItemType = SingleChoiceActionItemType.ItemIsOperation
            };

            _filtreRapide.Items.Add(new ChoiceActionItem("Tous", "Tous les bulletins", null));
            _filtreRapide.Items.Add(new ChoiceActionItem("MoisEnCours",
                "Mois en cours", null));
            _filtreRapide.Items.Add(new ChoiceActionItem("MoisPrecedent",
                "Mois précédent", null));
            _filtreRapide.Items.Add(new ChoiceActionItem("Brouillons",
                "Brouillons uniquement", null));
            _filtreRapide.Items.Add(new ChoiceActionItem("Valides",
                "Validés (non envoyés)", null));
            _filtreRapide.Items.Add(new ChoiceActionItem("Envoyes",
                "Envoyés", null));
            _filtreRapide.Items.Add(new ChoiceActionItem("Clotures",
                "Clôturés", null));

            _filtreRapide.Execute += OnFiltreRapide;
        }

        protected override void OnActivated()
        {
            base.OnActivated();

            // Masquer pour les salariés (ils ont BulletinTelechargerController)
            bool estSalarie;
            try
            {
                estSalarie = AdiPAIE_V02.Module.Controllers.EspaceSalarieHelper
                    .EstSalarieConnecte(ObjectSpace);
            }
            catch { estSalarie = false; }

            _telechargerPdf.Active["estRH"] = !estSalarie;
            _envoyerEmail.Active["estRH"] = !estSalarie;

            // Filtres rapides uniquement sur ListView
            _filtreRapide.Active["estRH"] = !estSalarie;
            _filtreRapide.Active["isListView"] = View is ListView;
        }

        // ════════════════════════════════════════════════════════════
        // Helper : récupérer le bulletin courant (ListView ou DetailView)
        // ════════════════════════════════════════════════════════════
        private Bulletin GetBulletinCourant(SimpleActionExecuteEventArgs e)
        {
            if (View is DetailView)
                return View.CurrentObject as Bulletin;

            return e.SelectedObjects.Count == 1
                ? e.SelectedObjects[0] as Bulletin
                : null;
        }

        // ════════════════════════════════════════════════════════════
        // 1. TÉLÉCHARGER PDF
        // ════════════════════════════════════════════════════════════
        private void OnTelechargerPdf(object sender, SimpleActionExecuteEventArgs e)
        {
            var bulletin = GetBulletinCourant(e);
            if (bulletin == null) return;

            try
            {
                byte[] pdfBytes;
                var fileName = $"Bulletin_{bulletin.Periode}_{bulletin.Salarie?.Matricule}.pdf";

                // Archive disponible → servir directement
                if (bulletin.PdfArchive != null && bulletin.PdfArchive.Size > 0)
                {
                    using var ms = new MemoryStream();
                    bulletin.PdfArchive.SaveToStream(ms);
                    pdfBytes = ms.ToArray();
                }
                else
                {
                    // Régénérer à la volée (sans mot de passe pour consultation RH)
                    using var osRead = Application.CreateObjectSpace(typeof(Bulletin));
                    pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(
                        osRead, bulletin.Oid);

                    if (pdfBytes == null || pdfBytes.Length == 0)
                        throw new UserFriendlyException(
                            "Impossible de générer le PDF. Vérifiez que le rapport « BulletinPaie » existe.");

                    // Archiver pour les prochains téléchargements
                    if (bulletin.PdfArchive == null)
                        bulletin.PdfArchive = ObjectSpace
                            .CreateObject<DevExpress.Persistent.BaseImpl.FileData>();
                    using var msArc = new MemoryStream(pdfBytes);
                    bulletin.PdfArchive.LoadFromStream(fileName, msArc);
                    ObjectSpace.CommitChanges();
                }

                // Téléchargement via JSInterop
                var js = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (js == null)
                    throw new UserFriendlyException(
                        "Téléchargement indisponible dans ce contexte.");

                var base64 = Convert.ToBase64String(pdfBytes);
                _ = js.InvokeVoidAsync(
                        "AdiPAIE.downloadFile", fileName, "application/pdf", base64)
                    .AsTask();

                Application.ShowViewStrategy?.ShowMessage(
                    $"Téléchargement de {fileName}…",
                    InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (UserFriendlyException) { throw; }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur PDF : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }

        // ════════════════════════════════════════════════════════════
        // 2. ENVOYER AU SALARIÉ
        // ════════════════════════════════════════════════════════════
        private async void OnEnvoyerEmail(object sender, SimpleActionExecuteEventArgs e)
        {
            _envoyerEmail.Active["Busy"] = false;
            try
            {
                var bulletin = GetBulletinCourant(e);
                if (bulletin == null) return;

                // Validations
                if (string.IsNullOrWhiteSpace(bulletin.Salarie?.Email))
                    throw new UserFriendlyException(
                        "Le salarié n'a pas d'adresse e-mail renseignée.");

                if (bulletin.Statut == BulletinStatut.Brouillon)
                    throw new UserFriendlyException(
                        "Veuillez d'abord valider le bulletin avant de l'envoyer.");

                // Générer ou récupérer le PDF
                using var osRead = Application.CreateObjectSpace(typeof(Bulletin));
                var bReloaded = osRead.GetObjectByKey<Bulletin>(bulletin.Oid)
                    ?? throw new UserFriendlyException("Bulletin introuvable.");

                var keyEnc = bReloaded.Salarie?.PayslipKeyEnc;
                byte[] pdfBytes;
                var fileName = $"Bulletin_{bReloaded.Periode}_{bReloaded.Salarie?.Matricule}.pdf";

                if (bulletin.PdfArchive != null && bulletin.PdfArchive.Size > 0)
                {
                    using var ms = new MemoryStream();
                    bulletin.PdfArchive.SaveToStream(ms);
                    pdfBytes = ms.ToArray();
                }
                else if (!string.IsNullOrWhiteSpace(keyEnc))
                {
                    var pwd = LocalSecretProtector.Unprotect(keyEnc);
                    pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(
                        osRead, bReloaded.Oid, pwd);
                }
                else
                {
                    // Sans clé RGPD → PDF sans mot de passe
                    pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(
                        osRead, bReloaded.Oid);
                }

                if (pdfBytes == null || pdfBytes.Length == 0)
                    throw new UserFriendlyException("Impossible de générer le PDF.");

                // Archiver si pas encore fait
                if (bulletin.PdfArchive == null || bulletin.PdfArchive.Size == 0)
                {
                    if (bulletin.PdfArchive == null)
                        bulletin.PdfArchive = ObjectSpace
                            .CreateObject<DevExpress.Persistent.BaseImpl.FileData>();
                    using var msArc = new MemoryStream(pdfBytes);
                    bulletin.PdfArchive.LoadFromStream(fileName, msArc);
                }

                // Envoyer l'email
                var p = ParametresPaie.TryGet(ObjectSpace)
                    ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
                var senderSvc = p.CreateEmailSender()
                    ?? throw new UserFriendlyException("Service d'envoi d'e-mails non configuré.");

                var subject = $"Bulletin de paie — {bReloaded.Periode}";
                var bodyHtml = $@"<p>Bonjour {bReloaded.Salarie?.FullName},</p>
<p>Veuillez trouver ci-joint votre bulletin de paie pour <b>{bReloaded.Periode}</b>.</p>
<p><i>Ce document est confidentiel.</i></p>
<p>Cordialement,<br/>{p.MailFromDisplayName}</p>";

                using var msAttach = new MemoryStream(pdfBytes);
                using var att = new Attachment(msAttach, fileName, MediaTypeNames.Application.Pdf);
                await senderSvc.SendAsync(bReloaded.Salarie.Email, subject, bodyHtml, att);

                // Mettre à jour le statut si pas encore envoyé
                if (bulletin.Statut == BulletinStatut.Valide)
                    bulletin.Statut = BulletinStatut.Envoye;

                ObjectSpace.CommitChanges();
                View.Refresh();

                AuditService.Enregistrer(Application, "Bulletin", "EnvoyerEmail",
                    bulletin.Oid.ToString(), bulletin.DisplayName,
                    $"Envoyé à {bReloaded.Salarie?.Email}",
                    ancienStatut: null, nouveauStatut: bulletin.Statut.ToString());

                Application.ShowViewStrategy?.ShowMessage(
                    $"Bulletin envoyé à {bReloaded.Salarie?.Email}.",
                    InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (UserFriendlyException) { throw; }
            catch (Exception ex)
            {
                throw new UserFriendlyException($"Échec envoi : {ex.Message}");
            }
            finally { _envoyerEmail.Active.RemoveItem("Busy"); }
        }

        // ════════════════════════════════════════════════════════════
        // 3. FILTRES RAPIDES (ListView uniquement)
        // ════════════════════════════════════════════════════════════
        private void OnFiltreRapide(object sender, SingleChoiceActionExecuteEventArgs e)
        {
            if (View is not ListView listView) return;

            var src = listView.CollectionSource;
            var choix = e.SelectedChoiceActionItem.Id;
            var now = DateTime.Today;

            switch (choix)
            {
                case "Tous":
                    src.Criteria.Remove("FiltreRapideRH");
                    break;

                case "MoisEnCours":
                    src.Criteria["FiltreRapideRH"] =
                        CriteriaOperator.Parse("Annee = ? AND Mois = ?", now.Year, now.Month);
                    break;

                case "MoisPrecedent":
                    var prev = now.AddMonths(-1);
                    src.Criteria["FiltreRapideRH"] =
                        CriteriaOperator.Parse("Annee = ? AND Mois = ?", prev.Year, prev.Month);
                    break;

                case "Brouillons":
                    src.Criteria["FiltreRapideRH"] =
                        CriteriaOperator.Parse("Statut = ?", BulletinStatut.Brouillon);
                    break;

                case "Valides":
                    src.Criteria["FiltreRapideRH"] =
                        CriteriaOperator.Parse("Statut = ?", BulletinStatut.Valide);
                    break;

                case "Envoyes":
                    src.Criteria["FiltreRapideRH"] =
                        CriteriaOperator.Parse("Statut = ?", BulletinStatut.Envoye);
                    break;

                case "Clotures":
                    src.Criteria["FiltreRapideRH"] =
                        CriteriaOperator.Parse("Statut = ?", BulletinStatut.Cloture);
                    break;
            }
        }
    }
}
