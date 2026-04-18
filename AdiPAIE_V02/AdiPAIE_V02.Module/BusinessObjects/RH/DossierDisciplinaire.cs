// AdiPAIE_V02.Module/BusinessObjects/RH/DossierDisciplinaire.cs
// Gestion des procédures disciplinaires — droit du travail sénégalais
using AdiPAIE_V02.Module.BusinessObjects;
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
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Dossier disciplinaire — suivi d'une procédure disciplinaire.
    ///
    /// Droit sénégalais (Code du Travail) :
    ///   - Faute simple → avertissement / blâme
    ///   - Faute grave → mise à pied (max 8 jours)
    ///   - Faute lourde → licenciement (après audition)
    ///
    /// Workflow : Initié → Notifié → Audition → Sanction → Clôturé
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Dossier disciplinaire")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Skull")]
    [NavigationItem("Ressources humaines")]
    [Appearance("Discip_Cloture", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DisciplinaireStatut,Cloture#",
        FontColor = "Gray", FontStyle = DevExpress.Drawing.DXFontStyle.Italic)]
    [RuleCriteria("Discip_Salarie_Required", DefaultContexts.Save,
        "Salarie Is Not Null",
        CustomMessageTemplate = "Veuillez sélectionner un salarié.")]
    public class DossierDisciplinaire : BaseObject
    {
        public DossierDisciplinaire(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = DisciplinaireStatut.Initie;
            DateOuverture = DateTime.Today;
            Reference = $"DISC-{DateTime.Today:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
            try { OuvertPar = SecuritySystem.CurrentUserName; } catch { }
        }

        // ── Référence ─────────────────────────────────────────────────
        [Size(30)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Référence")]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value);
        }
        string reference;

        // ── Salarié ───────────────────────────────────────────────────
        [RuleRequiredField]
        [XafDisplayName("Salarié")]
        [ImmediatePostData]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        // ── Dates ─────────────────────────────────────────────────────
        [XafDisplayName("Date d'ouverture")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime DateOuverture
        {
            get => dateOuverture;
            set => SetPropertyValue(nameof(DateOuverture), ref dateOuverture, value);
        }
        DateTime dateOuverture;

        [XafDisplayName("Date des faits")]
        [RuleRequiredField]
        [VisibleInListView(false)]
        public DateTime? DateFaits
        {
            get => dateFaits;
            set => SetPropertyValue(nameof(DateFaits), ref dateFaits, value);
        }
        DateTime? dateFaits;

        [XafDisplayName("Date de notification au salarié")]
        [VisibleInListView(false)]
        public DateTime? DateNotification
        {
            get => dateNotification;
            set => SetPropertyValue(nameof(DateNotification), ref dateNotification, value);
        }
        DateTime? dateNotification;

        [XafDisplayName("Date de l'audition")]
        [VisibleInListView(false)]
        public DateTime? DateAudition
        {
            get => dateAudition;
            set => SetPropertyValue(nameof(DateAudition), ref dateAudition, value);
        }
        DateTime? dateAudition;

        [XafDisplayName("Date de la sanction")]
        [VisibleInListView(false)]
        public DateTime? DateSanction
        {
            get => dateSanction;
            set => SetPropertyValue(nameof(DateSanction), ref dateSanction, value);
        }
        DateTime? dateSanction;

        // ── Nature de la faute ────────────────────────────────────────
        [XafDisplayName("Catégorie de faute")]
        [RuleRequiredField]
        public CategorieFaute? Categorie
        {
            get => categorie;
            set => SetPropertyValue(nameof(Categorie), ref categorie, value);
        }
        CategorieFaute? categorie;

        [Size(500)]
        [XafDisplayName("Description des faits")]
        [RuleRequiredField]
        [VisibleInListView(false)]
        public string DescriptionFaits
        {
            get => descriptionFaits;
            set => SetPropertyValue(nameof(DescriptionFaits), ref descriptionFaits, value);
        }
        string descriptionFaits;

        [Size(500)]
        [XafDisplayName("Articles du règlement intérieur")]
        [VisibleInListView(false)]
        public string ArticlesRI
        {
            get => articlesRI;
            set => SetPropertyValue(nameof(ArticlesRI), ref articlesRI, value);
        }
        string articlesRI;

        // ── Audition ──────────────────────────────────────────────────
        [Size(SizeAttribute.Unlimited)]
        [XafDisplayName("Procès-verbal d'audition")]
        [VisibleInListView(false)]
        public string PVAudition
        {
            get => pvAudition;
            set => SetPropertyValue(nameof(PVAudition), ref pvAudition, value);
        }
        string pvAudition;

        [Size(200)]
        [XafDisplayName("Témoins / Représentants")]
        [VisibleInListView(false)]
        public string Temoins
        {
            get => temoins;
            set => SetPropertyValue(nameof(Temoins), ref temoins, value);
        }
        string temoins;

        [Size(SizeAttribute.Unlimited)]
        [XafDisplayName("Observations du salarié")]
        [VisibleInListView(false)]
        public string ObservationsSalarie
        {
            get => observationsSalarie;
            set => SetPropertyValue(nameof(ObservationsSalarie), ref observationsSalarie, value);
        }
        string observationsSalarie;

        // ── Sanction ──────────────────────────────────────────────────
        [XafDisplayName("Type de sanction")]
        public TypeSanction? Sanction
        {
            get => sanction;
            set => SetPropertyValue(nameof(Sanction), ref sanction, value);
        }
        TypeSanction? sanction;

        [XafDisplayName("Durée mise à pied (jours)")]
        [ModelDefault("DisplayFormat", "N0")]
        [VisibleInListView(false)]
        public int DureeMiseAPied
        {
            get => dureeMiseAPied;
            set => SetPropertyValue(nameof(DureeMiseAPied), ref dureeMiseAPied, value);
        }
        int dureeMiseAPied;

        [Size(SizeAttribute.Unlimited)]
        [XafDisplayName("Motivation de la sanction")]
        [VisibleInListView(false)]
        public string MotivationSanction
        {
            get => motivationSanction;
            set => SetPropertyValue(nameof(MotivationSanction), ref motivationSanction, value);
        }
        string motivationSanction;

        // ── Statut et traçabilité ─────────────────────────────────────
        [XafDisplayName("Statut")]
        [ModelDefault("AllowEdit", "False")]
        public DisciplinaireStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }
        DisciplinaireStatut statut;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Ouvert par")]
        [VisibleInListView(false)]
        public string OuvertPar
        {
            get => ouvertPar;
            set => SetPropertyValue(nameof(OuvertPar), ref ouvertPar, value);
        }
        string ouvertPar;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Sanction prononcée par")]
        [VisibleInListView(false)]
        public string SanctionPar
        {
            get => sanctionPar;
            set => SetPropertyValue(nameof(SanctionPar), ref sanctionPar, value);
        }
        string sanctionPar;

        [Size(1000)]
        [XafDisplayName("Observations")]
        [VisibleInListView(false)]
        public string Observations
        {
            get => observations;
            set => SetPropertyValue(nameof(Observations), ref observations, value);
        }
        string observations;

        // ── Document ──────────────────────────────────────────────────
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [XafDisplayName("Lettre de sanction (.pdf)")]
        [VisibleInListView(false)]
        public FileData DocumentSanction
        {
            get => documentSanction;
            set => SetPropertyValue(nameof(DocumentSanction), ref documentSanction, value);
        }
        FileData documentSanction;

        // ── Propriétés calculées ──────────────────────────────────────
        [NonPersistent]
        public string DisplayName =>
            $"{Salarie?.FullName ?? "—"} — {Categorie} ({DateOuverture:dd/MM/yyyy})";

        public override string ToString() => DisplayName;
    }
}