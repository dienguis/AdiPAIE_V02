// =============================================================================
//  SuiviAbsencesDto.cs — V1.3.2 (mai 2026)
//
//  REFONTE pour atteindre le niveau de l'Excel "SUIVI ABSENCES 2026" ELTON :
//    - 6 KPIs (Effectif Moyen, se sont Absentés, Total jours, Taux Absentéisme,
//             JO Perdus, Resp. Tmp Travail)
//    - Décomposition fine par motif (10 codes : 5 Absentéisme + 4 Programmées)
//    - Tableaux : Par Motif, Par Ancienneté, Par Catégorie, Par Département
//    - Liste détaillée employés avec décomposition par motif
//    - Évolution mensuelle Absentéisme + Programmées (2 séries)
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>
    /// 10 motifs d'absence ELTON, regroupés en Absentéisme (5) + Programmée (4) + Autre.
    /// Le mapping depuis CongeType.Code et FamilleConge se fait dans le service
    /// (méthode MapMotif).
    /// </summary>
    public enum MotifAbsence
    {
        // ── Absentéisme (non programmé, impacte la productivité) ──
        AUT   = 0,   // Autre absentéisme
        NAUT  = 1,   // Non Autorisé / Absence injustifiée
        EVENT = 2,   // Événement familial
        MAL   = 3,   // Maladie
        AT    = 4,   // Accident du Travail

        // ── Programmées (planifié, prévu) ──
        CPAYE = 10,  // Congé payé
        FORM  = 11,  // Formation
        MAT   = 12,  // Maternité
        PAT   = 13,  // Paternité

        Autre = 99
    }

    public sealed class SuiviAbsencesDto
    {
        // KPIs synthèse
        public KpiAbsencesDto Kpis { get; set; } = new();

        // ── Tableaux ──
        /// <summary>Décomposition par motif (10 lignes max) avec total et %.</summary>
        public List<MotifRowDto> ParMotif { get; set; } = new();

        /// <summary>Par tranche d'ancienneté (ex 1-4, 5-9, 10-14...).</summary>
        public List<TrancheAncienneteRowDto> ParAnciennete { get; set; } = new();

        /// <summary>Par catégorie professionnelle.</summary>
        public List<DimRowDto> ParCategorie { get; set; } = new();

        /// <summary>Par département.</summary>
        public List<DimRowDto> ParDepartement { get; set; } = new();

        /// <summary>Évolution mensuelle 12 mois — Absentéisme + Programmées séparées.</summary>
        public List<MoisAbsenceDto> ParMois { get; set; } = new();

        /// <summary>Liste détaillée par employé avec décomposition motif (toutes lignes).</summary>
        public List<EmployeAbsenceRowDto> Employes { get; set; } = new();

        /// <summary>Vue calendrier matricielle (rempli uniquement si Vue=Calendrier).</summary>
        public CalendrierAbsencesDto? Calendrier { get; set; }

        public DateTime CalculatedAt { get; set; }
    }

    /// <summary>
    /// Vue calendrier : 1 mois × N employés. Chaque employé a un tableau de jours
    /// avec le code motif si absent ce jour, vide sinon.
    /// </summary>
    public sealed class CalendrierAbsencesDto
    {
        public int Annee { get; set; }
        public int Mois { get; set; }
        public string MoisLibelle { get; set; } = "";
        public int NbJours { get; set; }                  // 28..31
        public List<CalendrierJourDto> Jours { get; set; } = new();
        public List<CalendrierLigneDto> Lignes { get; set; } = new();
    }

    public sealed class CalendrierJourDto
    {
        public int Jour { get; set; }                     // 1..31
        public DateTime Date { get; set; }
        public string LettreJour { get; set; } = "";       // "L", "M", "M", "J", "V", "S", "D"
        public bool IsWeekend { get; set; }
        public bool IsFerie { get; set; }
    }

    public sealed class CalendrierLigneDto
    {
        public Guid SalarieOid { get; set; }
        public string Matricule { get; set; } = "";
        public string NomComplet { get; set; } = "";
        public string Categorie { get; set; } = "";
        public string Departement { get; set; } = "";
        /// <summary>Indexé par jour (1..31). Null = pas d'absence ce jour.</summary>
        public Dictionary<int, MotifAbsence?> Cellules { get; set; } = new();
        public decimal TotalJours { get; set; }
    }

    public sealed class KpiAbsencesDto
    {
        /// <summary>Effectif moyen sur la période = moyenne mensuelle de salariés actifs.</summary>
        public decimal EffectifMoyen { get; set; }

        /// <summary>Nombre distinct de salariés ayant eu au moins 1 absence sur la période.</summary>
        public int NbSalariesAbsents { get; set; }

        /// <summary>Total absences (en nombre de jours).</summary>
        public decimal TotalJours { get; set; }

        /// <summary>Taux absentéisme = JOPerdus / (Effectif × Jours ouvrés) × 100.</summary>
        public decimal TauxAbsenteismePct { get; set; }

        /// <summary>Jours Ouvrés Perdus = Total jours d'absentéisme uniquement (hors programmées).</summary>
        public decimal JOPerdus { get; set; }

        /// <summary>Respect du Temps de Travail = 100 - Taux Absentéisme (=% de présence).</summary>
        public decimal RespTempsTravailPct { get; set; }

        // Indicateurs détaillés (utilisés en sous-titres KPIs)
        public int     NbAbsences          { get; set; }
        public decimal DureeMoyenneJours   { get; set; }
        public string  TopAbsent           { get; set; } = "";
        public decimal TopAbsentJours      { get; set; }
        public int     MoisPic             { get; set; }
        public string  MoisPicLibelle      { get; set; } = "";
    }

    public sealed class MotifRowDto
    {
        public MotifAbsence Motif { get; set; }
        public string Code { get; set; } = "";          // Ex "MAL"
        public string Libelle { get; set; } = "";        // Ex "Maladie"
        public bool   IsAbsenteisme { get; set; }
        public decimal NbJours { get; set; }
        public int     NbAbsences { get; set; }
        public decimal PourcentageDuTotal { get; set; }  // sur le total absences (toutes catégories)
    }

    public sealed class TrancheAncienneteRowDto
    {
        public string Tranche { get; set; } = "";        // "1-4", "5-9", "10-14", "15+"
        public int    NbSalariesActifs { get; set; }
        public decimal JOPerdus { get; set; }
        public decimal TauxAbsenteismePct { get; set; }
    }

    public sealed class DimRowDto
    {
        /// <summary>Libellé de la dimension (ex "Cadre", "DAF", "Siège").</summary>
        public string Libelle { get; set; } = "";
        public int     NbSalariesActifs { get; set; }
        public decimal JOPerdus { get; set; }
        public decimal TauxAbsenteismePct { get; set; }
    }

    public sealed class MoisAbsenceDto
    {
        public int    Mois { get; set; }                 // 1..12
        public string Libelle { get; set; } = "";         // "Jan", "Fév"...
        public decimal JoursAbsenteisme { get; set; }     // MAL+AT+EVENT+AUT+NAUT
        public decimal JoursProgrammees { get; set; }     // CPAYE+FORM+MAT+PAT
        public int     NbAbsences { get; set; }
    }

    public sealed class EmployeAbsenceRowDto
    {
        public Guid    SalarieOid { get; set; }
        public string  Matricule { get; set; } = "";
        public string  NomComplet { get; set; } = "";
        public string  Site { get; set; } = "";
        public string  Departement { get; set; } = "";
        public string  Categorie { get; set; } = "";
        public string  Fonction { get; set; } = "";
        public decimal AncienneteAnnees { get; set; }

        // ── Absences agrégées ──
        public decimal JoursAbsenteisme { get; set; }
        public decimal JoursProgrammees { get; set; }
        public decimal TotalJours { get; set; }
        public decimal RespTempsTravailPct { get; set; }   // 100 - taux

        // ── Décomposition motif (5 absentéisme + 4 programmées) ──
        public decimal JoursAUT { get; set; }
        public decimal JoursNAUT { get; set; }
        public decimal JoursEVENT { get; set; }
        public decimal JoursMAL { get; set; }
        public decimal JoursAT { get; set; }
        public decimal JoursCPAYE { get; set; }
        public decimal JoursFORM { get; set; }
        public decimal JoursMAT { get; set; }
        public decimal JoursPAT { get; set; }
    }
}
