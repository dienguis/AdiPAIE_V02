// =============================================================================
//  IRemunerationDashboardService.cs
//  Tableau N°4 (Rémunération — Égalité des salaires).
// =============================================================================

using System.Collections.Generic;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <summary>Service du Tableau N°4 « Rémunération ».</summary>
    public interface IRemunerationDashboardService
    {
        RemunerationDto GetData(RemunerationFilterModel filter, IObjectSpace os);

        List<int>             GetAnneesDisponibles(IObjectSpace os);
        List<Site>            GetSitesActifs(IObjectSpace os);
        List<StationService>  GetStationsServiceActives(IObjectSpace os);
        List<Departement>     GetDepartements(IObjectSpace os);
        List<Categories>      GetCategories(IObjectSpace os);
        List<PosteInterimaire> GetPostesInterimaire(IObjectSpace os);

        /// <summary>
        /// V1.1 — retourne les unités organisationnelles d'un site donné
        /// (BU pour Stations, Départements pour Siège, etc.). Si siteOid
        /// est null, retourne TOUTES les unités actives (toutes confondues).
        /// </summary>
        List<UniteOrganisationnelle> GetUnitesPourSite(IObjectSpace os, System.Guid? siteOid);

        void InvalidateCache(RemunerationFilterModel filter);
    }
}
