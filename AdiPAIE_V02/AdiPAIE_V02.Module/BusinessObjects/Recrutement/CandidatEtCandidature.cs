// =============================================================================
//  CandidatEtCandidature.cs — V1.4 (mai 2026)
//
//  - Candidat    : personne ayant postulé (1 fiche par personne, multi-candidatures)
//  - Candidature : association Candidat × Poste avec workflow de pipeline
// =============================================================================

using System;
using System.ComponentModel;
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects.Recrutement
{
    [DefaultClassOptions]
    [XafDisplayName("Candidat")]
    [DefaultProperty(nameof(NomComplet))]
    [ImageName("BO_Person")]
    [NavigationItem("GRH - Recrutement")]
    public class Candidat : BaseObject
    {
        public Candidat(Session session) : base(session) { }

        // ── Identification ────────────────────────────────────────

        private string _matricule;
        [Indexed(Unique = true), Size(30)]
        [XafDisplayName("Référence")]
        [ToolTip("Code unique candidat (ex CAND-2026-0001).")]
        public string Matricule
        {
            get => _matricule;
            set => SetPropertyValue(nameof(Matricule), ref _matricule, value?.Trim().ToUpperInvariant());
        }

        private string _nom;
        [RuleRequiredField, Size(60)]
        [XafDisplayName("Nom")]
        public string Nom
        {
            get => _nom;
            set => SetPropertyValue(nameof(Nom), ref _nom, value?.Trim().ToUpperInvariant());
        }

        private string _prenom;
        [RuleRequiredField, Size(60)]
        [XafDisplayName("Prénom")]
        public string Prenom
        {
            get => _prenom;
            set => SetPropertyValue(nameof(Prenom), ref _prenom, value?.Trim());
        }

        [XafDisplayName("Nom complet")]
        public string NomComplet => $"{Nom} {Prenom}".Trim();

        private Sexe _sexe;
        [XafDisplayName("Sexe")]
        public Sexe Sexe { get => _sexe; set => SetPropertyValue(nameof(Sexe), ref _sexe, value); }

        private DateTime _dateNaissance;
        [XafDisplayName("Date de naissance")]
        public DateTime DateNaissance
        {
            get => _dateNaissance;
            set => SetPropertyValue(nameof(DateNaissance), ref _dateNaissance, value);
        }

        // ── Contact ───────────────────────────────────────────────

        private string _email;
        [Size(120)]
        [RuleRegularExpression(@"^$|^[^@\s]+@[^@\s]+\.[^@\s]+$", CustomMessageTemplate = "Format email invalide.")]
        [XafDisplayName("Email")]
        public string Email
        {
            get => _email;
            set => SetPropertyValue(nameof(Email), ref _email, value?.Trim().ToLower());
        }

        private string _telephone;
        [Size(20)]
        [XafDisplayName("Téléphone")]
        public string Telephone
        {
            get => _telephone;
            set => SetPropertyValue(nameof(Telephone), ref _telephone, value?.Trim());
        }

        private string _adresse;
        [Size(200)]
        [XafDisplayName("Ville / Adresse")]
        public string Adresse
        {
            get => _adresse;
            set => SetPropertyValue(nameof(Adresse), ref _adresse, value?.Trim());
        }

        // ── Profil ────────────────────────────────────────────────

        private int _experienceAnnees;
        [XafDisplayName("Expérience (années)")]
        public int ExperienceAnnees
        {
            get => _experienceAnnees;
            set => SetPropertyValue(nameof(ExperienceAnnees), ref _experienceAnnees, value);
        }

        private string _competencesCles;
        [Size(1000)]
        [XafDisplayName("Compétences clés")]
        public string CompetencesCles
        {
            get => _competencesCles;
            set => SetPropertyValue(nameof(CompetencesCles), ref _competencesCles, value);
        }

        private SourceRecrutement _source;
        [XafDisplayName("Source initiale")]
        [ToolTip("Source par laquelle le candidat est arrivé en base (peut différer par candidature).")]
        public SourceRecrutement Source
        {
            get => _source;
            set => SetPropertyValue(nameof(Source), ref _source, value);
        }

        private DateTime _dateInscription;
        [XafDisplayName("Date d'inscription")]
        public DateTime DateInscription
        {
            get => _dateInscription;
            set => SetPropertyValue(nameof(DateInscription), ref _dateInscription, value);
        }

        // ── Candidatures associées ────────────────────────────────

        [Association("Candidat-Candidatures"), Aggregated]
        [XafDisplayName("Candidatures")]
        public XPCollection<Candidature> Candidatures => GetCollection<Candidature>(nameof(Candidatures));

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateInscription = DateTime.Today;
        }
    }

    /// <summary>Statut d'une candidature dans le pipeline.</summary>
    public enum CandidatureStatut
    {
        [XafDisplayName("Reçue")]              Recue = 0,
        [XafDisplayName("Pré-sélection")]      PreSelection = 10,
        [XafDisplayName("Entretien planifié")] EntretienPlanifie = 20,
        [XafDisplayName("Entretien fait")]     EntretienFait = 30,
        [XafDisplayName("Offre proposée")]     OffreProposee = 40,
        [XafDisplayName("Offre acceptée")]     OffreAcceptee = 50,
        [XafDisplayName("Embauché")]            Embauche = 60,
        [XafDisplayName("Refusé")]              Refuse = 90,
        [XafDisplayName("Désistement candidat")] Desistement = 91,
        [XafDisplayName("Annulé")]              Annule = 99
    }

    [DefaultClassOptions]
    [XafDisplayName("Candidature")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Sale_Item")]
    [NavigationItem("GRH - Recrutement")]
    public class Candidature : BaseObject
    {
        public Candidature(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = CandidatureStatut.Recue;
            DateSoumission = DateTime.Today;
        }

        // ── Liens ────────────────────────────────────────────────

        private Candidat _candidat;
        [RuleRequiredField, Association("Candidat-Candidatures")]
        [XafDisplayName("Candidat")]
        public Candidat Candidat
        {
            get => _candidat;
            set => SetPropertyValue(nameof(Candidat), ref _candidat, value);
        }

        private PosteVacant _poste;
        [RuleRequiredField]
        [XafDisplayName("Poste visé")]
        public PosteVacant Poste
        {
            get => _poste;
            set => SetPropertyValue(nameof(Poste), ref _poste, value);
        }

        private SourceRecrutement _sourcePourCePoste;
        [XafDisplayName("Source pour ce poste")]
        public SourceRecrutement SourcePourCePoste
        {
            get => _sourcePourCePoste;
            set => SetPropertyValue(nameof(SourcePourCePoste), ref _sourcePourCePoste, value);
        }

        // ── Suivi ────────────────────────────────────────────────

        private DateTime _dateSoumission;
        [XafDisplayName("Date de soumission")]
        public DateTime DateSoumission
        {
            get => _dateSoumission;
            set => SetPropertyValue(nameof(DateSoumission), ref _dateSoumission, value);
        }

        private CandidatureStatut _statut;
        [XafDisplayName("Statut")]
        public CandidatureStatut Statut
        {
            get => _statut;
            set => SetPropertyValue(nameof(Statut), ref _statut, value);
        }

        private DateTime _dateDecision;
        [XafDisplayName("Date de décision")]
        public DateTime DateDecision
        {
            get => _dateDecision;
            set => SetPropertyValue(nameof(DateDecision), ref _dateDecision, value);
        }

        // ── Refus ─────────────────────────────────────────────────

        private MotifRefusCandidat _motifRefusCandidat;
        [XafDisplayName("Motif refus (côté entreprise)")]
        public MotifRefusCandidat MotifRefusCandidat
        {
            get => _motifRefusCandidat;
            set => SetPropertyValue(nameof(MotifRefusCandidat), ref _motifRefusCandidat, value);
        }

        // ── Notation finale ───────────────────────────────────────

        private decimal _noteFinale;
        [XafDisplayName("Note finale (1-5)")]
        [ToolTip("Moyenne des notes d'entretien si plusieurs.")]
        public decimal NoteFinale
        {
            get => _noteFinale;
            set => SetPropertyValue(nameof(NoteFinale), ref _noteFinale, value);
        }

        // ── Embauche ──────────────────────────────────────────────

        private Salarie _salarieCree;
        [XafDisplayName("Salarié créé (si embauché)")]
        [ToolTip("Lien vers la fiche Salarié créée à l'embauche.")]
        public Salarie SalarieCree
        {
            get => _salarieCree;
            set => SetPropertyValue(nameof(SalarieCree), ref _salarieCree, value);
        }

        private string _commentaire;
        [Size(2000)]
        [XafDisplayName("Commentaire")]
        public string Commentaire
        {
            get => _commentaire;
            set => SetPropertyValue(nameof(Commentaire), ref _commentaire, value);
        }

        // ── Affichage ─────────────────────────────────────────────

        [VisibleInListView(false)]
        public string DisplayName =>
            $"{(Candidat?.NomComplet ?? "?")} — {(Poste?.Libelle ?? "?")} [{Statut}]";

        public override string ToString() => DisplayName ?? "";
    }
}
