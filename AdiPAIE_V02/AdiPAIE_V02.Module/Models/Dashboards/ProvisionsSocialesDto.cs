// =============================================================================
//  ProvisionsSocialesDto.cs — V1.2 (mai 2026)
//
//  DTO du dashboard "Provisions Sociales" (cf. MISSION_STATE 9.3).
//  Calcule les provisions IDR (Indemnité de Départ à la Retraite, barème
//  Convention Collective Interprofessionnelle Sénégal) et Congés Payés
//  acquis non pris.
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class ProvisionsSocialesDto
    {
        public KpiProvisionsDto Kpis { get; set; } = new();

        // Top 10 salariés à plus forte provision IDR
        public List<ProvisionSalarieDto> TopProvisionsIDR { get; set; } = new();

        // Détail par salarié (utilisé pour export Excel CAC)
        public List<ProvisionSalarieDto> Details { get; set; } = new();

        // Évolution mensuelle de la provision IDR sur 12 mois
        public List<EvolutionProvisionDto> EvolutionIDR { get; set; } = new();

        // Ventilation par tranche d'ancienneté
        public List<TrancheAncienneteProvisionDto> ParTrancheAnciennete { get; set; } = new();

        public DateTime DateReference { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    public sealed class KpiProvisionsDto
    {
        // IDR
        public decimal ProvisionIDRTotale { get; set; }
        public int NombreSalariesConcernes { get; set; }
        public decimal ProvisionIDRMoyenneParSalarie { get; set; }

        // Congés payés
        public decimal ProvisionCongesPayesTotale { get; set; }
        public decimal NombreJoursCongesAcquis { get; set; }

        // Total cumulé (IDR + CP)
        public decimal ProvisionTotale { get; set; }

        // Variation vs mois précédent
        public decimal VariationMois { get; set; }
        public decimal VariationMoisPct { get; set; }
    }

    public sealed class ProvisionSalarieDto
    {
        public Guid SalarieOid { get; set; }
        public string Matricule { get; set; } = "";
        public string NomComplet { get; set; } = "";
        public DateTime DateEmbauche { get; set; }
        public decimal AncienneteAnnees { get; set; }
        public decimal SalaireMensuelReference { get; set; }
        public decimal TauxIDR { get; set; }            // 0.25 / 0.30 / 0.40
        public decimal ProvisionIDR { get; set; }
        public decimal SoldeCongesAcquis { get; set; }   // jours
        public decimal ProvisionCongesPayes { get; set; }
        public decimal ProvisionTotale => ProvisionIDR + ProvisionCongesPayes;
    }

    public sealed class EvolutionProvisionDto
    {
        public int Annee { get; set; }
        public int Mois { get; set; }
        public string Libelle { get; set; } = "";   // "Jan 2026"
        public decimal ProvisionIDR { get; set; }
        public decimal ProvisionCongesPayes { get; set; }
    }

    public sealed class TrancheAncienneteProvisionDto
    {
        public string Tranche { get; set; } = "";   // "< 5 ans" / "5-10 ans" / "> 10 ans"
        public int NombreSalaries { get; set; }
        public decimal TauxApplique { get; set; }   // 0.25 / 0.30 / 0.40
        public decimal ProvisionIDR { get; set; }
        public decimal ProvisionMoyenne { get; set; }
    }

    public sealed class ProvisionsSocialesFilterModel
    {
        /// <summary>Date de calcul (défaut = aujourd'hui).</summary>
        public DateTime DateReference { get; set; } = DateTime.Today;

        /// <summary>Filtre site (null = tous).</summary>
        public Guid? SiteOid { get; set; }

        public string ToCacheKey() =>
            $"provisions-sociales|d={DateReference:yyyyMMdd}|s={SiteOid}";
    }
}
