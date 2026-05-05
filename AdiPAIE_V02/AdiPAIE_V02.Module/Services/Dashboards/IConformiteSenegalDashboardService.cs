// =============================================================================
//  IConformiteSenegalDashboardService.cs — V1.2 Sprint 5
// =============================================================================

using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    public interface IConformiteSenegalDashboardService
    {
        ConformiteSenegalDto GetData(ConformiteSenegalFilterModel filter, IObjectSpace objectSpace);
    }
}
