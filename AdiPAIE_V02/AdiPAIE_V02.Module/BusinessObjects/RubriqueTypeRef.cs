// AdiPAIE_V02.Module/BusinessObjects/RubriqueTypeRef.cs
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [ImageName("BO_Type")]  // V1.1 — icône XAF native (type/référentiel)
    [DefaultProperty(nameof(Libelle))]
    [XafDisplayName("Type de rubrique")]
    public class RubriqueTypeRef : BaseObject
    {
        public RubriqueTypeRef(Session session) : base(session) { }

        // ── Identification ────────────────────────────────────────────────

        string code;
        [RuleRequiredField, Size(50)]
        [Indexed(Unique = true)]
        [RuleRegularExpression(@"^[A-Z0-9_]{2,20}$",
            CustomMessageTemplate = "Code en MAJUSCULES, 2–20 caractères, chiffres et underscore autorisés.")]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code, value);
        }

        string lib;
        [RuleRequiredField, Size(120)]
        public string Libelle
        {
            get => lib;
            set => SetPropertyValue(nameof(Libelle), ref lib, value);
        }

        // ── Groupe d'impression ───────────────────────────────────────────

        GroupeImpressionRef grp;
        [RuleRequiredField]
        [Association("GroupeImpressionRef-Types")]
        public GroupeImpressionRef Groupe
        {
            get => grp;
            set => SetPropertyValue(nameof(Groupe), ref grp, value);
        }

        // ── Défauts hérités par les rubriques ─────────────────────────────

        bool bf;
        bool bs;
        RubriqueTypeCalcul calc = RubriqueTypeCalcul.Gain;
        SensAssiette sens = SensAssiette.Plus;

        [ModelDefault("ImmediatePostData", "True")]
        [XafDisplayName("Brut Fiscal")]
        [ToolTip("Les rubriques de ce type entrent dans le Brut Fiscal")]
        public bool BruteFiscal
        {
            get => bf;
            set => SetPropertyValue(nameof(BruteFiscal), ref bf, value);
        }

        [ModelDefault("ImmediatePostData", "True")]
        [XafDisplayName("Brut Social")]
        [ToolTip("Les rubriques de ce type entrent dans le Brut Social")]
        public bool BruteSocial
        {
            get => bs;
            set => SetPropertyValue(nameof(BruteSocial), ref bs, value);
        }

        [ModelDefault("ImmediatePostData", "True")]
        [XafDisplayName("Type de calcul")]
        public RubriqueTypeCalcul DefaultTypeCalcul
        {
            get => calc;
            set => SetPropertyValue(nameof(DefaultTypeCalcul), ref calc, value);
        }

        [XafDisplayName("Sens assiette")]
        public SensAssiette DefaultSens
        {
            get => sens;
            set => SetPropertyValue(nameof(DefaultSens), ref sens, value);
        }

        bool actif = true;
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }

        // ── Déclaration 1024 (DGID Sénégal) ──────────────────────────────

        bool col13;
        [XafDisplayName("Colonne 13 — État 1024")]
        [ToolTip("Les montants de ce type entrent en colonne 13 de l'état 1024 : "
               + "Montant annuel des traitements, salaires et rémunérations imposables.")]
        [ModelDefault("ImmediatePostData", "True")]
        public bool EntreColonne13_1024
        {
            get => col13;
            set => SetPropertyValue(nameof(EntreColonne13_1024), ref col13, value);
        }

        bool col14;
        [XafDisplayName("Colonne 14 — État 1024")]
        [ToolTip("Les montants de ce type entrent en colonne 14 de l'état 1024 : "
               + "Évaluation des avantages en nature au barème forfaitaire.")]
        [ModelDefault("ImmediatePostData", "True")]
        public bool EntreColonne14_1024
        {
            get => col14;
            set => SetPropertyValue(nameof(EntreColonne14_1024), ref col14, value);
        }

        // ── Collection inverse ────────────────────────────────────────────

        [Association("TypeRef-Rubriques")]
        public XPCollection<Rubrique> Rubriques => GetCollection<Rubrique>(nameof(Rubriques));

        // ── Sauvegarde ────────────────────────────────────────────────────

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted)
            {
                Code = (Code ?? "").Trim();
                Libelle = (Libelle ?? "").Trim();
            }
        }
    }
}
