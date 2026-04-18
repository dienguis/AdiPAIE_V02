using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Controllers;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Filtre la ListView EntretienAnnuel selon le profil connecté.
    ///
    /// Salarié → voit uniquement SON entretien (>= PlanifieRH)
    /// Manager → voit les entretiens dont il est l'Évaluateur + le sien
    /// RH / Admin → ne touche pas au filtre (voit tout)
    /// </summary>
    public class EntretienFilterController
        : ObjectViewController<ListView, EntretienAnnuel>
    {
        private const string FilterKey = "EntretienFilter";

        protected override void OnActivated()
        {
            base.OnActivated();

            // Récupérer le salarié lié à l'utilisateur
            var salConn = EspaceSalarieHelper.GetSalarieConnecte(ObjectSpace);
            if (salConn == null)
                return; // Pas de salarié lié → ne rien toucher

            // RH : pas de filtre → return sans toucher aux criteria
            if (!EspaceSalarieHelper.DoitRestreindreEspaceSalarie(ObjectSpace))
                return;

            // Statuts visibles par le salarié/manager (pas Brouillon)
            var statutsVisibles = CriteriaOperator.Parse(
                "Statut != ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+EntretienStatut,Brouillon#");

            // Son propre entretien (>= PlanifieRH)
            var critPropre = new GroupOperator(GroupOperatorType.And,
                CriteriaOperator.Parse("Salarie.Oid = ?", salConn.Oid),
                statutsVisibles);

            // Entretiens dont il est évaluateur (manager)
            var critEvaluateur = CriteriaOperator.Parse(
                "Evaluateur.Oid = ?", salConn.Oid);

            // Combine avec OR
            View.CollectionSource.Criteria[FilterKey] =
                new GroupOperator(GroupOperatorType.Or,
                    critPropre,
                    critEvaluateur);
        }

        protected override void OnDeactivated()
        {
            if (View?.CollectionSource != null)
                View.CollectionSource.Criteria.Remove(FilterKey);
            base.OnDeactivated();
        }
    }
}
