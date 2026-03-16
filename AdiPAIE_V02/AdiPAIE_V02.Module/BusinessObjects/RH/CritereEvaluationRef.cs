using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Référentiel des critères d'évaluation paramétrables.
    /// Chaque critère peut être pondéré et typé (compétence, comportement, objectif).
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Critères d'évaluation")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_List")]
    [NavigationItem("GRH - Évaluation")]
    public class CritereEvaluationRef : BaseObject
    {
        public CritereEvaluationRef(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Actif = true;
            Poids = 1m;
        }

        // ── Identité ──────────────────────────────────────────────
        string code;
        [RuleRequiredField]
        [Size(20)]
        [Indexed(Unique = true)]
        [RuleRegularExpression(@"^[A-Z0-9_]{2,20}$",
            CustomMessageTemplate = "Code en MAJUSCULES, 2–20 caractères (A-Z, 0-9, _).")]
        [XafDisplayName("Code")]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code, value?.Trim()?.ToUpperInvariant());
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

        [PersistentAlias("Concat(Code, ' - ', Libelle)")]
        [VisibleInListView(false), VisibleInDetailView(false)]
        [VisibleInLookupListView(true)]
        public string DisplayName => Convert.ToString(EvaluateAlias(nameof(DisplayName)));

        // ── Paramétrage ───────────────────────────────────────────
        TypeCritere typeCritere;
        [XafDisplayName("Type de critère")]
        public TypeCritere TypeCritere
        {
            get => typeCritere;
            set => SetPropertyValue(nameof(TypeCritere), ref typeCritere, value);
        }

        decimal poids;
        [RuleRange(0.1, 10)]
        [ModelDefault("DisplayFormat", "N1")]
        [ModelDefault("EditMask", "N1")]
        [XafDisplayName("Pondération (coeff.)")]
        public decimal Poids
        {
            get => poids;
            set => SetPropertyValue(nameof(Poids), ref poids, value);
        }

        string description;
        [Size(512)]
        [XafDisplayName("Description / critères d'appréciation")]
        public string Description
        {
            get => description;
            set => SetPropertyValue(nameof(Description), ref description, value?.Trim());
        }

        int ordre;
        [XafDisplayName("Ordre d'affichage")]
        public int Ordre
        {
            get => ordre;
            set => SetPropertyValue(nameof(Ordre), ref ordre, value);
        }

        bool actif;
        [XafDisplayName("Actif")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
    }
}
