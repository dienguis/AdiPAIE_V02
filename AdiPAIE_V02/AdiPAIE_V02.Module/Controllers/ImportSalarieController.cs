// AdiPAIE_V02.Module/Controllers/ImportSalarieController.cs
// Import en masse des salariés depuis un fichier Excel (.xlsx)
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;
using System.IO;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Controller placé sur la ListView des Salariés.
    ///
    /// Deux actions :
    ///   1. « Importer depuis Excel » — ouvre un popup avec FileData,
    ///      lit le fichier et appelle ImportSalarieService.Importer()
    ///   2. « Télécharger modèle » — génère le fichier modèle vide
    ///      et le télécharge via JSInterop
    /// </summary>
    public class ImportSalarieController
        : ObjectViewController<ListView, Salarie>
    {
        private readonly PopupWindowShowAction importerAction;
        private readonly SimpleAction modeleAction;

        public ImportSalarieController()
        {
            // ── Importer depuis Excel ────────────────────────────────
            importerAction = new PopupWindowShowAction(this,
                "Salarie_ImporterExcel", PredefinedCategory.Edit)
            {
                Caption = "Importer depuis Excel",
                ImageName = "Action_Export",
                ToolTip = "Importe des salariés en masse depuis un fichier Excel (.xlsx).",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            importerAction.CustomizePopupWindowParams += OnCustomizeImportPopup;
            importerAction.Execute += OnExecuteImport;

            // ── Télécharger modèle ───────────────────────────────────
            modeleAction = new SimpleAction(this,
                "Salarie_TelechargerModele", PredefinedCategory.Edit)
            {
                Caption = "Télécharger modèle import",
                ImageName = "Action_Download",
                ToolTip = "Télécharge un fichier Excel modèle avec les colonnes attendues et un exemple.",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            modeleAction.Execute += OnTelechargerModele;
        }

        // ── Popup d'import ───────────────────────────────────────────
        private void OnCustomizeImportPopup(object sender,
            CustomizePopupWindowParamsEventArgs e)
        {
            // Utiliser un ObjectSpace XPO pour pouvoir créer un FileData
            var os = Application.CreateObjectSpace(typeof(FileData));
            var xpOs = (DevExpress.ExpressApp.Xpo.XPObjectSpace)os;
            var param = new ImportSalarieParam(xpOs.Session);

            e.View = Application.CreateDetailView(os, param);
            e.View.Caption = "Importer des salariés depuis Excel";
            e.DialogController.SaveOnAccept = false;
            e.DialogController.AcceptAction.Caption = "Importer";
            e.DialogController.CancelAction.Caption = "Annuler";
        }

        private void OnExecuteImport(object sender,
            PopupWindowShowActionExecuteEventArgs e)
        {
            var param = e.PopupWindowViewCurrentObject as ImportSalarieParam;
            if (param?.FichierExcel == null || param.FichierExcel.Size == 0)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    "Veuillez sélectionner un fichier Excel (.xlsx).",
                    InformationType.Warning, 4000, InformationPosition.Top);
                return;
            }

            try
            {
                // Lire les bytes du fichier uploadé
                using var ms = new MemoryStream();
                param.FichierExcel.SaveToStream(ms);
                byte[] xlsxBytes = ms.ToArray();

                // Appeler le service d'import
                var result = ImportSalarieService.Importer(ObjectSpace, xlsxBytes);

                // Rafraîchir la ListView
                if (result.Crees > 0)
                    View.Refresh();

                // Audit
                AuditService.Enregistrer(Application,
                    "Salarie", "Import en masse",
                    "-", $"{result.Crees} salarié(s) créé(s)",
                    $"{result.Resume}\n"
                    + (result.Details.Count > 0
                        ? string.Join("\n", result.Details)
                        : "Aucun détail."));

                // Message de résultat
                var infoType = result.Erreurs > 0
                    ? InformationType.Warning
                    : InformationType.Success;

                Application.ShowViewStrategy?.ShowMessage(
                    result.Resume, infoType, 6000, InformationPosition.Top);
            }
            catch (UserFriendlyException ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    ex.Message, InformationType.Warning, 6000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur lors de l'import : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }

        // ── Télécharger modèle ───────────────────────────────────────
        private async void OnTelechargerModele(object sender,
            SimpleActionExecuteEventArgs e)
        {
            try
            {
                byte[] xlsxBytes = ImportSalarieService.GenererModele();
                string fileName = "Modele_Import_Salaries.xlsx";
                string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                string base64 = Convert.ToBase64String(xlsxBytes);

                var jsRuntime = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (jsRuntime != null)
                    await jsRuntime.InvokeVoidAsync(
                        "AdiPAIE.downloadFile", fileName, mimeType, base64);

                Application.ShowViewStrategy?.ShowMessage(
                    "Modèle Excel téléchargé.",
                    InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur : {ex.Message}",
                    InformationType.Error, 5000, InformationPosition.Top);
            }
        }
    }

    // ── Paramètre non-persistant pour le popup d'import ─────────────────
    /// <summary>
    /// Objet temporaire affiché dans le popup d'import.
    /// Contient uniquement un FileData pour le fichier Excel à importer.
    /// </summary>
    [NonPersistent]
    [XafDisplayName("Import de salariés")]
    public class ImportSalarieParam : BaseObject
    {
        public ImportSalarieParam(Session session) : base(session) { }

        [DevExpress.Xpo.Aggregated]
        [ExpandObjectMembers(ExpandObjectMembers.Never)]
        [XafDisplayName("Fichier Excel (.xlsx)")]
        public FileData FichierExcel
        {
            get => fichierExcel;
            set => SetPropertyValue(nameof(FichierExcel), ref fichierExcel, value);
        }
        FileData fichierExcel;
    }
}
