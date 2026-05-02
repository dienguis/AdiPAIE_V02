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
    public partial class ExportComptableController : ObjectViewController<DetailView, Bulletin>
    {
        // Use CodeRush to create Controllers and Actions with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/403133/
        public ExportComptableController()
        {
            InitializeComponent();
            // Target required Views (via the TargetXXX properties) and create their Actions.
            var a = new SimpleAction(this, "GenererEcritureComptable", PredefinedCategory.Save)
            {
                Caption = "Écriture comptable",
                ImageName = "BO_Invoice"
            };
            a.Execute += OnExecute;
        }

        private void OnExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var os = ObjectSpace;
            var b = (Bulletin)View.CurrentObject;
            if (b == null) return;

            // S'assure que les totaux sont à jour
           // b.RecalculerTotaux();
            b.RecalculerSurGrilleExistante();

            var ecr = ExportComptableService.GenererEcriturePourBulletin(os, b);

            os.CommitChanges();
            Application.ShowViewStrategy.ShowMessage("Écriture comptable générée.", InformationType.Success, 3000, InformationPosition.Top);

            // Ouvre l'écriture si tu veux
            // var dv = Application.CreateDetailView(os, ecr);
            // e.ShowViewParameters.CreatedView = dv;
        }


        protected override void OnActivated()
        {
            base.OnActivated();
            // Perform various tasks depending on the target View.
        }
        protected override void OnViewControlsCreated()
        {
            base.OnViewControlsCreated();
            // Access and customize the target View control.
        }
        protected override void OnDeactivated()
        {
            // Unsubscribe from previously subscribed events and release other references and resources.
            base.OnDeactivated();
        }
    }
}
