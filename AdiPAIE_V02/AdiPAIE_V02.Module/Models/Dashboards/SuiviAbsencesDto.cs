// =============================================================================
//  SuiviAbsencesDto.cs
//  Tableau N°5 (Suivi des Absences) — DTOs renvoyés par le service au Razor.
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class SuiviAbsencesDto
    {
        public KpiAbsencesDto Kpis { get; set; } = new();

        // Charts
        public List<BarItemDto> ParFamille    { get; set; } = new();   // Annuel / Maladie / ...
        public List<BarItemDto> ParCategorie  { get; set; } = new();
        public List<BarItemDto> ParSegment    { get; set; } = new();
        public List<MoisAbsenceDto> ParMois   { get; set; } = new();   // 12 mois

        // Top 10 absents
        public List<TopAbsentRowDto> Top10Absents { get; set; } = new();

        public DateTime CalculatedAt { get; set; }
    }

    public sealed class KpiAbsencesDto
    {
        public int     NbAbsences          { get; set; }
        public decimal TotalJours          { get; set; }
        /// <summary>Taux d'absentéisme = Σ jours / (nb salariés × jours ouvrables) × 100.</summary>
        public decimal TauxAbsenteisme     { get; set; }
        public decimal DureeMoyenneJours   { get; set; }
        public decimal PourcentageJustifie { get; set; }
        /// <summary>Salarié le plus absent (TopJours.Libelle = nom complet).</summary>
        public string  TopAbsent           { get; set; } = "";
        public decimal TopAbsentJours      { get; set; }
        /// <summary>Mois pic (1–12).</summary>
        public int     MoisPic             { get; set; }
        public string  MoisPicLibelle      { get; set; } = "";
        /// <summary>Coût estimé pour les familles à impact "Impaye" (jours × salaire moyen jour).</summary>
        public decimal CoutEstimeImpaye    { get; set; }
        public int     NbSalariesAbsents   { get; set; }
        public int     NbSalariesEffectif  { get; set; }
    }

    public sealed class MoisAbsenceDto
    {
        public int     Mois          { get; set; }   // 1..12
        public string  Libelle       { get; set; } = "";
        public decimal NbJours       { get; set; }
        public int     NbAbsences    { get; set; }
    }

    public sealed class TopAbsentRowDto
    {
        public string  NomSalarie         { get; set; } = "";
        public string  Categorie          { get; set; } = "";
        public string  Segment            { get; set; } = "";
        public int     NbAbsences         { get; set; }
        public decimal TotalJours         { get; set; }
        public string  FamillePrincipale  { get; set; } = "";
    }
}
