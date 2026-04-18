using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Controller sur la DetailView CongeDemande.
    /// Débloque le champ DateReprise uniquement quand le RH clique "Accorder"
    /// pour forcer la saisie avant de valider.
    /// </summary>
    public class CongeAccordController
        : ObjectViewController<DetailView, CongeDemande>
    {
        readonly SimpleAction accorderDetailAction;

        public CongeAccordController()
        {
            accorderDetailAction = new SimpleAction(this,
                "Conge_Accorder_Detail", PredefinedCategory.Edit)
            {
                Caption = "Accorder",
                ImageName = "Action_Approve",
                ToolTip = "Accorde le congé — renseignez la date de reprise avant de valider.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Soumise#"
            };
            accorderDetailAction.Execute += AccorderDetail_Execute;
        }

        void AccorderDetail_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (CongeDemande)e.CurrentObject;

            // Vérifie que DateReprise a bien été saisie
            if (d.DateReprise == null)
            {
                throw new UserFriendlyException(
                    "Veuillez renseigner la date de reprise avant d'accorder le congé.");
            }

            if (d.DateReprise.Value.Date <= d.DateFin.Date)
            {
                throw new UserFriendlyException(
                    "La date de reprise doit être postérieure à la date de fin de congé.");
            }

            d.Accorder(d.DateReprise.Value);
            // Débiter le solde
            SoldeCongeCalculService.DebiterJours(ObjectSpace, d);

            // Impact paie si congé non payé
            if (d.Type?.ImpactSalaire == CongeImpactSalaire.Impaye
            // || d.Type?.ImpactSalaire == CongeImpactSalaire.Partiel
             )
            {
                CongeImpactPaieService.CreerRetenue(ObjectSpace, d);
                d.ImpactPaieGenere = true;
            }

            // Notification salarié
            _NotifierSalarie(d);

            AuditService.Enregistrer(Application, "CongeDemande", "Accorder",
                d.Oid.ToString(), d.Salarie?.FullName,
                $"Demande du {d.DateDebut:dd/MM/yyyy} au {d.DateFin:dd/MM/yyyy} — Reprise le {d.DateReprise:dd/MM/yyyy}",
                ancienStatut: "Soumise", nouveauStatut: "Accordée");
            ObjectSpace.CommitChanges();
            PlanningCongeController.MettreAJourEvenement(ObjectSpace, d);
            ObjectSpace.CommitChanges();
            View.Refresh();
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            // Débloque DateReprise pour saisie uniquement si Soumise
            UpdateDateRepriseEditable();
            ((DetailView)View).CurrentObjectChanged += (_, __) => UpdateDateRepriseEditable();
        }

        void UpdateDateRepriseEditable()
        {
            var d = View?.CurrentObject as CongeDemande;
            if (d == null) return;

            bool estSoumise = d.Statut == CongeStatut.Soumise;

            var editor = View.FindItem(nameof(CongeDemande.DateReprise));
            if (editor is DevExpress.ExpressApp.Editors.PropertyEditor pe)
            {
                // Forcer l'editabilite : supprimer tout verrou puis autoriser
                if (estSoumise)
                {
                    pe.AllowEdit.Clear();
                    pe.AllowEdit.SetItemValue("AccordMode", true);
                }
                else
                {
                    pe.AllowEdit.SetItemValue("AccordMode", false);
                }
            }
        }

        private void _NotifierSalarie(CongeDemande demande)
        {
            try
            {
                if (demande.Salarie == null) return;
                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = demande.Salarie;
                notif.Titre = "Votre congé a été accordé";
                notif.Corps = $"Votre congé du {demande.DateDebut:dd/MM/yyyy} "
                                + $"au {demande.DateFin:dd/MM/yyyy} ({demande.DureeJours:n1} jour(s)) "
                                + $"a été accordé. Date de reprise prévue : "
                                + $"{demande.DateReprise:dd/MM/yyyy}.";
                notif.Categorie = "Congé";
                notif.Priorite = NotificationPriorite.Important;

                ObjectSpace.CommitChanges();

                var notifOid = notif.Oid;
                var osFactory = Application.ServiceProvider
        .GetRequiredService<IObjectSpaceFactory>();

                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        using var os = osFactory.CreateObjectSpace(typeof(NotificationSalarie));
                        var n = os.GetObjectByKey<NotificationSalarie>(notifOid);
                        if (n != null)
                            AdiPAIE_V02.Module.Services.NotificationEmailService.Envoyer(n, os);
                    }
                    catch { }
                });
            }
            catch { }
        }
    }
}
