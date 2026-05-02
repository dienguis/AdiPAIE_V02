using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Controller offboarding.
    ///
    /// Workflow :
    ///   1. Calculer solde — calcule automatiquement tous les éléments
    ///   2. Valider RH     — approuve le solde calculé
    ///   3. Valider DAF    — validation financière finale
    ///   4. Clôturer       — désactive le salarié, pose DateSortie, archive
    /// </summary>
    public class OffboardingController
        : ObjectViewController<DetailView, DossierOffboarding>
    {
        readonly SimpleAction preRemplirAction;
        readonly SimpleAction calculerAction;
        readonly SimpleAction validerRHAction;
        readonly SimpleAction validerDAFAction;
        readonly SimpleAction cloturerAction;

        public OffboardingController()
        {
            // ── Pré-remplir ───────────────────────────────────────────
            preRemplirAction = new SimpleAction(this,
                "Offboarding_PreRemplir", PredefinedCategory.Edit)
            {
                Caption = "Charger salarié",
                ImageName = "Action_Refresh",
                ToolTip = "Charge le salaire, l'ancienneté et les congés restants."
            };
            preRemplirAction.Execute += (s, e) =>
            {
                var d = (DossierOffboarding)View.CurrentObject;
                if (d.Salarie == null)
                    throw new UserFriendlyException("Sélectionnez d'abord un salarié.");
                ObjectSpace.SetModified(d);
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Données chargées.",
                    InformationType.Success, 2000, InformationPosition.Top);
            };

            // ── Calculer solde ────────────────────────────────────────
            calculerAction = new SimpleAction(this,
                "Offboarding_Calculer", PredefinedCategory.Edit)
            {
                Caption = "Calculer solde",
                ImageName = "Action_RunDiagram",
                ConfirmationMessage = "Calculer le solde de tout compte ?",
                ToolTip = "Calcule automatiquement tous les éléments du solde de tout compte."
            };
            calculerAction.Execute += (s, e) =>
            {
                var d = (DossierOffboarding)View.CurrentObject;
                if (d.Salarie == null)
                    throw new UserFriendlyException("Salarié non renseigné.");
                if (d.MotifDepart == null)
                    throw new UserFriendlyException("Motif de départ non renseigné.");
                if (d.SalaireBase <= 0)
                    throw new UserFriendlyException(
                        "Chargez d'abord les données salarié (salaire = 0).");

                d.CalculerSoldeToutCompte();
                d.Statut = OffboardingStatut.SoldeCalcule;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                AuditService.Enregistrer(Application, "DossierOffboarding", "Calculer STC",
                    d.Oid.ToString(), d.DisplayName,
                    $"Total STC : {d.TotalSoldeToutCompte:N0} FCFA — Congés: {d.IndemniteCongés:N0}, Préavis: {d.IndemnitePreavis:N0}, Licenciement: {d.IndemniteLicenciement:N0}",
                    nouveauStatut: "Solde calculé");

                // Email → RH : STC calculé, à valider
                WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                    WorkflowEmailHelper.ExtraireEmailsRH(Application),
                    $"[AdiPAIE] STC calculé — {d.Salarie?.FullName}",
                    WorkflowEmailHelper.HtmlTableau("Solde de tout compte calculé",
                        "Le STC est prêt pour validation RH.",
                        new[] {
                            ("Salarié", d.Salarie?.FullName ?? "—"),
                            ("Motif", d.MotifDepart?.ToString() ?? "—"),
                            ("Total STC", $"{d.TotalSoldeToutCompte:N0} FCFA"),
                            ("Congés", $"{d.IndemniteCongés:N0} FCFA"),
                            ("Préavis", $"{d.IndemnitePreavis:N0} FCFA"),
                            ("Licenciement", $"{d.IndemniteLicenciement:N0} FCFA"),
                        }));

                Application.ShowViewStrategy?.ShowMessage(
                    $"Solde calculé : {d.TotalSoldeToutCompte:N0} FCFA",
                    InformationType.Success, 4000, InformationPosition.Top);
            };

            // ── Valider RH ────────────────────────────────────────────
            validerRHAction = new SimpleAction(this,
                "Offboarding_ValiderRH", PredefinedCategory.Edit)
            {
                Caption = "Valider (RH)",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Approuver le solde de tout compte ?"
            };
            validerRHAction.Execute += (s, e) =>
            {
                var d = (DossierOffboarding)View.CurrentObject;
                d.ValideRHPar = SecuritySystem.CurrentUserName;
                d.DateValidationRH = DateTime.Now;
                d.Statut = OffboardingStatut.ValideRH;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();
                AuditService.Enregistrer(Application, "DossierOffboarding", "Valider RH",
                    d.Oid.ToString(), d.DisplayName,
                    ancienStatut: "Solde calculé", nouveauStatut: "Validé RH");

                // Email → DAF : STC validé RH, en attente DAF
                WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                    WorkflowEmailHelper.ExtraireEmailsDAF(Application),
                    $"[AdiPAIE] STC validé RH — {d.Salarie?.FullName}",
                    WorkflowEmailHelper.HtmlTableau("Solde de tout compte — Validation DAF requise",
                        "Le RH a validé le STC. Merci de procéder à la validation financière.",
                        new[] {
                            ("Salarié", d.Salarie?.FullName ?? "—"),
                            ("Total STC", $"{d.TotalSoldeToutCompte:N0} FCFA"),
                            ("Validé RH par", d.ValideRHPar ?? "—"),
                        }));

                Application.ShowViewStrategy?.ShowMessage(
                    "Validé par le RH.", InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Valider DAF ───────────────────────────────────────────
            validerDAFAction = new SimpleAction(this,
                "Offboarding_ValiderDAF", PredefinedCategory.Edit)
            {
                Caption = "Valider (DAF)",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Confirmer la validation financière du solde ?"
            };
            validerDAFAction.Execute += (s, e) =>
            {
                var d = (DossierOffboarding)View.CurrentObject;
                d.ValideDAFPar = SecuritySystem.CurrentUserName;
                d.DateValidationDAF = DateTime.Now;
                d.Statut = OffboardingStatut.ValidéDAF;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();
                AuditService.Enregistrer(Application, "DossierOffboarding", "Valider DAF",
                    d.Oid.ToString(), d.DisplayName,
                    ancienStatut: "Validé RH", nouveauStatut: "Validé DAF");

                // Email → RH : DAF a validé, prêt à clôturer
                WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                    WorkflowEmailHelper.ExtraireEmailsRH(Application),
                    $"[AdiPAIE] STC validé DAF — {d.Salarie?.FullName}",
                    WorkflowEmailHelper.HtmlTableau("Solde de tout compte — Validation DAF effectuée",
                        "Le DAF a validé le STC. Le dossier peut être clôturé.",
                        new[] {
                            ("Salarié", d.Salarie?.FullName ?? "—"),
                            ("Total STC", $"{d.TotalSoldeToutCompte:N0} FCFA"),
                            ("Validé DAF par", d.ValideDAFPar ?? "—"),
                        }));

                Application.ShowViewStrategy?.ShowMessage(
                    "Validé par le DAF.", InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Clôturer ──────────────────────────────────────────────
            cloturerAction = new SimpleAction(this,
                "Offboarding_Cloturer", PredefinedCategory.Edit)
            {
                Caption = "Clôturer",
                ImageName = "Action_Close",
                ConfirmationMessage = "Clôturer définitivement ce dossier ?\n\n"
                                    + "Le salarié sera désactivé et sa date de sortie enregistrée."
            };
            cloturerAction.Execute += (s, e) =>
            {
                var d = (DossierOffboarding)View.CurrentObject;
                if (d.Salarie == null) return;

                // Désactiver le salarié
                d.Salarie.IsActif = false;
                d.Salarie.DateSortie = d.DateSortie;

                d.Statut = OffboardingStatut.Cloture;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                AuditService.Enregistrer(Application, "DossierOffboarding", "Clôturer",
                    d.Oid.ToString(), d.DisplayName,
                    $"Salarié {d.Salarie.FullName} désactivé. Date de sortie : {d.DateSortie:dd/MM/yyyy}",
                    nouveauStatut: "Clôturé");

                // Email → Salarié : dossier clôturé
                var emailSalarie = d.Salarie?.Email?.Trim();
                if (!string.IsNullOrWhiteSpace(emailSalarie))
                {
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                        new[] { emailSalarie },
                        $"[AdiPAIE] Votre dossier de sortie est clôturé",
                        WorkflowEmailHelper.HtmlTableau("Dossier de sortie clôturé",
                            "Votre dossier de départ a été finalisé. Veuillez vous rapprocher du service RH pour les modalités de règlement.",
                            new[] {
                                ("Nom", d.Salarie.FullName ?? "—"),
                                ("Date de sortie", $"{d.DateSortie:dd/MM/yyyy}"),
                                ("Total STC", $"{d.TotalSoldeToutCompte:N0} FCFA"),
                            }));
                }

                // Email → Comptable : salarié sorti, STC à régler
                WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                    WorkflowEmailHelper.ExtraireEmailsComptable(Application),
                    $"[AdiPAIE] STC à régler — {d.Salarie.FullName}",
                    WorkflowEmailHelper.HtmlTableau("Solde de tout compte — À régler",
                        "Le dossier de sortie est clôturé. Merci de procéder au règlement.",
                        new[] {
                            ("Salarié", d.Salarie.FullName ?? "—"),
                            ("Date de sortie", $"{d.DateSortie:dd/MM/yyyy}"),
                            ("Total STC", $"{d.TotalSoldeToutCompte:N0} FCFA"),
                        }));

                Application.ShowViewStrategy?.ShowMessage(
                    $"Dossier clôturé. {d.Salarie.FullName} désactivé(e).",
                    InformationType.Success, 5000, InformationPosition.Top);
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
            var d = View?.CurrentObject as DossierOffboarding;
            if (d == null) return;
            var s = d.Statut;

            preRemplirAction.Active["s"] = s == OffboardingStatut.Initie
                                         || s == OffboardingStatut.EnCours;
            calculerAction.Active["s"] = s == OffboardingStatut.Initie
                                         || s == OffboardingStatut.EnCours;
            validerRHAction.Active["s"] = s == OffboardingStatut.SoldeCalcule;
            validerDAFAction.Active["s"] = s == OffboardingStatut.ValideRH;
            cloturerAction.Active["s"] = s == OffboardingStatut.ValidéDAF;
        }
    }
}
