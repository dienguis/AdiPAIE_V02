using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.Persistent.Base;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    public class SalarieDossierRHController : ObjectViewController<DetailView, Salarie>
    {
        private readonly SimpleAction openDossierRhAction;

        public SalarieDossierRHController()
        {
            openDossierRhAction = new SimpleAction(this, "OpenDossierRH", PredefinedCategory.View)
            {
                Caption = "Ouvrir dossier RH",
                ImageName = "BO_Folder"
            };

            openDossierRhAction.Execute += OpenDossierRhAction_Execute;
        }

        private void OpenDossierRhAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var salarieCourant = View.CurrentObject as Salarie;
            if (salarieCourant == null)
                return;

            if (ObjectSpace.IsNewObject(salarieCourant))
                throw new UserFriendlyException("Enregistrez d'abord le salarié avant d'ouvrir son dossier RH.");

            // 1) Nouveau ObjectSpace pour la nouvelle root view
            var osNew = Application.CreateObjectSpace(typeof(DossierSalarie));

            // 2) Recharger le salarié dans ce nouvel ObjectSpace
            var salarie = osNew.GetObject(salarieCourant);

            // 3) Chercher le dossier RH
            var dossier = salarie.DossierSalarie.FirstOrDefault();

            // 4) Le créer si absent
            if (dossier == null)
            {
                dossier = osNew.CreateObject<DossierSalarie>();
                dossier.Salarie = salarie;
                dossier.DateOuverture = DateTime.Today;
                dossier.Reference = $"DS-{salarie.Matricule}";
                osNew.CommitChanges();
            }

            // 5) Ouvrir la fiche avec le NOUVEL ObjectSpace
            var detailView = Application.CreateDetailView(osNew, dossier);
            detailView.ViewEditMode = ViewEditMode.Edit;

            var svp = new ShowViewParameters(detailView)
            {
                TargetWindow = TargetWindow.NewWindow
            };

            Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(Frame, null));
        }
    }
}