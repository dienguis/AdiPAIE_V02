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
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdiPAIE_V02.Module.Controllers
{
    // For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.ViewController.
    public partial class ChargerDepuisModeleController : ObjectViewController<DetailView, Bulletin>
    {
        // Use CodeRush to create Controllers and Actions with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/403133/
        public ChargerDepuisModeleController()
        {
            InitializeComponent();
            // Target required Views (via the TargetXXX properties) and create their Actions.
            var a = new SimpleAction(this, "ChargerDepuisModele", PredefinedCategory.Edit)
            {
                Caption = "Charger depuis le modèle",
                ImageName = "Action_Import"
            };
            a.Execute += OnExecute;
        }

     

        private void OnExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var b = (Bulletin)View.CurrentObject;
            if (b == null) return;

            // Fusion : ajoute/complète sans écraser les lignes existantes
            b.CopierDepuisModele(overwriteExistingLines: false, onlyIncludeDefault: false);

            ObjectSpace.CommitChanges();
            ObjectSpace.Refresh();
            Application.ShowViewStrategy.ShowMessage("Modèle appliqué (fusion).", InformationType.Success, 3000, InformationPosition.Top);
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
