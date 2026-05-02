using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.Services;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Workflow de la campagne d'évaluation.
    ///  - Ouvrir         : Brouillon → Ouverte
    ///  - Générer entretiens : crée un EntretienAnnuel pour chaque salarié actif
    ///  - Clôturer       : → Clôturée
    /// </summary>
    public class CampagneEvaluationWorkflowController
        : ObjectViewController<ListView, CampagneEvaluation>
    {
        readonly SimpleAction ouvrirAction;
        readonly SimpleAction genererAction;
        readonly SimpleAction cloturerAction;

        public CampagneEvaluationWorkflowController()
        {
            ouvrirAction = new SimpleAction(this, "Campagne_Ouvrir", PredefinedCategory.Edit)
            {
                Caption = "Ouvrir",
                ImageName = "Action_Open",
                ToolTip = "Passe la campagne en état Ouverte.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CampagneStatut,Brouillon#"
            };
            ouvrirAction.Execute += OuvrirAction_Execute;

            genererAction = new SimpleAction(this, "Campagne_GenererEntretiens", PredefinedCategory.Edit)
            {
                Caption = "Générer entretiens",
                ImageName = "BO_List",
                ToolTip = "Crée un entretien annuel pour chaque salarié actif de l'entreprise.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CampagneStatut,Ouverte#",
                ConfirmationMessage = "Créer les entretiens pour tous les salariés actifs ? Les entretiens existants ne seront pas écrasés."
            };
            genererAction.Execute += GenererAction_Execute;

            cloturerAction = new SimpleAction(this, "Campagne_Cloturer", PredefinedCategory.Edit)
            {
                Caption = "Clôturer",
                ImageName = "Action_Approve",
                ToolTip = "Archive définitivement la campagne.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria = "Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CampagneStatut,Cloturee# "
                                      + "AND Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CampagneStatut,Brouillon#",
                ConfirmationMessage = "Clôturer définitivement cette campagne ? Cette action est irréversible."
            };
            cloturerAction.Execute += CloturerAction_Execute;
        }

        // ── Handlers ─────────────────────────────────────────────
        void OuvrirAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var campagne = (CampagneEvaluation)e.CurrentObject;
            var ancienStatut = campagne.Statut.ToString();
            campagne.Ouvrir();
            var nouveauStatut = campagne.Statut.ToString();
            AuditService.Enregistrer(Application, "CampagneEvaluation", "Ouvrir",
                campagne.Oid.ToString(), campagne.DisplayName ?? campagne.Annee.ToString(),
                ancienStatut: ancienStatut, nouveauStatut: nouveauStatut);
            ObjectSpace.CommitChanges();
            View.Refresh();

            // Email → RH : campagne ouverte
            WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                WorkflowEmailHelper.ExtraireEmailsRH(Application),
                $"[AdiPAIE] Campagne d'évaluation ouverte — {campagne.Annee}",
                WorkflowEmailHelper.HtmlTableau("Campagne d'évaluation ouverte",
                    "La campagne est maintenant ouverte. Vous pouvez générer les entretiens.",
                    new[] {
                        ("Campagne", campagne.DisplayName ?? campagne.Annee.ToString()),
                        ("Statut", nouveauStatut),
                    }));
        }

        void GenererAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var campagne = (CampagneEvaluation)e.CurrentObject;
            var session = ((XPObjectSpace)ObjectSpace).Session;

            // Récupère tous les salariés actifs de l'entreprise
            var salaries = new XPQuery<Salarie>(session)
                .Where(s => s.IsActif == true)
                .ToList();

            int crees = 0;
            foreach (var sal in salaries)
            {
                // Idempotence : on ne crée pas si un entretien existe déjà pour ce salarié
                bool existe = campagne.Entretiens.Any(en => en.Salarie?.Oid == sal.Oid);
                if (existe) continue;

                var entretien = ObjectSpace.CreateObject<EntretienAnnuel>();
                entretien.Campagne = campagne;
                entretien.Salarie = sal;
                entretien.Statut = EntretienStatut.Brouillon;
                crees++;
            }

            campagne.LancerEntretiens();
            ObjectSpace.CommitChanges();
            View.Refresh();

            // Email → tous les salariés concernés : entretien créé
            var emailsSalaries = salaries
                .Where(s => !string.IsNullOrWhiteSpace(s.Email))
                .Select(s => s.Email.Trim())
                .Where(e => e.Contains('@'))
                .ToList();
            if (emailsSalaries.Any())
            {
                WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                    emailsSalaries,
                    $"[AdiPAIE] Campagne d'évaluation {campagne.Annee} — Votre entretien est planifié",
                    WorkflowEmailHelper.HtmlTableau("Entretien annuel planifié",
                        "Un entretien annuel a été créé dans le cadre de la campagne d'évaluation. Veuillez vous rapprocher de votre manager pour fixer la date.",
                        new[] {
                            ("Campagne", campagne.DisplayName ?? campagne.Annee.ToString()),
                        }));
            }

            Application.ShowViewStrategy?.ShowMessage(
                $"{crees} entretien(s) créé(s). Campagne passée en état « En cours ».",
                InformationType.Success, 5000, InformationPosition.Top);
        }

        void CloturerAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var campagne = (CampagneEvaluation)e.CurrentObject;
            var ancienStatut = campagne.Statut.ToString();
            campagne.Cloturer();
            var nouveauStatut = campagne.Statut.ToString();
            AuditService.Enregistrer(Application, "CampagneEvaluation", "Cloturer",
                campagne.Oid.ToString(), campagne.DisplayName ?? campagne.Annee.ToString(),
                ancienStatut: ancienStatut, nouveauStatut: nouveauStatut);
            ObjectSpace.CommitChanges();
            View.Refresh();

            // Email → RH : campagne clôturée
            WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                WorkflowEmailHelper.ExtraireEmailsRH(Application),
                $"[AdiPAIE] Campagne d'évaluation clôturée — {campagne.Annee}",
                WorkflowEmailHelper.HtmlTableau("Campagne d'évaluation clôturée",
                    "La campagne a été clôturée définitivement.",
                    new[] {
                        ("Campagne", campagne.DisplayName ?? campagne.Annee.ToString()),
                        ("Entretiens", $"{campagne.Entretiens?.Count ?? 0}"),
                    }));
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            View.SelectionChanged += (_, __) => UpdateStates();
            UpdateStates();
        }
        protected override void OnDeactivated()
        {
            View.SelectionChanged -= (_, __) => UpdateStates();
            base.OnDeactivated();
        }

        void UpdateStates()
        {
            var c = View.CurrentObject as CampagneEvaluation;
            bool sel = c != null && View.SelectedObjects?.Count == 1;
            ouvrirAction.Active["sel"] = genererAction.Active["sel"] = cloturerAction.Active["sel"] = sel;
        }
    }
}
