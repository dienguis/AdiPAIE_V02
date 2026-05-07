// =============================================================================
//  EntretienOffreEtPeriodeEssai.cs — V1.4 (mai 2026)
//
//  - Entretien      : un rendez-vous mené sur une candidature (1..N par candidature)
//  - OffreEmploi    : proposition formelle envoyée au candidat (0..1 par candidature
//                     active, plusieurs si retouche/négociation)
//  - PeriodeEssai   : période d'essai du salarié embauché (suivi post-recrutement)
// =============================================================================

using System;
using System.ComponentModel;
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace AdiPAIE_V02.Module.BusinessObjects.Recrutement
{
    // =========================================================================
    //  ENTRETIEN
    // =========================================================================

    /// <summary>Type d'entretien dans le pipeline de recrutement.</summary>
    public enum TypeEntretien
    {
        [XafDisplayName("Téléphonique")]   Telephonique = 0,
        [XafDisplayName("RH")]             RH = 10,
        [XafDisplayName("Technique")]      Technique = 20,
        [XafDisplayName("Manager")]        Manager = 30,
        [XafDisplayName("Final / DG")]     Final = 40,
        [XafDisplayName("Test pratique")]  TestPratique = 50
    }

    /// <summary>Avis émis suite à un entretien.</summary>
    public enum AvisEntretien
    {
        [XafDisplayName("Non renseigné")]      NonRenseigne = 0,
        [XafDisplayName("Très favorable")]     TresFavorable = 10,
        [XafDisplayName("Favorable")]          Favorable = 20,
        [XafDisplayName("Mitigé")]             Mitige = 30,
        [XafDisplayName("Défavorable")]        Defavorable = 40
    }

    [DefaultClassOptions]
    [XafDisplayName("Entretien")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Appointment")]
    [NavigationItem("GRH - Recrutement")]
    public class Entretien : BaseObject
    {
        public Entretien(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateEntretien = DateTime.Today;
            Type = TypeEntretien.Telephonique;
            Avis = AvisEntretien.NonRenseigne;
        }

        // ── Lien candidature ──────────────────────────────────────

        private Candidature _candidature;
        [RuleRequiredField]
        [XafDisplayName("Candidature")]
        public Candidature Candidature
        {
            get => _candidature;
            set => SetPropertyValue(nameof(Candidature), ref _candidature, value);
        }

        // ── Planification ─────────────────────────────────────────

        private DateTime _dateEntretien;
        [RuleRequiredField]
        [XafDisplayName("Date entretien")]
        public DateTime DateEntretien
        {
            get => _dateEntretien;
            set => SetPropertyValue(nameof(DateEntretien), ref _dateEntretien, value);
        }

        private TypeEntretien _type;
        [XafDisplayName("Type")]
        public TypeEntretien Type
        {
            get => _type;
            set => SetPropertyValue(nameof(Type), ref _type, value);
        }

        private Salarie _intervieweur;
        [XafDisplayName("Intervieweur (salarié)")]
        [ToolTip("Salarié ELTON qui a mené l'entretien.")]
        public Salarie Intervieweur
        {
            get => _intervieweur;
            set => SetPropertyValue(nameof(Intervieweur), ref _intervieweur, value);
        }

        private string _lieu;
        [Size(120)]
        [XafDisplayName("Lieu / Plateforme")]
        [ToolTip("Salle siège, Visio Teams, Téléphone, etc.")]
        public string Lieu
        {
            get => _lieu;
            set => SetPropertyValue(nameof(Lieu), ref _lieu, value?.Trim());
        }

        // ── Évaluation ────────────────────────────────────────────

        private decimal _note;
        [XafDisplayName("Note (1-5)")]
        [ToolTip("Note globale de l'entretien.")]
        public decimal Note
        {
            get => _note;
            set => SetPropertyValue(nameof(Note), ref _note, value);
        }

        private AvisEntretien _avis;
        [XafDisplayName("Avis")]
        public AvisEntretien Avis
        {
            get => _avis;
            set => SetPropertyValue(nameof(Avis), ref _avis, value);
        }

        private string _pointsForts;
        [Size(2000)]
        [XafDisplayName("Points forts")]
        public string PointsForts
        {
            get => _pointsForts;
            set => SetPropertyValue(nameof(PointsForts), ref _pointsForts, value);
        }

        private string _pointsFaibles;
        [Size(2000)]
        [XafDisplayName("Points faibles / Réserves")]
        public string PointsFaibles
        {
            get => _pointsFaibles;
            set => SetPropertyValue(nameof(PointsFaibles), ref _pointsFaibles, value);
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
            $"{(Candidature?.Candidat?.NomComplet ?? "?")} — {Type} ({DateEntretien:dd/MM/yyyy})";

        public override string ToString() => DisplayName ?? "";
    }

    // =========================================================================
    //  OFFRE D'EMPLOI
    // =========================================================================

    /// <summary>Statut d'une offre proposée au candidat.</summary>
    public enum OffreEmploiStatut
    {
        [XafDisplayName("Préparée")]          Preparee = 0,
        [XafDisplayName("Envoyée")]           Envoyee = 10,
        [XafDisplayName("Acceptée")]          Acceptee = 20,
        [XafDisplayName("Refusée candidat")]  RefuseeCandidat = 30,
        [XafDisplayName("Retirée entreprise")] RetireeEntreprise = 40,
        [XafDisplayName("Expirée")]           Expiree = 50
    }

    [DefaultClassOptions]
    [XafDisplayName("Offre d'emploi")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Contract")]
    [NavigationItem("GRH - Recrutement")]
    public class OffreEmploi : BaseObject
    {
        public OffreEmploi(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = OffreEmploiStatut.Preparee;
            DateProposition = DateTime.Today;
        }

        // ── Lien candidature ──────────────────────────────────────

        private Candidature _candidature;
        [RuleRequiredField]
        [XafDisplayName("Candidature")]
        public Candidature Candidature
        {
            get => _candidature;
            set => SetPropertyValue(nameof(Candidature), ref _candidature, value);
        }

        // ── Contenu offre ─────────────────────────────────────────

        private DateTime _dateProposition;
        [XafDisplayName("Date proposition")]
        public DateTime DateProposition
        {
            get => _dateProposition;
            set => SetPropertyValue(nameof(DateProposition), ref _dateProposition, value);
        }

        private DateTime _dateValiditeFin;
        [XafDisplayName("Validité jusqu'au")]
        [ToolTip("Date au-delà de laquelle l'offre est considérée expirée.")]
        public DateTime DateValiditeFin
        {
            get => _dateValiditeFin;
            set => SetPropertyValue(nameof(DateValiditeFin), ref _dateValiditeFin, value);
        }

        private decimal _salaireProposeMensuel;
        [XafDisplayName("Salaire proposé (FCFA)")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        public decimal SalaireProposeMensuel
        {
            get => _salaireProposeMensuel;
            set => SetPropertyValue(nameof(SalaireProposeMensuel), ref _salaireProposeMensuel, value);
        }

        private decimal _avantagesMensuels;
        [XafDisplayName("Avantages mensuels (FCFA)")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ToolTip("Logement, transport, autres primes mensuelles.")]
        public decimal AvantagesMensuels
        {
            get => _avantagesMensuels;
            set => SetPropertyValue(nameof(AvantagesMensuels), ref _avantagesMensuels, value);
        }

        private DateTime _datePriseDePoste;
        [XafDisplayName("Date prise de poste prévue")]
        public DateTime DatePriseDePoste
        {
            get => _datePriseDePoste;
            set => SetPropertyValue(nameof(DatePriseDePoste), ref _datePriseDePoste, value);
        }

        // ── Statut + suivi ────────────────────────────────────────

        private OffreEmploiStatut _statut;
        [XafDisplayName("Statut")]
        public OffreEmploiStatut Statut
        {
            get => _statut;
            set => SetPropertyValue(nameof(Statut), ref _statut, value);
        }

        private DateTime _dateReponse;
        [XafDisplayName("Date réponse candidat")]
        public DateTime DateReponse
        {
            get => _dateReponse;
            set => SetPropertyValue(nameof(DateReponse), ref _dateReponse, value);
        }

        private MotifRefusOffre _motifRefus;
        [XafDisplayName("Motif refus (si refusée)")]
        [ToolTip("Motif côté candidat lorsqu'il décline l'offre.")]
        public MotifRefusOffre MotifRefus
        {
            get => _motifRefus;
            set => SetPropertyValue(nameof(MotifRefus), ref _motifRefus, value);
        }

        private string _commentaire;
        [Size(2000)]
        [XafDisplayName("Commentaire")]
        public string Commentaire
        {
            get => _commentaire;
            set => SetPropertyValue(nameof(Commentaire), ref _commentaire, value);
        }

        // ── Indicateurs calculés ──────────────────────────────────

        [XafDisplayName("Package mensuel total (FCFA)")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        public decimal PackageMensuelTotal => SalaireProposeMensuel + AvantagesMensuels;

        [XafDisplayName("Délai réponse (jours)")]
        public int DelaiReponseJours
        {
            get
            {
                if (DateProposition == default || DateReponse == default) return 0;
                return Math.Max(0, (DateReponse - DateProposition).Days);
            }
        }

        // ── Affichage ─────────────────────────────────────────────

        [VisibleInListView(false)]
        public string DisplayName =>
            $"Offre {(Candidature?.Candidat?.NomComplet ?? "?")} — {SalaireProposeMensuel:N0} FCFA [{Statut}]";

        public override string ToString() => DisplayName ?? "";
    }

    // =========================================================================
    //  PÉRIODE D'ESSAI
    // =========================================================================

    /// <summary>Issue de la période d'essai post-embauche.</summary>
    public enum PeriodeEssaiStatut
    {
        [XafDisplayName("En cours")]                     EnCours = 0,
        [XafDisplayName("Concluante (confirmé)")]        Concluante = 10,
        [XafDisplayName("Prolongée")]                    Prolongee = 20,
        [XafDisplayName("Rompue par employeur")]         RompueEmployeur = 30,
        [XafDisplayName("Rompue par salarié")]           RompueSalarie = 40,
        [XafDisplayName("Rupture commune accord")]       RuptureAccord = 50
    }

    [DefaultClassOptions]
    [XafDisplayName("Période d'essai")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Document")]
    [NavigationItem("GRH - Recrutement")]
    public class PeriodeEssai : BaseObject
    {
        public PeriodeEssai(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = PeriodeEssaiStatut.EnCours;
            DateDebut = DateTime.Today;
            DureeMois = 3; // standard CDI Sénégal
        }

        // ── Lien Salarié + Candidature d'origine ──────────────────

        private Salarie _salarie;
        [RuleRequiredField]
        [XafDisplayName("Salarié")]
        public Salarie Salarie
        {
            get => _salarie;
            set => SetPropertyValue(nameof(Salarie), ref _salarie, value);
        }

        private Candidature _candidatureOrigine;
        [XafDisplayName("Candidature d'origine")]
        [ToolTip("Lien vers la candidature qui a abouti à l'embauche (traçabilité).")]
        public Candidature CandidatureOrigine
        {
            get => _candidatureOrigine;
            set => SetPropertyValue(nameof(CandidatureOrigine), ref _candidatureOrigine, value);
        }

        // ── Périodes ──────────────────────────────────────────────

        private DateTime _dateDebut;
        [RuleRequiredField]
        [XafDisplayName("Date début")]
        public DateTime DateDebut
        {
            get => _dateDebut;
            set => SetPropertyValue(nameof(DateDebut), ref _dateDebut, value);
        }

        private int _dureeMois;
        [XafDisplayName("Durée prévue (mois)")]
        [ToolTip("Durée standard : 3 mois (cadres) ou 1 mois (employés/ouvriers) selon CCT Sénégal.")]
        public int DureeMois
        {
            get => _dureeMois;
            set => SetPropertyValue(nameof(DureeMois), ref _dureeMois, value);
        }

        private DateTime _dateFinPrevue;
        [XafDisplayName("Date fin prévue")]
        public DateTime DateFinPrevue
        {
            get => _dateFinPrevue;
            set => SetPropertyValue(nameof(DateFinPrevue), ref _dateFinPrevue, value);
        }

        private DateTime _dateFin;
        [XafDisplayName("Date fin réelle")]
        [ToolTip("Date de clôture effective (peut différer de la date prévue en cas de rupture anticipée).")]
        public DateTime DateFin
        {
            get => _dateFin;
            set => SetPropertyValue(nameof(DateFin), ref _dateFin, value);
        }

        // ── Statut + évaluation ───────────────────────────────────

        private PeriodeEssaiStatut _statut;
        [XafDisplayName("Statut")]
        public PeriodeEssaiStatut Statut
        {
            get => _statut;
            set => SetPropertyValue(nameof(Statut), ref _statut, value);
        }

        private decimal _evaluationFinale;
        [XafDisplayName("Évaluation finale (1-5)")]
        [ToolTip("Note d'évaluation du salarié au terme de la période d'essai.")]
        public decimal EvaluationFinale
        {
            get => _evaluationFinale;
            set => SetPropertyValue(nameof(EvaluationFinale), ref _evaluationFinale, value);
        }

        private string _appreciationManager;
        [Size(2000)]
        [XafDisplayName("Appréciation manager")]
        public string AppreciationManager
        {
            get => _appreciationManager;
            set => SetPropertyValue(nameof(AppreciationManager), ref _appreciationManager, value);
        }

        private string _motifRupture;
        [Size(500)]
        [XafDisplayName("Motif rupture (si rompue)")]
        public string MotifRupture
        {
            get => _motifRupture;
            set => SetPropertyValue(nameof(MotifRupture), ref _motifRupture, value);
        }

        private string _commentaire;
        [Size(2000)]
        [XafDisplayName("Commentaire")]
        public string Commentaire
        {
            get => _commentaire;
            set => SetPropertyValue(nameof(Commentaire), ref _commentaire, value);
        }

        // ── Indicateurs calculés ──────────────────────────────────

        [XafDisplayName("Concluante ?")]
        public bool EstConcluante =>
            Statut == PeriodeEssaiStatut.Concluante;

        [XafDisplayName("Rompue ?")]
        public bool EstRompue =>
            Statut == PeriodeEssaiStatut.RompueEmployeur
            || Statut == PeriodeEssaiStatut.RompueSalarie
            || Statut == PeriodeEssaiStatut.RuptureAccord;

        [XafDisplayName("Jours écoulés")]
        public int JoursEcoules
        {
            get
            {
                if (DateDebut == default) return 0;
                var fin = (DateFin != default) ? DateFin : DateTime.Today;
                return Math.Max(0, (fin - DateDebut).Days);
            }
        }

        // ── Affichage ─────────────────────────────────────────────

        [VisibleInListView(false)]
        public string DisplayName
        {
            get
            {
                var nom = Salarie != null
                    ? $"{Salarie.LastName} {Salarie.FirstName}".Trim()
                    : "?";
                return $"PE {nom} — {DateDebut:dd/MM/yyyy} [{Statut}]";
            }
        }

        public override string ToString() => DisplayName ?? "";
    }
}
