using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Référentiel des postes intérimaires ELTON.
    /// Exemples : Steward, Manager, Caissier, Chef Boutique, Laveur...
    /// Partagé entre DemandeRecrutementInterim et ContratInterim.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Poste intérimaire")]
    [DefaultProperty(nameof(Libelle))]
    [ImageName("BO_Organization")]
   // [NavigationItem("GRH - Intérimaires")]
    public class PosteInterimaire : BaseObject
    {
        public PosteInterimaire(Session session) : base(session) { }

        [RuleRequiredField]
        [Size(100)]
        [RuleUniqueValue(DefaultContexts.Save,
            CustomMessageTemplate = "Ce libellé existe déjà.")]
        [XafDisplayName("Libellé")]
        public string Libelle
        {
            get => libelle;
            set => SetPropertyValue(nameof(Libelle), ref libelle, value?.Trim());
        }
        string libelle;

        [Size(300)]
        [XafDisplayName("Description")]
        public string Description
        {
            get => description;
            set => SetPropertyValue(nameof(Description), ref description, value?.Trim());
        }
        string description;

        [XafDisplayName("Actif")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif = true;

        public override string ToString() => Libelle;
    }
}
