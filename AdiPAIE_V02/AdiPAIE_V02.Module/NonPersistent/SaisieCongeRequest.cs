// =============================================================================
//  SaisieCongeRequest.cs — V1.8 (juin 2026)
//
//  DTO non-persistant pour le popup "Saisir un congé" sur fiche Salarié.
//
//  Couvre 2 cas métier :
//
//    A) ALLOCATION DE CONGÉ (le salarié part en congé)
//       → TypeOperation = AllocationConge
//       → Rubrique CONGE_PAYE générée
//       → Selon ParametresPaie.ModeBulletinConges :
//          - BulletinUnique : ligne ajoutée au bulletin mensuel
//          - BulletinSepare : nouveau bulletin distinct pour le mois
//
//    B) RACHAT (ICCP) — compensation monétaire sans départ physique
//       → TypeOperation = RachatICCP
//       → Rubrique ICCP générée
//       → Toujours ajoutée au bulletin mensuel normal
//
//  Modes de calcul du montant :
//    - AUTO   : (Σ brut 12 mois / 12) × (JoursDus / 24)
//               Utilisable si l'historique AdiPAIE contient ≥ 12 bulletins
//    - MANUEL : le RH saisit directement le montant (cas rétroactif où
//               AdiPAIE n'a pas l'historique de l'ancien système)
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.NonPersistent
{
    /// <summary>
    /// Type d'opération congé à effectuer.
    /// </summary>
    public enum TypeOperationConge
    {
        // Cas 1 : le salarié part en congé OU on régularise rétroactivement
        //         un bulletin de congé déjà payé dans l'ancien système.
        [XafDisplayName("Allocation de congé (départ / régularisation)")]
        AllocationConge = 0,
        // Cas 2 : compensation monétaire des jours non pris, sans départ
        //         physique (rachat ponctuel décidé par RH/DAF).
        [XafDisplayName("Rachat ICCP (compensation monétaire)")]
        RachatICCP = 1,
    }

    /// <summary>
    /// Mode de calcul du montant de l'allocation.
    /// </summary>
    public enum ModeCalculIndemnite
    {
        [XafDisplayName("Automatique (depuis l'historique 12 mois)")]
        Auto = 0,
        [XafDisplayName("Manuel (saisie directe du montant)")]
        Manuel = 1,
    }

    [DomainComponent]
    [XafDisplayName("Saisir un congé")]
    [ImageName("Action_GrantPermission")]
    public class SaisieCongeRequest : NonPersistentBaseObject
    {
        // ── Type d'opération ────────────────────────────────────────
        // V1.8 — value type : pas de [RuleRequiredField] (XAF0009).
        // La valeur par défaut "AllocationConge" sert de pré-sélection,
        // le RH peut changer en "RachatICCP" via la liste déroulante.
        [XafDisplayName("Type d'opération")]
        [ToolTip("• ALLOCATION DE CONGÉ : à utiliser dans 2 cas :\n" +
                 "  (1) Le salarié part en congé maintenant (cas standard)\n" +
                 "  (2) Régularisation rétroactive d'un bulletin de congé " +
                 "déjà payé dans l'ancien système (mise en prod). Dans ce " +
                 "cas, choisir Mode MANUEL + saisir le montant exact lu sur " +
                 "l'ancien bulletin.\n\n" +
                 "• RACHAT ICCP : compensation monétaire des jours non pris, " +
                 "sans départ physique. Le salarié continue de travailler et " +
                 "touche un complément sur son bulletin mensuel normal.")]
        [ImmediatePostData]
        public TypeOperationConge TypeOperation { get; set; } = TypeOperationConge.AllocationConge;

        // ── Type de congé ──────────────────────────────────────────
        [RuleRequiredField]
        [XafDisplayName("Type de congé")]
        public CongeType TypeConge { get; set; }

        // ── Année d'origine du solde à débiter ─────────────────────
        [RuleRange(2020, 2050)]
        [XafDisplayName("Année origine du solde")]
        [ToolTip("Année dont on débite les jours (typiquement l'année " +
                 "précédente si on solde un cumul, l'année courante si c'est " +
                 "le congé annuel régulier).")]
        public int AnneeOrigineSolde { get; set; } = DateTime.Today.Year;

        // ── Nombre de jours ────────────────────────────────────────
        [ModelDefault("DisplayFormat", "N2"), ModelDefault("EditMask", "N2")]
        [XafDisplayName("Nombre de jours")]
        [ToolTip("Nombre de jours de congé pris (ou rachetés). Inclut " +
                 "les bonus ancienneté + mère si applicables.")]
        public decimal NbJours { get; set; }

        // ── Dates effectives (uniquement pour AllocationConge) ────
        [XafDisplayName("Date de début")]
        [ToolTip("Premier jour de congé (utilisé uniquement pour l'allocation, " +
                 "pas pour le rachat).")]
        public DateTime? DateDebut { get; set; }

        [XafDisplayName("Date de fin")]
        [ToolTip("Dernier jour de congé.")]
        public DateTime? DateFin { get; set; }

        // ── Période de paie destinataire ───────────────────────────
        [RuleRange(2020, 2050)]
        [XafDisplayName("Année du bulletin")]
        [ToolTip("Année du bulletin où la ligne sera ajoutée.")]
        public int AnneeBulletin { get; set; } = DateTime.Today.Year;

        [RuleRange(1, 12)]
        [XafDisplayName("Mois du bulletin")]
        [ToolTip("Mois du bulletin où la ligne sera ajoutée. Pour un congé, " +
                 "c'est généralement le mois du départ.")]
        public int MoisBulletin { get; set; } = DateTime.Today.Month;

        // ── Mode de calcul du montant ──────────────────────────────
        [XafDisplayName("Mode de calcul")]
        [ToolTip("AUTO : calcul automatique depuis l'historique des bulletins. " +
                 "MANUEL : saisie directe du montant (utiliser pour les " +
                 "saisies rétroactives sans historique AdiPAIE).")]
        [ImmediatePostData]
        public ModeCalculIndemnite ModeCalcul { get; set; } = ModeCalculIndemnite.Auto;

        // ── V1.8 — Bascule bulletin mensuel → bulletin de congé ───
        // Coché par défaut car c'est la pratique ELTON courante : quand
        // le salarié part en congé tout le mois, son bulletin contient
        // l'indemnité de congé EN REMPLACEMENT du salaire (pas en plus).
        // Décocher uniquement si on veut conserver le salaire mensuel
        // et juste y ajouter l'indemnité en plus (cas exceptionnel).
        [XafDisplayName("Basculer en bulletin de congé (supprimer le salaire normal)")]
        [ToolTip("Si coché ET type = Allocation de congé : supprime " +
                 "automatiquement les rubriques de salaire normal " +
                 "(Salaire de base, Sursalaire, Ancienneté, Indemnité " +
                 "logement, Prime transport, Heures sup, Indemnités " +
                 "génériques) sur le bulletin AVANT d'ajouter l'indemnité " +
                 "de congé. Les avantages en nature (véhicule, téléphone) " +
                 "et les cotisations sont CONSERVÉS — ils se recalculeront " +
                 "automatiquement sur la nouvelle base.")]
        public bool BasculerEnBulletinDeConge { get; set; } = true;

        // ── Montant (en mode manuel) ──────────────────────────────
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Montant manuel (FCFA)")]
        [ToolTip("Saisir directement le montant en FCFA (utilisé uniquement " +
                 "en mode MANUEL). Pour un calcul AUTO, ce champ est ignoré.")]
        public decimal MontantManuel { get; set; }

        // ── Motif libre ────────────────────────────────────────────
        [XafDisplayName("Motif")]
        [ToolTip("Note libre (ex: 'Congé annuel 2025 — départ effectif', " +
                 "'Rachat 18j non pris 2024 — accord DG', etc.).")]
        [ModelDefault("RowCount", "3")]
        public string Motif { get; set; }
    }
}
