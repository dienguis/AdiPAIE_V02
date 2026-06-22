using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    //[DefaultClassOptions]
    [XafDisplayName("Ligne de bulletin")]
    [DefaultProperty(nameof(DisplayName))]
    [RuleCriteria("BL_Montants_NonNegatifs", DefaultContexts.Save,
        "Base >= 0 AND Montant >= 0 AND MontantEmployeur >= 0",
        CustomMessageTemplate = "Les montants d'une ligne de bulletin ne peuvent pas être négatifs.")]
    // ─────────────────────────────────────────────────────────────
    // V1.8 — Unicité (Bulletin, Rubrique)
    // Une même rubrique ne peut apparaître qu'une seule fois sur un
    // bulletin donné. Empêche les duplications accidentelles, notamment
    // via le popup "Saisir un congé" qui pourrait sinon créer plusieurs
    // lignes CONGE_PAYE / ICCP avec des motifs différents et cumuler à
    // tort l'allocation. Pour modifier un montant, supprimer la ligne
    // existante puis re-saisir, ou éditer directement la ligne.
    // ─────────────────────────────────────────────────────────────
    [RuleCombinationOfPropertiesIsUnique(
        "BulletinLigne_Bulletin_Rubrique_Unique", DefaultContexts.Save,
        "Bulletin;Rubrique",
        CustomMessageTemplate =
            "Une ligne avec la rubrique « {Rubrique} » existe déjà sur ce bulletin. " +
            "Pour modifier le montant, éditez la ligne existante. Pour la remplacer, " +
            "supprimez d'abord l'ancienne ligne puis créez la nouvelle.")]
    [Appearance(
    "BL_Disable_All_When_Not_Manual",
    // ➜ ligne non manuelle = grisée (lecture seule)
    Criteria = "Source <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+RubriqueSource,SaisieManuelle#",
    Enabled = false,
    TargetItems = "*"      // * = toutes les colonnes/éditeurs de la ligne
)]
    public class BulletinLigne : BaseObject
    {
        public BulletinLigne(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Source = RubriqueSource.SaisieManuelle; // ✅ ton enum
            IsSystem = false;
            Base = 0m;
            Montant = 0m;
            MontantEmployeur = 0m;
            // Taux reste nullable
        }

        // ============================
        // Liens
        // ============================
        [Association("Bulletin-Lignes"), RuleRequiredField]
        public Bulletin Bulletin
        {
            get => bulletin;
            set => SetPropertyValue(nameof(Bulletin), ref bulletin, value);
        }
        Bulletin bulletin;

        [RuleRequiredField]
        [ImmediatePostData]
        public Rubrique Rubrique
        {
            get => rubrique;
            set
            {
                if (SetPropertyValue(nameof(Rubrique), ref rubrique, value))
                {
                    // Hériter de l’ordre de la rubrique si non défini
                    if (!OrdreCalcul.HasValue && Rubrique?.OrdreAffichage != null)
                        OrdreCalcul = Rubrique.OrdreAffichage;
                }
            }
        }
        Rubrique rubrique;

        // ============================
        // Méta / organisation
        // ============================
        [XafDisplayName("Référence"), Size(100)]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value?.Trim());
        }
        string reference;

        [XafDisplayName("Ordre de calcul")]
        public int? OrdreCalcul
        {
            get => ordreCalcul;
            set => SetPropertyValue(nameof(OrdreCalcul), ref ordreCalcul, value);
        }
        int? ordreCalcul;



        [XafDisplayName("Provenance")]
        public RubriqueSource Source
        {
            get => source;
            set => SetPropertyValue(nameof(Source), ref source, value);
        }
        RubriqueSource source;

        [XafDisplayName("Ligne système")]
        public bool IsSystem
        {
            get => isSystem;
            set => SetPropertyValue(nameof(IsSystem), ref isSystem, value);
        }
        bool isSystem;

        [NonPersistent]
        public string DisplayName
            => Rubrique == null ? "(Rubrique ?)" : $"{Rubrique.Code} - {Rubrique.Libelle}";

        // ============================
        // Montants
        // ============================
        [ModelDefault("DisplayFormat", "n0"), ModelDefault("EditMask", "n0")]
        [DbType("decimal(18,0)")]
        public decimal Base
        {
            get => _base;
            set => SetPropertyValue(nameof(Base), ref _base, value);
        }
        decimal _base;

        [ModelDefault("DisplayFormat", "n2"), ModelDefault("EditMask", "n2")]
        [DbType("decimal(18,2)")]
        public decimal? Taux
        {
            get => taux;
            set => SetPropertyValue(nameof(Taux), ref taux, value);
        }
        decimal? taux;

        [ModelDefault("DisplayFormat", "n0"), ModelDefault("EditMask", "n0")]
        [DbType("decimal(18,0)")]
        public decimal Montant
        {
            get => montant;
            set => SetPropertyValue(nameof(Montant), ref montant, value);
        }
        decimal montant;

        [ModelDefault("DisplayFormat", "n0"), ModelDefault("EditMask", "n0")]
        [XafDisplayName("Montant employeur")]
        [DbType("decimal(18,0)")]
        public decimal MontantEmployeur
        {
            get => montantEmployeur;
            set => SetPropertyValue(nameof(MontantEmployeur), ref montantEmployeur, value);
        }
        decimal montantEmployeur;

        // ============================
        // Infos dérivées
        // ============================
        [PersistentAlias("Rubrique.TypeRef.Groupe.Libelle")]
        [XafDisplayName("Section")]
        public string SectionImpression
            => (string)EvaluateAlias(nameof(SectionImpression));

        // Ordre du groupe (groupe d'impression) pour un tri stable des sections
        [PersistentAlias("Rubrique.TypeRef.Groupe.OrdreGroupe")]
        [XafDisplayName("Ordre section")]
        public int? SectionOrdre => (int?)EvaluateAlias(nameof(SectionOrdre));

        // -> Utilise l'ordre numérique puis le libellé, donc le tri est stable.
    //    [PersistentAlias("Concat(FormatString('{0:000}', Rubrique.TypeRef.Groupe.OrdreGroupe), ' - ', Rubrique.TypeRef.Groupe.Libelle)")]
        // Clé triable "NNN - Libellé" sans FormatString
        [PersistentAlias(
            "Concat(" +
                "Iif(Rubrique.TypeRef.Groupe.OrdreGroupe < 10, " +
                    "Concat('00', ToStr(Rubrique.TypeRef.Groupe.OrdreGroupe)), " +
                    "Iif(Rubrique.TypeRef.Groupe.OrdreGroupe < 100, " +
                        "Concat('0', ToStr(Rubrique.TypeRef.Groupe.OrdreGroupe)), " +
                        "ToStr(Rubrique.TypeRef.Groupe.OrdreGroupe)" +
                    ")" +
                "), " +
                "' - ', Rubrique.TypeRef.Groupe.Libelle" +
            ")"
        )]
        [XafDisplayName("Section (clé)")]
        public string SectionImpressionKey => (string)EvaluateAlias(nameof(SectionImpressionKey));


        // Tri interne : OrdreCalcul si présent, sinon OrdreAffichage de la Rubrique
        [PersistentAlias("Iif(IsNull(OrdreCalcul), Rubrique.OrdreAffichage, OrdreCalcul)")]
        [XafDisplayName("Ordre ligne")]
        public int? OrdreLigne => (int?)EvaluateAlias(nameof(OrdreLigne));




        [PersistentAlias("Rubrique.Canonique")]
        [XafDisplayName("Rôle canonique")]
        public RubriqueCanonique? RoleCanonique
            => (RubriqueCanonique?)EvaluateAlias(nameof(RoleCanonique));

         [NonPersistent]
        [XafDisplayName("Type calcul")]
        public RubriqueTypeCalcul TypeCalcul => Rubrique?.TypeCalcul ?? RubriqueTypeCalcul.Gain;


        // Signe visuel
        [NonPersistent]
        [XafDisplayName("±")]
        public string Sens =>
            Rubrique?.TypeCalcul switch
            {
                RubriqueTypeCalcul.Gain => "+",
                //RubriqueTypeCalcul.Indemnite => "+",
                RubriqueTypeCalcul.Retenue => "-",
                _ => ""
            };
        // Dans class BulletinLigne

        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnChanged(propertyName, oldValue, newValue);

            if (propertyName == nameof(Rubrique) && Rubrique != null)
            {
                // 👉 Ordre de calcul hérité de la rubrique, sinon prochain créneau
                if (!OrdreCalcul.HasValue || OrdreCalcul.Value <= 0)
                    OrdreCalcul = Rubrique.OrdreAffichage ?? CalcNextOrdreCalcul();
            }
        }

        private int CalcNextOrdreCalcul()
        {
            var max = Bulletin?.Lignes?
                .Where(x => !Equals(x, this) && x.OrdreCalcul.HasValue)
                .Select(x => x.OrdreCalcul.Value)
                .DefaultIfEmpty(0)
                .Max() ?? 0;
            return max + 10;
        }

        // ============================
        // Garde-fous
        // ============================
        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted)
            {
                if (Bulletin == null)
                    throw new UserFriendlyException("La ligne doit être rattachée à un bulletin.");
                if (Rubrique == null)
                    throw new UserFriendlyException("La ligne doit référencer une rubrique.");
            }
        }
    }
}
