using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions, XafDisplayName("Fonction")]
    [DefaultProperty(nameof(Intitule))]
    public class Fonction : BaseObject
    {
        public Fonction(Session session) : base(session) { }

        [Size(20)]
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
        [XafDisplayName("Intitulé")]
        public string Intitule
        {
            get => intitule;
            set => SetPropertyValue(nameof(Intitule), ref intitule, value?.Trim());
        }
        string intitule;

        [XafDisplayName("Actif")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif = true;

        [Association("Fonction-Salaries")]
        [XafDisplayName("Salariés")]
        public XPCollection<Salarie> Salarie => GetCollection<Salarie>(nameof(Salarie));
    }
}
