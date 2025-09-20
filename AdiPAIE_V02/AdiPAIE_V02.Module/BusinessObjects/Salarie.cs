using AdiPAIE_V02.Module.Domain;
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
    // 🔒 Bloque l’enregistrement si un conjoint “en cours” existe et que l’état n’est pas “Marié”
    [RuleCriteria(
        "Salarie_MustBeMarried_IfAnyCurrentSpouse",
        DefaultContexts.Save,
        "Conjoints[IsNull(DateFinUnion)].Count() = 0 OR SatutMarital = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SituationMaritale,Marie#",
        CustomMessageTemplate = "Impossible de quitter l'état 'Marié' tant qu'un conjoint est en cours."
    )]
    // 🎛️ Grise le champ Situation Maritale si un conjoint “en cours” existe
    [Appearance(
        "Disable_SatutMarital_WhenCurrentSpouseExists",
        Criteria = "Conjoints[IsNull(DateFinUnion)].Count() > 0",
        Enabled = false,
        TargetItems = nameof(SatutMarital)
    )]
    // 🎛️ Grise la collection Conjoints tant que l’état n’est pas “Marié”
    [Appearance(
        "Disable_Conjoints_When_NotMarried",
        Criteria = "SatutMarital <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SituationMaritale,Marie#",
        Enabled = false,
        TargetItems = nameof(Conjoints)
    )]

    // --- Validation : cohérence avec l’échelon choisi ---
    [RuleCriteria("Salarie_CatMustMatchEchelon",
        DefaultContexts.Save,
        "IsNull(Echelon) OR Categories = Echelon.Categories",
        CustomMessageTemplate = "La catégorie du salarié doit correspondre à celle de l’échelon.")]
    [RuleCriteria("Salarie_ConvMustMatchEchelon",
        DefaultContexts.Save,
        "IsNull(Echelon) OR Convention = Echelon.Categories.Convention",
        CustomMessageTemplate = "La convention du salarié doit correspondre à celle de l’échelon.")]
    [Appearance(
    "DisablePrimeTransportWhenHasVehicle",
    TargetItems = nameof(PrimeTransport),
    Criteria    = nameof(PossedeVehicule) + " = True",
    Enabled     = false)]
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

        // --- Identité ---
        [Size(20)]
        [RuleRequiredField]
        // [Indexed(Unique = true, Name = "IX_Salarie_Matricule")]
        [RuleUniqueValue(DefaultContexts.Save, CustomMessageTemplate = "Cet matricul est déjà utilisé par un autre salarié.")]
        public string Matricule { get => matricule; set => SetPropertyValue(nameof(Matricule), ref matricule, value); }

        public Civilite Civilite { get => civilite; set => SetPropertyValue(nameof(Civilite), ref civilite, value); }

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

        // --- Rémunération / rattachements ---
        [NonPersistent]
        public int Anciennete => AncienneteHelper.NombreAnnee(DateEmbauche, DateTime.Today);

        [Appearance("Salaire_ReadOnly_When_EchelonSet",
    Criteria = "Not IsNull(Echelon)", Enabled = false, TargetItems = nameof(SalaireBase))]
        [Appearance("Indem_ReadOnly_When_EchelonSet",
    Criteria = "Not IsNull(Echelon)", Enabled = false, TargetItems = nameof(IndemniteLogement))]

        [DbType("decimal(18,0)")]
        public decimal SalaireBase { get => salaireBase; set => SetPropertyValue(nameof(SalaireBase), ref salaireBase, value); }

        [DbType("decimal(18,0)")]
        public decimal IndemniteLogement { get => indemniteLogement; set => SetPropertyValue(nameof(IndemniteLogement), ref indemniteLogement, value); }

        [Size(50)]
        public string CaisseSecurite { get => caisseSecurite; set => SetPropertyValue(nameof(CaisseSecurite), ref caisseSecurite, value); }

        [Size(50)]
        public string NumeroIPRESS { get => numeroIPRESS; set => SetPropertyValue(nameof(NumeroIPRESS), ref numeroIPRESS, value); }

        [Size(50)]
        public string NumeroCNI { get => numeroCNI; set => SetPropertyValue(nameof(NumeroCNI), ref numeroCNI, value); }

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

        // --- Echelon + alignement Catégorie/Convention ---
        Echelons echelon;
        [ImmediatePostData]
        [Association("Echelons-Salaries")]
        public Echelons Echelon
        {
            get => echelon;
            set => SetPropertyValue(nameof(Echelon), ref echelon, value);
        }

        //[Appearance("Categories_ReadOnly_When_EchelonSet", Criteria = "Not IsNull(Echelon)", Enabled = false, TargetItems = nameof(Categories))]

        // --- Catégorie : toujours lecture seule ---
        [Association("Categories-Salaries")]
        [ModelDefault("AllowEdit", "False")] // DetailView
        [System.ComponentModel.ReadOnly(true)] // ListView (colonnes)
        [Appearance("Categories_AlwaysReadOnly", Criteria = "True", Enabled = false, TargetItems = nameof(Categories))]

        public Categories Categories { get => categories; set => SetPropertyValue(nameof(Categories), ref categories, value); }

        //  [Appearance("Convention_ReadOnly_When_EchelonSet", Criteria = "Not IsNull(Echelon)", Enabled = false, TargetItems = nameof(Convention))]

        // --- Convention : toujours lecture seule --- 
        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        [Appearance("Convention_AlwaysReadOnly", Criteria = "True", Enabled = false, TargetItems = nameof(Convention))]
        [Association("Convention-Salaries")]
        public Convention Convention { get => convention; set => SetPropertyValue(nameof(Convention), ref convention, value); }

        // --- Modèle & bulletins ---
        [Association("Salarie-Modele"), Aggregated]
        public XPCollection<BulletinModele> Modeles => GetCollection<BulletinModele>(nameof(Modeles));

        [Association("Salarie-Bulletins"), Aggregated]
        public XPCollection<Bulletin> Bulletins => GetCollection<Bulletin>(nameof(Bulletins));

        // --- Conjoints (agrégat côté parent uniquement) ---
        [Association("Salarie-Conjoints"), Aggregated]
        public XPCollection<Conjoint> Conjoints => GetCollection<Conjoint>(nameof(Conjoints));


        [Association("Salarie-Prets")]
        public XPCollection<Pret> Prets => GetCollection<Pret>(nameof(Prets));


        // Compteurs utiles
        [NonPersistent, XafDisplayName("Conjoints actuels")]
        public int NbConjointsActuels => Conjoints.Count(c => c.DateFinUnion == null);

        [NonPersistent]
        [XafDisplayName("Conjoints inactifs à charge")]
        public int NbConjointsInactifsACharge
        {
            get
            {
                // 1) ne JAMAIS lancer une requête pendant le chargement
                if (Session?.IsObjectsLoading == true || IsLoading)
                    return _nbConjointsCache; // 0 par défaut ou dernière valeur calculée

                // 2) compte côté base, sans traverser la collection Conjoints
                var crit = CriteriaOperator.Parse(
                  "Salarie = ? AND IsNull(DateFinUnion) AND Statut = ? AND ACharge = true",
                  this, Domain.DomainEnums.StatutConjoint.Inactif
                );

                var res = Session.Evaluate(typeof(Conjoint), CriteriaOperator.Parse("Count()"), crit);
                var n = (res is int i) ? i : (res is long l ? (int)l : 0);

                // 3) petit cache simple pour éviter une rafale de requêtes UI
                _nbConjointsCache = n;
                return n;
            }
        }
        private int _nbConjointsCache = 0;

        //ADIENG 11/09/2025 AJOUT AUTOMATISATION MODEL DEBUT
        // … (vos membres existants)

        [Category("Rémunération"), XafDisplayName("Sursalaire")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal Sursalaire
        {
            get => sursalaire;
            set => SetPropertyValue(nameof(Sursalaire), ref sursalaire, value);
        }
        decimal sursalaire;

        [Category("Rémunération"), XafDisplayName("Prime de transport")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal PrimeTransport
        {
            get => primeTransport;
            set => SetPropertyValue(nameof(PrimeTransport), ref primeTransport, value);
        }
        decimal primeTransport;

        [Category("Rémunération"), XafDisplayName("Avantage en nature - Véhicule")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal AvantageVehicule
        {
            get => avantageVehicule;
            set => SetPropertyValue(nameof(AvantageVehicule), ref avantageVehicule, value);
        }
        decimal avantageVehicule ; // valeur par défaut SN actuelle

        private bool _PossedeVehicule;

       [ModelDefault("Caption", "Possède un véhicule")]
        [ModelDefault("ImmediatePostData", "True")] // réaction immédiate en UI
        public bool PossedeVehicule
        {
            get => _PossedeVehicule;
            set => SetPropertyValue(nameof(PossedeVehicule), ref _PossedeVehicule, value);
        }
        // Validation métier : si PossedeVehicule = true → PrimeTransport doit être 0
        [RuleFromBoolProperty("Salarie_NoTransportWhenVehicle", DefaultContexts.Save,
            CustomMessageTemplate = "Prime de transport interdite si un véhicule est attribué.")]
        public bool IsNoTransportWhenVehicle =>
            !PossedeVehicule || PrimeTransport == 0m;



        //ADIENG 11/09/2025 AJOUT FIN

        // Optionnel : invalider le cache quand un conjoint change (si tu exposes la collection)
        protected override void OnLoaded()
        {
            base.OnLoaded();
            _nbConjointsCache = 0; // force un recalcul au 1er accès après chargement
        }
        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted && !string.IsNullOrWhiteSpace(Email))
                Email = Email.Trim().ToLowerInvariant();

        }
        // Calculs fiscaux (affichage)
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

        // --- Cohérence automatique depuis l’Echelon (sans auto-modifier marié/conjoints) ---
        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnChanged(propertyName, oldValue, newValue);
            if (IsDeleted) return;

            // 1) Quand l'échelon change : aligner systématiquement
            if (propertyName == nameof(Echelon))
            {
                AlignerDepuisEchelon();
            }

            // 2) Si on touche Catégorie/Convention alors qu’un échelon est présent, réaligner (hors chargement/sauvegarde)
            if (!IsLoading && !IsSaving && Echelon != null &&
                (propertyName == nameof(Categories) || propertyName == nameof(Convention)))
            {
                AlignerDepuisEchelon();
            }

            // --- Exclusivité & synchronisation véhicule / primes ---
            if (propertyName == nameof(PossedeVehicule))
            {
                if (Equals(newValue, true))
                {
                    // Véhicule attribué → Prime transport = 0, avantage = valeur par défaut ParametresPaie
                    if (PrimeTransport != 0m)
                        PrimeTransport = 0m;

                    var defVeh = ResolveAvantageVehiculeDefaut(Session);
                    if (AvantageVehicule != defVeh)
                        AvantageVehicule = defVeh;
                }
                else
                {
                    // Pas de véhicule → avantage en nature = 0 (prime transport libre)
                    if (AvantageVehicule != 0m)
                        AvantageVehicule = 0m;
                }
            }
            else if (propertyName == nameof(AvantageVehicule))
            {
                // Saisie d'un avantage → couper la prime transport
                if (Convert.ToDecimal(newValue) > 0m && PrimeTransport != 0m)
                    PrimeTransport = 0m;

                // Si on met un avantage > 0, on force PossedeVehicule = true
                if (Convert.ToDecimal(newValue) > 0m && !PossedeVehicule)
                    PossedeVehicule = true;
            }
            else if (propertyName == nameof(PrimeTransport))
            {
                // Saisie d'une prime transport → couper l’avantage
                if (Convert.ToDecimal(newValue) > 0m && AvantageVehicule != 0m)
                    AvantageVehicule = 0m;

                // Si prime > 0, on force PossedeVehicule = false (logique exclusive)
                if (Convert.ToDecimal(newValue) > 0m && PossedeVehicule)
                    PossedeVehicule = false;
            }

        }

        private void AlignerDepuisEchelon()
        {
            if (Echelon == null) return;

            // a) Toujours recaler Catégorie/Convention depuis l’échelon
            if (!Equals(Categories, Echelon.Categories))
                Categories = Echelon.Categories;

            var conv = Echelon.Categories?.Convention;
            if (conv != null && !Equals(Convention, conv))
                Convention = conv;

            // b) Toujours mettre à jour les montants depuis l’échelon
            SalaireBase = Echelon.SalaireBase;
            IndemniteLogement = Echelon.IdemniteLogement;
        }
        // Propriété factice pour porter les RuleCriteria (si nécessaire)
        //  public int _ValidationEchelonConsistencyHelper { get; set; }

        [Association("Salarie-Conges")]
        public XPCollection<CongeDemande> Conges => GetCollection<CongeDemande>(nameof(Conges));

        //ADIENG 11/09/2025 AJOUT DEBUT

        // Bouton manuel sur la fiche Salarié
        [Action(
            Caption = "Re-générer le modèle",
            ImageName = "BO_Resume",
            AutoCommit = true,
            ConfirmationMessage = "Recréer/compléter le modèle de bulletin pour ce salarié ?"
        )]
        public void RegenererModele()
        {
            if (Session.IsNewObject(this))
                throw new UserFriendlyException("Enregistrez d'abord le salarié, puis relancez l'action.");

            // Vérifier que les rubriques nécessaires existent/actives
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
                throw new UserFriendlyException("Rubriques manquantes/inaffectives : " + string.Join(", ", missing) +
                    ". Veuillez les créer/activer puis relancer l’action.");

            EnsureBulletinModeleParDefaut(); // crée/complète de façon idempotente
        }


        // Helper : crée le modèle de bulletin (si absent)
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
            Upsert(PaieConsts.Rubriques.SB, 1, true);  // Salaire de base
            Upsert(PaieConsts.Rubriques.SURSAL, 20, true);  // Sursalaire
            Upsert(PaieConsts.Rubriques.LOGT, 60, true);  // (si tu utilises LOGT)
            Upsert(PaieConsts.Rubriques.TRANS, 83, true);  // Prime de transport  
            Upsert(PaieConsts.Rubriques.AV_NAT_VEH, 180, true);  // Avantage véhicule
        }

        private static decimal ResolveAvantageVehiculeDefaut(Session session)
        {
            var pp = ParametresPaie.TryGet(session);
            return pp?.ModeleAuto_Defaut_AvantageVehicule ?? 0m;
        }


        //ADIENG 11/09/2025 AJOUT FIN

    }

    public enum Civilite { Monsieur, Madame, Mademoiselle }
    public enum Sexe { Masculin, [XafDisplayName("Féminin")] Feminin }

}
