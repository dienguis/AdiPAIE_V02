// =============================================================================
//  BulletinInterim.cs — V1.3 Sprint 1 (mai 2026)
//
//  1 ligne par intérimaire × mois × société d'intérim émettrice de la facture.
//  Reflète le contenu du fichier "Livre de paie intérim" envoyé chaque mois
//  par les sociétés d'intérim partenaires d'ELTON.
//
//  ⚠️ Ne pas confondre avec l'entité Bulletin (salariés internes CDI/CDD/Stage).
//  Ici on parle de la FACTURATION d'un intérimaire externe.
//
//  Le coût RÉEL pour ELTON est dans le champ TTC (col 53 du fichier Excel).
//  Comparer à la valeur "contrat théorique" (TauxJournalier × jours) du
//  ContratInterim associé permet de mesurer l'écart contrat vs réel.
// =============================================================================

using System;
using System.ComponentModel;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace AdiPAIE_V02.Module.BusinessObjects.Interim
{
    /// <summary>
    /// Statut d'un BulletinInterim après import depuis le fichier Excel.
    /// </summary>
    public enum BulletinInterimStatut
    {
        [XafDisplayName("Importé OK")]
        ImporteOk = 0,

        [XafDisplayName("Fiche créée auto (à compléter)")]
        FicheCreeeAuto = 1,

        [XafDisplayName("Prestataire (hors fiche)")]
        Prestataire = 2,

        [XafDisplayName("Erreur d'import")]
        Erreur = 99
    }

    [DefaultClassOptions]
    [XafDisplayName("Bulletin intérimaire")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Sale_Item")]
    [NavigationItem("GRH_Interimaires")]
    [RuleCriteria("BltInt_AnneeValide", DefaultContexts.Save,
        "Annee >= 2000 AND Annee <= 2100",
        CustomMessageTemplate = "L'année doit être comprise entre 2000 et 2100.")]
    [RuleCriteria("BltInt_MoisValide", DefaultContexts.Save,
        "Mois >= 1 AND Mois <= 12",
        CustomMessageTemplate = "Le mois doit être entre 1 et 12.")]
    public class BulletinInterim : BaseObject
    {
        public BulletinInterim(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Annee = DateTime.Today.Year;
            Mois = DateTime.Today.Month;
            DateImport = DateTime.Now;
            Statut = BulletinInterimStatut.ImporteOk;
        }

        // ═══ Période ═════════════════════════════════════════════════════════

        private int _annee;
        [XafDisplayName("Année")]
        [Indexed("Mois;SocieteEmettrice")]
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

        // ═══ Acteurs ═════════════════════════════════════════════════════════

        private SocieteInterim _societeEmettrice;
        [XafDisplayName("Société d'intérim (émettrice)")]
        [RuleRequiredField]
        public SocieteInterim SocieteEmettrice
        {
            get => _societeEmettrice;
            set => SetPropertyValue(nameof(SocieteEmettrice), ref _societeEmettrice, value);
        }

        private Interimaire _interimaire;
        [XafDisplayName("Intérimaire")]
        [ToolTip("Lookup par matricule. Null si la ligne est un Prestataire.")]
        public Interimaire Interimaire
        {
            get => _interimaire;
            set => SetPropertyValue(nameof(Interimaire), ref _interimaire, value);
        }

        // ═══ Snapshot du fichier source (résilience aux changements) ═════════

        private string _matriculeOriginal;
        [XafDisplayName("Matricule original")]
        [Size(50)]
        [ToolTip("Matricule tel qu'apparaît dans le fichier Excel source. Utile si la fiche Interimaire est renommée plus tard.")]
        [VisibleInListView(false)]
        public string MatriculeOriginal
        {
            get => _matriculeOriginal;
            set => SetPropertyValue(nameof(MatriculeOriginal), ref _matriculeOriginal, value);
        }

        private string _nomComplet;
        [XafDisplayName("Nom complet")]
        [Size(150)]
        [VisibleInListView(false)]
        public string NomComplet
        {
            get => _nomComplet;
            set => SetPropertyValue(nameof(NomComplet), ref _nomComplet, value);
        }

        private string _fonction;
        [XafDisplayName("Fonction")]
        [Size(100)]
        public string Fonction
        {
            get => _fonction;
            set => SetPropertyValue(nameof(Fonction), ref _fonction, value);
        }

        private string _siteAffectation;
        [XafDisplayName("Site")]
        [Size(100)]
        public string SiteAffectation
        {
            get => _siteAffectation;
            set => SetPropertyValue(nameof(SiteAffectation), ref _siteAffectation, value);
        }

        // ═══ Présence ════════════════════════════════════════════════════════

        private decimal _trentieme;
        [XafDisplayName("30ème")]
        [ToolTip("Jours payés / 30. Ex : 30 = mois complet, 15 = demi-mois.")]
        public decimal Trentieme
        {
            get => _trentieme;
            set => SetPropertyValue(nameof(Trentieme), ref _trentieme, value);
        }

        // ═══ Éléments du brut salarié ════════════════════════════════════════

        private decimal _salaireBase;
        [XafDisplayName("Salaire de base")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [VisibleInListView(false)]
        public decimal SalaireBase
        {
            get => _salaireBase;
            set => SetPropertyValue(nameof(SalaireBase), ref _salaireBase, value);
        }

        private decimal _brutImposable;
        [XafDisplayName("Brut imposable")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        public decimal BrutImposable
        {
            get => _brutImposable;
            set => SetPropertyValue(nameof(BrutImposable), ref _brutImposable, value);
        }

        // ═══ Cotisations sociales (parts salariale + patronale) ═════════════

        private decimal _ipresSal;
        [XafDisplayName("IPRES Gén. Sal.")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal IpresSal
        {
            get => _ipresSal;
            set => SetPropertyValue(nameof(IpresSal), ref _ipresSal, value);
        }

        private decimal _ipresPat;
        [XafDisplayName("IPRES Gén. Pat.")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal IpresPat
        {
            get => _ipresPat;
            set => SetPropertyValue(nameof(IpresPat), ref _ipresPat, value);
        }

        private decimal _cssAll;
        [XafDisplayName("CSS Allocations")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal CssAll
        {
            get => _cssAll;
            set => SetPropertyValue(nameof(CssAll), ref _cssAll, value);
        }

        private decimal _cssAcc;
        [XafDisplayName("CSS Accidents")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal CssAcc
        {
            get => _cssAcc;
            set => SetPropertyValue(nameof(CssAcc), ref _cssAcc, value);
        }

        private decimal _ipmSal;
        [XafDisplayName("IPM Salariale")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal IpmSal
        {
            get => _ipmSal;
            set => SetPropertyValue(nameof(IpmSal), ref _ipmSal, value);
        }

        private decimal _ipmPat;
        [XafDisplayName("IPM Patronale")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal IpmPat
        {
            get => _ipmPat;
            set => SetPropertyValue(nameof(IpmPat), ref _ipmPat, value);
        }

        // ═══ Cotisations fiscales ════════════════════════════════════════════

        private decimal _cfce;
        [XafDisplayName("CFCE")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal CFCE
        {
            get => _cfce;
            set => SetPropertyValue(nameof(CFCE), ref _cfce, value);
        }

        private decimal _retenueIR;
        [XafDisplayName("Retenue IR")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal RetenueIR
        {
            get => _retenueIR;
            set => SetPropertyValue(nameof(RetenueIR), ref _retenueIR, value);
        }

        private decimal _retenueTRIMF;
        [XafDisplayName("Retenue TRIMF")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal RetenueTRIMF
        {
            get => _retenueTRIMF;
            set => SetPropertyValue(nameof(RetenueTRIMF), ref _retenueTRIMF, value);
        }

        // ═══ Indemnités complémentaires ══════════════════════════════════════

        private decimal _primeTransport;
        [XafDisplayName("Prime de transport")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal PrimeTransport
        {
            get => _primeTransport;
            set => SetPropertyValue(nameof(PrimeTransport), ref _primeTransport, value);
        }

        private decimal _primePanier;
        [XafDisplayName("Prime de panier")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal PrimePanier
        {
            get => _primePanier;
            set => SetPropertyValue(nameof(PrimePanier), ref _primePanier, value);
        }

        private decimal _indemnitesDiverses;
        [XafDisplayName("Indemnités diverses")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        [VisibleInListView(false)]
        public decimal IndemnitesDiverses
        {
            get => _indemnitesDiverses;
            set => SetPropertyValue(nameof(IndemnitesDiverses), ref _indemnitesDiverses, value);
        }

        // ═══ Net à payer (versé à l'intérimaire par la société d'intérim) ═══

        private decimal _netAPayer;
        [XafDisplayName("Net à payer")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        public decimal NetAPayer
        {
            get => _netAPayer;
            set => SetPropertyValue(nameof(NetAPayer), ref _netAPayer, value);
        }

        // ═══ ★ BLOC FACTURATION (cœur métier — coût réel ELTON) ★ ═══════════

        private decimal _debours;
        [XafDisplayName("Débours")]
        [ToolTip("Total payé par la société d'intérim au salarié + organismes (brut + charges + indemnités).")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [VisibleInListView(false)]
        public decimal Debours
        {
            get => _debours;
            set => SetPropertyValue(nameof(Debours), ref _debours, value);
        }

        private decimal _commissionAgence;
        [XafDisplayName("Commission agence")]
        [ToolTip("Marge facturée par la société d'intérim (typiquement ~9 % du débours).")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [VisibleInListView(false)]
        public decimal CommissionAgence
        {
            get => _commissionAgence;
            set => SetPropertyValue(nameof(CommissionAgence), ref _commissionAgence, value);
        }

        private decimal _montantHT;
        [XafDisplayName("Montant HT")]
        [ToolTip("Débours + Commission agence.")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [VisibleInListView(false)]
        public decimal MontantHT
        {
            get => _montantHT;
            set => SetPropertyValue(nameof(MontantHT), ref _montantHT, value);
        }

        private decimal _tva;
        [XafDisplayName("TVA")]
        [ToolTip("TVA 18 % sur le Montant HT.")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [VisibleInListView(false)]
        public decimal TVA
        {
            get => _tva;
            set => SetPropertyValue(nameof(TVA), ref _tva, value);
        }

        private decimal _ttc;
        [XafDisplayName("TTC (coût réel ELTON)")]
        [ToolTip("Montant TTC facturé par la société d'intérim. C'est ce qu'ELTON paye pour cet intérimaire ce mois-là.")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        public decimal TTC
        {
            get => _ttc;
            set => SetPropertyValue(nameof(TTC), ref _ttc, value);
        }

        // ═══ Traçabilité import ══════════════════════════════════════════════

        private DateTime _dateImport;
        [XafDisplayName("Date d'import")]
        [ModelDefault("DisplayFormat", "{0:dd/MM/yyyy HH:mm}")]
        [VisibleInListView(false)]
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
        [VisibleInListView(false)]
        public string ImportePar
        {
            get => _importePar;
            set => SetPropertyValue(nameof(ImportePar), ref _importePar, value);
        }

        private ImportBulletinInterimBatch _batch;
        [XafDisplayName("Lot d'import")]
        [Association("Batch-Bulletins")]
        [VisibleInListView(false)]
        public ImportBulletinInterimBatch Batch
        {
            get => _batch;
            set => SetPropertyValue(nameof(Batch), ref _batch, value);
        }

        private BulletinInterimStatut _statut;
        [XafDisplayName("Statut import")]
        public BulletinInterimStatut Statut
        {
            get => _statut;
            set => SetPropertyValue(nameof(Statut), ref _statut, value);
        }

        // ═══ Indicateurs calculés (NonPersistent) ════════════════════════════

        /// <summary>Multiplicateur Brut imposable → TTC. Permet d'évaluer le coût total / brut.</summary>
        [NonPersistent]
        [XafDisplayName("Multiplicateur Brut→TTC")]
        public decimal MultiplicateurBrutTTC =>
            BrutImposable > 0 ? Math.Round(TTC / BrutImposable, 2) : 0m;

        /// <summary>Coût réel (= TTC, pour clarté dans les filtres et graphiques).</summary>
        [NonPersistent]
        [Browsable(false)]
        public decimal CoutReel => TTC;

        /// <summary>True si la ligne correspond à un Prestataire (hors fiche Intérimaire).</summary>
        [NonPersistent]
        [XafDisplayName("Prestataire ?")]
        [VisibleInListView(false)]
        public bool IsPrestataire => Statut == BulletinInterimStatut.Prestataire;

        // ═══ Affichage ═══════════════════════════════════════════════════════

        [NonPersistent]
        [Browsable(false)]
        public string DisplayName
        {
            get
            {
                var nom = Interimaire?.FullName ?? NomComplet ?? MatriculeOriginal ?? "(?)";
                return $"{Annee}-{Mois:D2} • {nom} • {TTC:N0} FCFA TTC";
            }
        }
    }
}
