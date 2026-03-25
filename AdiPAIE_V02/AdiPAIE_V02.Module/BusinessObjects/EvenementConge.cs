using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
// Event est la classe BaseImpl DevExpress qui implémente IEvent complètement
using DxEvent = DevExpress.Persistent.BaseImpl.Event;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    /// <summary>
    /// Événement calendrier généré depuis une CongeDemande.
    /// Implémente IEvent pour être affiché dans le SchedulerControl XAF.
    ///
    /// Les événements sont générés/synchronisés par PlanningCongeController
    /// à chaque accord ou annulation d'un congé.
    ///
    /// Couleurs par statut :
    ///   Accordée   → Vert  (label 6)
    ///   Soumise    → Bleu  (label 2)
    ///   En attente → Orange (label 7)
    ///   Annulée    → Gris  (label 0)
    ///   Férié      → Rouge (label 3)
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Planning congés")]
    [DefaultProperty(nameof(Subject))]
    [ImageName("BO_Event")]
    [NavigationItem("GRH - Congés")]
    [Appearance("CongeAccorde", TargetItems = "*",
        Criteria = "TypeEvenement = 0",
        FontColor = "#1B6C2A")]
    [Appearance("CongeEnAttente", TargetItems = "*",
        Criteria = "TypeEvenement = 1",
        FontColor = "#C55A11")]
    [Appearance("JourFerie", TargetItems = "*",
        Criteria = "TypeEvenement = 3",
        FontColor = "#C00000")]
    public class EvenementConge : DxEvent
    {
        public EvenementConge(Session session) : base(session) { }

        // ── Lien source ───────────────────────────────────────
        [Association("CongeDemande-EvenementConge")]
        [XafDisplayName("Demande de congé")]
        [ModelDefault("AllowEdit", "False")]
        public CongeDemande DemandeCongé
        {
            get => demandeCongé;
            set => SetPropertyValue(nameof(DemandeCongé), ref demandeCongé, value);
        }
        CongeDemande demandeCongé;

        // ── Type d'événement ──────────────────────────────────
        int typeEvenement;
        [XafDisplayName("Type")]
        [ModelDefault("AllowEdit", "False")]
        public int TypeEvenement
        {
            get => typeEvenement;
            set => SetPropertyValue(nameof(TypeEvenement), ref typeEvenement, value);
        }
        // 0 = Accordé, 1 = En attente/Soumis, 2 = Brouillon, 3 = Férié, 4 = Annulé

        // ── Salarié ───────────────────────────────────────────
        [Association("Salarie-EvenementsConge")]
        [XafDisplayName("Salarié")]
        [ModelDefault("AllowEdit", "False")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        string departementNom;
        [Size(120)]
        [XafDisplayName("Département")]
        [ModelDefault("AllowEdit", "False")]
        public string DepartementNom
        {
            get => departementNom;
            set => SetPropertyValue(nameof(DepartementNom), ref departementNom, value);
        }

        // ════════════════════════════════════════════════════════
        // IEvent — interface obligatoire pour le SchedulerControl
        // ════════════════════════════════════════════════════════

        // ── Factory ───────────────────────────────────────────
        /// <summary>
        /// Crée ou met à jour l'événement calendrier depuis une CongeDemande.
        /// </summary>
        public static EvenementConge CreerOuMettreAJour(
            DevExpress.ExpressApp.IObjectSpace os,
            CongeDemande demande)
        {
            if (demande?.Salarie == null) return null;

            // Chercher un événement existant
            var evt = os.GetObjectsQuery<EvenementConge>()
                .ToList()
                .FirstOrDefault(e => e.DemandeCongé?.Oid == demande.Oid);

            if (evt == null)
                evt = os.CreateObject<EvenementConge>();

            evt.DemandeCongé = demande;
            evt.Salarie = demande.Salarie;
            evt.DepartementNom = demande.Salarie.Departement?.Nom ?? "";
            evt.StartOn = demande.DateDebut.Date;
            evt.EndOn = demande.DateFin.Date.AddDays(1); // Exclusif dans Scheduler
            evt.AllDay = true;
            evt.Subject = $"{demande.Salarie.FullName} — {demande.Type?.Libelle ?? "Congé"}"
                               + $" ({demande.DureeJours:N0}j)";
            evt.Description = $"Du {demande.DateDebut:dd/MM/yyyy} au {demande.DateFin:dd/MM/yyyy}\n"
                               + $"Type : {demande.Type?.Libelle}\n"
                               + $"Statut : {demande.Statut}"
                               + (string.IsNullOrWhiteSpace(demande.Motif) ? "" : $"\nMotif : {demande.Motif}");

            // Label couleur selon statut
            (evt.Label, evt.TypeEvenement) = demande.Statut switch
            {
                CongeStatut.Accordee => (6, 0), // Vert
                CongeStatut.Soumise => (2, 1), // Bleu
                CongeStatut.EnAttenteN1 or
                CongeStatut.EnAttenteN2 => (7, 1), // Orange
                CongeStatut.Annulee => (0, 4), // Gris
                CongeStatut.Refusee => (3, 4), // Rouge
                _ => (2, 2), // Bleu (brouillon)
            };

            evt.Status = demande.Statut == CongeStatut.Accordee ? 2 : 0;

            return evt;
        }

        /// <summary>
        /// Crée un événement pour un jour férié.
        /// </summary>
        public static EvenementConge CreerJourFerie(
            DevExpress.ExpressApp.IObjectSpace os,
            JourFerie ferie)
        {
            // Vérifier si déjà créé
            var existant = os.GetObjectsQuery<EvenementConge>()
                .ToList()
                .FirstOrDefault(e => e.TypeEvenement == 3
                                  && e.StartOn.Date == ferie.Date.Date);
            if (existant != null) return existant;

            var evt = os.CreateObject<EvenementConge>();
            evt.TypeEvenement = 3;
            evt.Subject = $"🔴 {ferie.Libelle}";
            evt.Description = $"Jour férié : {ferie.Libelle}";
            evt.StartOn = ferie.Date.Date;
            evt.EndOn = ferie.Date.Date.AddDays(1);
            evt.AllDay = true;
            evt.Label = 3; // Rouge
            evt.Status = 2;
            return evt;
        }
    }
}
