using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Réordonne les lignes d’un bulletin (OrdreCalcul) sans se brancher aux événements de CollectionSource.
    /// - Pose un ordre (max+10) à la création d’une ligne (ObjectSaving)
    /// - Renumérote juste avant le commit (Committing)
    /// - Bouton "Réordonner lignes" pour le faire à la demande
    /// </summary>
    public sealed class BulletinLignesOrderController : ObjectViewController<DetailView, Bulletin>
    {
        private readonly SimpleAction normalizeAction;
        private ListPropertyEditor lignesEditor;

        public BulletinLignesOrderController()
        {
            TargetObjectType = typeof(Bulletin);

            normalizeAction = new SimpleAction(this, "NormalizeBulletinLines", PredefinedCategory.Edit)
            {
                Caption = "Réordonner",
                ImageName = "btn_reordonner",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            normalizeAction.Execute += OnNormalizeExecute; // souscription unique
        }

        protected override void OnActivated()
        {
            base.OnActivated();

            // Éditeur de la collection des lignes (juste pour Refresh après action)
            lignesEditor = View.FindItem(nameof(Bulletin.Lignes)) as ListPropertyEditor;

            // Événements ObjectSpace (pas d’abonnement à CollectionSource.*)
            ObjectSpace.ObjectSaving += ObjectSpace_ObjectSaving;   // détecter nouvelle ligne et poser max+10
            ObjectSpace.Committing += ObjectSpace_Committing;     // renumérotation avant commit
            // (Optionnel) ObjectSpace.ObjectChanged si tu veux renuméroter sur changement d’OrdreCalcul
            // ObjectSpace.ObjectChanged += ObjectSpace_ObjectChanged;
        }

        protected override void OnDeactivated()
        {
            ObjectSpace.ObjectSaving -= ObjectSpace_ObjectSaving;
            ObjectSpace.Committing -= ObjectSpace_Committing;
            // ObjectSpace.ObjectChanged -= ObjectSpace_ObjectChanged;

            normalizeAction.Execute -= OnNormalizeExecute;

            lignesEditor = null;
            base.OnDeactivated();
        }

        // -------------------------------------------------
        // 1) Pose un ordre "max + 10" lors de la création
        // -------------------------------------------------

        private void ObjectSpace_ObjectSaving(object sender, ObjectManipulatingEventArgs e)
        {
            if (e.Object is BulletinLigne bl && ObjectSpace.IsNewObject(bl) && bl.Bulletin != null)
            {
                var max = bl.Bulletin.Lignes
                    .Where(x => !ReferenceEquals(x, bl) && x.OrdreCalcul.HasValue)
                    .Select(x => x.OrdreCalcul.Value)
                    .DefaultIfEmpty(0)
                    .Max();

                if (!bl.OrdreCalcul.HasValue || bl.OrdreCalcul.Value <= 0)
                    bl.OrdreCalcul = max + 10;

                ObjectSpace.SetModified(bl);
            }
        }


        // ----------------------------------------------------------------
        // 2) Renumérote toutes les lignes juste avant le CommitChanges()
        // ----------------------------------------------------------------

        private void ObjectSpace_Committing(object sender, EventArgs e)
        {
            if (View?.CurrentObject is Bulletin b)
            {
                NormalizeAndMarkModified(b);
            }
        }

        private void LignesEditor_ControlCreated(object sender, EventArgs e)
        {
            ApplyAscendingSort();
        }

        // (Optionnel) Renumérotation si OrdreCalcul modifié à la main :
        // private void ObjectSpace_ObjectChanged(object sender, ObjectChangedEventArgs e)
        // {
        //     if (e.Object is BulletinLigne && e.PropertyName == nameof(BulletinLigne.OrdreCalcul))
        //     {
        //         if (View?.CurrentObject is Bulletin b) Normalize(b);
        //     }
        // }

        // -------------------------------------------------
        // 3) Action manuelle pour réordonner immédiatement
        // -------------------------------------------------
        private void OnNormalizeExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var b = View?.CurrentObject as Bulletin;
            if (b == null) return;

            int changed = NormalizeAndMarkModified(b);
            ObjectSpace.SetModified(b);
            ObjectSpace.CommitChanges();

            ApplyAscendingSort(); // ✅ tri visuel
            (View.FindItem(nameof(Bulletin.Lignes)) as ListPropertyEditor)?.Refresh();
            View.Refresh();

            var msg = changed == 0 ? "Ordre déjà normalisé." : $"Réordonné : {changed} ligne(s).";
            Application.ShowViewStrategy.ShowMessage(msg, InformationType.Success, 3000, InformationPosition.Top);

        }

        /// <summary>
        /// Renumérote 10,20,30… en triant par OrdreCalcul (nulls à la fin) puis Oid.
        /// Marque chaque ligne SetModified pour forcer la prise en compte & le rafraîchissement.
        /// Retourne le nombre de lignes dont la valeur a changé.
        /// </summary>
        public int NormalizeAndMarkModified(Bulletin b)
        {
            if (b?.Lignes == null || b.Lignes.Count == 0) return 0;

            int i = 10;
            int changed = 0;

            foreach (var l in b.Lignes
                               .OrderBy(x => x.OrdreCalcul ?? int.MaxValue)
                               .ThenBy(x => x.Oid))
            {
                var old = l.OrdreCalcul;
                // ⚠️ on affecte systématiquement (même si égal) pour être sûr que l’UI voie du mouvement
                l.OrdreCalcul = i;

                // Marque l’objet modifié pour la couche XPO/XAF
                View?.ObjectSpace?.SetModified(l);

                if (old != i) changed++;
                i += 10;
            }
            return changed;
        }

        // -------------------------------------------------
        // Logique de normalisation
        // -------------------------------------------------
        public static void Normalize(Bulletin b)
        {
            if (b?.Lignes == null || b.Lignes.Count == 0) return;

            int i = 10;
            foreach (var l in b.Lignes
                               .OrderBy(x => x.OrdreCalcul ?? int.MaxValue)
                               .ThenBy(x => x.Oid))
            {
                if (l.OrdreCalcul != i)
                    l.OrdreCalcul = i;
                i += 10;
            }
        }


        protected override void OnViewControlsCreated()
        {
            base.OnViewControlsCreated();
        }

        private void ApplyAscendingSort()
        {
            var lv = lignesEditor?.ListView;
            if (lv?.CollectionSource == null) return;

            var sorting = lv.CollectionSource.Sorting;
            sorting.Clear();
            sorting.Add(new SortProperty(
                nameof(BulletinLigne.OrdreCalcul),
                SortingDirection.Ascending
            ));
        }


    }
}
