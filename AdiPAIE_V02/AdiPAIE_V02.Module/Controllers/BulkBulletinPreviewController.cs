using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Services;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class BulkBulletinPreviewController : ViewController
    {
        private readonly PopupWindowShowAction _previewSend;
        private SimpleAction _selectAll, _unselectAll, _launchSend;

        public BulkBulletinPreviewController()
        {
            TargetViewId = "Bulletin_ListView"; // lance depuis la liste des bulletins, ou mets un Controller global

            _previewSend = new PopupWindowShowAction(this, "ApercuEnvoiBulletinsMois", PredefinedCategory.Edit)
            {
                Caption = "Aperçu envoi (mois)…",
                ImageName = "MailMerge",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            _previewSend.CustomizePopupWindowParams += OnCustomizePopup;
            _previewSend.Execute += OnDoNothing; // l’envoi se fera via bouton dans le popup
        }

        private void OnCustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            // Paramètres (ici simplifiés) — tu peux faire un vrai objet param si besoin
            string periode = DateTime.Today.AddMonths(-1).ToString("yyyy-MM");
            bool onlyValidated = true;
            bool dryRun = true;
            string overrideEmail = "tests@exemple.com";

            // 1) Construire la collection non persistante
            var npos = Application.CreateObjectSpace(typeof(BulkSendPreviewItem)); // NonPersistentObjectSpace
            var cs = new CollectionSource(npos, typeof(BulkSendPreviewItem));

            using var os = Application.CreateObjectSpace(typeof(Bulletin));
            var crit = CriteriaOperator.Parse("Periode = ?" + (onlyValidated ? " AND Statut = 1" : ""), periode);
            var list = os.GetObjects<Bulletin>(crit);

            foreach (var b in list)
            {
                var item = npos.CreateObject<BulkSendPreviewItem>();
                item.BulletinOid = b.Oid;
                item.Selected = !string.IsNullOrWhiteSpace(b.Salarie?.Email); // pré-sélection si email OK
                item.Periode = b.Periode;
                item.Matricule = b.Salarie?.Matricule;
                item.FullName = b.Salarie?.FullName;
                item.Email = b.Salarie?.Email;
                item.HasArchive = b.PdfArchive != null && b.PdfArchive.Size > 0;
                item.FileName = $"Bulletin_{b.Periode}_{b.Salarie?.Matricule}.pdf";
                item.Note = string.IsNullOrWhiteSpace(b.Salarie?.Email) ? "Email manquant" :
                            (string.IsNullOrEmpty(b.Salarie?.PayslipKeyEnc) ? "Clé RGPD absente" :
                            (item.HasArchive ? "Archive existante" : "À générer"));
                cs.Add(item);
            }

            // 2) Créer la ListView avec la CS (PAS d’affectation après)
            var viewId = Application.FindListViewId(typeof(BulkSendPreviewItem));
            var lv = Application.CreateListView(viewId, cs, /*isRoot*/ false);

            // 3) Ouvrir le popup
            var svp = new ShowViewParameters
            {
                TargetWindow = TargetWindow.NewModalWindow,
                CreatedView = lv
            };
            var dc = Application.CreateController<DialogController>();
            dc.SaveOnAccept = false;
            dc.CancelAction.Caption = "Fermer";
            svp.Controllers.Add(dc);

            Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(Frame, null));



            // 3) Ajouter 3 actions dans le popup : Tout sélectionner / Tout désélectionner / Lancer l’envoi
            _selectAll = new SimpleAction(this, "SelectAllPreview", PredefinedCategory.Edit)
            {
                Caption = "Tout sélectionner",
                ImageName = "Check",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            _unselectAll = new SimpleAction(this, "UnselectAllPreview", PredefinedCategory.Edit)
            {
                Caption = "Tout désélectionner",
                ImageName = "Uncheck",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            _launchSend = new SimpleAction(this, "LaunchBulkSend", PredefinedCategory.Edit)
            {
                Caption = "Lancer l'envoi",
                ImageName = "MailSend",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };

            _selectAll.Execute += (_, __) =>
            {
                foreach (BulkSendPreviewItem it in cs.List) it.Selected = true;
                npos.SetModified(null);
            };
            _unselectAll.Execute += (_, __) =>
            {
                foreach (BulkSendPreviewItem it in cs.List) it.Selected = false;
                npos.SetModified(null);
            };
            _launchSend.Execute += async (_, __) =>
            {
                var pick = cs.List.Cast<BulkSendPreviewItem>()
                                  .Where(x => x.Selected && !string.IsNullOrWhiteSpace(x.Email))
                                  .Select(x => x.BulletinOid)
                                  .ToList();
                if (pick.Count == 0)
                {
                    Application.ShowViewStrategy.ShowMessage("Aucune ligne sélectionnée.", InformationType.Warning, 3000, InformationPosition.Top);
                    return;
                }

                int sent = 0, total = pick.Count;
                void Progress(int s, int t, string info) =>
                    Application.ShowViewStrategy.ShowMessage($"Envoi {s}/{t}… {info}", InformationType.Info, 1200, InformationPosition.Top);

                var results = await BulkBulletinSenderService.SendSpecificAsync(
                    Application, pick, dryRun, overrideEmail, Progress);

                var ok = results.Count(r => r.Ok);
                var ko = results.Where(r => !r.Ok).ToList();
                var sb = new StringBuilder();
                sb.AppendLine($"Aperçu période: {periode} | Dry-run: {dryRun}");
                sb.AppendLine($"Succès: {ok} / Échecs: {ko.Count}");
                foreach (var r in ko.Take(20))
                    sb.AppendLine($"- {r.Matricule} → {r.To}: {r.Error}");
                if (ko.Count > 20) sb.AppendLine($"… (+{ko.Count - 20} autres)");

                Application.ShowViewStrategy.ShowMessage(
                    sb.ToString(), ko.Count == 0 ? InformationType.Success : InformationType.Warning, 7000, InformationPosition.Top);
            };

            // 4) Injecter la ListView dans le popup
            e.View = lv;
            e.DialogController.SaveOnAccept = false;
            e.DialogController.CancelAction.Caption = "Fermer";

            // 5) Raccorder visuellement les actions au popup
            e.DialogController.Actions.Add(_selectAll);
            e.DialogController.Actions.Add(_unselectAll);
            e.DialogController.Actions.Add(_launchSend);
        }

        private void OnDoNothing(object s, PopupWindowShowActionExecuteEventArgs e) { /* no-op */ }
    }
}
