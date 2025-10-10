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
using System.Linq;
using System.Text;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Assistant 2 étapes (Paramètres -> Aperçu -> Envoi) basé sur PeriodePaie.
    /// </summary>
    public sealed class BulkBulletinTwoStepPeriodeController : ObjectViewController<DetailView, PeriodePaie>
    {
        private readonly PopupWindowShowAction _wizard;

        public BulkBulletinTwoStepPeriodeController()
        {
            _wizard = new PopupWindowShowAction(this, "BulkSendWizardForPeriode", PredefinedCategory.Edit)
            {
                Caption = "Envoi bulletins (assistant)…",
                ImageName = "MailMerge",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            _wizard.CustomizePopupWindowParams += ShowParamsPopup;
            _wizard.Execute += OnParamsAccepted;
        }

        // ---- POPUP 1 : PARAMÈTRES (préremplis depuis la Période courante) ----
   
        private void ShowParamsPopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var periode = View.CurrentObject as PeriodePaie   // ← cast fort
                          ?? throw new UserFriendlyException("Période introuvable.");

            var npos = Application.CreateObjectSpace(typeof(BulkSendParams));
            var prm = npos.CreateObject<BulkSendParams>();
            prm.PeriodeOid = periode.Oid;                 // OK
            prm.PeriodeCaption = periode.DisplayName;         // OK

            var dv = Application.CreateDetailView(npos, prm, true);
            e.View = dv;
            e.DialogController.SaveOnAccept = true;
        }

        private void OnParamsAccepted(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var prm = e.PopupWindowViewCurrentObject as BulkSendParams;
            if (prm == null) return;
            ShowPreviewPopup(prm);
        }

        // ---- POPUP 2 : APERÇU + ENVOI ----
        private void ShowPreviewPopup(BulkSendParams prm)
        {
            // 1) charger la période
            using var os = Application.CreateObjectSpace(typeof(PeriodePaie));
            var p = os.GetObjectByKey<PeriodePaie>(prm.PeriodeOid)
                    ?? throw new UserFriendlyException("Période introuvable.");

            // 2) préparer l'espace NP + collection
            var npos = Application.CreateObjectSpace(typeof(BulkSendPreviewItem));
            var cs = new CollectionSource(npos, typeof(BulkSendPreviewItem));

            // 3) critère des bulletins (VARIANTE 1 : Company sur Bulletin)
            var crit = new GroupOperator(GroupOperatorType.And,
                new BinaryOperator("Company", p.Company),
                new BinaryOperator("Annee", p.Annee),
                new BinaryOperator("Mois", p.Mois));

            if (prm.OnlyValidated)
                crit = new GroupOperator(GroupOperatorType.And, crit,
                    new BinaryOperator("Statut", Domain.DomainEnums.BulletinStatut.Valide));

            var bulletins = os.GetObjects<Bulletin>(crit);

            // 4) remplir l’aperçu
            foreach (var b in bulletins)
            {
                var item = npos.CreateObject<BulkSendPreviewItem>();
                item.BulletinOid = b.Oid;
                item.Selected = !string.IsNullOrWhiteSpace(b.Salarie?.Email)
                                   && !string.IsNullOrEmpty(b.Salarie?.PayslipKeyEnc);
                item.Periode = $"{b.Mois:D2}/{b.Annee}";
                item.Matricule = b.Salarie?.Matricule;
                item.FullName = b.Salarie?.FullName;
                item.Email = b.Salarie?.Email;
                item.HasArchive = b.PdfArchive != null && b.PdfArchive.Size > 0;
                item.FileName = $"Bulletin_{b.Periode}_{b.Salarie?.Matricule}.pdf";
                item.Note =
                    string.IsNullOrWhiteSpace(b.Salarie?.Email) ? "Email manquant" :
                    (string.IsNullOrEmpty(b.Salarie?.PayslipKeyEnc) ? "Clé RGPD absente" :
                    (item.HasArchive ? "Archive existante" : "À générer"));

                cs.Add(item);
            }

            // 5) créer la ListView en passant la CS (pas d’assignation après)
            var viewId = Application.FindListViewId(typeof(BulkSendPreviewItem));
            var lv = Application.CreateListView(viewId, cs, false);

            // 6) actions du popup 2
            var selectAll = new SimpleAction(this, "PreviewSelectAll_Per", PredefinedCategory.Edit)
            { Caption = "Tout sélectionner", ImageName = "Check", PaintStyle = ActionItemPaintStyle.CaptionAndImage };
            var unselectAll = new SimpleAction(this, "PreviewUnselectAll_Per", PredefinedCategory.Edit)
            { Caption = "Tout désélectionner", ImageName = "Uncheck", PaintStyle = ActionItemPaintStyle.CaptionAndImage };
            var launch = new SimpleAction(this, "PreviewLaunchSend_Per", PredefinedCategory.Edit)
            { Caption = prm.DryRun ? "Lancer l'envoi (Dry-run)" : "Lancer l'envoi", ImageName = "MailSend", PaintStyle = ActionItemPaintStyle.CaptionAndImage };

            selectAll.Execute += (_, __) =>
            {
                foreach (BulkSendPreviewItem it in cs.List) it.Selected = true;
                npos.SetModified(null); // ou it.ObjectSpace?.SetModified(it);
            };
            unselectAll.Execute += (_, __) =>
            {
                foreach (BulkSendPreviewItem it in cs.List) it.Selected = false;
                npos.SetModified(null);
            };
            launch.Execute += async (_, __) =>
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

                void Progress(int s, int t, string info) =>
                    Application.ShowViewStrategy.ShowMessage($"Envoi {s}/{t}… {info}",
                        InformationType.Info, 1000, InformationPosition.Top);

                var results = await BulkBulletinSenderService.SendSpecificAsync(
                    Application, pick, prm.DryRun, prm.OverrideEmail, Progress);

                var ok = results.Count(r => r.Ok);
                var ko = results.Where(r => !r.Ok).ToList();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Période: {prm.PeriodeCaption} | Dry-run: {prm.DryRun} {(prm.DryRun ? $"→ {prm.OverrideEmail}" : "")}");
                sb.AppendLine($"Succès: {ok} / Échecs: {ko.Count}");
                foreach (var r in ko.Take(20))
                    sb.AppendLine($"- {r.Matricule} → {r.To}: {r.Error}");
                if (ko.Count > 20) sb.AppendLine($"… (+{ko.Count - 20} autres)");

                Application.ShowViewStrategy.ShowMessage(
                    sb.ToString(), ko.Count == 0 ? InformationType.Success : InformationType.Warning, 8000, InformationPosition.Top);
            };

            // 7) ouvrir le popup 2 (ShowViewParameters via constructeur)
            var svp = new ShowViewParameters
            {
                TargetWindow = TargetWindow.NewModalWindow,
                CreatedView = lv
            };

            var dc = Application.CreateController<DialogController>();
            dc.SaveOnAccept = false;
            dc.CancelAction.Caption = "Fermer";
            dc.Actions.Add(selectAll);
            dc.Actions.Add(unselectAll);
            dc.Actions.Add(launch);

            svp.Controllers.Add(dc);
            Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(Frame, null));
        }


    }
}
