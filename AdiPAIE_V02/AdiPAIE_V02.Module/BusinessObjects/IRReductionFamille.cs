using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [ImageName("BO_Family")]  // V1.1 — icône XAF native (réduction familiale fiscale)
    [XafDisplayName("IR - Réduction familiale")]
    [RuleCombinationOfPropertiesIsUnique("IR_Reduction_UniqueParts", DefaultContexts.Save, "NbrePart",
        CustomMessageTemplate = "Une ligne existe déjà pour ce nombre de parts.")]
    public class IRReductionFamille : BaseObject
    {
        public IRReductionFamille(Session s) : base(s) { }

        [DbType("decimal(5,1)")]
        [XafDisplayName("Nombre de parts")]
        [ModelDefault("DisplayFormat", "N1"), ModelDefault("EditMask", "N1")]
        public decimal NbrePart { get => parts; set => SetPropertyValue(nameof(NbrePart), ref parts, value); }
        decimal parts;

        [ModelDefault("DisplayFormat", "N2"), ModelDefault("EditMask", "N2")]
        [XafDisplayName("Taux de réduction (%)")]
        public decimal Taux { get => taux; set => SetPropertyValue(nameof(Taux), ref taux, value); }
        decimal taux;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Réduction min (annuel)")]
        public decimal MinAnnuel { get => minA; set => SetPropertyValue(nameof(MinAnnuel), ref minA, value); }
        decimal minA;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Réduction max (annuel)")]
        public decimal MaxAnnuel { get => maxA; set => SetPropertyValue(nameof(MaxAnnuel), ref maxA, value); }
        decimal maxA;

        [XafDisplayName("Actif")]
        public bool Actif { get => actif; set => SetPropertyValue(nameof(Actif), ref actif, value); }
        bool actif = true;
    }
}
