
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;

namespace AdiPAIE_V02.Module.Controllers
{
    public class BulletinPretController : ObjectViewController<DetailView, Bulletin>
    {
    //    readonly SimpleAction validerPrets;

    //    public BulletinPretController()
    //    {
            //validerPrets = new SimpleAction(this, "ValiderRemboursementsPrets", PredefinedCategory.Edit)
            //{
            //    Caption = "Valider remboursements prêts",
            //    ImageName = "Action_Import",
            //    PaintStyle = ActionItemPaintStyle.Caption,
            //    ToolTip = "Marque les échéances de prêts du mois comme prélevées.",
            //    SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            //};
            //validerPrets.Execute += (s, e) =>
            //{
            //    var b = View.CurrentObject as Bulletin;
            //    if (b == null) return;
            //    b.ValiderRemboursementsPrets();
            //    ObjectSpace.CommitChanges();
            //    Application.ShowViewStrategy.ShowMessage(
            //        "Remboursements validés.",
            //        InformationType.Success, 2500, InformationPosition.Top);
            //    View.ObjectSpace.Refresh();
            //};
       // }
    }
}