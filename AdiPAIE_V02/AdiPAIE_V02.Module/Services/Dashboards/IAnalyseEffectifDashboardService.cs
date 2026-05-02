// =============================================================================
//  IAnalyseEffectifDashboardService.cs
//  Tableau N°2 (Analyse de l'Effectif) — interface du service métier.
// =============================================================================

using System.Collections.Generic;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <summary>
    /// Service du Tableau N°2 « Analyse de l'Effectif ».
    /// Périmètre : INTERNE (Salarie) ou EXTERNE (Interimaire) — défini par
    /// <see cref="AnalyseEffectifFilterModel.Personnel"/>.
    /// </summary>
    public interface IAnalyseEffectifDashboardService
    {
        AnalyseEffectifDto GetData(AnalyseEffectifFilterModel filter, IObjectSpace os);

        List<int>              GetAnneesDisponibles(IObjectSpace os);
        List<Site>             GetSitesActifs(IObjectSpace os);             // INTERNE : Siège / Dépôts
        List<StationService>   GetStationsServiceActives(IObjectSpace os);  // EXTERNE : stations service ELTON
        List<Departement>      GetDepartements(IObjectSpace os);            // INTERNE : segment
        List<Categories>       GetCategories(IObjectSpace os);              // INTERNE : catégorie pro
        List<SocieteInterim>   GetSocietesInterim(IObjectSpace os);         // EXTERNE : société d'intérim
        List<PosteInterimaire> GetPostesInterimaire(IObjectSpace os);       // EXTERNE : poste

        void InvalidateCache(AnalyseEffectifFilterModel filter);
    }
}
