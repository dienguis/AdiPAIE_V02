using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Filtre la ListView SoldeConge pour l'espace salarie.
    ///
    ///   Salarie pur → voit uniquement SES soldes actifs (annee en cours et N-1)
    ///   RH          → voit tout (aucun filtre)
    ///   Autres      → ne touche pas au filtre
    /// </summary>
    public class SoldeCongeEspaceSalarieController
        : ObjectViewController<ListView, SoldeConge>
    {
        private const string FilterKey = "SoldeCongeEspaceFilter";

        protected override void OnActivated()
        {
            base.OnActivated();

            // Étape 1 : récupérer le salarié connecté
            var salConn = EspaceSalarieHelper.GetSalarieConnecte(ObjectSpace);

            // Pas de salarié lié → ne rien toucher (admin pur sans fiche salarié)
            if (salConn == null)
                return;

            // Étape 2 : vérifier si on doit restreindre (true = pas RH)
            if (!EspaceSalarieHelper.DoitRestreindreEspaceSalarie(ObjectSpace))
                return;

            // Étape 3 : appliquer le filtre — l'employé voit uniquement SES soldes
            var annee = System.DateTime.Today.Year;

            View.CollectionSource.Criteria[FilterKey] =
                new GroupOperator(GroupOperatorType.And,
                    CriteriaOperator.Parse("Salarie.Oid = ?", salConn.Oid),
                    CriteriaOperator.Parse("Statut = ?", (int)SoldeCongeStatut.Actif),
                    new GroupOperator(GroupOperatorType.Or,
                        CriteriaOperator.Parse("Annee = ?", annee),
                        CriteriaOperator.Parse("Annee = ?", annee - 1)));
        }

        protected override void OnDeactivated()
        {
            if (View?.CollectionSource != null)
                View.CollectionSource.Criteria.Remove(FilterKey);
            base.OnDeactivated();
        }
    }
}
