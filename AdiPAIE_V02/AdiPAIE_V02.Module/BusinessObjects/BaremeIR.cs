using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    
    [XafDisplayName("Barème IR (DPP)")]
    [RuleCriteria("BaremeIR_DateRangeOK", DefaultContexts.Save,
        "IsNull(DateFin) Or IsNull(DateDebut) Or DateDebut <= DateFin",
        CustomMessageTemplate = "La date de début doit être antérieure ou égale à la date de fin.")]
    public class BaremeIR : BaseObject
    {
        public BaremeIR(Session s) : base(s) { }

        [RuleRequiredField, Size(40)]
        [XafDisplayName("Code (ex: IR_DPP_2025)")]
        [Indexed(Unique = true)]
        public string Code { get => code; set => SetPropertyValue(nameof(Code), ref code, value?.Trim()); }
        string code;

        public bool Actif { get => actif; set => SetPropertyValue(nameof(Actif), ref actif, value); }
        bool actif = true;

        public DateTime? DateDebut { get => d1; set => SetPropertyValue(nameof(DateDebut), ref d1, value); }
        public DateTime? DateFin { get => d2; set => SetPropertyValue(nameof(DateFin), ref d2, value); }
        DateTime? d1, d2;

        [Association("BaremeIR-Tranches"), Aggregated]
        public XPCollection<BaremeIRTranche> Tranches
            => GetCollection<BaremeIRTranche>(nameof(Tranches));
    }


    [XafDisplayName("Barème IR – Tranche")]
    [RuleCriteria("IR_Tranche_MinLEMax", DefaultContexts.Save,
        "MontantMin <= MontantMax",
        CustomMessageTemplate = "La borne Min doit être inférieure ou égale à la borne Max.")]
    [RuleCombinationOfPropertiesIsUnique("IR_Tranche_Unique",
        DefaultContexts.Save, "Bareme;MontantMin;MontantMax",
        CustomMessageTemplate = "Une tranche avec ce barème et cette plage existe déjà.")]
    public class BaremeIRTranche : BaseObject
    {
        public BaremeIRTranche(Session s) : base(s) { }

        [Association("BaremeIR-Tranches"), RuleRequiredField]
        public BaremeIR Bareme { get => bareme; set => SetPropertyValue(nameof(Bareme), ref bareme, value); }
        BaremeIR bareme;

        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [DbType("decimal(18,0)")]
        public decimal MontantMin { get => min; set => SetPropertyValue(nameof(MontantMin), ref min, value); }
        decimal min;

        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [DbType("decimal(18,0)")]
        public decimal MontantMax { get => max; set => SetPropertyValue(nameof(MontantMax), ref max, value); }
        decimal max;

        [ModelDefault("DisplayFormat", "N2"), ModelDefault("EditMask", "N2")]
        [XafDisplayName("Taux (%)")]
        [DbType("decimal(18,2)")]
        public decimal Taux { get => taux; set => SetPropertyValue(nameof(Taux), ref taux, value); }
        decimal taux;

        [PersistentAlias("Concat('[', ToStr(MontantMin), ' .. ', ToStr(MontantMax), '] → ', ToStr(Taux), '%')")]
        public string Display => (string)EvaluateAlias(nameof(Display));
    }
}
