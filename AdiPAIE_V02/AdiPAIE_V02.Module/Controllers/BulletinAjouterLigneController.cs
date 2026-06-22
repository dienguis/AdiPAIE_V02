// =============================================================================
//  BulletinAjouterLigneController.cs — V1.8 (juin 2026)
//
//  Workaround pour le bug XAF Blazor : le bouton "Nouveau" sur la grille
//  des lignes (Aggregated collection BulletinLigne dans Bulletin.Lignes)
//  n'apparaît pas malgré toutes les permissions et settings XAFML corrects.
//
//  Au lieu de continuer à chercher dans XAF, on ajoute une action explicite
//  "Ajouter une ligne" dans la barre Edit du DetailView du Bulletin.
//  Elle ouvre un popup pour créer une BulletinLigne pré-attachée au
//  Bulletin courant.
//
//  Avantage : 100% fiable, indépendant des subtilités XAF Blazor
//  sur les nested ListViews et les permissions cumulées.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class BulletinAjouterLigneController
        : ObjectViewController<DetailView, Bulletin>
    {
        readonly PopupWindowShowAction ajouterLigneAction;

        public BulletinAjouterLigneController()
        {
            ajouterLigneAction = new PopupWindowShowAction(this,
                "Bulletin_AjouterLigneManuelle", PredefinedCategory.Edit)
            {
                Caption = "Ajouter une ligne",
                ImageName = "Action_New",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Ajouter manuellement une rubrique au bulletin " +
                          "(ex: avantage en nature sur un bulletin de congé). " +
                          "Le bulletin est pré-attaché automatiquement. " +
                          "Pensez à cliquer ensuite sur « Recalculer cotisations » " +
                          "pour mettre à jour les cotisations sociales et fiscales.",
                AcceptButtonCaption = "Enregistrer la ligne",
                CancelButtonCaption = "Annuler"
            };
            ajouterLigneAction.CustomizePopupWindowParams += OnCustomizePopup;
            ajouterLigneAction.Execute += OnExecute;
        }

        // ─────────────────────────────────────────────────────────────
        //  Construction du popup avec Bulletin pré-rempli
        // ─────────────────────────────────────────────────────────────
        void OnCustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var bulletin = (Bulletin)View.CurrentObject;
            if (bulletin == null) return;

            var os = Application.CreateObjectSpace(typeof(BulletinLigne));

            // Récupérer le Bulletin dans le nouvel OS pour pouvoir le lier à la ligne
            var bulletinInOs = os.GetObjectByKey<Bulletin>(bulletin.Oid);

            // Créer la nouvelle BulletinLigne pré-attachée
            var ligne = os.CreateObject<BulletinLigne>();
            ligne.Bulletin = bulletinInOs;

            // Construire le DetailView (mode Edit par défaut)
            var detailView = Application.CreateDetailView(os, ligne);
            detailView.Caption = $"Nouvelle ligne — bulletin {bulletin.Mois:D2}/{bulletin.Annee}";

            e.View = detailView;
        }

        // ─────────────────────────────────────────────────────────────
        //  Au clic "Enregistrer" : commit la nouvelle ligne
        // ─────────────────────────────────────────────────────────────
        void OnExecute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            try
            {
                var ligne = e.PopupWindowViewCurrentObject as BulletinLigne;
                if (ligne == null)
                {
                    ShowError("Ligne introuvable.");
                    return;
                }

                if (ligne.Rubrique == null)
                {
                    ShowError("Veuillez choisir une rubrique.");
                    return;
                }

                if (ligne.Bulletin == null)
                {
                    ShowError("Bulletin non rattaché — réessayez.");
                    return;
                }

                // Récupérer l'OS du popup et commiter
                var popupOs = ligne.Session.GetObjectByKey<Bulletin>(ligne.Bulletin.Oid)?.Session;
                e.PopupWindow.View.ObjectSpace.CommitChanges();

                // Rafraîchir le DetailView du bulletin pour voir la nouvelle ligne
                View.ObjectSpace.Refresh();

                Application.ShowViewStrategy?.ShowMessage(
                    $"✅ Ligne « {ligne.Rubrique?.Libelle} » ajoutée au bulletin. " +
                    $"Pensez à cliquer sur « Recalculer cotisations » pour " +
                    $"actualiser les cotisations.",
                    InformationType.Success, 6000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                ShowError($"Erreur ajout ligne : {ex.Message}");
            }
        }

        void ShowError(string message)
        {
            Application.ShowViewStrategy?.ShowMessage(
                $"❌ {message}",
                InformationType.Error, 6000, InformationPosition.Top);
        }
    }
}
