using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Controllers;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
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
    /// </summary>
    public class CongeFilterController
        : ObjectViewController<ListView, CongeDemande>
    {
        private const string FilterKey = "RoleFilter";

        protected override void OnActivated()
        {
            base.OnActivated();
            AppliquerFiltre();
        }

        private void AppliquerFiltre()
        {
            // Récupérer le salarié connecté
            var salConn = EspaceSalarieHelper.GetSalarieConnecte(ObjectSpace);

            // Pas de salarié lié → ne rien toucher
            if (salConn == null)
                return;

            // RH → filtre RH (voit les demandes soumises/accordées/refusées/annulées)
            if (!EspaceSalarieHelper.DoitRestreindreEspaceSalarie(ObjectSpace))
            {
                View.CollectionSource.Criteria[FilterKey] =
                    CriteriaOperator.Parse(
                        "Statut = ? OR Statut = ? OR Statut = ? OR Statut = ?",
                        (int)CongeStatut.Soumise,
                        (int)CongeStatut.Accordee,
                        (int)CongeStatut.Refusee,
                        (int)CongeStatut.Annulee);
                return;
            }

            // ── Employé : ses propres demandes + celles où il est valideur ──
            var criterePropres = CriteriaOperator.Parse("Salarie.Oid = ?", salConn.Oid);

            var critereValideurN1 = CriteriaOperator.Parse(
                "Statut = ? AND ValideurN1.Oid = ?",
                (int)CongeStatut.EnAttenteN1, salConn.Oid);

            var critereValideurN2 = CriteriaOperator.Parse(
                "Statut = ? AND ValideurN2.Oid = ?",
                (int)CongeStatut.EnAttenteN2, salConn.Oid);

            View.CollectionSource.Criteria[FilterKey] = new GroupOperator(
                GroupOperatorType.Or,
                criterePropres,
                critereValideurN1,
                critereValideurN2);
        }

        protected override void OnDeactivated()
        {
            if (View?.CollectionSource != null)
                View.CollectionSource.Criteria.Remove(FilterKey);
            base.OnDeactivated();
        }
    }
}
