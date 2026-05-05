// =============================================================================
//  BulletinInterimImportController.cs — V1.3 Sprint 1 (mai 2026)
//
//  Ajoute le bouton "Charger livre de paie" dans la barre d'actions de la
//  ListView de ImportBulletinInterimBatch. Le clic redirige vers la page
//  Razor /interim/import-livre-paie (wizard 4 étapes).
// =============================================================================

using System;
using AdiPAIE_V02.Module.BusinessObjects.Interim;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.JSInterop;
using Microsoft.Extensions.DependencyInjection;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Controller actif uniquement sur ListView de ImportBulletinInterimBatch.
    /// Ajoute une action "Charger livre de paie" qui ouvre le wizard Razor.
    /// </summary>
    public class BulletinInterimImportController : ViewController
    {
        public BulletinInterimImportController()
        {
            TargetObjectType = typeof(ImportBulletinInterimBatch);
            TargetViewType = ViewType.ListView;

            var action = new SimpleAction(this, "ChargerLivrePaieInterim", PredefinedCategory.RecordEdit)
            {
                Caption = "Charger livre de paie",
                ImageName = "Action_Import",
                ToolTip = "Importer un fichier Excel de facturation envoyé par une société d'intérim."
            };
            action.Execute += OnExecute;
        }

        private async void OnExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var jsRuntime = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (jsRuntime != null)
                {
                    // Ouvre dans un nouvel onglet pour préserver le contexte XAF courant
                    await jsRuntime.InvokeVoidAsync("open", "/interim/import-livre-paie", "_blank");
                }
                else
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        "Impossible d'ouvrir le wizard d'import (service JS indisponible).",
                        InformationType.Warning, 6000, InformationPosition.Top);
                }
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur d'ouverture : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }
    }
}
