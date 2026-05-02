
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;

namespace AdiPAIE_V02.Module.Controllers
{
    public class BulletinEmailController : ObjectViewController<ObjectView, Bulletin>
    {
        readonly SimpleAction action;

        public BulletinEmailController()
        {
            action = new SimpleAction(this, "EnvoyerBulletinEmail",
                PredefinedCategory.ObjectsCreation)
            {
                Caption = "Email",
                ImageName = "BO_Mail"
            };
            action.Execute += OnExecute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            // Masquer partout — remplacé par l'envoi en lot
            action.Active["Obsolete"] = false;
        }

        private void OnExecute(object sender, SimpleActionExecuteEventArgs e) { }
    }
}
