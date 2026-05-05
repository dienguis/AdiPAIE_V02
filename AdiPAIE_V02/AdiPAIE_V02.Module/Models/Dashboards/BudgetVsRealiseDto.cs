// =============================================================================
//  BudgetVsRealiseDto.cs — V1.2.1 (mai 2026)
//
//  REFONTE annuelle :
//    - Plus de granularité mensuelle ni rubriques.
//    - On compare le BRUT annuel saisi (entité BudgetMasseSalariale) avec
//      le RÉALISÉ = SUM(Bulletin.BrutFiscal) sur l'année.
//    - Évolution sur 5 années glissantes (N-4 → N) pour comparaison historique.
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class BudgetVsRealiseDto
    {
        // KPIs synthèse de l'année cible
        public KpiBudgetDto Kpis { get; set; } = new();

        // Évolution sur 5 années glissantes (Budget vs Réalisé annuel)
        public List<EvolutionAnnuelleDto> Evolution { get; set; } = new();

        // Détail par site pour l'année cible
        public List<SiteEcartDto> ParSite { get; set; } = new();

        // Métadonnées
        public int Annee { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    public sealed class KpiBudgetDto
    {
        public decimal BudgetBrutAnnuel { get; set; }
        public decimal RealiseBrutAnnuel { get; set; }
        public decimal Ecart { get; set; }
        public decimal EcartPct { get; set; }

        // Indicateurs informatifs
        public int NbSitesBudgetes { get; set; }
        public int NbSitesEnDepassement { get; set; }
    }

    /// <summary>
    /// Une ligne par année dans la vue 5 ans glissants.
    /// </summary>
    public sealed class EvolutionAnnuelleDto
    {
        public int Annee { get; set; }
        public decimal Budget { get; set; }
        public decimal Realise { get; set; }
        public decimal Ecart => Realise - Budget;
        public decimal EcartPct => Budget > 0 ? Math.Round(Ecart * 100m / Budget, 2) : 0m;
    }

    /// <summary>
    /// Écart par site sur l'année cible.
    /// </summary>
    public sealed class SiteEcartDto
    {
        public Guid? SiteOid { get; set; }
        public string SiteNom { get; set; } = "";
        public decimal Budget { get; set; }
        public decimal Realise { get; set; }
        public decimal Ecart => Realise - Budget;
        public decimal EcartPct => Budget > 0 ? Math.Round(Ecart * 100m / Budget, 2) : 0m;
    }
}
