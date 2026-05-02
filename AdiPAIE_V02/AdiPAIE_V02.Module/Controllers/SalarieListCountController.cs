// AdiPAIE_V02.Module/Controllers/SalarieListCountController.cs
// Affiche le nombre de salariés dans le titre de la ListView.
// Met à jour : "Salariés (N)" ou "Salariés (N filtrés / Total T)" si un filtre est appliqué.
//
// Branche-toi sur :
//  - View activée (ouverture de la liste)
//  - Données chargées
//  - Sélection changée (en cas de tri / filtrage rapide)
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    public class SalarieListCountController
        : ObjectViewController<ListView, Salarie>
    {
        private string _captionDeBase;

        protected override void OnActivated()
        {
            base.OnActivated();
            _captionDeBase = View?.Caption ?? "Salariés";

            if (View?.CollectionSource != null)
            {
                View.CollectionSource.CollectionChanged += OnCollectionChanged;
                View.CollectionSource.CriteriaApplied += OnCriteriaApplied;
            }

            UpdateCaption();
        }

        protected override void OnDeactivated()
        {
            if (View?.CollectionSource != null)
            {
                View.CollectionSource.CollectionChanged -= OnCollectionChanged;
                View.CollectionSource.CriteriaApplied -= OnCriteriaApplied;
            }
            base.OnDeactivated();
        }

        private void OnCollectionChanged(object sender, EventArgs e) => UpdateCaption();
        private void OnCriteriaApplied(object sender, EventArgs e) => UpdateCaption();

        private void UpdateCaption()
        {
            if (View == null || ObjectSpace == null) return;

            try
            {
                int affiches = View.CollectionSource?.GetCount() ?? 0;
                int total = ObjectSpace.GetObjectsCount(typeof(Salarie), null);

                string nouveau;
                if (affiches == total)
                    nouveau = $"{_captionDeBase} ({affiches:N0})";
                else
                    nouveau = $"{_captionDeBase} ({affiches:N0} affichés / {total:N0} au total)";

                if (View.Caption != nouveau)
                    View.Caption = nouveau;
            }
            catch
            {
                // En cas de session disposée ou autre, on ne casse pas la vue
            }
        }
    }
}
