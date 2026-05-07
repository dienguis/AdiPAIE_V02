// =============================================================================
//  PosteVacant.cs — V1.4 (mai 2026)
//
//  Représente un poste à pourvoir au sein d'ELTON.
//  Workflow simple via enum Statut (pas de XAF state machine pour rester léger).
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
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects.Recrutement
{
    /// <summary>Statut d'un poste vacant dans son cycle de vie.</summary>
    public enum PosteVacantStatut
    {
        [XafDisplayName("Brouillon")]      Brouillon = 0,
        [XafDisplayName("Validé DRH")]     Valide = 10,
        [XafDisplayName("Publié")]         Publie = 20,
        [XafDisplayName("En recrutement")] EnRecrutement = 30,
        [XafDisplayName("Pourvu")]         Pourvu = 40,
        [XafDisplayName("Annulé")]         Annule = 99
    }

    [DefaultClassOptions]
    [XafDisplayName("Poste vacant")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Task")]
    [NavigationItem("GRH - Recrutement")]
    [RuleCriteria("PosteVacant_FourchetteCoherente", DefaultContexts.Save,
        "FourchetteSalaireMin <= FourchetteSalaireMax OR FourchetteSalaireMax = 0",
        CustomMessageTemplate = "Le salaire minimum doit être inférieur ou égal au maximum.")]
    public class PosteVacant : BaseObject
    {
        public PosteVacant(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = PosteVacantStatut.Brouillon;
            DateOuverture = DateTime.Today;
        }

        // ── Identification ────────────────────────────────────────

        private string _code;
        [RuleRequiredField, RuleUniqueValue, Size(30)]
        [XafDisplayName("Code")]
        public string Code
        {
            get => _code;
            set => SetPropertyValue(nameof(Code), ref _code, value?.Trim().ToUpperInvariant());
        }

        private string _libelle;
        [RuleRequiredField, Size(150)]
        [XafDisplayName("Libellé poste")]
        public string Libelle
        {
            get => _libelle;
            set => SetPropertyValue(nameof(Libelle), ref _libelle, value?.Trim());
        }

        // ── Caractéristiques métier ───────────────────────────────

        private Departement _departement;
        [XafDisplayName("Département")]
        public Departement Departement
        {
            get => _departement;
            set => SetPropertyValue(nameof(Departement), ref _departement, value);
        }

        private Categories _categorie;
        [XafDisplayName("Catégorie professionnelle")]
        public Categories Categorie
        {
            get => _categorie;
            set => SetPropertyValue(nameof(Categorie), ref _categorie, value);
        }

        private Site _site;
        [XafDisplayName("Site d'affectation")]
        public Site Site
        {
            get => _site;
            set => SetPropertyValue(nameof(Site), ref _site, value);
        }

        private TypeContrat _typeContrat;
        [XafDisplayName("Type de contrat")]
        public TypeContrat TypeContrat
        {
            get => _typeContrat;
            set => SetPropertyValue(nameof(TypeContrat), ref _typeContrat, value);
        }

        private MotifOuverturePoste _motif;
        [XafDisplayName("Motif d'ouverture")]
        public MotifOuverturePoste Motif
        {
            get => _motif;
            set => SetPropertyValue(nameof(Motif), ref _motif, value);
        }

        private int _experienceRequiseAnnees;
        [XafDisplayName("Expérience requise (années)")]
        public int ExperienceRequiseAnnees
        {
            get => _experienceRequiseAnnees;
            set => SetPropertyValue(nameof(ExperienceRequiseAnnees), ref _experienceRequiseAnnees, value);
        }

        private string _profilRecherche;
        [XafDisplayName("Profil recherché")]
        [Size(2000)]
        public string ProfilRecherche
        {
            get => _profilRecherche;
            set => SetPropertyValue(nameof(ProfilRecherche), ref _profilRecherche, value);
        }

        // ── Fourchette salariale ──────────────────────────────────

        private decimal _fourchetteSalaireMin;
        [XafDisplayName("Salaire mini (FCFA)")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        public decimal FourchetteSalaireMin
        {
            get => _fourchetteSalaireMin;
            set => SetPropertyValue(nameof(FourchetteSalaireMin), ref _fourchetteSalaireMin, value);
        }

        private decimal _fourchetteSalaireMax;
        [XafDisplayName("Salaire maxi (FCFA)")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        public decimal FourchetteSalaireMax
        {
            get => _fourchetteSalaireMax;
            set => SetPropertyValue(nameof(FourchetteSalaireMax), ref _fourchetteSalaireMax, value);
        }

        // ── Cycle de vie ──────────────────────────────────────────

        private PosteVacantStatut _statut;
        [XafDisplayName("Statut")]
        public PosteVacantStatut Statut
        {
            get => _statut;
            set => SetPropertyValue(nameof(Statut), ref _statut, value);
        }

        private DateTime _dateOuverture;
        [XafDisplayName("Date d'ouverture")]
        public DateTime DateOuverture
        {
            get => _dateOuverture;
            set => SetPropertyValue(nameof(DateOuverture), ref _dateOuverture, value);
        }

        private DateTime _dateCloture;
        [XafDisplayName("Date de clôture")]
        public DateTime DateCloture
        {
            get => _dateCloture;
            set => SetPropertyValue(nameof(DateCloture), ref _dateCloture, value);
        }

        // ── Indicateur calculé ────────────────────────────────────

        /// <summary>Nombre de jours depuis l'ouverture (utile pour KPI "Délai recrutement").</summary>
        [XafDisplayName("Jours ouverts")]
        public int JoursOuverts
        {
            get
            {
                if (DateOuverture == default) return 0;
                var fin = (Statut == PosteVacantStatut.Pourvu || Statut == PosteVacantStatut.Annule)
                    ? (DateCloture != default ? DateCloture : DateTime.Today)
                    : DateTime.Today;
                return Math.Max(0, (fin - DateOuverture).Days);
            }
        }

        // ── Affichage ─────────────────────────────────────────────

        [VisibleInListView(false)]
        public string DisplayName => $"{Code} — {Libelle}";

        public override string ToString() => DisplayName ?? "";
    }
}
