// =============================================================================
//  DemoDataWipeController.cs — V1.1 (mai 2026)
//
//  Contrôleur XAF qui expose une SimpleAction « Vider données démo » dans
//  l'écran de la SuperUser / Administration. Supprime UNIQUEMENT les
//  enregistrements préfixés "DEMO_" (Sites, Unités, Intérimaires, Contrats,
//  Mouvements, Postes, Sociétés d'intérim).
//
//  ⚠️ AUCUN risque pour les seeds réels (rubriques de paie, paramètres,
//      catégories métier) — leurs Code n'ont pas le préfixe DEMO_.
//
//  Avant exécution : popup de confirmation avec le décompte des éléments
//  qui seront supprimés.
// =============================================================================

using System;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.DatabaseUpdate;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Action « Vider données démo » disponible sur la fiche utilisateur
    /// (ApplicationUser DetailView) — réservée aux Administrateurs.
    /// </summary>
    public sealed class DemoDataWipeController
        : ObjectViewController<DetailView, ApplicationUser>
    {
        private readonly SimpleAction _wipeAction;

        public DemoDataWipeController()
        {
            _wipeAction = new SimpleAction(this,
                id: "DemoDataWipe_Run",
                category: PredefinedCategory.Tools)
            {
                Caption     = "Vider les données démo",
                ImageName   = "Action_Delete",
                ToolTip     = "Supprime tous les enregistrements préfixés DEMO_ (Sites, Unités, Intérimaires, Contrats, Mouvements). Aucun risque pour les seeds réels.",
                ConfirmationMessage = "Êtes-vous sûr de vouloir supprimer toutes les données démo (préfixe DEMO_) ?\n\n"
                                    + "Cette action est IRRÉVERSIBLE mais SANS RISQUE pour les données métier réelles "
                                    + "(rubriques de paie, paramètres, catégories — leurs codes n'ont pas le préfixe DEMO_).",
                PaintStyle  = ActionItemPaintStyle.CaptionAndImage,
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _wipeAction.Execute += OnWipeExecute;
        }

        private void OnWipeExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                using var os = Application.CreateObjectSpace(typeof(Site));
                var (s, u, c, m, i, p, sc) = DemoDataWipe.WipeAll(os);

                Application.ShowViewStrategy?.ShowMessage(
                    $"✅ Données démo supprimées : "
                    + $"{s} sites · {u} unités · {c} contrats · {m} mouvements · "
                    + $"{i} intérimaires · {p} postes · {sc} sociétés. "
                    + $"Total : {s + u + c + m + i + p + sc} enregistrements. "
                    + $"Pour ne pas les recréer au prochain démarrage, "
                    + $"passez 'Dashboards:SeedDemoData' à false dans appsettings.json.",
                    InformationType.Success, 12000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"❌ Erreur lors de la suppression : {ex.Message}",
                    InformationType.Error, 10000, InformationPosition.Top);
            }
        }
    }
}
