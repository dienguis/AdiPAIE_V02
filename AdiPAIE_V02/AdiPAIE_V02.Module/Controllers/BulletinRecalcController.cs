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

namespace AdiPAIE_V02.Module.Controllers
{
    // For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.ViewController.
    public partial class BulletinRecalcController : ObjectViewController<ObjectView, Bulletin>
    {
        // Use CodeRush to create Controllers and Actions with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/403133/
        readonly SimpleAction recalcAction;
        public BulletinRecalcController()
        {
            InitializeComponent();
            // Target required Views (via the TargetXXX properties) and create their Actions.
            TargetObjectType = typeof(Bulletin);
            TargetViewType = ViewType.Any;

            recalcAction = new SimpleAction(this, "Bulletin_RecalculerMaintenant", PredefinedCategory.Edit)
            {
                Caption = "Recalculer maintenant",
                //ImageName = "Action_Calculate",
                ImageName = "Action_Refresh",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            recalcAction.Execute += RecalcAction_Execute;
        }
        private void RecalcAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var os = ObjectSpace;
            var bulletins = e.SelectedObjects.Cast<Bulletin>().ToList();
            if (View is DetailView dv && dv.CurrentObject is Bulletin one && bulletins.Count == 0)
                bulletins.Add(one);

            if (bulletins.Count == 0)
            {
                Application.ShowViewStrategy.ShowMessage("Sélectionne au moins un bulletin.", InformationType.Warning, 3000, InformationPosition.Top);
                return;
            }

            foreach (var b in bulletins)
            {
                try
                {
                    b.RecalculerCotisationsEtTotaux();
                }
                catch (Exception ex)
                {
                    Application.ShowViewStrategy.ShowMessage($"Erreur sur bulletin {b?.DisplayName ?? ""} : {ex.Message}",
                        InformationType.Error, 6000, InformationPosition.Top);
                }
            }

            os.CommitChanges();
            View?.ObjectSpace?.Refresh();
            Application.ShowViewStrategy.ShowMessage("Recalcul terminé.", InformationType.Success, 3000, InformationPosition.Top);
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
