using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Filtre la ListView Bulletin pour l'espace salarié.
    ///
    ///   Salarié connecté → voit uniquement SES bulletins (Validé ou Envoyé)
    ///   RH / Admin       → voit tout (aucun filtre appliqué ici)
    ///
    /// Pattern identique à EntretienFilterController et FormationFilterController.
    /// </summary>
    public class BulletinSalarieFilterController
        : ObjectViewController<ListView, Bulletin>
    {
        protected override void OnActivated()
        {
            base.OnActivated();
            AppliquerFiltre();
        }

        private void AppliquerFiltre()
        {
            var session = ((XPObjectSpace)ObjectSpace).Session;
            var userName = DevExpress.ExpressApp.SecuritySystem.CurrentUserName;

            var user = new XPQuery<ApplicationUser>(session)
                .FirstOrDefault(u => u.UserName == userName);

            // Pas de fiche salarié → profil RH/Admin — aucun filtre
            if (user?.Salarie == null)
            {
                View.CollectionSource.Criteria["BulletinSalarieFilter"] = null;
                return;
            }

            var salConn = user.Salarie;

            // Salarié voit uniquement ses bulletins Validé ou Envoyé
            //View.CollectionSource.Criteria["BulletinSalarieFilter"] =
            //    new GroupOperator(GroupOperatorType.And,
            //        CriteriaOperator.Parse("Salarie = ?", salConn),
            //        CriteriaOperator.Parse(
            //            "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+BulletinStatut,Valide# " +
            //            "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+BulletinStatut,Envoye# " +
            //            "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+BulletinStatut,Cloture#"));

            View.CollectionSource.Criteria["BulletinSalarieFilter"] =
    new GroupOperator(GroupOperatorType.And,
        CriteriaOperator.Parse("Salarie = ?", salConn),
        CriteriaOperator.Parse(
            "Statut != ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+BulletinStatut,Brouillon#"));
        }
    }
}
