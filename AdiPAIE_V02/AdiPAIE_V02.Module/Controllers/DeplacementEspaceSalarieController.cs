using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Espace salarié : auto-remplit et grise le champ Salarié
    /// sur la DetailView DemandeDeplacement pour un employé connecté.
    /// RH / Admin → aucune restriction.
    /// </summary>
    public class DeplacementEspaceSalarieController
        : ObjectViewController<DetailView, DemandeDeplacement>
    {
        private const string ReasonKey = "EspaceSalarieDeplacement";

        protected override void OnActivated()
        {
            base.OnActivated();

            if (!EspaceSalarieHelper.EstSalarieConnecte(ObjectSpace))
                return;

            var demande = (DemandeDeplacement)View.CurrentObject;
            if (demande == null) return;

            // Auto-remplir le champ Salarié
            if (demande.Salarie == null)
            {
                var sal = EspaceSalarieHelper.GetSalarieConnecte(ObjectSpace);
                if (sal != null)
                    demande.Salarie = (Salarie)ObjectSpace.GetObject(sal);
            }

            // Auto-remplir ValideurN1 depuis le Manager (N+1) du salarié
            if (demande.ValideurN1 == null && demande.Salarie?.Manager != null)
            {
                demande.ValideurN1 = (Salarie)ObjectSpace.GetObject(demande.Salarie.Manager);
            }

            // Griser les champs Salarié et ValideurN1
            var item = View.FindItem("Salarie");
            if (item is PropertyEditor pe)
                pe.AllowEdit[ReasonKey] = false;

            var itemN1 = View.FindItem("ValideurN1");
            if (itemN1 is PropertyEditor peN1)
                peN1.AllowEdit[ReasonKey] = false;

            // ── Protéger les demandes déjà soumises (pas en Brouillon) ──
            if (demande.Statut != DeplacementStatut.Brouillon)
            {
                MasquerAction("Delete");
                MasquerAction("Save");
                MasquerAction("SaveAndClose");
                MasquerAction("SaveAndNew");
                View.AllowEdit[ReasonKey] = false;
            }
        }

        private void MasquerAction(string actionId)
        {
            foreach (var controller in Frame.Controllers)
                foreach (ActionBase action in controller.Actions)
                    if (action.Id == actionId)
                        action.Active[ReasonKey] = false;
        }

        protected override void OnDeactivated()
        {
            var item = View?.FindItem("Salarie");
            if (item is PropertyEditor pe)
                pe.AllowEdit.RemoveItem(ReasonKey);

            base.OnDeactivated();
        }
    }
}
