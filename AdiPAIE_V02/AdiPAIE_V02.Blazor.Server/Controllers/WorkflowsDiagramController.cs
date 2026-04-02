using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.JSInterop;
using Microsoft.Extensions.DependencyInjection;

namespace AdiPAIE_V02.Blazor.Server.Controllers
{
    /// <summary>
    /// Controller qui ouvre la page des workflows dans un nouvel onglet.
    /// Disponible partout dans l'application (WindowController).
    ///
    
    /// </summary>
    public class WorkflowsDiagramController : WindowController
    {
        readonly SimpleAction ouvrirAction;

        public WorkflowsDiagramController()
        {
            ouvrirAction = new SimpleAction(this,
                "Workflows_OuvrirDiagrammes",
                PredefinedCategory.View)
            {
                Caption = "Diagrammes de workflow",
                ImageName = "Action_Debug",
                ToolTip = "Affiche les diagrammes de tous les workflows AdiPAIE."
            };
            ouvrirAction.Execute += async (s, e) =>
            {
                var js = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (js != null)
                    await js.InvokeVoidAsync("open", "/workflows.html", "_blank");
            };
        }
    }
}
