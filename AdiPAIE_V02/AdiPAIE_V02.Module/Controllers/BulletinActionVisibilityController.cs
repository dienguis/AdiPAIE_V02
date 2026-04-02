
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;

namespace AdiPAIE_V02.Module.Controllers
{
    public class BulletinActionVisibilityController
        : ObjectViewController<ListView, Bulletin>
    {
        protected override void OnActivated()
        {
            base.OnActivated();

            // Ces actions [Action] sur Bulletin.cs apparaissent sur ListView
            // mais n'ont de sens qu'en DetailView — on les masque
            MasquerAction("Bulletin.ActionRecalculerDepuisParametrageSalarie");
            MasquerAction("Bulletin.ActionRecalculerGrille");
        }

        private void MasquerAction(string actionId)
        {
            // Chercher dans tous les controllers du Frame
            foreach (var controller in Frame.Controllers)
            {
                foreach (ActionBase action in controller.Actions)
                {
                    if (action.Id == actionId)
                    {
                        action.Active["ListViewOnly"] = false;
                        return;
                    }
                }
            }
        }
    }
}