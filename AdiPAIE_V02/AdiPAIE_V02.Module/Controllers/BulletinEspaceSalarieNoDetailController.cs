// =============================================================================
//  BulletinEspaceSalarieNoDetailController.cs — V1.4.3
//
//  Bloque l'accès au DetailView du bulletin sur Bulletin_EspaceSalarie_ListView.
//  Pourquoi : on ne veut pas que le salarié voie l'écran de détail technique
//  (avec rubriques, lignes de calcul, métadonnées internes…). L'unique moyen
//  d'accéder à un bulletin doit être le bouton "Télécharger" → PDF.
//
//  Mécanisme : on désactive ListViewProcessCurrentObjectController.ProcessCurrentObjectAction
//  (= le double-clic / clic central qui ouvre le détail) sur cette vue.
// =============================================================================
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.SystemModule;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class BulletinEspaceSalarieNoDetailController
        : ObjectViewController<ListView, Bulletin>
    {
        private const string ReasonKey = "EspaceSalarieNoDrillDown";

        public BulletinEspaceSalarieNoDetailController()
        {
            TargetViewId = "Bulletin_EspaceSalarie_ListView";
        }

        protected override void OnActivated()
        {
            base.OnActivated();

            var proc = Frame.GetController<ListViewProcessCurrentObjectController>();
            if (proc != null)
            {
                proc.Active.SetItemValue(ReasonKey, false);
                if (proc.ProcessCurrentObjectAction != null)
                    proc.ProcessCurrentObjectAction.Active.SetItemValue(ReasonKey, false);
            }

            var newCtl = Frame.GetController<NewObjectViewController>();
            if (newCtl != null)
                newCtl.Active.SetItemValue(ReasonKey, false);
        }

        protected override void OnDeactivated()
        {
            var proc = Frame.GetController<ListViewProcessCurrentObjectController>();
            if (proc != null)
            {
                proc.Active.RemoveItem(ReasonKey);
                proc.ProcessCurrentObjectAction?.Active.RemoveItem(ReasonKey);
            }

            var newCtl = Frame.GetController<NewObjectViewController>();
            if (newCtl != null)
                newCtl.Active.RemoveItem(ReasonKey);

            base.OnDeactivated();
        }
    }
}
