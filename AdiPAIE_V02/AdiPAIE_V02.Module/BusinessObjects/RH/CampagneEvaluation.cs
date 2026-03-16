using DevExpress.Drawing;
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
    /// <summary>
    /// Campagne d'évaluation annuelle.
    /// Une campagne regroupe tous les entretiens d'une période donnée.
    /// Workflow : Brouillon → Ouverte → EnCours → Clôturée.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Campagne d'évaluation")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Event")]
    [NavigationItem("GRH - Évaluation")]

    // Unicité Entreprise + Année
    [RuleCombinationOfPropertiesIsUnique(
        "CampagneEval_Company_Annee_Unique", DefaultContexts.Save, "Company;Annee",
        CustomMessageTemplate = "Une campagne existe déjà pour cette entreprise et cette année.")]

    // Styles par statut
    [Appearance("Campagne_Cloturee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CampagneStatut,Cloturee#",
        FontColor = "Gray", FontStyle = DXFontStyle.Italic)]
    [Appearance("Campagne_Ouverte", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CampagneStatut,Ouverte#",
        FontColor = "Green", FontStyle = DXFontStyle.Bold)]

    // Verrouillage des clés hors Brouillon
    [Appearance("Lock_Keys_Campagne",
        Criteria = "Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CampagneStatut,Brouillon#",
        TargetItems = "Company;Annee", Enabled = false)]
    public class CampagneEvaluation : BaseObject
    {
        public CampagneEvaluation(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Annee = DateTime.Today.Year;
            DateDebut = new DateTime(DateTime.Today.Year, 1, 1);
            DateFin = new DateTime(DateTime.Today.Year, 12, 31);
            Statut = CampagneStatut.Brouillon;
            DateCreation = DateTime.Now;
            try { CreePar = SecuritySystem.CurrentUserName; } catch { }
        }

        // ── Identité ──────────────────────────────────────────────
        [Association("Company-Campagnes")]
        [RuleRequiredField]
        [XafDisplayName("Entreprise")]
        public Company Company
        {
            get => company;
            set => SetPropertyValue(nameof(Company), ref company, value);
        }
        Company company;

        [RuleRange(2000, 2100)]
        [XafDisplayName("Année")]
        public int Annee
        {
            get => annee;
            set => SetPropertyValue(nameof(Annee), ref annee, value);
        }
        int annee;

        [PersistentAlias("Concat(Company.RaisonSociale, ' - Campagne ', ToStr(Annee))")]
        [VisibleInLookupListView(true)]
        public string DisplayName => (string)EvaluateAlias(nameof(DisplayName));

        string titre;
        [Size(150)]
        [XafDisplayName("Titre de la campagne")]
        public string Titre
        {
            get => titre;
            set => SetPropertyValue(nameof(Titre), ref titre, value?.Trim());
        }

        // ── Période ───────────────────────────────────────────────
        [XafDisplayName("Date d'ouverture")]
        public DateTime DateDebut
        {
            get => dateDebut;
            set => SetPropertyValue(nameof(DateDebut), ref dateDebut, value);
        }
        DateTime dateDebut;

        [XafDisplayName("Date de clôture prévue")]
        public DateTime DateFin
        {
            get => dateFin;
            set => SetPropertyValue(nameof(DateFin), ref dateFin, value);
        }
        DateTime dateFin;

        // ── Statut ────────────────────────────────────────────────
        CampagneStatut statut;
        [XafDisplayName("Statut")]
        public CampagneStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        // ── Instructions RH ───────────────────────────────────────
        string instructions;
        [Size(2048)]
        [XafDisplayName("Instructions / message aux managers")]
        public string Instructions
        {
            get => instructions;
            set => SetPropertyValue(nameof(Instructions), ref instructions, value?.Trim());
        }

        // ── Traçabilité ───────────────────────────────────────────
        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        public DateTime DateCreation
        {
            get => dateCreation;
            set => SetPropertyValue(nameof(DateCreation), ref dateCreation, value);
        }
        DateTime dateCreation;

        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        [Size(50)]
        public string CreePar
        {
            get => creePar;
            set => SetPropertyValue(nameof(CreePar), ref creePar, value);
        }
        string creePar;

        // ── Relations ─────────────────────────────────────────────
        [Association("CampagneEvaluation-Entretiens"), Aggregated]
        [XafDisplayName("Entretiens")]
        public XPCollection<EntretienAnnuel> Entretiens => GetCollection<EntretienAnnuel>(nameof(Entretiens));

        // ── Compteurs (non persistants) ───────────────────────────
        [NonPersistent]
        [XafDisplayName("Total entretiens")]
        public int NbEntretiens => Entretiens.Count;

        [NonPersistent]
        [XafDisplayName("Entretiens clôturés")]
        public int NbClotures => Entretiens.Count(e => e.Statut == EntretienStatut.Cloture);

        [NonPersistent]
        [XafDisplayName("% avancement")]
        [ModelDefault("DisplayFormat", "P0")]
        public decimal PctAvancement =>
            NbEntretiens == 0 ? 0m : (decimal)NbClotures / NbEntretiens;

        // ── Workflow (méthodes appelées par le controller) ────────
        public void Ouvrir()
        {
            if (Statut != CampagneStatut.Brouillon)
                throw new UserFriendlyException("La campagne n'est pas en état Brouillon.");
            Statut = CampagneStatut.Ouverte;
        }

        public void LancerEntretiens()
        {
            if (Statut != CampagneStatut.Ouverte)
                throw new UserFriendlyException("La campagne doit être Ouverte pour lancer les entretiens.");
            Statut = CampagneStatut.EnCours;
        }

        public void Cloturer()
        {
            if (Statut == CampagneStatut.Cloturee)
                throw new UserFriendlyException("La campagne est déjà clôturée.");
            Statut = CampagneStatut.Cloturee;
        }
    }
}
