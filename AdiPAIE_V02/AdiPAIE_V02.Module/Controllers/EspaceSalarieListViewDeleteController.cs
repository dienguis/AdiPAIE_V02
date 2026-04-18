using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using System.Collections;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Protège le bouton Delete sur les ListViews "Mon espace" :
    /// la suppression n'est autorisée que si TOUTES les demandes
    /// sélectionnées sont en Brouillon.
    ///
    /// S'applique à : DemandeDeplacement, CongeDemande, DemandeAttestation.
    /// Ne touche à rien pour RH / Admin.
    /// </summary>
    public class DeplacementListViewDeleteController
        : ObjectViewController<ListView, DemandeDeplacement>
    {
        private const string ReasonKey = "EspaceSalarieDeleteProtect";

        protected override void OnActivated()
        {
            base.OnActivated();
            if (!EspaceSalarieHelper.EstSalarieConnecte(ObjectSpace))
                return;
            View.SelectionChanged += View_SelectionChanged;
            EvaluerDelete();
        }

        protected override void OnDeactivated()
        {
            View.SelectionChanged -= View_SelectionChanged;
            foreach (var c in Frame.Controllers)
                foreach (ActionBase a in c.Actions)
                    a.Active.RemoveItem(ReasonKey);
            base.OnDeactivated();
        }

        private void View_SelectionChanged(object sender, System.EventArgs e)
        {
            EvaluerDelete();
        }

        private void EvaluerDelete()
        {
            var selected = View.SelectedObjects;
            bool peutSupprimer = true;

            if (selected == null || selected.Count == 0)
            {
                peutSupprimer = false;
            }
            else
            {
                foreach (var obj in selected)
                {
                    if (obj is DemandeDeplacement d && d.Statut != DeplacementStatut.Brouillon)
                    {
                        peutSupprimer = false;
                        break;
                    }
                }
            }

            foreach (var c in Frame.Controllers)
                foreach (ActionBase a in c.Actions)
                    if (a.Id == "Delete")
                        a.Active[ReasonKey] = peutSupprimer;
        }
    }

    /// <summary>
    /// Même protection pour CongeDemande ListView.
    /// </summary>
    public class CongeListViewDeleteController
        : ObjectViewController<ListView, CongeDemande>
    {
        private const string ReasonKey = "EspaceSalarieDeleteProtect";

        protected override void OnActivated()
        {
            base.OnActivated();
            if (!EspaceSalarieHelper.EstSalarieConnecte(ObjectSpace))
                return;
            View.SelectionChanged += View_SelectionChanged;
            EvaluerDelete();
        }

        protected override void OnDeactivated()
        {
            View.SelectionChanged -= View_SelectionChanged;
            foreach (var c in Frame.Controllers)
                foreach (ActionBase a in c.Actions)
                    a.Active.RemoveItem(ReasonKey);
            base.OnDeactivated();
        }

        private void View_SelectionChanged(object sender, System.EventArgs e)
        {
            EvaluerDelete();
        }

        private void EvaluerDelete()
        {
            var selected = View.SelectedObjects;
            bool peutSupprimer = true;

            if (selected == null || selected.Count == 0)
            {
                peutSupprimer = false;
            }
            else
            {
                foreach (var obj in selected)
                {
                    if (obj is CongeDemande d && d.Statut != CongeStatut.Brouillon)
                    {
                        peutSupprimer = false;
                        break;
                    }
                }
            }

            foreach (var c in Frame.Controllers)
                foreach (ActionBase a in c.Actions)
                    if (a.Id == "Delete")
                        a.Active[ReasonKey] = peutSupprimer;
        }
    }

    /// <summary>
    /// Même protection pour DemandeAttestation ListView.
    /// Aucun statut "Brouillon" n'existe — le premier statut est EnAttenteN1.
    /// Donc on bloque la suppression dès que le statut >= Soumise.
    /// Pour les demandes en EnAttenteN1/N2, l'employé peut encore supprimer.
    /// </summary>
    public class AttestationListViewDeleteController
        : ObjectViewController<ListView, DemandeAttestation>
    {
        private const string ReasonKey = "EspaceSalarieDeleteProtect";

        protected override void OnActivated()
        {
            base.OnActivated();
            if (!EspaceSalarieHelper.EstSalarieConnecte(ObjectSpace))
                return;
            View.SelectionChanged += View_SelectionChanged;
            EvaluerDelete();
        }

        protected override void OnDeactivated()
        {
            View.SelectionChanged -= View_SelectionChanged;
            foreach (var c in Frame.Controllers)
                foreach (ActionBase a in c.Actions)
                    a.Active.RemoveItem(ReasonKey);
            base.OnDeactivated();
        }

        private void View_SelectionChanged(object sender, System.EventArgs e)
        {
            EvaluerDelete();
        }

        private void EvaluerDelete()
        {
            var selected = View.SelectedObjects;
            bool peutSupprimer = true;

            if (selected == null || selected.Count == 0)
            {
                peutSupprimer = false;
            }
            else
            {
                foreach (var obj in selected)
                {
                    if (obj is DemandeAttestation d && d.Statut >= DemandeStatut.Soumise)
                    {
                        peutSupprimer = false;
                        break;
                    }
                }
            }

            foreach (var c in Frame.Controllers)
                foreach (ActionBase a in c.Actions)
                    if (a.Id == "Delete")
                        a.Active[ReasonKey] = peutSupprimer;
        }
    }
}
