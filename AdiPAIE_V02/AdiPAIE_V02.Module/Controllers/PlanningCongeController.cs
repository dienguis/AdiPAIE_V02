using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Controller de synchronisation et de filtrage du planning des congés.
    ///
    /// Deux rôles :
    ///   1. Synchroniser les EvenementConge depuis les CongeDemande
    ///      (appelé à l'accord, au refus, à l'annulation d'un congé)
    ///   2. Filtrer la vue Scheduler (département, statuts, jours fériés)
    ///
    /// Utilisation :
    ///   - Action manuelle "Synchroniser le planning" sur la ListView EvenementConge
    ///   - Appels automatiques depuis CongeWorkflowController + CongeAccordController
    /// </summary>
    public class PlanningCongeController
        : ObjectViewController<ListView, EvenementConge>
    {
        readonly SimpleAction synchroniserAction;
        readonly SimpleAction filtrerAccordesAction;
        readonly SimpleAction filtrerTousAction;
        readonly SingleChoiceAction filtrerDeptAction;

        public PlanningCongeController()
        {
            // ── Synchroniser tout ─────────────────────────────
            synchroniserAction = new SimpleAction(this,
                "Planning_Synchroniser", PredefinedCategory.View)
            {
                Caption = "Synchroniser",
                ImageName = "Action_Refresh",
                ToolTip = "Régénère tous les événements depuis les demandes de congé.",
                ConfirmationMessage =
                    "Régénérer tous les événements du planning ?\n" +
                    "Les événements existants seront mis à jour."
            };
            synchroniserAction.Execute += SynchroniserAction_Execute;

            // ── Filtre : Accordés seulement ───────────────────
            filtrerAccordesAction = new SimpleAction(this,
                "Planning_FiltrerAccordes", PredefinedCategory.View)
            {
                Caption = "Congés accordés",
                ImageName = "Action_Approve",
                ToolTip = "Affiche uniquement les congés accordés."
            };
            filtrerAccordesAction.Execute += (s, e) =>
            {
                View.CollectionSource.Criteria["statut"] =
                    CriteriaOperator.Parse("TypeEvenement = 0 OR TypeEvenement = 3");
                View.Refresh();
            };

            // ── Filtre : Tous les congés ──────────────────────
            filtrerTousAction = new SimpleAction(this,
                "Planning_FiltrerTous", PredefinedCategory.View)
            {
                Caption = "Tous les congés",
                ImageName = "Action_Clear",
                ToolTip = "Affiche tous les congés (tous statuts)."
            };
            filtrerTousAction.Execute += (s, e) =>
            {
                View.CollectionSource.Criteria.Remove("statut");
                View.CollectionSource.Criteria.Remove("dept");
                View.Refresh();
            };

            // ── Filtre : Par département ──────────────────────
            filtrerDeptAction = new SingleChoiceAction(this,
                "Planning_FiltrerDept", PredefinedCategory.View)
            {
                Caption = "Filtrer dept.",
                ImageName = "BO_Department",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ItemType = SingleChoiceActionItemType.ItemIsOperation,
                ShowItemsOnClick = true
            };
            filtrerDeptAction.Execute += FiltrerDeptAction_Execute;
        }

        // ── Activation ────────────────────────────────────────
        protected override void OnActivated()
        {
            base.OnActivated();
            ChargerDepartements();
        }

        // ── Chargement des départements dans le filtre ────────
        void ChargerDepartements()
        {
            filtrerDeptAction.Items.Clear();
            filtrerDeptAction.Items.Add(
                new ChoiceActionItem("Tous les départements", null));

            var depts = ObjectSpace.GetObjectsQuery<Departement>()
                .OrderBy(d => d.Nom)
                .ToList();

            foreach (var d in depts)
                filtrerDeptAction.Items.Add(
                    new ChoiceActionItem(d.Nom, d.Nom));
        }

        void FiltrerDeptAction_Execute(object sender, SingleChoiceActionExecuteEventArgs e)
        {
            var deptNom = e.SelectedChoiceActionItem?.Data as string;
            if (string.IsNullOrEmpty(deptNom))
            {
                View.CollectionSource.Criteria.Remove("dept");
            }
            else
            {
                View.CollectionSource.Criteria["dept"] =
                    CriteriaOperator.Parse(
                        "DepartementNom = ?", deptNom);
            }
            View.Refresh();
        }

        // ── Synchronisation complète ──────────────────────────
        void SynchroniserAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var nb = SynchroniserTout(ObjectSpace);
                ObjectSpace.CommitChanges();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    $"Planning synchronisé — {nb} événement(s) créés/mis à jour.",
                    InformationType.Success, 4000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur synchronisation : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }

        // ════════════════════════════════════════════════════════
        // MÉTHODES STATIQUES — appelables depuis d'autres controllers
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Synchronise toutes les demandes de congé vers EvenementConge.
        /// Retourne le nombre d'événements créés ou mis à jour.
        /// </summary>
        public static int SynchroniserTout(IObjectSpace os)
        {
            int nb = 0;

            // Tous les congés (sauf brouillon)
            var demandes = os.GetObjectsQuery<CongeDemande>()
                .Where(d => d.Statut != CongeStatut.Brouillon
                         && d.Salarie != null)
                .ToList();

            foreach (var d in demandes)
            {
                EvenementConge.CreerOuMettreAJour(os, d);
                nb++;
            }

            // Jours fériés de l'année courante et suivante
            var anneeMin = DateTime.Today.Year;
            var feries = os.GetObjectsQuery<JourFerie>()
                .Where(f => f.Date.Year >= anneeMin)
                .ToList();

            foreach (var f in feries)
            {
                EvenementConge.CreerJourFerie(os, f);
                nb++;
            }

            return nb;
        }

        /// <summary>
        /// Met à jour l'événement d'une seule demande.
        /// À appeler depuis CongeWorkflowController à chaque changement de statut.
        /// </summary>
        public static void MettreAJourEvenement(
            IObjectSpace os, CongeDemande demande)
        {
            if (demande == null) return;

            if (demande.Statut == CongeStatut.Brouillon)
            {
                // Supprimer l'événement si la demande repasse en brouillon
                var existant = os.GetObjectsQuery<EvenementConge>()
                    .ToList()
                    .FirstOrDefault(e => e.DemandeCongé?.Oid == demande.Oid);
                if (existant != null)
                    os.Delete(existant);
                return;
            }

            EvenementConge.CreerOuMettreAJour(os, demande);
        }
    }
}
