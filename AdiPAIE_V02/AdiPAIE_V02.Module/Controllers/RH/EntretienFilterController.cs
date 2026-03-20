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
    /// <summary>
    /// Filtre la ListView EntretienAnnuel selon le profil connecté.
    ///
    /// Salarié → voit uniquement SON entretien,
    ///           ET seulement si statut >= PlanifieRH
    ///           (pas Brouillon — l'entretien n'est pas encore envoyé)
    ///
    /// Manager → voit les entretiens dont il est l'Évaluateur
    ///           + son propre entretien (>= PlanifieRH)
    ///
    /// RH / Admin → voit tout
    /// </summary>
    public class EntretienFilterController
        : ObjectViewController<ListView, EntretienAnnuel>
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
                // ── RH / Admin — voit tout ─────────────────────
                View.CollectionSource.Criteria["EntretienFilter"] = null;
                return;
            }

            // Statuts visibles par le salarié/manager (pas Brouillon)
            var statutsVisibles = CriteriaOperator.Parse(
                "Statut != ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,Brouillon#");

            // Son propre entretien (>= PlanifieRH)
            var critPropre = new GroupOperator(GroupOperatorType.And,
                CriteriaOperator.Parse("Salarie = ?", salConn),
                statutsVisibles);

            // Entretiens dont il est évaluateur (manager)
            var critEvaluateur = CriteriaOperator.Parse(
                "Evaluateur = ?", salConn);

            // Combine avec OR
            View.CollectionSource.Criteria["EntretienFilter"] =
                new GroupOperator(GroupOperatorType.Or,
                    critPropre,
                    critEvaluateur);
        }
    }
}