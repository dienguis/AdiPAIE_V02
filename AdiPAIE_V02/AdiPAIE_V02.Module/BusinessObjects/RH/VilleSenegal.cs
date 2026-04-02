using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Référentiel des villes sénégalaises avec orthographe exacte Google Maps.
    /// Évite les erreurs de saisie (M'Bour vs Mbour, etc.)
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Ville (Sénégal)")]
    [DefaultProperty(nameof(Nom))]
    //[NavigationItem("GRH - Administration")]
    public class VilleSenegal : BaseObject
    {
        public VilleSenegal(Session session) : base(session) { }

        [RuleRequiredField]
        [Size(100)]
        [RuleUniqueValue(DefaultContexts.Save,
            CustomMessageTemplate = "Cette ville existe déjà.")]
        [XafDisplayName("Nom (orthographe exacte)")]
        public string Nom
        {
            get => nom;
            set => SetPropertyValue(nameof(Nom), ref nom, value?.Trim());
        }
        string nom;

        [Size(100)]
        [XafDisplayName("Région")]
        public string Region
        {
            get => region;
            set => SetPropertyValue(nameof(Region), ref region, value?.Trim());
        }
        string region;

        [XafDisplayName("Active")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif = true;

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Region) ? Nom : $"{Nom} ({Region})";
    }
}
