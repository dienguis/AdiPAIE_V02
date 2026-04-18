using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.JSInterop;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Bouton "Aide" dans la barre principale.
    /// Ouvre une fenêtre popup avec le guide contextuel du module courant.
    /// Le mapping ViewId → page HTML est géré côté JS (help.js).
    /// </summary>
    public class HelpController : WindowController
    {
        private readonly SimpleAction _openHelpAction;

        public HelpController()
        {
            TargetWindowType = WindowType.Main;

            _openHelpAction = new SimpleAction(this, "OpenHelp", PredefinedCategory.Tools)
            {
                Caption = "Aide",
                ImageName = "Help",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Ouvre le guide d'utilisation du module courant."
            };
            _openHelpAction.Execute += OpenHelp_Execute;
        }

        private async void OpenHelp_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var currentViewId = Frame?.View?.Id ?? "";

            try
            {
                var jsRuntime = Application?.ServiceProvider?
                    .GetService(typeof(Microsoft.JSInterop.IJSRuntime))
                    as Microsoft.JSInterop.IJSRuntime;

                if (jsRuntime != null)
                {
                    await jsRuntime.InvokeVoidAsync("AdiPAIE.openHelp", currentViewId);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[HelpController] Erreur ouverture aide : {ex.Message}");
            }
        }
    }
}
