using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Layout;
using DevExpress.ExpressApp.Model.NodeGenerators;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.ExpressApp.Utils;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdiPAIE_V02.Module.Controllers
{
    // For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.ViewController.
    public partial class GenerateEcritureController : ObjectViewController<DetailView, Bulletin>
    {
        SimpleAction action;

        public GenerateEcritureController()
        {
            action = new SimpleAction(this, "GenerateEcritureFromBulletin", PredefinedCategory.RecordEdit)
            {
                Caption = "Générer écriture comptable",
                ImageName = "BO_Invoice",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            action.Execute += OnExecute;
        }

        private void OnExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var bulletin = View?.CurrentObject as Bulletin;
            if (bulletin == null) return;

            var svc = new EcritureFromBulletinService(ObjectSpace);
            var ecr = svc.GenererEcriture(bulletin);

            // Ouvrir l’écriture générée
            var os2 = Application.CreateObjectSpace(typeof(Ecriture));
            var ecr2 = os2.GetObject(ecr);
            var dv = Application.CreateDetailView(os2, ecr2);
            dv.ViewEditMode = ViewEditMode.Edit;
            e.ShowViewParameters.CreatedView = dv;
            e.ShowViewParameters.TargetWindow = TargetWindow.NewModalWindow;
        }
    }
}
