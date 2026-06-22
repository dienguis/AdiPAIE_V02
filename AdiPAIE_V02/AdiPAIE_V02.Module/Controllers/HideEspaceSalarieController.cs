// =============================================================================
//  HideEspaceSalarieController.cs — V1.8 (juin 2026)
//
//  Masque le menu "Mon espace" (GRH_EspaceSalarie) et ses sous-items pour les
//  utilisateurs qui ont les rôles RH, DAF ou DG, MÊME si le rôle Employé leur
//  donnait l'Allow normalement.
//
//  POURQUOI un controller runtime plutôt qu'une Navigation Permission Deny ?
//  -------------------------------------------------------------------------
//  En XAF Blazor, quand 2 rôles sont combinés (RH + Employé) :
//    - Employé a Allow implicite sur "Mon espace"
//    - RH a Deny explicite (tentative initiale, échec)
//    → Allow gagne ! Comportement non documenté de XAF Blazor pour les
//      Navigation Permissions sur les groupes parents et sub-items.
//
//  Solution : modifier dynamiquement le Model.NavigationItems après login
//  via OnActivated du WindowController. La modification est par-session
//  (pas persistée) — chaque user obtient son propre rendu navigation.
//
//  Rôles concernés : RH, DAF, DG (managers avec vue 360°).
//  Le rôle Employé seul (ou Comptable seul, Assistant, etc.) garde "Mon espace".
//
//  IMPLEMENTATION NOTE :
//    On utilise IModelNode (interface universelle XAF) + réflexion pour la
//    propriété "Visible", car les noms exacts IModelNavigationItem(s)
//    diffèrent selon les versions XAF (25.1, 25.2, etc.).
// =============================================================================

using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// WindowController qui masque "Mon espace" pour les managers (RH/DAF/DG).
    /// Cible la fenêtre principale uniquement (TargetWindowType = Main).
    /// </summary>
    public sealed class HideEspaceSalarieController : WindowController
    {
        // Rôles "managers" pour lesquels on masque l'auto-consultation salarié
        private static readonly string[] RolesManagers = new[] { "RH", "DAF", "DG" };

        // ID du groupe de menu à masquer (cf. Model.DesignedDiffs.xafml)
        private const string NavItemId = "GRH_EspaceSalarie";

        public HideEspaceSalarieController()
        {
            TargetWindowType = WindowType.Main;
        }

        protected override void OnActivated()
        {
            base.OnActivated();

            try
            {
                if (!CurrentUserIsManager()) return;
                HideNavigationItemByReflection(NavItemId);
            }
            catch
            {
                // Non bloquant : si l'API XAF change ou que le modèle est
                // indisponible, on laisse simplement le menu visible plutôt
                // que de planter l'app.
            }
        }

        protected override void OnDeactivated()
        {
            // V1.8 — Empêcher XAF de persister la modif Visible=false dans
            // le ModelDifference de l'utilisateur. Sans ce restore, à chaque
            // logout XAF sauvegardait Visible=false dans le MD user, ce qui
            // est par-user et persistant. Pas ce qu'on veut.
            try
            {
                RestoreNavigationItemByReflection(NavItemId);
            }
            catch { /* non bloquant */ }
            base.OnDeactivated();
        }

        /// <summary>
        /// Vérifie si l'utilisateur connecté possède au moins un rôle "manager".
        /// </summary>
        private static bool CurrentUserIsManager()
        {
            try
            {
                var user = SecuritySystem.CurrentUser as PermissionPolicyUser;
                if (user?.Roles == null) return false;

                return user.Roles
                    .OfType<PermissionPolicyRole>()
                    .Any(r => !string.IsNullOrEmpty(r.Name)
                           && RolesManagers.Contains(r.Name, StringComparer.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Symétrique de HideNavigationItemByReflection : remet Visible=true.
        /// Appelé au OnDeactivated pour éviter que XAF ne persiste la modif
        /// Visible=false dans le ModelDifference de l'utilisateur.
        /// </summary>
        private void RestoreNavigationItemByReflection(string itemId)
        {
            var modelApp = Application?.Model;
            if (modelApp == null) return;

            var navProp = modelApp.GetType().GetProperty("NavigationItems");
            var navRoot = navProp?.GetValue(modelApp) as IModelNode;
            if (navRoot == null) return;

            var target = FindNodeRecursive(navRoot, itemId);
            if (target == null) return;

            var visibleProp = target.GetType().GetProperty("Visible");
            if (visibleProp != null && visibleProp.CanWrite)
                visibleProp.SetValue(target, true);
        }

        /// <summary>
        /// Cherche un IModelNode par Id dans la sous-arborescence NavigationItems,
        /// puis bascule sa propriété "Visible" à false via réflexion.
        ///
        /// Pourquoi réflexion : la propriété Visible est définie sur des
        /// interfaces XAF dont les noms varient (IModelNavigationItem,
        /// IModelChoiceActionItem, IModelViewLayoutElement, etc.).
        /// La réflexion garantit la compatibilité multi-versions XAF.
        /// </summary>
        private void HideNavigationItemByReflection(string itemId)
        {
            var modelApp = Application?.Model;
            if (modelApp == null) return;

            // Récupérer le node racine "NavigationItems" via réflexion
            // (équivalent : modelApp.NavigationItems mais sans dépendance type)
            var navProp = modelApp.GetType().GetProperty("NavigationItems");
            var navRoot = navProp?.GetValue(modelApp) as IModelNode;
            if (navRoot == null) return;

            // Recherche récursive de l'item ciblé
            var target = FindNodeRecursive(navRoot, itemId);
            if (target == null) return;

            // Basculer Visible = false via réflexion
            var visibleProp = target.GetType().GetProperty("Visible");
            if (visibleProp != null && visibleProp.CanWrite)
            {
                visibleProp.SetValue(target, false);
            }
        }

        /// <summary>
        /// Recherche récursive d'un IModelNode par Id, insensible à la casse.
        /// L'Id n'est pas exposé directement par IModelNode (il l'est par
        /// IModelObjectIdentifier dans DevExpress.ExpressApp.Model.Core) —
        /// on passe par réflexion pour rester agnostique à la version XAF.
        /// </summary>
        private static IModelNode FindNodeRecursive(IModelNode parent, string itemId)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.NodeCount; i++)
            {
                var child = parent.GetNode(i);
                if (child == null) continue;

                var childId = GetNodeId(child);
                if (!string.IsNullOrEmpty(childId)
                    && string.Equals(childId, itemId, StringComparison.OrdinalIgnoreCase))
                    return child;

                var deeper = FindNodeRecursive(child, itemId);
                if (deeper != null) return deeper;
            }
            return null;
        }

        /// <summary>
        /// Lit la propriété "Id" d'un IModelNode via réflexion.
        /// La plupart des nodes XAF implémentent IModelObjectIdentifier qui
        /// expose Id, mais le type concret varie selon la version.
        /// </summary>
        private static string GetNodeId(IModelNode node)
        {
            if (node == null) return null;
            try
            {
                var prop = node.GetType().GetProperty("Id");
                return prop?.GetValue(node) as string;
            }
            catch
            {
                return null;
            }
        }
    }
}
