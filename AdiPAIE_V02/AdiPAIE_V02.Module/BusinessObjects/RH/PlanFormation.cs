using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp;
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
    /// <summary>
    /// Plan annuel de formation — approuvé par la direction avant exécution.
    ///
    /// Cycle : Brouillon → Soumis → Approuvé → En cours → Clôturé
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Plan de formation")]
    [DefaultProperty(nameof(Titre))]
    [ImageName("BO_List")]
    [NavigationItem("GRH - Formation")]
    [Appearance("PlanApprouve", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PlanFormationStatut,Approuve#",
        FontColor = "#1B6C2A", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("PlanAnnule", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PlanFormationStatut,Annule#",
        FontColor = "Gray", FontStyle = DevExpress.Drawing.DXFontStyle.Italic)]
    public class PlanFormation : BaseObject
    {
        public PlanFormation(Session session) : base(session) { }

        // ── Entête ────────────────────────────────────────────
        string titre;
        [RuleRequiredField]
        [Size(200)]
        [XafDisplayName("Titre du plan")]
        public string Titre
        {
            get => titre;
            set => SetPropertyValue(nameof(Titre), ref titre, value?.Trim());
        }

        int annee;
        [XafDisplayName("Année")]
        public int Annee
        {
            get => annee;
            set => SetPropertyValue(nameof(Annee), ref annee, value);
        }

        // ── Périmètre ────────────────────────────────────────
        [Association("Company-PlansFormation")]
        [XafDisplayName("Entreprise")]
        public Company Entreprise
        {
            get => entreprise;
            set => SetPropertyValue(nameof(Entreprise), ref entreprise, value);
        }
        Company entreprise;

        // ── Budget ───────────────────────────────────────────
        decimal budgetPrevisionnel;
        [XafDisplayName("Budget prévisionnel (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        public decimal BudgetPrevisionnel
        {
            get => budgetPrevisionnel;
            set => SetPropertyValue(nameof(BudgetPrevisionnel), ref budgetPrevisionnel, value);
        }

        // ── Statut & workflow ─────────────────────────────────
        PlanFormationStatut statut;
        [XafDisplayName("Statut")]
        [ModelDefault("AllowEdit", "False")]
        public PlanFormationStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        DateTime? dateSoumission;
        [XafDisplayName("Date de soumission")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateSoumission
        {
            get => dateSoumission;
            set => SetPropertyValue(nameof(DateSoumission), ref dateSoumission, value);
        }

        DateTime? dateApprobation;
        [XafDisplayName("Date d'approbation")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateApprobation
        {
            get => dateApprobation;
            set => SetPropertyValue(nameof(DateApprobation), ref dateApprobation, value);
        }

        [Association("Salarie-PlansApprouves")]
        [XafDisplayName("Approuvé par")]
        [ModelDefault("AllowEdit", "False")]
        public Salarie ApprovePar
        {
            get => approvePar;
            set => SetPropertyValue(nameof(ApprovePar), ref approvePar, value);
        }
        Salarie approvePar;

        string commentaire;
        [Size(1000)]
        [XafDisplayName("Commentaire / Motif refus")]
        public string Commentaire
        {
            get => commentaire;
            set => SetPropertyValue(nameof(Commentaire), ref commentaire, value?.Trim());
        }

        // ── KPIs calculés ─────────────────────────────────────
        [NonPersistent]
        [XafDisplayName("Nb sessions planifiées")]
        public int NbSessions => Sessions.Count;

        [NonPersistent]
        [XafDisplayName("Nb inscrits total")]
        public int NbInscrits => Sessions.SelectMany(s => s.Inscriptions).Count();

        [NonPersistent]
        [XafDisplayName("Coût réel total (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        public decimal CoutReel => Sessions.Sum(s => s.CoutReel);

        [NonPersistent]
        [XafDisplayName("Taux consommation budget (%)")]
        [ModelDefault("DisplayFormat", "N1")]
        public decimal TauxConsommation =>
            BudgetPrevisionnel > 0
                ? Math.Round(CoutReel / BudgetPrevisionnel * 100, 1)
                : 0;

        // ── Collections ───────────────────────────────────────
        [Association("PlanFormation-Sessions"), Aggregated]
        [XafDisplayName("Sessions de formation")]
        public XPCollection<SessionFormation> Sessions
            => GetCollection<SessionFormation>(nameof(Sessions));

        // ── Transitions de statut ─────────────────────────────
        public void Soumettre()
        {
            if (Statut != PlanFormationStatut.Brouillon)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "Le plan doit être en brouillon pour être soumis.");
            Statut = PlanFormationStatut.Soumis;
            DateSoumission = DateTime.Now;
        }

        public void Approuver(Salarie par)
        {
            if (Statut != PlanFormationStatut.Soumis)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "Le plan doit être soumis pour être approuvé.");
            Statut = PlanFormationStatut.Approuve;
            DateApprobation = DateTime.Now;
            ApprovePar = par;
        }

        public void Rejeter(string motif)
        {
            if (Statut != PlanFormationStatut.Soumis)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "Le plan doit être soumis pour être rejeté.");
            Statut = PlanFormationStatut.Brouillon;
            Commentaire = motif;
        }

        public void Demarrer()
        {
            if (Statut != PlanFormationStatut.Approuve)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "Le plan doit être approuvé pour démarrer.");
            Statut = PlanFormationStatut.EnCours;
        }

        public void Cloturer()
        {
            if (Statut != PlanFormationStatut.EnCours)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "Le plan doit être en cours pour être clôturé.");
            Statut = PlanFormationStatut.Cloture;
        }

        public void Annuler()
        {
            if (Statut == PlanFormationStatut.Cloture)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "Un plan clôturé ne peut pas être annulé.");
            Statut = PlanFormationStatut.Annule;
        }

        // ── Cycle de vie ──────────────────────────────────────
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Annee = DateTime.Today.Year;
            Statut = PlanFormationStatut.Brouillon;
        }
    }
}
