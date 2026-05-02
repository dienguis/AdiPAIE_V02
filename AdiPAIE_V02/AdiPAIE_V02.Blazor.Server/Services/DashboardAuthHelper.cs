// =============================================================================
//  DashboardAuthHelper.cs
//  Helper partagé d'autorisation pour les pages /dashboards/*.
//
//  App.razor utilise <RouteView> et non <AuthorizeRouteView>, donc l'attribut
//  [Authorize] des pages Razor n'est pas appliqué. On effectue donc le check
//  d'authentification + RBAC manuellement dans chaque OnInitialized().
//
//  Rôles autorisés à voir les dashboards :
//    - Administrators (sysadmin)
//    - RH_Manager     (consultation pure — DG / COMEX)
//    - RH             (équipe RH opérationnelle)
//    - DAF            (direction financière, vision masse salariale)
//  Tous les autres rôles → redirection vers /LoginPage.
//
//  Note : ce helper vit dans le projet Blazor.Server (et non Module) car il
//  dépend de INonSecuredObjectSpaceFactory de DevExpress.ExpressApp.Blazor.
// =============================================================================

using System;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;          // INonSecuredObjectSpaceFactory (vrai namespace)
using Microsoft.AspNetCore.Http;

namespace AdiPAIE_V02.Blazor.Server.Services
{
    /// <summary>
    /// Helper statique pour vérifier l'autorisation d'accès aux pages
    /// /dashboards/*. Utilisé en remplacement de l'attribut [Authorize]
    /// inopérant (cf. App.razor RouteView).
    /// </summary>
    public static class DashboardAuthHelper
    {
        /// <summary>Rôles autorisés à voir le module Tableaux de Bord RH.</summary>
        public static readonly string[] AllowedRoles =
        {
            "Administrators",
            "RH_Manager",
            "RH",
            "DAF"
        };

        /// <summary>
        /// Vérifie que l'utilisateur courant est (1) authentifié via cookie
        /// ASP.NET et (2) titulaire d'au moins un des rôles autorisés.
        /// Retourne <c>true</c> si l'accès est permis.
        /// </summary>
        public static bool CanAccessDashboards(
            IHttpContextAccessor? httpCtx,
            INonSecuredObjectSpaceFactory? osFactory)
        {
            // ── (1) Authentification cookie ──
            var user = httpCtx?.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return false;

            var userName = user.Identity.Name;
            if (string.IsNullOrWhiteSpace(userName)) return false;

            // ── (2) RBAC : vérification des rôles dans la base XPO ──
            //   On utilise un NonSecuredObjectSpace pour bypass les
            //   permissions XAF (sinon on lit l'utilisateur courant
            //   avec ses propres permissions, risque de récursion).
            if (osFactory == null) return false;
            try
            {
                using var os = osFactory.CreateNonSecuredObjectSpace(typeof(ApplicationUser));
                var appUser = os.GetObjectsQuery<ApplicationUser>()
                    .ToList()
                    .FirstOrDefault(u => string.Equals(u.UserName, userName,
                        StringComparison.OrdinalIgnoreCase));
                if (appUser == null) return false;

                // Roles est une XPCollection — on itère défensivement
                foreach (var role in appUser.Roles)
                {
                    if (role == null) continue;
                    if (AllowedRoles.Contains(role.Name)) return true;
                }
                return false;
            }
            catch
            {
                // En cas d'erreur de lecture (XPO down, etc.) on REFUSE
                // l'accès par défaut (fail closed) — c'est la posture sûre.
                return false;
            }
        }
    }
}
