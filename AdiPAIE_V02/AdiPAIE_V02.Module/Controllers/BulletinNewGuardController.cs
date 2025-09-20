// [PER] [BUL]
// Guard: interdire la création de Bulletins s'il n'existe aucune Période de paie "Ouverte".
// Effet : grise (désactive) l'action "Nouveau" sur toutes les ListView de Bulletin.

using System;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.SystemModule;
using AdiPAIE_V02.Module.BusinessObjects;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    public class BulletinNewGuardController : ViewController<ListView>
    {
        private NewObjectViewController newCtl;

        public BulletinNewGuardController()
        {
            TargetObjectType = typeof(Bulletin);
            TargetViewType = ViewType.ListView; // inclut les listes imbriquées
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            newCtl = Frame.GetController<NewObjectViewController>();
            if (newCtl != null)
            {
                UpdateNewState();
                var refreshCtl = Frame.GetController<RefreshController>();
                if (refreshCtl != null)
                    refreshCtl.RefreshAction.Execute += (_, __) => UpdateNewState();
            }
        }

        protected override void OnViewControlsCreated()
        {
            base.OnViewControlsCreated();
            UpdateNewState();
        }

        protected override void OnDeactivated()
        {
            base.OnDeactivated();
            newCtl = null;
        }

        private void UpdateNewState()
        {
            if (newCtl == null) return;

            using var os = Application.CreateObjectSpace(typeof(PeriodePaie));
            bool hasOpen = os.GetObjectsQuery<PeriodePaie>()
                             .Any(p => p.Statut == PeriodePaieStatut.Ouverte);

            // Désactiver l'action (grisée). Pour la cacher, voir variante plus bas.
            newCtl.NewObjectAction.Enabled["PeriodeOpenRequired"] = hasOpen;

            newCtl.NewObjectAction.ToolTip = hasOpen
                ? "Créer un nouveau bulletin."
                : "Aucune période de paie Ouverte.\nCréez/ouvrez d’abord une Période (Menu : Périodes de paie).";

            newCtl.NewObjectAction.Caption = hasOpen ? "Nouveau" : "Nouveau (période requise)";
        }
    }
}
