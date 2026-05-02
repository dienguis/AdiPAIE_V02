using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Controller qui peuple automatiquement le TableauBordEffectif
    /// quand sa DetailView est ouverte, et permet l'export Excel.
    /// </summary>
    public class DashboardEffectifController : ObjectViewController<DetailView, TableauBordEffectif>
    {
        private SimpleAction _refreshAction;
        private SimpleAction _exportExcelAction;

        public DashboardEffectifController()
        {
            _refreshAction = new SimpleAction(this, "RefreshDashboardEffectif", "View")
            {
                Caption = "Actualiser",
                ImageName = "Action_Refresh",
                ToolTip = "Recalculer les indicateurs"
            };
            _refreshAction.Execute += RefreshAction_Execute;

            _exportExcelAction = new SimpleAction(this, "ExportDashboardExcel", "View")
            {
                Caption = "Exporter Excel",
                ImageName = "Action_Export_ToXLSX",
                ToolTip = "Exporter le tableau de bord en fichier Excel"
            };
            _exportExcelAction.Execute += ExportExcelAction_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            Refresh();
        }

        private void RefreshAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            Refresh();
        }

        private void ExportExcelAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var tb = ViewCurrentObject;
            if (tb == null) return;

            try
            {
                byte[] fileBytes = DashboardExportExcelService.Generate(tb);
                string fileName = $"TableauBord_Effectifs_RH_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

                // Déclencher l'événement pour que le contrôleur Blazor puisse gérer le téléchargement
                ExcelFileGenerated?.Invoke(this, new ExcelFileGeneratedEventArgs(fileName, fileBytes));
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException($"Erreur lors de l'export Excel : {ex.Message}");
            }
        }

        private void Refresh()
        {
            var tb = ViewCurrentObject;
            if (tb == null) return;

            var xpoOs = Application.CreateObjectSpace(typeof(Salarie));
            DashboardEffectifService.Populate(tb, xpoOs);

            View?.Refresh();
        }

        /// <summary>
        /// Événement déclenché quand le fichier Excel est prêt à être téléchargé.
        /// Le contrôleur Blazor s'abonne à cet événement.
        /// </summary>
        public event EventHandler<ExcelFileGeneratedEventArgs> ExcelFileGenerated;
    }

    public class ExcelFileGeneratedEventArgs : EventArgs
    {
        public string FileName { get; }
        public byte[] FileBytes { get; }

        public ExcelFileGeneratedEventArgs(string fileName, byte[] fileBytes)
        {
            FileName = fileName;
            FileBytes = fileBytes;
        }
    }
}
