using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Controllers.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.Persistent.Base;
using System.Collections.Generic;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Espace salarie : adapte les vues CongeDemande pour un salarie connecte.
    ///
    ///   1. Auto-remplit le champ Salarie avec le salarie connecte
    ///   2. Grise (read-only) le champ Salarie et les champs RH
    ///   3. Desactive les controleurs purement RH (CongeAccordController)
    ///   4. Masque les boutons RH non autorises
    ///   5. Ajoute un bouton Soumettre sur la DetailView
    ///   6. NE desactive PAS CongeHierarchieWorkflowController
    ///      (il gere sa propre visibilite — le N+1 voit Valider/Rejeter)
    ///   7. Desactive la navigation vers la fiche Salarie (employe + N+1)
    ///
    /// RH / Admin (non salarie) -> aucune restriction.
    /// </summary>
    public class CongeDemandeEspaceSalarieController
        : ObjectViewController<ObjectView, CongeDemande>
    {
        private const string ReasonKey = "EspaceSalarieCongeDemande";

        private readonly List<Controller> _controlleursDesactives = new List<Controller>();
        private readonly SimpleAction _soumettreDetailAction;
        private bool _estSalarieRestreint;

        /// <summary>
        /// Actions autorisees pour un salarie (employe ou N+1).
        /// Les actions de CongeHierarchieWorkflowController sont aussi autorisees
        /// car ce controleur gere sa propre visibilite.
        /// </summary>
        private static readonly HashSet<string> ActionsAutorisees = new HashSet<string>
        {
            "Conge_Soumettre",                // ListView (salarié)
            "Conge_Soumettre_Detail",         // DetailView (salarié)
            "Conge_Hierarchie_Valider",       // ListView (N+1 / N+2)
            "Conge_Hierarchie_Rejeter",       // ListView (N+1 / N+2)
            "New",
            "Save",
            "SaveAndClose",
            "SaveAndNew",
            "Delete",
            "ListViewProcessCurrentObject",
            "Refresh",
            "FullTextSearch",
            "SetFilter",
            "ClearFilter",
            "ShowNavigationItem",
            "Close",
            "Cancel",
            "NavigateBack",
            "NextObject",
            "PreviousObject",
            "ShowAllContexts",
            "ColumnChooser",
            "DialogOK",
            "DialogCancel"
        };

        public CongeDemandeEspaceSalarieController()
        {
            // Action Soumettre pour la DetailView (le salarie doit pouvoir soumettre)
            _soumettreDetailAction = new SimpleAction(this,
                "Conge_Soumettre_Detail", PredefinedCategory.Edit)
            {
                Caption = "Soumettre",
                ImageName = "Action_Forward",
                ToolTip = "Soumet la demande pour validation.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetViewType = ViewType.DetailView,
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Brouillon#",
                ConfirmationMessage = "Soumettre cette demande de conge ?"
            };
            _soumettreDetailAction.Execute += SoumettreDetail_Execute;
            // Masquer par defaut, activer uniquement pour les salaries
            _soumettreDetailAction.Active["OnlyForSalarie"] = false;
        }

        protected override void OnActivated()
        {
            base.OnActivated();

            // ── 0. Auto-remplir + griser le salarié (pour TOUT salarié connecté, y compris RH) ──
            //    On utilise EstSalarieConnecte (comme DemandeAttestation) pour que
            //    cela fonctionne quel que soit le type d'ObjectSpace (nested, etc.)
            if (EspaceSalarieHelper.EstSalarieConnecte(ObjectSpace) && View is DetailView dvAuto)
            {
                AutoRemplirSalarie(dvAuto);
                GriserChampSalarie(dvAuto);
            }

            // Écouter les changements d'objet courant (ex: "Nouveau" depuis la liste)
            View.CurrentObjectChanged += View_CurrentObjectChanged;

            // ── Restrictions espace salarié (sauf RH) ──
            if (!EspaceSalarieHelper.DoitRestreindreEspaceSalarie(ObjectSpace))
                return;

            // ══════════════════════════════════════════════════════
            // A partir d'ici : l'utilisateur est un PUR employe
            // (ou N+1 avec uniquement le role Employe)
            // ══════════════════════════════════════════════════════

            _estSalarieRestreint = true;

            // Activer le bouton Soumettre pour l'employe
            _soumettreDetailAction.Active["OnlyForSalarie"] = true;

            // ---- 1. Desactiver CongeAccordController (RH uniquement) ----
            DesactiverControleur<CongeAccordController>();

            // ---- 2. Masquer les actions RH du CongeWorkflowController ----
            var cwc = Frame.GetController<CongeWorkflowController>();
            if (cwc != null)
            {
                foreach (ActionBase action in cwc.Actions)
                {
                    if (action.Id != "Conge_Soumettre")
                        action.Active[ReasonKey] = false;
                }
            }

            // ---- 3. Masquer les actions residuelles non autorisees ----
            foreach (var controller in Frame.Controllers)
            {
                foreach (ActionBase action in controller.Actions)
                {
                    if (ActionsAutorisees.Contains(action.Id))
                        continue;

                    if (action.Id == "Workflows_OuvrirDiagrammes")
                        action.Active[ReasonKey] = false;
                }
            }

            // ---- 4. Masquer les colonnes qui pointent vers l'objet Salarie ----
            MasquerColonnesSalarieLookup();

            // ---- 5. Griser les champs RH + lecture seule si pas Brouillon (DetailView) ----
            if (View is DetailView dv)
            {
                GriserChampsRH(dv);

                var demande = dv.CurrentObject as CongeDemande;
                if (demande != null && demande.Statut != CongeStatut.Brouillon)
                {
                    MasquerAction("Delete");
                    MasquerAction("Save");
                    MasquerAction("SaveAndClose");
                    MasquerAction("SaveAndNew");
                    dv.AllowEdit[ReasonKey] = false;
                }
            }
        }

        protected override void OnDeactivated()
        {
            View.CurrentObjectChanged -= View_CurrentObjectChanged;
            _estSalarieRestreint = false;

            foreach (var ctrl in _controlleursDesactives)
                ctrl.Active.RemoveItem(ReasonKey);
            _controlleursDesactives.Clear();

            foreach (var controller in Frame.Controllers)
                foreach (ActionBase action in controller.Actions)
                    action.Active.RemoveItem(ReasonKey);

            base.OnDeactivated();
        }

        private void View_CurrentObjectChanged(object sender, System.EventArgs e)
        {
            // Auto-remplir pour tout salarié connecté (pas juste les restreints)
            if (!EspaceSalarieHelper.EstSalarieConnecte(ObjectSpace)) return;
            if (View is DetailView dv)
            {
                AutoRemplirSalarie(dv);
                GriserChampSalarie(dv);
            }
        }

        private void SoumettreDetail_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (CongeDemande)View.CurrentObject;
            d.Soumettre();

            // Notifier le N+1 (même logique que CongeWorkflowController)
            NotifierResponsable(d);

            ObjectSpace.CommitChanges();
            PlanningCongeController.MettreAJourEvenement(ObjectSpace, d);
            View.Refresh();
            Application.ShowViewStrategy?.ShowMessage(
                "Demande soumise avec succes.",
                InformationType.Success, 3000, InformationPosition.Top);
        }

        /// <summary>
        /// Crée une notification pour le valideur N+1 (ou le manager)
        /// et envoie un email. Réplique la logique de CongeWorkflowController.
        /// </summary>
        private void NotifierResponsable(CongeDemande demande)
        {
            try
            {
                var destinataire = demande.ValideurN1 ?? demande.Salarie?.Manager;
                if (destinataire == null) return;

                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = destinataire;
                notif.Titre = "Demande de congé en attente de votre validation";
                notif.Corps = $"{demande.Salarie?.FullName} souhaite prendre un congé "
                    + $"du {demande.DateDebut:dd/MM/yyyy} au {demande.DateFin:dd/MM/yyyy} "
                    + $"({demande.DureeJours:n1} jour(s) — {demande.Type?.Libelle}).";
                notif.Categorie = "Congé";
                notif.Priorite = NotificationPriorite.Important;

                WorkflowEmailHelper.EnvoyerNotifAsync(Application, notif);
            }
            catch { /* Ne pas bloquer la soumission si la notification échoue */ }
        }

        private void AutoRemplirSalarie(DetailView dv)
        {
            var demande = (CongeDemande)dv.CurrentObject;
            if (demande == null) return;

            if (demande.Salarie == null)
            {
                var sal = EspaceSalarieHelper.GetSalarieConnecte(ObjectSpace);
                if (sal != null)
                    demande.Salarie = (Salarie)ObjectSpace.GetObject(sal);
            }

            // Pré-remplir ValideurN1 depuis le Manager (N+1) du salarié
            if (demande.ValideurN1 == null && demande.Salarie?.Manager != null)
            {
                demande.ValideurN1 = (Salarie)ObjectSpace.GetObject(demande.Salarie.Manager);
            }
        }

        private void GriserChampSalarie(DetailView dv)
        {
            var item = dv.FindItem("Salarie");
            if (item is PropertyEditor pe)
                pe.AllowEdit[ReasonKey] = false;
        }

        private void GriserChampsRH(DetailView dv)
        {
            string[] champsRH = {
                "Statut", "TraiteParRH", "DateTraitementRH",
                "ValideurN1", "ValideurN2",
                "DateValidationN1", "DateValidationN2",
                "RejeteParRH"
            };

            foreach (var champ in champsRH)
            {
                var item = dv.FindItem(champ);
                if (item is PropertyEditor pe)
                    pe.AllowEdit[ReasonKey] = false;
            }
        }

        private void MasquerAction(string actionId)
        {
            foreach (var controller in Frame.Controllers)
                foreach (ActionBase action in controller.Actions)
                    if (action.Id == actionId)
                        action.Active[ReasonKey] = false;
        }

        /// <summary>
        /// Masque les colonnes lookup qui pointent vers des objets Salarie
        /// dans la ListView (Salarie, ValideurN1, ValideurN2).
        /// col.Index = -1 cache la colonne sans la supprimer du model.
        /// </summary>
        private void MasquerColonnesSalarieLookup()
        {
            if (View is not ListView lv) return;

            var colonnesAMasquer = new HashSet<string>
            {
                "Salarie", "ValideurN1", "ValideurN2"
            };

            foreach (var col in lv.Model.Columns)
            {
                if (colonnesAMasquer.Contains(col.PropertyName))
                    col.Index = -1;
            }
        }

        private void DesactiverControleur<T>() where T : Controller
        {
            var ctrl = Frame.GetController<T>();
            if (ctrl != null)
            {
                ctrl.Active[ReasonKey] = false;
                _controlleursDesactives.Add(ctrl);
            }
        }
    }
}
