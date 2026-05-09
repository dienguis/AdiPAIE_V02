// =============================================================================
//  Enfant.cs — V1.6 — Entité Enfant à charge d'un salarié
//
//  Calque sur le pattern Conjoint :
//    - Association "Salarie-Enfants" (collection agrégée côté Salarie)
//    - Champs : nom, prénom, date naissance, sexe, scolarisé, à charge fiscale
//    - DateNaissance + ACharge fiscale alimentent la base de calcul TRIMF
//      (parts fiscales : 1 + conjoint inactif à charge + 0.5 par enfant à charge,
//      cap à 5 selon Code Général des Impôts du Sénégal)
//
//  IMPORTANT : la propriété Salarie.NombreEnfant existante reste en place et
//  peut continuer à être saisie manuellement (compteur libre). Cette nouvelle
//  collection apporte le détail nominatif sans remplacer le compteur.
// =============================================================================

using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [NavigationItem(false)]
    [ImageName("BO_Salutation")] // membre famille (icône XAF native)
    [DefaultProperty(nameof(NomComplet))]
    public class Enfant : BaseObject
    {
        public Enfant(Session session) : base(session) { }

        // ── Rattachement au salarié (agrégat côté parent) ─────────
        [Association("Salarie-Enfants"), RuleRequiredField]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        // ── Identité ──────────────────────────────────────────────
        [Size(80)]
        [RuleRequiredField]
        public string NomComplet
        {
            get => nomComplet;
            set => SetPropertyValue(nameof(NomComplet), ref nomComplet, value?.Trim());
        }
        string nomComplet;

        public DateTime? DateNaissance
        {
            get => dateNaissance;
            set => SetPropertyValue(nameof(DateNaissance), ref dateNaissance, value);
        }
        DateTime? dateNaissance;

        public Sexe Sexe
        {
            get => sexe;
            set => SetPropertyValue(nameof(Sexe), ref sexe, value);
        }
        Sexe sexe;

        // ── Scolarité / charge fiscale ────────────────────────────
        [XafDisplayName("Situation")]
        [ToolTip("Situation scolaire/professionnelle. Détermine si l'enfant peut rester " +
                 "dans le quotient familial au-delà de l'âge limite (élève/étudiant/apprenti).")]
        public SituationEnfant Situation
        {
            get => situation;
            set => SetPropertyValue(nameof(Situation), ref situation, value);
        }
        SituationEnfant situation = SituationEnfant.NonScolarise;

        [XafDisplayName("À charge fiscale")]
        [ToolTip("Si oui, alimente le calcul TRIMF (0.5 part fiscale par enfant à charge, cap 5 parts total). " +
                 "Règles à valider avec RH (Code Général des Impôts Sénégal — limite d'âge sauf étudiant/apprenti).")]
        public bool ACharge
        {
            get => aCharge;
            set => SetPropertyValue(nameof(ACharge), ref aCharge, value);
        }
        bool aCharge = true;

        // ── Âge calculé (lecture seule) ───────────────────────────
        [NonPersistent]
        [XafDisplayName("Âge")]
        public int? Age
        {
            get
            {
                if (!DateNaissance.HasValue) return null;
                var today = DateTime.Today;
                var age = today.Year - DateNaissance.Value.Year;
                if (DateNaissance.Value.Date > today.AddYears(-age)) age--;
                return age < 0 ? 0 : age;
            }
        }
    }
}
