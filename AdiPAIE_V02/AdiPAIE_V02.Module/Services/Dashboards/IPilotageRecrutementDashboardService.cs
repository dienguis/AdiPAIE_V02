// =============================================================================
//  IPilotageRecrutementDashboardService.cs — V1.4 (mai 2026)
//  Contrat du service Dashboard N°12 Pilotage Recrutement.
// =============================================================================

using System;
using System.Collections.Generic;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    public interface IPilotageRecrutementDashboardService
    {
        PilotageRecrutementDto GetData(PilotageRecrutementFilterModel filter, IObjectSpace objectSpace);

        // Listes pour les filtres
        List<int> GetAnneesDisponibles(IObjectSpace objectSpace);
        List<Site> GetSitesActifs(IObjectSpace objectSpace);
        List<Departement> GetDepartements(IObjectSpace objectSpace);
        List<Categories> GetCategories(IObjectSpace objectSpace);

        void InvalidateCache();
    }
}
