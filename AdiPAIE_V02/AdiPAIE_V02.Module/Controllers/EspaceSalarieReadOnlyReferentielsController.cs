using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Passe les fiches de référence en lecture seule pour les salariés.
    /// Empêche un employé de modifier CongeType, CategorieFrais, VilleSenegal, etc.
    /// s'il y accède via un lien dans une ListView.
    ///
    /// RH / Admin → aucune restriction.
    /// </summary>
    public class CongeTypeReadOnlyController
        : ObjectViewController<DetailView, CongeType>
    {
        private const string ReasonKey = "EspaceSalarieRefReadOnly";

        protected override void OnActivated()
        {
            base.OnActivated();
            if (EspaceSalarieHelper.EstSalarieConnecte(ObjectSpace))
                View.AllowEdit[ReasonKey] = false;
        }

        protected override void OnDeactivated()
        {
            View.AllowEdit.RemoveItem(ReasonKey);
            base.OnDeactivated();
        }
    }

    public class VilleSenegalReadOnlyController
        : ObjectViewController<DetailView, VilleSenegal>
    {
        private const string ReasonKey = "EspaceSalarieRefReadOnly";

        protected override void OnActivated()
        {
            base.OnActivated();
            if (EspaceSalarieHelper.EstSalarieConnecte(ObjectSpace))
                View.AllowEdit[ReasonKey] = false;
        }

        protected override void OnDeactivated()
        {
            View.AllowEdit.RemoveItem(ReasonKey);
            base.OnDeactivated();
        }
    }

    public class CategorieFraisReadOnlyController
        : ObjectViewController<DetailView, CategorieFraisMission>
    {
        private const string ReasonKey = "EspaceSalarieRefReadOnly";

        protected override void OnActivated()
        {
            base.OnActivated();
            if (EspaceSalarieHelper.EstSalarieConnecte(ObjectSpace))
                View.AllowEdit[ReasonKey] = false;
        }

        protected override void OnDeactivated()
        {
            View.AllowEdit.RemoveItem(ReasonKey);
            base.OnDeactivated();
        }
    }
}
