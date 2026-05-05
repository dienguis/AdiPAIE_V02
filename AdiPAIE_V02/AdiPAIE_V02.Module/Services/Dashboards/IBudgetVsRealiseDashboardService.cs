// =============================================================================
//  IBudgetVsRealiseDashboardService.cs — V1.2 (mai 2026)
// =============================================================================

using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    public interface IBudgetVsRealiseDashboardService
    {
        BudgetVsRealiseDto GetData(BudgetVsRealiseFilterModel filter, IObjectSpace objectSpace);
    }
}
