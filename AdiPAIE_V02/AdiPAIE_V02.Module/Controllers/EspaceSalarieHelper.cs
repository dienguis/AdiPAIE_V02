using System;
using System.Collections.Generic;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Helper partage pour la detection employe dans l'espace salarie.
    ///
    /// Deux strategies de detection (tentees dans l'ordre) :
    ///   1. ApplicationUser.Salarie != null  (lien direct)
    ///   2. Salarie.Email == CurrentUserName  (meme logique que les permissions de securite)
    ///   3. L'utilisateur a le role "Employe"
    ///
    /// Ceci evite les incoherences entre la securite XAF
    /// (qui utilise "Salarie.Email = CurrentUserName()") et les controleurs.
    ///
    /// IMPORTANT : TryGetSession() extrait la Session XPO via :
    ///   - Cast direct si l'OS est un XPObjectSpace
    ///   - Parcours de AdditionalObjectSpaces si c'est un CompositeObjectSpace
    /// Cela corrige le bug ou "objectSpace is not XPObjectSpace" echouait
    /// silencieusement sur les ObjectSpaces wrappes (CompositeObjectSpace, etc.).
    /// En fallback, utilise IObjectSpace.FindObject (fonctionne avec tout type d'OS).
    /// </summary>
    public static class EspaceSalarieHelper
    {
        // ══════════════════════════════════════════════════════════
        //  Extraction de la Session XPO (compatible tous wrappers)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Tente d'extraire la Session XPO depuis l'ObjectSpace :
        ///   - XPObjectSpace              → directement .Session
        ///   - CompositeObjectSpace       → cherche un inner XPObjectSpace dans AdditionalObjectSpaces
        ///   - SecuredObjectSpace ou autre → null (fallback sur IObjectSpace.FindObject)
        /// </summary>
        private static Session TryGetSession(IObjectSpace objectSpace)
        {
            // Cas 1 : ObjectSpace XPO direct
            if (objectSpace is XPObjectSpace xpOs)
                return xpOs.Session;

            // Cas 2 : CompositeObjectSpace (créé quand NonPersistentObjectSpaceProvider est enregistré)
            // Le CompositeObjectSpace hérite de BaseObjectSpace et contient des inner ObjectSpaces.
            // On parcourt AdditionalObjectSpaces pour trouver un XPObjectSpace.
            if (objectSpace is CompositeObjectSpace compositeOs)
            {
                foreach (var innerOs in compositeOs.AdditionalObjectSpaces)
                {
                    if (innerOs is XPObjectSpace innerXpOs)
                        return innerXpOs.Session;
                }
            }

            // Cas 3 : SecuredObjectSpace ou autre → null (fallback FindObject)
            return null;
        }

        /// <summary>
        /// Recherche un ApplicationUser par UserName.
        /// Utilise la Session XPO si disponible (contourne la sécurité, comme l'ancien code),
        /// sinon fallback sur IObjectSpace.FindObject.
        /// </summary>
        private static ApplicationUser FindUserByName(IObjectSpace objectSpace, string userName)
        {
            var session = TryGetSession(objectSpace);
            if (session != null)
            {
                // XPQuery contourne la sécurité → trouve toujours le user courant
                return new XPQuery<ApplicationUser>(session)
                    .FirstOrDefault(u => u.UserName == userName);
            }

            // Fallback IObjectSpace (passe par la sécurité, mais au pire
            // retourne null — même comportement que l'ancien code qui faisait
            // "return null" quand l'ObjectSpace n'était pas un XPObjectSpace).
            //
            // try/catch défensif : si l'ObjectSpace est de type
            // NonPersistentObjectSpace ou CompositeObjectSpace sans
            // ApplicationUser dans son AdditionalObjectSpaces, FindObject
            // throw ArgumentException. On respecte alors le contrat de
            // « retourner null » documenté ci-dessus.
            try
            {
                return objectSpace.FindObject<ApplicationUser>(
                    CriteriaOperator.Parse("UserName = ?", userName));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Recherche un Salarie par critère.
        /// Utilise la Session XPO si disponible, sinon fallback sur IObjectSpace.FindObject.
        /// Retourne null si l'ObjectSpace ne sait pas gérer le type Salarie
        /// (ex. NonPersistentObjectSpace) — comportement défensif aligné sur
        /// FindUserByName.
        /// </summary>
        private static Salarie FindSalarieByCriteria(
            IObjectSpace objectSpace, CriteriaOperator criteria)
        {
            var session = TryGetSession(objectSpace);
            if (session != null)
            {
                return session.FindObject<Salarie>(criteria);
            }

            // try/catch défensif : si l'ObjectSpace n'embarque pas le type
            // Salarie dans son AdditionalObjectSpaces (cas des
            // NonPersistentObjectSpace pour les classes non-persistantes
            // comme DashboardsRHMenu), FindObject throw ArgumentException.
            // On retourne null pour que les contrôleurs appelants puissent
            // simplement « ne rien faire » sur ce type de view.
            try
            {
                return objectSpace.FindObject<Salarie>(criteria);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }


        // ══════════════════════════════════════════════════════════
        //  Méthodes publiques (API identique à l'ancien code)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Renvoie true si l'utilisateur courant est un salarie.
        /// </summary>
        public static bool EstSalarieConnecte(IObjectSpace objectSpace)
        {
            if (objectSpace == null) return false;

            var userName = SecuritySystem.CurrentUserName;
            if (string.IsNullOrEmpty(userName)) return false;

            // Strategie 1 : lien direct ApplicationUser.Salarie
            var user = FindUserByName(objectSpace, userName);
            if (user?.Salarie != null) return true;

            // Strategie 2 : Salarie.Email == UserName
            // (meme critere que les permissions de securite dans Updater.cs)
            var salByEmail = FindSalarieByCriteria(objectSpace,
                CriteriaOperator.Parse("Upper(Email) = ?", userName.ToUpper()));
            if (salByEmail != null) return true;

            // Strategie 3 : le user a le role "Employe"
            if (user != null && user.Roles.Any(r => r.Name == "Employe"))
                return true;

            return false;
        }

        /// <summary>
        /// Renvoie true si l'utilisateur est un salarie ET n'a QUE des roles
        /// basiques (Employe, Default). Un utilisateur avec un role RH, Admin,
        /// DAF, etc. retourne false meme s'il est aussi salarie.
        /// Cela evite d'appliquer les restrictions "espace salarie" aux RH.
        /// </summary>
        public static bool EstUniquementEmploye(IObjectSpace objectSpace)
        {
            if (objectSpace == null) return false;

            var userName = SecuritySystem.CurrentUserName;
            if (string.IsNullOrEmpty(userName)) return false;

            var user = FindUserByName(objectSpace, userName);
            if (user == null) return false;

            // Doit etre un salarie
            if (!EstSalarieConnecte(objectSpace)) return false;

            // Rôles "privilégiés" qui donnent des droits RH / Admin
            var rolesPrivilegies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "RH", "Admin", "Administrators", "DRH", "DAF", "Paie", "Comptabilite" };

            // Si l'utilisateur a au moins un rôle privilégié → PAS un pur employé
            if (user.Roles.Any(r => rolesPrivilegies.Contains(r.Name)))
                return false;

            // Sinon (aucun rôle, ou uniquement des rôles basiques) → pur employé
            return true;
        }

        /// <summary>
        /// Renvoie true si l'utilisateur doit être restreint dans l'espace salarié.
        /// C'est-à-dire : il est lié à un salarié ET il n'a PAS le rôle RH.
        ///
        /// L'employé (ex: dienguis) reste restreint dans "Mon espace".
        /// Le RH (rh@elton.sn) n'est jamais restreint (besoin d'accès complet
        /// aux vues Paie même s'il est aussi lié à un salarié).
        /// </summary>
        public static bool DoitRestreindreEspaceSalarie(IObjectSpace objectSpace)
        {
            if (!EstSalarieConnecte(objectSpace)) return false;

            if (objectSpace == null) return false;

            var userName = SecuritySystem.CurrentUserName;
            if (string.IsNullOrEmpty(userName)) return false;

            var user = FindUserByName(objectSpace, userName);

            // Si on ne peut pas lire l'ApplicationUser (sécurité XAF : le rôle Employe
            // n'a pas de permission Read sur ApplicationUser), on RESTREINT par défaut.
            // C'est le comportement sûr : un salarié dont on ne peut pas vérifier
            // les rôles voit uniquement ses propres données.
            // Les utilisateurs RH/Admin ont AllowAllByDefault ou IsAdministrative=true,
            // donc FindUserByName les trouvera toujours.
            if (user == null) return true;

            // Le rôle RH a besoin d'un accès complet — ne pas restreindre
            if (user.Roles.Any(r => r.Name == "RH"))
                return false;

            return true;
        }

        /// <summary>
        /// Retrouve le Salarie lie a l'utilisateur courant.
        /// Retourne null si l'utilisateur n'est pas un salarie.
        ///
        /// IMPORTANT : pour que cette methode fonctionne, il faut que
        /// ApplicationUser.Salarie soit renseigne dans l'admin,
        /// OU que Salarie.Email corresponde au UserName de connexion.
        /// </summary>
        public static Salarie GetSalarieConnecte(IObjectSpace objectSpace)
        {
            if (objectSpace == null) return null;

            var userName = SecuritySystem.CurrentUserName;
            if (string.IsNullOrEmpty(userName)) return null;

            // Strategie 1 : lien direct ApplicationUser.Salarie
            var user = FindUserByName(objectSpace, userName);
            if (user?.Salarie != null) return user.Salarie;

            // Strategie 2 : Salarie.Email == UserName (insensible a la casse)
            var salByEmail = FindSalarieByCriteria(objectSpace,
                CriteriaOperator.Parse("Upper(Email) = ?", userName.ToUpper()));
            if (salByEmail != null) return salByEmail;

            // Strategie 3 : si l'utilisateur a le role "Employe",
            // chercher un Salarie dont l'email contient le username
            // (ex: UserName = "stine" et Salarie FullName contient "TINE")
            if (user != null && user.Roles.Any(r => r.Name == "Employe"))
            {
                var salByName = FindSalarieByCriteria(objectSpace,
                    CriteriaOperator.Parse("Upper(Email) Like ?",
                        "%" + userName.ToUpper() + "%"));
                if (salByName != null) return salByName;
            }

            return null;
        }
    }
}
