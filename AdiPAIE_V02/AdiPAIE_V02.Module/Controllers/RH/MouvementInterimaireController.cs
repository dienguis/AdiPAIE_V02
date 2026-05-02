using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Controller des mouvements intérimaires.
    /// Exigence ELTON : aucun mouvement n'est effectif sans validation RH.
    ///
    /// Actions :
    ///   - Valider RH    : valide le mouvement, met à jour le contrat actif
    ///   - Rejeter       : rejette le mouvement (retour brouillon)
    ///   - Fin de mission: clôture le contrat actif + statut Disponible
    /// </summary>
    public class MouvementInterimaireController
        : ObjectViewController<DetailView, MouvementInterimaire>
    {
        readonly SimpleAction validerRHAction;
        readonly SimpleAction rejeterAction;
        readonly SimpleAction finMissionAction;

        public MouvementInterimaireController()
        {
            // ── Valider RH ────────────────────────────────────────────
            validerRHAction = new SimpleAction(this,
                "Mvt_ValiderRH", PredefinedCategory.Edit)
            {
                Caption = "Valider",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Valider ce mouvement ? Il sera effectif immédiatement."
            };
            validerRHAction.Execute += OnValiderRH;

            // ── Rejeter ───────────────────────────────────────────────
            rejeterAction = new SimpleAction(this,
                "Mvt_Rejeter", PredefinedCategory.Edit)
            {
                Caption = "Rejeter",
                ImageName = "Action_Cancel",
                ConfirmationMessage = "Rejeter ce mouvement ?"
            };
            rejeterAction.Execute += (s, e) =>
            {
                var m = (MouvementInterimaire)View.CurrentObject;
                if (string.IsNullOrWhiteSpace(m.Motif))
                    throw new UserFriendlyException("Saisissez un motif de rejet.");
                // Réinitialiser — le mouvement reste en base pour historique
                // mais est marqué rejeté via ValideRH=false + motif
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Mouvement rejeté.",
                    InformationType.Warning, 3000, InformationPosition.Top);
            };

            // ── Fin de mission ────────────────────────────────────────
            finMissionAction = new SimpleAction(this,
                "Mvt_FinMission", PredefinedCategory.Edit)
            {
                Caption = "Fin de mission",
                ImageName = "Action_Close",
                ConfirmationMessage = "Clôturer la mission de cet intérimaire ?"
            };
            finMissionAction.Execute += OnFinMission;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateStates();
            View.CurrentObjectChanged += (_, __) => UpdateStates();
        }

        void UpdateStates()
        {
            var m = View?.CurrentObject as MouvementInterimaire;
            if (m == null) return;
            var nonValide = !m.ValideRH;
            validerRHAction.Active["s"] = nonValide;
            rejeterAction.Active["s"] = nonValide;
            finMissionAction.Active["s"] =
                m.TypeMouvement == MouvementInterimaireType.FinMission && nonValide;
        }

        // ── Valider le mouvement et appliquer les effets ──────────────
        private void OnValiderRH(object sender, SimpleActionExecuteEventArgs e)
        {
            var m = (MouvementInterimaire)View.CurrentObject;
            var inter = m.Interimaire;
            if (inter == null)
                throw new UserFriendlyException("Intérimaire non renseigné.");

            m.ValideRH = true;
            m.ValideParNom = DevExpress.ExpressApp.SecuritySystem.CurrentUserName;
            m.DateValidation = DateTime.Now;

            // Appliquer les effets selon le type de mouvement
            switch (m.TypeMouvement)
            {
                case MouvementInterimaireType.Affectation:
                case MouvementInterimaireType.Reaffectation:
                case MouvementInterimaireType.MutationInterne:
                    AppliquerMutation(m, inter);
                    break;

                case MouvementInterimaireType.FinMission:
                case MouvementInterimaireType.Demission:
                case MouvementInterimaireType.RuptureContrat:
                    AppliquerFinMission(m, inter);
                    break;
            }

            ObjectSpace.CommitChanges();
            UpdateStates(); View.Refresh();

            Application.ShowViewStrategy?.ShowMessage(
                "Mouvement validé et appliqué.",
                InformationType.Success, 3000, InformationPosition.Top);
        }

        private void OnFinMission(object sender, SimpleActionExecuteEventArgs e)
        {
            var m = (MouvementInterimaire)View.CurrentObject;
            if (m.Interimaire == null) return;

            m.TypeMouvement = MouvementInterimaireType.FinMission;
            m.ValideRH = true;
            m.ValideParNom = DevExpress.ExpressApp.SecuritySystem.CurrentUserName;
            m.DateValidation = DateTime.Now;

            AppliquerFinMission(m, m.Interimaire);

            ObjectSpace.CommitChanges();
            UpdateStates(); View.Refresh();
            Application.ShowViewStrategy?.ShowMessage(
                "Mission clôturée. Intérimaire disponible.",
                InformationType.Success, 3000, InformationPosition.Top);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private void AppliquerMutation(MouvementInterimaire m, Interimaire inter)
        {
            // Clôturer le contrat actif sur l'ancienne station/BU
            var contratActif = inter.ContratActif;
            if (contratActif != null)
            {
                contratActif.Statut = ContratInterimStatut.Termine;
                contratActif.DateFinReelle = DateTime.Today;
            }

            // Créer un nouveau contrat sur la nouvelle station/BU
            if (m.StationDestination != null || m.DestinationEstDG)
            {
                var nouveauContrat = ObjectSpace.CreateObject<ContratInterim>();
                nouveauContrat.Interimaire = inter;
                nouveauContrat.Station = m.StationDestination;
                nouveauContrat.BU = m.BUDestination;
                nouveauContrat.EstDG = m.DestinationEstDG;
                nouveauContrat.PosteOccupe = contratActif?.PosteOccupe;
                nouveauContrat.MotifRecours = m.Motif ?? "Mutation interne";
                nouveauContrat.DateDebut = m.DateMouvement;
                nouveauContrat.DateFin = contratActif?.DateFin ?? DateTime.Today.AddMonths(1);
                nouveauContrat.TauxJournalier = contratActif?.TauxJournalier ?? 0;
                nouveauContrat.TypeContrat = ContratInterimType.Renouvellement;
                nouveauContrat.Statut = ContratInterimStatut.EnCours;
                inter.Statut = InterimaireStatut.EnMission;
            }
        }

        private static void AppliquerFinMission(MouvementInterimaire m, Interimaire inter)
        {
            var contratActif = inter.ContratActif;
            if (contratActif != null)
            {
                contratActif.Statut = ContratInterimStatut.Termine;
                contratActif.DateFinReelle = DateTime.Today;
            }
            inter.Statut = InterimaireStatut.Disponible;
        }
    }
}
