
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;

namespace AdiPAIE_V02.Module.Controllers
{
 
    public partial class GenerateEcritureController : ObjectViewController<DetailView, Bulletin>
    {
        SimpleAction action;

        public GenerateEcritureController()
        {
            action = new SimpleAction(this, "GenerateEcritureFromBulletin", PredefinedCategory.Edit)
            {
                Caption = "Générer écriture comptable",
                ImageName = "BO_Invoice",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            action.Execute += OnExecute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            // Masquer — utiliser l'export batch par mois
            action.Active["UsesBatchExport"] = false;
        }

        private void OnExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var bulletin = View?.CurrentObject as Bulletin;
            if (bulletin == null) return;

            var svc = new EcritureFromBulletinService(ObjectSpace);
            var ecr = svc.GenererEcriture(bulletin);

            var os2 = Application.CreateObjectSpace(typeof(Ecriture));
            var ecr2 = os2.GetObject(ecr);
            var dv = Application.CreateDetailView(os2, ecr2);
            dv.ViewEditMode = ViewEditMode.Edit;
            e.ShowViewParameters.CreatedView = dv;
            e.ShowViewParameters.TargetWindow = TargetWindow.NewModalWindow;
        }
    }
}
