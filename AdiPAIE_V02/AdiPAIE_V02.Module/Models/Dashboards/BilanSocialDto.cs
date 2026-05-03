// =============================================================================
//  BilanSocialDto.cs
//  Tableau N°6 (Bilan Social Mensuel) — DTOs renvoyés par le service au Razor.
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class BilanSocialDto
    {
        public KpiBilanSocialDto Kpis { get; set; } = new();

        /// <summary>12 lignes Jan→Déc + 1 ligne Total (en queue).</summary>
        public List<MoisBilanSocialDto> Mois { get; set; } = new();

        /// <summary>Ligne complémentaire « dont Intérimaires » (pour info managériale).</summary>
        public IntemRowDto? DontInterimaires { get; set; }

        public DateTime CalculatedAt { get; set; }
    }

    /// <summary>Ligne « dont Intérimaires » — info managériale, hors DTSS officiel.</summary>
    public sealed class IntemRowDto
    {
        public int     EffectifMoyen   { get; set; }
        public int     NbContrats      { get; set; }
        public decimal CoutTotal       { get; set; }
        public int     ArriveesContrats { get; set; }
        public int     DepartsContrats  { get; set; }
    }

    public sealed class KpiBilanSocialDto
    {
        public int     EffectifMoyenAnnuel  { get; set; }
        public int     EffectifFinAnnee     { get; set; }
        public int     TotalEmbauches       { get; set; }
        public int     TotalDeparts         { get; set; }
        public decimal MasseSalarialeAnnuelle { get; set; }
        public decimal ChargesPatronalesAnnuelles { get; set; }
        public decimal CoutEmployeurAnnuel  { get; set; }
        public int     TotalJoursAbsence    { get; set; }
        public decimal TauxAbsenteisme      { get; set; }    // % sur jours ouvrables
        /// <summary>% de cellules « renseignées » (i.e. > 0) — indicateur qualité données.</summary>
        public decimal CompletudeData       { get; set; }
    }

    public sealed class MoisBilanSocialDto
    {
        public int     Mois                 { get; set; }    // 1..12, ou 0 = ligne Total
        public string  Libelle              { get; set; } = ""; // "Janv.", ..., "TOTAL"
        public bool    EstTotal             { get; set; }

        // ── Effectif & mouvements ──────────────────────────────────────
        public int     EffectifFinMois      { get; set; }
        public int     Embauches            { get; set; }
        public int     Departs              { get; set; }

        // ── Rémunération ───────────────────────────────────────────────
        public decimal MasseSalariale       { get; set; }
        public decimal ChargesPatronales    { get; set; }
        public decimal CoutEmployeur        => MasseSalariale + ChargesPatronales;

        // ── Absences ───────────────────────────────────────────────────
        public int     EmployesAbsents      { get; set; }
        public int     JoursAbsence         { get; set; }

        // ── Indicateurs non disponibles dans le modèle actuel ──────────
        //   Marqués nullable<int> pour différencier 0 (réel) de N/A.
        public int?    MouvementsEmplois    { get; set; }    // mutations internes
        public int?    MesuresDisciplinaires { get; set; }
        public int?    AccidentsTravail     { get; set; }
        public int?    AccidentsTrajet      { get; set; }
        public int?    MaladiesProf         { get; set; }
        public int?    JoursArretTravail    { get; set; }
        public decimal? BudgetFormation     { get; set; }
        public int?    HeuresFormation      { get; set; }
        public int?    EmployesFormes       { get; set; }
    }
}
