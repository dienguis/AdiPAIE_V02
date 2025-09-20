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
    public partial class ExportComptableBatchController : ViewController
    {
        // Use CodeRush to create Controllers and Actions with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/403133/
        public ExportComptableBatchController()
        {
            InitializeComponent();
            // Target required Views (via the TargetXXX properties) and create their Actions.
            // Bouton global (pas lié à un objet précis) => WindowController
            var a = new SimpleAction(this, "ExporterBulletinsMoisCourant", PredefinedCategory.Save)
            {
                Caption = "Exporter bulletins (mois courant)",
                ImageName = "BO_Invoice"
            };
            a.Execute += OnExecuteExportMoisCourant;
        }
        private void OnExecuteExportMoisCourant(object sender, SimpleActionExecuteEventArgs e)
        {
            var os = Application.CreateObjectSpace(typeof(Bulletin)); // OS de travail
            var today = DateTime.Today;
            int annee = today.Year;
            int mois = today.Month;

            var ecritures = ExportComptableBatchService.ExporterMois(os, annee, mois);

            os.CommitChanges();

            Application.ShowViewStrategy.ShowMessage(
                $"{ecritures.Count} écriture(s) générée(s) pour {mois:D2}/{annee}.",
                InformationType.Success, 4000, InformationPosition.Top);

            // Option : ouvrir la liste des écritures créées
            // var lv = Application.CreateListView(os, typeof(Ecriture), true);
            // e.ShowViewParameters.CreatedView = lv;
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
