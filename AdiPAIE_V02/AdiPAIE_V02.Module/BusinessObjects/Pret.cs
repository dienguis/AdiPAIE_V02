using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
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
    //[NavigationItem("Traitement")]
    [DefaultClassOptions]
    [XafDisplayName("Prêt salarié")]
    [DefaultProperty(nameof(DisplayName))]
    public class Pret : BaseObject
    {
        public Pret(Session s) : base(s) { }

        // ---------- Helpers ----------
        private static decimal N(decimal? v) => v ?? 0m;

        // ---------- Identité ----------
        [Association("Salarie-Prets"), RuleRequiredField]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        private Salarie salarie;

        [Size(60)]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value?.Trim());
        }
        private string reference;

        [Size(120)]
        public string Intitule
        {
            get => intitule;
            set => SetPropertyValue(nameof(Intitule), ref intitule, value?.Trim());
        }
        private string intitule;

        [PersistentAlias("Iif(IsNull(Intitule) Or Len(Intitule)=0, Concat('Prêt ', ToStr(Oid)), Intitule)")]
        public string DisplayName => (string)EvaluateAlias(nameof(DisplayName));

        // ---------- Paramètres financiers ----------
        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Montant principal"), RuleRange(1, 999999999)]
        public decimal MontantPrincipal
        {
            get => principal;
            set => SetPropertyValue(nameof(MontantPrincipal), ref principal, value);
        }
        private decimal principal;

        [DbType("decimal(18,2)"), ModelDefault("DisplayFormat", "N2"), ModelDefault("EditMask", "N2")]
        [XafDisplayName("Taux annuel %")]
        public decimal? TauxAnnuelPercent
        {
            get => tauxAnnuel;
            set => SetPropertyValue(nameof(TauxAnnuelPercent), ref tauxAnnuel, value);
        }
        private decimal? tauxAnnuel;

        [RuleRange(1, 480)]
        public int DureeMois
        {
            get => duree;
            set => SetPropertyValue(nameof(DureeMois), ref duree, value);
        }
        private int duree;

        public PretAmortissement ModeAmortissement
        {
            get => mode;
            set => SetPropertyValue(nameof(ModeAmortissement), ref mode, value);
        }
        private PretAmortissement mode = PretAmortissement.PrincipalConstant;

        public DateTime DateDebut
        {
            get => d0;
            set => SetPropertyValue(nameof(DateDebut), ref d0, value);
        }
        private DateTime d0 = DateTime.Today;

        // ---------- Suivi / Statut ----------
        public PretStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }
        private PretStatut statut = PretStatut.Brouillon;

        public PretNature Nature
        {
            get => nature;
            set => SetPropertyValue(nameof(Nature), ref nature, value);
        }
        private PretNature nature = PretNature.Pret;

        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [DbType("decimal(18,0)")]
        [XafDisplayName("Total à prélever")]
        public decimal TotalAPrelever
        {
            get => totalAPrelever;
            set => SetPropertyValue(nameof(TotalAPrelever), ref totalAPrelever, value);
        }
        private decimal totalAPrelever;

        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [DbType("decimal(18,0)")]
        [XafDisplayName("Total prélevé")]
        public decimal TotalPreleve
        {
            get => totalPreleve;
            set => SetPropertyValue(nameof(TotalPreleve), ref totalPreleve, value);
        }
        private decimal totalPreleve;

        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [DbType("decimal(18,0)")]
        [XafDisplayName("Reste à régler")]
        public decimal ResteARegler
        {
            get => resteARegler;
            set => SetPropertyValue(nameof(ResteARegler), ref resteARegler, value);
        }
        private decimal resteARegler;

        // ---------- Collections ----------
        [Association("Pret-Echeances"), Aggregated]
        public XPCollection<PretEcheance> Echeances => GetCollection<PretEcheance>(nameof(Echeances));

        [Association("PretType-Prets")]
        [RuleRequiredField(CustomMessageTemplate = "Le type de prêt est obligatoire.")]
        public PretType TypePret
        {
            get => type; set => SetPropertyValue(nameof(TypePret), ref type, value);
        }
        PretType type;

        // (facultatif) garde la possibilité d'override au niveau du prêt
        [RuleRequiredField(CustomMessageTemplate = "La rubrique de retenue est obligatoire.")]
        public Rubrique RubriqueRetenue
        {
            get => rub; set => SetPropertyValue(nameof(RubriqueRetenue), ref rub, value);
        }
        Rubrique rub;

        // Resolve effectif : Prêt.RubriqueRetenue > PretType.RubriqueRetenue > ParametresPaie par Nature
        public Rubrique GetRubriqueRetenueEffective()
        {
            if (RubriqueRetenue != null) return RubriqueRetenue;
            if (TypePret?.RubriqueRetenue != null) return TypePret.RubriqueRetenue;
            var prm = new XPQuery<ParametresPaie>(Session).FirstOrDefault();
            return prm?.ResolveRubriqueRetenue(TypePret?.Nature ?? Nature);
        }

        // ---------- Méthodes métier ----------
        public void GenererEcheancier(bool effacerExistant = true)
        {
            if (Salarie == null) throw new UserFriendlyException("Sélectionnez le salarié.");
            if (MontantPrincipal <= 0) throw new UserFriendlyException("Montant principal invalide.");
            if (DureeMois <= 0) throw new UserFriendlyException("Durée invalide.");

            // purge optionnelle
            if (effacerExistant)
            {
                foreach (var e in Echeances.ToList()) e.Delete();
                Session.FlushChanges();
            }

            var rMens = (TauxAnnuelPercent ?? 0m) / 12m / 100m;
            var capitalRestant = MontantPrincipal;

            // annuité constante
            decimal annuite = 0m;
            if (ModeAmortissement == PretAmortissement.AnnuiteConstante)
            {
                if (rMens > 0m)
                {
                    var pow = Math.Pow(1 + (double)rMens, DureeMois);
                    annuite = (decimal)((double)MontantPrincipal * (double)rMens / (1.0 - 1.0 / pow));
                }
                else
                {
                    annuite = Math.Round(MontantPrincipal / DureeMois, 0, MidpointRounding.AwayFromZero);
                }
            }

            for (int i = 0; i < DureeMois; i++)
            {
                var dEch = DateDebut.AddMonths(i);
                var dernierJour = new DateTime(dEch.Year, dEch.Month, DateTime.DaysInMonth(dEch.Year, dEch.Month));

                decimal interet = Math.Round(capitalRestant * rMens, 0, MidpointRounding.AwayFromZero);
                decimal capital;

                if (ModeAmortissement == PretAmortissement.PrincipalConstant)
                {
                    capital = Math.Round(MontantPrincipal / DureeMois, 0, MidpointRounding.AwayFromZero);
                    if (i == DureeMois - 1) // ajuste la dernière
                        capital = Math.Max(0m, capitalRestant);
                }
                else
                {
                    var total = Math.Round(annuite, 0, MidpointRounding.AwayFromZero);
                    capital = Math.Max(0m, total - interet);
                    if (i == DureeMois - 1)
                        capital = Math.Max(0m, capitalRestant); // ajuste le reliquat
                }

                var e = new PretEcheance(Session)
                {
                    Pret = this,
                    DateEcheance = dernierJour,
                    MontantCapital = capital,
                    MontantInteret = interet,
                    Statut = PretEcheanceStatut.Prevue
                };

                capitalRestant = Math.Max(0m, capitalRestant - capital);
            }

            RecalculerEtat();
        }

        public void RecalculerEtat()
        {
            if (IsDeleted) return;

            var echs = Echeances?.ToList() ?? new System.Collections.Generic.List<PretEcheance>();

            var totalPrevu = echs.Where(e => e.Statut != PretEcheanceStatut.Annulee)
                                 .Sum(e => e.MontantTotal);

            var totalPreleve = echs.Where(e => e.Statut == PretEcheanceStatut.Prelevee)
                                   .Sum(e => e.MontantTotal);

            TotalAPrelever = totalPrevu;
            TotalPreleve = totalPreleve;
            ResteARegler = Math.Max(0m, totalPrevu - totalPreleve);

            if (Statut != PretStatut.Suspendu)
            {
                if (!echs.Any())
                    Statut = PretStatut.Brouillon;
                else if (ResteARegler <= 0m)
                    Statut = PretStatut.Termine;
                else
                    Statut = PretStatut.EnCours;
            }
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            if (!Session.IsObjectsLoading) RecalculerEtat();
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted) RecalculerEtat();
        }
    }
}