using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.SystemModule;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Intercepte la navigation vers TableauBordEffectif pour afficher
    /// directement une DetailView peuplée (au lieu d'une ListView vide).
    /// Masque aussi l'ancien item "Tableaux de bord RH" du DashboardsModule.
    /// </summary>
    public class TableauBordNavigationController : WindowController
    {
        protected override void OnActivated()
        {
            base.OnActivated();
            var navController = Frame.GetController<ShowNavigationItemController>();
            if (navController != null)
            {
                navController.CustomShowNavigationItem += OnCustomShowNavigationItem;
                navController.NavigationItemCreated += OnNavigationItemCreated;
            }
        }

        protected override void OnDeactivated()
        {
            var navController = Frame.GetController<ShowNavigationItemController>();
            if (navController != null)
            {
                navController.CustomShowNavigationItem -= OnCustomShowNavigationItem;
                navController.NavigationItemCreated -= OnNavigationItemCreated;
            }
            base.OnDeactivated();
        }

        /// <summary>
        /// Masquer les anciens items Dashboard DevExpress (DashboardData_ListView).
        /// </summary>
        private void OnNavigationItemCreated(object sender, NavigationItemCreatedEventArgs e)
        {
            if (e.NavigationItem?.Id != null)
            {
                var id = e.NavigationItem.Id;
                // Masquer l'item DashboardData (ancien module Dashboard DevExpress)
                if (id.Contains("DashboardData") || id.Contains("Dashboard_ListView"))
                {
                    e.NavigationItem.Active["HideDashboard"] = false;
                }
            }
        }

        /// <summary>
        /// Quand on clique sur "Effectifs RH", créer et peupler le TableauBordEffectif.
        /// </summary>
        private void OnCustomShowNavigationItem(object sender, CustomShowNavigationItemEventArgs e)
        {
            if (e.ActionArguments?.SelectedChoiceActionItem?.Data is ViewShortcut shortcut
                && shortcut.ViewId != null)
            {
                if (shortcut.ViewId.Contains("TableauBordEffectif"))
                {
                    e.Handled = true;
                    ShowNonPersistentDashboard<TableauBordEffectif, Salarie>(e,
                        (tb, xpoOs) => DashboardEffectifService.Populate(tb, xpoOs));
                }
                else if (shortcut.ViewId.Contains("TableauBordInterimaire"))
                {
                    e.Handled = true;
                    ShowNonPersistentDashboard<TableauBordInterimaire, Interimaire>(e,
                        (tb, xpoOs) => DashboardInterimaireService.Populate(tb, xpoOs));
                }
            }
        }

        /// <summary>
        /// Méthode générique pour afficher un tableau de bord non-persistant.
        /// </summary>
        private void ShowNonPersistentDashboard<TDashboard, TPersistent>(
            CustomShowNavigationItemEventArgs e,
            System.Action<TDashboard, IObjectSpace> populate)
            where TDashboard : class
        {
            var os = Application.CreateObjectSpace(typeof(TDashboard));
            var xpoOs = Application.CreateObjectSpace(typeof(TPersistent));

            if (os is DevExpress.ExpressApp.CompositeObjectSpace compositeOs)
                compositeOs.AdditionalObjectSpaces.Add(xpoOs);
            else if (os is DevExpress.ExpressApp.NonPersistentObjectSpace npOs)
                npOs.AdditionalObjectSpaces.Add(xpoOs);

            var tb = os.CreateObject<TDashboard>();
            populate(tb, xpoOs);

            var detailView = Application.CreateDetailView(os, tb);
            detailView.ViewEditMode = DevExpress.ExpressApp.Editors.ViewEditMode.View;

            e.ActionArguments.ShowViewParameters.CreatedView = detailView;
        }
    }
}
