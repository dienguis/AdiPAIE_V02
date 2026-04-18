using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Espace salarie : consultation seule de la fiche Salarie.
    ///
    ///   Salarie connecte -> masque les actions RH et passe en lecture seule.
    ///   RH / Admin       -> aucune restriction.
    /// </summary>
    public class SalarieEspaceSalarieReadOnlyController
        : ObjectViewController<ObjectView, Salarie>
    {
        private const string ReasonKey = "EspaceSalarieReadOnly";

        private static readonly HashSet<string> ActionsAutorisees =
            new HashSet<string>
            {
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
                "ColumnChooser"
            };

        private static readonly HashSet<string> ActionsExplicitementMasquees =
            new HashSet<string>
            {
                "Salarie.Active",
                "Salarie.Desactive",
                "Salarie.RegenererModele",
                "OpenDossierRH",
                "EnvoyerClePDF",
                "CreateBulletinsForPeriod",
                "OpenEmployeeBulletins",
                "New",
                "Delete",
                "Save",
                "SaveAndClose",
                "SaveAndNew"
            };

        protected override void OnActivated()
        {
            base.OnActivated();

            if (!EspaceSalarieHelper.DoitRestreindreEspaceSalarie(ObjectSpace))
                return;

            foreach (var controller in Frame.Controllers)
            {
                foreach (ActionBase action in controller.Actions)
                {
                    if (ActionsAutorisees.Contains(action.Id))
                        continue;

                    if (ActionsExplicitementMasquees.Contains(action.Id)
                        || action.Category == "Edit"
                        || action.Category == "RecordEdit")
                    {
                        action.Active[ReasonKey] = false;
                    }
                }
            }

            if (View is DetailView dv)
            {
                dv.AllowEdit[ReasonKey] = false;
            }
        }

        protected override void OnDeactivated()
        {
            foreach (var controller in Frame.Controllers)
                foreach (ActionBase action in controller.Actions)
                    action.Active.RemoveItem(ReasonKey);

            if (View is DetailView dv)
                dv.AllowEdit.RemoveItem(ReasonKey);

            base.OnDeactivated();
        }
    }
}
