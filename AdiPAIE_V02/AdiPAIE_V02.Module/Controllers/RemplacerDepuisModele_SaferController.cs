using AdiPAIE_V02.Module.BusinessObjects;
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
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    // For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.ViewController.
    public partial class RemplacerDepuisModele_SaferController : ObjectViewController<DetailView, Bulletin>
    {
        // Use CodeRush to create Controllers and Actions with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/403133/
        public RemplacerDepuisModele_SaferController()
        {
            InitializeComponent();
            // Target required Views (via the TargetXXX properties) and create their Actions.
            var a = new SimpleAction(this, "RemplacerLignesModeleSeulement", PredefinedCategory.Edit)
            {
                Caption = "Remplacer (lignes du modèle uniquement)",
                ImageName = "Action_Reset",
                ConfirmationMessage = "Les lignes provenant du modèle seront remplacées; les lignes saisies manuellement seront conservées. Continuer ?"
            };
            a.Execute += OnExecute;
        }
        private void OnExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var b = (Bulletin)View.CurrentObject;
            if (b == null) return;

            if (b.Statut != BulletinStatut.Brouillon)
            {
                Application.ShowViewStrategy.ShowMessage("Action autorisée uniquement en Brouillon.", InformationType.Warning, 3000, InformationPosition.Top);
                return;
            }

            b.RemplacerLignesIssuesDuModele(onlyIncludeDefault: false);

            ObjectSpace.CommitChanges();
            ObjectSpace.Refresh();
            Application.ShowViewStrategy.ShowMessage("Lignes du modèle remplacées.", InformationType.Success, 3000, InformationPosition.Top);
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            // Bouton retiré de l'interface
            foreach (ActionBase a in Actions) a.Active["Retired"] = false;
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
