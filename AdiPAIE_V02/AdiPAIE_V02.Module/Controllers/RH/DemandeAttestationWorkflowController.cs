using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.Data.Utils;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Notifications;
using DevExpress.Persistent.Base;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Workflow du traitement des demandes d'attestation.
    /// ListView RH : Prendre en charge | Traiter | Rejeter
    /// </summary>
    public class DemandeAttestationWorkflowController
        : ObjectViewController<ListView, DemandeAttestation>
    {
        readonly SimpleAction prendreEnChargeAction;
        readonly SimpleAction traiterAction;
        readonly SimpleAction rejeterAction;

        public DemandeAttestationWorkflowController()
        {
            prendreEnChargeAction = new SimpleAction(this, "Demande_PrendreEnCharge", PredefinedCategory.Edit)
            {
                Caption = "Prendre en charge",
                ImageName = "Action_Open",
                ToolTip = "Marque la demande En traitement.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,Soumise#"
            };
            prendreEnChargeAction.Execute += (s, e) =>
            {
                var d = (DemandeAttestation)e.CurrentObject;
                d.PrendreEnCharge();
                ObjectSpace.CommitChanges();
                View.Refresh();
            };

            traiterAction = new SimpleAction(this, "Demande_Traiter", PredefinedCategory.Edit)
            {
                Caption = "Marquer Traitée",
                ImageName = "Action_Approve",
                ToolTip = "Indique que le document a été généré et remis.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,EnTraitement#",
                ConfirmationMessage = "Confirmer que l'attestation a été traitée et remise au salarié ?"
            };
            traiterAction.Execute += (s, e) =>
            {
                var d = (DemandeAttestation)e.CurrentObject;
                d.Traiter();

                // Crée une notification automatique pour le salarié
                _EnvoyerNotifTraitement(d);
                ObjectSpace.CommitChanges();
                View.Refresh();
            };

            rejeterAction = new SimpleAction(this, "Demande_Rejeter", PredefinedCategory.Edit)
            {
                Caption = "Rejeter",
                ImageName = "Action_Cancel",
                ToolTip = "Rejeter la demande avec un motif.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,Soumise# "
                                      + "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,EnTraitement#",
                ConfirmationMessage = "Rejeter cette demande ?"
            };
            rejeterAction.Execute += (s, e) =>
            {
                var d = (DemandeAttestation)e.CurrentObject;
                d.Rejeter("Demande non recevable - voir le service RH.");

                _EnvoyerNotifRejet(d);
                ObjectSpace.CommitChanges();
                View.Refresh();
            };
        }

        // ── Helpers notif ─────────────────────────────────────────
        void _EnvoyerNotifTraitement(DemandeAttestation demande)
        {
            try
            {
                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = demande.Salarie;
                notif.Titre = $"Votre attestation ({demande.Nature}) est disponible";
                notif.Corps = $"Votre demande {demande.Nature} du "
                                + $"{demande.DateDemande:dd/MM/yyyy} a été traitée. "
                                + "Vous pouvez la récupérer auprès du service RH.";
                notif.Categorie = "Attestation";
                notif.Priorite = NotificationPriorite.Important;

                ObjectSpace.CommitChanges();

                // Email en arrière-plan
                var notifOid = notif.Oid;
                var osFactory = Application.ServiceProvider
                    .GetService<IObjectSpaceFactory>();

                Task.Run(() =>
                {
                    try
                    {
                        using var os = osFactory.CreateObjectSpace(
                            typeof(NotificationSalarie));
                        var n = os.GetObjectByKey<NotificationSalarie>(notifOid);
                        if (n != null)
                            NotificationEmailService.Envoyer(n, os);
                    }
                    catch { }
                });
            }
            catch { }
        }

        void _EnvoyerNotifRejet(DemandeAttestation demande)
        {
            try
            {
                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = demande.Salarie;
                notif.Titre = "Demande d'attestation refusée";
                notif.Corps = $"Votre demande {demande.Nature} du "
                                + $"{demande.DateDemande:dd/MM/yyyy} n'a pas pu être traitée. "
                                + "Motif : " + (demande.CommentaireRH ?? "voir le service RH.");
                notif.Categorie = "Attestation";
                notif.Priorite = NotificationPriorite.Important;

                ObjectSpace.CommitChanges();

                // Email en arrière-plan
                var notifOid = notif.Oid;
                var osFactory = Application.ServiceProvider
                    .GetService<IObjectSpaceFactory>();

                Task.Run(() =>
                {
                    try
                    {
                        using var os = osFactory.CreateObjectSpace(
                            typeof(NotificationSalarie));
                        var n = os.GetObjectByKey<NotificationSalarie>(notifOid);
                        if (n != null)
                            NotificationEmailService.Envoyer(n, os);
                    }
                    catch { }
                });
            }
            catch { }
        }
        //void _EnvoyerNotifTraitement(DemandeAttestation demande)
        //{
        //    try
        //    {
        //        var notif = ObjectSpace.CreateObject<NotificationSalarie>();
        //        notif.Salarie = demande.Salarie;
        //        notif.Titre = $"Votre attestation ({demande.Nature}) est disponible";
        //        notif.Corps = $"Votre demande d'attestation {demande.Nature} du "
        //                    + $"{demande.DateDemande:dd/MM/yyyy} a été traitée. "
        //                    + "Vous pouvez la récupérer auprès du service RH.";
        //        notif.Categorie = "Attestation";
        //        notif.Priorite = NotificationPriorite.Important;

        //        ObjectSpace.CommitChanges();
        //        NotificationEmailService.Envoyer(notif, ObjectSpace);
        //    }
        //    catch { /* Ne pas bloquer le workflow si la notif échoue */ }
        //}

        //void _EnvoyerNotifRejet(DemandeAttestation demande)
        //{
        //    try
        //    {
        //        var notif = ObjectSpace.CreateObject<NotificationSalarie>();
        //        notif.Salarie = demande.Salarie;
        //        notif.Titre = $"Demande d'attestation refusée";
        //        notif.Corps = $"Votre demande d'attestation {demande.Nature} du "
        //                    + $"{demande.DateDemande:dd/MM/yyyy} n'a pas pu être traitée. "
        //                    + "Motif : " + (demande.CommentaireRH ?? "voir le service RH.");
        //        notif.Categorie = "Attestation";
        //        notif.Priorite = NotificationPriorite.Important;

        //        ObjectSpace.CommitChanges();
        //        NotificationEmailService.Envoyer(notif, ObjectSpace);
        //    }
        //    catch { }
        //}


    }
}
