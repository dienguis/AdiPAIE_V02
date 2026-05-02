// =============================================================================
//  DashboardsRHNavigationController.cs
//  Module « Tableaux de Bord RH » — contrôleur d'ouverture.
//
//  Ce contrôleur est attaché aux DetailView de DashboardsRHMenu (la classe
//  non persistante qui sert d'entrée de menu XAF). Il expose une SimpleAction
//  « Ouvrir les Tableaux de Bord » qui, via JSInterop, redirige le navigateur
//  vers la page Razor /dashboards/ — cette page (Blazor Server) héberge la
//  vraie interface (DxGrid, DxChart, DxComboBox, DxTagBox, …).
//
//  Pattern repris de BilanSocialController.cs (SimpleAction + IJSRuntime).
//
//  Étape 2 — squelette uniquement. La page /dashboards/ sera complétée à
//  l'Étape 3 (page d'accueil 6 cartes) puis à l'Étape 4 (un tableau par commit).
// =============================================================================

using AdiPAIE_V02.Module.NonPersistent;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Contrôleur de la DetailView de <see cref="DashboardsRHMenu"/>.
    /// Expose le bouton « Ouvrir les Tableaux de Bord » qui redirige
    /// vers la page Razor <c>/dashboards/</c>.
    /// </summary>
    public sealed class DashboardsRHNavigationController
        : ObjectViewController<DetailView, DashboardsRHMenu>
    {
        private readonly SimpleAction _openAction;

        public DashboardsRHNavigationController()
        {
            _openAction = new SimpleAction(this,
                id: "DashboardsRH_Open",
                category: PredefinedCategory.View)
            {
                Caption = "Ouvrir les Tableaux de Bord",
                ImageName = "BO_Report",
                ToolTip = "Ouvre la page d'accueil des tableaux de bord RH.",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _openAction.Execute += OnOpenExecute;
        }

        private void OnOpenExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var js = Application.ServiceProvider?.GetService<IJSRuntime>();
            if (js == null)
            {
                throw new UserFriendlyException(
                    "Redirection indisponible dans ce contexte (IJSRuntime introuvable).");
            }

            // Redirection plein-écran (sans nouvel onglet) vers la page Razor
            // d'accueil des tableaux de bord. Le bouton « Précédent » du
            // navigateur permet de revenir au shell XAF.
            _ = js.InvokeVoidAsync("location.assign", "/dashboards/").AsTask();
        }
    }
}
