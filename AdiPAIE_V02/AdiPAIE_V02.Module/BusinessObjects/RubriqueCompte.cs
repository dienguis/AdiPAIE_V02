using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [XafDisplayName("Override compte (par rubrique)")]
    [NavigationItem(false)]
    [DefaultProperty(nameof(DisplayName))]
    public class RubriqueCompte : BaseObject
    {
        public RubriqueCompte(Session s) : base(s) { }

        // Constantes compatibles SQL Server DATETIME (évite MinValue)
        private static readonly DateTime SqlMin = new(1753, 1, 1);
        private static readonly DateTime SqlMax = new(9999, 12, 31, 23, 59, 59);

        // --- Liens ---
        [Association("Rubrique-RubriqueComptes"), RuleRequiredField]
        public Rubrique Rubrique
        {
            get => rubrique; set => SetPropertyValue(nameof(Rubrique), ref rubrique, value);
        }
        Rubrique rubrique;

        // Nullable pour RuleRequiredField
        [RuleRequiredField]
        public TypeAffectationCompte? Type
        {
            get => type; set => SetPropertyValue(nameof(Type), ref type, value);
        }
        TypeAffectationCompte? type;

        [RuleRequiredField]
        public PlanComptable Compte
        {
            get => compte; set => SetPropertyValue(nameof(Compte), ref compte, value);
        }
        PlanComptable compte;

        // --- Période de validité (optionnelle) ---
        [ModelDefault("DisplayFormat", "d")]
        public DateTime? DateDebut
        {
            get => d1; set => SetPropertyValue(nameof(DateDebut), ref d1, value);
        }
        [ModelDefault("DisplayFormat", "d")]
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

        [PersistentAlias("Concat(Rubrique.Code, ' | ', Iif(IsNull(Type), '—', ToStr(Type)), ' | ', Compte.Code)")]
        public string DisplayName => Convert.ToString(EvaluateAlias(nameof(DisplayName)));

        protected override void OnSaving()
        {
            base.OnSaving();

            if (!Actif || Rubrique == null || Type == null || Compte == null)
                return;

            // Vérif borne logique
            var start = DateDebut ?? SqlMin;
            var endOpt = DateFin;
            if (endOpt.HasValue && endOpt.Value < start)
                throw new UserFriendlyException("La date de fin ne peut pas être antérieure à la date de début.");

            var endVal = endOpt ?? SqlMax;

            // Contrôle chevauchement (lambda d’expression traduisible par XPO)
            var hasOverlap = Session.Query<RubriqueCompte>()
                .Where(x => x.Oid != Oid
                         && x.Rubrique == Rubrique
                         && x.Type == Type
                         && x.Actif)
                .Any(x =>
                    (start <= (x.DateFin ?? SqlMax)) &&
                    ((x.DateDebut ?? SqlMin) <= endVal)
                );

            if (hasOverlap)
                throw new UserFriendlyException("Périodes qui se chevauchent pour cette Rubrique / Type d'affectation.");
        }
    }
}
