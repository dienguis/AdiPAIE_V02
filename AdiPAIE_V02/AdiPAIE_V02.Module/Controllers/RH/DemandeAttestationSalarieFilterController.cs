using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.Xpo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    public class DemandeAttestationSalarieFilterController
        : ObjectViewController<ListView, DemandeAttestation>
    {
        //protected override void OnActivated()
        //{
        //    base.OnActivated();

        //    // Ne filtrer que si ce n'est pas le RH qui consulte
        //    if (SecuritySystem.IsGranted(
        //        new PermissionRequest(typeof(DemandeAttestation),
        //            SecurityOperations.Read)))
        //        return; // RH voit tout

        //    var session = ((XPObjectSpace)ObjectSpace).Session;
        //    var currentUser = session.FindObject<ApplicationUser>(
        //        CriteriaOperator.Parse("UserName = ?",
        //            SecuritySystem.CurrentUserName));

        //    if (currentUser?.Salarie != null)
        //    {
        //        View.CollectionSource.Criteria["SalarieFilter"] =
        //            CriteriaOperator.Parse("Salarie = ?", currentUser.Salarie);
        //    }
        //}

        protected override void OnActivated()
        {
            base.OnActivated();

            var session = ((XPObjectSpace)ObjectSpace).Session;
            var currentUser = session.FindObject<ApplicationUser>(
                CriteriaOperator.Parse("UserName = ?",
                    SecuritySystem.CurrentUserName));

            // Si pas de fiche salarié liée → c'est un RH/admin, on ne filtre pas
            if (currentUser?.Salarie == null)
                return;

            // Sinon → salarié connecté, on filtre sur ses demandes uniquement
            View.CollectionSource.Criteria["SalarieFilter"] =
                CriteriaOperator.Parse("Salarie = ?", currentUser.Salarie);
        }


    }
}
