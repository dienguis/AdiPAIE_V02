using AdiPAIE_V02.Module.Domain;
using DevExpress.Data.Filtering;
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
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    /// <summary>
    /// V1.7.2 — Gratification (= prime exceptionnelle) ad hoc.
    ///
    /// Règles ELTON (cf. grille RH validée 2026-05-11) :
    ///   - Décision discrétionnaire DG (lien évaluations en V1.8+)
    ///   - Base au choix : multiple du Brut récurrent, multiple du Net, ou forfait
    ///   - Multiplicateur libre (variable selon situation)
    ///   - Sans fréquence (ad hoc)
    ///   - Soumise intégralement IR + CSS + IPRES + IPM (comme 13ième)
    ///   - Workflow : RH saisit → DAF valide → RH intègre au bulletin
    ///
    /// Granularité : 1 ligne par (Salarié × Année × Mois × ... )
    /// → pas d'unicité stricte car un salarié peut recevoir 2 gratifications
    /// la même année (ex : prime mi-année + prime fin d'année).
    /// </summary>
    [DefaultClassOptions]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Money")]
    [OptimisticLocking(true)]
    // ── Badges colorés sur le statut (5 états) ──────────────────────
    [Appearance("Gratif_Statut_Brouillon",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,BrouillonRH#",
        BackColor = "Gainsboro", FontColor = "DimGray",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Gratif_Statut_EnAttente",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,EnAttenteValidationDAF#",
        BackColor = "LightYellow", FontColor = "DarkGoldenrod",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Gratif_Statut_Validee",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,ValideeDAF#",
        BackColor = "LightSkyBlue", FontColor = "DarkBlue",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Gratif_Statut_Integree",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,IntegreeBulletin#",
        BackColor = "PaleGreen", FontColor = "DarkGreen",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Gratif_Statut_Payee",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,Payee#",
        BackColor = "MediumSeaGreen", FontColor = "White",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Gratif_Statut_Annule",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,Annule#",
        BackColor = "LightCoral", FontColor = "DarkRed",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    // ── Lecture seule après validation DAF ──────────────────────────
    [Appearance("Gratif_ReadOnly_ApresValidation",
        TargetItems = "Salarie;BaseCalcul;Multiplicateur;MontantForfait",
        Criteria = "Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,BrouillonRH# " +
                   "AND Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,EnAttenteValidationDAF#",
        Enabled = false,
        Context = "DetailView")]
    // ── Tout en lecture seule après intégration ─────────────────────
    [Appearance("Gratif_FullReadOnly_Apres_Integration",
        TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,IntegreeBulletin# " +
                   "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,Payee#",
        Enabled = false,
        Context = "DetailView")]
    public class Gratification : BaseObject
    {
        public Gratification(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = GratificationStatut.BrouillonRH;
            DateCreation = DateTime.Now;
            try { CreePar = DevExpress.ExpressApp.SecuritySystem.CurrentUserName; } catch { }
            BaseCalcul = GratificationBaseCalcul.BrutRecurrent;
            Multiplicateur = 1m;
            Annee = DateTime.Today.Year;
            MoisPaiement = DateTime.Today.Month;
        }

        // ─────────────────────────────────────────────────────────
        // Identification / contexte
        // ─────────────────────────────────────────────────────────
        int annee;
        [RuleRequiredField]
        [RuleRange(2000, 2100,
            CustomMessageTemplate = "Année incohérente ({Annee}).")]
        [XafDisplayName("Année")]
        [ModelDefault("DisplayFormat", "{0:####}")]
        [ModelDefault("EditMask", "####")]
        public int Annee
        {
            get => annee;
            set => SetPropertyValue(nameof(Annee), ref annee, value);
        }

        int moisPaiement;
        [RuleRequiredField]
        [RuleRange(1, 12,
            CustomMessageTemplate = "Mois entre 1 et 12 ({MoisPaiement}).")]
        [XafDisplayName("Mois paiement")]
        [ToolTip("Mois civil sur lequel la gratification sera intégrée au bulletin.")]
        public int MoisPaiement
        {
            get => moisPaiement;
            set => SetPropertyValue(nameof(MoisPaiement), ref moisPaiement, value);
        }

        Salarie salarie;
        [RuleRequiredField]
        [Association("Salarie-Gratifications")]
        [XafDisplayName("Salarié")]
        [ModelDefault("ImmediatePostData", "True")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }

        // ─────────────────────────────────────────────────────────
        // Paramètres de calcul (saisis par RH)
        // ─────────────────────────────────────────────────────────
        GratificationBaseCalcul baseCalcul;
        [RuleRequiredField]
        [XafDisplayName("Base de calcul")]
        [ModelDefault("ImmediatePostData", "True")]
        [ToolTip("BrutRecurrent : multiple du brut hors congés. " +
                 "NetRecurrent : multiple du net mensuel normal. " +
                 "Forfait : montant fixe en FCFA (Multiplicateur ignoré).")]
        public GratificationBaseCalcul BaseCalcul
        {
            get => baseCalcul;
            set => SetPropertyValue(nameof(BaseCalcul), ref baseCalcul, value);
        }

        decimal multiplicateur;
        [XafDisplayName("Multiplicateur")]
        [ModelDefault("DisplayFormat", "{0:N2}")]
        [ToolTip("Multiple du salaire (ex 2,5 = 2,5 mois). " +
                 "Ignoré si BaseCalcul = Forfait.")]
        public decimal Multiplicateur
        {
            get => multiplicateur;
            set => SetPropertyValue(nameof(Multiplicateur), ref multiplicateur, value);
        }

        decimal montantForfait;
        [XafDisplayName("Montant forfait")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ToolTip("Montant fixe en FCFA si BaseCalcul = Forfait. " +
                 "Sinon laisser à 0.")]
        public decimal MontantForfait
        {
            get => montantForfait;
            set => SetPropertyValue(nameof(MontantForfait), ref montantForfait, value);
        }

        // ─────────────────────────────────────────────────────────
        // Résultat du calcul (figé au moment de la soumission)
        // ─────────────────────────────────────────────────────────
        decimal baseReference;
        [XafDisplayName("Base réf. (snapshot)")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ToolTip("Snapshot de la base utilisée au moment du calcul : " +
                 "BR récurrent OU Net mensuel OU 0 si Forfait.")]
        public decimal BaseReference
        {
            get => baseReference;
            set => SetPropertyValue(nameof(BaseReference), ref baseReference, value);
        }

        decimal montantCalcule;
        [XafDisplayName("Montant brut calculé")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ToolTip("Montant final = BaseReference × Multiplicateur (ou Forfait).")]
        public decimal MontantCalcule
        {
            get => montantCalcule;
            set => SetPropertyValue(nameof(MontantCalcule), ref montantCalcule, value);
        }

        // ─────────────────────────────────────────────────────────
        // Workflow
        // ─────────────────────────────────────────────────────────
        GratificationStatut statut;
        [RuleRequiredField]
        [XafDisplayName("Statut")]
        public GratificationStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        Bulletin bulletinLie;
        [VisibleInListView(false)]
        [XafDisplayName("Bulletin de versement")]
        public Bulletin BulletinLie
        {
            get => bulletinLie;
            set => SetPropertyValue(nameof(BulletinLie), ref bulletinLie, value);
        }

        // ─────────────────────────────────────────────────────────
        // Traçabilité (5 dates + 3 utilisateurs)
        // ─────────────────────────────────────────────────────────
        DateTime dateCreation;
        [VisibleInListView(false)]
        [XafDisplayName("Date saisie")]
        public DateTime DateCreation
        {
            get => dateCreation;
            set => SetPropertyValue(nameof(DateCreation), ref dateCreation, value);
        }

        string creePar;
        [Size(100), VisibleInListView(false)]
        [XafDisplayName("Saisi par (RH)")]
        public string CreePar
        {
            get => creePar;
            set => SetPropertyValue(nameof(CreePar), ref creePar, value);
        }

        DateTime? dateSoumission;
        [VisibleInListView(false)]
        [XafDisplayName("Date soumission DAF")]
        public DateTime? DateSoumission
        {
            get => dateSoumission;
            set => SetPropertyValue(nameof(DateSoumission), ref dateSoumission, value);
        }

        DateTime? dateValidationDAF;
        [VisibleInListView(false)]
        [XafDisplayName("Date validation DAF")]
        public DateTime? DateValidationDAF
        {
            get => dateValidationDAF;
            set => SetPropertyValue(nameof(DateValidationDAF), ref dateValidationDAF, value);
        }

        string validePar;
        [Size(100), VisibleInListView(false)]
        [XafDisplayName("Validé par (DAF)")]
        public string ValidePar
        {
            get => validePar;
            set => SetPropertyValue(nameof(ValidePar), ref validePar, value);
        }

        DateTime? dateIntegration;
        [VisibleInListView(false)]
        [XafDisplayName("Date intégration bulletin")]
        public DateTime? DateIntegration
        {
            get => dateIntegration;
            set => SetPropertyValue(nameof(DateIntegration), ref dateIntegration, value);
        }

        string integrePar;
        [Size(100), VisibleInListView(false)]
        [XafDisplayName("Intégré par (RH)")]
        public string IntegrePar
        {
            get => integrePar;
            set => SetPropertyValue(nameof(IntegrePar), ref integrePar, value);
        }

        string commentaire;
        [Size(500)]
        [XafDisplayName("Motif / commentaire")]
        [ToolTip("Justification de la gratification (résultat exceptionnel, " +
                 "événement, etc.). Visible par DAF lors de la validation.")]
        public string Commentaire
        {
            get => commentaire;
            set => SetPropertyValue(nameof(Commentaire), ref commentaire, value);
        }

        // ─────────────────────────────────────────────────────────
        // Affichage
        // ─────────────────────────────────────────────────────────
        [PersistentAlias(
            "Concat(IsNull(Salarie.Matricule, '?'), ' - ', " +
            "IsNull(Salarie.FullName, '?'), ' / Gratif ', " +
            "ToStr(Annee), '/', ToStr(MoisPaiement))")]
        [VisibleInListView(false), VisibleInDetailView(false)]
        [VisibleInLookupListView(true)]
        public string DisplayName => Convert.ToString(EvaluateAlias(nameof(DisplayName)));
    }
}
