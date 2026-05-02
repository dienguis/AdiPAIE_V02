using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Controller pour le Tableau de Bord Intérimaires.
    /// Peuple les données à l'ouverture et fournit un bouton Actualiser + Export Excel.
    /// </summary>
    public class DashboardInterimaireController : ObjectViewController<DetailView, TableauBordInterimaire>
    {
        private SimpleAction _refreshAction;
        private SimpleAction _exportExcelAction;

        public DashboardInterimaireController()
        {
            _refreshAction = new SimpleAction(this, "RefreshDashboardInterimaire", "View")
            {
                Caption = "Actualiser",
                ImageName = "Action_Refresh",
                ToolTip = "Recalculer les indicateurs intérimaires"
            };
            _refreshAction.Execute += (s, e) => Refresh();

            _exportExcelAction = new SimpleAction(this, "ExportDashboardInterimaireExcel", "View")
            {
                Caption = "Exporter Excel",
                ImageName = "Action_Export_ToXLSX",
                ToolTip = "Exporter le tableau de bord intérimaires en Excel"
            };
            _exportExcelAction.Execute += ExportExcel_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            Refresh();
        }

        private void Refresh()
        {
            var tb = ViewCurrentObject;
            if (tb == null) return;

            var xpoOs = Application.CreateObjectSpace(typeof(Interimaire));
            DashboardInterimaireService.Populate(tb, xpoOs);
            View?.Refresh();
        }

        private void ExportExcel_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var tb = ViewCurrentObject;
            if (tb == null) return;

            try
            {
                byte[] fileBytes = DashboardExportInterimaireService.Generate(tb);
                string fileName = $"TableauBord_Interimaires_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                ExcelFileGenerated?.Invoke(this, new ExcelFileGeneratedEventArgs(fileName, fileBytes));
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException($"Erreur lors de l'export Excel : {ex.Message}");
            }
        }

        public event EventHandler<ExcelFileGeneratedEventArgs> ExcelFileGenerated;
    }
}
