// AdiPAIE_V02.Module/Controllers/RH/DisciplinaireController.cs
// Workflow disciplinaire : Initié → Notifié → Audition → Sanction → Clôturé
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Controller dossier disciplinaire.
    ///
    /// Workflow :
    ///   1. Notifier salarié  — enregistre la date de notification
    ///   2. Programmer audition — passe en "Audition programmée"
    ///   3. Enregistrer audition — PV rempli, passe en "Audition réalisée"
    ///   4. Prononcer sanction — enregistre la sanction choisie
    ///   5. Clôturer — archive le dossier
    /// </summary>
    public class DisciplinaireController
        : ObjectViewController<DetailView, DossierDisciplinaire>
    {
        readonly SimpleAction notifierAction;
        readonly SimpleAction programmerAuditionAction;
        readonly SimpleAction enregistrerAuditionAction;
        readonly SimpleAction prononcerSanctionAction;
        readonly SimpleAction cloturerAction;

        public DisciplinaireController()
        {
            // ── Notifier ─────────────────────────────────────────────
            notifierAction = new SimpleAction(this,
                "Discip_Notifier", PredefinedCategory.Edit)
            {
                Caption = "Notifier le salarié",
                ImageName = "BO_Mail",
                ToolTip = "Enregistre la notification de la procédure au salarié.",
                ConfirmationMessage = "Confirmer la notification au salarié ?"
            };
            notifierAction.Execute += (s, e) =>
            {
                var d = (DossierDisciplinaire)View.CurrentObject;
                if (d.Salarie == null)
                    throw new UserFriendlyException("Sélectionnez d'abord un salarié.");

                if (d.DateNotification == null)
                    d.DateNotification = DateTime.Today;
                d.Statut = DisciplinaireStatut.Notifie;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                // ── Envoi du mail de notification au salarié ──
                var emailSalarie = d.Salarie?.Email?.Trim();
                if (!string.IsNullOrWhiteSpace(emailSalarie))
                {
                    try
                    {
                        var categorie = d.Categorie?.ToString() ?? "—";
                        var dateFaits = d.DateFaits?.ToString("dd/MM/yyyy") ?? "—";
                        var body = WorkflowEmailHelper.HtmlTableau(
                            "Notification de procédure disciplinaire",
                            "Vous êtes prié(e) de prendre connaissance de cette notification. "
                            + "Une convocation pour audition vous sera communiquée ultérieurement.",
                            new (string, string)[]
                            {
                                ("Salarié", d.Salarie.FullName ?? "—"),
                                ("Référence", d.Reference ?? "—"),
                                ("Date des faits", dateFaits),
                                ("Catégorie de faute", categorie),
                                ("Description", d.DescriptionFaits ?? "—"),
                                ("Date de notification", d.DateNotification?.ToString("dd/MM/yyyy") ?? "—"),
                            });

                        var sender = WorkflowEmailHelper.ExtraireSender(Application);
                        if (sender != null)
                            WorkflowEmailHelper.EnvoyerAsync(sender, emailSalarie,
                                $"[SunuPaie] Notification disciplinaire — {d.Reference}",
                                body);
                    }
                    catch { /* L'envoi d'email ne doit pas bloquer le workflow */ }
                }

                AuditService.Enregistrer(Application, "DossierDisciplinaire", "Notifier",
                    d.Oid.ToString(), d.DisplayName,
                    $"Notification à {d.Salarie?.FullName} le {d.DateNotification:dd/MM/yyyy}"
                    + (string.IsNullOrWhiteSpace(emailSalarie) ? " (pas d'email)" : $" — email envoyé à {emailSalarie}"),
                    nouveauStatut: "Notifié");

                var msg = string.IsNullOrWhiteSpace(emailSalarie)
                    ? "Salarié notifié (pas d'adresse email — notification par email non envoyée)."
                    : $"Salarié notifié — email envoyé à {emailSalarie}.";
                Application.ShowViewStrategy?.ShowMessage(
                    msg, InformationType.Success, 4000, InformationPosition.Top);
            };

            // ── Programmer audition ──────────────────────────────────
            programmerAuditionAction = new SimpleAction(this,
                "Discip_ProgrammerAudition", PredefinedCategory.Edit)
            {
                Caption = "Programmer audition",
                ImageName = "BO_Scheduler",
                ToolTip = "Programme l'audition du salarié (obligatoire avant toute sanction)."
            };
            programmerAuditionAction.Execute += (s, e) =>
            {
                var d = (DossierDisciplinaire)View.CurrentObject;
                if (d.DateAudition == null)
                    throw new UserFriendlyException("Renseignez la date d'audition avant de continuer.");
                d.Statut = DisciplinaireStatut.AuditionProgrammee;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                AuditService.Enregistrer(Application, "DossierDisciplinaire", "Programmer audition",
                    d.Oid.ToString(), d.DisplayName,
                    $"Audition programmée le {d.DateAudition:dd/MM/yyyy}",
                    nouveauStatut: "Audition programmée");

                Application.ShowViewStrategy?.ShowMessage(
                    $"Audition programmée le {d.DateAudition:dd/MM/yyyy}.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Enregistrer audition ─────────────────────────────────
            enregistrerAuditionAction = new SimpleAction(this,
                "Discip_EnregistrerAudition", PredefinedCategory.Edit)
            {
                Caption = "Enregistrer audition",
                ImageName = "Action_Approve",
                ToolTip = "Confirme que l'audition a eu lieu et que le PV est rempli."
            };
            enregistrerAuditionAction.Execute += (s, e) =>
            {
                var d = (DossierDisciplinaire)View.CurrentObject;
                if (string.IsNullOrWhiteSpace(d.PVAudition))
                    throw new UserFriendlyException(
                        "Remplissez le procès-verbal d'audition avant de valider.");
                d.Statut = DisciplinaireStatut.AuditionRealisee;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                AuditService.Enregistrer(Application, "DossierDisciplinaire", "Enregistrer audition",
                    d.Oid.ToString(), d.DisplayName,
                    nouveauStatut: "Audition réalisée");

                Application.ShowViewStrategy?.ShowMessage(
                    "Audition enregistrée.", InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Prononcer sanction ───────────────────────────────────
            prononcerSanctionAction = new SimpleAction(this,
                "Discip_PrononcerSanction", PredefinedCategory.Edit)
            {
                Caption = "Prononcer la sanction",
                ImageName = "Action_RunDiagram",
                ConfirmationMessage = "Confirmer le prononcé de la sanction ?",
                ToolTip = "Enregistre officiellement la sanction disciplinaire."
            };
            prononcerSanctionAction.Execute += (s, e) =>
            {
                var d = (DossierDisciplinaire)View.CurrentObject;
                if (d.Sanction == null)
                    throw new UserFriendlyException("Sélectionnez d'abord le type de sanction.");
                if (string.IsNullOrWhiteSpace(d.MotivationSanction))
                    throw new UserFriendlyException("La motivation de la sanction est obligatoire.");
                if (d.Sanction == TypeSanction.MiseAPied && d.DureeMiseAPied > 8)
                    throw new UserFriendlyException(
                        "La mise à pied disciplinaire ne peut excéder 8 jours (Code du Travail sénégalais).");

                d.DateSanction = DateTime.Today;
                try { d.SanctionPar = SecuritySystem.CurrentUserName; } catch { }
                d.Statut = DisciplinaireStatut.SanctionPrononcee;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                AuditService.Enregistrer(Application, "DossierDisciplinaire", "Prononcer sanction",
                    d.Oid.ToString(), d.DisplayName,
                    $"Sanction : {d.Sanction}" + (d.DureeMiseAPied > 0 ? $" ({d.DureeMiseAPied} jours)" : ""),
                    nouveauStatut: "Sanction prononcée");

                Application.ShowViewStrategy?.ShowMessage(
                    $"Sanction prononcée : {d.Sanction}.",
                    InformationType.Success, 4000, InformationPosition.Top);
            };

            // ── Clôturer ─────────────────────────────────────────────
            cloturerAction = new SimpleAction(this,
                "Discip_Cloturer", PredefinedCategory.Edit)
            {
                Caption = "Clôturer le dossier",
                ImageName = "Action_Close",
                ConfirmationMessage = "Clôturer définitivement ce dossier disciplinaire ?"
            };
            cloturerAction.Execute += (s, e) =>
            {
                var d = (DossierDisciplinaire)View.CurrentObject;
                d.Statut = DisciplinaireStatut.Cloture;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                AuditService.Enregistrer(Application, "DossierDisciplinaire", "Clôturer",
                    d.Oid.ToString(), d.DisplayName,
                    nouveauStatut: "Clôturé");

                Application.ShowViewStrategy?.ShowMessage(
                    "Dossier disciplinaire clôturé.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateStates();
            View.CurrentObjectChanged += (_, __) => UpdateStates();
        }

        void UpdateStates()
        {
            var d = View?.CurrentObject as DossierDisciplinaire;
            if (d == null) return;
            var s = d.Statut;

            notifierAction.Active["s"] = s == DisciplinaireStatut.Initie;
            programmerAuditionAction.Active["s"] = s == DisciplinaireStatut.Notifie;
            enregistrerAuditionAction.Active["s"] = s == DisciplinaireStatut.AuditionProgrammee;
            prononcerSanctionAction.Active["s"] = s == DisciplinaireStatut.AuditionRealisee;
            cloturerAction.Active["s"] = s == DisciplinaireStatut.SanctionPrononcee;
        }
    }
}
