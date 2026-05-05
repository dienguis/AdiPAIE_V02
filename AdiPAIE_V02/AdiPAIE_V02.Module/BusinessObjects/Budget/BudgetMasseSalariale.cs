// =============================================================================
//  BudgetMasseSalariale.cs — V1.2 (mai 2026)
//
//  Entité socle du module Budget RH (cf. MISSION_STATE section 9.1).
//  Stocke le budget prévisionnel de masse salariale ventilé par :
//    - Année + Mois (granularité mensuelle obligatoire)
//    - Site (FK nullable : NULL = budget global non ventilé par site)
//    - Rubrique (catégorie de coût RH : SalairesBase, Primes, 13e mois,
//      Charges patronales, Avantages, Formation, Recrutement)
//
//  Ce budget est comparé au RÉALISÉ (calculé depuis Bulletin + BulletinLigne)
//  dans le dashboard "Budget vs Réalisé" pour produire les écarts mensuels
//  et cumulés YTD.
//
//  Saisie possible via :
//    1. Formulaire XAF (rubrique × montant annuel) avec mensualisation auto
//    2. Import Excel (template fourni : rubrique × mois × site)
//    3. Recopie N-1 + inflation %
// =============================================================================

using System;
using System.ComponentModel;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace AdiPAIE_V02.Module.BusinessObjects.Budget
{
    /// <summary>
    /// Catégorie de coût RH pour le budget. Les rubriques sont alignées
    /// sur la grille du DAF ELTON (cf. cadrage 2026-05-04).
    /// </summary>
    public enum BudgetRubrique
    {
        [XafDisplayName("Salaires de base")]
        SalairesBase = 0,

        [XafDisplayName("Primes")]
        Primes = 1,

        [XafDisplayName("13e mois")]
        TreiziemeMois = 2,

        [XafDisplayName("Gratifications")]
        Gratifications = 3,

        [XafDisplayName("Indemnités")]
        Indemnites = 4,

        [XafDisplayName("Charges patronales")]
        ChargesPatronales = 5,

        [XafDisplayName("Avantages en nature")]
        AvantagesNature = 6,

        [XafDisplayName("Formation")]
        Formation = 7,

        [XafDisplayName("Recrutement")]
        Recrutement = 8,

        [XafDisplayName("Autre")]
        Autre = 99
    }

    /// <summary>
    /// Source de la ligne budgétaire (traçabilité pour audit).
    /// </summary>
    public enum BudgetSource
    {
        [XafDisplayName("Saisie manuelle")]
        SaisieManuelle = 0,

        [XafDisplayName("Import Excel")]
        ImportExcel = 1,

        [XafDisplayName("Recopie N-1")]
        RecopieN1 = 2,

        [XafDisplayName("Auto-mensualisé")]
        AutoMensualise = 3
    }

    [DefaultClassOptions]
    [XafDisplayName("Budget Masse Salariale")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Money")]
    [NavigationItem("GRH - Administration")]   // V1.2 — ID interne du groupe affiché "Paramétrage" (cf. Model.DesignedDiffs.xafml)
    [RuleCriteria("BudgetMS_MoisValide", DefaultContexts.Save,
        "Mois >= 1 AND Mois <= 12",
        CustomMessageTemplate = "Le mois doit être compris entre 1 et 12.")]
    [RuleCriteria("BudgetMS_AnneeValide", DefaultContexts.Save,
        "Annee >= 2000 AND Annee <= 2100",
        CustomMessageTemplate = "L'année doit être comprise entre 2000 et 2100.")]
    [RuleCriteria("BudgetMS_MontantPositif", DefaultContexts.Save,
        "Montant >= 0",
        CustomMessageTemplate = "Le montant budgété ne peut pas être négatif.")]
    public class BudgetMasseSalariale : BaseObject
    {
        public BudgetMasseSalariale(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Annee = DateTime.Today.Year;
            Mois = DateTime.Today.Month;
            Source = BudgetSource.SaisieManuelle;
        }

        // ── Période ──────────────────────────────────────────────

        private int _annee;
        [XafDisplayName("Année")]
        [Indexed("Mois;Site;Rubrique", Name = "IX_BudgetMS_Annee_Mois_Site_Rubrique")]
        public int Annee
        {
            get => _annee;
            set => SetPropertyValue(nameof(Annee), ref _annee, value);
        }

        private int _mois;
        [XafDisplayName("Mois (1-12)")]
        public int Mois
        {
            get => _mois;
            set => SetPropertyValue(nameof(Mois), ref _mois, value);
        }

        // ── Périmètre ────────────────────────────────────────────

        private Site _site;
        [XafDisplayName("Site")]
        [ToolTip("Laisser vide pour un budget global non ventilé par site.")]
        public Site Site
        {
            get => _site;
            set => SetPropertyValue(nameof(Site), ref _site, value);
        }

        // ── Catégorisation ───────────────────────────────────────

        private BudgetRubrique _rubrique;
        [XafDisplayName("Rubrique")]
        public BudgetRubrique Rubrique
        {
            get => _rubrique;
            set => SetPropertyValue(nameof(Rubrique), ref _rubrique, value);
        }

        // ── Montant ──────────────────────────────────────────────

        private decimal _montant;
        [XafDisplayName("Montant budgété (FCFA)")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ModelDefault("EditMask", "N0")]
        public decimal Montant
        {
            get => _montant;
            set => SetPropertyValue(nameof(Montant), ref _montant, value);
        }

        // ── Traçabilité ──────────────────────────────────────────

        private BudgetSource _source;
        [XafDisplayName("Source")]
        public BudgetSource Source
        {
            get => _source;
            set => SetPropertyValue(nameof(Source), ref _source, value);
        }

        private string _commentaire;
        [XafDisplayName("Commentaire")]
        [Size(500)]
        public string Commentaire
        {
            get => _commentaire;
            set => SetPropertyValue(nameof(Commentaire), ref _commentaire, value);
        }

        // ── Affichage ────────────────────────────────────────────

        [NonPersistent]
        [Browsable(false)]
        public string DisplayName
        {
            get
            {
                var siteLbl = Site?.Nom ?? "Global";
                return $"{Annee}-{Mois:D2} • {Rubrique} • {siteLbl} • {Montant:N0} FCFA";
            }
        }
    }
}
