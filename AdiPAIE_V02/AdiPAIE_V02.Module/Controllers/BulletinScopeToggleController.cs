using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Controllers
{
    public class BulletinScopeToggleController : ObjectViewController<ListView, Bulletin>
    {
        private const string FilterKey = "EmployeSelfFilter";
        private readonly SimpleAction toggleScopeAction;

        public BulletinScopeToggleController()
        {
            TargetObjectType = typeof(Bulletin);
            TargetViewType = ViewType.ListView;
            TargetViewNesting = Nesting.Root;

            toggleScopeAction = new SimpleAction(this, "ToggleBulletinScope", PredefinedCategory.View)
            {
                Caption = "Voir mes bulletins",
                ImageName = "Action_Filter" // change si tu préfères une autre icône
            };
            toggleScopeAction.Execute += ToggleScopeAction_Execute; // Abonnement unique (pas d'unsubscribe)
        }

        protected override void OnActivated()
        {
            base.OnActivated();

            bool isAdminish = UserIsInAnyRole("Administrators", "Admin", "DRH", "Paie");
            toggleScopeAction.Active["AdminsOnly"] = isAdminish;

            // Cache le bouton pour les simples "Employe"
            if (!isAdminish && UserIsInRole("Employe"))
            {
                toggleScopeAction.Active["HideForEmployees"] = false; // false => caché
                return;
            }

            // Ajuste le libellé en fonction de l'état courant du filtre
            var lv = View as ListView;
            if (lv?.CollectionSource != null && lv.CollectionSource.Criteria.ContainsKey(FilterKey))
            {
                toggleScopeAction.Caption = "Voir tout";
            }
            else
            {
                toggleScopeAction.Caption = "Voir mes bulletins";
            }
        }

        private void ToggleScopeAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var lv = View as ListView;
            if (lv?.CollectionSource == null)
                return;

            if (lv.CollectionSource.Criteria.ContainsKey(FilterKey))
            {
                // Enlève le filtre => Voir tout
                lv.CollectionSource.Criteria.Remove(FilterKey);
                toggleScopeAction.Caption = "Voir mes bulletins";
            }
            else
            {
                // Applique le filtre => Voir mes bulletins
                string currentUserEmail = ResolveCurrentUserEmail();
                CriteriaOperator crit = !string.IsNullOrEmpty(currentUserEmail)
                    ? (CriteriaOperator)new BinaryOperator("Salarie.Email", currentUserEmail)
                    : CriteriaOperator.Parse("Salarie.Email = CurrentUserName()");

                lv.CollectionSource.Criteria[FilterKey] = crit;
                toggleScopeAction.Caption = "Voir tout";
            }
        }

        // ================= Helpers =================

        private static bool UserIsInRole(string roleName)
        {
            if (SecuritySystem.CurrentUser is PermissionPolicyUser ppu)
            {
                return ppu.Roles.Any(r => r != null &&
                    string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase));
            }
            var user = SecuritySystem.CurrentUser;
            var rolesProp = user?.GetType().GetProperty("Roles", BindingFlags.Instance | BindingFlags.Public);
            if (rolesProp?.GetValue(user) is IEnumerable roles)
            {
                foreach (var role in roles)
                {
                    var nameProp = role.GetType().GetProperty("Name");
                    if (nameProp?.GetValue(role) is string name &&
                        string.Equals(name, roleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool UserIsInAnyRole(params string[] roleNames)
            => roleNames != null && roleNames.Any(UserIsInRole);

        private static string ResolveCurrentUserEmail()
        {
            var user = SecuritySystem.CurrentUser;
            var emailProp = user?.GetType().GetProperty("Email", BindingFlags.Instance | BindingFlags.Public);
            if (emailProp?.GetValue(user) is string mail && !string.IsNullOrWhiteSpace(mail))
            {
                return mail.Trim().ToLowerInvariant();
            }
            return SecuritySystem.CurrentUserName?.Trim().ToLowerInvariant();
        }
    }
}
