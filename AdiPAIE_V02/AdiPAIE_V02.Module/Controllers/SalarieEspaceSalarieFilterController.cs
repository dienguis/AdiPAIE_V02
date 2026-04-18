using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Filtre la ListView Salarie pour l'espace salarie.
    ///
    ///   Salarie connecte -> voit uniquement SA propre fiche.
    ///   RH / Admin       -> ne touche pas au filtre (voit tout).
    /// </summary>
    public class SalarieEspaceSalarieFilterController
        : ObjectViewController<ListView, Salarie>
    {
        private const string FilterKey = "EspaceSalarieFilter";

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

            // Employé : ne voit que sa propre fiche
            View.CollectionSource.Criteria[FilterKey] =
                CriteriaOperator.Parse("Oid = ?", salConn.Oid);
        }

        protected override void OnDeactivated()
        {
            if (View?.CollectionSource != null)
                View.CollectionSource.Criteria.Remove(FilterKey);
            base.OnDeactivated();
        }
    }
}
