using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Controllers;
using DevExpress.ExpressApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;

namespace AdiPAIE_V02.Blazor.Server.Controllers
{
    /// <summary>
    /// Contrôleur Blazor pour le téléchargement Excel du dashboard Effectifs RH.
    /// </summary>
    public class DashboardExportEffectifController : ObjectViewController<DetailView, TableauBordEffectif>
    {
        protected override void OnActivated()
        {
            base.OnActivated();
            var ctrl = Frame.GetController<DashboardEffectifController>();
            if (ctrl != null) ctrl.ExcelFileGenerated += OnExcelFileGenerated;
        }

        protected override void OnDeactivated()
        {
            var ctrl = Frame.GetController<DashboardEffectifController>();
            if (ctrl != null) ctrl.ExcelFileGenerated -= OnExcelFileGenerated;
            base.OnDeactivated();
        }

        private void OnExcelFileGenerated(object sender, ExcelFileGeneratedEventArgs e)
            => DownloadFile(e.FileName, e.FileBytes);

        private void DownloadFile(string fileName, byte[] bytes)
        {
            try
            {
                var js = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (js != null)
                {
                    _ = js.InvokeVoidAsync("AdiPAIE.downloadFile", fileName,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        Convert.ToBase64String(bytes)).AsTask();
                    Application.ShowViewStrategy.ShowMessage(
                        $"Export Excel généré : {fileName}",
                        InformationType.Success, 4000, InformationPosition.Top);
                }
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur téléchargement : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }
    }

    /// <summary>
    /// Contrôleur Blazor pour le téléchargement Excel du dashboard Intérimaires.
    /// </summary>
    public class DashboardExportInterimaireController : ObjectViewController<DetailView, TableauBordInterimaire>
    {
        protected override void OnActivated()
        {
            base.OnActivated();
            var ctrl = Frame.GetController<DashboardInterimaireController>();
            if (ctrl != null) ctrl.ExcelFileGenerated += OnExcelFileGenerated;
        }

        protected override void OnDeactivated()
        {
            var ctrl = Frame.GetController<DashboardInterimaireController>();
            if (ctrl != null) ctrl.ExcelFileGenerated -= OnExcelFileGenerated;
            base.OnDeactivated();
        }

        private void OnExcelFileGenerated(object sender, ExcelFileGeneratedEventArgs e)
        {
            try
            {
                var js = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (js != null)
                {
                    _ = js.InvokeVoidAsync("AdiPAIE.downloadFile", e.FileName,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        Convert.ToBase64String(e.FileBytes)).AsTask();
                    Application.ShowViewStrategy.ShowMessage(
                        $"Export Excel généré : {e.FileName}",
                        InformationType.Success, 4000, InformationPosition.Top);
                }
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur téléchargement : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }
    }
}
