using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.JSInterop;
using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace AdiPAIE_V02.Blazor.Server.Controllers
{
    /// <summary>
    /// Génère la feuille d'émargement d'une SessionFormation et la propose
    /// en téléchargement direct dans le navigateur via JSInterop.
    /// Placé dans le projet Blazor.Server pour avoir accès à IJSRuntime.
    /// </summary>
    public class EmargementActionController
        : ObjectViewController<DetailView, SessionFormation>
    {
        private readonly SimpleAction _emargementAction;

        public EmargementActionController()
        {
            _emargementAction = new SimpleAction(this,
                "SessionFormation_Emargement",
                PredefinedCategory.View)
            {
                Caption = "Émargement",
                ImageName = "Action_Print",
                ToolTip = "Génère la feuille de présence PDF à imprimer."
            };
            _emargementAction.Execute += OnEmargementExecute;
        }

        private void OnEmargementExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var session = (SessionFormation)View.CurrentObject;
            try
            {
                // 1. Génération en mémoire — aucun fichier conservé sur disque
                var (contenu, nomFichier, estPdf) =
                    FeuillEmargementService.Generer(ObjectSpace, session);

                // 2. Téléchargement via JSInterop
                var jsRuntime = Application.ServiceProvider?
                    .GetService<IJSRuntime>();

                if (jsRuntime != null)
                {
                    var mimeType = estPdf ? "application/pdf" : "text/html";
                    var base64 = Convert.ToBase64String(contenu);

                    // Fire-and-forget — handler synchrone, pas d'await possible
                    _ = jsRuntime.InvokeVoidAsync(
                            "AdiPAIE.downloadFile", nomFichier, mimeType, base64)
                        .AsTask();

                    Application.ShowViewStrategy?.ShowMessage(
                        $"Feuille d'émargement prête : {nomFichier}",
                        InformationType.Success, 4000, InformationPosition.Top);
                }
                else
                {
                    // Ne devrait pas arriver en Blazor Server
                    Application.ShowViewStrategy?.ShowMessage(
                        "JSRuntime indisponible — impossible de télécharger le fichier.",
                        InformationType.Warning, 5000, InformationPosition.Top);
                }
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur feuille d'émargement : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }
    }
}
