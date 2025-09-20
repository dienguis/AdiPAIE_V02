using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions, XafDisplayName("Type de prêt")]
    [DefaultProperty(nameof(DisplayName))]

    // Règle de cohérence : si une rubrique est renseignée, elle doit être de type Retenue
    [RuleCriteria("PretType.RubriqueRetenueIsRetenue", DefaultContexts.Save,
            "RubriqueRetenue is null or RubriqueRetenue.TypeCalcul = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+RubriqueTypeCalcul,Retenue#",
            "La rubrique doit être de type Retenue.")]
    public class PretType : BaseObject
    {
        public PretType(Session s) : base(s) { }

        [Size(30), Indexed(Unique = true), RuleRequiredField]
        public string Code
        {
            get => code; set => SetPropertyValue(nameof(Code), ref code, value?.Trim());
        }
        string code;

        [Size(120), RuleRequiredField]
        public string Libelle
        {
            get => lib; set => SetPropertyValue(nameof(Libelle), ref lib, value?.Trim());
        }
        string lib;

        public PretNature Nature
        {
            get => nature; set => SetPropertyValue(nameof(Nature), ref nature, value);
        }
        PretNature nature = PretNature.Pret;

        public PretAmortissement AmortissementDefaut
        {
            get => amort; set => SetPropertyValue(nameof(AmortissementDefaut), ref amort, value);
        }
        PretAmortissement amort = PretAmortissement.PrincipalConstant;

        [DbType("decimal(9,2)")]
        public decimal? TauxInteretDefaut
        {
            get => tx; set => SetPropertyValue(nameof(TauxInteretDefaut), ref tx, value);
        }
        decimal? tx;

        public int? DureeMaxMois
        {
            get => duree; set => SetPropertyValue(nameof(DureeMaxMois), ref duree, value);
        }
        int? duree;

        [DbType("decimal(18,0)")]
        public decimal? PlafondMontant
        {
            get => plaf; set => SetPropertyValue(nameof(PlafondMontant), ref plaf, value);
        }
        decimal? plaf;

        // Rubrique de retenue associée à ce type (côté salarié)
        [XafDisplayName("Rubrique de retenue")]
        public Rubrique RubriqueRetenue
        {
            get => rub; set => SetPropertyValue(nameof(RubriqueRetenue), ref rub, value);
        }
        Rubrique rub;

        public bool Actif
        {
            get => actif; set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif = true;

        [Association("PretType-Prets")]
        public XPCollection<Pret> Prets => GetCollection<Pret>(nameof(Prets));

        [PersistentAlias("Concat(Code,' - ',Libelle)")]
        public string DisplayName => (string)EvaluateAlias(nameof(DisplayName));


        public bool RubriqueOK => RubriqueRetenue == null || RubriqueRetenue.TypeCalcul == RubriqueTypeCalcul.Retenue;
    }
}
