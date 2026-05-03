// AdiPAIE_V02.Module/Controllers/ImportConjointController.cs
// Import en masse des conjoints depuis un fichier Excel (.xlsx).
// Boutons placés sur la liste des Salariés (Conjoint étant aggregated sur Salarie).
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Templates;
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
    /// Controller placé sur le DetailView du Centre d'imports.
    ///
    /// Deux actions (catégorie "ImportsHub") :
    ///   1. « Importer Conjoints » — popup avec FileData + ImportConjointService.Importer()
    ///   2. « Modèle Conjoints »   — télécharge un Excel modèle vide
    /// </summary>
    public class ImportConjointController
        : ObjectViewController<DetailView, CentreImports>
    {
        private readonly PopupWindowShowAction importerAction;
        private readonly SimpleAction modeleAction;

        public ImportConjointController()
        {
            importerAction = new PopupWindowShowAction(this,
                "Conjoint_ImporterExcel", PredefinedCategory.Edit)
            {
                Caption = "Importer Conjoints",
                ImageName = "BO_Person",
                ToolTip = "Importe des conjoints en masse depuis un fichier Excel (.xlsx).",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            importerAction.CustomizePopupWindowParams += OnCustomizeImportPopup;
            importerAction.Execute += OnExecuteImport;

            modeleAction = new SimpleAction(this,
                "Conjoint_TelechargerModele", PredefinedCategory.Edit)
            {
                Caption = "Modèle Conjoints",
                ImageName = "Action_Download",
                ToolTip = "Télécharge un Excel modèle pour l'import des conjoints.",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            modeleAction.Execute += OnTelechargerModele;
        }

        private void OnCustomizeImportPopup(object sender,
            CustomizePopupWindowParamsEventArgs e)
        {
            var os = Application.CreateObjectSpace(typeof(FileData));
            var xpOs = (DevExpress.ExpressApp.Xpo.XPObjectSpace)os;
            var param = new ImportConjointParam(xpOs.Session);

            e.View = Application.CreateDetailView(os, param);
            e.View.Caption = "Importer conjoints";
            e.DialogController.SaveOnAccept = false;
            e.DialogController.AcceptAction.Caption = "Importer";
            e.DialogController.CancelAction.Caption = "Annuler";
        }

        private void OnExecuteImport(object sender,
            PopupWindowShowActionExecuteEventArgs e)
        {
            var param = e.PopupWindowViewCurrentObject as ImportConjointParam;
            if (param?.FichierExcel == null || param.FichierExcel.Size == 0)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    "Veuillez sélectionner un fichier Excel (.xlsx).",
                    InformationType.Warning, 4000, InformationPosition.Top);
                return;
            }

            try
            {
                using var ms = new MemoryStream();
                param.FichierExcel.SaveToStream(ms);
                byte[] xlsxBytes = ms.ToArray();

                var result = ImportConjointService.Importer(ObjectSpace, xlsxBytes);

                if (result.Crees > 0)
                    View.Refresh();

                AuditService.Enregistrer(Application,
                    "Conjoint", "Import en masse",
                    "-", $"{result.Crees} conjoint(s) créé(s)",
                    $"{result.Resume}\n"
                    + (result.Details.Count > 0
                        ? string.Join("\n", result.Details)
                        : "Aucun détail."));

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

        private async void OnTelechargerModele(object sender,
            SimpleActionExecuteEventArgs e)
        {
            try
            {
                byte[] xlsxBytes = ImportConjointService.GenererModele();
                string fileName = "Modele_Import_Conjoints.xlsx";
                string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                string base64 = Convert.ToBase64String(xlsxBytes);

                var jsRuntime = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (jsRuntime != null)
                    await jsRuntime.InvokeVoidAsync(
                        "AdiPAIE.downloadFile", fileName, mimeType, base64);

                Application.ShowViewStrategy?.ShowMessage(
                    "Modèle Conjoints téléchargé.",
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

    [NonPersistent]
    [XafDisplayName("Import de conjoints")]
    public class ImportConjointParam : BaseObject
    {
        public ImportConjointParam(Session session) : base(session) { }

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
