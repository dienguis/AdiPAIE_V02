// AdiPAIE_V02.Module/BusinessObjects/Site.cs
//
// Référentiel UNIFIÉ des sites de travail — V1.1 (refonte mai 2026).
//
// Sert pour TOUT le personnel :
//   - INTERNE (Salarie.Site)
//   - EXTERNE (ContratInterim.Site, MouvementInterimaire.SiteOrigine/Destination)
//
// Le champ Type permet de distinguer :
//   - StationService  : station service classique du réseau ELTON
//                       (BANDIA, MERMOZ, VDN, CAP DES BICHES…)
//                       Possède des BU enfants (Boutique / Piste / E-Service /
//                       Espace Auto) modélisées via UniteOrganisationnelle.
//   - Siege           : Direction Générale (1 seule en général).
//                       Possède des Departements enfants (DSI, Commerciale,
//                       Admin & Financière, RH…) modélisés via UniteOrganisationnelle.
//   - Depot           : entrepôt logistique (Dépôt Dakar, Dépôt Thiès…).
//   - Autre           : cas particuliers.
//
// Avant la V1.1 : il existait une entité distincte StationService — supprimée
// et migrée vers Site Type=StationService. Idem BusinessUnitStation → devient
// UniteOrganisationnelle Type=BU.
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    /// <summary>
    /// Type d'un Site (catégorie macro). Permet de distinguer les stations
    /// service du réseau, le siège (Direction Générale), les dépôts logistiques.
    /// </summary>
    public enum TypeSite
    {
        [XafDisplayName("🏪 Station service")]
        StationService = 0,
        [XafDisplayName("🏢 Siège (Direction Générale)")]
        Siege          = 1,
        [XafDisplayName("📦 Dépôt")]
        Depot          = 2,
        [XafDisplayName("🏭 Autre")]
        Autre          = 99
    }

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

        // ── ⭐ V1.1 : Type macro du site (StationService / Siège / Dépôt) ──
        [XafDisplayName("Type de site")]
        [ImmediatePostData]
        public TypeSite Type
        {
            get => type;
            set => SetPropertyValue(nameof(Type), ref type, value);
        }
        TypeSite type = TypeSite.StationService;

        // ── Association inverse : sous-structure organisationnelle ──
        // Pour une StationService → 4 BU (Boutique / Piste / E-Service / Espace Auto)
        // Pour un Siège           → N Départements (DSI / Commerciale / Admin & Fin / RH...)
        // Pour un Dépôt           → généralement aucune (mais possible)
        [Association("Site-Unites"), Aggregated]
        [XafDisplayName("Unités organisationnelles")]
        public XPCollection<UniteOrganisationnelle> Unites
            => GetCollection<UniteOrganisationnelle>(nameof(Unites));

        // ── Existant : salariés INTERNE rattachés au site ──
        [Association("Site-Salaries")]
        [XafDisplayName("Salariés")]
        public XPCollection<Salarie> Salaries => GetCollection<Salarie>(nameof(Salaries));

        // ── ⭐ V1.1 : contrats intérim sur ce site ──
        [Association("Site-ContratsInterim")]
        [XafDisplayName("Contrats intérim")]
        public XPCollection<ContratInterim> ContratsInterim
            => GetCollection<ContratInterim>(nameof(ContratsInterim));

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Code) ? Nom : $"{Nom} ({Code})";
    }
}
