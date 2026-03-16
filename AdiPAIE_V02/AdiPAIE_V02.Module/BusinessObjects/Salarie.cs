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
using System.ComponentModel;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    //[DefaultClassOptions]
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
    [RuleCriteria("Salarie_Manager_NotSelf",
        DefaultContexts.Save,
        "IsNull(Manager) OR Manager.Oid != Oid",
        CustomMessageTemplate = "Un salarié ne peut pas être son propre responsable.")]
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
        DateTime dateSortie;
        DateTime dateEmbauche;
        int nombreEnfant;
        SituationMaritale satutMarital;
        string nationalite;
        Civilite civilite;
        string matricule;

        // ── Identité ──────────────────────────────────────────
        [Size(20)]
        [RuleRequiredField]
        [RuleUniqueValue(DefaultContexts.Save, CustomMessageTemplate = "Ce matricule est déjà utilisé par un autre salarié.")]
        public string Matricule { get => matricule; set => SetPropertyValue(nameof(Matricule), ref matricule, value); }

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

        [Size(50)]
        public string Nationalite { get => nationalite; set => SetPropertyValue(nameof(Nationalite), ref nationalite, value); }

        [ImmediatePostData]
        public SituationMaritale SatutMarital { get => satutMarital; set => SetPropertyValue(nameof(SatutMarital), ref satutMarital, value); }

        [RuleRange(0, int.MaxValue)]
        [XafDisplayName("Nombre d'enfants")]
        public int NombreEnfant { get => nombreEnfant; set => SetPropertyValue(nameof(NombreEnfant), ref nombreEnfant, value); }

        public DateTime DateEmbauche { get => dateEmbauche; set => SetPropertyValue(nameof(DateEmbauche), ref dateEmbauche, value); }
        public DateTime DateSortie { get => dateSortie; set => SetPropertyValue(nameof(DateSortie), ref dateSortie, value); }

        [XafDisplayName("Active")]
        public bool IsActif { get => isActif; set => SetPropertyValue(nameof(IsActif), ref isActif, value); }

        [Action(Caption = "Activé", ImageName = "BO_Task", TargetObjectsCriteria = "IsActif=false", AutoCommit = true)]
        public void Active() => IsActif = true;
        [Action(Caption = "Désactivé", ImageName = "BO_Task", TargetObjectsCriteria = "IsActif=true", AutoCommit = true)]
        public void Desactive() => IsActif = false;

        // ── Rémunération ──────────────────────────────────────
        [NonPersistent]
        public int Anciennete => AncienneteHelper.NombreAnnee(DateEmbauche, DateTime.Today);

        [Appearance("Salaire_ReadOnly_When_EchelonSet", Criteria = "Not IsNull(Echelon)", Enabled = false, TargetItems = nameof(SalaireBase))]
        [Appearance("Indem_ReadOnly_When_EchelonSet", Criteria = "Not IsNull(Echelon)", Enabled = false, TargetItems = nameof(IndemniteLogement))]
        [DbType("decimal(18,0)")]
        public decimal SalaireBase { get => salaireBase; set => SetPropertyValue(nameof(SalaireBase), ref salaireBase, value); }

        [DbType("decimal(18,0)")]
        public decimal IndemniteLogement { get => indemniteLogement; set => SetPropertyValue(nameof(IndemniteLogement), ref indemniteLogement, value); }

        [Size(50)] public string CaisseSecurite { get => caisseSecurite; set => SetPropertyValue(nameof(CaisseSecurite), ref caisseSecurite, value); }
        [Size(50)] public string NumeroIPRESS { get => numeroIPRESS; set => SetPropertyValue(nameof(NumeroIPRESS), ref numeroIPRESS, value); }
        [Size(50)] public string NumeroCNI { get => numeroCNI; set => SetPropertyValue(nameof(NumeroCNI), ref numeroCNI, value); }

        public int Base30Jour { get => base30Jour; set => SetPropertyValue(nameof(Base30Jour), ref base30Jour, value); }

        private string notes;
        [Size(4096)]
        public string Notes { get => notes; set => SetPropertyValue(nameof(Notes), ref notes, value); }

        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        public DateTime DateCreation { get => dateCreation; set => SetPropertyValue(nameof(DateCreation), ref dateCreation, value); }

        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        [Size(50)]
        public string CreePar { get => creePar; set => SetPropertyValue(nameof(CreePar), ref creePar, value); }

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
        [Appearance("Categories_AlwaysReadOnly", Criteria = "True", Enabled = false, TargetItems = nameof(Categories))]
        public Categories Categories { get => categories; set => SetPropertyValue(nameof(Categories), ref categories, value); }

        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        [Appearance("Convention_AlwaysReadOnly", Criteria = "True", Enabled = false, TargetItems = nameof(Convention))]
        [Association("Convention-Salaries")]
        public Convention Convention { get => convention; set => SetPropertyValue(nameof(Convention), ref convention, value); }

        // ── Collections principales ───────────────────────────
        [Association("Salarie-Modele"), Aggregated]
        public XPCollection<BulletinModele> Modeles => GetCollection<BulletinModele>(nameof(Modeles));

        [Association("Salarie-Bulletins"), Aggregated]
        public XPCollection<Bulletin> Bulletins => GetCollection<Bulletin>(nameof(Bulletins));

        [Association("Salarie-Conjoints"), Aggregated]
        public XPCollection<Conjoint> Conjoints => GetCollection<Conjoint>(nameof(Conjoints));

        [Association("Salarie-Prets")]
        public XPCollection<Pret> Prets => GetCollection<Pret>(nameof(Prets));

        [Association("Salarie-Conges")]
        public XPCollection<CongeDemande> Conges => GetCollection<CongeDemande>(nameof(Conges));

        // ── Compteurs conjoints ───────────────────────────────
        [NonPersistent, XafDisplayName("Conjoints actuels")]
        public int NbConjointsActuels => Conjoints.Count(c => c.DateFinUnion == null);

        [NonPersistent, XafDisplayName("Conjoints inactifs à charge")]
        public int NbConjointsInactifsACharge
        {
            get
            {
                if (Session?.IsObjectsLoading == true || IsLoading)
                    return _nbConjointsCache;
                var crit = CriteriaOperator.Parse(
                    "Salarie = ? AND IsNull(DateFinUnion) AND Statut = ? AND ACharge = true",
                    this, Domain.DomainEnums.StatutConjoint.Inactif);
                var res = Session.Evaluate(typeof(Conjoint), CriteriaOperator.Parse("Count()"), crit);
                var n = (res is int i) ? i : (res is long l ? (int)l : 0);
                _nbConjointsCache = n;
                return n;
            }
        }
        private int _nbConjointsCache = 0;

        // ── Rémunération complémentaire ───────────────────────
        [Category("Rémunération"), XafDisplayName("Sursalaire")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal Sursalaire { get => sursalaire; set => SetPropertyValue(nameof(Sursalaire), ref sursalaire, value); }
        decimal sursalaire;

        [Category("Rémunération"), XafDisplayName("Prime de transport")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal PrimeTransport { get => primeTransport; set => SetPropertyValue(nameof(PrimeTransport), ref primeTransport, value); }
        decimal primeTransport;

        [Category("Rémunération"), XafDisplayName("Avantage en nature - Véhicule")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal AvantageVehicule { get => avantageVehicule; set => SetPropertyValue(nameof(AvantageVehicule), ref avantageVehicule, value); }
        decimal avantageVehicule;

        private bool _PossedeVehicule;
        [ModelDefault("Caption", "Possède un véhicule")]
        [ModelDefault("ImmediatePostData", "True")]
        public bool PossedeVehicule { get => _PossedeVehicule; set => SetPropertyValue(nameof(PossedeVehicule), ref _PossedeVehicule, value); }

        [RuleFromBoolProperty("Salarie_NoTransportWhenVehicle", DefaultContexts.Save,
            CustomMessageTemplate = "Prime de transport interdite si un véhicule est attribué.")]
        public bool IsNoTransportWhenVehicle => !PossedeVehicule || PrimeTransport == 0m;

        // ── Sexe ──────────────────────────────────────────────
        Sexe sexe;
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

        // ── Sécurité bulletins ────────────────────────────────
        [Size(2048)]
        public string PayslipKeyEnc { get; set; }
        public DateTime? PayslipKeyAssignedOn { get; set; }

        // ── Simulations ───────────────────────────────────────
        [Association("Salarie-Simulations")]
        public XPCollection<SimulationSursalaire> Simulations
            => GetCollection<SimulationSursalaire>(nameof(Simulations));

        // ── GRH — Dossier / Entretiens / Attestations / Notifs ─
        [Association("Salarie-DossiersRH"), Aggregated]
        public XPCollection<DossierSalarie> DossiersRH
            => GetCollection<DossierSalarie>(nameof(DossiersRH));

        [Association("Salarie-Entretiens"), Aggregated]
        [XafDisplayName("Entretiens annuels")]
        public XPCollection<EntretienAnnuel> Entretiens => GetCollection<EntretienAnnuel>(nameof(Entretiens));

        [Association("Evaluateur-Entretiens")]
        [XafDisplayName("Entretiens menés (évaluateur)")]
        public XPCollection<EntretienAnnuel> EntretiensMenes => GetCollection<EntretienAnnuel>(nameof(EntretiensMenes));

        [Association("Salarie-DemandesAttestation"), Aggregated]
        [XafDisplayName("Demandes d'attestation")]
        public XPCollection<DemandeAttestation> DemandesAttestation => GetCollection<DemandeAttestation>(nameof(DemandesAttestation));

        [Association("Salarie-Notifications"), Aggregated]
        [XafDisplayName("Notifications")]
        public XPCollection<NotificationSalarie> Notifications => GetCollection<NotificationSalarie>(nameof(Notifications));

        [NonPersistent, XafDisplayName("Notifications non lues")]
        public int NbNotificationsNonLues => Notifications.Count(n =>
            n.Statut == Domain.DomainEnums.NotificationStatut.NonLue);

        // ── Manager hiérarchique (N+1) ────────────────────────
        [XafDisplayName("Responsable hiérarchique (N+1)")]
        [DataSourceCriteria("IsActif = true")]
        public Salarie Manager
        {
            get => manager;
            set => SetPropertyValue(nameof(Manager), ref manager, value);
        }
        Salarie manager;

        public System.Collections.Generic.List<Salarie> GetManagerChain(int maxLevels = 2)
        {
            var chain = new System.Collections.Generic.List<Salarie>();
            var current = this.Manager;
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
            var chain = subordonne.GetManagerChain(maxLevels);
            return chain.Any(m => m.Oid == this.Oid);
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

        // ── Overrides ─────────────────────────────────────────
        protected override void OnLoaded()
        {
            base.OnLoaded();
            _nbConjointsCache = 0;
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted && !string.IsNullOrWhiteSpace(Email))
                Email = Email.Trim().ToLowerInvariant();
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
            Caption = "Re-générer le modèle",
            ImageName = "BO_Resume",
            AutoCommit = true,
            ConfirmationMessage = "Recréer/compléter le modèle de bulletin pour ce salarié ?")]
        public void RegenererModele()
        {
            if (Session.IsNewObject(this))
                throw new UserFriendlyException("Enregistrez d'abord le salarié, puis relancez l'action.");

            string[] codes = {
                PaieConsts.Rubriques.SB,
                PaieConsts.Rubriques.SURSAL,
                PaieConsts.Rubriques.TRANS,
                PaieConsts.Rubriques.AV_NAT_VEH,
                PaieConsts.Rubriques.LOGT
            };
            var missing = codes.Where(c => new XPQuery<Rubrique>(Session)
                .FirstOrDefault(r => r.Actif && r.Code == c) == null).ToList();
            if (missing.Any())
                throw new UserFriendlyException("Rubriques manquantes/inactives : " +
                    string.Join(", ", missing) + ". Veuillez les créer/activer puis relancer l'action.");

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