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
using DevExpress.ExpressApp.Templates;
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

            // Ouvre les tableaux de bord dans un NOUVEL ONGLET (pattern aligné
            // sur le help SunuPaie). Cela évite de masquer le shell XAF
            // principal, l'utilisateur peut naviguer entre l'application et
            // les tableaux de bord sans perdre son contexte.
            //
            // window.open(url, target, features). Le 3e paramètre vide ⇒
            // onglet standard avec barres d'outils complètes (et non
            // popup minimaliste).
            _ = js.InvokeVoidAsync("open", "/dashboards/", "_blank").AsTask();
        }
    }
}
