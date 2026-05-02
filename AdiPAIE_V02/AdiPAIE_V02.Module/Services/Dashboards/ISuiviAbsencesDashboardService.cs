// =============================================================================
//  ISuiviAbsencesDashboardService.cs
//  Tableau N°5 (Suivi des Absences) — INTERNE uniquement.
// =============================================================================

using System.Collections.Generic;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <summary>Service du Tableau N°5 « Suivi des Absences ».</summary>
    public interface ISuiviAbsencesDashboardService
    {
        SuiviAbsencesDto GetData(SuiviAbsencesFilterModel filter, IObjectSpace os);

        List<int>             GetAnneesDisponibles(IObjectSpace os);
        List<Site>            GetSitesActifs(IObjectSpace os);
        List<Departement>     GetDepartements(IObjectSpace os);
        List<Categories>      GetCategories(IObjectSpace os);

        void InvalidateCache(SuiviAbsencesFilterModel filter);
    }
}
