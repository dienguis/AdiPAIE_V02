using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Filtre la ListView Bulletin pour l'espace salarie.
    ///
    /// V1.4.3 — Critère de visibilité : DatePublication IS NOT NULL.
    /// C'est la garantie que RH a explicitement publié le bulletin via
    /// BulletinPublicationService.Publier(). Les bulletins legacy
    /// (statut Envoye/Cloture sans DatePublication) ne sont PAS visibles
    /// tant que RH ne les republie pas manuellement.
    ///
    /// Deux modes :
    ///   A) Vue Bulletin_EspaceSalarie_ListView (menu "Mes bulletins") :
    ///      filtre TOUJOURS appliqué — Salarie.Oid = utilisateur courant
    ///      ET DatePublication != null.
    ///
    ///   B) Vue Bulletin_ListView (RH gestion) :
    ///      filtre appliqué uniquement si l'utilisateur connecté est un
    ///      Salarié sans droits RH. RH/Admin voient tout (y compris les
    ///      bulletins en Brouillon, Validé, et publiés).
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

            // V1.4.3 — Sur la vue dédiée Espace Salarié, le filtre est
            // TOUJOURS actif, même pour RH.
            bool estVueEspaceSalarie = string.Equals(
                View?.Id, "Bulletin_EspaceSalarie_ListView",
                System.StringComparison.OrdinalIgnoreCase);

            if (!estVueEspaceSalarie)
            {
                // Vue RH classique : on ne filtre que les vrais utilisateurs salariés
                if (!EspaceSalarieHelper.DoitRestreindreEspaceSalarie(ObjectSpace))
                    return;
            }

            // V1.4.3 — Filtre : ses bulletins ET publiés (DatePublication != null)
            View.CollectionSource.Criteria[FilterKey] =
                CriteriaOperator.Parse(
                    "Salarie.Oid = ? AND DatePublication IS NOT NULL",
                    salConn.Oid);
        }

        protected override void OnDeactivated()
        {
            if (View?.CollectionSource != null)
                View.CollectionSource.Criteria.Remove(FilterKey);
            base.OnDeactivated();
        }
    }
}
