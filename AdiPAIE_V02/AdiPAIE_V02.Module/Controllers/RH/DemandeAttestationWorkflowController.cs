using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Domain;
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
                _ArchiverDansDossier(d);
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
                WorkflowEmailHelper.EnvoyerNotifAsync(Application, notif);
            }
            catch (Exception ex) { Tracing.Tracer.LogError(ex); }
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
                WorkflowEmailHelper.EnvoyerNotifAsync(Application, notif);
            }
            catch (Exception ex) { Tracing.Tracer.LogError(ex); }
        }

        void _ArchiverDansDossier(DemandeAttestation demande)
        {
            try
            {
                if (demande.Document?.Content == null || demande.Salarie == null) return;

                var session = ((DevExpress.ExpressApp.Xpo.XPObjectSpace)ObjectSpace).Session;

                // ── 1. Trouve ou crée le DossierSalarie ──────────────
                var dossier = ObjectSpace.GetObjectsQuery<DossierSalarie>()
                    .FirstOrDefault(d => d.Salarie.Oid == demande.Salarie.Oid);

                if (dossier == null)
                {
                    dossier = ObjectSpace.CreateObject<DossierSalarie>();
                    dossier.Salarie = demande.Salarie;
                }

                // ── 2. Titre du document ──────────────────────────────
                var titre = $"Attestation {demande.Nature} — {demande.DateDemande:dd/MM/yyyy}";

                // ── 3. Crée le DossierDocument ────────────────────────
                var doc = ObjectSpace.CreateObject<DossierDocument>();
                doc.Dossier = dossier;
                doc.Categorie = DomainEnums.DossierCategorieDocument.Attestation;
                doc.Titre = titre;
                doc.SourceAuto = "Généré automatiquement via Demande d'attestation";
                doc.DateDocument = DateTime.Today;
                doc.Confidentiel = false;

                // ── 4. Attache le fichier en PieceJointe ─────────────
                var pj = ObjectSpace.CreateObject<DossierPieceJointe>();
                pj.DossierDocument = doc;
                pj.Titre = demande.Document.FileName;

                if (pj.Fichier == null)
                    pj.Fichier = ObjectSpace.CreateObject<DevExpress.Persistent.BaseImpl.FileData>();

                using var ms = new System.IO.MemoryStream(demande.Document.Content);
                pj.Fichier.LoadFromStream(demande.Document.FileName, ms);
            }
            catch { /* ne pas bloquer le workflow si archivage échoue */ }
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
