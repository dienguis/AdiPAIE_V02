using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using Microsoft.Extensions.DependencyInjection;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Workflow principal des demandes de congé.
    ///
    /// Actions Salarié :
    ///   - Soumettre
    ///
    /// Actions RH :
    ///   - Accorder (+ saisie date de reprise)
    ///   - Refuser
    ///   - Modifier les dates
    ///   - Annuler
    /// </summary>
    public class CongeWorkflowController
        : ObjectViewController<ListView, CongeDemande>
    {
        readonly SimpleAction soumettreAction;
        readonly SimpleAction accorderAction;
        readonly SimpleAction refuserAction;
        readonly SimpleAction modifierDatesAction;
        readonly SimpleAction annulerAction;

        public CongeWorkflowController()
        {
            // ── Salarié : Soumettre ───────────────────────────
            soumettreAction = new SimpleAction(this, "Conge_Soumettre", PredefinedCategory.Edit)
            {
                Caption = "Soumettre",
                ImageName = "Action_Forward",
                ToolTip = "Soumet la demande pour validation hiérarchique.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Brouillon#",
                ConfirmationMessage = "Soumettre cette demande de congé ?"
            };
            soumettreAction.Execute += SoumettreAction_Execute;

            // ── RH : Accorder ─────────────────────────────────
            accorderAction = new SimpleAction(this, "Conge_Accorder", PredefinedCategory.Edit)
            {
                Caption = "Accorder",
                ImageName = "Action_Approve",
                ToolTip = "Accorde le congé et renseigne la date de reprise.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Soumise#"
            };
            accorderAction.Execute += AccorderAction_Execute;

            // ── RH : Refuser ──────────────────────────────────
            refuserAction = new SimpleAction(this, "Conge_Refuser", PredefinedCategory.Edit)
            {
                Caption = "Refuser",
                ImageName = "Action_Cancel",
                ToolTip = "Refuse la demande de congé.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Soumise#",
                ConfirmationMessage = "Refuser cette demande de congé ?"
            };
            refuserAction.Execute += RefuserAction_Execute;

            // ── RH : Modifier les dates ───────────────────────
            modifierDatesAction = new SimpleAction(this, "Conge_ModifierDates", PredefinedCategory.Edit)
            {
                Caption = "Modifier les dates",
                ImageName = "Action_Edit",
                ToolTip = "Repasse la demande en Brouillon pour modifier les dates.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Soumise#",
                ConfirmationMessage = "Renvoyer la demande en brouillon pour modifier les dates ?"
            };
            modifierDatesAction.Execute += ModifierDatesAction_Execute;

            // ── RH : Annuler ──────────────────────────────────
            annulerAction = new SimpleAction(this, "Conge_Annuler", PredefinedCategory.Edit)
            {
                Caption = "Annuler le congé",
                ImageName = "Action_Close",
                ToolTip = "Annule un congé déjà accordé.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Accordee#",
                ConfirmationMessage = "Annuler ce congé accordé ?"
            };
            annulerAction.Execute += AnnulerAction_Execute;
        }

        // ── Handlers ─────────────────────────────────────────

        void SoumettreAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (CongeDemande)e.CurrentObject;
            d.Soumettre();

            _NotifierResponsable(d);

            ObjectSpace.CommitChanges();
            View.Refresh();
        }

        void AccorderAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (CongeDemande)e.CurrentObject;

            // Ouvre la DetailView pour saisir la date de reprise
            var osNew = Application.CreateObjectSpace(typeof(CongeDemande));
            var demande = osNew.GetObject(d);

            // Calcule la date de reprise par défaut = DateFin + 1 jour ouvrable
            var dateReprise = CalculerDateReprise(demande.DateFin);

            // Popup de saisie via DetailView du congé avec DateReprise débloquée
            demande.DateReprise = dateReprise;
            demande.Accorder(dateReprise);
            osNew.CommitChanges();

            _NotifierSalarie(d,
                $"Votre congé du {d.DateDebut:dd/MM/yyyy} au {d.DateFin:dd/MM/yyyy} a été accordé.",
                $"Date de reprise prévue : {dateReprise:dd/MM/yyyy}.");

            ObjectSpace.GetObjectByKey<CongeDemande>(d.Oid);
            ObjectSpace.Refresh();
            View.Refresh();
        }

        void RefuserAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (CongeDemande)e.CurrentObject;
            d.Refuser(d.CommentaireRH);

            _NotifierSalarie(d,
                "Votre demande de congé a été refusée.",
                $"Période : {d.DateDebut:dd/MM/yyyy} au {d.DateFin:dd/MM/yyyy}. "
                + "Motif : " + (d.CommentaireRH ?? "voir le service RH."));

            ObjectSpace.CommitChanges();
            View.Refresh();
        }

        void ModifierDatesAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (CongeDemande)e.CurrentObject;

            // Repasse en Brouillon pour permettre modification
            d.Statut = CongeStatut.Brouillon;
            d.ValideurN1 = null;
            d.ValideurN2 = null;
            d.DateValidationN1 = null;
            d.DateValidationN2 = null;

            _NotifierSalarie(d,
                "Votre demande de congé nécessite une modification.",
                $"Le service RH a renvoyé votre demande du "
                + $"{d.DateDebut:dd/MM/yyyy} au {d.DateFin:dd/MM/yyyy} en brouillon. "
                + (string.IsNullOrWhiteSpace(d.CommentaireRH) ? "" : "Commentaire : " + d.CommentaireRH));

            ObjectSpace.CommitChanges();
            View.Refresh();
        }

        void AnnulerAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (CongeDemande)e.CurrentObject;
            d.Annuler(d.CommentaireRH);

            _NotifierSalarie(d,
                "Votre congé a été annulé.",
                $"Le congé du {d.DateDebut:dd/MM/yyyy} au {d.DateFin:dd/MM/yyyy} "
                + "a été annulé par le service RH. "
                + (string.IsNullOrWhiteSpace(d.CommentaireRH) ? "" : "Motif : " + d.CommentaireRH));

            ObjectSpace.CommitChanges();
            View.Refresh();
        }

        // ── Helpers ──────────────────────────────────────────

        private DateTime CalculerDateReprise(DateTime dateFin)
        {
            var reprise = dateFin.AddDays(1);
            // Passe le weekend
            while (reprise.DayOfWeek == DayOfWeek.Saturday ||
                   reprise.DayOfWeek == DayOfWeek.Sunday)
                reprise = reprise.AddDays(1);
            return reprise;
        }

        private void _NotifierResponsable(CongeDemande demande)
        {
            try
            {
                var destinataire = demande.ValideurN1 ?? demande.Salarie?.Manager;
                if (destinataire == null) return;

                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = destinataire;
                notif.Titre = $"Demande de congé en attente de votre validation";
                notif.Corps = $"{demande.Salarie?.FullName} souhaite prendre un congé "
                                + $"du {demande.DateDebut:dd/MM/yyyy} au {demande.DateFin:dd/MM/yyyy} "
                                + $"({demande.DureeJours:n1} jour(s) — {demande.Type?.Libelle}).";
                notif.Categorie = "Congé";
                notif.Priorite = NotificationPriorite.Important;

                ObjectSpace.CommitChanges();
                _EnvoyerEmailAsync(notif);
            }
            catch { }
        }

        private void _NotifierSalarie(CongeDemande demande, string titre, string corps)
        {
            try
            {
                if (demande.Salarie == null) return;

                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = demande.Salarie;
                notif.Titre = titre;
                notif.Corps = corps;
                notif.Categorie = "Congé";
                notif.Priorite = NotificationPriorite.Important;

                ObjectSpace.CommitChanges();
                _EnvoyerEmailAsync(notif);
            }
            catch { }
        }

        private void _EnvoyerEmailAsync(NotificationSalarie notif)
        {
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
    }
}
