using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
   

    // ========= Racine du barème =========
    [DefaultClassOptions]
    [XafDisplayName("TRIMF - Barème")]
    [RuleCriteria("BaremeTRIMF_DateRangeOK", DefaultContexts.Save,
        "IsNull(DateFin) Or IsNull(DateDebut) Or DateDebut <= DateFin",
        CustomMessageTemplate = "La date de début doit être antérieure ou égale à la date de fin.")]
    public class BaremeTRIMF : BaseObject
    {
        public BaremeTRIMF(Session s) : base(s) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            if (!Nature.HasValue)
                Nature = Domain.DomainEnums.TrimfNature.Mensuel; // ou Annuel, selon ton besoin
        }


        [RuleRequiredField, Size(40)]
        [XafDisplayName("Code (ex: TRIMF_2025 / TRIMF_AN_2025)")]
        [Indexed(nameof(Nature), Unique = true, Name = "UX_BaremeTRIMF_Code_Nature")]
        public string Code { get => code; set => SetPropertyValue(nameof(Code), ref code, value?.Trim()); }
        string code;

        [RuleRequiredField]
        public Domain.DomainEnums.TrimfNature? Nature
        {
            get => nature;
            set => SetPropertyValue(nameof(Nature), ref nature, value);
        }
        Domain.DomainEnums.TrimfNature? nature;
     
        public bool Actif { get => actif; set => SetPropertyValue(nameof(Actif), ref actif, value); }
        bool actif = true;

        public DateTime? DateDebut { get => d1; set => SetPropertyValue(nameof(DateDebut), ref d1, value); }
        public DateTime? DateFin { get => d2; set => SetPropertyValue(nameof(DateFin), ref d2, value); }
        DateTime? d1, d2;

        [Association("BaremeTRIMF-Tranches"), Aggregated]
        public XPCollection<BaremeTRIMFTranche> Tranches
            => GetCollection<BaremeTRIMFTranche>(nameof(Tranches));
    }

    // ========= Tranche =========
   // [DefaultClassOptions]
    [XafDisplayName("TRIMF - Tranche")]
    // ✅ Règles au NIVEAU CLASSE (pas sur une propriété)
    [RuleCriteria("TRIMF_Tranche_MinLEMax", DefaultContexts.Save,
        "MontantMin <= MontantMax",
        CustomMessageTemplate = "La borne Min doit être inférieure ou égale à la borne Max.")]
    [RuleCombinationOfPropertiesIsUnique("TRIMF_Tranche_Unique",
        DefaultContexts.Save, "Bareme;MontantMin;MontantMax",
        CustomMessageTemplate = "Une tranche avec ce barème et cette plage existe déjà.")]
    public class BaremeTRIMFTranche : BaseObject
    {
        public BaremeTRIMFTranche(Session s) : base(s) { }

        [Association("BaremeTRIMF-Tranches"), RuleRequiredField]
        public BaremeTRIMF Bareme { get => bareme; set => SetPropertyValue(nameof(Bareme), ref bareme, value); }
        BaremeTRIMF bareme;

        [RuleRange(1, 999)]
        public int Ordre { get => ordre; set => SetPropertyValue(nameof(Ordre), ref ordre, value); }
        int ordre;

        // Montants en FCFA (integers) : N0 + decimal(18,0)
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [DbType("decimal(18,0)")]
        public decimal MontantMin { get => min; set => SetPropertyValue(nameof(MontantMin), ref min, value); }
        decimal min;

        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [DbType("decimal(18,0)")]
        public decimal MontantMax { get => max; set => SetPropertyValue(nameof(MontantMax), ref max, value); }
        decimal max;

        [XafDisplayName("Montant par part")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [DbType("decimal(18,0)")]
        public decimal Montant { get => montant; set => SetPropertyValue(nameof(Montant), ref montant, value); }
        decimal montant;

        // (facultatif) alias lisible
        [PersistentAlias("Concat('[', ToStr(MontantMin), ' .. ', ToStr(MontantMax), '] → ', ToStr(Montant))")]
        public string Display => (string)EvaluateAlias(nameof(Display));
    }
}
