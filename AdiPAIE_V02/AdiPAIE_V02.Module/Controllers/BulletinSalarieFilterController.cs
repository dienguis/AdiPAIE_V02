using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Filtre la ListView Bulletin pour l'espace salarie.
    ///
    ///   Salarie connecte -> voit uniquement SES bulletins Envoye / Comptabilise / Cloture
    ///   RH / Admin       -> ne touche pas au filtre (voit tout)
    /// </summary>
    public class BulletinSalarieFilterController
        : ObjectViewController<ListView, Bulletin>
    {
        private const string FilterKey = "BulletinSalarieFilter";

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

            // Employé : ses bulletins + statuts >= Envoye uniquement
            var filtreStatut = new InOperator("Statut", new object[]
            {
                BulletinStatut.Envoye,
                BulletinStatut.Comptabilise,
                BulletinStatut.Cloture
            });

            View.CollectionSource.Criteria[FilterKey] =
                new GroupOperator(GroupOperatorType.And,
                    CriteriaOperator.Parse("Salarie.Oid = ?", salConn.Oid),
                    filtreStatut);
        }

        protected override void OnDeactivated()
        {
            if (View?.CollectionSource != null)
                View.CollectionSource.Criteria.Remove(FilterKey);
            base.OnDeactivated();
        }
    }
}
