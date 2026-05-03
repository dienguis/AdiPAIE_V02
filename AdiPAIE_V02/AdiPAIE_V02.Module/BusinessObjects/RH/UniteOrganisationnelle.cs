// =============================================================================
//  UniteOrganisationnelle.cs — V1.1 (mai 2026)
//
//  Entité hiérarchique récursive représentant la sous-structure d'un Site.
//  Permet de modéliser :
//    - Les BU des stations service (Boutique, Piste, E-Service, Espace Auto)
//    - Les Départements du Siège (DSI, Commerciale, Admin & Fin, RH…)
//    - Les Segments commerciaux (Consommateurs, BTP, Mines… sous Direction
//      Commerciale)
//    - Toute autre sous-division métier (ad-hoc via Type=Autre)
//
//  Hiérarchie ILLIMITÉE via la FK Parent (self-référence) :
//
//    Site "Siège" (TypeSite=Siege)
//      └── Unite "DSI"                       (Type=Departement, Parent=null, Niveau=1)
//      └── Unite "Direction Commerciale"     (Type=Departement, Parent=null, Niveau=1)
//            └── Unite "Segment Consommateurs" (Type=Segment, Parent=DirComm, Niveau=2)
//            └── Unite "Segment BTP"            (Type=Segment, Parent=DirComm, Niveau=2)
//            └── Unite "Segment Mines"          (Type=Segment, Parent=DirComm, Niveau=2)
//
//  Multi-affectation des intérimaires : un ContratInterim peut être lié à
//  plusieurs unités via l'association N-N "Contrat-Unites" (sans % de temps,
//  cf. spec ELTON V1.1).
// =============================================================================

using System.ComponentModel;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Type d'une unité organisationnelle (catégorie au sein d'un Site).
    /// Un Site StationService aura typiquement des unités Type=BU.
    /// Un Site Siège aura des unités Type=Departement (avec parfois des
    /// enfants Type=Segment).
    /// </summary>
    public enum TypeUnite
    {
        [XafDisplayName("BU (Business Unit)")]
        BU          = 0,
        [XafDisplayName("Département")]
        Departement = 1,
        [XafDisplayName("Segment")]
        Segment     = 2,
        [XafDisplayName("Autre")]
        Autre       = 99
    }

    [DefaultClassOptions]
    [XafDisplayName("Unité organisationnelle")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Department")]
    [NavigationItem("GRH - Administration")]
    public class UniteOrganisationnelle : BaseObject
    {
        public UniteOrganisationnelle(Session session) : base(session) { }

        // ── Site racine (obligatoire) ─────────────────────────────────
        [Association("Site-Unites")]
        [RuleRequiredField]
        [XafDisplayName("Site")]
        [ImmediatePostData]
        public Site Site
        {
            get => site;
            set => SetPropertyValue(nameof(Site), ref site, value);
        }
        Site site;

        // ── Identification ────────────────────────────────────────────
        [RuleRequiredField]
        [Size(60)]
        [XafDisplayName("Code")]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code, value?.Trim().ToUpperInvariant());
        }
        string code;

        [RuleRequiredField]
        [Size(120)]
        [XafDisplayName("Nom")]
        public string Nom
        {
            get => nom;
            set => SetPropertyValue(nameof(Nom), ref nom, value?.Trim());
        }
        string nom;

        [RuleRequiredField]
        [XafDisplayName("Type d'unité")]
        public TypeUnite TypeUnite
        {
            get => typeUnite;
            set => SetPropertyValue(nameof(TypeUnite), ref typeUnite, value);
        }
        TypeUnite typeUnite = TypeUnite.BU;

        // ── Hiérarchie récursive (self-référence) ─────────────────────
        [Association("Unite-Enfants")]
        [XafDisplayName("Parent")]
        [DataSourceCriteria("Site = '@This.Site' AND Oid <> '@This.Oid'")]
        public UniteOrganisationnelle Parent
        {
            get => parent;
            set => SetPropertyValue(nameof(Parent), ref parent, value);
        }
        UniteOrganisationnelle parent;

        [Association("Unite-Enfants")]
        [XafDisplayName("Sous-unités")]
        public XPCollection<UniteOrganisationnelle> Enfants
            => GetCollection<UniteOrganisationnelle>(nameof(Enfants));

        // ── Métadonnées ───────────────────────────────────────────────
        [XafDisplayName("Couleur")]
        public CouleurPalette Palette
        {
            get => palette;
            set => SetPropertyValue(nameof(Palette), ref palette, value);
        }
        CouleurPalette palette = CouleurPalette.Aucune;

        /// <summary>Code hex dérivé via <see cref="CouleurPaletteHelper.GetHex"/>.</summary>
        [NonPersistent, Browsable(false)]
        public string CouleurHex => CouleurPaletteHelper.GetHex(Palette);

        [XafDisplayName("Ordre")]
        public int Ordre
        {
            get => ordre;
            set => SetPropertyValue(nameof(Ordre), ref ordre, value);
        }
        int ordre;

        [XafDisplayName("Actif")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif = true;

        // ── Multi-affectation des intérimaires (N-N) ──────────────────
        [Association("Contrat-Unites")]
        [XafDisplayName("Contrats intérim")]
        public XPCollection<ContratInterim> ContratsInterim
            => GetCollection<ContratInterim>(nameof(ContratsInterim));

        // ── Helpers calculés ──────────────────────────────────────────
        /// <summary>
        /// Niveau dans la hiérarchie (1 = top sous le Site, 2 = sub, etc.).
        /// </summary>
        [NonPersistent]
        [XafDisplayName("Niveau")]
        public int Niveau => Parent == null ? 1 : Parent.Niveau + 1;

        /// <summary>Chemin complet "Site › Parent › Unité" pour l'affichage.</summary>
        [NonPersistent]
        [XafDisplayName("Chemin")]
        public string Chemin
        {
            get
            {
                var siteName = Site?.Nom ?? "(?)";
                if (Parent == null) return $"{siteName} › {Nom}";
                return $"{Parent.Chemin} › {Nom}";
            }
        }

        /// <summary>Nom court avec type entre parenthèses pour les listes.</summary>
        [NonPersistent]
        [XafDisplayName("Affichage")]
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Nom)) return Code ?? "(nouveau)";
                return Site != null ? $"{Nom} — {Site.Nom}" : Nom;
            }
        }

        public override string ToString() => DisplayName;
    }
}
