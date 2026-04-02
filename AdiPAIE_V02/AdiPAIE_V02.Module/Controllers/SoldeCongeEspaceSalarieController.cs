using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Filtre la ListView SoldeConge pour l'espace salarié.
    ///
    ///   Salarié    → voit uniquement SES soldes actifs (année en cours)
    ///   RH / Admin → voit tout (aucun filtre)
    ///
    /// Vue en lecture seule — aucune action d'édition.
    /// </summary>
    public class SoldeCongeEspaceSalarieController
        : ObjectViewController<ListView, SoldeConge>
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

            // RH / Admin — aucun filtre
            if (user?.Salarie == null)
            {
                View.CollectionSource.Criteria["SoldeCongeEspaceFilter"] = null;
                return;
            }

            var salConn = user.Salarie;
            var annee = System.DateTime.Today.Year;

            // Salarié voit ses soldes actifs de l'année en cours et N-1
            View.CollectionSource.Criteria["SoldeCongeEspaceFilter"] =
                new GroupOperator(GroupOperatorType.And,
                    CriteriaOperator.Parse("Salarie = ?", salConn),
                    CriteriaOperator.Parse(
                        "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SoldeCongeStatut,Actif#"),
                    CriteriaOperator.Parse(
                        "Annee = ? OR Annee = ?", annee, annee - 1));
        }
    }
}
