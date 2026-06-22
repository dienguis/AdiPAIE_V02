using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.NonPersistent
{
    /// <summary>
    /// V1.7.2 — Entité non-persistante pour la saisie d'un changement de
    /// mot de passe par l'utilisateur connecté lui-même.
    ///
    /// Tous les champs sont en PasswordPropertyEditor pour masquer
    /// l'affichage pendant la saisie.
    ///
    /// Le controller ChangeMyPasswordController valide :
    ///   - L'ancien mot de passe est correct
    ///   - Le nouveau mot de passe a au moins 6 caractères
    ///   - Nouveau == Confirmation
    ///   - Nouveau != Ancien
    /// </summary>
    [DomainComponent]
    [XafDisplayName("Changer mon mot de passe")]
    [ImageName("Action_Security")]
    public class ChangePasswordRequest : NonPersistentBaseObject
    {
        [RuleRequiredField(CustomMessageTemplate = "Ancien mot de passe obligatoire.")]
        [PasswordPropertyText(true)]
        [ModelDefault("EditorAlias", "PasswordPropertyEditor")]
        [XafDisplayName("Ancien mot de passe")]
        [ToolTip("Saisir votre mot de passe actuel pour validation. " +
                 "Si c'est votre première connexion, laisser vide.")]
        public string OldPassword { get; set; }

        [RuleRequiredField(CustomMessageTemplate = "Nouveau mot de passe obligatoire.")]
        [PasswordPropertyText(true)]
        [ModelDefault("EditorAlias", "PasswordPropertyEditor")]
        [XafDisplayName("Nouveau mot de passe")]
        [ToolTip("Minimum 6 caractères. Recommandé : majuscule, minuscule, " +
                 "chiffre, caractère spécial.")]
        public string NewPassword { get; set; }

        [RuleRequiredField(CustomMessageTemplate = "Confirmation obligatoire.")]
        [PasswordPropertyText(true)]
        [ModelDefault("EditorAlias", "PasswordPropertyEditor")]
        [XafDisplayName("Confirmer le nouveau mot de passe")]
        [ToolTip("Resaisir exactement le même mot de passe que ci-dessus.")]
        public string ConfirmPassword { get; set; }
    }
}
