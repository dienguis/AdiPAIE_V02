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
    // ════════════════════════════════════════════════════════════
    // PLAN DE FORMATION — DetailView
    // Qui fait quoi :
    //   RH          → Soumettre, Démarrer, Clôturer, Annuler
    //   Direction   → Approuver, Rejeter
    //   (XAF sécurité gère la visibilité par rôle via permissions)
    // ════════════════════════════════════════════════════════════
    public class PlanFormationWorkflowController
        : ObjectViewController<DetailView, PlanFormation>
    {
        readonly SimpleAction soumettreAction;
        readonly SimpleAction approuverAction;
        readonly SimpleAction rejeterAction;
        readonly SimpleAction demarrerAction;
        readonly SimpleAction cloturerAction;
        readonly SimpleAction annulerAction;

        public PlanFormationWorkflowController()
        {
            // ── RH : Soumettre ────────────────────────────────
            soumettreAction = new SimpleAction(this,
                "PlanFormation_Soumettre", PredefinedCategory.Edit)
            {
                Caption = "Soumettre pour approbation",
                ImageName = "Action_Forward",
                ConfirmationMessage = "Soumettre ce plan à la direction pour approbation ?",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PlanFormationStatut,Brouillon#"
            };
            soumettreAction.Execute += (s, e) =>
            {
                var plan = (PlanFormation)View.CurrentObject;
                plan.Soumettre();
                ObjectSpace.CommitChanges();
                UpdateActions();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Plan soumis à la direction pour approbation.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Direction : Approuver ─────────────────────────
            approuverAction = new SimpleAction(this,
                "PlanFormation_Approuver", PredefinedCategory.Edit)
            {
                Caption = "Approuver",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Approuver ce plan de formation ?",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PlanFormationStatut,Soumis#"
            };
            approuverAction.Execute += (s, e) =>
            {
                var plan = (PlanFormation)View.CurrentObject;
                // Récupérer le salarié connecté comme approbateur
                var salarie = _GetSalarieConnecte();
                plan.Approuver(salarie);
                ObjectSpace.CommitChanges();
                UpdateActions();
                View.Refresh();

                // Notifier le RH
                var rhEmails = WorkflowEmailHelper.ExtraireEmailsRH(Application);
                if (rhEmails.Any())
                {
                    var body = WorkflowEmailHelper.HtmlTableau(
                        "Plan de formation approuvé",
                        "Le plan a été approuvé. Vous pouvez maintenant le démarrer et créer les sessions.",
                        new[]
                        {
                            ("Plan",    plan.Titre),
                            ("Année",   plan.Annee.ToString()),
                            ("Budget",  plan.BudgetPrevisionnel.ToString("N0") + " FCFA"),
                            ("Approuvé par", salarie?.FullName ?? "Direction"),
                        });
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application, rhEmails,
                        $"[AdiPAIE] Plan de formation approuvé — {plan.Titre}", body);
                }

                Application.ShowViewStrategy?.ShowMessage(
                    "Plan approuvé. Le RH a été notifié.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Direction : Rejeter ───────────────────────────
            rejeterAction = new SimpleAction(this,
                "PlanFormation_Rejeter", PredefinedCategory.Edit)
            {
                Caption = "Rejeter",
                ImageName = "Action_Cancel",
                ConfirmationMessage = "Rejeter ce plan ? Il retournera en brouillon pour correction.",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PlanFormationStatut,Soumis#"
            };
            rejeterAction.Execute += (s, e) =>
            {
                var plan = (PlanFormation)View.CurrentObject;
                if (string.IsNullOrWhiteSpace(plan.Commentaire))
                    throw new UserFriendlyException(
                        "Veuillez saisir un motif de rejet dans le champ Commentaire avant de rejeter.");
                plan.Rejeter(plan.Commentaire);
                ObjectSpace.CommitChanges();
                UpdateActions();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Plan rejeté. Le RH peut le corriger et le resoumettre.",
                    InformationType.Warning, 3000, InformationPosition.Top);
            };

            // ── RH : Démarrer ─────────────────────────────────
            demarrerAction = new SimpleAction(this,
                "PlanFormation_Demarrer", PredefinedCategory.Edit)
            {
                Caption = "Démarrer",
                ImageName = "Action_RunDiagram",
                ConfirmationMessage = "Démarrer l'exécution du plan de formation ?",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PlanFormationStatut,Approuve#"
            };
            demarrerAction.Execute += (s, e) =>
            {
                var plan = (PlanFormation)View.CurrentObject;
                plan.Demarrer();
                ObjectSpace.CommitChanges();
                UpdateActions();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Plan démarré. Vous pouvez créer les sessions.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── RH : Clôturer ─────────────────────────────────
            cloturerAction = new SimpleAction(this,
                "PlanFormation_Cloturer", PredefinedCategory.Edit)
            {
                Caption = "Clôturer",
                ImageName = "Action_Close",
                ConfirmationMessage = "Clôturer définitivement ce plan de formation ?",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PlanFormationStatut,EnCours#"
            };
            cloturerAction.Execute += (s, e) =>
            {
                var plan = (PlanFormation)View.CurrentObject;
                plan.Cloturer();
                ObjectSpace.CommitChanges();
                UpdateActions();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Plan clôturé.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── RH : Annuler ──────────────────────────────────
            annulerAction = new SimpleAction(this,
                "PlanFormation_Annuler", PredefinedCategory.Edit)
            {
                Caption = "Annuler le plan",
                ImageName = "Action_Delete",
                ConfirmationMessage = "Annuler ce plan ? Cette action est irréversible.",
                TargetObjectsCriteria =
                    "Statut != ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PlanFormationStatut,Cloture# AND " +
                    "Statut != ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+PlanFormationStatut,Annule#"
            };
            annulerAction.Execute += (s, e) =>
            {
                var plan = (PlanFormation)View.CurrentObject;
                plan.Annuler();
                ObjectSpace.CommitChanges();
                UpdateActions();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Plan annulé.",
                    InformationType.Warning, 3000, InformationPosition.Top);
            };
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateActions();
            View.CurrentObjectChanged += (_, __) => UpdateActions();
        }

        void UpdateActions()
        {
            var plan = View?.CurrentObject as PlanFormation;
            if (plan == null) return;
            var s = plan.Statut;

            soumettreAction.Active["s"] = s == PlanFormationStatut.Brouillon;
            approuverAction.Active["s"] = s == PlanFormationStatut.Soumis;
            rejeterAction.Active["s"] = s == PlanFormationStatut.Soumis;
            demarrerAction.Active["s"] = s == PlanFormationStatut.Approuve;
            cloturerAction.Active["s"] = s == PlanFormationStatut.EnCours;
            annulerAction.Active["s"] = s != PlanFormationStatut.Cloture
                                       && s != PlanFormationStatut.Annule;
        }

        Salarie _GetSalarieConnecte()
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

    // ════════════════════════════════════════════════════════════
    // SESSION DE FORMATION — DetailView
    // Qui fait quoi :
    //   RH → Confirmer, Démarrer, Terminer, Annuler
    // ════════════════════════════════════════════════════════════
    public class SessionFormationWorkflowController
        : ObjectViewController<DetailView, SessionFormation>
    {
        readonly SimpleAction confirmerAction;
        readonly SimpleAction demarrerAction;
        readonly SimpleAction terminerAction;
        readonly SimpleAction annulerAction;

        public SessionFormationWorkflowController()
        {
            // ── Confirmer ─────────────────────────────────────
            confirmerAction = new SimpleAction(this,
                "SessionFormation_Confirmer", PredefinedCategory.Edit)
            {
                Caption = "Confirmer la session",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Confirmer cette session ? Les inscrits seront notifiés.",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,Planifiee#"
            };
            confirmerAction.Execute += (s, e) =>
            {
                var session = (SessionFormation)View.CurrentObject;
                session.Confirmer();
                ObjectSpace.CommitChanges();
                UpdateActions();
                View.Refresh();

                // Notifier tous les inscrits confirmés
                var inscrits = session.Inscriptions
                    .Where(i => i.Statut == InscriptionStatut.Confirmee
                             || i.Statut == InscriptionStatut.EnAttente)
                    .ToList();

                foreach (var insc in inscrits.Where(i => i.Salarie?.Email != null))
                {
                    var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                    notif.Salarie = insc.Salarie;
                    notif.Titre = $"Convocation — {session.Intitule}";
                    notif.Corps = $"Vous êtes convoqué(e) à la formation '{session.Intitule}' "
                                    + $"du {session.DateDebut:dd/MM/yyyy} au {session.DateFin:dd/MM/yyyy} "
                                    + $"({session.DureeJours} jour(s)). "
                                    + (string.IsNullOrWhiteSpace(session.Lieu) ? "" : $"Lieu : {session.Lieu}.");
                    notif.Categorie = "Formation";
                    notif.Priorite = NotificationPriorite.Important;
                }
                ObjectSpace.CommitChanges();

                // Email aux inscrits
                var sender = WorkflowEmailHelper.ExtraireSender(Application);
                if (sender != null)
                {
                    foreach (var insc in inscrits.Where(i =>
                        !string.IsNullOrWhiteSpace(i.Salarie?.Email)))
                    {
                        var body = WorkflowEmailHelper.HtmlTableau(
                            $"Convocation — {session.Intitule}",
                            $"Vous êtes convoqué(e) à cette formation. Merci de confirmer votre présence.",
                            new[]
                            {
                                ("Formation",  session.Intitule),
                                ("Domaine",    session.Domaine?.Libelle ?? "—"),
                                ("Dates",      $"{session.DateDebut:dd/MM/yyyy} → {session.DateFin:dd/MM/yyyy}"),
                                ("Durée",      $"{session.DureeJours} jour(s) / {session.DureeHeures} h"),
                                ("Lieu",       session.Lieu ?? "À confirmer"),
                                ("Formateur",  session.FormateurNom ?? "—"),
                            });
                        WorkflowEmailHelper.EnvoyerAsync(sender, insc.Salarie.Email,
                            $"[AdiPAIE] Convocation formation — {session.Intitule}", body);
                    }
                }

                Application.ShowViewStrategy?.ShowMessage(
                    $"Session confirmée. {inscrits.Count} inscrit(s) notifié(s).",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Démarrer ──────────────────────────────────────
            demarrerAction = new SimpleAction(this,
                "SessionFormation_Demarrer", PredefinedCategory.Edit)
            {
                Caption = "Démarrer la session",
                ImageName = "Action_RunDiagram",
                ConfirmationMessage = "Démarrer la session maintenant ?",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,Confirmee#"
            };
            demarrerAction.Execute += (s, e) =>
            {
                var session = (SessionFormation)View.CurrentObject;
                session.Demarrer();
                ObjectSpace.CommitChanges();
                UpdateActions();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Session démarrée.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Terminer ──────────────────────────────────────
            terminerAction = new SimpleAction(this,
                "SessionFormation_Terminer", PredefinedCategory.Edit)
            {
                Caption = "Terminer la session",
                ImageName = "Action_Close",
                ConfirmationMessage =
                    "Terminer la session ? Les suivis post-formation seront créés pour les participants présents.",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,EnCours#"
            };
            terminerAction.Execute += (s, e) =>
            {
                var session = (SessionFormation)View.CurrentObject;
                session.Terminer();

                // Créer les SuiviFormation pour toutes les inscriptions non annulées
                // (Presence peut être saisie après — on crée le suivi pour tout le monde)
                // Filtre : Confirmée OU Absente (pas Annulée)
                var inscriptions = session.Inscriptions
                    .Where(i => i.Statut == InscriptionStatut.Confirmee
                             || i.Statut == InscriptionStatut.Absente
                             || i.Statut == InscriptionStatut.EnAttente)
                    .ToList();

                // Charger tous les suivis de cette session pour le check idempotent
                // On passe par .ToList() côté client pour éviter les requêtes XPO
                // sur propriétés de navigation (non supportées par le provider LINQ XPO)
                var dejaCrees = ObjectSpace.GetObjectsQuery<SuiviFormation>()
                    .ToList()
                    .Where(sf => sf.Inscription != null
                              && inscriptions.Any(i => i.Oid == sf.Inscription.Oid))
                    .Select(sf => sf.Inscription.Oid)
                    .ToHashSet();

                int nbSuivis = 0;
                foreach (var insc in inscriptions)
                {
                    // Idempotent — pas de doublon
                    if (dejaCrees.Contains(insc.Oid)) continue;

                    var suivi = SuiviFormation.CreerDepuisInscription(ObjectSpace, insc);
                    if (suivi != null) nbSuivis++;
                }

                ObjectSpace.CommitChanges();
                UpdateActions();
                View.Refresh();

                Application.ShowViewStrategy?.ShowMessage(
                    $"Session terminée. {nbSuivis} suivi(s) post-formation créé(s).",
                    InformationType.Success, 4000, InformationPosition.Top);
            };

            // ── Annuler ───────────────────────────────────────
            annulerAction = new SimpleAction(this,
                "SessionFormation_Annuler", PredefinedCategory.Edit)
            {
                Caption = "Annuler la session",
                ImageName = "Action_Delete",
                ConfirmationMessage = "Annuler cette session ? Les inscrits seront notifiés.",
                TargetObjectsCriteria =
                    "Statut != ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,Terminee# AND " +
                    "Statut != ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,Annulee#"
            };
            annulerAction.Execute += (s, e) =>
            {
                var session = (SessionFormation)View.CurrentObject;
                if (string.IsNullOrWhiteSpace(session.MotifAnnulation))
                    throw new UserFriendlyException(
                        "Veuillez saisir un motif d'annulation dans le champ prévu avant d'annuler.");

                session.Annuler(session.MotifAnnulation);

                // Notifier les inscrits
                var inscrits = session.Inscriptions
                    .Where(i => i.Statut != InscriptionStatut.Annulee)
                    .ToList();

                foreach (var insc in inscrits.Where(i => i.Salarie != null))
                {
                    var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                    notif.Salarie = insc.Salarie;
                    notif.Titre = $"Session annulée — {session.Intitule}";
                    notif.Corps = $"La session '{session.Intitule}' du {session.DateDebut:dd/MM/yyyy} "
                                    + $"a été annulée. Motif : {session.MotifAnnulation}.";
                    notif.Categorie = "Formation";
                    notif.Priorite = NotificationPriorite.Important;
                }

                ObjectSpace.CommitChanges();
                UpdateActions();
                View.Refresh();

                Application.ShowViewStrategy?.ShowMessage(
                    $"Session annulée. {inscrits.Count} inscrit(s) notifié(s).",
                    InformationType.Warning, 3000, InformationPosition.Top);
            };
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateActions();
            View.CurrentObjectChanged += (_, __) => UpdateActions();
        }

        void UpdateActions()
        {
            var session = View?.CurrentObject as SessionFormation;
            if (session == null) return;
            var s = session.Statut;

            confirmerAction.Active["s"] = s == SessionFormationStatut.Planifiee;
            demarrerAction.Active["s"] = s == SessionFormationStatut.Confirmee;
            terminerAction.Active["s"] = s == SessionFormationStatut.EnCours;
            annulerAction.Active["s"] = s != SessionFormationStatut.Terminee
                                       && s != SessionFormationStatut.Annulee;
        }
    }

    // ════════════════════════════════════════════════════════════
    // INSCRIPTION — Actions rapides sur DetailView
    // ════════════════════════════════════════════════════════════
    public class InscriptionFormationWorkflowController
        : ObjectViewController<DetailView, InscriptionFormation>
    {
        readonly SimpleAction confirmerAction;
        readonly SimpleAction annulerAction;
        readonly SimpleAction marquerAbsentAction;
        readonly SimpleAction genererAttestationAction;

        public InscriptionFormationWorkflowController()
        {
            confirmerAction = new SimpleAction(this,
                "Inscription_Confirmer", PredefinedCategory.Edit)
            {
                Caption = "Confirmer",
                ImageName = "Action_Approve",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+InscriptionStatut,EnAttente#"
            };
            confirmerAction.Execute += (s, e) =>
            {
                ((InscriptionFormation)View.CurrentObject).Confirmer();
                ObjectSpace.CommitChanges();
                UpdateActions(); View.Refresh();
            };

            annulerAction = new SimpleAction(this,
                "Inscription_Annuler", PredefinedCategory.Edit)
            {
                Caption = "Annuler l'inscription",
                ImageName = "Action_Cancel",
                ConfirmationMessage = "Annuler cette inscription ?",
                TargetObjectsCriteria =
                    "Statut != ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+InscriptionStatut,Annulee#"
            };
            annulerAction.Execute += (s, e) =>
            {
                var insc = (InscriptionFormation)View.CurrentObject;
                if (string.IsNullOrWhiteSpace(insc.MotifAnnulation))
                    throw new UserFriendlyException(
                        "Veuillez saisir un motif d'annulation avant d'annuler.");
                insc.Annuler(insc.MotifAnnulation);
                ObjectSpace.CommitChanges();
                UpdateActions(); View.Refresh();
            };

            marquerAbsentAction = new SimpleAction(this,
                "Inscription_MarquerAbsent", PredefinedCategory.Edit)
            {
                Caption = "Marquer absent",
                ImageName = "Action_Clear",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+InscriptionStatut,Confirmee#"
            };
            marquerAbsentAction.Execute += (s, e) =>
            {
                ((InscriptionFormation)View.CurrentObject).MarquerAbsent();
                ObjectSpace.CommitChanges();
                UpdateActions(); View.Refresh();
            };

            // ── Générer l'attestation ─────────────────────────
            genererAttestationAction = new SimpleAction(this,
                "Inscription_GenererAttestation", PredefinedCategory.Edit)
            {
                Caption = "Générer l'attestation",
                ImageName = "Action_Export",
                ToolTip = "Génère l'attestation PDF et l'archive dans le dossier salarié.",
                TargetObjectsCriteria =
                    "Presence = true AND " +
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+InscriptionStatut,Confirmee#"
            };
            genererAttestationAction.Execute += (s, e) =>
            {
                var insc = (InscriptionFormation)View.CurrentObject;
                try
                {
                    var result = AdiPAIE_V02.Module.Services.AttestationFormationService
                        .GenererEtArchiver(ObjectSpace, insc);
                    ObjectSpace.CommitChanges();
                    View.Refresh();
                    Application.ShowViewStrategy?.ShowMessage(
                        result.Message,
                        result.EstReussi ? InformationType.Success : InformationType.Warning,
                        4000, InformationPosition.Top);
                }
                catch (Exception ex)
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        $"Erreur : {ex.Message}",
                        InformationType.Error, 6000, InformationPosition.Top);
                }
            };
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateActions();
            View.CurrentObjectChanged += (_, __) => UpdateActions();
        }

        void UpdateActions()
        {
            var insc = View?.CurrentObject as InscriptionFormation;
            if (insc == null) return;
            var s = insc.Statut;
            confirmerAction.Active["s"] = s == InscriptionStatut.EnAttente;
            annulerAction.Active["s"] = s != InscriptionStatut.Annulee;
            marquerAbsentAction.Active["s"] = s == InscriptionStatut.Confirmee;
            genererAttestationAction.Active["s"] = s == InscriptionStatut.Confirmee;
        }
    }
}
