using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Controller de validation hiérarchique pour les demandes d'attestation.
    ///
    /// Ajoute deux actions dans la ListView DemandeAttestation :
    ///   - "Valider (hiérarchie)"  → N+1 ou N+2 approuve
    ///   - "Rejeter (hiérarchie)" → N+1 ou N+2 refuse
    ///
    /// Les boutons sont visibles uniquement quand le user connecté
    /// est le ValideurN1 ou ValideurN2 de la demande sélectionnée.
    /// </summary>
    public class DemandeHierarchieWorkflowController
        : ObjectViewController<ListView, DemandeAttestation>
    {
        readonly SimpleAction validerAction;
        readonly SimpleAction rejeterAction;

        public DemandeHierarchieWorkflowController()
        {
            validerAction = new SimpleAction(this, "Hierarchie_Valider", PredefinedCategory.Edit)
            {
                Caption = "Valider (hiérarchie)",
                ImageName = "Action_Approve",
                ToolTip = "Approuve la demande et la transmet au niveau suivant ou au RH.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                ConfirmationMessage = "Valider cette demande et la transmettre au niveau suivant ?"
            };
            validerAction.Execute += ValiderAction_Execute;

            rejeterAction = new SimpleAction(this, "Hierarchie_Rejeter", PredefinedCategory.Edit)
            {
                Caption = "Rejeter (hiérarchie)",
                ImageName = "Action_Cancel",
                ToolTip = "Rejette la demande. Le salarié sera notifié.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                ConfirmationMessage = "Rejeter cette demande ? Le salarié recevra une notification."
            };
            rejeterAction.Execute += RejeterAction_Execute;
        }

        // ── Handlers ─────────────────────────────────────────────

        void ValiderAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var demande = (DemandeAttestation)e.CurrentObject;
            var salarieConnecte = GetSalarieConnecte();

            if (demande.Statut == DemandeStatut.EnAttenteN1)
            {
                demande.ValiderN1();

                // Notifier N+2 si la demande remonte
                if (demande.Statut == DemandeStatut.EnAttenteN2)
                    _NotifierResponsable(demande, demande.ValideurN2, "N+2");
                else
                    _NotifierRH(demande); // Pas de N+2 → RH directement
            }
            else if (demande.Statut == DemandeStatut.EnAttenteN2)
            {
                demande.ValiderN2();
                _NotifierRH(demande);
            }

            ObjectSpace.CommitChanges();
            View.Refresh();
        }

        void RejeterAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var demande = (DemandeAttestation)e.CurrentObject;

            demande.RejeterHierarchie();

            // Notifier le salarié
            _NotifierSalarie(demande,
                "Votre demande d'attestation a été refusée",
                $"Votre demande d'attestation {demande.Nature} du "
                + $"{demande.DateDemande:dd/MM/yyyy} a été refusée par votre responsable. "
                + "Motif : " + (demande.CommentaireRH ?? "voir votre responsable."));

            ObjectSpace.CommitChanges();
            View.Refresh();
        }

        // ── Activation conditionnelle ─────────────────────────────

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateStates();
            View.SelectionChanged += (_, __) => UpdateStates();
        }

        protected override void OnDeactivated()
        {
            View.SelectionChanged -= (_, __) => UpdateStates();
            base.OnDeactivated();
        }

        void UpdateStates()
        {
            var demande = View?.CurrentObject as DemandeAttestation;
            var salConn = GetSalarieConnecte();
            bool sel = demande != null;

            if (!sel || salConn == null)
            {
                validerAction.Active["sel"] = rejeterAction.Active["sel"] = false;
                return;
            }

            // Le bouton n'est actif que si le user connecté est le valideur attendu
            bool estValideurN1 = demande.Statut == DemandeStatut.EnAttenteN1
                              && demande.ValideurN1?.Oid == salConn.Oid;

            bool estValideurN2 = demande.Statut == DemandeStatut.EnAttenteN2
                              && demande.ValideurN2?.Oid == salConn.Oid;

            bool peutValider = estValideurN1 || estValideurN2;

            validerAction.Active["sel"] = peutValider;
            rejeterAction.Active["sel"] = peutValider;
        }

        // ── Helpers ──────────────────────────────────────────────

        /// <summary>Retourne la fiche Salarie de l'utilisateur connecté.</summary>
        private Salarie GetSalarieConnecte()
        {
            try
            {
                var userName = DevExpress.ExpressApp.SecuritySystem.CurrentUserName;
                var user = ObjectSpace.GetObjectsQuery<ApplicationUser>()
                    .FirstOrDefault(u => u.UserName == userName);
                return user?.Salarie;
            }
            catch { return null; }
        }

        /// <summary>Notifie un responsable qu'une demande attend sa validation.</summary>
        private void _NotifierResponsable(DemandeAttestation demande, Salarie responsable, string niveau)
        {
            if (responsable == null) return;
            try
            {
                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = responsable;
                notif.Titre = "Demande d'attestation en attente de votre validation";
                notif.Corps = $"{demande.Salarie?.FullName} a soumis une demande d'attestation "
                                + $"({demande.Nature}) le {demande.DateDemande:dd/MM/yyyy}. "
                                + $"Elle attend votre validation en tant que responsable {niveau}.";
                notif.Categorie = "Attestation";
                notif.Priorite = NotificationPriorite.Important;
                ObjectSpace.CommitChanges();
                WorkflowEmailHelper.EnvoyerNotifAsync(Application, notif);
            }
            catch (Exception ex) { Tracing.Tracer.LogError(ex); }
        }

        /// <summary>Notifie le RH qu'une demande a été validée par la hiérarchie.</summary>
        private void _NotifierRH(DemandeAttestation demande)
        {
            try
            {
                var rhEmails = WorkflowEmailHelper.ExtraireEmailsRH(Application);
                if (!rhEmails.Any()) return;

                var valideurs = demande.ValideurN1?.FullName
                    + (demande.ValideurN2 != null ? " → " + demande.ValideurN2.FullName : "");
                var sujet = $"[AdiPAIE] Demande d'attestation validée — {demande.Salarie?.FullName}";
                var body = WorkflowEmailHelper.HtmlTableau(
                    "Demande d'attestation — validée par la hiérarchie",
                    "La demande est disponible pour traitement dans AdiPAIE.",
                    new[]
                    {
                        ("Salarié",      demande.Salarie?.FullName ?? "—"),
                        ("Nature",       demande.Nature?.ToString() ?? "—"),
                        ("Date demande", demande.DateDemande.ToString("dd/MM/yyyy")),
                        ("Validé par",   valideurs),
                    });

                WorkflowEmailHelper.EnvoyerEmailsAsync(Application, rhEmails, sujet, body);
            }
            catch (Exception ex) { Tracing.Tracer.LogError(ex); }
        }

        /// <summary>Notifie le salarié d'un rejet hiérarchique.</summary>
        private void _NotifierSalarie(DemandeAttestation demande, string titre, string corps)
        {
            if (demande.Salarie == null) return;
            try
            {
                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = demande.Salarie;
                notif.Titre = titre;
                notif.Corps = corps;
                notif.Categorie = "Attestation";
                notif.Priorite = NotificationPriorite.Important;
                ObjectSpace.CommitChanges();
                WorkflowEmailHelper.EnvoyerNotifAsync(Application, notif);
            }
            catch (Exception ex) { Tracing.Tracer.LogError(ex); }
        }
    }
}
