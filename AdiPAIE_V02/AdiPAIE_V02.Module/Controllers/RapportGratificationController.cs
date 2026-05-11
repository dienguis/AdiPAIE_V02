// =============================================================================
//  RapportGratificationController.cs — V1.7.2e
//
//  Hook NonPersistentObjectSpace pour peupler la ListView de
//  RapportGratification.
//
//  Charge N et N-1 par défaut — filtrage UI possible.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class RapportGratificationController
        : ObjectViewController<ListView, RapportGratification>
    {
        private NonPersistentObjectSpace _nonPersistentOs;

        protected override void OnActivated()
        {
            base.OnActivated();
            _nonPersistentOs = GetNonPersistentObjectSpace(ObjectSpace);
            if (_nonPersistentOs != null)
                _nonPersistentOs.ObjectsGetting += NonPersistentOs_ObjectsGetting;
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

        private void NonPersistentOs_ObjectsGetting(
            object sender, ObjectsGettingEventArgs e)
        {
            if (e.ObjectType != typeof(RapportGratification)) return;

            using var persistentOs = Application.CreateObjectSpace(typeof(Salarie));

            // Charge N et N-1 par défaut — l'utilisateur filtre dans la UI
            int anneeCourante = DateTime.Today.Year;
            var allLignes = new System.Collections.Generic.List<RapportGratification>();

            for (int delta = -1; delta <= 0; delta++)
            {
                var lignes = RapportGratificationService.Calculer(
                    persistentOs, _nonPersistentOs, anneeCourante + delta);
                allLignes.AddRange(lignes);
            }

            e.Objects = new System.ComponentModel.BindingList<RapportGratification>(allLignes);
        }
    }
}
