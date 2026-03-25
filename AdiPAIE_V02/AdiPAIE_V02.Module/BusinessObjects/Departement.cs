using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions, XafDisplayName("Département")]
    [DefaultProperty(nameof(Nom))]
    public class Departement : BaseObject
    {
        public Departement(Session session) : base(session) { }

        // Code (12 max) + unicité
        [Size(12)]
        [Indexed(Unique = true)]
        [RuleRequiredField]
        [XafDisplayName("Code")]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code, value?.Trim());
        }
        string code;

        [Size(120)]
        [RuleRequiredField]
        [XafDisplayName("Nom")]
        public string Nom
        {
            get => nom;
            set => SetPropertyValue(nameof(Nom), ref nom, value?.Trim());
        }
        string nom;

        [XafDisplayName("Actif")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif = true;

        // Rattachement des salariés
        [Association("Departement-Salaries")]
        [XafDisplayName("Salariés")]
        public XPCollection<Salarie> Salarie => GetCollection<Salarie>(nameof(Salarie));

        [Association("DeptActuel-Avancements")]
        [Browsable(false)]
        public XPCollection<DemandeAvancement> AvancementsActuels
            => GetCollection<DemandeAvancement>(nameof(AvancementsActuels));

        [Association("NouveauDept-Avancements")]
        [Browsable(false)]
        public XPCollection<DemandeAvancement> AvancementsVers
            => GetCollection<DemandeAvancement>(nameof(AvancementsVers));
    }
}
