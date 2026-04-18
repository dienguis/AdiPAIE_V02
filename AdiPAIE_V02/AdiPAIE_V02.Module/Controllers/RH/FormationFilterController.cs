using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    // ══════════════════════════════════════════════════════════════════════
    // FILTRE INSCRIPTIONS
    //   Salarié    → uniquement ses propres inscriptions
    //   Manager    → ses propres + celles de ses subordonnés directs
    //   RH / Admin → toutes
    // ══════════════════════════════════════════════════════════════════════
    public class InscriptionFormationFilterController
        : ObjectViewController<ListView, InscriptionFormation>
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
            var salConn = user?.Salarie;

            if (salConn == null)
            {
                // RH / Admin — voit tout
                View.CollectionSource.Criteria["InscriptionFilter"] = null;
                return;
            }

            // Salarié voit ses propres inscriptions
            var critPropre = CriteriaOperator.Parse("Salarie.Oid = ?", salConn.Oid);

            // Manager voit aussi les inscriptions de ses subordonnés directs
            // (où salarie.Manager = salConn)
            var critSubordonne = CriteriaOperator.Parse(
                "Salarie.Manager.Oid = ?", salConn.Oid);

            View.CollectionSource.Criteria["InscriptionFilter"] =
                new GroupOperator(GroupOperatorType.Or,
                    critPropre, critSubordonne);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // FILTRE SUIVIS DE FORMATION
    //   Salarié    → uniquement son propre suivi
    //   Manager    → son suivi + ceux de ses subordonnés directs
    //   RH / Admin → tous
    // ══════════════════════════════════════════════════════════════════════
    public class SuiviFormationFilterController
        : ObjectViewController<ListView, SuiviFormation>
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
            var salConn = user?.Salarie;

            if (salConn == null)
            {
                View.CollectionSource.Criteria["SuiviFilter"] = null;
                return;
            }

            var critPropre = CriteriaOperator.Parse("Salarie.Oid = ?", salConn.Oid);
            var critSubordonne = CriteriaOperator.Parse(
                "Salarie.Manager.Oid = ?", salConn.Oid);

            View.CollectionSource.Criteria["SuiviFilter"] =
                new GroupOperator(GroupOperatorType.Or,
                    critPropre, critSubordonne);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // FILTRE SESSIONS DE FORMATION
    //   Salarié    → uniquement les sessions Confirmées ou En cours
    //               (pas les sessions Planifiée/Annulée — pas encore publiques)
    //   Manager    → idem
    //   RH / Admin → toutes (aucun filtre)
    // ══════════════════════════════════════════════════════════════════════
    public class SessionFormationFilterController
        : ObjectViewController<ListView, SessionFormation>
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

            if (user?.Salarie == null)
            {
                // RH / Admin — voit tout
                View.CollectionSource.Criteria["SessionFilter"] = null;
                return;
            }

            View.CollectionSource.Criteria["SessionFilter"] =
    CriteriaOperator.Parse(
        "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,Confirmee# " +
        "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,EnCours#");

        }
    }
}
