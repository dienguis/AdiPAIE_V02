// =============================================================================
//  CoutCompletDto.cs — V1.2 Sprint 4 (mai 2026)
//
//  Dashboard "Coût Complet par Salarié" (Fully Loaded Cost).
//  Décomposition : Net + Cotisations salariales + Charges patronales +
//                  Avantages en nature + Formation
//  Cf. MISSION_STATE 9.4
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class CoutCompletDto
    {
        public KpiCoutCompletDto Kpis { get; set; } = new();

        // Détail par salarié (utilisé pour Top 10, Bottom 10 et Excel export)
        public List<CoutSalarieDto> Salaries { get; set; } = new();

        // Décomposition du coût total annuel (donut)
        public List<DecompositionCoutDto> Decomposition { get; set; } = new();

        // Coût moyen par catégorie pro
        public List<CoutCategorieDto> ParCategorie { get; set; } = new();

        // Coût moyen par site
        public List<CoutSiteDto> ParSite { get; set; } = new();

        public int Annee { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    public sealed class KpiCoutCompletDto
    {
        public decimal CoutTotalAnnuel { get; set; }
        public decimal CoutMoyenSalarie { get; set; }
        public decimal SalaireBrutTotal { get; set; }
        public decimal ChargesPatronalesTotal { get; set; }
        public decimal AvantagesNatureTotal { get; set; }
        public decimal FormationTotal { get; set; }
        public int NbSalaries { get; set; }

        /// <summary>Multiplicateur magique : Coût Total / Salaire Net.</summary>
        public decimal MultiplicateurNetVersTotal { get; set; }

        /// <summary>Charges patronales en % du brut.</summary>
        public decimal TauxChargesPatronalesPct { get; set; }
    }

    public sealed class CoutSalarieDto
    {
        public Guid SalarieOid { get; set; }
        public string Matricule { get; set; } = "";
        public string NomComplet { get; set; } = "";
        public string Categorie { get; set; } = "";
        public string Site { get; set; } = "";
        public decimal SalaireBrutAnnuel { get; set; }
        public decimal SalaireNetAnnuel { get; set; }
        public decimal CotisationsSalariales { get; set; }
        public decimal ChargesPatronales { get; set; }
        public decimal AvantagesNature { get; set; }
        public decimal Formation { get; set; }
        public decimal CoutTotalAnnuel =>
            SalaireBrutAnnuel + ChargesPatronales + AvantagesNature + Formation;
        public decimal MultiplicateurNet =>
            SalaireNetAnnuel > 0 ? Math.Round(CoutTotalAnnuel / SalaireNetAnnuel, 2) : 0m;
    }

    public sealed class DecompositionCoutDto
    {
        public string Composante { get; set; } = "";   // "Salaire net", "Cotisations sal.", ...
        public decimal Montant { get; set; }
        public decimal PourcentageDuTotal { get; set; }
        public string CouleurHex { get; set; } = "";
    }

    public sealed class CoutCategorieDto
    {
        public string Categorie { get; set; } = "";
        public int NbSalaries { get; set; }
        public decimal CoutTotal { get; set; }
        public decimal CoutMoyen { get; set; }
        public decimal SalaireBrutMoyen { get; set; }
    }

    public sealed class CoutSiteDto
    {
        public Guid? SiteOid { get; set; }
        public string SiteNom { get; set; } = "";
        public int NbSalaries { get; set; }
        public decimal CoutTotal { get; set; }
        public decimal CoutMoyen { get; set; }
    }

    public sealed class CoutCompletFilterModel
    {
        public int Annee { get; set; } = DateTime.Today.Year;
        public Guid? SiteOid { get; set; }
        public Guid? CategorieOid { get; set; }

        public string ToCacheKey() =>
            $"cout-complet|y={Annee}|s={SiteOid}|c={CategorieOid}";
    }
}
