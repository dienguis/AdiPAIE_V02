// =============================================================================
//  SaisieSoldeInitialRequest.cs — V1.8 (juin 2026)
//
//  DTO non-persistant pour le popup "Saisir solde initial" sur fiche Salarié.
//
//  Cas d'usage : à la mise en production, le RH saisit le solde de congés
//  cumulé connu pour chaque salarié, en s'appuyant sur le fichier Excel
//  "Planning Congés 2026". Les 16 lignes en bleu (RH ne connaît pas le
//  solde) sont saisies avec SoldeAVerifier = true et JoursReportes = 0.
//
//  Le controller SaisieSoldeInitialController crée (ou met à jour) un
//  SoldeConge actif pour le Salarie + Type + Année, en appliquant ces
//  valeurs et en traçant la source d'origine.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using System;

namespace AdiPAIE_V02.Module.NonPersistent
{
    [DomainComponent]
    [XafDisplayName("Saisir solde initial de congés")]
    [ImageName("BO_List")]
    public class SaisieSoldeInitialRequest : NonPersistentBaseObject
    {
        // ── Type de congé concerné ─────────────────────────────────
        [RuleRequiredField(CustomMessageTemplate = "Type de congé obligatoire.")]
        [XafDisplayName("Type de congé")]
        [ToolTip("Sélectionner le type de congé concerné (en général CPAYE).")]
        public CongeType TypeConge { get; set; }

        // ── Année du solde ─────────────────────────────────────────
        // V1.8 — int est value type → pas de [RuleRequiredField] (XAF0009)
        [RuleRange(2020, 2050)]
        [XafDisplayName("Année du solde")]
        [ToolTip("Année de l'exercice de référence du solde (typiquement " +
                 "l'année de mise en production).")]
        public int Annee { get; set; } = DateTime.Today.Year;

        // ── Jours reportés (= cumul existant à la date d'arrêté) ──
        [ModelDefault("DisplayFormat", "N2"), ModelDefault("EditMask", "N2")]
        [XafDisplayName("Jours reportés (cumul actuel)")]
        [ToolTip("Solde cumulé en jours, tel que lu dans le fichier Excel " +
                 "RH ou l'ancien système. Peut être négatif (cas Gueladio BA : " +
                 "jours pris en avance, à imputer sur les acquisitions à venir).")]
        public decimal JoursReportes { get; set; }

        // ── Date d'arrêté du solde ─────────────────────────────────
        [XafDisplayName("Solde arrêté au")]
        [ToolTip("Date à laquelle ce solde a été constaté (lecture du " +
                 "fichier Excel RH). Si laissée vide, on prendra la date " +
                 "d'aujourd'hui par défaut.")]
        public DateTime? SoldeArreteAu { get; set; }

        // ── Flag à vérifier (lignes bleues du fichier Excel) ─────
        [XafDisplayName("Solde à vérifier ultérieurement")]
        [ToolTip("Cocher si la valeur saisie est incertaine (cas des lignes " +
                 "en bleu du fichier Excel ELTON pour lesquelles le RH ne " +
                 "connaît pas le vrai solde). Permet une régularisation " +
                 "ultérieure.")]
        public bool SoldeAVerifier { get; set; }

        // ── Source ────────────────────────────────────────────────
        [XafDisplayName("Source")]
        [ToolTip("Origine de la donnée (ex: 'Excel Planning Congés 2026', " +
                 "'Ancien système', 'Décision RH', etc.).")]
        public string Source { get; set; } = "Excel Planning Congés 2026";

        // ── Commentaire libre ────────────────────────────────────
        [XafDisplayName("Commentaire")]
        [ToolTip("Note libre pour audit (ex: 'à confirmer avec Aissatou', " +
                 "'inclut bonus ancienneté', etc.).")]
        [ModelDefault("RowCount", "3")]
        public string Commentaire { get; set; }
    }
}
