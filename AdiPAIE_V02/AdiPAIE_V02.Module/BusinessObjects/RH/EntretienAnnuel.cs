using DevExpress.Drawing;
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
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Entretien annuel d'évaluation individuel.
    /// Lié à une CampagneEvaluation et à un Salarié.
    /// Contient les lignes d'évaluation (critères + notes) et les objectifs N/N+1.
    ///
    /// Workflow :
    ///   Brouillon → PlanifieRH (convocation) → SaisieSalarie (auto-évaluation)
    ///             → EnCours (saisie manager) → ValideManager → Cloture
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Entretien annuel")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Contact")]
    [NavigationItem("GRH - Évaluation")]

    [RuleCriteria("Entretien_DateRealisation_Required",
        DefaultContexts.Save,
        "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,Brouillon# OR NOT IsNull(DateRealisation)",
        CustomMessageTemplate = "La date de réalisation est obligatoire dès que le statut dépasse Brouillon.")]

    // Verrouillage total en lecture seule si clôturé
    [Appearance("Entretien_Lock_Cloture",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,Cloture#",
        TargetItems = "*", Enabled = false)]

    // Style couleur par statut
    [Appearance("Entretien_Style_ValideManager", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,ValideManager#",
        FontColor = "Green", FontStyle = DXFontStyle.Bold)]
    [Appearance("Entretien_Style_Cloture", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,Cloture#",
        FontColor = "Gray", FontStyle = DXFontStyle.Italic)]
    public class EntretienAnnuel : BaseObject
    {
        public EntretienAnnuel(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = EntretienStatut.Brouillon;
            DateCreation = DateTime.Now;
            try { CreePar = SecuritySystem.CurrentUserName; } catch { }
        }

        // ── Liaisons principales ──────────────────────────────────
        [Association("CampagneEvaluation-Entretiens")]
        [RuleRequiredField]
        [XafDisplayName("Campagne")]
        public CampagneEvaluation Campagne
        {
            get => campagne;
            set => SetPropertyValue(nameof(Campagne), ref campagne, value);
        }
        CampagneEvaluation campagne;

        [Association("Salarie-Entretiens")]
        [RuleRequiredField]
        [XafDisplayName("Salarié évalué")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        /// <summary>Manager / évaluateur (peut être un autre Salarie ou un nom libre)</summary>
        [Association("Evaluateur-Entretiens")]
        [XafDisplayName("Évaluateur (N+1)")]
        public Salarie Evaluateur
        {
            get => evaluateur;
            set => SetPropertyValue(nameof(Evaluateur), ref evaluateur, value);
        }
        Salarie evaluateur;

        // ── Affichage ─────────────────────────────────────────────
        [PersistentAlias("Concat(Salarie.LastName, ' ', Salarie.FirstName, ' – ', ToStr(Campagne.Annee))")]
        public string DisplayName => (string)EvaluateAlias(nameof(DisplayName));

        // ── Dates ─────────────────────────────────────────────────
        [XafDisplayName("Date planifiée")]
        public DateTime? DatePlanifiee
        {
            get => datePlanifiee;
            set => SetPropertyValue(nameof(DatePlanifiee), ref datePlanifiee, value);
        }
        DateTime? datePlanifiee;

        [XafDisplayName("Date de réalisation effective")]
        public DateTime? DateRealisation
        {
            get => dateRealisation;
            set => SetPropertyValue(nameof(DateRealisation), ref dateRealisation, value);
        }
        DateTime? dateRealisation;

        [XafDisplayName("Durée (minutes)")]
        public int DureeMinutes
        {
            get => dureeMinutes;
            set => SetPropertyValue(nameof(DureeMinutes), ref dureeMinutes, value);
        }
        int dureeMinutes;

        // ── Statut ────────────────────────────────────────────────
        EntretienStatut statut;
        [XafDisplayName("Statut")]
        public EntretienStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        // ── Commentaires ──────────────────────────────────────────
        string commentaireSalarie;
        [Size(4096)]
        [XafDisplayName("Auto-évaluation (commentaire salarié)")]
        [EditorAlias(EditorAliases.RichTextPropertyEditor)]
        public string CommentaireSalarie
        {
            get => commentaireSalarie;
            set => SetPropertyValue(nameof(CommentaireSalarie), ref commentaireSalarie, value);
        }

        string commentaireManager;
        [Size(4096)]
        [XafDisplayName("Commentaire du manager")]
        [EditorAlias(EditorAliases.RichTextPropertyEditor)]
        public string CommentaireManager
        {
            get => commentaireManager;
            set => SetPropertyValue(nameof(CommentaireManager), ref commentaireManager, value);
        }

        string conclusionGenerale;
        [Size(2048)]
        [XafDisplayName("Conclusion générale")]
        public string ConclusionGenerale
        {
            get => conclusionGenerale;
            set => SetPropertyValue(nameof(ConclusionGenerale), ref conclusionGenerale, value);
        }

        // ── Mobilité / décisions RH ───────────────────────────────
        bool promotionProposee;
        [XafDisplayName("Promotion proposée")]
        public bool PromotionProposee
        {
            get => promotionProposee;
            set => SetPropertyValue(nameof(PromotionProposee), ref promotionProposee, value);
        }

        bool augmentationProposee;
        [XafDisplayName("Augmentation proposée")]
        public bool AugmentationProposee
        {
            get => augmentationProposee;
            set => SetPropertyValue(nameof(AugmentationProposee), ref augmentationProposee, value);
        }

        bool formationIdentifiee;
        [XafDisplayName("Formation identifiée")]
        public bool FormationIdentifiee
        {
            get => formationIdentifiee;
            set => SetPropertyValue(nameof(FormationIdentifiee), ref formationIdentifiee, value);
        }

        string notesDecisionRH;
        [Size(1024)]
        [XafDisplayName("Notes décision RH (confidentiel)")]
        public string NotesDecisionRH
        {
            get => notesDecisionRH;
            set => SetPropertyValue(nameof(NotesDecisionRH), ref notesDecisionRH, value);
        }

        // ── Score global calculé ──────────────────────────────────
        /// <summary>
        /// Score pondéré global (0–5), calculé à partir des lignes d'évaluation.
        /// Stocké lors du recalcul manuel ou à la validation.
        /// </summary>
        decimal scoreGlobal;
        [DbType("decimal(18,2)")]
        [ModelDefault("DisplayFormat", "N2")]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Score global (0-5)")]
        public decimal ScoreGlobal
        {
            get => scoreGlobal;
            set => SetPropertyValue(nameof(ScoreGlobal), ref scoreGlobal, value);
        }

        // ── Collections ───────────────────────────────────────────
        [Association("EntretienAnnuel-Lignes"), Aggregated]
        [XafDisplayName("Grille d'évaluation")]
        public XPCollection<EntretienLigne> Lignes => GetCollection<EntretienLigne>(nameof(Lignes));

        [Association("EntretienAnnuel-Objectifs"), Aggregated]
        [XafDisplayName("Objectifs N / N+1")]
        public XPCollection<ObjectifAnnuel> Objectifs => GetCollection<ObjectifAnnuel>(nameof(Objectifs));

        // ── Traçabilité ───────────────────────────────────────────
        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        public DateTime DateCreation
        {
            get => dateCreation;
            set => SetPropertyValue(nameof(DateCreation), ref dateCreation, value);
        }
        DateTime dateCreation;

        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        [Size(50)]
        public string CreePar
        {
            get => creePar;
            set => SetPropertyValue(nameof(CreePar), ref creePar, value);
        }
        string creePar;

        DateTime? dateCloture;
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date de clôture")]
        public DateTime? DateCloture
        {
            get => dateCloture;
            set => SetPropertyValue(nameof(DateCloture), ref dateCloture, value);
        }

        string cloturePar;
        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Clôturé par")]
        public string CloturePar
        {
            get => cloturePar;
            set => SetPropertyValue(nameof(CloturePar), ref cloturePar, value);
        }

        // ── Workflow ──────────────────────────────────────────────
        public void Planifier(DateTime date)
        {
            if (Statut != EntretienStatut.Brouillon)
                throw new UserFriendlyException("L'entretien n'est pas en état Brouillon.");
            DatePlanifiee = date;
            Statut = EntretienStatut.PlanifieRH;
        }

        public void EnvoyerAutoEvaluation()
        {
            if (Statut != EntretienStatut.PlanifieRH)
                throw new UserFriendlyException("L'entretien doit être planifié avant d'envoyer l'auto-évaluation.");
            Statut = EntretienStatut.SaisieSalarie;
        }

        public void LancerSaisieManager()
        {
            if (Statut != EntretienStatut.SaisieSalarie)
                throw new UserFriendlyException("L'auto-évaluation salarié doit être transmise d'abord.");
            Statut = EntretienStatut.EnCours;
        }

        public void ValiderParManager()
        {
            if (Statut != EntretienStatut.EnCours)
                throw new UserFriendlyException("L'entretien doit être en cours pour être validé.");
            RecalculerScore();
            Statut = EntretienStatut.ValideManager;
        }

        public void Cloturer()
        {
            if (Statut == EntretienStatut.Cloture)
                throw new UserFriendlyException("L'entretien est déjà clôturé.");
            DateCloture = DateTime.Now;
            try { CloturePar = SecuritySystem.CurrentUserName; } catch { }
            Statut = EntretienStatut.Cloture;
        }

        /// <summary>
        /// Recalcule le score pondéré global depuis les lignes d'évaluation.
        /// Score = Somme(Note × Poids) / Somme(Poids).
        /// </summary>
        public void RecalculerScore()
        {
            var lignesNotees = Lignes.Where(l => l.Note != NoteEvaluation.NonEvalue).ToList();
            if (!lignesNotees.Any()) { ScoreGlobal = 0m; return; }

            var totalPoids = lignesNotees.Sum(l => l.Poids);
            if (totalPoids == 0m) { ScoreGlobal = 0m; return; }

            ScoreGlobal = lignesNotees.Sum(l => (decimal)l.Note * l.Poids) / totalPoids;
        }
    }
}
