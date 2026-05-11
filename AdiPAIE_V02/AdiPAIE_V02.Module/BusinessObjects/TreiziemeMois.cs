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
    /// V1.7.2 — Calcul annuel du 13ième mois pour un salarié donné.
    ///
    /// Règles ELTON (cf. grille RH validée le 2026-05-11) :
    ///   - Droit conventionnel automatique pour tous les salariés actifs
    ///   - Calcul = BrutRecurrent × MoisPresence / 12
    ///   - Base = dernier brut récurrent perçu (hors congés et exceptionnels)
    ///   - Versé sur bulletin de décembre (cas normal) OU sur STC (départ)
    ///   - Soumis intégralement IR + CSS + IPRES + IPM
    ///   - Pas de lissage fiscal
    ///
    /// Entité PERSISTANTE (table TreiziemeMois) car on a besoin de tracer
    /// le statut, le montant figé, le bulletin de versement, etc.
    ///
    /// Unicité : Salarie + Annee — un seul 13ième par salarié par année.
    /// </summary>
    [DefaultClassOptions]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Money")]  // icône XAF native (paie)
    [OptimisticLocking(true)]
    [RuleCombinationOfPropertiesIsUnique(
        "TreiziemeMois_Salarie_Annee_Unique", DefaultContexts.Save,
        "Salarie;Annee",
        CustomMessageTemplate = "Un calcul 13ième mois existe déjà pour ce salarié sur l'année {Annee}.")]
    // ── Badges colorés sur le statut ────────────────────────────────
    [Appearance("M13_Statut_Calcule",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TreiziemeMoisStatut,Calcule#",
        BackColor = "LightYellow", FontColor = "DarkGoldenrod",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("M13_Statut_Integree",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TreiziemeMoisStatut,IntegreeBulletin#",
        BackColor = "PaleGreen", FontColor = "DarkGreen",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("M13_Statut_Annule",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TreiziemeMoisStatut,Annule#",
        BackColor = "LightCoral", FontColor = "DarkRed",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    // ── Lecture seule après intégration au bulletin ─────────────────
    [Appearance("M13_ReadOnly_Apres_Integration",
        TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TreiziemeMoisStatut,IntegreeBulletin#",
        Enabled = false,
        Context = "DetailView")]
    public class TreiziemeMois : BaseObject
    {
        public TreiziemeMois(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = TreiziemeMoisStatut.Calcule;
            DateCalcul = DateTime.Now;
            try { CalculePar = DevExpress.ExpressApp.SecuritySystem.CurrentUserName; } catch { }
            EstSurSTC = false;
        }

        // ─────────────────────────────────────────────────────────
        // Identification
        // ─────────────────────────────────────────────────────────
        int annee;
        // [RuleRequiredField] retiré : XAF0009 interdit sur value type (int).
        // Le RuleRange ci-dessous + l'init dans AfterConstruction suffisent.
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

        Salarie salarie;
        [RuleRequiredField]
        [Association("Salarie-TreiziemeMois")]
        [XafDisplayName("Salarié")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }

        // ─────────────────────────────────────────────────────────
        // Données de calcul (figées au moment du calcul)
        // ─────────────────────────────────────────────────────────
        decimal brutRecurrentReference;
        [XafDisplayName("Brut récurrent réf.")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ModelDefault("EditMask", "N0")]
        [ToolTip("Brut récurrent du dernier bulletin avant calcul, " +
                 "hors congés et hors éléments exceptionnels.")]
        public decimal BrutRecurrentReference
        {
            get => brutRecurrentReference;
            set => SetPropertyValue(
                nameof(BrutRecurrentReference), ref brutRecurrentReference, value);
        }

        int moisPresence;
        [RuleRange(0, 12,
            CustomMessageTemplate = "Mois de présence entre 0 et 12 ({MoisPresence}).")]
        [XafDisplayName("Mois de présence")]
        [ToolTip("Nombre de bulletins existants pour ce salarié dans l'année.")]
        public int MoisPresence
        {
            get => moisPresence;
            set => SetPropertyValue(nameof(MoisPresence), ref moisPresence, value);
        }

        decimal montantBrut;
        [XafDisplayName("Montant brut 13ième")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ModelDefault("EditMask", "N0")]
        [ToolTip("= Brut récurrent × Mois présence / 12")]
        public decimal MontantBrut
        {
            get => montantBrut;
            set => SetPropertyValue(nameof(MontantBrut), ref montantBrut, value);
        }

        // ─────────────────────────────────────────────────────────
        // Workflow
        // ─────────────────────────────────────────────────────────
        TreiziemeMoisStatut statut;
        // [RuleRequiredField] retiré : XAF0009 interdit sur value type (enum).
        // AfterConstruction initialise à TreiziemeMoisStatut.Calcule.
        [XafDisplayName("Statut")]
        public TreiziemeMoisStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        bool estSurSTC;
        [XafDisplayName("Sur STC")]
        [ToolTip("True si le 13ième a été versé sur le solde de tout " +
                 "compte (départ en cours d'année), false si versement " +
                 "normal sur bulletin de décembre.")]
        public bool EstSurSTC
        {
            get => estSurSTC;
            set => SetPropertyValue(nameof(EstSurSTC), ref estSurSTC, value);
        }

        Bulletin bulletinLie;
        [XafDisplayName("Bulletin de versement")]
        [ToolTip("Bulletin (décembre ou STC) où le 13ième a été intégré.")]
        public Bulletin BulletinLie
        {
            get => bulletinLie;
            set => SetPropertyValue(nameof(BulletinLie), ref bulletinLie, value);
        }

        // ─────────────────────────────────────────────────────────
        // Traçabilité
        // ─────────────────────────────────────────────────────────
        DateTime dateCalcul;
        [VisibleInListView(false)]
        [XafDisplayName("Date calcul")]
        public DateTime DateCalcul
        {
            get => dateCalcul;
            set => SetPropertyValue(nameof(DateCalcul), ref dateCalcul, value);
        }

        string calculePar;
        [Size(100), VisibleInListView(false)]
        [XafDisplayName("Calculé par")]
        public string CalculePar
        {
            get => calculePar;
            set => SetPropertyValue(nameof(CalculePar), ref calculePar, value);
        }

        DateTime? dateIntegration;
        [VisibleInListView(false)]
        [XafDisplayName("Date intégration bulletin")]
        public DateTime? DateIntegration
        {
            get => dateIntegration;
            set => SetPropertyValue(nameof(DateIntegration), ref dateIntegration, value);
        }

        string commentaire;
        [Size(500), VisibleInListView(false)]
        [XafDisplayName("Commentaire")]
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
            "IsNull(Salarie.FullName, '?'), ' / 13M ', ToStr(Annee))")]
        [VisibleInListView(false), VisibleInDetailView(false)]
        [VisibleInLookupListView(true)]
        public string DisplayName => Convert.ToString(EvaluateAlias(nameof(DisplayName)));
    }
}
