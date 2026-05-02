// =============================================================================
//  IMouvementsDashboardService.cs
//  Tableau N°3 (Mouvements — Arrivées / Départs).
// =============================================================================

using System.Collections.Generic;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <summary>Service du Tableau N°3 « Mouvements (Arrivées / Départs) ».</summary>
    public interface IMouvementsDashboardService
    {
        MouvementsDto GetData(MouvementsFilterModel filter, IObjectSpace os);

        List<int>             GetAnneesDisponibles(IObjectSpace os);
        List<Site>            GetSitesActifs(IObjectSpace os);
        List<StationService>  GetStationsServiceActives(IObjectSpace os);
        List<Departement>     GetDepartements(IObjectSpace os);
        List<Categories>      GetCategories(IObjectSpace os);

        void InvalidateCache(MouvementsFilterModel filter);
    }
}
