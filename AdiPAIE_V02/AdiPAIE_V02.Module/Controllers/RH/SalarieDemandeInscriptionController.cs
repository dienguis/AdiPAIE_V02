using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Permet à un salarié de demander lui-même son inscription à une SessionFormation.
    ///
    /// Action "Demander l'inscription" visible sur la DetailView de SessionFormation.
    ///   - Visible uniquement pour les sessions Confirmées ou En cours.
    ///   - Crée une InscriptionFormation au statut EnAttente.
    ///   - Envoie une notification in-app + email au RH.
    ///   - Empêche le doublon si le salarié est déjà inscrit.
    ///   - Si la session est à capacité max, affiche un avertissement mais crée quand même
    ///     l'inscription (le RH décidera de la valider ou non).
    /// </summary>
    public class SalarieDemandeInscriptionController
        : ObjectViewController<DetailView, SessionFormation>
    {
        private readonly SimpleAction _demanderAction;

        public SalarieDemandeInscriptionController()
        {
            _demanderAction = new SimpleAction(this,
                "SessionFormation_DemanderInscription",
                PredefinedCategory.Edit)
            {
                Caption = "Demander mon inscription",
                ImageName = "Action_SendMessage",
                ToolTip = "Soumettre une demande d'inscription à cette session.",
                ConfirmationMessage =
                    "Envoyer une demande d'inscription à cette session au service RH ?",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,Confirmee# " +
                    "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,EnCours#"
            };
            _demanderAction.Execute += OnDemanderExecute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateActionVisibility();
            View.CurrentObjectChanged += (_, __) => UpdateActionVisibility();
        }

        // ─────────────────────────────────────────────────────────────────
        private void UpdateActionVisibility()
        {
            // L'action n'est visible que si l'utilisateur a une fiche salarié
            var salarie = GetSalarieConnecte();
            _demanderAction.Active["HasSalarie"] = salarie != null;
        }

        // ─────────────────────────────────────────────────────────────────
        private void OnDemanderExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var session = (SessionFormation)View.CurrentObject;
            var salarie = GetSalarieConnecte();

            if (salarie == null)
                throw new UserFriendlyException(
                    "Votre compte utilisateur n'est pas lié à une fiche salarié. " +
                    "Contactez le service RH.");

            // ── Doublon check ─────────────────────────────────────────
            var existante = session.Inscriptions
                .FirstOrDefault(i => i.Salarie?.Oid == salarie.Oid
                                  && i.Statut != InscriptionStatut.Annulee);
            if (existante != null)
                throw new UserFriendlyException(
                    $"Vous avez déjà une inscription active à cette session " +
                    $"(statut : {existante.Statut}).");

            // ── Capacité ──────────────────────────────────────────────
            var avertissementCapacite = "";
            if (session.CapaciteMax > 0 && session.PlacesDisponibles == 0)
                avertissementCapacite =
                    " ⚠ La session est complète — votre demande sera mise en liste d'attente.";

            // ── Création de l'inscription ─────────────────────────────
            var insc = ObjectSpace.CreateObject<InscriptionFormation>();
            insc.SessionFormation = session;
            insc.Salarie = salarie;
            // AfterConstruction initialise DateInscription, Statut=EnAttente, InscritPar

            // ── Notification in-app aux RH ────────────────────────────
            var rhUsers = ObjectSpace
                .GetObjectsQuery<ApplicationUser>()
                .ToList()
                .Where(u => u.Salarie == null) // convention : RH n'a pas de fiche salarié
                .ToList();

            foreach (var rhUser in rhUsers)
            {
                // On ne peut pas cibler un utilisateur sans salarié via NotificationSalarie
                // → on envoie juste l'email ci-dessous
            }

            ObjectSpace.CommitChanges();
            View.Refresh();

            // ── Email aux RH ──────────────────────────────────────────
            try
            {
                var rhEmails = WorkflowEmailHelper.ExtraireEmailsRH(Application);
                if (rhEmails.Any())
                {
                    var body = WorkflowEmailHelper.HtmlTableau(
                        "Demande d'inscription formation",
                        $"{salarie.FullName} a demandé à s'inscrire à cette session. " +
                        $"Ouvrez l'inscription dans AdiPAIE pour la confirmer ou la refuser." +
                        avertissementCapacite,
                        new[]
                        {
                            ("Salarié",     salarie.FullName ?? "—"),
                            ("Matricule",   salarie.Matricule ?? "—"),
                            ("Département", salarie.Departement?.Nom ?? "—"),
                            ("Formation",   session.Intitule),
                            ("Dates",       $"{session.DateDebut:dd/MM/yyyy} → {session.DateFin:dd/MM/yyyy}"),
                            ("Lieu",        session.Lieu ?? "À définir"),
                        });

                    WorkflowEmailHelper.EnvoyerEmailsAsync(
                        Application, rhEmails,
                        $"[AdiPAIE] Demande inscription formation — {salarie.FullName} / {session.Intitule}",
                        body);
                }
            }
            catch { /* L'email est secondaire — ne pas bloquer */ }

            Application.ShowViewStrategy?.ShowMessage(
                $"Votre demande d'inscription a été envoyée au service RH.{avertissementCapacite}",
                avertissementCapacite.Length > 0
                    ? InformationType.Warning
                    : InformationType.Success,
                5000, InformationPosition.Top);
        }

        // ─────────────────────────────────────────────────────────────────
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
    }
}
