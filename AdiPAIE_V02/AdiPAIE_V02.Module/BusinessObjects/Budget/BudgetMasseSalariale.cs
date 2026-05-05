// =============================================================================
//  BudgetMasseSalariale.cs — V1.2.1 (mai 2026)
//
//  REFONTE : passage à un modèle simplifié sur demande DAF.
//
//  AVANT (V1.2)  : saisie mensuelle × 9 rubriques × site (~180 lignes/an)
//  APRÈS (V1.2.1) : saisie ANNUELLE sur le BRUT × site (1-N lignes/an)
//
//  Le DAF saisit un montant brut annuel validé en CODIR. La comparaison se
//  fait avec le RÉALISÉ = SUM(Bulletin.BrutFiscal) sur l'année.
//
//  Granularité conservée :
//    - Année (obligatoire)
//    - Site (nullable : NULL = budget global non ventilé)
//
//  Granularité supprimée :
//    - Mois (le budget est annuel, pas besoin de mensualisation)
//    - Rubrique (on ne suit que le brut, pas la décomposition rubrique)
//
//  Saisie possible via :
//    1. Formulaire XAF — 1 ligne par site, ou 1 ligne global
//    2. Recopie N-1 + inflation %
//    3. Import Excel (template simple : Année / Site / Montant)
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

        [XafDisplayName("Données de démo")]
        Demo = 3
    }

    [DefaultClassOptions]
    [XafDisplayName("Budget Masse Salariale")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Money")]
    [NavigationItem("GRH - Administration")]
    [RuleCriteria("BudgetMS_AnneeValide", DefaultContexts.Save,
        "Annee >= 2000 AND Annee <= 2100",
        CustomMessageTemplate = "L'année doit être comprise entre 2000 et 2100.")]
    [RuleCriteria("BudgetMS_MontantPositif", DefaultContexts.Save,
        "MontantBrutAnnuel >= 0",
        CustomMessageTemplate = "Le montant budgété ne peut pas être négatif.")]
    public class BudgetMasseSalariale : BaseObject
    {
        public BudgetMasseSalariale(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Annee = DateTime.Today.Year;
            Source = BudgetSource.SaisieManuelle;
        }

        // ── Période ──────────────────────────────────────────────

        private int _annee;
        [XafDisplayName("Année")]
        [Indexed("Site", Unique = true, Name = "UX_BudgetMS_Annee_Site")]
        [ToolTip("Une seule ligne de budget par couple (Année, Site).")]
        public int Annee
        {
            get => _annee;
            set => SetPropertyValue(nameof(Annee), ref _annee, value);
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

        // ── Montant ──────────────────────────────────────────────

        private decimal _montantBrutAnnuel;
        [XafDisplayName("Montant brut annuel (FCFA)")]
        [ToolTip("Enveloppe brute annuelle validée en CODIR (avant charges patronales).")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ModelDefault("EditMask", "N0")]
        public decimal MontantBrutAnnuel
        {
            get => _montantBrutAnnuel;
            set => SetPropertyValue(nameof(MontantBrutAnnuel), ref _montantBrutAnnuel, value);
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
                var siteLbl = Site?.Nom ?? "Global (tous sites)";
                return $"Budget {Annee} • {siteLbl} • {MontantBrutAnnuel:N0} FCFA";
            }
        }
    }
}
