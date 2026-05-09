                                                    using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
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
    [ImageName("BO_Salutation")]  // V1.1 — icône XAF native (membre famille)
    [DefaultProperty(nameof(NomComplet))]

    // 🔒 Interdit d’enregistrer un conjoint si le salarié n’est pas "Marié"
    [RuleCriteria(
        "Conjoint_OnlyIfSalarieMarried",
        DefaultContexts.Save,
        "Salarie.SatutMarital = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SituationMaritale,Marie#",
        CustomMessageTemplate = "Le salarié doit être 'Marié' pour gérer des conjoints."
    )]
    public class Conjoint : BaseObject
    {
        public Conjoint(Session session) : base(session) { }

        // Rattachement au salarié (agrégat côté parent seulement)
        [Association("Salarie-Conjoints"), RuleRequiredField]
        public Salarie Salarie { get => salarie; set => SetPropertyValue(nameof(Salarie), ref salarie, value); }
        Salarie salarie;

        [Size(120)]
        public string NomComplet { get => nom; set => SetPropertyValue(nameof(NomComplet), ref nom, value?.Trim()); }
        string nom;

        // V1.6 — date de naissance (oubli initial corrigé)
        [XafDisplayName("Date de naissance")]
        public DateTime? DateNaissance { get => dn; set => SetPropertyValue(nameof(DateNaissance), ref dn, value); }
        DateTime? dn;

        public DateTime? DateMariage { get => dm; set => SetPropertyValue(nameof(DateMariage), ref dm, value); }
        public DateTime? DateFinUnion { get => df; set => SetPropertyValue(nameof(DateFinUnion), ref df, value); }
        DateTime? dm; DateTime? df;

        // V1.6 — âge calculé (lecture seule)
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

        public StatutConjoint Statut { get => statut; set => SetPropertyValue(nameof(Statut), ref statut, value); }
        StatutConjoint statut = StatutConjoint.Inactif;

        /// <summary>Éligible TRIMF (ex. conjoint inactif à charge)</summary>
        public bool ACharge { get => acharge; set => SetPropertyValue(nameof(ACharge), ref acharge, value); }
        bool acharge = false;

        // Indicateur “en cours” (lecture seule)
        [PersistentAlias("Iif(IsNull(DateFinUnion), 1, 0)")]
        [XafDisplayName("Union en cours")]
        public bool EstActuel
        {
            get
            {
                try { return Convert.ToInt32(EvaluateAlias(nameof(EstActuel)) ?? 0) == 1; }
                catch (ObjectDisposedException) { return false; }
            }
        }

        // Variante blocage : AUCUNE auto-correction ici.
        // (Pas de OnSaving qui force SatutMarital = Marié)
    }
}
