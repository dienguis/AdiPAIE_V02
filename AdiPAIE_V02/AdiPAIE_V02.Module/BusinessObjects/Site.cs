// AdiPAIE_V02.Module/BusinessObjects/Site.cs
//
// Référentiel paramétrable des Sites de travail (personnel INTERNE).
// Exemples : Siège, Dépôt CDB, Dépôt Hann.
//
// Pour le personnel EXTERNE (intérimaires) : voir StationService
// (Mermoz, VDN, Cap des Biches, etc.)
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [XafDisplayName("Site")]
    [DefaultProperty(nameof(Nom))]
    [ImageName("BO_Organization")]
    public class Site : BaseObject
    {
        public Site(Session s) : base(s) { }

        [Size(20)]
        [Indexed(Unique = true)]
        [RuleRequiredField]
        [XafDisplayName("Code")]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code, value?.Trim()?.ToUpperInvariant());
        }
        string code;

        [Size(120)]
        [RuleRequiredField]
        [XafDisplayName("Nom du site")]
        public string Nom
        {
            get => nom;
            set => SetPropertyValue(nameof(Nom), ref nom, value?.Trim());
        }
        string nom;

        [Size(200)]
        [XafDisplayName("Adresse")]
        public string Adresse
        {
            get => adresse;
            set => SetPropertyValue(nameof(Adresse), ref adresse, value?.Trim());
        }
        string adresse;

        [Size(100)]
        [XafDisplayName("Ville")]
        public string Ville
        {
            get => ville;
            set => SetPropertyValue(nameof(Ville), ref ville, value?.Trim());
        }
        string ville;

        [XafDisplayName("Actif")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif = true;

        [Association("Site-Salaries")]
        [XafDisplayName("Salariés")]
        public XPCollection<Salarie> Salaries => GetCollection<Salarie>(nameof(Salaries));

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Code) ? Nom : $"{Nom} ({Code})";
    }
}
