// =============================================================================
//  AnalyseEffectifDto.cs
//  Tableau N°2 (Analyse de l'Effectif) — DTOs renvoyés par le service.
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class AnalyseEffectifDto
    {
        public KpiAnalyseEffectifDto Kpis { get; set; } = new();

        /// <summary>Évolution effectif sur 8 années glissantes (du plus ancien au plus récent).</summary>
        public List<EvolutionAnneeDto> EvolutionHuitAns { get; set; } = new();

        /// <summary>Distribution par tranche d'âge (4 tranches + vide).</summary>
        public List<BarItemDto> ParTrancheAge { get; set; } = new();

        /// <summary>Distribution par tranche d'ancienneté (5 tranches + vide).</summary>
        public List<BarItemDto> ParAnciennete { get; set; } = new();

        /// <summary>Distribution par segment (Département pour INTERNE / BU pour EXTERNE).</summary>
        public List<BarItemDto> ParSegment { get; set; } = new();

        /// <summary>Top 5 catégories professionnelles.</summary>
        public List<BarItemDto> ParCategorieTop5 { get; set; } = new();

        /// <summary>Distribution par type de contrat (CDI/CDD/Stage pour INTERNE, INTERIM pour EXTERNE).</summary>
        public List<BarItemDto> ParTypeContrat { get; set; } = new();

        public DateTime DateReference { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    public sealed class KpiAnalyseEffectifDto
    {
        public int     EffectifTotal       { get; set; }
        public decimal PourcentageDeparts  { get; set; }   // % de sorties sur l'année
        public decimal PourcentageRotation { get; set; }   // turnover %
        public decimal AncienneteMoyenne   { get; set; }   // années
        public decimal PourcentageFemmes   { get; set; }   // % F sur effectif total
        public decimal PourcentageHommes   { get; set; }   // % H sur effectif total
        public decimal AgeMoyen            { get; set; }   // années
    }

    /// <summary>Élément d'un bar chart (libellé + valeur + % sur total).</summary>
    public sealed class BarItemDto
    {
        public string  Libelle      { get; set; } = "";
        public int     Valeur       { get; set; }
        public decimal Pourcentage  { get; set; }   // 0–100
    }
}
