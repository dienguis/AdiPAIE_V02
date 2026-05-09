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
   // [NavigationItem("GRH - Évaluation")]

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
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,SoumiseRH#",
        FontColor = "Green", FontStyle = DXFontStyle.Bold)]
    [Appearance("Entretien_Style_Cloture", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,Cloture#",
        FontColor = "Gray", FontStyle = DXFontStyle.Italic)]
    // V1.6.2 — Badges colorés sur Statut (workflow entretien annuel)
    [Appearance("Entretien_Badge_Brouillon",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,Brouillon#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,PlanifieRH#",
        BackColor = "Gainsboro", FontColor = "DimGray", FontStyle = DXFontStyle.Bold)]
    [Appearance("Entretien_Badge_EnCoursSaisie",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,SaisieManager#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,SaisieSalarie#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,ValidationN1#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,EnAttenteN2#",
        BackColor = "Moccasin", FontColor = "DarkOrange", FontStyle = DXFontStyle.Bold)]
    [Appearance("Entretien_Badge_SoumiseRH",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,SoumiseRH#",
        BackColor = "PaleGreen", FontColor = "DarkGreen", FontStyle = DXFontStyle.Bold)]
    [Appearance("Entretien_Badge_Cloture",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,Cloture#",
        BackColor = "DarkGray", FontColor = "White", FontStyle = DXFontStyle.Bold)]
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
        // Campagne — ne doit pas être changée
        [ModelDefault("AllowEdit", "False")]
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
        // Évaluateur — ne doit pas être modifié par le salarié
        [ModelDefault("AllowEdit", "False")]
        public Salarie Evaluateur
        {
            get => evaluateur;
            set => SetPropertyValue(nameof(Evaluateur), ref evaluateur, value);
        }
        Salarie evaluateur;


        [VisibleInListView(false)]
        [XafDisplayName("Valideur N+2")]
        [ModelDefault("AllowEdit", "False")]
        public Salarie ValideurN2
        {
            get => valideurN2;
            set => SetPropertyValue(nameof(ValideurN2), ref valideurN2, value);
        }
        Salarie valideurN2;


        [VisibleInListView(false)]
        [XafDisplayName("Date validation N+1")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateValidationN1
        {
            get => dateValidationN1;
            set => SetPropertyValue(nameof(DateValidationN1), ref dateValidationN1, value);
        }
        DateTime? dateValidationN1;

        [VisibleInListView(false)]
        [XafDisplayName("Date validation N+2")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateValidationN2
        {
            get => dateValidationN2;
            set => SetPropertyValue(nameof(DateValidationN2), ref dateValidationN2, value);
        }
        DateTime? dateValidationN2;

        [VisibleInListView(false)]
        [Size(100)]
        [XafDisplayName("Rejeté par")]
        [ModelDefault("AllowEdit", "False")]
        public string RejeteParNom
        {
            get => rejeteParNom;
            set => SetPropertyValue(nameof(RejeteParNom), ref rejeteParNom, value);
        }
        string rejeteParNom;

        [VisibleInListView(false)]
        [Size(500)]
        [XafDisplayName("Motif de rejet N+2")]
        [ModelDefault("AllowEdit", "False")]
        public string MotifRejetN2
        {
            get => motifRejetN2;
            set => SetPropertyValue(nameof(MotifRejetN2), ref motifRejetN2, value?.Trim());
        }
        string motifRejetN2;


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

        [VisibleInListView(false)]
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
        [ModelDefault("AllowEdit", "False")]
        public EntretienStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        // ── Commentaires ──────────────────────────────────────────
        string commentaireSalarie;
        [VisibleInListView(false)]
        [Size(4096)]
        [XafDisplayName("Auto-évaluation (commentaire salarié)")]
        [EditorAlias(EditorAliases.RichTextPropertyEditor)]
        public string CommentaireSalarie
        {
            get => commentaireSalarie;
            set => SetPropertyValue(nameof(CommentaireSalarie), ref commentaireSalarie, value);
        }

        string commentaireManager;
        [VisibleInListView(false)]
        [Size(4096)]
        [XafDisplayName("Commentaire du manager")]
        [EditorAlias(EditorAliases.RichTextPropertyEditor)]
        public string CommentaireManager
        {
            get => commentaireManager;
            set => SetPropertyValue(nameof(CommentaireManager), ref commentaireManager, value);
        }

        string conclusionGenerale;
        [VisibleInListView(false)]
        [Size(2048)]
        [XafDisplayName("Conclusion générale")]
        public string ConclusionGenerale
        {
            get => conclusionGenerale;
            set => SetPropertyValue(nameof(ConclusionGenerale), ref conclusionGenerale, value);
        }

        // ── Mobilité / décisions RH ───────────────────────────────
        bool promotionProposee;
        [VisibleInListView(false)]
        [XafDisplayName("Promotion proposée")]
        public bool PromotionProposee
        {
            get => promotionProposee;
            set => SetPropertyValue(nameof(PromotionProposee), ref promotionProposee, value);
        }

        bool augmentationProposee;
        [VisibleInListView(false)]
        [XafDisplayName("Augmentation proposée")]
        public bool AugmentationProposee
        {
            get => augmentationProposee;
            set => SetPropertyValue(nameof(AugmentationProposee), ref augmentationProposee, value);
        }

        bool formationIdentifiee;
        [VisibleInListView(false)]
        [XafDisplayName("Formation identifiée")]
        public bool FormationIdentifiee
        {
            get => formationIdentifiee;
            set => SetPropertyValue(nameof(FormationIdentifiee), ref formationIdentifiee, value);
        }

        string notesDecisionRH;
        [VisibleInListView(false)]
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
        [VisibleInListView(false)]
        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        public DateTime DateCreation
        {
            get => dateCreation;
            set => SetPropertyValue(nameof(DateCreation), ref dateCreation, value);
        }
        DateTime dateCreation;

        [VisibleInListView(false)]
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
        [VisibleInListView(false)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date de clôture")]
        public DateTime? DateCloture
        {
            get => dateCloture;
            set => SetPropertyValue(nameof(DateCloture), ref dateCloture, value);
        }

        string cloturePar;
        [VisibleInListView(false)]
        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Clôturé par")]
        public string CloturePar
        {
            get => cloturePar;
            set => SetPropertyValue(nameof(CloturePar), ref cloturePar, value);
        }

        // ── Workflow ──────────────────────────────────────────────
        /// <summary>RH fixe la date — Brouillon → PlanifiéRH</summary>
        public void Planifier(DateTime date)
        {
            if (Statut != EntretienStatut.Brouillon)
                throw new UserFriendlyException("L'entretien n'est pas en état Brouillon.");
            DatePlanifiee = date;
            Statut = EntretienStatut.PlanifieRH;
        }

        /// <summary>
        /// RH lance l'évaluation — PlanifiéRH → SaisieManager.
        /// Résout la chaîne hiérarchique (snapshot N+1 / N+2).
        /// Notifie le N+1.
        /// </summary>
        public void LancerEvaluation()
        {
            if (Statut != EntretienStatut.PlanifieRH)
                throw new UserFriendlyException("L'entretien doit être planifié avant de lancer l'évaluation.");

            if (Salarie == null)
                throw new UserFriendlyException("Le salarié n'est pas renseigné.");

            // Snapshot hiérarchie
            var chain = Salarie.GetManagerChain(2);
            if (Evaluateur == null && chain.Count >= 1)
                Evaluateur = chain[0];
            if (ValideurN2 == null && chain.Count >= 2)
                ValideurN2 = chain[1];

            Statut = EntretienStatut.SaisieManager;
        }


        /// <summary>
        /// N+1 soumet l'évaluation au salarié — SaisieManager → SaisieSalarie.
        /// Notifie le salarié.
        /// </summary>
        public void SoumettreAuSalarie()
        {
            if (Statut != EntretienStatut.SaisieManager)
                throw new UserFriendlyException("L'évaluation doit être en saisie manager.");
            Statut = EntretienStatut.SaisieSalarie;
        }

        /// <summary>
        /// Salarié soumet ses observations — SaisieSalarie → ValidationN1.
        /// Notifie le N+1.
        /// </summary>
        public void SoumettreObservations()
        {
            if (Statut != EntretienStatut.SaisieSalarie)
                throw new UserFriendlyException("L'entretien n'est pas en phase d'observations salarié.");
            Statut = EntretienStatut.ValidationN1;
        }

        /// <summary>
        /// N+1 valide les observations — ValidationN1 → EnAttenteN2 ou SoumiseRH.
        /// </summary>
        public void ValiderObservationsN1()
        {
            if (Statut != EntretienStatut.ValidationN1)
                throw new UserFriendlyException("L'entretien n'est pas en attente de validation N+1.");

            DateValidationN1 = DateTime.Now;
            Statut = ValideurN2 != null
                ? EntretienStatut.EnAttenteN2
                : EntretienStatut.SoumiseRH;
        }

        /// <summary>
        /// N+2 valide — EnAttenteN2 → SoumiseRH.
        /// </summary>
        public void ValiderN2()
        {
            if (Statut != EntretienStatut.EnAttenteN2)
                throw new UserFriendlyException("L'entretien n'est pas en attente de validation N+2.");

            DateValidationN2 = DateTime.Now;
            Statut = EntretienStatut.SoumiseRH;
        }

        /// <summary>
        /// N+2 rejette — EnAttenteN2 → retour SaisieManager pour correction N+1.
        /// </summary>
        public void RejeterN2(string motif = null)
        {
            if (Statut != EntretienStatut.EnAttenteN2)
                throw new UserFriendlyException("L'entretien n'est pas en attente de validation N+2.");

            try { RejeteParNom = SecuritySystem.CurrentUserName; } catch { }
            if (!string.IsNullOrWhiteSpace(motif)) MotifRejetN2 = motif;

            // Réinitialise pour une nouvelle saisie N+1
            DateValidationN1 = null;
            Statut = EntretienStatut.SaisieManager;
        }

        /// <summary>RH clôture — SoumiseRH → Clôturé.</summary>
        public void Cloturer()
        {
            if (Statut == EntretienStatut.Cloture)
                throw new UserFriendlyException("L'entretien est déjà clôturé.");
            if (Statut != EntretienStatut.SoumiseRH)
                throw new UserFriendlyException("L'entretien doit être soumis au RH avant clôture.");

            RecalculerScore();
            DateCloture = DateTime.Now;
            try { CloturePar = SecuritySystem.CurrentUserName; } catch { }
            Statut = EntretienStatut.Cloture;
        }

        /// <summary>Score pondéré global (0-5) depuis les lignes d'évaluation.</summary>
        public void RecalculerScore()
        {
            var lignesNotees = Lignes.Where(l => l.Note != NoteEvaluation.NonEvalue).ToList();
            if (!lignesNotees.Any()) { ScoreGlobal = 0m; return; }
            var totalPoids = lignesNotees.Sum(l => l.Poids);
            if (totalPoids == 0m) { ScoreGlobal = 0m; return; }
            ScoreGlobal = lignesNotees.Sum(l => (decimal)l.Note * l.Poids) / totalPoids;
        }



        // ── Partie 0 : Identification ─────────────────────────────

        string niveauInstruction;
        [VisibleInListView(false)]
        [Size(500)]
        [XafDisplayName("Niveau d'instruction / formations complémentaires")]
        public string NiveauInstruction
        {
            get => niveauInstruction;
            set => SetPropertyValue(nameof(NiveauInstruction), ref niveauInstruction, value?.Trim());
        }

        // ── Partie SYNTHESE : Notes globales ──────────────────────

        NoteGlobale? noteGlobaleManager;
        [VisibleInListView(false)]
        [XafDisplayName("Note globale manager (A+ à F)")]
        [ToolTip("Note attribuée par le manager sur l'ensemble de la période.")]
        public NoteGlobale? NoteGlobaleManager
        {
            get => noteGlobaleManager;
            set => SetPropertyValue(nameof(NoteGlobaleManager), ref noteGlobaleManager, value);
        }

        NoteGlobale? noteGlobaleService;
        [VisibleInListView(false)]
        [XafDisplayName("Note globale service (A+ à F)")]
        [ToolTip("Note attribuée par le directeur de département sur les objectifs du service.")]
        public NoteGlobale? NoteGlobaleService
        {
            get => noteGlobaleService;
            set => SetPropertyValue(nameof(NoteGlobaleService), ref noteGlobaleService, value);
        }

        string commentairesHierarchie;
        [VisibleInListView(false)]
        [Size(4096)]
        [XafDisplayName("Commentaires de la hiérarchie")]
        [EditorAlias(DevExpress.ExpressApp.Editors.EditorAliases.RichTextPropertyEditor)]
        public string CommentairesHierarchie
        {
            get => commentairesHierarchie;
            set => SetPropertyValue(nameof(CommentairesHierarchie), ref commentairesHierarchie, value);
        }

        string commentairesCollaborateur;
        [VisibleInListView(false)]
        [Size(4096)]
        [XafDisplayName("Commentaires du collaborateur")]
        [EditorAlias(DevExpress.ExpressApp.Editors.EditorAliases.RichTextPropertyEditor)]
        public string CommentairesCollaborateur
        {
            get => commentairesCollaborateur;
            set => SetPropertyValue(nameof(CommentairesCollaborateur), ref commentairesCollaborateur, value);
        }

        // ── Partie II : Projet professionnel ──────────────────────

        string evolutionSouhaitee;
        [VisibleInListView(false)]
        [Size(2048)]
        [XafDisplayName("Évolution souhaitée par le collaborateur")]
        public string EvolutionSouhaitee
        {
            get => evolutionSouhaitee;
            set => SetPropertyValue(nameof(EvolutionSouhaitee), ref evolutionSouhaitee, value?.Trim());
        }

        // ── Partie III : Management ────────────────────────────────

        bool estEnSituationEncadrement;
        [VisibleInListView(false)]
        [XafDisplayName("En situation d'encadrement")]
        [ImmediatePostData]
        [ToolTip("Cocher si ce salarié encadre d'autres collaborateurs. Active la Partie III.")]
        public bool EstEnSituationEncadrement
        {
            get => estEnSituationEncadrement;
            set => SetPropertyValue(nameof(EstEnSituationEncadrement), ref estEnSituationEncadrement, value);
        }

        // ── Nouvelles collections ─────────────────────────────────

        [Association("EntretienAnnuel-Missions"), Aggregated]
        [XafDisplayName("Missions et responsabilités (Partie I-A)")]
        public XPCollection<EntretienMission> Missions
            => GetCollection<EntretienMission>(nameof(Missions));

        [Association("EntretienAnnuel-BesoinsFormation"), Aggregated]
        [XafDisplayName("Besoins en formation (Partie II)")]
        public XPCollection<EntretienBesoinFormation> BesoinsFormation
            => GetCollection<EntretienBesoinFormation>(nameof(BesoinsFormation));

        [Association("EntretienAnnuel-Aptitudes"), Aggregated]
        [XafDisplayName("Aptitudes au management (Partie III)")]
        [Appearance("Aptitudes_Visible",
            Criteria = "EstEnSituationEncadrement = true",
            Visibility = DevExpress.ExpressApp.Editors.ViewItemVisibility.Show,
            TargetItems = "Aptitudes")]
        [Appearance("Aptitudes_Hidden",
            Criteria = "EstEnSituationEncadrement = false",
            Visibility = DevExpress.ExpressApp.Editors.ViewItemVisibility.Hide,
            TargetItems = "Aptitudes")]
        public XPCollection<EntretienManagement> Aptitudes
            => GetCollection<EntretienManagement>(nameof(Aptitudes));

    }
}
