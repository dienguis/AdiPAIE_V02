// =============================================================================
//  BudgetVsRealiseDto.cs — V1.2 (mai 2026)
//
//  DTO du dashboard "Budget vs Réalisé Masse Salariale" (cf. MISSION_STATE 9.2).
//  Compare le budget mensuel (entité BudgetMasseSalariale) avec le réalisé
//  calculé depuis Bulletin.BrutFiscal + BulletinLigne.MontantEmployeur.
// =============================================================================

using System;
using System.Collections.Generic;
using AdiPAIE_V02.Module.BusinessObjects.Budget;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class BudgetVsRealiseDto
    {
        // KPIs synthèse
        public KpiBudgetDto Kpis { get; set; } = new();

        // 12 lignes mensuelles (1..12) avec budget / réalisé / écart
        public List<MoisBudgetDto> Mensuel { get; set; } = new();

        // Cumul YTD glissant (12 points : YTD au 31/jan, 28/fev, …)
        public List<CumulYtdDto> CumulYtd { get; set; } = new();

        // Détail par rubrique × écart (top dépassements / sous-conso)
        public List<RubriqueEcartDto> ParRubrique { get; set; } = new();

        // Détail par site (si ventilation site disponible dans le budget)
        public List<SiteEcartDto> ParSite { get; set; } = new();

        // Métadonnées
        public int Annee { get; set; }
        public int? MoisCourant { get; set; }   // mois jusqu'auquel le réalisé existe
        public DateTime CalculatedAt { get; set; }
    }

    public sealed class KpiBudgetDto
    {
        // Annuel
        public decimal BudgetAnnuel { get; set; }
        public decimal ProjectionAnnuelle { get; set; }   // YTD réalisé + budget restant
        public decimal EcartProjection { get; set; }       // ProjectionAnnuelle - BudgetAnnuel
        public decimal EcartProjectionPct { get; set; }    // % du budget annuel

        // YTD
        public decimal BudgetYtd { get; set; }
        public decimal RealiseYtd { get; set; }
        public decimal EcartYtd { get; set; }
        public decimal EcartYtdPct { get; set; }

        // Mois courant
        public decimal BudgetMois { get; set; }
        public decimal RealiseMois { get; set; }
        public decimal EcartMois { get; set; }
        public decimal EcartMoisPct { get; set; }

        // Alertes
        public int NombreRubriquesEnDepassement { get; set; }
        public int NombreSitesEnDepassement { get; set; }
    }

    /// <summary>
    /// Une ligne mensuelle (mois 1..12) avec budget / réalisé / écart.
    /// </summary>
    public sealed class MoisBudgetDto
    {
        public int Mois { get; set; }
        public string MoisLibelle { get; set; } = "";   // "Janvier", "Février"...
        public decimal Budget { get; set; }
        public decimal Realise { get; set; }
        public decimal Ecart => Realise - Budget;
        public decimal EcartPct => Budget > 0 ? Ecart * 100m / Budget : 0m;
        public bool RealiseDisponible { get; set; }     // false pour mois futurs
    }

    /// <summary>
    /// Un point de la courbe cumul YTD (cumul au 30/M de chaque mois).
    /// </summary>
    public sealed class CumulYtdDto
    {
        public int Mois { get; set; }
        public string MoisLibelle { get; set; } = "";
        public decimal BudgetCumule { get; set; }
        public decimal RealiseCumule { get; set; }
        public decimal EcartCumule => RealiseCumule - BudgetCumule;
    }

    /// <summary>
    /// Écart par rubrique sur l'année (toutes rubriques).
    /// </summary>
    public sealed class RubriqueEcartDto
    {
        public BudgetRubrique Rubrique { get; set; }
        public string RubriqueLibelle { get; set; } = "";
        public decimal Budget { get; set; }
        public decimal Realise { get; set; }
        public decimal Ecart => Realise - Budget;
        public decimal EcartPct => Budget > 0 ? Ecart * 100m / Budget : 0m;
    }

    /// <summary>
    /// Écart par site sur l'année.
    /// </summary>
    public sealed class SiteEcartDto
    {
        public Guid? SiteOid { get; set; }
        public string SiteNom { get; set; } = "";
        public decimal Budget { get; set; }
        public decimal Realise { get; set; }
        public decimal Ecart => Realise - Budget;
        public decimal EcartPct => Budget > 0 ? Ecart * 100m / Budget : 0m;
    }
}
