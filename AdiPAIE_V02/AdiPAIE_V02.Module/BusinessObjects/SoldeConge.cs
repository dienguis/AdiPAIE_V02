using DevExpress.Drawing;
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

namespace AdiPAIE_V02.Module.BusinessObjects
{
    /// <summary>
    /// Solde de congés d'un salarié pour un type de congé et une année donnée.
    ///
    /// SoldeDisponible = JoursAcquis + JoursReportes - JoursPris - JoursEnAttente
    ///
    /// Alimenté par :
    ///   - SoldeCongeCalculService.AcquirirMensuel() — chaque mois
    ///   - SoldeCongeCalculService.Reporter()        — clôture exercice N vers N+1
    ///   - CongeWorkflowController                   — à l'accord d'un congé
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Solde de congés")]
    [DefaultProperty(nameof(DisplaySolde))]
    [ImageName("BO_List")]
    //[NavigationItem("GRH - Congés")]
    [Appearance("SoldeNegatif", TargetItems = "*",
        Criteria = "SoldeDisponible < 0",
        FontColor = "Red", FontStyle = DXFontStyle.Bold)]
    [Appearance("SoldeAlerte", TargetItems = "*",
        Criteria = "SoldeDisponible >= 0 AND SoldeDisponible <= 3",
        FontColor = "OrangeRed")]
    public class SoldeConge : BaseObject
    {
        public SoldeConge(Session session) : base(session) { }

