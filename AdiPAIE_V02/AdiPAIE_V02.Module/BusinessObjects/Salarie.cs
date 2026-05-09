using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.Services;
using DevExpress.Data.Filtering;
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [ImageName("BO_Employee")]  // V1.1 — icône XAF native pour cohérence visuelle
    [DefaultProperty(nameof(Person.FullName))]
    [RuleCriteria(
        "Salarie_MustBeMarried_IfAnyCurrentSpouse",
        DefaultContexts.Save,
        "Conjoints[IsNull(DateFinUnion)].Count() = 0 OR SatutMarital = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SituationMaritale,Marie#",
        CustomMessageTemplate = "Impossible de quitter l'état 'Marié' tant qu'un conjoint est en cours."
    )]
    [Appearance(
        "Disable_SatutMarital_WhenCurrentSpouseExists",
        Criteria = "Conjoints[IsNull(DateFinUnion)].Count() > 0",
        Enabled = false,
        TargetItems = nameof(SatutMarital)
    )]
    [Appearance(
        "Disable_Conjoints_When_NotMarried",
        Criteria = "SatutMarital <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SituationMaritale,Marie#",
        Enabled = false,
        TargetItems = nameof(Conjoints)
    )]
    [RuleCriteria("Salarie_CatMustMatchEchelon",
        DefaultContexts.Save,
        "IsNull(Echelon) OR Categories = Echelon.Categories",
        CustomMessageTemplate = "La catégorie du salarié doit correspondre à celle de l'échelon.")]
    [RuleCriteria("Salarie_ConvMustMatchEchelon",
        DefaultContexts.Save,
        "IsNull(Echelon) OR Convention = Echelon.Categories.Convention",
        CustomMessageTemplate = "La convention du salarié doit correspondre à celle de l'échelon.")]
    [Appearance(
        "DisablePrimeTransportWhenHasVehicle",
        TargetItems = nameof(PrimeTransport),
        Criteria = nameof(PossedeVehicule) + " = True",
        Enabled = false)]
    [Appearance(
        "DisablePrimeTransportWhenAvantageVehicule",
        TargetItems = nameof(PrimeTransport),
        Criteria = nameof(AvantageVehicule) + " > 0",
        Enabled = false)]
    [Appearance(
        "DisableAvantageVehiculeWhenNoVehicle",
        TargetItems = nameof(AvantageVehicule),
        Criteria = nameof(PossedeVehicule) + " = False",
        Enabled = false)]
    [Appearance(
        "HideMotifDepart_SiPasDeDepart",
        AppearanceItemType = "ViewItem",
        TargetItems = "MotifDepart",
        Criteria = "GetYear(DateSortie) < 2",
        Visibility = ViewItemVisibility.Hide)]
    [RuleCriteria("Salarie_Manager_NotSelf",
        DefaultContexts.Save,
        "IsNull(Manager) OR Manager.Oid != Oid",
        CustomMessageTemplate = "Un salarié ne peut pas être son propre responsable.")]
    [RuleCriteria("Salarie_DateSortie_GTE_DateEmbauche",
        DefaultContexts.Save,
        "DateSortie = #01/01/0001# OR DateSortie >= DateEmbauche",
        CustomMessageTemplate = "La date de sortie ne peut pas être antérieure à la date d'embauche.")]
    [RuleCriteria("Salarie_SalaireBase_Positif",
        DefaultContexts.Save,
        "Not IsNull(Echelon) OR SalaireBase >= 0",
        CustomMessageTemplate = "Le salaire de base doit être positif (ou affectez un échelon).")]
    [RuleCriteria("Salarie_Remunerations_NonNegatives",
        DefaultContexts.Save,
        "IndemniteLogement >= 0 AND Sursalaire >= 0 AND PrimeTransport >= 0 AND AvantageVehicule >= 0",
        CustomMessageTemplate = "Les montants de rémunération ne peuvent pas être négatifs.")]
    // V1.6.1 — Badge statut coloré (vert / orange / gris)
    [Appearance("Salarie_Statut_Actif",
        TargetItems = "StatutAffichage",
        Criteria = "IsActif = True AND (IsNull(DateConfirmation) OR DateConfirmation <= LocalDateTimeToday())",
        BackColor = "PaleGreen", FontColor = "DarkGreen", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Salarie_Statut_PeriodeEssai",
        TargetItems = "StatutAffichage",
        Criteria = "IsActif = True AND DateConfirmation > LocalDateTimeToday()",
        BackColor = "Moccasin", FontColor = "DarkOrange", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Salarie_Statut_Inactif",
        TargetItems = "StatutAffichage",
        Criteria = "IsActif = False",
        BackColor = "Gainsboro", FontColor = "DimGray", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    // V1.6.1 — Alertes visuelles sur dates critiques
    // CNI expirée : rouge soutenu — action urgente
    [Appearance("Salarie_CNI_Expiree",
        TargetItems = "DateExpirationCNI",
        Criteria = "Not IsNull(DateExpirationCNI) AND DateExpirationCNI < LocalDateTimeToday()",
        BackColor = "LightCoral", FontColor = "DarkRed", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    // CNI expire bientôt (≤ 60 jours) : orange — anticipation
    [Appearance("Salarie_CNI_BientotExpiree",
        TargetItems = "DateExpirationCNI",
        Criteria = "Not IsNull(DateExpirationCNI) AND DateExpirationCNI >= LocalDateTimeToday() AND DateExpirationCNI <= AddDays(LocalDateTimeToday(), 60)",
        BackColor = "Moccasin", FontColor = "DarkOrange", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    // Passeport expiré : rouge
    [Appearance("Salarie_Passeport_Expire",
        TargetItems = "DateExpirationPasseport",
        Criteria = "Not IsNull(DateExpirationPasseport) AND DateExpirationPasseport < LocalDateTimeToday()",
        BackColor = "LightCoral", FontColor = "DarkRed", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    // Passeport expire bientôt (≤ 90 jours) : orange
    [Appearance("Salarie_Passeport_BientotExpire",
        TargetItems = "DateExpirationPasseport",
        Criteria = "Not IsNull(DateExpirationPasseport) AND DateExpirationPasseport >= LocalDateTimeToday() AND DateExpirationPasseport <= AddDays(LocalDateTimeToday(), 90)",
        BackColor = "Moccasin", FontColor = "DarkOrange", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    // Fin période d'essai dans ≤ 15 jours : orange — RH doit décider
    [Appearance("Salarie_FinPeriodeEssai_Approche",
        TargetItems = "DateConfirmation",
        Criteria = "Not IsNull(DateConfirmation) AND DateConfirmation > LocalDateTimeToday() AND DateConfirmation <= AddDays(LocalDateTimeToday(), 15)",
        BackColor = "Moccasin", FontColor = "DarkOrange", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [DeferredDeletion(false)]
    public class Salarie : Person
    {
        public Salarie(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            IsActif = true;
            DateCreation = DateTime.Now;
            try { CreePar = SecuritySystem.CurrentUserName; } catch { }
            Base30Jour = 30;
        }

        // ── Backing fields ────────────────────────────────────
        string creePar;
        DateTime dateCreation;
        int base30Jour;
        string numeroCNI;
        string numeroIPRESS;
        string caisseSecurite;
        decimal indemniteLogement;
        decimal salaireBase;
        Convention convention;
        Categories categories;
        bool isActif;
        MotifDepart? motifDepart;
        DateTime dateSortie;
        DateTime dateEmbauche;
        int nombreEnfant;
        SituationMaritale satutMarital;
        string nationalite;
        Civilite civilite;
        string matricule;
        // Nouveaux champs
        string lieuNaissance;
        string paysNaissance;
        DateTime? dateExpirationCNI;
        string numeroPasseport;
        DateTime? dateExpirationPasseport;
        DateTime? dateConfirmation;
        string contactUrgenceNom;
        string contactUrgenceTel;
        string contactUrgenceLien;
        string groupeSanguin;
        string permisConduire;
        string filsDe;

        // ── Identité ──────────────────────────────────────────
        [Size(20)]
        [RuleRequiredField]
        [RuleUniqueValue(DefaultContexts.Save,
            CustomMessageTemplate = "Ce matricule est déjà utilisé par un autre salarié.")]
        // V1.6 — Matricule verrouillé après création :
        // - clé de mapping JDE (le modifier romprait la liaison Répertoire d'adresses)
        // - référencé dans bulletins, contrats, historiques, audit
        // Reste éditable en saisie initiale (IsNewObject = true) puis grisé à vie.
        [Appearance("Salarie_Matricule_LockAfterCreate",
            Criteria = "Not IsNewObject(this)",
            Enabled = false,
            TargetItems = nameof(Matricule))]
        public string Matricule
        {
            get => matricule;
            set => SetPropertyValue(nameof(Matricule), ref matricule, value);
        }

        [VisibleInListView(false)]
        [XafDisplayName("Civilité")]
        public Civilite Civilite
        {
            get => civilite;
            set
            {
                var old = civilite;
                SetPropertyValue(nameof(Civilite), ref civilite, value);
                if (old != value) SyncSexeFromCivilite();
            }
        }

        [VisibleInListView(false)]
        [Size(50)]
        public string Nationalite
        {
            get => nationalite;
            set => SetPropertyValue(nameof(Nationalite), ref nationalite, value);
        }

        [VisibleInListView(false)]
        [Size(200)]
        [Category("Identité")]
        [XafDisplayName("Fils / Fille de")]
        public string FilsDe
        {
            get => filsDe;
            set => SetPropertyValue(nameof(FilsDe), ref filsDe, value?.Trim());
        }

        [VisibleInListView(false)]
        [ImmediatePostData]
        public SituationMaritale SatutMarital
        {
            get => satutMarital;
            set => SetPropertyValue(nameof(SatutMarital), ref satutMarital, value);
        }

        [VisibleInListView(false)]
        [RuleRange(0, int.MaxValue)]
        [XafDisplayName("Nombre d'enfants")]
        public int NombreEnfant
        {
            get => nombreEnfant;
            set => SetPropertyValue(nameof(NombreEnfant), ref nombreEnfant, value);
        }

        public DateTime DateEmbauche
        {
            get => dateEmbauche;
            set => SetPropertyValue(nameof(DateEmbauche), ref dateEmbauche, value);
        }

        [VisibleInListView(false)]
        [ImmediatePostData]
        public DateTime DateSortie
        {
            get => dateSortie;
            set => SetPropertyValue(nameof(DateSortie), ref dateSortie, value);
        }

        [VisibleInListView(false)]
        [XafDisplayName("Motif de départ")]
        public MotifDepart? MotifDepart
        {
            get => motifDepart;
            set => SetPropertyValue(nameof(MotifDepart), ref motifDepart, value);
        }

        [XafDisplayName("Salarié Actif")]
        public bool IsActif
        {
            get => isActif;
            set => SetPropertyValue(nameof(IsActif), ref isActif, value);
        }

        [Action(Caption = "Activer", ImageName = "BO_Task",
            TargetObjectsCriteria = "IsActif=false", AutoCommit = true)]
        public void Active() => IsActif = true;

        [Action(Caption = "Désactiver", ImageName = "BO_Task",
            TargetObjectsCriteria = "IsActif=true", AutoCommit = true)]
        public void Desactive() => IsActif = false;

        // ── V1.5 — Mapping Assistant Commercial → Stations sous responsabilité
        // Utilisé par DemandeMouvementInterim pour filtrer les intérimaires
        // que l'AC peut sélectionner. Symétrique côté StationService (collection
        // AssistantsCommerciaux). Vide pour les non-AC.
        [Association("AC-StationsGerees")]
        [XafDisplayName("Stations sous responsabilité (AC)")]
        [VisibleInListView(false)]
        public XPCollection<StationService> StationsGerees =>
            GetCollection<StationService>(nameof(StationsGerees));

        // ── État civil complet ────────────────────────────────
        [VisibleInListView(false)]
        [Size(100)]
        [XafDisplayName("Lieu de naissance")]
        public string LieuNaissance
        {
            get => lieuNaissance;
            set => SetPropertyValue(nameof(LieuNaissance), ref lieuNaissance, value?.Trim());
        }

        [VisibleInListView(false)]
        [Size(60)]
        [XafDisplayName("Pays de naissance")]
        public string PaysNaissance
        {
            get => paysNaissance;
            set => SetPropertyValue(nameof(PaysNaissance), ref paysNaissance, value?.Trim());
        }

        // ── Pièces d'identité ─────────────────────────────────
        [VisibleInListView(false)]
        [Size(50)]
        public string NumeroCNI
        {
            get => numeroCNI;
            set => SetPropertyValue(nameof(NumeroCNI), ref numeroCNI, value?.Trim());
        }

        [VisibleInListView(false)]
        [XafDisplayName("Date expiration CNI")]
        public DateTime? DateExpirationCNI
        {
            get => dateExpirationCNI;
            set => SetPropertyValue(nameof(DateExpirationCNI), ref dateExpirationCNI, value);
        }

        [VisibleInListView(false)]
        [Size(30)]
        [XafDisplayName("Numéro passeport")]
        public string NumeroPasseport
        {
            get => numeroPasseport;
            set => SetPropertyValue(nameof(NumeroPasseport), ref numeroPasseport, value?.Trim());
        }

        [VisibleInListView(false)]
        [XafDisplayName("Date expiration passeport")]
        public DateTime? DateExpirationPasseport
        {
            get => dateExpirationPasseport;
            set => SetPropertyValue(nameof(DateExpirationPasseport), ref dateExpirationPasseport, value);
        }

        [VisibleInListView(false)]
        [Size(50)]
        public string NumeroIPRESS
        {
            get => numeroIPRESS;
            set => SetPropertyValue(nameof(NumeroIPRESS), ref numeroIPRESS, value?.Trim());
        }

        [VisibleInListView(false)]
        [Size(50)]
        public string CaisseSecurite
        {
            get => caisseSecurite;
            set => SetPropertyValue(nameof(CaisseSecurite), ref caisseSecurite, value?.Trim());
        }

        // ── Contrat ───────────────────────────────────────────
        [VisibleInListView(false)]
        [XafDisplayName("Date de confirmation")]
        [ToolTip("Fin de période d'essai")]
        public DateTime? DateConfirmation
        {
            get => dateConfirmation;
            set => SetPropertyValue(nameof(DateConfirmation), ref dateConfirmation, value);
        }

        // ── Contact d'urgence ─────────────────────────────────
        [VisibleInListView(false)]
        [Size(100)]
        [XafDisplayName("Contact urgence (nom)")]
        public string ContactUrgenceNom
        {
            get => contactUrgenceNom;
            set => SetPropertyValue(nameof(ContactUrgenceNom), ref contactUrgenceNom, value?.Trim());
        }

        [VisibleInListView(false)]
        [Size(20)]
        [XafDisplayName("Contact urgence (tél.)")]
        public string ContactUrgenceTel
        {
            get => contactUrgenceTel;
            set => SetPropertyValue(nameof(ContactUrgenceTel), ref contactUrgenceTel, value?.Trim());
        }

        [VisibleInListView(false)]
        [Size(50)]
        [XafDisplayName("Contact urgence (lien)")]
        [ToolTip("Ex: Épouse, Père, Mère, Frère...")]
        public string ContactUrgenceLien
        {
            get => contactUrgenceLien;
            set => SetPropertyValue(nameof(ContactUrgenceLien), ref contactUrgenceLien, value?.Trim());
        }

        // ── Informations complémentaires ──────────────────────
        [VisibleInListView(false)]
        [Size(5)]
        [XafDisplayName("Groupe sanguin")]
        [ToolTip("Ex: A+, O-, B+...")]
        public string GroupeSanguin
        {
            get => groupeSanguin;
            set => SetPropertyValue(nameof(GroupeSanguin), ref groupeSanguin,
                value?.Trim()?.ToUpperInvariant());
        }

        [VisibleInListView(false)]
        [Size(30)]
        [XafDisplayName("Permis de conduire")]
        [ToolTip("Ex: B, D, BCDE...")]
        public string PermisConduire
        {
            get => permisConduire;
            set => SetPropertyValue(nameof(PermisConduire), ref permisConduire,
                value?.Trim()?.ToUpperInvariant());
        }

        // ── Rémunération ──────────────────────────────────────
        [NonPersistent]
        public int Anciennete => AncienneteHelper.NombreAnnee(DateEmbauche, DateTime.Today);

        // V1.6.1 — KPI affichés en bandeau d'en-tête de la fiche
        [NonPersistent]
        [XafDisplayName("Ancienneté")]
        public string AncienneteAffichage =>
            DateEmbauche == default ? "—"
            : $"{Anciennete} an{(Anciennete > 1 ? "s" : "")}";

        [NonPersistent]
        [XafDisplayName("Salaire base")]
        public string SalaireBaseAffichage =>
            SalaireBase <= 0m ? "—" : $"{SalaireBase:N0} FCFA";

        [NonPersistent]
        [XafDisplayName("Échelon")]
        public string EchelonAffichage => Echelon?.Code ?? "—";

        [NonPersistent]
        [XafDisplayName("Site")]
        public string SiteAffichage => Site?.Code ?? "—";

        // V1.6.1 — Statut visuel (badge coloré dans le bandeau)
        [NonPersistent]
        [XafDisplayName("Statut")]
        public string StatutAffichage
        {
            get
            {
                if (!IsActif) return "Inactif";
                if (DateConfirmation.HasValue && DateConfirmation.Value > DateTime.Today)
                    return "En période d'essai";
                return "Actif";
            }
        }

        [VisibleInListView(false)]
        [Appearance("Salaire_ReadOnly_When_EchelonSet",
            Criteria = "Not IsNull(Echelon)", Enabled = false, TargetItems = nameof(SalaireBase))]
        [Appearance("Indem_ReadOnly_When_EchelonSet",
            Criteria = "Not IsNull(Echelon)", Enabled = false, TargetItems = nameof(IndemniteLogement))]
        [DbType("decimal(18,0)")]
        public decimal SalaireBase
        {
            get => salaireBase;
            set => SetPropertyValue(nameof(SalaireBase), ref salaireBase, value);
        }

        [VisibleInListView(false)]
        [DbType("decimal(18,0)")]
        public decimal IndemniteLogement
        {
            get => indemniteLogement;
            set => SetPropertyValue(nameof(IndemniteLogement), ref indemniteLogement, value);
        }

        [VisibleInListView(false)]
        public int Base30Jour
        {
            get => base30Jour;
            set => SetPropertyValue(nameof(Base30Jour), ref base30Jour, value);
        }

        [VisibleInListView(false)]
        [Category("Rémunération"), XafDisplayName("Sursalaire")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal Sursalaire
        {
            get => sursalaire;
            set => SetPropertyValue(nameof(Sursalaire), ref sursalaire, value);
        }
        decimal sursalaire;

        [VisibleInListView(false)]
        [Category("Rémunération"), XafDisplayName("Prime de transport")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal PrimeTransport
        {
            get => primeTransport;
            set => SetPropertyValue(nameof(PrimeTransport), ref primeTransport, value);
        }
        decimal primeTransport;

        [VisibleInListView(false)]
        [Category("Rémunération"), XafDisplayName("Avantage en nature - Véhicule")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal AvantageVehicule
        {
            get => avantageVehicule;
            set => SetPropertyValue(nameof(AvantageVehicule), ref avantageVehicule, value);
        }
        decimal avantageVehicule;

        [VisibleInListView(false)]
        [ModelDefault("Caption", "Possède un véhicule")]
        [ModelDefault("ImmediatePostData", "True")]
        public bool PossedeVehicule
        {
            get => _possedeVehicule;
            set => SetPropertyValue(nameof(PossedeVehicule), ref _possedeVehicule, value);
        }
        private bool _possedeVehicule;

        [RuleFromBoolProperty("Salarie_NoTransportWhenVehicle", DefaultContexts.Save,
            CustomMessageTemplate = "Prime de transport interdite si un véhicule est attribué.")]
        public bool IsNoTransportWhenVehicle => !PossedeVehicule || PrimeTransport == 0m;

        // ── Notes ─────────────────────────────────────────────
        private string notes;
        [VisibleInListView(false)]
        [Size(4096)]
        public string Notes
        {
            get => notes;
            set => SetPropertyValue(nameof(Notes), ref notes, value);
        }

        // ── Audit ─────────────────────────────────────────────
        [VisibleInListView(false)]
        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        public DateTime DateCreation
        {
            get => dateCreation;
            set => SetPropertyValue(nameof(DateCreation), ref dateCreation, value);
        }

        [VisibleInListView(false)]
        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        [Size(50)]
        public string CreePar
        {
            get => creePar;
            set => SetPropertyValue(nameof(CreePar), ref creePar, value);
        }

        // ── Sexe ──────────────────────────────────────────────
        [XafDisplayName("Sexe")]
        public Sexe Sexe
        {
            get => sexe;
            set
            {
                var old = sexe;
                SetPropertyValue(nameof(Sexe), ref sexe, value);
                if (old != value) SyncCiviliteFromSexe();
            }
        }
        Sexe sexe;

        // ── Echelon / Catégorie / Convention ──────────────────
        Echelons echelon;
        [ImmediatePostData]
        [Association("Echelons-Salaries")]
        public Echelons Echelon
        {
            get => echelon;
            set => SetPropertyValue(nameof(Echelon), ref echelon, value);
        }

        [Association("Categories-Salaries")]
        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        [Appearance("Categories_AlwaysReadOnly", Criteria = "True",
            Enabled = false, TargetItems = nameof(Categories))]
        public Categories Categories
        {
            get => categories;
            set => SetPropertyValue(nameof(Categories), ref categories, value);
        }

        [VisibleInListView(false)]
        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        [Appearance("Convention_AlwaysReadOnly", Criteria = "True",
            Enabled = false, TargetItems = nameof(Convention))]
        [Association("Convention-Salaries")]
        public Convention Convention
        {
            get => convention;
            set => SetPropertyValue(nameof(Convention), ref convention, value);
        }

        // ── Département / Fonction ────────────────────────────
        [XafDisplayName("Département")]
        [Association("Departement-Salaries")]
        [DataSourceCriteria("Actif = True")]
        public Departement Departement
        {
            get => departement;
            set => SetPropertyValue(nameof(Departement), ref departement, value);
        }
        private Departement departement;

        [XafDisplayName("Fonction")]
        [Association("Fonction-Salaries")]
        [DataSourceCriteria("Actif = True")]
        public Fonction Fonction
        {
            get => fonction;
            set => SetPropertyValue(nameof(Fonction), ref fonction, value);
        }
        private Fonction fonction;

        // ── Site (lieu de travail : Siège, Dépôt CDB, Dépôt Hann...) ─
        [XafDisplayName("Site")]
        [Association("Site-Salaries")]
        [DataSourceCriteria("Actif = True")]
        [ToolTip("Lieu de travail (Siège, Dépôt, etc.). Pour les intérimaires : voir Station de service.")]
        public Site Site
        {
            get => site;
            set => SetPropertyValue(nameof(Site), ref site, value);
        }
        private Site site;

        // ── Manager hiérarchique (N+1) ────────────────────────
        [VisibleInListView(false)]
        [XafDisplayName("Responsable hiérarchique (N+1)")]
        [DataSourceCriteria("IsActif = true")]
        public Salarie Manager
        {
            get => manager;
            set => SetPropertyValue(nameof(Manager), ref manager, value);
        }
        Salarie manager;

        // ── Sécurité bulletins ────────────────────────────────
        [VisibleInListView(false)]
        [Size(2048)]
        public string PayslipKeyEnc { get; set; }
        [VisibleInListView(false)]
        public DateTime? PayslipKeyAssignedOn { get; set; }

        // ── Collections ───────────────────────────────────────
        [Association("Salarie-Modele"), Aggregated]
        public XPCollection<BulletinModele> Modeles
            => GetCollection<BulletinModele>(nameof(Modeles));

        [Association("Salarie-Bulletins"), Aggregated]
        public XPCollection<Bulletin> Bulletins
            => GetCollection<Bulletin>(nameof(Bulletins));

        [Association("Salarie-Conjoints"), Aggregated]
        public XPCollection<Conjoint> Conjoints
            => GetCollection<Conjoint>(nameof(Conjoints));

        // V1.6 — Liste nominative des enfants (en complément du compteur NombreEnfant)
        [Association("Salarie-Enfants"), Aggregated]
        [XafDisplayName("Enfants")]
        public XPCollection<Enfant> Enfants
            => GetCollection<Enfant>(nameof(Enfants));

        [Association("Salarie-Prets")]
        public XPCollection<Pret> Prets
            => GetCollection<Pret>(nameof(Prets));

        [Association("Salarie-Conges")]
        public XPCollection<CongeDemande> Conges
            => GetCollection<CongeDemande>(nameof(Conges));

        [Association("Salarie-Simulations")]
        public XPCollection<SimulationSursalaire> Simulations
            => GetCollection<SimulationSursalaire>(nameof(Simulations));

        [Association("Salarie-DossierSalarie"), Aggregated]
        public XPCollection<DossierSalarie> DossierSalarie
            => GetCollection<DossierSalarie>(nameof(DossierSalarie));

        [Association("Salarie-Entretiens"), Aggregated]
        [XafDisplayName("Entretiens annuels")]
        public XPCollection<EntretienAnnuel> Entretiens
            => GetCollection<EntretienAnnuel>(nameof(Entretiens));

        [Association("Evaluateur-Entretiens")]
        [XafDisplayName("Entretiens menés (évaluateur)")]
        public XPCollection<EntretienAnnuel> EntretiensMenes
            => GetCollection<EntretienAnnuel>(nameof(EntretiensMenes));

        [Association("Salarie-DemandesAttestation"), Aggregated]
        [XafDisplayName("Demandes d'attestation")]
        public XPCollection<DemandeAttestation> DemandesAttestation
            => GetCollection<DemandeAttestation>(nameof(DemandesAttestation));

        [Association("Salarie-Notifications"), Aggregated]
        [XafDisplayName("Notifications")]
        public XPCollection<NotificationSalarie> Notifications
            => GetCollection<NotificationSalarie>(nameof(Notifications));

        [Association("Salarie-Deplacements"), Aggregated]
        [XafDisplayName("Demandes de déplacement")]
        public XPCollection<DemandeDeplacement> Deplacements
            => GetCollection<DemandeDeplacement>(nameof(Deplacements));

        [Association("Salarie-Avancements")]  // Retiré [Aggregated] — il empêchait XAF d'afficher Salarie dans la DetailView du child
        [XafDisplayName("Avancements / Promotions")]
        public XPCollection<DemandeAvancement> Avancements
            => GetCollection<DemandeAvancement>(nameof(Avancements));

        [Association("Salarie-HistoriquePostes"), Aggregated]
        [XafDisplayName("Historique de poste")]
        public XPCollection<HistoriquePoste> HistoriquePostes
            => GetCollection<HistoriquePoste>(nameof(HistoriquePostes));

        [Association("DG-Avancements")]
        [Browsable(false)]
        public XPCollection<DemandeAvancement> AvancementsApprouves
            => GetCollection<DemandeAvancement>(nameof(AvancementsApprouves));

        [Association("Salarie-InscriptionsFormation")]
        [XafDisplayName("Inscriptions formation")]
        public XPCollection<InscriptionFormation> InscriptionsFormation
            => GetCollection<InscriptionFormation>(nameof(InscriptionsFormation));

        [Association("Salarie-SuivisFormation")]
        [XafDisplayName("Historique formation")]
        public XPCollection<SuiviFormation> SuivisFormation
            => GetCollection<SuiviFormation>(nameof(SuivisFormation));

        [Association("Salarie-PlansApprouves")]
        [XafDisplayName("Plans approuvés")]
        [Browsable(false)]
        public XPCollection<PlanFormation> PlansApprouves
            => GetCollection<PlanFormation>(nameof(PlansApprouves));

        [Association("Salarie-SoldesConge")]
        [XafDisplayName("Soldes de congés")]
        public XPCollection<SoldeConge> SoldesConge
            => GetCollection<SoldeConge>(nameof(SoldesConge));

        [Association("Salarie-EvenementsConge")]
        [Browsable(false)]
        public XPCollection<EvenementConge> EvenementsConge
            => GetCollection<EvenementConge>(nameof(EvenementsConge));

        [Association("Salarie-Contrats"), Aggregated]
        [XafDisplayName("Contrats de travail")]
        public XPCollection<ContratSalarie> Contrats
            => GetCollection<ContratSalarie>(nameof(Contrats));

        // ── Comptes bancaires ─────────────────────────────────
        [Association("Salarie-ComptesBancaires"), Aggregated]
        [XafDisplayName("Comptes bancaires")]
        public XPCollection<CompteBancaireSalarie> ComptesBancaires
            => GetCollection<CompteBancaireSalarie>(nameof(ComptesBancaires));

        // ── Compteurs ─────────────────────────────────────────
        [NonPersistent, XafDisplayName("Conjoints actuels")]
        public int NbConjointsActuels
        {
            get
            {
                try { return Session == null ? 0 : Conjoints.Count(c => c.DateFinUnion == null); }
                catch (ObjectDisposedException) { return 0; }
            }
        }

        [NonPersistent, XafDisplayName("Conjoints inactifs à charge")]
        public int NbConjointsInactifsACharge
        {
            get
            {
                if (Session?.IsObjectsLoading == true || IsLoading || Session == null)
                    return _nbConjointsCache;
                try
                {
                    var crit = CriteriaOperator.Parse(
                        "Salarie = ? AND IsNull(DateFinUnion) AND Statut = ? AND ACharge = true",
                        this, Domain.DomainEnums.StatutConjoint.Inactif);
                    var res = Session.Evaluate(typeof(Conjoint),
                        CriteriaOperator.Parse("Count()"), crit);
                    var n = (res is int i) ? i : (res is long l ? (int)l : 0);
                    _nbConjointsCache = n;
                    return n;
                }
                catch (ObjectDisposedException) { return _nbConjointsCache; }
            }
        }
        private int _nbConjointsCache = 0;

        [NonPersistent, XafDisplayName("Notifications non lues")]
        public int NbNotificationsNonLues
        {
            get
            {
                try { return Session == null ? 0 : Notifications.Count(n => n.Statut == Domain.DomainEnums.NotificationStatut.NonLue); }
                catch (ObjectDisposedException) { return 0; }
            }
        }

        // ── Calculs fiscaux ───────────────────────────────────
        [NonPersistent, XafDisplayName("Nombre de parts fiscales")]
        [ModelDefault("DisplayFormat", "n1")]
        [DbType("decimal(18,2)")]
        public decimal NombrePartsFiscales
        {
            get
            {
                var enf = (decimal)Math.Max(0, NombreEnfant);
                var baseParts = (SatutMarital == SituationMaritale.Marie)
                    ? (NbConjointsInactifsACharge >= 1 ? 2m : 1.5m)
                    : 1m;
                return Math.Min(5m, baseParts + (enf / 2m));
            }
        }

        [NonPersistent, XafDisplayName("TRIMF (parts)")]
        public int TrimfParts => Math.Min(5, 1 + NbConjointsInactifsACharge);

        // ── Virements bancaires ───────────────────────────────
        /// <summary>
        /// Calcule les virements pour un net donné.
        /// Ordre : MontantFixe → Pourcentage → Reliquat.
        /// </summary>
        public List<(CompteBancaireSalarie Compte, decimal Montant)>
            CalculerVirements(decimal netAPayer)
        {
            var result = new List<(CompteBancaireSalarie Compte, decimal Montant)>();

            var actifs = ComptesBancaires
                .OfType<CompteBancaireSalarie>()
                .Where(c => c.Actif)
                .OrderBy(c => (int)(c.Mode ?? ModeVirement.MontantFixe))
                .ToList();

            decimal alloue = 0m;
            foreach (CompteBancaireSalarie compte in actifs)
            {
                decimal montant = compte.CalculerMontant(netAPayer, alloue);
                if (montant > 0m)
                {
                    result.Add((compte, montant));
                    alloue += montant;
                }
            }
            return result;
        }

        /// <summary>
        /// Valide la cohérence de la répartition bancaire.
        /// Retourne null si OK, sinon le message d'erreur.
        /// </summary>
        public string ValiderRepartitionBancaire(decimal netAPayer)
        {
            var comptes = ComptesBancaires
                .OfType<CompteBancaireSalarie>()
                .Where(c => c.Actif)
                .ToList();

            if (!comptes.Any()) return null;

            // Un seul reliquat autorisé
            int nbReliquats = comptes.Count(c => c.Mode == ModeVirement.Reliquat);
            if (nbReliquats > 1)
                return "Un seul compte reliquat est autorisé par salarié.";

            // Total pourcentages ≤ 100
            decimal totalPct = comptes
                .Where(c => c.Mode == ModeVirement.Pourcentage)
                .Sum(c => c.Valeur);
            if (totalPct > 100)
                return $"La somme des pourcentages ({totalPct:N1}%) dépasse 100%.";

            // Total montants fixes + % ≤ net
            decimal totalFixe = comptes
                .Where(c => c.Mode == ModeVirement.MontantFixe)
                .Sum(c => c.Valeur);
            decimal totalPctMontant = netAPayer > 0 ? netAPayer * totalPct / 100m : 0m;

            if (totalFixe + totalPctMontant > netAPayer)
                return $"La somme des virements ({totalFixe + totalPctMontant:N0} FCFA) "
                     + $"dépasse le net à payer ({netAPayer:N0} FCFA).";

            return null;
        }

        // ── Hiérarchie ────────────────────────────────────────
        public List<Salarie> GetManagerChain(int maxLevels = 2)
        {
            var chain = new List<Salarie>();
            var current = Manager;
            int level = 0;
            while (current != null && level < maxLevels)
            {
                chain.Add(current);
                current = current.Manager;
                level++;
            }
            return chain;
        }

        public bool EstManagerDe(Salarie subordonne, int maxLevels = 2)
        {
            if (subordonne == null) return false;
            return subordonne.GetManagerChain(maxLevels).Any(m => m.Oid == Oid);
        }

        // ── Overrides ─────────────────────────────────────────
        protected override void OnDeleting()
        {
            // ── Bulletins de paie ─────────────────────────────────────────
            if (Bulletins != null && Bulletins.Any())
                throw new UserFriendlyException(
                    $"Impossible de supprimer {FullName} : "
                    + $"{Bulletins.Count} bulletin(s) de paie existent. "
                    + "Utilisez 'Désactiver' plutôt que de supprimer le salarié.");

            // ── Prêts ─────────────────────────────────────────────────────
            if (Prets != null && Prets.Any())
                throw new UserFriendlyException(
                    $"Impossible de supprimer {FullName} : "
                    + $"{Prets.Count} prêt(s) existent pour ce salarié.");

            // ── Contrats de travail ───────────────────────────────────────
            if (Contrats != null && Contrats.Any())
                throw new UserFriendlyException(
                    $"Impossible de supprimer {FullName} : "
                    + $"{Contrats.Count} contrat(s) de travail existent.");

            // ── Comptes bancaires ─────────────────────────────────────────
            if (ComptesBancaires != null && ComptesBancaires.Any())
                throw new UserFriendlyException(
                    $"Impossible de supprimer {FullName} : "
                    + $"{ComptesBancaires.Count} compte(s) bancaire(s) existent. "
                    + "Supprimez-les d'abord ou désactivez le salarié.");

            // ── Dossier salarié ───────────────────────────────────────────
            if (DossierSalarie != null && DossierSalarie.Any())
                throw new UserFriendlyException(
                    $"Impossible de supprimer {FullName} : "
                    + "Un dossier RH existe pour ce salarié.");

            // ── Avancements ───────────────────────────────────────────────
            if (Avancements != null && Avancements.Any())
                throw new UserFriendlyException(
                    $"Impossible de supprimer {FullName} : "
                    + $"{Avancements.Count} avancement(s) / promotion(s) existent.");

            // ── Désactivation avant suppression physique ─────────────────
            // Même si le salarié sera supprimé physiquement (DeferredDeletion=false),
            // on trace la désactivation au cas où un audit ou log l'intercepte.
            IsActif = false;
            if (DateSortie == DateTime.MinValue || DateSortie.Year < 2)
                DateSortie = DateTime.Today;

            base.OnDeleting();
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            _nbConjointsCache = 0;
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted && !string.IsNullOrWhiteSpace(Email))
            {
                Email = Email.Trim().ToLowerInvariant();
                if (!System.Text.RegularExpressions.Regex.IsMatch(Email,
                    @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    throw new UserFriendlyException(
                        $"Format d'email invalide : « {Email} ». Exemple : prenom.nom@domaine.sn");
            }
        }

        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnChanged(propertyName, oldValue, newValue);
            if (IsDeleted) return;

            if (propertyName == nameof(Echelon))
                AlignerDepuisEchelon();

            if (!IsLoading && !IsSaving && Echelon != null &&
                (propertyName == nameof(Categories) || propertyName == nameof(Convention)))
                AlignerDepuisEchelon();

            if (propertyName == nameof(PossedeVehicule))
            {
                if (Equals(newValue, true))
                {
                    if (PrimeTransport != 0m) PrimeTransport = 0m;
                    var defVeh = ResolveAvantageVehiculeDefaut(Session);
                    if (AvantageVehicule != defVeh) AvantageVehicule = defVeh;
                }
                else
                {
                    if (AvantageVehicule != 0m) AvantageVehicule = 0m;
                }
            }
            else if (propertyName == nameof(AvantageVehicule))
            {
                if (Convert.ToDecimal(newValue) > 0m && PrimeTransport != 0m) PrimeTransport = 0m;
                if (Convert.ToDecimal(newValue) > 0m && !PossedeVehicule) PossedeVehicule = true;
            }
            else if (propertyName == nameof(PrimeTransport))
            {
                if (Convert.ToDecimal(newValue) > 0m && AvantageVehicule != 0m) AvantageVehicule = 0m;
                if (Convert.ToDecimal(newValue) > 0m && PossedeVehicule) PossedeVehicule = false;
            }
        }

        // ── Helpers privés ────────────────────────────────────
        private void AlignerDepuisEchelon()
        {
            if (Echelon == null) return;
            if (!Equals(Categories, Echelon.Categories)) Categories = Echelon.Categories;
            var conv = Echelon.Categories?.Convention;
            if (conv != null && !Equals(Convention, conv)) Convention = conv;
            SalaireBase = Echelon.SalaireBase;
            IndemniteLogement = Echelon.IdemniteLogement;
        }

        bool _syncing;
        void SyncCiviliteFromSexe()
        {
            if (_syncing) return;
            _syncing = true;
            try
            {
                switch (Sexe)
                {
                    case Sexe.Masculin:
                        if (Civilite != Civilite.Monsieur) Civilite = Civilite.Monsieur;
                        break;
                    case Sexe.Feminin:
                        if (Civilite != Civilite.Madame && Civilite != Civilite.Mademoiselle)
                            Civilite = Civilite.Madame;
                        break;
                }
            }
            finally { _syncing = false; }
        }

        void SyncSexeFromCivilite()
        {
            if (_syncing) return;
            _syncing = true;
            try
            {
                switch (Civilite)
                {
                    case Civilite.Monsieur:
                        if (Sexe != Sexe.Masculin) Sexe = Sexe.Masculin;
                        break;
                    case Civilite.Madame:
                    case Civilite.Mademoiselle:
                        if (Sexe != Sexe.Feminin) Sexe = Sexe.Feminin;
                        break;
                }
            }
            finally { _syncing = false; }
        }

        private static decimal ResolveAvantageVehiculeDefaut(Session session)
        {
            var pp = ParametresPaie.TryGet(session);
            return pp?.ModeleAuto_Defaut_AvantageVehicule ?? 0m;
        }

        // ── Modèle bulletin ───────────────────────────────────
        [Action(
            Caption = "Régénérer modèle",
            ImageName = "BO_Resume",
            AutoCommit = true,
            ConfirmationMessage = "Recréer/compléter le modèle de bulletin pour ce salarié ?")]
        public void RegenererModele()
        {
            if (Session.IsNewObject(this))
                throw new UserFriendlyException(
                    "Enregistrez d'abord le salarié, puis relancez l'action.");

            string[] codes = {
                PaieConsts.Rubriques.SB,
                PaieConsts.Rubriques.SURSAL,
                PaieConsts.Rubriques.TRANS,
                PaieConsts.Rubriques.AV_NAT_VEH,
                PaieConsts.Rubriques.LOGT
            };
            var missing = codes.Where(c =>
                new XPQuery<Rubrique>(Session)
                    .FirstOrDefault(r => r.Actif && r.Code == c) == null).ToList();
            if (missing.Any())
                throw new UserFriendlyException(
                    "Rubriques manquantes/inactives : " + string.Join(", ", missing) +
                    ". Veuillez les créer/activer puis relancer l'action.");

            EnsureBulletinModeleParDefaut();
        }

        public void EnsureBulletinModeleParDefaut()
        {
            var md = Modeles.FirstOrDefault(m => m.Actif)
                     ?? new BulletinModele(Session) { Salarie = this, Actif = true };

            Rubrique FindRub(string code) =>
                new XPQuery<Rubrique>(Session).FirstOrDefault(r => r.Actif && r.Code == code);

            void Upsert(string code, int ordre, bool inclure)
            {
                var rub = FindRub(code);
                if (rub == null) return;
                var l = md.Lignes.FirstOrDefault(x => x.ReferenceDefaut == code)
                        ?? new BulletinModeleLigne(Session) { Modele = md, ReferenceDefaut = code };
                l.Rubrique = rub;
                l.Ordre = ordre;
                l.InclureParDefaut = inclure;
            }

            Upsert(PaieConsts.Rubriques.SB, 1, true);
            Upsert(PaieConsts.Rubriques.SURSAL, 20, true);
            Upsert(PaieConsts.Rubriques.LOGT, 60, true);
            Upsert(PaieConsts.Rubriques.TRANS, 83, true);
            Upsert(PaieConsts.Rubriques.AV_NAT_VEH, 180, true);
        }
    }

    public enum Civilite { Monsieur = 0, Madame = 1, Mademoiselle = 2 }
    public enum Sexe { Masculin = 0, [XafDisplayName("Féminin")] Feminin = 1 }
}
