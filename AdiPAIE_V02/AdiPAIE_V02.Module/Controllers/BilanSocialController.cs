// ============================================================
//  BilanSocialController.cs
//  AdiPAIE V02 — Déclenchement du Bilan Social annuel
//  Placé sur PeriodePaie ListView — bouton "Bilan Social..."
// ============================================================
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class BilanSocialController
        : ObjectViewController<ObjectView, PeriodePaie>
    {
        private readonly SimpleAction _action;

        public BilanSocialController()
        {
            _action = new SimpleAction(this, "BilanSocial_Generer", PredefinedCategory.Reports)
            {
                Caption = "Bilan Social…",
                ImageName = "BO_Report",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Génère le Bilan Social annuel (formulaire DTSS Sénégal) au format Word."
            };
            _action.Execute += OnExecute;
        }

        private void OnExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            // ── Récupérer l'année depuis la période sélectionnée ou popup ──
            int annee = ResolveAnnee();
            if (annee <= 0) return;

            try
            {
                var bytes = BilanSocialService.Generer(annee, ObjectSpace);
                var fileName = $"Bilan_Social_{annee}.docx";
                TelechargerDocx(bytes, fileName);
                Application.ShowViewStrategy.ShowMessage(
                    $"Bilan Social {annee} généré.",
                    InformationType.Success, 4000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }

        private int ResolveAnnee()
        {
            // Si une période est sélectionnée → on prend son année
            var periode = View.SelectedObjects?.OfType<PeriodePaie>().FirstOrDefault()
                       ?? View.CurrentObject as PeriodePaie;
            if (periode != null)
                return periode.Annee;

            // Sinon → popup saisie année
            return DemanderAnneeViaPopup();
        }

        private int DemanderAnneeViaPopup()
        {
            int anneeChoisie = 0;

            var npos = Application.CreateObjectSpace(typeof(ParametreDeclaration));
            var sel = npos.CreateObject<ParametreDeclaration>();
            sel.Annee = DateTime.Today.Year - 1;

            var dv = Application.CreateDetailView(npos, sel, true);
            dv.ViewEditMode = DevExpress.ExpressApp.Editors.ViewEditMode.Edit;
            dv.Caption = "Choisir l'année du bilan social";

            var svp = new ShowViewParameters
            {
                CreatedView = dv,
                TargetWindow = TargetWindow.NewModalWindow
            };

            var dc = Application.CreateController<DialogController>();
            dc.SaveOnAccept = false;
            dc.AcceptAction.Caption = "Générer";
            dc.AcceptAction.Execute += (_, __) => { anneeChoisie = sel.Annee; };

            svp.Controllers.Add(dc);
            Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(Frame, null));

            return anneeChoisie;
        }

        private void TelechargerDocx(byte[] bytes, string fileName)
        {
            var js = Application.ServiceProvider?.GetService<IJSRuntime>();
            if (js != null)
            {
                var b64 = Convert.ToBase64String(bytes);
                var mime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                _ = js.InvokeVoidAsync("AdiPAIE.downloadFile", fileName, mime, b64);
            }
        }
    }
}
