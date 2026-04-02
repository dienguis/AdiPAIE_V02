using DevExpress.ExpressApp.ConditionalAppearance;
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
    /// Référentiel des catégories de frais de mission.
    /// Exemples : Carburant, Péage, Repas, Hébergement, Per diem, Divers.
    /// Le taux journalier est modifiable à la saisie de chaque demande.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Catégorie de frais de mission")]
    [DefaultProperty(nameof(Libelle))]
    [ImageName("BO_List")]
   // [NavigationItem("GRH - Administration")]

    [Appearance("Inactif_Grise", Criteria = "Actif = false",
        FontColor = "Gray", FontStyle = DevExpress.Drawing.DXFontStyle.Italic,
        TargetItems = "*")]
    public class CategorieFraisMission : BaseObject
    {
        public CategorieFraisMission(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Actif = true;
            ModeCalcul = FraisCalculMode.TauxJournalier;
            TauxDefaut = 0m;
            OrdreAffichage = 10;
        }

        // ── Identification ─────────────────────────────────────
        [RuleRequiredField]
        [Size(20)]
        [Indexed(Unique = true)]
        [XafDisplayName("Code")]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code,
                value?.Trim().ToUpperInvariant());
        }
        string code;

        [RuleRequiredField]
        [Size(100)]
        [XafDisplayName("Libellé")]
        public string Libelle
        {
            get => libelle;
            set => SetPropertyValue(nameof(Libelle), ref libelle, value?.Trim());
        }
        string libelle;

        // ── Mode de calcul ─────────────────────────────────────
        [XafDisplayName("Mode de calcul")]
        [ImmediatePostData]
        public FraisCalculMode ModeCalcul
        {
            get => modeCalcul;
            set => SetPropertyValue(nameof(ModeCalcul), ref modeCalcul, value);
        }
        FraisCalculMode modeCalcul;

        // ── Taux / montant par défaut ──────────────────────────
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        [XafDisplayName("Taux / Montant par défaut (FCFA)")]
        [ToolTip("Pour TauxJournalier : montant par jour. Pour Forfait : montant fixe. Pour Kilométrique : montant par km.")]
        public decimal TauxDefaut
        {
            get => tauxDefaut;
            set => SetPropertyValue(nameof(TauxDefaut), ref tauxDefaut, value);
        }
        decimal tauxDefaut;

        // ── Options ────────────────────────────────────────────
        [XafDisplayName("Ordre d'affichage")]
        public int OrdreAffichage
        {
            get => ordreAffichage;
            set => SetPropertyValue(nameof(OrdreAffichage), ref ordreAffichage, value);
        }
        int ordreAffichage;

        [XafDisplayName("Actif")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif;

        [Size(300)]
        [XafDisplayName("Description")]
        public string Description
        {
            get => description;
            set => SetPropertyValue(nameof(Description), ref description, value?.Trim());
        }
        string description;
    }
}
