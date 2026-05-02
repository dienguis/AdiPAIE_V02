// ============================================================
//  BilanSocialController.cs
//  AdiPAIE V02 — Bilan Social annuel (DTSS Sénégal)
//  Bouton accessible depuis Périodes de paie.
//
//  Workflow en 2 étapes (wizard) :
//    1) Popup "Choix de l'année"          (BilanSocialAnneeSelection)
//    2) Popup "Vérifier et compléter"     (BilanSocialFormulaire)
//
//  Une fois l'année choisie, les données sont chargées et figées.
//  L'année n'est plus modifiable dans le formulaire principal.
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
                ToolTip = "Génère le Bilan Social annuel (formulaire DTSS Sénégal) au format Word.",
                // Pas besoin de sélection — le bilan est annuel
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _action.Execute += OnExecute;
        }

        // ──────────────────────────────────────────────────────────────
        //  ÉTAPE 1 — Popup de sélection de l'année
        // ──────────────────────────────────────────────────────────────
        private void OnExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            // Année par défaut = N-1 (l'année courante n'est pas close)
            int anneeDefaut = DateTime.Today.Year - 1;

            var selOs = Application.CreateObjectSpace(typeof(BilanSocialAnneeSelection));
            var selection = selOs.CreateObject<BilanSocialAnneeSelection>();
            selection.Annee = anneeDefaut;

            var selDv = Application.CreateDetailView(selOs, selection, true);
            selDv.ViewEditMode = DevExpress.ExpressApp.Editors.ViewEditMode.Edit;
            selDv.Caption = "Bilan Social — Choix de l'année";

            var svp = new ShowViewParameters
            {
                CreatedView = selDv,
                TargetWindow = TargetWindow.NewModalWindow
            };

            var dc = Application.CreateController<DialogController>();
            dc.SaveOnAccept = false;
            dc.AcceptAction.Caption = "Charger les données…";
            dc.AcceptAction.ImageName = "BO_Report";
            dc.AcceptAction.Execute += (_, __) =>
            {
                int anneeChoisie = selection.Annee;
                OuvrirFormulairePopup(anneeChoisie);
            };

            svp.Controllers.Add(dc);
            Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(Frame, null));
        }

        // ──────────────────────────────────────────────────────────────
        //  ÉTAPE 2 — Popup formulaire pré-rempli (année figée)
        // ──────────────────────────────────────────────────────────────
        private void OuvrirFormulairePopup(int annee)
        {
            // ── Créer le formulaire et pré-remplir ──────────────────────
            var formOs = Application.CreateObjectSpace(typeof(BilanSocialFormulaire));
            var formulaire = formOs.CreateObject<BilanSocialFormulaire>();
            formulaire.Annee = annee;

            try
            {
                BilanSocialService.PreRemplir(formulaire, ObjectSpace);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Pré-remplissage partiel : {ex.Message}",
                    InformationType.Warning, 5000, InformationPosition.Top);
            }

            // ── Ouvrir le formulaire en popup ───────────────────────────
            var dv = Application.CreateDetailView(formOs, formulaire, true);
            dv.ViewEditMode = DevExpress.ExpressApp.Editors.ViewEditMode.Edit;
            dv.Caption = $"Bilan Social {annee} — Vérifier et compléter";

            var svp = new ShowViewParameters
            {
                CreatedView = dv,
                TargetWindow = TargetWindow.NewModalWindow
            };

            var dc = Application.CreateController<DialogController>();
            dc.SaveOnAccept = false;
            dc.AcceptAction.Caption = "Générer le document";
            dc.AcceptAction.ImageName = "BO_Report";
            dc.AcceptAction.Execute += (_, __) =>
            {
                try
                {
                    var bytes = BilanSocialService.Generer(formulaire, ObjectSpace);
                    if (bytes == null || bytes.Length == 0)
                        throw new UserFriendlyException("Le document généré est vide.");

                    var fileName = $"Bilan_Social_{formulaire.Annee}.docx";

                    // Téléchargement via JSInterop
                    var js = Application.ServiceProvider?.GetService<IJSRuntime>();
                    if (js == null)
                        throw new UserFriendlyException(
                            "Téléchargement indisponible dans ce contexte.");

                    var b64 = Convert.ToBase64String(bytes);
                    var mime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    _ = js.InvokeVoidAsync("AdiPAIE.downloadFile", fileName, mime, b64)
                        .AsTask();

                    // Audit
                    AuditService.Enregistrer(Application, "BilanSocial", "Generer",
                        formulaire.Annee.ToString(), $"Bilan Social {formulaire.Annee}",
                        $"Généré pour {formulaire.RaisonSociale}");

                    Application.ShowViewStrategy?.ShowMessage(
                        $"Bilan Social {formulaire.Annee} généré — téléchargement en cours.",
                        InformationType.Success, 4000, InformationPosition.Top);
                }
                catch (UserFriendlyException) { throw; }
                catch (Exception ex)
                {
                    throw new UserFriendlyException($"Erreur génération : {ex.Message}");
                }
            };

            svp.Controllers.Add(dc);
            Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(Frame, null));
        }
    }
}
