using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [XafDisplayName("Heure supplémentaire")]
    [DefaultProperty(nameof(DisplayName))]
    public class HeureSupplementaire : BaseObject
    {
        public HeureSupplementaire(Session session) : base(session) { }

        // ── Lien vers le bulletin ──
        [Association("Bulletin-HeuresSupplementaires"), RuleRequiredField]
        public Bulletin Bulletin
        {
            get => bulletin;
            set => SetPropertyValue(nameof(Bulletin), ref bulletin, value);
        }
        private Bulletin bulletin;

        // ── Type d'heure supplémentaire ──
        [XafDisplayName("Type")]
        public TypeHeureSupplementaire TypeHS
        {
            get => typeHS;
            set
            {
                if (SetPropertyValue(nameof(TypeHS), ref typeHS, value))
                    RecalculerMontants();
            }
        }
        private TypeHeureSupplementaire typeHS;

        // ── Nombre d'heures ──
        [XafDisplayName("Nombre d'heures")]
        [DbType("decimal(18,2)")]
        [ModelDefault("DisplayFormat", "N2"), ModelDefault("EditMask", "N2")]
        [RuleRange(0.25, 744, CustomMessageTemplate = "Le nombre d'heures doit être entre 0.25 et 744.")]
        public decimal NombreHeures
        {
            get => nombreHeures;
            set
            {
                if (SetPropertyValue(nameof(NombreHeures), ref nombreHeures, value))
                    RecalculerMontants();
            }
        }
        private decimal nombreHeures;

        // ── Taux de majoration (%) — calculé automatiquement, modifiable ──
        [XafDisplayName("Taux majoration (%)")]
        [DbType("decimal(18,2)")]
        [ModelDefault("DisplayFormat", "N2"), ModelDefault("EditMask", "N2")]
        public decimal TauxMajoration
        {
            get => tauxMajoration;
            set
            {
                if (SetPropertyValue(nameof(TauxMajoration), ref tauxMajoration, value))
                    RecalculerMontants();
            }
        }
        private decimal tauxMajoration;

        // ── Taux horaire de base (calculé depuis le salaire du bulletin) ──
        [XafDisplayName("Taux horaire base")]
        [DbType("decimal(18,2)")]
        [ModelDefault("DisplayFormat", "N2"), ModelDefault("EditMask", "N2")]
        [ModelDefault("AllowEdit", "False")]
        public decimal TauxHoraireBase
        {
            get => tauxHoraireBase;
            set => SetPropertyValue(nameof(TauxHoraireBase), ref tauxHoraireBase, value);
        }
        private decimal tauxHoraireBase;

        // ── Montant de base (heures × taux horaire) ──
        [XafDisplayName("Montant base")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal MontantBase
        {
            get => montantBase;
            set => SetPropertyValue(nameof(MontantBase), ref montantBase, value);
        }
        private decimal montantBase;

        // ── Montant majoration ──
        [XafDisplayName("Montant majoration")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal MontantMajoration
        {
            get => montantMajoration;
            set => SetPropertyValue(nameof(MontantMajoration), ref montantMajoration, value);
        }
        private decimal montantMajoration;

        // ── Montant total (base + majoration) ──
        [XafDisplayName("Montant total")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal MontantTotal
        {
            get => montantTotal;
            set => SetPropertyValue(nameof(MontantTotal), ref montantTotal, value);
        }
        private decimal montantTotal;

        // ── Commentaire (optionnel) ──
        [Size(200)]
        [XafDisplayName("Commentaire")]
        public string Commentaire
        {
            get => commentaire;
            set => SetPropertyValue(nameof(Commentaire), ref commentaire, value?.Trim());
        }
        private string commentaire;

        // ── Display ──
        [NonPersistent]
        public string DisplayName => $"{TypeHS} — {NombreHeures:N2}h";

        // ── Logique métier ──

        /// <summary>
        /// Renvoie le taux de majoration légal sénégalais par défaut pour le type donné.
        /// Ces taux peuvent être surchargés dans ParametresPaie.
        /// </summary>
        public static decimal GetTauxLegalDefaut(TypeHeureSupplementaire type) => type switch
        {
            TypeHeureSupplementaire.JourOuvrable => 15m,
            TypeHeureSupplementaire.Nuit => 40m,
            TypeHeureSupplementaire.DimancheFerie => 60m,
            TypeHeureSupplementaire.NuitDimancheFerie => 100m,
            _ => 0m
        };

        /// <summary>
        /// Initialise le taux de majoration depuis les paramètres de paie ou les taux légaux par défaut.
        /// </summary>
        public void InitialiserTauxDepuisParametrage()
        {
            var prm = ParametresPaie.TryGet(Session);
            TauxMajoration = prm != null
                ? prm.GetTauxHS(TypeHS)
                : GetTauxLegalDefaut(TypeHS);
        }

        /// <summary>
        /// Calcule le taux horaire de base : SalaireBase mensuel / 173,33h (40h × 52 semaines / 12 mois).
        /// C'est la base légale au Sénégal pour une durée hebdomadaire de 40 heures.
        /// </summary>
        public void CalculerTauxHoraireBase()
        {
            const decimal HEURES_MENSUELLES_LEGALES = 173.33m;
            var salaireBase = Bulletin?.Salarie?.SalaireBase ?? 0m;
            TauxHoraireBase = salaireBase > 0
                ? Math.Round(salaireBase / HEURES_MENSUELLES_LEGALES, 2, MidpointRounding.AwayFromZero)
                : 0m;
        }

        /// <summary>
        /// Recalcule tous les montants dérivés.
        /// </summary>
        public void RecalculerMontants()
        {
            if (TauxHoraireBase <= 0)
                CalculerTauxHoraireBase();

            MontantBase = Math.Round(NombreHeures * TauxHoraireBase, 0, MidpointRounding.AwayFromZero);
            MontantMajoration = Math.Round(MontantBase * TauxMajoration / 100m, 0, MidpointRounding.AwayFromZero);
            MontantTotal = MontantBase + MontantMajoration;
        }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            TypeHS = TypeHeureSupplementaire.JourOuvrable;
            TauxMajoration = GetTauxLegalDefaut(TypeHeureSupplementaire.JourOuvrable);
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted)
            {
                RecalculerMontants();
                if (Bulletin == null)
                    throw new UserFriendlyException("L'heure supplémentaire doit être rattachée à un bulletin.");
            }
        }
    }
}
