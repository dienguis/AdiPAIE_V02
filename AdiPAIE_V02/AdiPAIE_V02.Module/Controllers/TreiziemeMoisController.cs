// =============================================================================
//  TreiziemeMoisController.cs — V1.7.2
//
//  Actions XAF sur la ListView de TreiziemeMois :
//    - "Calculer 13ième mois pour année..." (menu déroulant des 3 dernières
//      années + année courante)
//    - "Intégrer aux bulletins de décembre" (génère les BulletinLigne 13EME)
//
//  Les rôles DAF + RH peuvent utiliser ces actions.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class TreiziemeMoisController
        : ObjectViewController<ListView, TreiziemeMois>
    {
        private readonly SingleChoiceAction calculerAction;
        private readonly SingleChoiceAction integrerAction;

        public TreiziemeMoisController()
        {
            // ────────── Action 1 : Calculer 13ième mois ──────────
            calculerAction = new SingleChoiceAction(this,
                "M13_Calculer", PredefinedCategory.RecordEdit)
            {
                Caption = "Calculer 13ième mois",
                ToolTip = "Calcule (ou recalcule) le 13ième mois pour tous "
                          + "les salariés actifs de l'année sélectionnée.",
                ImageName = "Action_Workflow_Run",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ItemType = SingleChoiceActionItemType.ItemIsOperation,
                ShowItemsOnClick = true,
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            PopulerAnneesDisponibles(calculerAction);
            calculerAction.Execute += CalculerAction_Execute;

            // ────────── Action 2 : Intégrer aux bulletins ──────────
            integrerAction = new SingleChoiceAction(this,
                "M13_Integrer", PredefinedCategory.RecordEdit)
            {
                Caption = "Intégrer aux bulletins de décembre",
                ToolTip = "Crée les lignes 13EME sur les bulletins de décembre "
                          + "des salariés concernés. Idempotent.",
                ImageName = "Action_Document_New",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ItemType = SingleChoiceActionItemType.ItemIsOperation,
                ShowItemsOnClick = true,
                ConfirmationMessage =
                    "Cette action va créer des lignes 13EME sur les bulletins "
                    + "de décembre des salariés calculés. Continuer ?",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            PopulerAnneesDisponibles(integrerAction);
            integrerAction.Execute += IntegrerAction_Execute;
        }

        // Construit le menu : N-2, N-1, N (les 3 dernières années + courante)
        private static void PopulerAnneesDisponibles(SingleChoiceAction action)
        {
            int anneeCourante = DateTime.Today.Year;
            for (int delta = 0; delta >= -2; delta--)
            {
                int annee = anneeCourante + delta;
                action.Items.Add(new ChoiceActionItem(
                    annee.ToString(),
                    $"Année {annee}",
                    annee));
            }
        }

        // ─── Handler : Calculer ─────────────────────────────────────
        private void CalculerAction_Execute(
            object sender, SingleChoiceActionExecuteEventArgs e)
        {
            try
            {
                int annee = (int)e.SelectedChoiceActionItem.Data;
                var resultats = TreiziemeMoisService.CalculerPourAnnee(ObjectSpace, annee);

                int nbExcluVsCalcul = resultats.Count;
                Application.ShowViewStrategy?.ShowMessage(
                    $"✅ Calcul 13ième mois {annee} terminé. "
                    + $"{nbExcluVsCalcul} salarié(s) traité(s). "
                    + "Vérifier la liste, puis lancer « Intégrer aux bulletins ».",
                    InformationType.Success, 6000, InformationPosition.Top);

                View.Refresh();
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"❌ Erreur calcul 13ième mois : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }

        // ─── Handler : Intégrer aux bulletins de décembre ──────────
        private void IntegrerAction_Execute(
            object sender, SingleChoiceActionExecuteEventArgs e)
        {
            try
            {
                int annee = (int)e.SelectedChoiceActionItem.Data;
                var rapport = TreiziemeMoisIntegrationService
                    .IntegrerAuBulletinDecembre(ObjectSpace, annee);

                Application.ShowViewStrategy?.ShowMessage(
                    rapport,
                    InformationType.Info, 10000, InformationPosition.Top);

                View.Refresh();
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"❌ Erreur intégration 13ième : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }
    }
}
