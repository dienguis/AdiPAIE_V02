using AdiPAIE_V02.Module.Controllers.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    public class PeriodePaieWorkflowController
        : ObjectViewController<ListView, AdiPAIE_V02.Module.BusinessObjects.PeriodePaie>
    {
        SimpleAction ouvrirAction, reouvrirAction, cloturerAction;

        public PeriodePaieWorkflowController()
        {
            // Ouvrir
            ouvrirAction = new SimpleAction(this, "Periode_Ouvrir", PredefinedCategory.Edit)
            {
                Caption = "Ouvrir",
                ImageName = "Action_Open",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            ouvrirAction.Execute += (s, e) => {
                var p = (BusinessObjects.PeriodePaie)e.CurrentObject;
                p?.Ouvrir();

                if (p != null)
                {
                    AuditService.Enregistrer(Application, "PeriodePaie", "Ouvrir",
                        p.Oid.ToString(), p.DisplayName,
                        $"Période {p.DateDebut:MM/yyyy} à {p.DateFin:MM/yyyy} ouverte",
                        ancienStatut: Domain.DomainEnums.PeriodePaieStatut.Brouillon.ToString(),
                        nouveauStatut: Domain.DomainEnums.PeriodePaieStatut.Ouverte.ToString());
                }

                ObjectSpace.CommitChanges();
                View.Refresh();
                UpdateActionStates();

                // Email → RH : période ouverte
                if (p != null)
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                        WorkflowEmailHelper.ExtraireEmailsRH(Application),
                        $"[AdiPAIE] Période de paie ouverte — {p.DisplayName}",
                        WorkflowEmailHelper.HtmlTableau("Période de paie ouverte",
                            "La période est ouverte. Vous pouvez créer et calculer les bulletins.",
                            new[] {
                                ("Période", p.DisplayName ?? "—"),
                                ("Début", $"{p.DateDebut:dd/MM/yyyy}"),
                                ("Fin", $"{p.DateFin:dd/MM/yyyy}"),
                            }));
            };

            // Réouvrir
            reouvrirAction = new SimpleAction(this, "Periode_Reouvrir", PredefinedCategory.Edit)
            {
                Caption = "Réouvrir",
                ImageName = "Action_ResetViewSettings",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            reouvrirAction.Execute += (s, e) => {
                var p = (BusinessObjects.PeriodePaie)e.CurrentObject;
                p?.Reouvrir();

                if (p != null)
                {
                    AuditService.Enregistrer(Application, "PeriodePaie", "Réouvrir",
                        p.Oid.ToString(), p.DisplayName,
                        $"Période {p.DateDebut:MM/yyyy} à {p.DateFin:MM/yyyy} réouverte",
                        ancienStatut: Domain.DomainEnums.PeriodePaieStatut.Cloturee.ToString(),
                        nouveauStatut: Domain.DomainEnums.PeriodePaieStatut.Ouverte.ToString());
                }

                ObjectSpace.CommitChanges();
                View.Refresh();
                UpdateActionStates();
            };
         
            // Clôturer
            cloturerAction = new SimpleAction(this, "Periode_Cloturer", PredefinedCategory.Edit)
            {
                Caption = "Clôturer",
                ImageName = "Action_Approve",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                ConfirmationMessage = "Voulez-vous vraiment clôturer cette période ? Cette action est irréversible."
            };
            cloturerAction.Execute += (s, e) => {
                var p = (BusinessObjects.PeriodePaie)e.CurrentObject;
                p?.Cloturer();

                if (p != null)
                {
                    AuditService.Enregistrer(Application, "PeriodePaie", "Clôturer",
                        p.Oid.ToString(), p.DisplayName,
                        $"Période {p.DateDebut:MM/yyyy} à {p.DateFin:MM/yyyy} clôturée",
                        ancienStatut: Domain.DomainEnums.PeriodePaieStatut.Ouverte.ToString(),
                        nouveauStatut: Domain.DomainEnums.PeriodePaieStatut.Cloturee.ToString());
                }

                ObjectSpace.CommitChanges();
                View.Refresh();
                UpdateActionStates();

                // Email → RH + DAF + Comptable : période clôturée
                if (p != null)
                {
                    var dests = WorkflowEmailHelper.ExtraireEmailsRH(Application)
                        .Concat(WorkflowEmailHelper.ExtraireEmailsDAF(Application))
                        .Concat(WorkflowEmailHelper.ExtraireEmailsComptable(Application))
                        .Distinct().ToList();
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application, dests,
                        $"[AdiPAIE] Période de paie clôturée — {p.DisplayName}",
                        WorkflowEmailHelper.HtmlTableau("Période de paie clôturée",
                            "La période a été clôturée définitivement. Les bulletins sont verrouillés.",
                            new[] {
                                ("Période", p.DisplayName ?? "—"),
                                ("Début", $"{p.DateDebut:dd/MM/yyyy}"),
                                ("Fin", $"{p.DateFin:dd/MM/yyyy}"),
                            }));
                }
            };

            // Masquage simple par statut + Company renseignée
            ouvrirAction.TargetObjectsCriteria = "[Statut] = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PeriodePaieStatut,Brouillon# And Not IsNull(Company)";
            reouvrirAction.TargetObjectsCriteria = "[Statut] = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PeriodePaieStatut,Cloturee# And Not IsNull(Company)";
            cloturerAction.TargetObjectsCriteria = "[Statut] = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PeriodePaieStatut,Ouverte# And Not IsNull(Company)";
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            View.SelectionChanged += (_, __) => UpdateActionStates();
            ObjectSpace.Committed += (_, __) => UpdateActionStates();
            UpdateActionStates();
        }
        protected override void OnDeactivated()
        {
            View.SelectionChanged -= (_, __) => UpdateActionStates();
            ObjectSpace.Committed -= (_, __) => UpdateActionStates();
            base.OnDeactivated();
        }

        private void UpdateActionStates()
        {
            var p = View.CurrentObject as BusinessObjects.PeriodePaie;
            bool hasOne = p != null && View.SelectedObjects?.Count == 1;

            ouvrirAction.Active["sel"] = hasOne;
            reouvrirAction.Active["sel"] = hasOne;
            cloturerAction.Active["sel"] = hasOne;

            if (!hasOne)
            {
                ouvrirAction.Enabled["state"] = reouvrirAction.Enabled["state"] = cloturerAction.Enabled["state"] = false;
                return;
            }

            ouvrirAction.Enabled["state"] = p.Statut == Domain.DomainEnums.PeriodePaieStatut.Brouillon && p.Company != null;
            reouvrirAction.Enabled["state"] = p.Statut == Domain.DomainEnums.PeriodePaieStatut.Cloturee && p.Company != null;
            cloturerAction.Enabled["state"] = p.Statut == Domain.DomainEnums.PeriodePaieStatut.Ouverte && p.Company != null;

            ouvrirAction.ToolTip = "Ouvrir la période (bloqué s’il existe déjà une période ouverte ou si des périodes précédentes ne sont pas clôturées).";
            reouvrirAction.ToolTip = "Réouvrir une période clôturée (si autorisé).";
            cloturerAction.ToolTip = "Clôturer définitivement la période (tous les bulletins doivent être sortis de brouillon).";
        }
    }
}
