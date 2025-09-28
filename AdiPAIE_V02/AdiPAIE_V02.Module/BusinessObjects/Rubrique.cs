using AdiPAIE_V02.Module.Utils;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    //[NavigationItem("Référentiel")]
    [DefaultProperty(nameof(DisplayName))]
    public class Rubrique : BaseObject
    {
        public Rubrique(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Option simple : si TypeRef est déjà renseigné (clonage/import), applique les défauts
            if (TypeRef != null)
                AppliquerDefautsDuType();
        }

        // ----------------------
        // Identité / affichage
        // ----------------------
        string code;
        [RuleRequiredField, Size(20)]
        [Indexed(Unique = true)]
        [RuleRegularExpression(@"^[A-Z0-9_]{2,20}$",
            CustomMessageTemplate = "Code en MAJUSCULES, 2–20 caractères (A-Z, 0-9, _).")]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code, value?.Trim()?.ToUpperInvariant());
        }

        string libelle;
        [RuleRequiredField, Size(100)]
    public string Libelle
    {
        get => libelle;
        set
        {
            var v = value?.Trim();
            if (!string.IsNullOrEmpty(v))
                v = TextCaseFr.ToTitleCaseFrPreserveAcronyms(v); // 👈 normalise tout de suite
            SetPropertyValue(nameof(Libelle), ref libelle, v);
        }
    }


    [PersistentAlias("Concat(Code, ' - ', Libelle)")]
        [VisibleInListView(false), VisibleInDetailView(false)]
        [VisibleInLookupListView(true)]
        public string DisplayName => Convert.ToString(EvaluateAlias(nameof(DisplayName)));

        // ----------------------
        // Référentiel de type
        // ----------------------
        RubriqueTypeRef typeRef;

        // ⚠ Assoc côté TypeRef : [Association("TypeRef-Rubriques")] public XPCollection<Rubrique> Rubriques { … }
        [RuleRequiredField, Association("TypeRef-Rubriques")]
        [ModelDefault("ImmediatePostData", "True")]
        [VisibleInLookupListView(false)]
        public RubriqueTypeRef TypeRef
        {
            get => typeRef;
            set => SetPropertyValue(nameof(TypeRef), ref typeRef, value);
        }

        // Option simple : applique les défauts à CHAQUE changement de TypeRef
        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnChanged(propertyName, oldValue, newValue);

            if (propertyName == nameof(TypeRef) && !IsLoading && !IsSaving && TypeRef != null)
                AppliquerDefautsDuType();


        }

        // ---------------------------
        // Valeurs persistées
        // ---------------------------
        RubriqueTypeCalcul typeCalcul;
        [VisibleInLookupListView(false)]
        public RubriqueTypeCalcul TypeCalcul
        {
            get => typeCalcul;
            set => SetPropertyValue(nameof(TypeCalcul), ref typeCalcul, value);
        }

        bool brutFiscal;
        [VisibleInLookupListView(false)]
        public bool BrutFiscal
        {
            get => brutFiscal;
            set => SetPropertyValue(nameof(BrutFiscal), ref brutFiscal, value);
        }

        bool brutSocial;
        [VisibleInLookupListView(false)]
        public bool BrutSocial
        {
            get => brutSocial;
            set => SetPropertyValue(nameof(BrutSocial), ref brutSocial, value);
        }

        int? ordreAffichage;
        [XafDisplayName("Ordre (impr.)")]
        [VisibleInLookupListView(true)]
        public int? OrdreAffichage
        {
            get => ordreAffichage;
            set => SetPropertyValue(nameof(OrdreAffichage), ref ordreAffichage, value);
        }

        bool actif = true;
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }

        // Lecture seule pour regroupement d'impression
        [PersistentAlias("TypeRef.Groupe.Libelle")]
        [XafDisplayName("Section")]
        [VisibleInLookupListView(true)]
        public string SectionImpression => Convert.ToString(EvaluateAlias(nameof(SectionImpression)));

        // ---------------------------
        // Action utilitaire
        // ---------------------------
        [Action(Caption = "Reprendre défauts du type", ImageName = "Action_Reset", AutoCommit = true)]
        public void ReprendreDefauts()
        {
            if (TypeRef == null)
                throw new UserFriendlyException("Aucun Type de rubrique sélectionné.");
            AppliquerDefautsDuType();
        }

        private void AppliquerDefautsDuType()
        {
            // Option simple : projection systématique des défauts
            TypeCalcul = TypeRef.DefaultTypeCalcul;
            BrutFiscal = TypeRef.BruteFiscal;
            BrutSocial = TypeRef.BruteSocial;
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted && TypeRef == null)
                throw new UserFriendlyException("Veuillez choisir un Type de rubrique.");

            // Unicité logique d’un rôle canonique (si utilisé)
            if (Canonique.HasValue)
            {
                var duplicate = Session.Query<Rubrique>()
                    .FirstOrDefault(r => r.Oid != Oid && r.Canonique == Canonique.Value);
                if (duplicate != null)
                    throw new UserFriendlyException(
                        $"Une rubrique canonique '{Canonique}' existe déjà : {duplicate.Code} - {duplicate.Libelle}.");
            }

            if (!string.IsNullOrWhiteSpace(Libelle))
            {
                var norm = TextCaseFr.ToTitleCaseFrPreserveAcronyms(Libelle);
                if (!string.Equals(Libelle, norm, StringComparison.Ordinal))
                    Libelle = norm;
            }

        }

        // ---------------------------
        // Taux / Plafond (optionnels)
        // ---------------------------
        decimal? taux1;
        [VisibleInLookupListView(false)]
        [DbType("decimal(18,2)")]
        [EditorAlias(EditorAliases.DecimalPropertyEditor)]
        [ModelDefault("DisplayFormat", "##0.##' %'")]   // affichage → 12,34 %
        [ModelDefault("EditMask", "n2")]
        public decimal? Taux1
        {
            get => taux1;
            set => SetPropertyValue(nameof(Taux1), ref taux1, value);
        }

        decimal? taux2;
        [VisibleInLookupListView(false)]
        [DbType("decimal(18,2)")]
        [EditorAlias(EditorAliases.DecimalPropertyEditor)]
        [ModelDefault("DisplayFormat", "##0.##' %'")]   // affichage → 12,34 %
        [ModelDefault("EditMask", "n2")]
        public decimal? Taux2
        {
            get => taux2;
            set => SetPropertyValue(nameof(Taux2), ref taux2, value);
        }

        decimal? plafond;
        [VisibleInLookupListView(false)]
        [DbType("decimal(18,0)")]
        [EditorAlias(EditorAliases.DecimalPropertyEditor)]
        public decimal? Plafond
        {
            get => plafond;
            set => SetPropertyValue(nameof(Plafond), ref plafond, value);
        }

        // ---------------------------
        // Comptes par défaut
        // ---------------------------
        PlanComptable compteDebitDefaut;
        [VisibleInLookupListView(false)]
        public PlanComptable CompteDebitDefaut
        {
            get => compteDebitDefaut;
            set => SetPropertyValue(nameof(CompteDebitDefaut), ref compteDebitDefaut, value);
        }

        PlanComptable compteCreditDefaut;
        [VisibleInLookupListView(false)]
        public PlanComptable CompteCreditDefaut
        {
            get => compteCreditDefaut;
            set => SetPropertyValue(nameof(CompteCreditDefaut), ref compteCreditDefaut, value);
        }

        // Comptes “tiers” liés aux taux (ex: IPRES RG/RC, IR, etc.)
        PlanComptable compteTaux1;
        [VisibleInLookupListView(false)]
        public PlanComptable CompteTaux1
        {
            get => compteTaux1;
            set => SetPropertyValue(nameof(CompteTaux1), ref compteTaux1, value);
        }

        PlanComptable compteTaux2;
        [VisibleInLookupListView(false)]
        public PlanComptable CompteTaux2
        {
            get => compteTaux2;
            set => SetPropertyValue(nameof(CompteTaux2), ref compteTaux2, value);
        }

        // ---------------------------
        // Rôle canonique (optionnel)
        // ---------------------------
        [XafDisplayName("Rôle canonique")]
        public RubriqueCanonique? Canonique
        {
            get => canon;
            set => SetPropertyValue(nameof(Canonique), ref canon, value);
        }
        RubriqueCanonique? canon;

        // Griser quand canonique (évitons les dérives)
        [Appearance("LockCoreWhenCanonical",
            Criteria = "Not IsNull(Canonique)", Enabled = false,
            TargetItems = "TypeRef,TypeCalcul,BrutFiscal,BrutSocial")]
        public int _uiLockHelper { get; set; }

        // ---------------------------
        // Overrides par rubrique
        // ---------------------------
        // Variante 2 : même nom d’association des deux côtés
        [Association("Rubrique-RubriqueComptes"), Aggregated]
        public XPCollection<RubriqueCompte> RubriqueComptes
            => GetCollection<RubriqueCompte>(nameof(RubriqueComptes));

    
       
        

    }
}
