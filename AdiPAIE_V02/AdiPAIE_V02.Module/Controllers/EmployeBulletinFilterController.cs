using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using AdiPAIE_V02.Module.BusinessObjects;

namespace AdiPAIE_V02.Module.Controllers
{
    public class EmployeBulletinFilterController : ObjectViewController<ListView,Bulletin>
    {
        private const string FilterKey = "EmployeSelfFilter";

        public EmployeBulletinFilterController()
        {
            TargetObjectType = typeof(Bulletin);
            TargetViewType = ViewType.ListView;
            TargetViewNesting = Nesting.Root; // évite d'impacter les ListViews imbriquées
        }

        protected override void OnActivated()
        {
            base.OnActivated();

            // Actif uniquement pour les "Employe"
            bool isEmployee = UserIsInRole("Employe");
            Active["OnlyForEmployees"] = isEmployee;  // indicateur visuel dans les diagnostics XAF
            if (!isEmployee)
                return;

            // Si l'utilisateur est aussi Admin/DRH/Paie, pas de filtre
            if (UserIsInAnyRole("Administrators", "Admin", "DRH", "Paie"))
            {
                RemoveFilter();
                return;
            }

            var lv = View as ListView;
            if (lv?.CollectionSource == null)
                return;

            string currentUserEmail = ResolveCurrentUserEmail();
            CriteriaOperator crit = !string.IsNullOrEmpty(currentUserEmail)
                ? (CriteriaOperator)new BinaryOperator("Salarie.Email", currentUserEmail)
                : CriteriaOperator.Parse("Salarie.Email = CurrentUserName()"); // fallback si login = email

            lv.CollectionSource.Criteria[FilterKey] = crit;
        }

        protected override void OnDeactivated()
        {
            RemoveFilter();
            base.OnDeactivated();
        }

        private void RemoveFilter()
        {
            if (View is ListView lv && lv.CollectionSource != null)
            {
                lv.CollectionSource.Criteria.Remove(FilterKey);
            }
        }

        // ================= Helpers =================

        private static bool UserIsInRole(string roleName)
        {
            // Cas standard : PermissionPolicyUser
            if (SecuritySystem.CurrentUser is PermissionPolicyUser ppu)
            {
                return ppu.Roles.Any(r => r != null &&
                    string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase));
            }
            // Cas custom : réflexion (propriété "Roles" avec membres ayant "Name")
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
            // Sinon on retombe sur le login (utile si UserName = email)
            return SecuritySystem.CurrentUserName?.Trim().ToLowerInvariant();
        }
    }

}
