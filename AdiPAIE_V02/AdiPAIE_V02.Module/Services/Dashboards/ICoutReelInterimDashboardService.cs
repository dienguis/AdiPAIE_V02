// =============================================================================
//  ICoutReelInterimDashboardService.cs — V1.3 Sprint 1
// =============================================================================

using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    public interface ICoutReelInterimDashboardService
    {
        CoutReelInterimDto GetData(CoutReelInterimFilterModel filter, IObjectSpace objectSpace);
    }
}
