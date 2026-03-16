using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Workflow de l'entretien annuel (DetailView).
    /// Boutons : Planifier | Envoyer auto-évaluation | Valider manager | Recalculer score | Clôturer
    /// </summary>
    public class EntretienWorkflowController
        : ObjectViewController<DetailView, EntretienAnnuel>
    {
        readonly SimpleAction planifierAction;
        readonly SimpleAction envoyerAutoEvalAction;
        readonly SimpleAction validerManagerAction;
        readonly SimpleAction recalculerScoreAction;
        readonly SimpleAction cloturerAction;

        public EntretienWorkflowController()
        {
            planifierAction = new SimpleAction(this, "Entretien_Planifier", PredefinedCategory.Edit)
            {
                Caption = "Planifier",
                ImageName = "Action_New",
                ToolTip = "Fixer une date et passer en état Planifié."
            };
            planifierAction.Execute += PlanifierAction_Execute;

            envoyerAutoEvalAction = new SimpleAction(this, "Entretien_EnvoyerAutoEval", PredefinedCategory.Edit)
            {
                Caption = "Envoyer auto-évaluation",
                ImageName = "Action_Send",
                ToolTip = "Transmets le formulaire d'auto-évaluation au salarié."
            };
            envoyerAutoEvalAction.Execute += (s, e) =>
            {
                var en = (EntretienAnnuel)View.CurrentObject;
                en.EnvoyerAutoEvaluation();
                ObjectSpace.CommitChanges();
                UpdateStates();
                View.Refresh();
            };

            validerManagerAction = new SimpleAction(this, "Entretien_ValiderManager", PredefinedCategory.Edit)
            {
                Caption = "Valider (manager)",
                ImageName = "Action_Approve",
                ToolTip = "Recalcule le score et passe en état Validé par le manager.",
                ConfirmationMessage = "Valider cet entretien ? Le score sera calculé et figé."
            };
            validerManagerAction.Execute += (s, e) =>
            {
                var en = (EntretienAnnuel)View.CurrentObject;
                en.ValiderParManager();
                ObjectSpace.CommitChanges();
                UpdateStates();
                View.Refresh();
            };

            recalculerScoreAction = new SimpleAction(this, "Entretien_RecalculerScore", PredefinedCategory.View)
            {
                Caption = "Recalculer le score",
                ImageName = "Action_Refresh",
                ToolTip = "Recalcule le score pondéré à partir des notes saisies."
            };
            recalculerScoreAction.Execute += (s, e) =>
            {
                var en = (EntretienAnnuel)View.CurrentObject;
                en.RecalculerScore();
                ObjectSpace.CommitChanges();
                View.Refresh();
            };

            cloturerAction = new SimpleAction(this, "Entretien_Cloturer", PredefinedCategory.Edit)
            {
                Caption = "Clôturer",
                ImageName = "Action_Approve",
                ToolTip = "Archive définitivement l'entretien.",
                ConfirmationMessage = "Clôturer cet entretien ? Il passera en lecture seule."
            };
            cloturerAction.Execute += (s, e) =>
            {
                var en = (EntretienAnnuel)View.CurrentObject;
                en.Cloturer();
                ObjectSpace.CommitChanges();
                UpdateStates();
                View.Refresh();
            };
        }

        void PlanifierAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var en = (EntretienAnnuel)View.CurrentObject;
            // Utilise DatePlanifiee déjà saisie ou propose aujourd'hui + 7j
            var date = en.DatePlanifiee ?? DateTime.Today.AddDays(7);
            en.Planifier(date);
            ObjectSpace.CommitChanges();
            UpdateStates();
            View.Refresh();
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateStates();
        }

        void UpdateStates()
        {
            var en = View?.CurrentObject as EntretienAnnuel;
            if (en == null) return;

            planifierAction.Active["statut"] = en.Statut == EntretienStatut.Brouillon;
            envoyerAutoEvalAction.Active["statut"] = en.Statut == EntretienStatut.PlanifieRH;
            validerManagerAction.Active["statut"] = en.Statut == EntretienStatut.EnCours;
            recalculerScoreAction.Active["statut"] = en.Statut != EntretienStatut.Cloture;
            cloturerAction.Active["statut"] = en.Statut == EntretienStatut.ValideManager;
        }
    }
}
