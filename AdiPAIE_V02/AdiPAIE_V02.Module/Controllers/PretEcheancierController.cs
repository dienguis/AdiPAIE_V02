using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;

namespace AdiPAIE_V02.Module.Controllers
{
    public class PretEcheancierController : ObjectViewController<DetailView, Pret>
    {
        SimpleAction genAction, suspendAction, relancerAction;

        public PretEcheancierController()
        {
            genAction = new SimpleAction(this, "GenererEcheancier", PredefinedCategory.Edit)
            {
                Caption = "Générer l'échéancier",
                ImageName = "Action_Refresh",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            genAction.Execute += (s, e) =>
            {
                var p = View.CurrentObject as Pret;
                p?.GenererEcheancier(effacerExistant: true);
                ObjectSpace.CommitChanges();
                Application.ShowViewStrategy.ShowMessage("Échéancier généré.", InformationType.Success, 2000, InformationPosition.Bottom);
            };

            suspendAction = new SimpleAction(this, "SuspendrePret", PredefinedCategory.Edit)
            {
                Caption = "Suspendre",
                ImageName = "Action_Suspend",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            suspendAction.Execute += (s, e) =>
            {
                var p = View.CurrentObject as Pret;
                if (p == null) return;
                p.Statut = DomainEnums.PretStatut.Suspendu;
                p.RecalculerEtat();
                ObjectSpace.CommitChanges();
            };

            relancerAction = new SimpleAction(this, "RelancerPret", PredefinedCategory.Edit)
            {
                Caption = "Relancer",
                ImageName = "Action_Grant",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            relancerAction.Execute += (s, e) =>
            {
                var p = View.CurrentObject as Pret;
                if (p == null) return;
                p.Statut = DomainEnums.PretStatut.EnCours;
                p.RecalculerEtat();
                ObjectSpace.CommitChanges();
            };
        }
    }
}
