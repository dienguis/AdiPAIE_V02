using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
   // [DefaultClassOptions]
    [XafDisplayName("Override compte (par rôle canonique)")]
    public class RubriqueCompteOverride : BaseObject
    {
        public RubriqueCompteOverride(Session s) : base(s) { }

        // Rôle canonique (nullable pour RuleRequiredField)
        [RuleRequiredField]
        public RubriqueCanonique? Canonique
        {
            get => role; set => SetPropertyValue(nameof(Canonique), ref role, value);
        }
        RubriqueCanonique? role;

        // Type d'affectation (nullable pour RuleRequiredField)
        [RuleRequiredField]
        public TypeAffectationCompte? Type
        {
            get => type; set => SetPropertyValue(nameof(Type), ref type, value);
        }
        TypeAffectationCompte? type;

        // Compte à utiliser
        [RuleRequiredField]
        public PlanComptable Compte
        {
            get => compte; set => SetPropertyValue(nameof(Compte), ref compte, value);
        }
        PlanComptable compte;

        // Période de validité (optionnelle)
        public DateTime? DateDebut
        {
            get => d1; set => SetPropertyValue(nameof(DateDebut), ref d1, value);
        }
        public DateTime? DateFin
        {
            get => d2; set => SetPropertyValue(nameof(DateFin), ref d2, value);
        }
        DateTime? d1, d2;

        public bool Actif
        {
            get => actif; set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif = true;

        protected override void OnSaving()
        {
            base.OnSaving();
            // Si pas encore complet, on laisse RuleRequiredField gérer
            if (Canonique == null || Type == null || !Actif) return;

            // Chevauchement interdit pour (Canonique, Type)
            var hasOverlap = Session.Query<RubriqueCompteOverride>()
                .Where(x => x.Oid != Oid && x.Canonique == Canonique && x.Type == Type && x.Actif)
                .Any(x =>
                    (DateDebut ?? DateTime.MinValue) <= (x.DateFin ?? DateTime.MaxValue) &&
                    (x.DateDebut ?? DateTime.MinValue) <= (DateFin ?? DateTime.MaxValue)
                );

            if (hasOverlap)
                throw new UserFriendlyException("Périodes qui se chevauchent pour ce rôle canonique / type.");
        }
    }
}
