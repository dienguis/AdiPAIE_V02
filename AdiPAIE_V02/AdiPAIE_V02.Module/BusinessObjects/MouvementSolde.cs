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
    /// <summary>
    /// Journal des mouvements de solde de congés.
    /// Chaque acquisition, prise, report ou ajustement crée une ligne.
    /// Immuable après création (audit trail).
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Mouvement de solde")]
    [DefaultProperty(nameof(DisplayMouvement))]
    [ImageName("BO_Audit")]
    //[NavigationItem("GRH - Congés")]
    public class MouvementSolde : BaseObject
    {
        public MouvementSolde(Session session) : base(session) { }

        // ── Rattachement ──────────────────────────────────────
        [Association("SoldeConge-Mouvements")]
        [RuleRequiredField]
        [XafDisplayName("Solde concerné")]
        [ModelDefault("AllowEdit", "False")]
        public SoldeConge Solde
        {
            get => solde;
            set => SetPropertyValue(nameof(Solde), ref solde, value);
        }
        SoldeConge solde;

        // ── Type et date ──────────────────────────────────────
        MouvementSoldeType typeMouvement;
        [XafDisplayName("Type")]
        [ModelDefault("AllowEdit", "False")]
        public MouvementSoldeType TypeMouvement
        {
            get => typeMouvement;
            set => SetPropertyValue(nameof(TypeMouvement), ref typeMouvement, value);
        }

        DateTime dateMouvement;
        [XafDisplayName("Date")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime DateMouvement
        {
            get => dateMouvement;
            set => SetPropertyValue(nameof(DateMouvement), ref dateMouvement, value);
        }

        // ── Quantité ──────────────────────────────────────────
        decimal quantite;
        [XafDisplayName("Quantité (jours)")]
        [ModelDefault("DisplayFormat", "N2")]
        [ModelDefault("AllowEdit", "False")]
        public decimal Quantite
        {
            get => quantite;
            set => SetPropertyValue(nameof(Quantite), ref quantite, value);
        }

        // ── Soldes avant / après ──────────────────────────────
        decimal soldeAvant;
        [XafDisplayName("Solde avant")]
        [ModelDefault("DisplayFormat", "N2")]
        [ModelDefault("AllowEdit", "False")]
        public decimal SoldeAvant
        {
            get => soldeAvant;
            set => SetPropertyValue(nameof(SoldeAvant), ref soldeAvant, value);
        }

        decimal soldeApres;
        [XafDisplayName("Solde après")]
        [ModelDefault("DisplayFormat", "N2")]
        [ModelDefault("AllowEdit", "False")]
        public decimal SoldeApres
        {
            get => soldeApres;
            set => SetPropertyValue(nameof(SoldeApres), ref soldeApres, value);
        }

        // ── Référence et commentaire ──────────────────────────
        string reference;
        [Size(100)]
        [XafDisplayName("Référence")]
        [ModelDefault("AllowEdit", "False")]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value?.Trim());
        }

        string commentaire;
        [Size(500)]
        [XafDisplayName("Commentaire")]
        [ModelDefault("AllowEdit", "False")]
        public string Commentaire
        {
            get => commentaire;
            set => SetPropertyValue(nameof(Commentaire), ref commentaire, value?.Trim());
        }

        string creePar;
        [Size(100)]
        [XafDisplayName("Créé par")]
        [ModelDefault("AllowEdit", "False")]
        public string CreePar
        {
            get => creePar;
            set => SetPropertyValue(nameof(CreePar), ref creePar, value);
        }

        // ── Période concernée (pour acquisition mensuelle) ────
        int? moisConcerne;
        [XafDisplayName("Mois concerné")]
        [ModelDefault("AllowEdit", "False")]
        public int? MoisConcerne
        {
            get => moisConcerne;
            set => SetPropertyValue(nameof(MoisConcerne), ref moisConcerne, value);
        }

        int? anneeConcernee;
        [XafDisplayName("Année concernée")]
        [ModelDefault("AllowEdit", "False")]
        public int? AnneeConcernee
        {
            get => anneeConcernee;
            set => SetPropertyValue(nameof(AnneeConcernee), ref anneeConcernee, value);
        }

        // ── Affichage ─────────────────────────────────────────
        [NonPersistent]
        public string DisplayMouvement =>
            $"{DateMouvement:dd/MM/yyyy} — {TypeMouvement} — {Quantite:+0.00;-0.00} j";

        // ── Cycle de vie ──────────────────────────────────────
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateMouvement = DateTime.Now;
            try { CreePar = SecuritySystem.CurrentUserName; } catch { }
        }

        // ── Factory ───────────────────────────────────────────
        /// <summary>
        /// Crée un mouvement et met à jour le solde en une seule opération.
        /// </summary>
        public static MouvementSolde Creer(
            DevExpress.ExpressApp.IObjectSpace os,
            SoldeConge solde,
            MouvementSoldeType type,
            decimal quantite,
            string reference = null,
            string commentaire = null,
            int? mois = null,
            int? annee = null)
        {
            var m = os.CreateObject<MouvementSolde>();
            m.Solde = solde;
            m.TypeMouvement = type;
            m.Quantite = quantite;
            m.Reference = reference;
            m.Commentaire = commentaire;
            m.MoisConcerne = mois;
            m.AnneeConcernee = annee;
            m.SoldeAvant = solde.SoldeDisponible;

            // Appliquer le mouvement
            switch (type)
            {
                case MouvementSoldeType.AcquisitionMensuelle:
                case MouvementSoldeType.Report:
                case MouvementSoldeType.Initialisation:
                    if (type == MouvementSoldeType.Report)
                        solde.JoursReportes += quantite;
                    else
                        solde.JoursAcquis += quantite;
                    break;

                case MouvementSoldeType.PriseCongé:
                    solde.JoursPris += quantite;
                    solde.JoursEnAttente = Math.Max(0, solde.JoursEnAttente - quantite);
                    break;

                case MouvementSoldeType.AnnulationCongé:
                    solde.JoursPris = Math.Max(0, solde.JoursPris - quantite);
                    break;

                case MouvementSoldeType.AjustementManuel:
                    // Quantité peut être négative
                    solde.JoursAcquis += quantite;
                    break;
            }

            m.SoldeApres = solde.SoldeDisponible;
            return m;
        }
    }
}
