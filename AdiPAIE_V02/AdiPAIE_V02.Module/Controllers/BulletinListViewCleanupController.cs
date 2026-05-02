// AdiPAIE_V02.Module/Controllers/BulletinListViewCleanupController.cs
//
// Sur la liste Consultation des Bulletins :
//   1. Désactive les actions globales non pertinentes (Rapport CEO, Workflows, etc.)
//   2. Met à jour le titre avec le compteur "Bulletin (X)" / "Bulletin (X affichés / Y total)"
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Controllers
{
    public class BulletinListViewCleanupController
        : ObjectViewController<ListView, Bulletin>
    {
        private string _captionDeBase;

        // Actions à masquer sur cette vue (IDs des actions globales gênantes)
        private static readonly HashSet<string> ActionsAMasquer = new(StringComparer.OrdinalIgnoreCase)
        {
            "GenererRapportCEO",          // RapportCEOController
            "ShowWorkflowsList",           // Workflow runtime designer (XAF)
            "ShowAllWorkflows",            // idem
            "ShowDiagrams",                // diagrammes XAF
            "ShowWorkflowDefinitions",     // designer workflows
        };

        protected override void OnActivated()
        {
            base.OnActivated();
            _captionDeBase = View?.Caption ?? "Bulletin";

            // 1. Désactiver les actions globales non pertinentes ─────
            DesactiverActionsGlobales();

            // 2. Compteur dans le titre ────────────────────────────────
            if (View?.CollectionSource != null)
            {
                View.CollectionSource.CollectionChanged += OnCollectionChanged;
                View.CollectionSource.CriteriaApplied += OnCriteriaApplied;
            }
            UpdateCaption();
        }

        protected override void OnDeactivated()
        {
            // Réactiver les actions globales (au cas où elles servent ailleurs)
            ReactiverActionsGlobales();

            if (View?.CollectionSource != null)
            {
                View.CollectionSource.CollectionChanged -= OnCollectionChanged;
                View.CollectionSource.CriteriaApplied -= OnCriteriaApplied;
            }
            base.OnDeactivated();
        }

        private void DesactiverActionsGlobales()
        {
            if (Frame == null) return;
            foreach (var ctrl in Frame.Controllers)
            {
                foreach (var act in ctrl.Actions)
                {
                    if (ActionsAMasquer.Contains(act.Id))
                        act.Active.SetItemValue("BulletinListViewCleanup", false);
                }
            }
        }

        private void ReactiverActionsGlobales()
        {
            if (Frame == null) return;
            foreach (var ctrl in Frame.Controllers)
            {
                foreach (var act in ctrl.Actions)
                {
                    if (ActionsAMasquer.Contains(act.Id))
                        act.Active.RemoveItem("BulletinListViewCleanup");
                }
            }
        }

        private void OnCollectionChanged(object sender, EventArgs e) => UpdateCaption();
        private void OnCriteriaApplied(object sender, EventArgs e) => UpdateCaption();

        private void UpdateCaption()
        {
            if (View == null || ObjectSpace == null) return;
            try
            {
                int affiches = View.CollectionSource?.GetCount() ?? 0;
                int total = ObjectSpace.GetObjectsCount(typeof(Bulletin), null);

                string nouveau = (affiches == total)
                    ? $"{_captionDeBase} ({affiches:N0})"
                    : $"{_captionDeBase} ({affiches:N0} affichés / {total:N0} au total)";

                if (View.Caption != nouveau)
                    View.Caption = nouveau;
            }
            catch
            {
                // En cas de session disposée, on ne casse pas la vue
            }
        }
    }
}
