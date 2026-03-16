using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Filtre automatique de la ListView DemandeAttestation selon le profil connecté.
    ///
    /// Logique de filtrage :
    ///
    ///   Salarié (a une fiche Salarie liée) :
    ///     → Voit uniquement SES propres demandes
    ///
    ///   Responsable N+1 (est ValideurN1 sur des demandes) :
    ///     → Voit les demandes EnAttenteN1 dont il est le ValideurN1
    ///     → Plus ses propres demandes en tant que salarié
    ///
    ///   Responsable N+2 (est ValideurN2 sur des demandes) :
    ///     → Voit les demandes EnAttenteN2 dont il est le ValideurN2
    ///     → Plus ses propres demandes en tant que salarié
    ///
    ///   RH / Admin (pas de fiche Salarie liée, ou rôle admin) :
    ///     → Voit toutes les demandes en statut Soumise, EnTraitement, Traitee, Rejetee
    ///     → NE voit PAS les demandes encore en circuit hiérarchique (EnAttenteN1/N2)
    ///
    /// Note : Un responsable peut avoir à la fois son propre rôle salarié ET
    /// son rôle de valideur — les deux filtres sont combinés avec OR.
    /// </summary>
    public class DemandeAttestationFilterController
        : ObjectViewController<ListView, DemandeAttestation>
    {
        protected override void OnActivated()
        {
            base.OnActivated();
            AppliquerFiltre();
        }

        private void AppliquerFiltre()
        {
            var session = ((XPObjectSpace)ObjectSpace).Session;
            var userName = DevExpress.ExpressApp.SecuritySystem.CurrentUserName;

            // Récupère le compte connecté
            var user = new XPQuery<ApplicationUser>(session)
                .FirstOrDefault(u => u.UserName == userName);

            var salConn = user?.Salarie;

            if (salConn == null)
            {
                // ── Profil RH / Admin ──────────────────────────────────
                // Voit toutes les demandes ayant dépassé le circuit hiérarchique
                View.CollectionSource.Criteria["RoleFilter"] =
                    CriteriaOperator.Parse(
                        "Statut = ? OR Statut = ? OR Statut = ? OR Statut = ?",
                        (int)DemandeStatut.Soumise,
                        (int)DemandeStatut.EnTraitement,
                        (int)DemandeStatut.Traitee,
                        (int)DemandeStatut.Rejetee);
                return;
            }

            // ── Profil avec fiche salarié ──────────────────────────────
            // Construit le filtre combiné selon les rôles du salarié connecté

            // 1. Ses propres demandes (toujours)
            var criterePropres = CriteriaOperator.Parse("Salarie = ?", salConn);

            // 2. Demandes EnAttenteN1 dont il est le ValideurN1
            var critereValideurN1 = CriteriaOperator.Parse(
                "Statut = ? AND ValideurN1 = ?",
                (int)DemandeStatut.EnAttenteN1, salConn);

            // 3. Demandes EnAttenteN2 dont il est le ValideurN2
            var critereValideurN2 = CriteriaOperator.Parse(
                "Statut = ? AND ValideurN2 = ?",
                (int)DemandeStatut.EnAttenteN2, salConn);

            // Combine avec OR
            var filtreFinal = new GroupOperator(
                GroupOperatorType.Or,
                criterePropres,
                critereValideurN1,
                critereValideurN2);

            View.CollectionSource.Criteria["RoleFilter"] = filtreFinal;
        }
    }
}
