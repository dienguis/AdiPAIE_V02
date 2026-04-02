
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.Controllers
{
    public partial class BulletinRecalcController : ObjectViewController<ObjectView, Bulletin>
    {
        readonly SimpleAction recalcAction;

        public BulletinRecalcController()
        {
            InitializeComponent(); // Designer.cs existe encore — garder

            recalcAction = new SimpleAction(this, "Bulletin_RecalculerMaintenant", PredefinedCategory.Edit)
            {
                Caption = "Recalculer",
                ImageName = "Action_Refresh",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Recalcule toutes les cotisations et totaux du bulletin.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            recalcAction.Execute += RecalcAction_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            // Visible uniquement en DetailView
            recalcAction.Active["DetailViewOnly"] = View is DetailView;
        }

        private void RecalcAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var b = View.CurrentObject as Bulletin;
            if (b == null) return;

            try
            {
                b.RecalculerCotisationsEtTotaux();
                ObjectSpace.CommitChanges();
                View.ObjectSpace.Refresh();
                Application.ShowViewStrategy.ShowMessage(
                    "Recalcul terminé.", InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur recalcul : {ex.Message}", InformationType.Error, 6000, InformationPosition.Top);
            }
        }

        protected override void OnDeactivated() => base.OnDeactivated();
    }
}
