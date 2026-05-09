// =============================================================================
//  ProvisionCongesController.cs — V1.7 — Charge la provision congés annuelle
//
//  Pattern XAF canonique : hook ObjectsGetting du NonPersistentObjectSpace.
//  Année par défaut : N-1 (l'année dernière clôturée). Les RH peuvent ajuster
//  via filtres XAF de la ListView (filter sur Annee).
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class ProvisionCongesController
        : ObjectViewController<ListView, ProvisionConges>
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
            if (e.ObjectType != typeof(ProvisionConges)) return;

            using var persistentOs = Application.CreateObjectSpace(typeof(Salarie));

            // Calcul pour les 3 dernières années (N-2, N-1, N)
            // → l'utilisateur peut filtrer sur Annee dans la ListView
            int anneeCourante = DateTime.Today.Year;
            var allLignes = new System.Collections.Generic.List<ProvisionConges>();
            for (int delta = -2; delta <= 0; delta++)
            {
                var lignes = ProvisionCongesService.Calculer(
                    persistentOs, _nonPersistentOs, anneeCourante + delta);
                allLignes.AddRange(lignes);
            }

            e.Objects = new System.ComponentModel.BindingList<ProvisionConges>(allLignes);
        }
    }
}
