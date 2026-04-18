using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Controllers.RH;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;

namespace AdiPAIE_V02.Module.Controllers
{
    public class PretEcheancierController : ObjectViewController<DetailView, Pret>
    {
        SimpleAction genAction, suspendAction, relancerAction;

        public PretEcheancierController()
        {
            genAction = new SimpleAction(this, "GenererEcheancier", PredefinedCategory.Edit)
            {
                Caption = "Générer l'échéancier",
                ImageName = "Action_Refresh",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            genAction.Execute += (s, e) =>
            {
                var p = View.CurrentObject as Pret;
                if (p == null) return;
                p.GenererEcheancier(effacerExistant: true);
                ObjectSpace.CommitChanges();
                View.ObjectSpace.Refresh();
                Application.ShowViewStrategy.ShowMessage(
                    "Échéancier généré.", InformationType.Success, 2000, InformationPosition.Bottom);
            };

            suspendAction = new SimpleAction(this, "SuspendrePret", PredefinedCategory.Edit)
            {
                Caption = "Suspendre",
                ImageName = "Action_Suspend",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            suspendAction.Execute += (s, e) =>
            {
                var p = View.CurrentObject as Pret;
                if (p == null) return;
                var oldStatut = p.Statut;
                p.Statut = DomainEnums.PretStatut.Suspendu;

                AuditService.Enregistrer(Application, "Pret", "Suspendre",
                    p.Oid.ToString(), p.DisplayName,
                    "Prêt suspendu",
                    ancienStatut: oldStatut.ToString(), nouveauStatut: p.Statut.ToString());

                ObjectSpace.CommitChanges();
                View.ObjectSpace.Refresh();
                ActualiserVisibilite(p);

                // Email → Salarié : prêt suspendu
                var emailSal = p.Salarie?.Email?.Trim();
                if (!string.IsNullOrWhiteSpace(emailSal))
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                        new[] { emailSal },
                        "[AdiPAIE] Votre prêt a été suspendu",
                        WorkflowEmailHelper.HtmlTableau("Prêt suspendu",
                            "Les prélèvements sur votre salaire sont temporairement suspendus.",
                            new[] {
                                ("Prêt", p.DisplayName ?? "—"),
                                ("Montant", $"{p.MontantPrincipal:N0} FCFA"),
                            }));

                Application.ShowViewStrategy.ShowMessage(
                    "Prêt suspendu.", InformationType.Success, 2000, InformationPosition.Top);
            };

            relancerAction = new SimpleAction(this, "RelancerPret", PredefinedCategory.Edit)
            {
                Caption = "Relancer",
                ImageName = "Action_Grant",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            relancerAction.Execute += (s, e) =>
            {
                var p = View.CurrentObject as Pret;
                if (p == null) return;
                var oldStatut = p.Statut;
                p.Statut = DomainEnums.PretStatut.EnCours;
                p.RecalculerEtat();

                AuditService.Enregistrer(Application, "Pret", "Relancer",
                    p.Oid.ToString(), p.DisplayName,
                    "Prêt relancé",
                    ancienStatut: oldStatut.ToString(), nouveauStatut: p.Statut.ToString());

                ObjectSpace.CommitChanges();
                View.ObjectSpace.Refresh();
                ActualiserVisibilite(p);

                // Email → Salarié : prêt relancé
                var emailSalR = p.Salarie?.Email?.Trim();
                if (!string.IsNullOrWhiteSpace(emailSalR))
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                        new[] { emailSalR },
                        "[AdiPAIE] Votre prêt a été relancé",
                        WorkflowEmailHelper.HtmlTableau("Prêt relancé",
                            "Les prélèvements sur votre salaire vont reprendre dès le prochain bulletin.",
                            new[] {
                                ("Prêt", p.DisplayName ?? "—"),
                                ("Montant", $"{p.MontantPrincipal:N0} FCFA"),
                            }));

                Application.ShowViewStrategy.ShowMessage(
                    "Prêt relancé.", InformationType.Success, 2000, InformationPosition.Top);
            };
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            View.CurrentObjectChanged += (s, e) => ActualiserVisibilite(View.CurrentObject as Pret);
            ActualiserVisibilite(View.CurrentObject as Pret);
        }

        private void ActualiserVisibilite(Pret p)
        {
            if (p == null)
            {
                suspendAction.Active["HasObject"] = false;
                relancerAction.Active["HasObject"] = false;
                return;
            }

            var suspendu = p.Statut == DomainEnums.PretStatut.Suspendu;
            var termine = p.Statut == DomainEnums.PretStatut.Termine;

            // Suspendre : visible uniquement si EnCours
            suspendAction.Active["CanSuspend"] = !suspendu && !termine;
            // Relancer : visible uniquement si Suspendu
            relancerAction.Active["CanRelancer"] = suspendu;
        }
    }
}

