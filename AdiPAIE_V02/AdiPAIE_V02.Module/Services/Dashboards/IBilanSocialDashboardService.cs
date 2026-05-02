// =============================================================================
//  IBilanSocialDashboardService.cs
//  Tableau N°6 (Bilan Social Mensuel) — INTERNE.
// =============================================================================

using System.Collections.Generic;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <summary>Service du Tableau N°6 « Bilan Social Mensuel ».</summary>
    public interface IBilanSocialDashboardService
    {
        BilanSocialDto GetData(BilanSocialFilterModel filter, IObjectSpace os);

        List<int>  GetAnneesDisponibles(IObjectSpace os);
        List<Site> GetSitesActifs(IObjectSpace os);

        void InvalidateCache(BilanSocialFilterModel filter);
    }
}
