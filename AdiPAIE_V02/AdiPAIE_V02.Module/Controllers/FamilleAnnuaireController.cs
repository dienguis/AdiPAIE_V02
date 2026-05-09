// =============================================================================
//  FamilleAnnuaireController.cs — V1.7 — Charge l'annuaire à l'ouverture
//
//  Pattern XAF canonique pour les NonPersistentObjectSpace :
//    1. À l'activation, on récupère le NonPersistentObjectSpace sous-jacent
//       (dans CompositeObjectSpace.AdditionalObjectSpaces ou directement)
//    2. On s'abonne à l'event ObjectsGetting qui est appelé quand XAF a besoin
//       des données pour peupler la vue
//    3. Dans le handler, on lit les données depuis un OS persistant frais
//       et on retourne la collection FamilleAnnuaire
//
//  Pourquoi cet event : le pipeline XAF Blazor déclenche ObjectsGetting au bon
//  moment du cycle de vie de la vue, contrairement à OnActivated qui peut être
//  trop tôt (avant que CollectionSource soit prêt).
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class FamilleAnnuaireController
        : ObjectViewController<ListView, FamilleAnnuaire>
    {
        private NonPersistentObjectSpace _nonPersistentOs;

        protected override void OnActivated()
        {
            base.OnActivated();

            _nonPersistentOs = GetNonPersistentObjectSpace(ObjectSpace);
            if (_nonPersistentOs != null)
            {
                _nonPersistentOs.ObjectsGetting += NonPersistentOs_ObjectsGetting;
            }
        }

        protected override void OnDeactivated()
        {
            if (_nonPersistentOs != null)
            {
                _nonPersistentOs.ObjectsGetting -= NonPersistentOs_ObjectsGetting;
                _nonPersistentOs = null;
            }
            base.OnDeactivated();
        }

        // ──────────────────────────────────────────────────────────────
        //  Récupère le NonPersistentObjectSpace, qu'il soit direct ou
        //  dans une CompositeObjectSpace (config XAF Blazor avec
        //  NonPersistentObjectSpaceProvider enregistré).
        // ──────────────────────────────────────────────────────────────
        private static NonPersistentObjectSpace GetNonPersistentObjectSpace(IObjectSpace os)
        {
            if (os is NonPersistentObjectSpace direct) return direct;
            if (os is CompositeObjectSpace composite)
            {
                return composite.AdditionalObjectSpaces
                    .OfType<NonPersistentObjectSpace>()
                    .FirstOrDefault();
            }
            return null;
        }

        // ──────────────────────────────────────────────────────────────
        //  Handler appelé par XAF quand il a besoin des objets à afficher.
        //  On peuple e.Objects avec la liste FamilleAnnuaire chargée à
        //  partir d'un OS persistant frais.
        // ──────────────────────────────────────────────────────────────
        private void NonPersistentOs_ObjectsGetting(
            object sender, ObjectsGettingEventArgs e)
        {
            if (e.ObjectType != typeof(FamilleAnnuaire)) return;

            // OS persistant dédié pour lire Salaries / Conjoints / Enfants
            using var persistentOs = Application.CreateObjectSpace(typeof(Salarie));

            // Charger toutes les lignes de l'annuaire
            var lignes = FamilleAnnuaireService.LoaderAnnuaire(
                persistentOs, _nonPersistentOs, actifsUniquement: true);

            // Retourner la collection à XAF (qui va peupler la vue)
            var bindingList = new System.ComponentModel.BindingList<FamilleAnnuaire>(lignes);
            e.Objects = bindingList;
        }
    }
}
