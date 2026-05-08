// =============================================================================
//  DemandeMouvementInterimAcFilterController.cs — V1.5
//
//  Sur la vue DemandeMouvementInterim_AC_ListView (menu "Mon espace"),
//  applique 2 filtres systématiques :
//
//    1. Initiateur.SystemUser.UserName = currentUserName
//       → l'AC ne voit que SES propres demandes
//
//    2. À la création d'une nouvelle demande, on auto-renseigne Initiateur =
//       Salarie lié à l'utilisateur courant. Évite d'avoir à le saisir.
//
//  Sécurité côté permissions XAF :
//    - Le rôle AssistantCommercial a déjà un ObjectPermission qui restreint
//      "Initiateur.SystemUser.UserName = CurrentUserName()". Le filtre ici
//      est donc un confort UX (la liste s'affiche correctement) ; la sécurité
//      est garantie par le RBAC.
// =============================================================================
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Controllers;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using System;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    public sealed class DemandeMouvementInterimAcFilterController
        : ObjectViewController<ListView, DemandeMouvementInterim>
    {
        private const string FilterKey = "DMI_AC_Filter";

        public DemandeMouvementInterimAcFilterController()
        {
            TargetViewId = "DemandeMouvementInterim_AC_ListView";
        }

        protected override void OnActivated()
        {
            base.OnActivated();

            // Récupère le Salarié lié à l'utilisateur courant via le helper
            // partagé avec l'Espace Salarié.
            var salarieAc = EspaceSalarieHelper.GetSalarieConnecte(ObjectSpace);

            // 1) Filtre liste : seulement mes demandes
            // (la sécurité réelle est garantie par les ObjectPermissions XAF
            // du rôle AssistantCommercial — ce filtre est UX uniquement)
            try
            {
                if (View?.CollectionSource != null && salarieAc != null)
                {
                    View.CollectionSource.Criteria[FilterKey] =
                        CriteriaOperator.Parse(
                            "Initiateur.Oid = ?", salarieAc.Oid);
                }
            }
            catch
            {
                // Pas bloquant — RBAC fait déjà le filtrage côté DB
            }

            // 2) Auto-rempli Initiateur à la création (hook ObjectSpace)
            if (ObjectSpace != null)
                ObjectSpace.ObjectChanged += ObjectSpace_ObjectChanged;
        }

        protected override void OnDeactivated()
        {
            if (View?.CollectionSource != null)
                View.CollectionSource.Criteria.Remove(FilterKey);

            if (ObjectSpace != null)
                ObjectSpace.ObjectChanged -= ObjectSpace_ObjectChanged;

            base.OnDeactivated();
        }

        private void ObjectSpace_ObjectChanged(object sender, ObjectChangedEventArgs e)
        {
            // Quand un nouvel objet DemandeMouvementInterim apparaît dans l'OS
            // (suite au New de la ListView), on auto-rempli Initiateur.
            if (e.Object is DemandeMouvementInterim d
                && d.Initiateur == null
                && ObjectSpace.IsNewObject(d))
            {
                try
                {
                    var salarieAc = EspaceSalarieHelper.GetSalarieConnecte(ObjectSpace);
                    if (salarieAc != null)
                        d.Initiateur = salarieAc;
                }
                catch
                {
                    // Pas bloquant
                }
            }
        }
    }
}
