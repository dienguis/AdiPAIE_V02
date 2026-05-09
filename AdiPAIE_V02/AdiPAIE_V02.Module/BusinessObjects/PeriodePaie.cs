using DevExpress.Data.Filtering;
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

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions, XafDisplayName("Période de paie")]
    [DefaultProperty(nameof(DisplayName))]
    [XafDefaultProperty(nameof(DisplayName))]

    // Unicité par entreprise + année + mois
    [RuleCombinationOfPropertiesIsUnique(
        "PeriodePaie_Company_Annee_Mois_Unique", DefaultContexts.Save, "Company;Annee;Mois",
        CustomMessageTemplate = "La période {Mois:D2}/{Annee} existe déjà pour cette entreprise.")]

    // Styles (optionnels)
    [Appearance("Periode_Cloturee_Style", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PeriodePaieStatut,Cloturee#",
        FontColor = "Gray", FontStyle = DXFontStyle.Italic )]
    [Appearance("Periode_Ouverte_Style", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PeriodePaieStatut,Ouverte#",
        FontColor = "Green", FontStyle = DXFontStyle.Bold )]
    [Appearance("Periode_Brouillon_Style", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PeriodePaieStatut,Brouillon#",
        FontColor = "Blue")]

    // Verrou UI des clés quand ≠ Brouillon
    [Appearance("LockKeysWhenNotBrouillon",
        Criteria = "Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PeriodePaieStatut,Brouillon#",
        TargetItems = "Company;Annee;Mois",
        Enabled = false)]
    // V1.6.2 — Badge statut coloré (gris Brouillon / vert Ouverte / sombre Clôturée)
    [Appearance("PeriodePaie_Statut_Brouillon",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PeriodePaieStatut,Brouillon#",
        BackColor = "Gainsboro", FontColor = "DimGray", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("PeriodePaie_Statut_Ouverte",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PeriodePaieStatut,Ouverte#",
        BackColor = "PaleGreen", FontColor = "DarkGreen", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("PeriodePaie_Statut_Cloturee",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PeriodePaieStatut,Cloturee#",
        BackColor = "DarkGray", FontColor = "White", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    public class PeriodePaie : BaseObject
    {
        public PeriodePaie(Session session) : base(session) { }

        // --- Clé fonctionnelle ---
        [Association("Company-Periodes")]
        [RuleRequiredField]
        public Company Company { get => company; set => SetPropertyValue(nameof(Company), ref company, value); }
        private Company company;

        [RuleRange(2000, 2100)]
        public int Annee { get => annee; set => SetPropertyValue(nameof(Annee), ref annee, value); }
        int annee;

        [RuleRange(1, 12)]
        public int Mois { get => mois; set => SetPropertyValue(nameof(Mois), ref mois, value); }
        int mois;

       //[PersistentAlias("Concat(Company.RaisonSociale, ' - ', ToStr(Mois,'00'), '/', ToStr(Annee))")]
        [PersistentAlias(
    "Concat(Company.RaisonSociale, ' - ', Iif(Mois < 10, '0', ''), ToStr(Mois), '/', ToStr(Annee))"
)]
        public string DisplayName
        {
            get
            {
                try { return (string)EvaluateAlias(nameof(DisplayName)); }
                catch (ObjectDisposedException) { return string.Empty; }
            }
        }

        [Size(16), Indexed(Name = "IX_PeriodePaie_Key")]
        public string Key { get => key; set => SetPropertyValue(nameof(Key), ref key, value?.Trim()); }
        string key;

        [Size(120), Indexed(Name = "IX_PeriodePaie_Libelle")]
        public string Libelle { get => libelle; set => SetPropertyValue(nameof(Libelle), ref libelle, value?.Trim()); }
        string libelle;

        // --- Bornes ---
        public DateTime? DateDebut { get => d1; set => SetPropertyValue(nameof(DateDebut), ref d1, value); }
        DateTime? d1;

        public DateTime? DateFin { get => d2; set => SetPropertyValue(nameof(DateFin), ref d2, value); }
        DateTime? d2;

        // --- Statut & méta ---
        public PeriodePaieStatut Statut { get => statut; set => SetPropertyValue(nameof(Statut), ref statut, value); }
        PeriodePaieStatut statut = PeriodePaieStatut.Brouillon;

        [XafDisplayName("Date d’ouverture")]
        public DateTime? DateOuverture { get => douvr; set => SetPropertyValue(nameof(DateOuverture), ref douvr, value); }
        DateTime? douvr;

        [XafDisplayName("Date de clôture")]
        public DateTime? DateCloture { get => dclot; set => SetPropertyValue(nameof(DateCloture), ref dclot, value); }
        DateTime? dclot;

        [Size(200)]
        public string Note { get => note; set => SetPropertyValue(nameof(Note), ref note, value?.Trim()); }
        string note;

        // --- Dérivés ---
        [NonPersistent] public bool EstCloturee => Statut == PeriodePaieStatut.Cloturee;
        [NonPersistent] public bool EstOuverte => Statut == PeriodePaieStatut.Ouverte;
        public int Ordinal => (Annee * 12) + Mois;

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            if (Company == null)
            {
                var one = new XPQuery<Company>(Session).Take(2).ToList();
                if (one.Count == 1) Company = one[0];
            }
            var today = DateTime.Today;
            if (Annee == 0) Annee = today.Year;
            if (Mois == 0) Mois = today.Month;
            EnsureDates();
            EnsureKeyAndLibelle();
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            EnsureDates();
            EnsureKeyAndLibelle();
            if (DateDebut.HasValue && DateFin.HasValue && DateDebut > DateFin)
                throw new UserFriendlyException("La Date de début doit être ≤ Date de fin.");
        }

        // Verrou métier dur (empêche toute modif des clés hors Brouillon)
        [NonPersistent] private bool _suppressKeyLock;
        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnChanged(propertyName, oldValue, newValue);
            if (_suppressKeyLock || IsLoading || IsSaving) return;

            if (propertyName == nameof(Company) || propertyName == nameof(Annee) || propertyName == nameof(Mois))
            {
                bool isNew = Session.IsNewObject(this);
                if (!isNew && Statut != PeriodePaieStatut.Brouillon)
                {
                    try { _suppressKeyLock = true; SetMemberValue(propertyName, oldValue); }
                    finally { _suppressKeyLock = false; }
                    throw new UserFriendlyException("Impossible de modifier Company/Année/Mois : la période n’est plus en Brouillon.");
                }
            }
        }

        // --- Utils ---
        private void EnsureDates()
        {
            if (!DateDebut.HasValue || !DateFin.HasValue)
            {
                var start = new DateTime(Annee, Mois, 1);
                DateDebut ??= start;
                DateFin ??= start.AddMonths(1).AddDays(-1);
            }
        }
        private void EnsureKeyAndLibelle()
        {
            Key = $"{Annee:D4}-{Mois:D2}";
            if (string.IsNullOrWhiteSpace(Libelle))
                Libelle = $"{Mois:D2}/{Annee}";
        }

        // Scopes (ici, pas de tenant car base par tenant ; on borne par Company)
        private GroupOperator Scope(params CriteriaOperator[] filters)
        {
            if (Company == null) throw new UserFriendlyException("L’entreprise (Company) est obligatoire.");
            var ops = filters?.ToList() ?? new System.Collections.Generic.List<CriteriaOperator>();
            ops.Insert(0, new BinaryOperator(nameof(Company), Company));
            return new GroupOperator(GroupOperatorType.And, ops);
        }
        private GroupOperator ScopeFor<T>(params CriteriaOperator[] filters)
        {
            var ops = filters?.ToList() ?? new System.Collections.Generic.List<CriteriaOperator>();
            var ci = Session.GetClassInfo(typeof(T));
            if (ci.FindMember("Company") != null)
                ops.Insert(0, new BinaryOperator("Company", Company));
            return new GroupOperator(GroupOperatorType.And, ops);
        }

        // --- Actions ---

     //   [Action(Caption = "Ouvrir", ImageName = "Action_Open", AutoCommit = true)]
        public void Ouvrir()
        {
            if (Company == null) throw new UserFriendlyException("Sélectionnez une entreprise.");
            if (Statut != PeriodePaieStatut.Brouillon)
                throw new UserFriendlyException("Seules les périodes en Brouillon peuvent être ouvertes.");

            // Aucune autre période OUVERTE pour cette Company
            var anyOpenCrit = Scope(
                new BinaryOperator(nameof(Statut), PeriodePaieStatut.Ouverte),
                new NotOperator(new BinaryOperator(nameof(Oid), Oid))
            );
            var nbOpen = Convert.ToInt32(Session.Evaluate(typeof(PeriodePaie), CriteriaOperator.Parse("Count()"), anyOpenCrit));
            if (nbOpen > 0)
                throw new UserFriendlyException("Impossible d’ouvrir : une autre période est déjà ouverte pour cette entreprise.");

            // Toutes les périodes précédentes (même Company) doivent être clôturées
            var prevPeriodCrit = new GroupOperator(GroupOperatorType.Or,
                new BinaryOperator(nameof(Annee), Annee, BinaryOperatorType.Less),
                new GroupOperator(GroupOperatorType.And,
                    new BinaryOperator(nameof(Annee), Annee),
                    new BinaryOperator(nameof(Mois), Mois, BinaryOperatorType.Less)
                )
            );
            var notClosedCrit = new NotOperator(new BinaryOperator(nameof(Statut), PeriodePaieStatut.Cloturee));
            var mustClosePrevCrit = Scope(prevPeriodCrit, notClosedCrit);

            var prevNotClosed = Convert.ToInt32(Session.Evaluate(typeof(PeriodePaie), CriteriaOperator.Parse("Count()"), mustClosePrevCrit));
            if (prevNotClosed > 0)
                throw new UserFriendlyException("Clôturez les périodes précédentes de cette entreprise avant d’ouvrir celle-ci.");

            Statut = PeriodePaieStatut.Ouverte;
            DateOuverture = DateTime.Now;
        }

    //    [Action(Caption = "Clôturer", ImageName = "Action_Approve", AutoCommit = true)]
        public void Cloturer()
        {
            if (Company == null) throw new UserFriendlyException("Sélectionnez une entreprise.");
            if (Statut != PeriodePaieStatut.Ouverte)
                throw new UserFriendlyException("La période doit être Ouverte pour être clôturée.");

            // Tous les bulletins de la Company + mois/année hors Brouillon
            var bullBase = new GroupOperator(GroupOperatorType.And,
                new BinaryOperator(nameof(Bulletin.Annee), Annee),
                new BinaryOperator(nameof(Bulletin.Mois), Mois)
            );
            var bullCrit = ScopeFor<Bulletin>(bullBase);

            var brouillonCrit = new GroupOperator(GroupOperatorType.And,
                bullCrit,
                new BinaryOperator(nameof(Bulletin.Statut), BulletinStatut.Brouillon)
            );

            var nbBrouillons = Convert.ToInt32(Session.Evaluate(typeof(Bulletin), CriteriaOperator.Parse("Count()"), brouillonCrit));
            if (nbBrouillons > 0)
            {
                var sample = new XPCollection<Bulletin>(Session, brouillonCrit) { TopReturnedObjects = 3 };
                var first3 = string.Join(", ", sample.Select(x => x.DisplayName));
                var more = nbBrouillons > 3 ? $" (+{nbBrouillons - 3} autres)" : "";
                throw new UserFriendlyException($"Impossible de clôturer : bulletins en brouillon : {first3}{more}.");
            }

            Statut = PeriodePaieStatut.Cloturee;
            DateCloture = DateTime.Now;
        }

       // [Action(Caption = "Réouvrir", ImageName = "Action_ResetViewSettings", AutoCommit = true)]
        public void Reouvrir()
        {
            if (Company == null) throw new UserFriendlyException("Sélectionnez une entreprise.");
            if (Statut != PeriodePaieStatut.Cloturee)
                throw new UserFriendlyException("Seules les périodes Clôturées peuvent être réouvertes.");

            // Garde-fous éventuels (exports, droits...)
            Statut = PeriodePaieStatut.Ouverte;
            DateCloture = null;
        }

        // --- Compteurs (scopés Company) ---
        [NonPersistent, XafDisplayName("Bulletins (total)")]
        public int NbBulletins
        {
            get
            {
                var baseCrit = new GroupOperator(GroupOperatorType.And,
                    new BinaryOperator(nameof(Bulletin.Annee), Annee),
                    new BinaryOperator(nameof(Bulletin.Mois), Mois)
                );
                var crit = ScopeFor<Bulletin>(baseCrit);
                return Convert.ToInt32(Session.Evaluate(typeof(Bulletin), CriteriaOperator.Parse("Count()"), crit));
            }
        }

        [NonPersistent, XafDisplayName("Bulletins brouillon")]
        public int NbBulletinsBrouillon
        {
            get
            {
                var baseCrit = new GroupOperator(GroupOperatorType.And,
                    new BinaryOperator(nameof(Bulletin.Annee), Annee),
                    new BinaryOperator(nameof(Bulletin.Mois), Mois),
                    new BinaryOperator(nameof(Bulletin.Statut), BulletinStatut.Brouillon)
                );
                var crit = ScopeFor<Bulletin>(baseCrit);
                return Convert.ToInt32(Session.Evaluate(typeof(Bulletin), CriteriaOperator.Parse("Count()"), crit));
            }
        }
    }
}
