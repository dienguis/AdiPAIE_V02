// =============================================================================
//  BusinessUnitType.cs
//  Référentiel partagé des « types » de Business Unit transverses aux stations.
//
//  Permet de regrouper toutes les BU « Boutique » (de toutes les stations) sous
//  une même catégorie pour les KPI dashboards. Avant cette entité, chaque BU
//  était propre à sa station (Boutique de MERMOZ ≠ Boutique de VDN) — le seul
//  lien transversal était le texte du Libelle, fragile aux variantes de saisie.
//
//  Seed initial (cf. Updater.cs / EnsureBusinessUnitTypesSeed) :
//    - BOUTIQUE
//    - PISTE
//    - E_SERVICE
//    - ESPACE_AUTO
//  Le métier peut ajouter d'autres types via l'écran XAF dédié sans
//  recompilation (c'est l'avantage clé vs un enum figé).
// =============================================================================

using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Palette de couleurs prédéfinies pour les bar charts dashboards.
    /// Chaque valeur a un libellé avec un carré coloré Unicode pour
    /// l'affichage visuel direct dans le dropdown XAF.
    /// </summary>
    public enum CouleurPalette
    {
        [XafDisplayName("⬛ Aucune (défaut navy)")]
        Aucune        = 0,
        [XafDisplayName("🟧 Orange ELTON")]
        OrangeElton   = 1,
        [XafDisplayName("🟦 Navy ELTON")]
        NavyElton     = 2,
        [XafDisplayName("🟥 Rouge ELTON")]
        RougeElton    = 3,
        [XafDisplayName("🟦 Bleu clair")]
        BleuClair     = 4,
        [XafDisplayName("🟩 Vert")]
        Vert          = 5,
        [XafDisplayName("🟪 Violet")]
        Violet        = 6,
        [XafDisplayName("🟨 Jaune")]
        Jaune         = 7,
        [XafDisplayName("🟫 Marron")]
        Marron        = 8,
        [XafDisplayName("🌸 Rose")]
        Rose          = 9,
        [XafDisplayName("🩶 Gris")]
        Gris          = 10
    }

    /// <summary>Helpers pour mapper l'enum CouleurPalette vers son code hex.</summary>
    public static class CouleurPaletteHelper
    {
        /// <summary>Code hexadécimal CSS associé à chaque couleur de la palette.</summary>
        public static string GetHex(CouleurPalette p) => p switch
        {
            CouleurPalette.OrangeElton => "#F18A1C",
            CouleurPalette.NavyElton   => "#142E4D",
            CouleurPalette.RougeElton  => "#E63946",
            CouleurPalette.BleuClair   => "#4DA3FF",
            CouleurPalette.Vert        => "#2EA75B",
            CouleurPalette.Violet      => "#A855F7",
            CouleurPalette.Jaune       => "#FCD34D",
            CouleurPalette.Marron      => "#92400E",
            CouleurPalette.Rose        => "#FF6FB5",
            CouleurPalette.Gris        => "#6B7686",
            _                          => "#142E4D"   // fallback navy
        };
    }

    /// <summary>
    /// Catalogue partagé des types de Business Unit (Boutique, Piste, etc.).
    /// Référentiel transversal — permet le regroupement des BU homonymes
    /// sur plusieurs stations pour les KPI agrégés.
    /// </summary>
    // ⚠️ V1.1 Sprint 1D — DEPRECATED. Remplacé par UniteOrganisationnelle.Palette
    // (enum CouleurPalette directement sur l'unité). Cette entité n'est plus
    // utilisée mais conservée pour ne pas perdre les éventuelles données qui
    // y sont déjà enregistrées. Masquée du menu XAF.
    [XafDisplayName("[Deprecated] Type de Business Unit")]
    [DefaultProperty(nameof(Libelle))]
    [ImageName("BO_Category")]
    [VisibleInReports(false)]
    public class BusinessUnitType : BaseObject
    {
        public BusinessUnitType(Session session) : base(session) { }

        /// <summary>
        /// Code court unique (BOUTIQUE, PISTE, E_SERVICE, ESPACE_AUTO…).
        /// Sert de clé technique pour les filtres dashboards.
        /// </summary>
        [RuleRequiredField]
        [Size(40)]
        [Indexed(Unique = true)]
        [RuleUniqueValue(DefaultContexts.Save,
            CustomMessageTemplate = "Ce code de type BU est déjà utilisé.")]
        [XafDisplayName("Code")]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code, value?.Trim().ToUpperInvariant());
        }
        string code;

        /// <summary>Libellé d'affichage (« Boutique », « Piste »…).</summary>
        [RuleRequiredField]
        [Size(120)]
        [XafDisplayName("Libellé")]
        public string Libelle
        {
            get => libelle;
            set => SetPropertyValue(nameof(Libelle), ref libelle, value?.Trim());
        }
        string libelle;

        /// <summary>
        /// Couleur d'accent pour les bar charts dashboards.
        /// L'enum est rendue par XAF en combobox avec un carré coloré
        /// Unicode dans le libellé pour visualiser le choix directement.
        /// Le code hex est dérivé via <see cref="CouleurHex"/>.
        /// </summary>
        [XafDisplayName("Couleur")]
        public CouleurPalette Palette
        {
            get => palette;
            set => SetPropertyValue(nameof(Palette), ref palette, value);
        }
        CouleurPalette palette = CouleurPalette.Aucune;

        /// <summary>
        /// Code hexadécimal CSS dérivé de <see cref="Palette"/>.
        /// Utilisé par les services dashboards pour styler les bar charts.
        /// Non persisté (calculé à la volée).
        /// </summary>
        [NonPersistent]
        [Browsable(false)]
        public string CouleurHex => CouleurPaletteHelper.GetHex(Palette);

        /// <summary>Type actif → visible dans les sélecteurs / filtres.</summary>
        [XafDisplayName("Actif")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif = true;

        /// <summary>
        /// Ordre d'affichage dans les listes / dashboards (0 = premier).
        /// </summary>
        [XafDisplayName("Ordre")]
        public int Ordre
        {
            get => ordre;
            set => SetPropertyValue(nameof(Ordre), ref ordre, value);
        }
        int ordre;

        // ── Association inverse : toutes les BU rattachées à ce type ────
        [Association("BUType-BUs")]
        [XafDisplayName("Business Units rattachées")]
        public XPCollection<BusinessUnitStation> BUs
            => GetCollection<BusinessUnitStation>(nameof(BUs));

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Libelle) ? Code : Libelle;
    }
}