        // ── Clés ─────────────────────────────────────────────
        [Association("Salarie-SoldesConge")]
        [RuleRequiredField]
        [XafDisplayName("Salarié")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        [Association("CongeType-Soldes")]
        [RuleRequiredField]
        [XafDisplayName("Type de congé")]
        public CongeType TypeConge
        {
            get => typeConge;
            set => SetPropertyValue(nameof(TypeConge), ref typeConge, value);
        }
        CongeType typeConge;

        int annee;
        [XafDisplayName("Année")]
        public int Annee
        {
            get => annee;
            set => SetPropertyValue(nameof(Annee), ref annee, value);
        }

        // ── Compteurs ─────────────────────────────────────────
        decimal joursAcquis;
        [XafDisplayName("Jours acquis")]
        [ModelDefault("DisplayFormat", "N2")]
        [ModelDefault("AllowEdit", "False")]
        public decimal JoursAcquis
        {
            get => joursAcquis;
            set => SetPropertyValue(nameof(JoursAcquis), ref joursAcquis, value);
        }

        decimal joursReportes;
        [XafDisplayName("Jours reportés (N-1)")]
        [ModelDefault("DisplayFormat", "N2")]
        [ModelDefault("AllowEdit", "False")]
        public decimal JoursReportes
        {
            get => joursReportes;
            set => SetPropertyValue(nameof(JoursReportes), ref joursReportes, value);
        }

        decimal joursPris;
        [XafDisplayName("Jours pris")]
        [ModelDefault("DisplayFormat", "N2")]
        [ModelDefault("AllowEdit", "False")]
        public decimal JoursPris
        {
            get => joursPris;
            set => SetPropertyValue(nameof(JoursPris), ref joursPris, value);
        }

        decimal joursEnAttente;
        [XafDisplayName("Jours en attente (demandes en cours)")]
        [ModelDefault("DisplayFormat", "N2")]
        [ModelDefault("AllowEdit", "False")]
        public decimal JoursEnAttente
        {
            get => joursEnAttente;
            set => SetPropertyValue(nameof(JoursEnAttente), ref joursEnAttente, value);
        }

        // ── Solde calculé ─────────────────────────────────────
        [NonPersistent]
        [XafDisplayName("Solde disponible")]
        [ModelDefault("DisplayFormat", "N2")]
        public decimal SoldeDisponible =>
            JoursAcquis + JoursReportes - JoursPris;

        [NonPersistent]
        [XafDisplayName("Solde réel (hors attente)")]
        [ModelDefault("DisplayFormat", "N2")]
        public decimal SoldeReel =>
            JoursAcquis + JoursReportes - JoursPris - JoursEnAttente;

        // ── Dernier mouvement ─────────────────────────────────
        DateTime? dateDerniereAcquisition;
        [XafDisplayName("Dernière acquisition")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateDerniereAcquisition
        {
            get => dateDerniereAcquisition;
            set => SetPropertyValue(nameof(DateDerniereAcquisition),
                ref dateDerniereAcquisition, value);
        }

        int moisDerniereAcquisition;
        [XafDisplayName("Dernier mois acquis")]
        [ModelDefault("AllowEdit", "False")]
        public int MoisDerniereAcquisition
        {
            get => moisDerniereAcquisition;
            set => SetPropertyValue(nameof(MoisDerniereAcquisition),
                ref moisDerniereAcquisition, value);
        }

        // ── Statut ───────────────────────────────────────────
        SoldeCongeStatut statut;
        [XafDisplayName("Statut")]
        [ModelDefault("AllowEdit", "False")]
        public SoldeCongeStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        // ═════════════════════════════════════════════════════════════════
        //  V1.8 — Traçabilité initialisation manuelle (mise en prod)
        //
        //  À la mise en production, le RH saisit le solde cumulé connu de
        //  chaque salarié à une date donnée (qui peut varier d'un salarié
        //  à l'autre selon le fichier source Excel).
        //
        //  Les 3 champs ci-dessous permettent :
        //   - de tracer la DATE à laquelle le solde a été constaté
        //   - de marquer les soldes "à vérifier" (16 lignes bleues du
        //     fichier Excel ELTON pour lesquels le RH ne connaît pas
        //     la valeur)
        //   - de tracer la source (Excel, ancien système, saisie manuelle)
        // ═════════════════════════════════════════════════════════════════

        DateTime? soldeArreteAu;
        [XafDisplayName("Solde arrêté au")]
        [ToolTip("Date à laquelle le solde initial a été constaté (lecture " +
                 "directe du fichier Excel RH ou de l'ancien système). " +
                 "Permet de connaître la date de référence du solde reporté.")]
        public DateTime? SoldeArreteAu
        {
            get => soldeArreteAu;
            set => SetPropertyValue(nameof(SoldeArreteAu), ref soldeArreteAu, value);
        }

        bool soldeAVerifier;
        [XafDisplayName("Solde à vérifier")]
        [ToolTip("Cocher si la valeur saisie est incertaine et doit être " +
                 "validée ultérieurement par le RH (cas des lignes en " +
                 "bleu du fichier Excel ELTON pour lesquelles aucun " +
                 "solde fiable n'est connu).")]
        public bool SoldeAVerifier
        {
            get => soldeAVerifier;
            set => SetPropertyValue(nameof(SoldeAVerifier), ref soldeAVerifier, value);
        }

        string sourceInitialisation;
        [Size(120)]
        [XafDisplayName("Source initialisation")]
        [ToolTip("Origine de la donnée initiale (ex: 'Excel Planning Congés 2026', " +
                 "'Ancien système BultinMensuel', 'Saisie manuelle RH').")]
        public string SourceInitialisation
        {
            get => sourceInitialisation;
            set => SetPropertyValue(nameof(SourceInitialisation), ref sourceInitialisation, value?.Trim());
        }

        // ── Collection mouvements ─────────────────────────────
        [Association("SoldeConge-Mouvements"), Aggregated]
        [XafDisplayName("Mouvements")]
        public XPCollection<MouvementSolde> Mouvements
            => GetCollection<MouvementSolde>(nameof(Mouvements));

        // ── Affichage ─────────────────────────────────────────
        [NonPersistent]
        public string DisplaySolde =>
            $"{Salarie?.LastName} — {TypeConge?.Libelle} {Annee} " +
            $"(Dispo : {SoldeDisponible:N1}j)";

        // ── Méthodes ─────────────────────────────────────────
        /// <summary>Débite le solde lors de l'accord d'un congé.</summary>
        public void Debiter(decimal jours, string reference = null)
        {
            JoursPris += jours;
            // Réduire les jours en attente correspondants
            JoursEnAttente = Math.Max(0, JoursEnAttente - jours);
        }

        /// <summary>Re-crédite le solde lors de l'annulation d'un congé accordé.</summary>
        public void Recréditer(decimal jours)
        {
            JoursPris = Math.Max(0, JoursPris - jours);
        }

        /// <summary>Réserve des jours (demande en cours, pas encore accordée).</summary>
        public void Reserver(decimal jours)
        {
            JoursEnAttente += jours;
        }

        /// <summary>Libère la réservation (demande annulée avant accord).</summary>
        public void LibererReservation(decimal jours)
        {
            JoursEnAttente = Math.Max(0, JoursEnAttente - jours);
        }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Annee = DateTime.Today.Year;
            Statut = SoldeCongeStatut.Actif;
        }
    }
}
