// =============================================================================
//  MonProfilController.cs — V1.7.2
//
//  Action globale (WindowController) "Changer mon mot de passe" dans la toolbar
//  principale de SunuPaie, accessible à tous les users connectés.
//
//  Pourquoi cette approche (et pas le DetailView ApplicationUser direct)
//  --------------------------------------------------------------------
//  Le DetailView ApplicationUser de XAF Blazor affiche tous les champs en
//  password-mask (*******) car ApplicationUser a des champs sensibles —
//  UX déroutante pour les RH/DAF/DG.
//
//  À la place, on ouvre un PopupWindow dédié avec 3 champs clairs :
//    - Ancien mot de passe
//    - Nouveau mot de passe
//    - Confirmation
//
//  La validation se fait dans AcceptingExecute :
//    - Ancien mot de passe correct (ou vide si 1ère connexion)
//    - Nouveau >= 6 caractères
//    - Nouveau == Confirmation
//    - Nouveau != Ancien
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Action globale "Changer mon mot de passe" dans la toolbar principale.
    /// </summary>
    public sealed class MonProfilController : WindowController
    {
        readonly PopupWindowShowAction changeMyPasswordAction;

        public MonProfilController()
        {
            TargetWindowType = WindowType.Main;

            changeMyPasswordAction = new PopupWindowShowAction(this,
                "ChangeMyPassword", PredefinedCategory.Tools)
            {
                Caption = "Changer mon mot de passe",
                ImageName = "Action_Security",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Ouvrir le formulaire de changement de votre mot de passe " +
                          "(saisie de l'ancien, du nouveau et de la confirmation).",
                AcceptButtonCaption = "Valider",
                CancelButtonCaption = "Annuler",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            changeMyPasswordAction.CustomizePopupWindowParams +=
                ChangeMyPasswordAction_CustomizePopupWindowParams;
            changeMyPasswordAction.Execute += ChangeMyPasswordAction_Execute;
        }

        // Construit le DetailView du popup avec une instance ChangePasswordRequest
        void ChangeMyPasswordAction_CustomizePopupWindowParams(
            object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var os = Application.CreateObjectSpace(typeof(ChangePasswordRequest));
            var request = os.CreateObject<ChangePasswordRequest>();

            var detailView = Application.CreateDetailView(os, request);
            detailView.Caption = "Changer mon mot de passe";

            e.View = detailView;
        }

        // Au clic "Valider" : valide et applique le changement
        void ChangeMyPasswordAction_Execute(
            object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            try
            {
                var request = e.PopupWindowViewCurrentObject as ChangePasswordRequest;
                if (request == null)
                {
                    ShowError("Formulaire introuvable. Réessayez.");
                    return;
                }

                // ─── Validation 1 : champs obligatoires ──────────────
                if (string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    ShowError("Le nouveau mot de passe est obligatoire.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
                {
                    ShowError("La confirmation est obligatoire.");
                    return;
                }

                // ─── Validation 2 : New == Confirm ───────────────────
                if (request.NewPassword != request.ConfirmPassword)
                {
                    ShowError("Le nouveau mot de passe et sa confirmation ne correspondent pas.");
                    return;
                }

                // ─── Validation 3 : longueur minimale ────────────────
                if (request.NewPassword.Length < 6)
                {
                    ShowError("Le nouveau mot de passe doit faire au moins 6 caractères.");
                    return;
                }

                // ─── Validation 4 : New != Old ───────────────────────
                if (!string.IsNullOrEmpty(request.OldPassword)
                    && request.NewPassword == request.OldPassword)
                {
                    ShowError("Le nouveau mot de passe doit être différent de l'ancien.");
                    return;
                }

                // ─── Récupérer le user connecté ──────────────────────
                var currentUser = SecuritySystem.CurrentUser as ApplicationUser;
                if (currentUser == null)
                {
                    ShowError("Session expirée. Reconnectez-vous.");
                    return;
                }

                // Ouvrir un ObjectSpace dédié pour modifier le user
                var userOs = Application.CreateObjectSpace(typeof(ApplicationUser));
                var userInOs = userOs.GetObject(currentUser);

                // ─── Validation 5 : vérifier l'ancien mot de passe ──
                // (sauf si aucun mot de passe défini = 1ère connexion)
                // StoredPassword étant protected en XAF, on utilise
                // ComparePassword(string.Empty) : si retourne true, le user
                // n'a pas encore de mot de passe défini.
                bool hasOldPassword = !userInOs.ComparePassword(string.Empty);
                if (hasOldPassword)
                {
                    if (string.IsNullOrEmpty(request.OldPassword))
                    {
                        ShowError("Saisissez votre mot de passe actuel pour validation.");
                        return;
                    }
                    if (!userInOs.ComparePassword(request.OldPassword))
                    {
                        ShowError("L'ancien mot de passe est incorrect.");
                        return;
                    }
                }

                // ─── Appliquer le nouveau mot de passe ───────────────
                userInOs.SetPassword(request.NewPassword);
                userInOs.ChangePasswordOnFirstLogon = false;
                userOs.CommitChanges();

                Application.ShowViewStrategy?.ShowMessage(
                    "✅ Mot de passe changé avec succès. " +
                    "Vous pouvez continuer à utiliser SunuPaie. " +
                    "Pensez à l'utiliser à votre prochaine connexion.",
                    InformationType.Success, 8000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                ShowError($"Erreur changement mot de passe : {ex.Message}");
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
