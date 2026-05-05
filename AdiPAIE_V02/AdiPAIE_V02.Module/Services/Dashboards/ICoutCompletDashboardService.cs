// =============================================================================
//  ICoutCompletDashboardService.cs — V1.2 Sprint 4
// =============================================================================

using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    public interface ICoutCompletDashboardService
    {
        CoutCompletDto GetData(CoutCompletFilterModel filter, IObjectSpace objectSpace);
    }
}
