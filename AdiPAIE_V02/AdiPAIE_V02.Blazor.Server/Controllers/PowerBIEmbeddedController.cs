// AdiPAIE_V02.Blazor.Server/Controllers/PowerBIEmbeddedController.cs
// Contrôleur Blazor qui charge la configuration Power BI dans la vue embarquée.
// Lit l'URL du rapport depuis ParametresPaie et l'injecte dans le PowerBIReportView.
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Blazor.Server.Controllers
{
    public class PowerBIEmbeddedController : ObjectViewController<DetailView, PowerBIReportView>
    {
        protected override void OnActivated()
        {
            base.OnActivated();
            LoadPowerBIConfig();
        }

        private void LoadPowerBIConfig()
        {
            try
            {
                using var os = Application.CreateObjectSpace(typeof(ParametresPaie));
                var param = ParametresPaie.TryGet(os);
                var obj = ViewCurrentObject;

                if (param == null || !param.PowerBIActif)
                {
                    obj.PowerBIActif = false;
                    obj.ReportUrl = null;
                }
                else if (string.IsNullOrWhiteSpace(param.PowerBI_ReportUrl))
                {
                    obj.PowerBIActif = true;
                    obj.ReportUrl = null;
                }
                else
                {
                    obj.PowerBIActif = true;
                    obj.ReportUrl = param.PowerBI_ReportUrl;
                }

                View.Refresh();
            }
            catch
            {
                // Ne pas bloquer la vue
            }
        }
    }
}
