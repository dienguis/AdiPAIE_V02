using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Suivi post-formation d'un salarié.
    /// Créé automatiquement à la clôture d'une session pour chaque participant présent.
    /// Permet de tracer l'historique de formation, les acquis et l'impact sur le poste.
    ///
    /// Lié à l'InscriptionFormation et visible depuis la fiche salarié.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Suivi formation")]
    [DefaultProperty(nameof(DisplaySuivi))]
    [ImageName("BO_FileAttachment")]
    [NavigationItem("GRH - Formation")]
    public class SuiviFormation : BaseObject
    {
        public SuiviFormation(Session session) : base(session) { }

        // ── Salarié ───────────────────────────────────────────
        [Association("Salarie-SuivisFormation")]
        [RuleRequiredField]
        [XafDisplayName("Salarié")]
        [ModelDefault("AllowEdit", "False")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        // ── Formation ────────────────────────────────────────
        [Association("InscriptionFormation-Suivi")]
        [XafDisplayName("Inscription")]
        [ModelDefault("AllowEdit", "False")]
        public InscriptionFormation Inscription
        {
            get => inscription;
            set => SetPropertyValue(nameof(Inscription), ref inscription, value);
        }
        InscriptionFormation inscription;

        // ── Données session (snapshot pour consultation rapide) ─
        string intituleFormation;
        [Size(200)]
        [XafDisplayName("Intitulé formation")]
        [ModelDefault("AllowEdit", "False")]
        public string IntituleFormation
        {
            get => intituleFormation;
            set => SetPropertyValue(nameof(IntituleFormation), ref intituleFormation, value);
        }

        string domaine;
        [Size(150)]
        [XafDisplayName("Domaine")]
        [ModelDefault("AllowEdit", "False")]
        public string Domaine
        {
            get => domaine;
            set => SetPropertyValue(nameof(Domaine), ref domaine, value);
        }

        DateTime dateFormation;
        [XafDisplayName("Date de formation")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime DateFormation
        {
            get => dateFormation;
            set => SetPropertyValue(nameof(DateFormation), ref dateFormation, value);
        }

        int dureeJours;
        [XafDisplayName("Durée (jours)")]
        [ModelDefault("AllowEdit", "False")]
        public int DureeJours
        {
            get => dureeJours;
            set => SetPropertyValue(nameof(DureeJours), ref dureeJours, value);
        }

        string formateurOuOrganisme;
        [Size(150)]
        [XafDisplayName("Formateur / Organisme")]
        [ModelDefault("AllowEdit", "False")]
        public string FormateurOuOrganisme
        {
            get => formateurOuOrganisme;
            set => SetPropertyValue(nameof(FormateurOuOrganisme), ref formateurOuOrganisme, value);
        }

        // ── Évaluation à froid (J+30/J+90) ───────────────────
        string acquis;
        [Size(2000)]
        [XafDisplayName("Compétences acquises / renforcées")]
        public string Acquis
        {
            get => acquis;
            set => SetPropertyValue(nameof(Acquis), ref acquis, value?.Trim());
        }

        string impactPoste;
        [Size(1000)]
        [XafDisplayName("Impact observé sur le poste")]
        public string ImpactPoste
        {
            get => impactPoste;
            set => SetPropertyValue(nameof(ImpactPoste), ref impactPoste, value?.Trim());
        }

        EvaluationFormationNote? noteGlobale;
        [XafDisplayName("Note globale (N+1)")]
        public EvaluationFormationNote? NoteGlobale
        {
            get => noteGlobale;
            set => SetPropertyValue(nameof(NoteGlobale), ref noteGlobale, value);
        }

        DateTime? dateEvaluationFroid;
        [XafDisplayName("Date évaluation à froid")]
        public DateTime? DateEvaluationFroid
        {
            get => dateEvaluationFroid;
            set => SetPropertyValue(nameof(DateEvaluationFroid), ref dateEvaluationFroid, value);
        }

        // ── Renouvellement ────────────────────────────────────
        bool renouvelerAnneeProchaine;
        [XafDisplayName("À renouveler l'année prochaine")]
        public bool RenouvelerAnneeProchaine
        {
            get => renouvelerAnneeProchaine;
            set => SetPropertyValue(nameof(RenouvelerAnneeProchaine),
                ref renouvelerAnneeProchaine, value);
        }

        // ── Évaluation à froid — champs du formulaire ────────────

        // Q1 — Besoin réel de suivre la formation ?
        BesoinFormationReponse? besoinReel;
        [XafDisplayName("Besoin réel de cette formation")]
        [Category("Évaluation à froid")]
        [ImmediatePostData]
        public BesoinFormationReponse? BesoinReel
        {
            get => besoinReel;
            set => SetPropertyValue(nameof(BesoinReel), ref besoinReel, value);
        }

        // Q2 — La formation répondait-elle au besoin ?
        AdequationFormationReponse? adequationBesoin;
        [XafDisplayName("Formation adaptée au besoin")]
        [Category("Évaluation à froid")]
        [ImmediatePostData]
        public AdequationFormationReponse? AdequationBesoin
        {
            get => adequationBesoin;
            set => SetPropertyValue(nameof(AdequationBesoin), ref adequationBesoin, value);
        }

        // Q2b — Si non/partiellement, pourquoi ?
        string raisonInadequation;
        [Size(500)]
        [XafDisplayName("Raison si inadéquation")]
        [Category("Évaluation à froid")]
        public string RaisonInadequation
        {
            get => raisonInadequation;
            set => SetPropertyValue(nameof(RaisonInadequation), ref raisonInadequation, value?.Trim());
        }

        // Q3 — Initiative de la formation ?
        InitiativeFormationReponse? initiativeFormation;
        [XafDisplayName("Initiative de la formation")]
        [Category("Évaluation à froid")]
        public InitiativeFormationReponse? InitiativeFormation
        {
            get => initiativeFormation;
            set => SetPropertyValue(nameof(InitiativeFormation), ref initiativeFormation, value);
        }

        // Q4 — Mise en pratique des connaissances ?
        MisePratiqueReponse? misePratique;
        [XafDisplayName("Mise en pratique des connaissances")]
        [Category("Évaluation à froid")]
        [ImmediatePostData]
        public MisePratiqueReponse? MisePratique
        {
            get => misePratique;
            set => SetPropertyValue(nameof(MisePratique), ref misePratique, value);
        }

        // Q5 — Fréquence de mise en pratique ?
        FrequencePratiqueReponse? frequencePratique;
        [XafDisplayName("Fréquence de mise en pratique")]
        [Category("Évaluation à froid")]
        public FrequencePratiqueReponse? FrequencePratique
        {
            get => frequencePratique;
            set => SetPropertyValue(nameof(FrequencePratique), ref frequencePratique, value);
        }

        // Q5b — Si non/partiellement, pourquoi ?
        string raisonNonPratique;
        [Size(500)]
        [XafDisplayName("Obstacle à la mise en pratique")]
        [Category("Évaluation à froid")]
        public string RaisonNonPratique
        {
            get => raisonNonPratique;
            set => SetPropertyValue(nameof(RaisonNonPratique), ref raisonNonPratique, value?.Trim());
        }

        // Q6 — Facteurs favorisant la mise en pratique ?
        string facteursavorisants;
        [Size(500)]
        [XafDisplayName("Facteurs favorisant la pratique")]
        [Category("Évaluation à froid")]
        public string FacteursFavorisants
        {
            get => facteursavorisants;
            set => SetPropertyValue(nameof(FacteursFavorisants), ref facteursavorisants, value?.Trim());
        }

        // Q7 — Entretien fait avec le collaborateur ?
        bool? entretienFait;
        [XafDisplayName("Entretien fait avec le collaborateur")]
        [Category("Évaluation à froid")]
        public bool? EntretienFait
        {
            get => entretienFait;
            set => SetPropertyValue(nameof(EntretienFait), ref entretienFait, value);
        }

        // Q8 — Remarques du collaborateur
        string remarquesCollaborateur;
        [Size(1000)]
        [XafDisplayName("Remarques du collaborateur")]
        [Category("Évaluation à froid")]
        public string RemarquesCollaborateur
        {
            get => remarquesCollaborateur;
            set => SetPropertyValue(nameof(RemarquesCollaborateur), ref remarquesCollaborateur, value?.Trim());
        }

        // Q9 — Résultat atteint ?
        ResultatAtteintReponse? resultatAtteint;
        [XafDisplayName("Résultat atteint")]
        [Category("Évaluation à froid")]
        public ResultatAtteintReponse? ResultatAtteint
        {
            get => resultatAtteint;
            set => SetPropertyValue(nameof(ResultatAtteint), ref resultatAtteint, value);
        }

        // ── Affichage ─────────────────────────────────────────
        [NonPersistent]
        public string DisplaySuivi =>
            $"{Salarie?.LastName} — {IntituleFormation} ({DateFormation:dd/MM/yyyy})";

        // ── Factory — crée depuis une inscription ─────────────
        /// <summary>
        /// Crée un SuiviFormation depuis une InscriptionFormation à la clôture de session.
        /// Appelé par le controller de workflow.
        /// </summary>
        public static SuiviFormation CreerDepuisInscription(
            DevExpress.ExpressApp.IObjectSpace os,
            InscriptionFormation insc)
        {
            if (insc?.Salarie == null || insc.SessionFormation == null)
                return null;

            var suivi = os.CreateObject<SuiviFormation>();
            suivi.Salarie = insc.Salarie;
            suivi.Inscription = insc;
            suivi.IntituleFormation = insc.SessionFormation.Intitule;
            suivi.Domaine = insc.SessionFormation.Domaine?.Libelle ?? "";
            suivi.DateFormation = insc.SessionFormation.DateDebut;
            suivi.DureeJours = insc.SessionFormation.DureeJours;
            suivi.FormateurOuOrganisme = insc.SessionFormation.FormateurNom ?? "";
            return suivi;
        }
    }
}
