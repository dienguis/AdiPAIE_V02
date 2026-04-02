using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions, XafDisplayName("Type de congé")]
    [DefaultProperty(nameof(Libelle))]
   // [NavigationItem("GRH - Administration")]
    public class CongeType : BaseObject
    {
        public CongeType(Session s) : base(s) { }

        [RuleRequiredField, Size(20), Indexed(Unique = true)]
        public string Code { get => code; set => SetPropertyValue(nameof(Code), ref code, value?.Trim()); }
        string code;

        [RuleRequiredField, Size(120)]
        public string Libelle { get => lib; set => SetPropertyValue(nameof(Libelle), ref lib, value?.Trim()); }
        string lib;

        public CongeImpactSalaire ImpactSalaire { get => impact; set => SetPropertyValue(nameof(ImpactSalaire), ref impact, value); }
        CongeImpactSalaire impact = CongeImpactSalaire.Paye;

        // Pour ImpactSalaire = Partiel (maintien de salaire, ex: 50%)
      //  [ModelDefault("DisplayFormat", "P0"), ModelDefault("EditMask", "P0")]
   //     public decimal? TauxMaintienSalaire { get => maintien; set => SetPropertyValue(nameof(TauxMaintienSalaire), ref maintien, value); }
    //    decimal? maintien;

        public bool CompteEnJoursOuvrables { get => ouvrables; set => SetPropertyValue(nameof(CompteEnJoursOuvrables), ref ouvrables, value); }
        bool ouvrables = true;

        [Association("CongeType-Soldes")]
        [XafDisplayName("Soldes")]
        [Browsable(false)]
        public XPCollection<SoldeConge> Soldes
          => GetCollection<SoldeConge>(nameof(Soldes));

        // ── Famille ───────────────────────────────────────────
        [XafDisplayName("Famille")]
        public FamilleConge Famille
        {
            get => famille;
            set => SetPropertyValue(nameof(Famille), ref famille, value);
        }
        FamilleConge famille;

        // ── Acquisition ──────────────────────────────────────
        /// <summary>Jours acquis par mois travaillé (ex: 2,5 pour congé annuel).</summary>
        [XafDisplayName("Acquisition / mois (jours)")]
        [ModelDefault("DisplayFormat", "N2")]
        [ModelDefault("EditMask", "N2")]
        public decimal AcquisitionMensuelle
        {
            get => acquisitionMensuelle;
            set => SetPropertyValue(nameof(AcquisitionMensuelle), ref acquisitionMensuelle, value);
        }
        decimal acquisitionMensuelle;

        /// <summary>Nombre de jours légaux accordés (ex: 30j maternité, 3j événement).</summary>
        [XafDisplayName("Durée légale (jours)")]
        public int DureeLegaleJours
        {
            get => dureeLegaleJours;
            set => SetPropertyValue(nameof(DureeLegaleJours), ref dureeLegaleJours, value);
        }
        int dureeLegaleJours;

        // ── Report ────────────────────────────────────────────
        /// <summary>Nombre maximum de jours reportables sur N+1 (0 = pas de report).</summary>
        [XafDisplayName("Plafond report (jours)")]
        public int PlafondReportJours
        {
            get => plafondReportJours;
            set => SetPropertyValue(nameof(PlafondReportJours), ref plafondReportJours, value);
        }
        int plafondReportJours;

        // ── Règles ────────────────────────────────────────────
        /// <summary>Justificatif obligatoire à fournir (maladie, événement familial).</summary>
        [XafDisplayName("Justificatif obligatoire")]
        public bool JustificatifObligatoire
        {
            get => justificatifObligatoire;
            set => SetPropertyValue(nameof(JustificatifObligatoire), ref justificatifObligatoire, value);
        }
        bool justificatifObligatoire;

        /// <summary>Ancienneté minimale requise (en mois). 0 = pas de condition.</summary>
        [XafDisplayName("Ancienneté minimale (mois)")]
        public int AncienneteMinMois
        {
            get => ancienneteMinMois;
            set => SetPropertyValue(nameof(AncienneteMinMois), ref ancienneteMinMois, value);
        }
        int ancienneteMinMois;

        /// <summary>Alimentation automatique du solde (faux pour maladie, événements).</summary>
        [XafDisplayName("Alimentation solde auto")]
        public bool AlimenteSolde
        {
            get => alimenteSolde;
            set => SetPropertyValue(nameof(AlimenteSolde), ref alimenteSolde, value);
        }
        bool alimenteSolde = true;

    }

    //-----------------Jours ferie-----------------------


    [DefaultClassOptions, XafDisplayName("Jour férié")]
    //[NavigationItem("GRH - Administration")]
    public class JourFerie : BaseObject
    {
        public JourFerie(Session s) : base(s) { }

        public DateTime Date { get => d; set => SetPropertyValue(nameof(Date), ref d, value.Date); }
        DateTime d;

        [Size(120)]
        public string Libelle { get => lib; set => SetPropertyValue(nameof(Libelle), ref lib, value?.Trim()); }
        string lib;
    }

}
