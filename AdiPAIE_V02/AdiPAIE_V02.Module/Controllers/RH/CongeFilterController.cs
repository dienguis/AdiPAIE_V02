using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Filtre la ListView CongeDemande selon le profil connecté.
    ///
    /// Salarié    → uniquement ses propres demandes
    /// N+1        → ses demandes + EnAttenteN1 dont il est ValideurN1
    /// N+2        → ses demandes + EnAttenteN2 dont il est ValideurN2
    /// RH / Admin → toutes les demandes Soumise, Accordée, Refusée, Annulée
    ///              (PAS les Brouillon ni les EnAttente encore en circuit hiérarchique)
    /// </summary>
    public class CongeFilterController
        : ObjectViewController<ListView, CongeDemande>
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

            var user = new XPQuery<ApplicationUser>(session)
                .FirstOrDefault(u => u.UserName == userName);

            var salConn = user?.Salarie;

            if (salConn == null)
            {
                // ── RH / Admin ─────────────────────────────────────
                View.CollectionSource.Criteria["RoleFilter"] =
                    CriteriaOperator.Parse(
                        "Statut = ? OR Statut = ? OR Statut = ? OR Statut = ?",
                        (int)CongeStatut.Soumise,
                        (int)CongeStatut.Accordee,
                        (int)CongeStatut.Refusee,
                        (int)CongeStatut.Annulee);
                return;
            }

            // ── Profil avec fiche salarié ──────────────────────────
            var criterePropres = CriteriaOperator.Parse("Salarie = ?", salConn);

            var critereValideurN1 = CriteriaOperator.Parse(
                "Statut = ? AND ValideurN1 = ?",
                (int)CongeStatut.EnAttenteN1, salConn);

            var critereValideurN2 = CriteriaOperator.Parse(
                "Statut = ? AND ValideurN2 = ?",
                (int)CongeStatut.EnAttenteN2, salConn);

            View.CollectionSource.Criteria["RoleFilter"] = new GroupOperator(
                GroupOperatorType.Or,
                criterePropres,
                critereValideurN1,
                critereValideurN2);
        }
    }
}
