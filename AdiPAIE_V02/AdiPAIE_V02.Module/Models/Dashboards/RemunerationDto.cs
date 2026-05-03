// =============================================================================
//  RemunerationDto.cs
//  Tableau N°4 (Rémunération) — DTOs renvoyés par le service au Razor.
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class RemunerationDto
    {
        public KpiRemunerationDto Kpis { get; set; } = new();

        // Tableaux d'égalité des salaires
        public List<EgaliteSalaireRowDto> ParSegment   { get; set; } = new();
        public List<EgaliteSalaireRowDto> ParCategorie { get; set; } = new();

        // Évolution mensuelle (12 mois Jan→Déc) — pour graphe d'évolution
        public List<MasseMensuelleDto> EvolutionMensuelle { get; set; } = new();

        // Décomposition par famille de rubrique (cf. SPEC mesure 10) — INTERNE
        public List<DecompositionRubriqueDto> ParFamilleRubrique { get; set; } = new();

        public DateTime CalculatedAt { get; set; }
    }

    public sealed class KpiRemunerationDto
    {
        /// <summary>Somme brute année selon le mode (BrutFiscal | NetAPayer | CoûtEmployeur).</summary>
        public decimal Total              { get; set; }
        public decimal SalaireMin         { get; set; }
        public decimal SalaireMax         { get; set; }
        public decimal SalaireMoyen       { get; set; }
        /// <summary>Coût annuel moyen par salarié (Total / NbSalariesDistincts).</summary>
        public decimal CoutMoyenSalarie   { get; set; }
        public int     NbSalariesDistincts { get; set; }
        public int     NbBulletins        { get; set; }   // INTERNE only
    }

    /// <summary>
    /// Une ligne du tableau « Rémunération par Segment / Catégorie — Égalité des Salaires ».
    /// Reproduit la maquette PowerBI :
    ///   Libellé · Total · CoûtMoy · Min · Max · Moyenne · MoyFemme · MoyHomme.
    /// </summary>
    public sealed class EgaliteSalaireRowDto
    {
        public string  Libelle     { get; set; } = "";
        public decimal Total       { get; set; }
        public decimal CoutMoyen   { get; set; }   // Total / NbSalariesDistincts du segment
        public decimal Min         { get; set; }
        public decimal Max         { get; set; }
        public decimal Moyenne     { get; set; }
        public decimal? MoyFemme   { get; set; }   // null si aucune femme dans le segment
        public decimal? MoyHomme   { get; set; }
        public int     NbSalaries  { get; set; }
        /// <summary>Pourcentage de la masse totale du segment vs total global (0–100).</summary>
        public decimal PourcentageMasse { get; set; }
    }

    public sealed class MasseMensuelleDto
    {
        public int     Mois        { get; set; }   // 1–12
        public string  Libelle     { get; set; } = "";
        public decimal Masse       { get; set; }
        public int     NbBulletins { get; set; }
    }

    public sealed class DecompositionRubriqueDto
    {
        public string  FamilleMacro    { get; set; } = "";
        public decimal TotalSalarial   { get; set; }
        public decimal TotalEmployeur  { get; set; }
        public decimal TotalCombined   { get; set; }
    }
}
