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
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    // ════════════════════════════════════════════════════════════════════
    // FICHE INTÉRIMAIRE
    // ════════════════════════════════════════════════════════════════════

    [DefaultClassOptions]
    [XafDisplayName("Intérimaire")]
    [DefaultProperty(nameof(FullName))]
    [ImageName("BO_Person")]
  //  [NavigationItem("GRH - Intérimaires")]
    [Appearance("Int_Inactif", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+InterimaireStatut,Inactif#",
        FontColor = "Gray", FontStyle = DevExpress.Drawing.DXFontStyle.Italic)]
    [Appearance("Int_Blackliste", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+InterimaireStatut,Blackliste#",
        FontColor = "Red")]
    public class Interimaire : BaseObject
    {
        public Interimaire(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = InterimaireStatut.Disponible;
            DateCreation = DateTime.Today;
            Matricule = GenererMatricule();
            try { CreePar = DevExpress.ExpressApp.SecuritySystem.CurrentUserName; } catch { }
        }

        // ── Matricule (identifiant unique) ────────────────────────────
        [Size(20)]
        [ModelDefault("AllowEdit", "False")]
        [RuleUniqueValue(DefaultContexts.Save,
            CustomMessageTemplate = "Ce matricule existe déjà.")]
        [XafDisplayName("Matricule")]
        public string Matricule
        {
            get => matricule;
            set => SetPropertyValue(nameof(Matricule), ref matricule, value);
        }
        string matricule;

        private string GenererMatricule()
        {
            // Compte les intérimaires créés cette année pour la séquence
            var annee = DateTime.Today.Year;
            try
            {
                var count = new DevExpress.Xpo.XPQuery<Interimaire>(Session)
                    .Count(i => i.DateCreation.Year == annee);
                return $"INT-{annee}-{(count + 1):D4}";
            }
            catch
            {
                return $"INT-{annee}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
            }
        }

        // ── Identité ──────────────────────────────────────────────────
        [RuleRequiredField]
        [Size(50)]
        [XafDisplayName("Nom")]
        public string Nom
        {
            get => nom;
            set => SetPropertyValue(nameof(Nom), ref nom, value?.Trim().ToUpper());
        }
        string nom;

        [RuleRequiredField]
        [Size(50)]
        [XafDisplayName("Prénom")]
        public string Prenom
        {
            get => prenom;
            set => SetPropertyValue(nameof(Prenom), ref prenom, value?.Trim());
        }
        string prenom;

        [NonPersistent]
        [XafDisplayName("Nom complet")]
        public string FullName => $"{Nom} {Prenom}".Trim();

        [Size(20)]
        [XafDisplayName("N° CNI / Passeport")]
        public string NumeroCNI
        {
            get => numeroCNI;
            set => SetPropertyValue(nameof(NumeroCNI), ref numeroCNI, value?.Trim());
        }
        string numeroCNI;

        [XafDisplayName("Date de naissance")]
        public DateTime? DateNaissance
        {
            get => dateNaissance;
            set => SetPropertyValue(nameof(DateNaissance), ref dateNaissance, value);
        }
        DateTime? dateNaissance;

        [Size(100)]
        [XafDisplayName("Nationalité")]
        public string Nationalite
        {
            get => nationalite;
            set => SetPropertyValue(nameof(Nationalite), ref nationalite, value?.Trim());
        }
        string nationalite;

        [Size(200)]
        [XafDisplayName("Adresse")]
        public string Adresse
        {
            get => adresse;
            set => SetPropertyValue(nameof(Adresse), ref adresse, value?.Trim());
        }
        string adresse;

        [Size(100)]
        [XafDisplayName("Email")]
        [RuleRegularExpression(@"^$|^[^@\s]+@[^@\s]+\.[^@\s]+$",
            CustomMessageTemplate = "Format email invalide.")]
        public string Email
        {
            get => email;
            set => SetPropertyValue(nameof(Email), ref email, value?.Trim().ToLower());
        }
        string email;

        [Size(20)]
        [XafDisplayName("Téléphone")]
        public string Telephone
        {
            get => telephone;
            set => SetPropertyValue(nameof(Telephone), ref telephone, value?.Trim());
        }
        string telephone;

        // ── Compétences / Poste ───────────────────────────────────────
        [Size(100)]
        [XafDisplayName("Fonction / Poste")]
        public string Fonction
        {
            get => fonction;
            set => SetPropertyValue(nameof(Fonction), ref fonction, value?.Trim());
        }
        string fonction;

        [FieldSize(FieldSizeAttribute.Unlimited)]
        [XafDisplayName("Compétences clés")]
        public string Competences
        {
            get => competences;
            set => SetPropertyValue(nameof(Competences), ref competences, value);
        }
        string competences;

        // ── Société d'intérim ─────────────────────────────────────────
        [RuleRequiredField]
        [XafDisplayName("Société d'intérim")]
        [Association("SocieteInterim-Interimaires")]
        public SocieteInterim SocieteInterim
        {
            get => societeInterim;
            set => SetPropertyValue(nameof(SocieteInterim), ref societeInterim, value);
        }
        SocieteInterim societeInterim;

        // ── Statut ────────────────────────────────────────────────────
        [XafDisplayName("Statut")]
        public InterimaireStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }
        InterimaireStatut statut;

        [XafDisplayName("Date de création")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime DateCreation
        {
            get => dateCreation;
            set => SetPropertyValue(nameof(DateCreation), ref dateCreation, value);
        }
        DateTime dateCreation;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Créé par")]
        public string CreePar
        {
            get => creePar;
            set => SetPropertyValue(nameof(CreePar), ref creePar, value);
        }
        string creePar;

        [FieldSize(FieldSizeAttribute.Unlimited)]
        [XafDisplayName("Observations")]
        public string Observations
        {
            get => observations;
            set => SetPropertyValue(nameof(Observations), ref observations, value);
        }
        string observations;

        // ── Collections ───────────────────────────────────────────────
        [Association("Interimaire-Contrats"), Aggregated]
        [XafDisplayName("Contrats")]
        public XPCollection<ContratInterim> Contrats
            => GetCollection<ContratInterim>(nameof(Contrats));

        [Association("Interimaire-Mouvements"), Aggregated]
        [XafDisplayName("Historique mouvements")]
        public XPCollection<MouvementInterimaire> Mouvements
            => GetCollection<MouvementInterimaire>(nameof(Mouvements));

        [Association("Interimaire-Alertes")]
        [XafDisplayName("Alertes")]
        public XPCollection<AlerteInterimaire> Alertes
            => GetCollection<AlerteInterimaire>(nameof(Alertes));

        [Association("Interimaire-Formations"), Aggregated]
        [XafDisplayName("Formations")]
        public XPCollection<FormationInterimaire> Formations
            => GetCollection<FormationInterimaire>(nameof(Formations));

        [Association("Interimaire-Evaluations"), Aggregated]
        [XafDisplayName("Évaluations")]
        public XPCollection<EvaluationInterimaire> Evaluations
            => GetCollection<EvaluationInterimaire>(nameof(Evaluations));

        // ── Propriétés calculées ──────────────────────────────────────
        [NonPersistent]
        [XafDisplayName("Affectation actuelle")]
        public string AffectationActuelle
        {
            get
            {
                var c = ContratActif;
                if (c == null) return "—";
                if (c.EstDG) return "Direction Générale";
                var bu = c.BU?.Libelle ?? "—";
                var sta = c.Station?.Nom ?? "—";
                return $"{sta} / {bu}";
            }
        }

        [NonPersistent]
        [Browsable(false)]
        public ContratInterim ContratActif =>
            Contrats.FirstOrDefault(c => c.Statut == ContratInterimStatut.EnCours);

        [NonPersistent]
        [XafDisplayName("Jours avant fin mission")]
        public int? JoursAvantFinMission
        {
            get
            {
                var c = ContratActif;
                return c == null ? (int?)null : (c.DateFin - DateTime.Today).Days;
            }
        }

        public override string ToString() => $"{Matricule} — {FullName}";
    }

    // ════════════════════════════════════════════════════════════════════
    // CONTRAT INTÉRIMAIRE
    // ════════════════════════════════════════════════════════════════════

    [DefaultClassOptions]
    [XafDisplayName("Contrat intérimaire")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Contract")]
   // [NavigationItem("GRH - Intérimaires")]
    [Appearance("CI_Termine", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+ContratInterimStatut,Termine#",
        FontColor = "Gray", FontStyle = DevExpress.Drawing.DXFontStyle.Italic)]
    [Appearance("CI_Resilie", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+ContratInterimStatut,Resilie#",
        FontColor = "Red")]
    [RuleCriteria("CI_DateFin_GTE_DateDebut", DefaultContexts.Save,
        "DateFin >= DateDebut",
        CustomMessageTemplate = "La date de fin doit être >= à la date de début.")]
    public class ContratInterim : BaseObject
    {
        public ContratInterim(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = ContratInterimStatut.EnCours;
            TypeContrat = ContratInterimType.PremiereMission;
            DateDebut = DateTime.Today;
            DateFin = DateTime.Today.AddMonths(1);
            EstDG = false;
        }

        [Association("Interimaire-Contrats")]
        [RuleRequiredField]
        [XafDisplayName("Intérimaire")]
        public Interimaire Interimaire
        {
            get => interimaire;
            set => SetPropertyValue(nameof(Interimaire), ref interimaire, value);
        }
        Interimaire interimaire;

        [XafDisplayName("Type")]
        public ContratInterimType TypeContrat
        {
            get => typeContrat;
            set => SetPropertyValue(nameof(TypeContrat), ref typeContrat, value);
        }
        ContratInterimType typeContrat;

        [XafDisplayName("Statut")]
        public ContratInterimStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }
        ContratInterimStatut statut;

        // ── ⚠️ V1.1 Sprint 1D — Affectation LEGACY (Station / BU / EstDG) ─
        //   Ces 3 FK sont conservées pour la compat des écrans hors dashboards
        //   (DemandeRecrutementInterim, AlerteInterimaireService) mais NE
        //   doivent PLUS être saisies. Utilisez Site (V1.1) + Unites à la place.
        //   Les dashboards V1.1 lisent uniquement Site/Unites.

        [VisibleInListView(false)]
        [XafDisplayName("[Legacy] Direction Générale")]
        [ImmediatePostData]
        public bool EstDG
        {
            get => estDG;
            set
            {
                SetPropertyValue(nameof(EstDG), ref estDG, value);
                if (value)
                {
                    Station = null;
                    BU = null;
                }
            }
        }
        bool estDG;

        [VisibleInListView(false)]
        [Association("Station-Contrats")]
        [XafDisplayName("[Legacy] Station de service")]
        [DataSourceCriteria("Actif = true")]
        [Appearance("CI_StationDisabled", Criteria = "EstDG = true", Enabled = false,
            TargetItems = "Station")]
        public StationService Station
        {
            get => station;
            set
            {
                SetPropertyValue(nameof(Station), ref station, value);
                if (BU != null && BU.Station?.Oid != value?.Oid)
                    BU = null;
            }
        }
        StationService station;

        [VisibleInListView(false)]
        [Association("BU-Contrats")]
        [XafDisplayName("[Legacy] Business Unit")]
        [DataSourceCriteria("Actif = true AND Station.Oid = '@This.Station.Oid'")]
        [Appearance("CI_BUDisabled", Criteria = "EstDG = true OR Station Is Null", Enabled = false,
            TargetItems = "BU")]
        public BusinessUnitStation BU
        {
            get => bu;
            set => SetPropertyValue(nameof(BU), ref bu, value);
        }
        BusinessUnitStation bu;

        // ════════════════════════════════════════════════════════════════
        //  V1.1 — Affectation unifiée (cohabitation avec Station/BU/EstDG)
        // ════════════════════════════════════════════════════════════════
        // À terme (V1.2) : suppression de Station/BU/EstDG → ne reste que
        // Site + Unites. Pour l'instant on coexiste pour ne pas casser
        // les vues XAF et services existants.

        /// <summary>
        /// Site d'affectation principal (Station / Siège / Dépôt).
        /// Remplace progressivement la combinaison Station + EstDG.
        /// </summary>
        [Association("Site-ContratsInterim")]
        [XafDisplayName("Site (V1.1)")]
        [DataSourceCriteria("Actif = true")]
        public Site Site
        {
            get => siteV2;
            set => SetPropertyValue(nameof(Site), ref siteV2, value);
        }
        Site siteV2;

        /// <summary>
        /// Multi-affectation sur plusieurs unités organisationnelles
        /// (BU / Département / Segment). N-N sans notion de % de temps.
        /// </summary>
        [Association("Contrat-Unites")]
        [XafDisplayName("Unités (V1.1)")]
        public XPCollection<UniteOrganisationnelle> Unites
            => GetCollection<UniteOrganisationnelle>(nameof(Unites));

        [XafDisplayName("Poste occupé")]
        [DataSourceCriteria("Actif = true")]
        public PosteInterimaire PosteOccupe
        {
            get => posteOccupe;
            set => SetPropertyValue(nameof(PosteOccupe), ref posteOccupe, value);
        }
        PosteInterimaire posteOccupe;

        [VisibleInListView(false)]
        [Size(300)]
        [RuleRequiredField]
        [XafDisplayName("Motif du recours")]
        public string MotifRecours
        {
            get => motifRecours;
            set => SetPropertyValue(nameof(MotifRecours), ref motifRecours, value?.Trim());
        }
        string motifRecours;

        // ── Dates ─────────────────────────────────────────────────────
        [RuleRequiredField]
        [XafDisplayName("Date de début")]
        public DateTime DateDebut
        {
            get => dateDebut;
            set => SetPropertyValue(nameof(DateDebut), ref dateDebut, value);
        }
        DateTime dateDebut;

        [RuleRequiredField]
        [XafDisplayName("Date de fin")]
        public DateTime DateFin
        {
            get => dateFin;
            set => SetPropertyValue(nameof(DateFin), ref dateFin, value);
        }
        DateTime dateFin;

        [VisibleInListView(false)]
        [XafDisplayName("Date fin réelle")]
        public DateTime? DateFinReelle
        {
            get => dateFinReelle;
            set => SetPropertyValue(nameof(DateFinReelle), ref dateFinReelle, value);
        }
        DateTime? dateFinReelle;

        // ── Financier ─────────────────────────────────────────────────
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        [XafDisplayName("Taux journalier (FCFA)")]
        public decimal TauxJournalier
        {
            get => tauxJournalier;
            set => SetPropertyValue(nameof(TauxJournalier), ref tauxJournalier, value);
        }
        decimal tauxJournalier;

        [NonPersistent]
        [ModelDefault("DisplayFormat", "N0")]
        [XafDisplayName("Coût total estimé (FCFA)")]
        public decimal CoutTotalEstime =>
            TauxJournalier * Math.Max(0, (DateFin - DateDebut).Days);

        // ── Référence contrat société d'intérim ───────────────────────
        [VisibleInListView(false)]
        [Size(50)]
        [XafDisplayName("Réf. contrat société intérim")]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value?.Trim());
        }
        string reference;

        [VisibleInListView(false)]
        [FieldSize(FieldSizeAttribute.Unlimited)]
        [XafDisplayName("Observations")]
        public string Observations
        {
            get => observations;
            set => SetPropertyValue(nameof(Observations), ref observations, value);
        }
        string observations;

        // ── Renouvellement ────────────────────────────────────────────
        [VisibleInListView(false)]
        [XafDisplayName("Contrat précédent (renouvellement)")]
        [Association("ContratPrecedent-Renouvellements")]
        public ContratInterim ContratPrecedent
        {
            get => contratPrecedent;
            set => SetPropertyValue(nameof(ContratPrecedent), ref contratPrecedent, value);
        }
        ContratInterim contratPrecedent;

        [Association("ContratPrecedent-Renouvellements")]
        [XafDisplayName("Renouvellements")]
        public XPCollection<ContratInterim> Renouvellements
            => GetCollection<ContratInterim>(nameof(Renouvellements));

        // ── Propriétés calculées ──────────────────────────────────────
        [NonPersistent]
        public string DisplayName
        {
            get
            {
                var nom = Interimaire?.FullName ?? "—";
                var lieu = EstDG ? "DG" : BU != null
                    ? $"{Station?.Nom ?? "—"} / {BU.Libelle}"
                    : Station?.Nom ?? "—";
                return $"{nom} — {lieu} ({DateDebut:MM/yyyy}→{DateFin:MM/yyyy})";
            }
        }

        [VisibleInListView(false)]
        [NonPersistent]
        [XafDisplayName("Jours restants")]
        public int JoursRestants => Math.Max(0, (DateFin - DateTime.Today).Days);

        [VisibleInListView(false)]
        [NonPersistent]
        [XafDisplayName("Durée (jours)")]
        public int DureeJours => Math.Max(0, (DateFin - DateDebut).Days);

        public override string ToString() => DisplayName;
    }

    // ════════════════════════════════════════════════════════════════════
    // MOUVEMENT INTÉRIMAIRE
    // ════════════════════════════════════════════════════════════════════

    [DefaultClassOptions]
    [XafDisplayName("Mouvement intérimaire")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("Action_Forward")]
    //[NavigationItem("GRH - Intérimaires")]
    [Appearance("Mvt_NonValide", TargetItems = "*",
        Criteria = "ValideRH = false",
        FontColor = "DarkOrange")]
    public class MouvementInterimaire : BaseObject
    {
        public MouvementInterimaire(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateMouvement = DateTime.Today;
            ValideRH = false;
            try { SaisiPar = DevExpress.ExpressApp.SecuritySystem.CurrentUserName; } catch { }
        }

        [Association("Interimaire-Mouvements")]
        [RuleRequiredField]
        [XafDisplayName("Intérimaire")]
        public Interimaire Interimaire
        {
            get => interimaire;
            set => SetPropertyValue(nameof(Interimaire), ref interimaire, value);
        }
        Interimaire interimaire;

        [RuleRequiredField]
        [XafDisplayName("Type de mouvement")]
        public MouvementInterimaireType? TypeMouvement
        {
            get => typeMouvement;
            set => SetPropertyValue(nameof(TypeMouvement), ref typeMouvement, value);
        }
        MouvementInterimaireType? typeMouvement;

        [RuleRequiredField]
        [XafDisplayName("Date")]
        public DateTime DateMouvement
        {
            get => dateMouvement;
            set => SetPropertyValue(nameof(DateMouvement), ref dateMouvement, value);
        }
        DateTime dateMouvement;

        // ── Origine [Legacy V1.0] — masqué UI depuis V1.5 ─────────────
        // Données conservées en BD pour ne pas casser les services qui les
        // utilisent encore. À supprimer définitivement en V1.6 après migration.
        [XafDisplayName("[Legacy] Station origine")]
        [VisibleInListView(false), VisibleInDetailView(false)]
        public StationService StationOrigine
        {
            get => stationOrigine;
            set => SetPropertyValue(nameof(StationOrigine), ref stationOrigine, value);
        }
        StationService stationOrigine;

        [XafDisplayName("[Legacy] BU origine")]
        [DataSourceCriteria("Station.Oid = '@This.StationOrigine.Oid'")]
        [VisibleInListView(false), VisibleInDetailView(false)]
        public BusinessUnitStation BUOrigine
        {
            get => buOrigine;
            set => SetPropertyValue(nameof(BUOrigine), ref buOrigine, value);
        }
        BusinessUnitStation buOrigine;

        [XafDisplayName("[Legacy] DG → Station (origine)")]
        [VisibleInListView(false), VisibleInDetailView(false)]
        public bool OrigineEstDG
        {
            get => origineEstDG;
            set => SetPropertyValue(nameof(OrigineEstDG), ref origineEstDG, value);
        }
        bool origineEstDG;

        // ── Destination [Legacy V1.0] — masqué UI depuis V1.5 ─────────
        [XafDisplayName("[Legacy] Station destination")]
        [VisibleInListView(false), VisibleInDetailView(false)]
        public StationService StationDestination
        {
            get => stationDestination;
            set => SetPropertyValue(nameof(StationDestination), ref stationDestination, value);
        }
        StationService stationDestination;

        [XafDisplayName("[Legacy] BU destination")]
        [DataSourceCriteria("Station.Oid = '@This.StationDestination.Oid'")]
        [VisibleInListView(false), VisibleInDetailView(false)]
        public BusinessUnitStation BUDestination
        {
            get => buDestination;
            set => SetPropertyValue(nameof(BUDestination), ref buDestination, value);
        }
        BusinessUnitStation buDestination;

        [XafDisplayName("[Legacy] Destination = Direction Générale")]
        [VisibleInListView(false), VisibleInDetailView(false)]
        public bool DestinationEstDG
        {
            get => destinationEstDG;
            set => SetPropertyValue(nameof(DestinationEstDG), ref destinationEstDG, value);
        }
        bool destinationEstDG;

        // ════════════════════════════════════════════════════════════════
        //  V1.1 — Mouvements via Site + Unité (cohabitation)
        // ════════════════════════════════════════════════════════════════

        [XafDisplayName("Site origine (V1.1)")]
        public Site SiteOrigineV1
        {
            get => siteOrigineV1;
            set => SetPropertyValue(nameof(SiteOrigineV1), ref siteOrigineV1, value);
        }
        Site siteOrigineV1;

        [XafDisplayName("Unité origine (V1.1)")]
        [DataSourceCriteria("Site.Oid = '@This.SiteOrigineV1.Oid'")]
        public UniteOrganisationnelle UniteOrigineV1
        {
            get => uniteOrigineV1;
            set => SetPropertyValue(nameof(UniteOrigineV1), ref uniteOrigineV1, value);
        }
        UniteOrganisationnelle uniteOrigineV1;

        [XafDisplayName("Site destination (V1.1)")]
        public Site SiteDestinationV1
        {
            get => siteDestinationV1;
            set => SetPropertyValue(nameof(SiteDestinationV1), ref siteDestinationV1, value);
        }
        Site siteDestinationV1;

        [XafDisplayName("Unité destination (V1.1)")]
        [DataSourceCriteria("Site.Oid = '@This.SiteDestinationV1.Oid'")]
        public UniteOrganisationnelle UniteDestinationV1
        {
            get => uniteDestinationV1;
            set => SetPropertyValue(nameof(UniteDestinationV1), ref uniteDestinationV1, value);
        }
        UniteOrganisationnelle uniteDestinationV1;

        // ── Motif & validation RH ─────────────────────────────────────
        [Size(500)]
        [XafDisplayName("Motif")]
        public string Motif
        {
            get => motif;
            set => SetPropertyValue(nameof(Motif), ref motif, value?.Trim());
        }
        string motif;

        [XafDisplayName("Validé par RH")]
        public bool ValideRH
        {
            get => valideRH;
            set => SetPropertyValue(nameof(ValideRH), ref valideRH, value);
        }
        bool valideRH;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Validé par")]
        public string ValideParNom
        {
            get => valideParNom;
            set => SetPropertyValue(nameof(ValideParNom), ref valideParNom, value);
        }
        string valideParNom;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date validation RH")]
        public DateTime? DateValidation
        {
            get => dateValidation;
            set => SetPropertyValue(nameof(DateValidation), ref dateValidation, value);
        }
        DateTime? dateValidation;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Saisi par")]
        public string SaisiPar
        {
            get => saisiPar;
            set => SetPropertyValue(nameof(SaisiPar), ref saisiPar, value);
        }
        string saisiPar;

        [NonPersistent]
        public string DisplayName
        {
            get
            {
                var dest = DestinationEstDG ? "DG" :
                    BUDestination != null
                        ? $"{StationDestination?.Nom ?? "—"}/{BUDestination.Libelle}"
                        : StationDestination?.Nom ?? "—";
                return $"{Interimaire?.Matricule ?? "—"} {Interimaire?.FullName ?? "—"} "
                     + $"— {TypeMouvement} → {dest} ({DateMouvement:dd/MM/yyyy})";
            }
        }

        public override string ToString() => DisplayName;
    }
}
