using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;

namespace AdiPAIE_V02.Module.Controllers
{
    public class BulletinPretController : ObjectViewController<DetailView, Bulletin>
    {
        readonly SimpleAction recalc;
        readonly SimpleAction validerPrets;

        public BulletinPretController()
        {
            recalc = new SimpleAction(this, "BulletinRecalcul", PredefinedCategory.Edit)
            {
                Caption = "Recalculer",
                ImageName = "Action_Calculate",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            recalc.Execute += (s, e) =>
            {
                var b = View.CurrentObject as Bulletin;
                if (b == null) return;
                b.RecalculerCotisationsEtTotaux();
                ObjectSpace.CommitChanges();
                Application.ShowViewStrategy.ShowMessage("Recalcul terminé.", InformationType.Success, 2500);
                View.ObjectSpace.Refresh();
            };

            validerPrets = new SimpleAction(this, "ValiderRemboursementsPrets", PredefinedCategory.Edit)
            {
                Caption = "Valider remboursements (prêts)",
                ImageName = "Action_Import",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            validerPrets.Execute += (s, e) =>
            {
                var b = View.CurrentObject as Bulletin;
                if (b == null) return;
                b.ValiderRemboursementsPrets(); // marque les échéances du mois comme prélevées
                ObjectSpace.CommitChanges();
                Application.ShowViewStrategy.ShowMessage("Remboursements validés.", InformationType.Success, 2500);
                View.ObjectSpace.Refresh();
            };
        }
    }
}
