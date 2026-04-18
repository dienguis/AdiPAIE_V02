using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Controllers;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Filtre la ListView DemandeAttestation selon le profil connecté.
    ///
    /// Salarié    → ses propres demandes + celles où il est valideur
    /// RH / Admin → demandes Soumise, EnTraitement, Traitee, Rejetee
    /// </summary>
    public class DemandeAttestationFilterController
        : ObjectViewController<ListView, DemandeAttestation>
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

            // RH → filtre RH
            if (!EspaceSalarieHelper.DoitRestreindreEspaceSalarie(ObjectSpace))
            {
                View.CollectionSource.Criteria[FilterKey] =
                    CriteriaOperator.Parse(
                        "Statut = ? OR Statut = ? OR Statut = ? OR Statut = ?",
                        (int)DemandeStatut.Soumise,
                        (int)DemandeStatut.EnTraitement,
                        (int)DemandeStatut.Traitee,
                        (int)DemandeStatut.Rejetee);
                return;
            }

            // ── Employé : ses propres demandes + celles où il est valideur ──
            var criterePropres = CriteriaOperator.Parse("Salarie.Oid = ?", salConn.Oid);

            var critereValideurN1 = CriteriaOperator.Parse(
                "Statut = ? AND ValideurN1.Oid = ?",
                (int)DemandeStatut.EnAttenteN1, salConn.Oid);

            var critereValideurN2 = CriteriaOperator.Parse(
                "Statut = ? AND ValideurN2.Oid = ?",
                (int)DemandeStatut.EnAttenteN2, salConn.Oid);

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
