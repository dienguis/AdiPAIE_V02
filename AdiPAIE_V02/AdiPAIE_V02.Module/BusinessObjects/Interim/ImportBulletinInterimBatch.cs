// =============================================================================
//  ImportBulletinInterimBatch.cs — V1.3 Sprint 1 (mai 2026)
//
//  Audit / grouping de chaque import de fichier "Livre de paie intérim".
//  1 batch = 1 fichier importé = 1 société d'intérim × 1 mois × 1 année.
//
//  Permet de :
//    - Suivre QUI a importé QUOI et QUAND
//    - Voir les statistiques agrégées (NbLignes, NbCreees, NbPrestataires, TotalTTC)
//    - Annuler/réimporter un batch en bloc (suppression ON DELETE CASCADE
//      sur les BulletinInterim associés via l'association)
//    - Empêcher les doublons (1 seul batch par triplet Année/Mois/Société)
// =============================================================================

using System;
using System.ComponentModel;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects.Interim
{
    [DefaultClassOptions]
    [XafDisplayName("Lot d'import — Bulletin intérim")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Audit")]
    [NavigationItem("GRH_Interimaires")]
    public class ImportBulletinInterimBatch : BaseObject
    {
        public ImportBulletinInterimBatch(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateImport = DateTime.Now;
            Annee = DateTime.Today.Year;
            Mois = DateTime.Today.Month;
        }

        // ── Période + Société ────────────────────────────────────

        private int _annee;
        [XafDisplayName("Année")]
        [Indexed("Mois;Societe", Unique = true, Name = "UX_Batch_Annee_Mois_Societe")]
        [ToolTip("Un seul batch d'import par couple (Année, Mois, Société d'intérim).")]
        public int Annee
        {
            get => _annee;
            set => SetPropertyValue(nameof(Annee), ref _annee, value);
        }

        private int _mois;
        [XafDisplayName("Mois")]
        public int Mois
        {
            get => _mois;
            set => SetPropertyValue(nameof(Mois), ref _mois, value);
        }

        private SocieteInterim _societe;
        [XafDisplayName("Société d'intérim")]
        [RuleRequiredField]
        public SocieteInterim Societe
        {
            get => _societe;
            set => SetPropertyValue(nameof(Societe), ref _societe, value);
        }

        // ── Métadonnées d'import ─────────────────────────────────

        private DateTime _dateImport;
        [XafDisplayName("Date d'import")]
        [ModelDefault("DisplayFormat", "{0:dd/MM/yyyy HH:mm}")]
        public DateTime DateImport
        {
            get => _dateImport;
            set => SetPropertyValue(nameof(DateImport), ref _dateImport, value);
        }

        private string _fichierSource;
        [XafDisplayName("Fichier source")]
        [Size(255)]
        [VisibleInListView(false)]
        public string FichierSource
        {
            get => _fichierSource;
            set => SetPropertyValue(nameof(FichierSource), ref _fichierSource, value);
        }

        private string _importePar;
        [XafDisplayName("Importé par")]
        [Size(80)]
        public string ImportePar
        {
            get => _importePar;
            set => SetPropertyValue(nameof(ImportePar), ref _importePar, value);
        }

        private string _notes;
        [XafDisplayName("Notes")]
        [Size(1000)]
        [VisibleInListView(false)]
        public string Notes
        {
            get => _notes;
            set => SetPropertyValue(nameof(Notes), ref _notes, value);
        }

        // ── Statistiques agrégées ────────────────────────────────

        private int _nbLignesImportees;
        [XafDisplayName("Nb lignes importées")]
        public int NbLignesImportees
        {
            get => _nbLignesImportees;
            set => SetPropertyValue(nameof(NbLignesImportees), ref _nbLignesImportees, value);
        }

        private int _nbFichesCreees;
        [XafDisplayName("Nb fiches Intérimaire créées auto")]
        [ToolTip("Nombre de fiches Intérimaire créées automatiquement faute de matricule existant. Ces fiches doivent être complétées par le RH (contrat à créer).")]
        public int NbFichesCreees
        {
            get => _nbFichesCreees;
            set => SetPropertyValue(nameof(NbFichesCreees), ref _nbFichesCreees, value);
        }

        private int _nbPrestataires;
        [XafDisplayName("Nb prestataires")]
        [VisibleInListView(false)]
        public int NbPrestataires
        {
            get => _nbPrestataires;
            set => SetPropertyValue(nameof(NbPrestataires), ref _nbPrestataires, value);
        }

        private decimal _totalTTC;
        [XafDisplayName("Total TTC du lot")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        public decimal TotalTTC
        {
            get => _totalTTC;
            set => SetPropertyValue(nameof(TotalTTC), ref _totalTTC, value);
        }

        private decimal _totalDebours;
        [XafDisplayName("Total débours")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [VisibleInListView(false)]
        public decimal TotalDebours
        {
            get => _totalDebours;
            set => SetPropertyValue(nameof(TotalDebours), ref _totalDebours, value);
        }

        private decimal _totalCommissionAgence;
        [XafDisplayName("Total commission agence")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [VisibleInListView(false)]
        public decimal TotalCommissionAgence
        {
            get => _totalCommissionAgence;
            set => SetPropertyValue(nameof(TotalCommissionAgence), ref _totalCommissionAgence, value);
        }

        private decimal _totalTVA;
        [XafDisplayName("Total TVA")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [VisibleInListView(false)]
        public decimal TotalTVA
        {
            get => _totalTVA;
            set => SetPropertyValue(nameof(TotalTVA), ref _totalTVA, value);
        }

        // ── Association avec les bulletins ───────────────────────

        [Association("Batch-Bulletins"), Aggregated]
        [XafDisplayName("Bulletins importés")]
        public XPCollection<BulletinInterim> Bulletins =>
            GetCollection<BulletinInterim>(nameof(Bulletins));

        // ── Affichage ────────────────────────────────────────────

        [NonPersistent]
        [Browsable(false)]
        public string DisplayName =>
            $"{Annee}-{Mois:D2} • {(Societe?.RaisonSociale ?? "?")} • {NbLignesImportees} lignes • {TotalTTC:N0} FCFA TTC";
    }
}
