using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
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
                Caption = "Charger données salarié",
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
                Caption = "Calculer le solde",
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
                Application.ShowViewStrategy?.ShowMessage(
                    "Validé par le DAF.", InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Clôturer ──────────────────────────────────────────────
            cloturerAction = new SimpleAction(this,
                "Offboarding_Cloturer", PredefinedCategory.Edit)
            {
                Caption = "Clôturer le dossier",
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
