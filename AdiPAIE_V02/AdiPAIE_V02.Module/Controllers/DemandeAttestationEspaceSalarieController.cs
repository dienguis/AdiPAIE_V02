using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Controllers.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using System.Collections.Generic;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Espace salarie : adapte les vues DemandeAttestation pour un employe.
    ///
    ///   Masque les boutons RH : Generer attestation, Prendre en charge,
    ///   Traiter, Rejeter, Diagrammes de workflow.
    ///   Grise les champs RH (Statut, TraitePar, etc.)
    ///
    /// RH / Admin -> aucune restriction.
    /// </summary>
    public class DemandeAttestationEspaceSalarieController
        : ObjectViewController<ObjectView, DemandeAttestation>
    {
        private const string ReasonKey = "EspaceSalarieAttestation";

        private readonly List<Controller> _controlleursDesactives = new List<Controller>();

        // Actions autorisees pour le salarie
        private static readonly HashSet<string> ActionsAutorisees = new HashSet<string>
        {
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

        protected override void OnActivated()
        {
            base.OnActivated();

            if (!EspaceSalarieHelper.EstSalarieConnecte(ObjectSpace))
                return;

            // ---- 1. Desactiver les controleurs RH entiers ----
            DesactiverControleur<DemandeAttestationWorkflowController>();
            DesactiverControleur<AttestationReportController>();
            DesactiverControleur<DemandeHierarchieWorkflowController>();

            // ---- 2. Masquer les actions non autorisees ----
            foreach (var controller in Frame.Controllers)
            {
                foreach (ActionBase action in controller.Actions)
                {
                    if (ActionsAutorisees.Contains(action.Id))
                        continue;

                    if (action.Id == "Workflows_OuvrirDiagrammes"
                        || action.Id == "Attestation_Generer"
                        || action.Id == "Demande_PrendreEnCharge"
                        || action.Id == "Demande_Traiter"
                        || action.Id == "Demande_Rejeter"
                        || action.Category == "Edit"
                        || action.Category == "RecordEdit")
                    {
                        action.Active[ReasonKey] = false;
                    }
                }
            }

            // ---- 3. Auto-remplir Salarié + griser les champs RH (DetailView) ----
            if (View is DetailView dv)
            {
                AutoRemplirSalarie(dv);
                GriserChampSalarie(dv);
                GriserChampsRH(dv);

                // Masquer Delete + Save si la demande n'est plus modifiable
                var demande = dv.CurrentObject as DemandeAttestation;
                if (demande != null && demande.Statut >= DemandeStatut.Soumise)
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
            foreach (var ctrl in _controlleursDesactives)
                ctrl.Active.RemoveItem(ReasonKey);
            _controlleursDesactives.Clear();

            foreach (var controller in Frame.Controllers)
                foreach (ActionBase action in controller.Actions)
                    action.Active.RemoveItem(ReasonKey);

            base.OnDeactivated();
        }

        private void AutoRemplirSalarie(DetailView dv)
        {
            var demande = dv.CurrentObject as DemandeAttestation;
            if (demande == null) return;

            if (demande.Salarie == null)
            {
                var sal = EspaceSalarieHelper.GetSalarieConnecte(ObjectSpace);
                if (sal != null)
                    demande.Salarie = (Salarie)ObjectSpace.GetObject(sal);
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
                "Statut", "TraitePar", "DateTraitement",
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
