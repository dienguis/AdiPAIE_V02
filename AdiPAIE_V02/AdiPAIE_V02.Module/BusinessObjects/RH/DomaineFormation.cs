using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Référentiel des domaines de formation.
    /// Exemples : Sécurité, Management, Informatique, Langues…
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Domaine de formation")]
    [DefaultProperty(nameof(Libelle))]
    [ImageName("BO_Category")]
  //  [NavigationItem("GRH - Formation")]
    public class DomaineFormation : BaseObject
    {
        public DomaineFormation(Session session) : base(session) { }

        // ── Identification ────────────────────────────────────
        string code;
        [RuleRequiredField]
        [Size(20)]
        [XafDisplayName("Code")]
        [RuleUniqueValue]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code, value?.Trim().ToUpper());
        }

        string libelle;
        [RuleRequiredField]
        [Size(150)]
        [XafDisplayName("Libellé")]
        public string Libelle
        {
            get => libelle;
            set => SetPropertyValue(nameof(Libelle), ref libelle, value?.Trim());
        }

        FormationCategorie categorie;
        [XafDisplayName("Catégorie")]
        public FormationCategorie Categorie
        {
            get => categorie;
            set => SetPropertyValue(nameof(Categorie), ref categorie, value);
        }

        string description;
        [Size(500)]
        [XafDisplayName("Description")]
        public string Description
        {
            get => description;
            set => SetPropertyValue(nameof(Description), ref description, value?.Trim());
        }

        bool actif;
        [XafDisplayName("Actif")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }

        // ── Collection inverse ────────────────────────────────
        [Association("DomaineFormation-Sessions")]
        [XafDisplayName("Sessions associées")]
        public XPCollection<SessionFormation> Sessions
            => GetCollection<SessionFormation>(nameof(Sessions));

        // ── Cycle de vie ──────────────────────────────────────
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Actif = true;
        }
    }
}
