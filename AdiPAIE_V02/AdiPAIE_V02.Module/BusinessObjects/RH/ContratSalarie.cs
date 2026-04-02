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
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Contrat de travail salarié — CDI, CDD, Stage.
    ///
    /// Lié à la fiche Salarie.
    /// Génère un document Word via ContratTemplateService
    /// en utilisant le template uploadé dans ParametresPaie.
    ///
    /// La génération du PDF est déclenchée par ContratSalarieController.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Contrat de travail")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Contract")]
   
    [Appearance("Contrat_Actif", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+ContratSalarieStatut,Actif#",
        FontColor = "#1B6C2A")]
    [Appearance("Contrat_Resilie", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+ContratSalarieStatut,Resilie#",
        FontColor = "Red", FontStyle = DevExpress.Drawing.DXFontStyle.Strikeout)]
    [Appearance("Contrat_Expire", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+ContratSalarieStatut,Expire#",
        FontColor = "Gray", FontStyle = DevExpress.Drawing.DXFontStyle.Italic)]
    [RuleCriteria("Contrat_DateFin_CDD", DefaultContexts.Save,
        "TypeContrat != ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TypeContrat,CDI# AND DateFin Is Null",
        CustomMessageTemplate = "La date de fin est obligatoire pour un CDD ou un stage.", InvertResult = true)]
    public class ContratSalarie : BaseObject
    {
        public ContratSalarie(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = ContratSalarieStatut.Brouillon;
            TypeContrat = Domain.DomainEnums.TypeContrat.CDI;
            DateDebut = DateTime.Today;
            DateDocument = DateTime.Today;
            VilleFait = "Dakar";
            try { CreePar = SecuritySystem.CurrentUserName; } catch { }
            Reference = $"CTR-{DateTime.Today:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
        }

        // ── Référence ─────────────────────────────────────────────────
        [Size(30)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Référence")]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value);
        }
        string reference;

        // ── Salarié ───────────────────────────────────────────────────
        [RuleRequiredField]
        [Association("Salarie-Contrats")]
        [XafDisplayName("Salarié")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        // ── Type et statut ────────────────────────────────────────────
        [XafDisplayName("Type de contrat")]
        [ImmediatePostData]
        public TypeContrat? TypeContrat
        {
            get => typeContrat;
            set => SetPropertyValue(nameof(TypeContrat), ref typeContrat, value);
        }
        TypeContrat? typeContrat;

        [XafDisplayName("Statut")]
        [ModelDefault("AllowEdit", "False")]
        public ContratSalarieStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }
        ContratSalarieStatut statut;

        // ── Dates ─────────────────────────────────────────────────────
        [RuleRequiredField]
        [XafDisplayName("Date de début")]
        public DateTime DateDebut
        {
            get => dateDebut;
            set => SetPropertyValue(nameof(DateDebut), ref dateDebut, value);
        }
        DateTime dateDebut;

        [Appearance("DateFin_Hidden",
            Criteria = "TypeContrat = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TypeContrat,CDI#",
            Visibility = ViewItemVisibility.Hide)]
        [XafDisplayName("Date de fin")]
        public DateTime? DateFin
        {
            get => dateFin;
            set => SetPropertyValue(nameof(DateFin), ref dateFin, value);
        }
        DateTime? dateFin;

        // ── Période d'essai ───────────────────────────────────────────
        [XafDisplayName("Période d'essai")]
        public bool PeriodeEssai
        {
            get => periodeEssai;
            set => SetPropertyValue(nameof(PeriodeEssai), ref periodeEssai, value);
        }
        bool periodeEssai;

        [Appearance("DureePE_Hidden",
            Criteria = "PeriodeEssai = false",
            Visibility = ViewItemVisibility.Hide)]
        [XafDisplayName("Durée période d'essai")]
        [Size(100)]
        public string DureePeriodeEssai
        {
            get => dureePeriodeEssai;
            set => SetPropertyValue(nameof(DureePeriodeEssai), ref dureePeriodeEssai, value?.Trim());
        }
        string dureePeriodeEssai;

        // ── Motif CDD ─────────────────────────────────────────────────
        [Appearance("MotifCDD_Hidden",
            Criteria = "TypeContrat = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TypeContrat,CDI#",
            Visibility = ViewItemVisibility.Hide)]
        [XafDisplayName("Motif du CDD")]
        public MotifCDD? MotifCDD
        {
            get => motifCDD;
            set => SetPropertyValue(nameof(MotifCDD), ref motifCDD, value);
        }
        MotifCDD? motifCDD;

        [Appearance("MotifCDDDetail_Hidden",
            Criteria = "TypeContrat = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TypeContrat,CDI#",
            Visibility = ViewItemVisibility.Hide)]
        [Size(300)]
        [XafDisplayName("Précision motif CDD")]
        public string MotifCDDDetail
        {
            get => motifCDDDetail;
            set => SetPropertyValue(nameof(MotifCDDDetail), ref motifCDDDetail, value?.Trim());
        }
        string motifCDDDetail;

        // ── Rémunération (snapshot à la signature) ────────────────────
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        [XafDisplayName("Salaire de base (FCFA)")]
        public decimal SalaireBase
        {
            get => salaireBase;
            set => SetPropertyValue(nameof(SalaireBase), ref salaireBase, value);
        }
        decimal salaireBase;

        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        [XafDisplayName("Indemnité de logement (FCFA)")]
        public decimal IndemniteLogement
        {
            get => indemniteLogement;
            set => SetPropertyValue(nameof(IndemniteLogement), ref indemniteLogement, value);
        }
        decimal indemniteLogement;

        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        [XafDisplayName("Prime de transport (FCFA)")]
        public decimal PrimeTransport
        {
            get => primeTransport;
            set => SetPropertyValue(nameof(PrimeTransport), ref primeTransport, value);
        }
        decimal primeTransport;

        [NonPersistent]
        [ModelDefault("DisplayFormat", "N0")]
        [XafDisplayName("Total brut (FCFA)")]
        public decimal TotalBrut => SalaireBase + IndemniteLogement + PrimeTransport;

        // ── Lieu et date document ─────────────────────────────────────
        [Size(100)]
        [XafDisplayName("Fait à")]
        public string VilleFait
        {
            get => villeFait;
            set => SetPropertyValue(nameof(VilleFait), ref villeFait, value?.Trim());
        }
        string villeFait;

        [XafDisplayName("Date du document")]
        public DateTime DateDocument
        {
            get => dateDocument;
            set => SetPropertyValue(nameof(DateDocument), ref dateDocument, value);
        }
        DateTime dateDocument;

        // ── Document généré ───────────────────────────────────────────
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [XafDisplayName("Document généré (.pdf)")]
        public FileData DocumentPdf
        {
            get => documentPdf;
            set => SetPropertyValue(nameof(DocumentPdf), ref documentPdf, value);
        }
        FileData documentPdf;

        // ── Traçabilité ───────────────────────────────────────────────
        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Créé par")]
        public string CreePar
        {
            get => creePar;
            set => SetPropertyValue(nameof(CreePar), ref creePar, value);
        }
        string creePar;

        [Size(500)]
        [XafDisplayName("Observations")]
        public string Observations
        {
            get => observations;
            set => SetPropertyValue(nameof(Observations), ref observations, value);
        }
        string observations;

        // ── Propriétés calculées ──────────────────────────────────────
        [NonPersistent]
        public string DisplayName =>
            $"{Salarie?.FullName ?? "—"} — {TypeContrat} ({DateDebut:MM/yyyy}" +
            (DateFin.HasValue ? $" → {DateFin:MM/yyyy}" : "") + ")";

        [NonPersistent]
        [XafDisplayName("Durée (mois)")]
        public int? DureeMois =>
            DateFin.HasValue
                ? (int?)((DateFin.Value - DateDebut).Days / 30)
                : null;

        public override string ToString() => DisplayName;
    }
}
