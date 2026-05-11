// =============================================================================
//  GratificationController.cs — V1.7.2d
//
//  Actions XAF du workflow Gratification :
//    1. "Calculer (preview)" — disponible en BrouillonRH (RH)
//    2. "Soumettre au DAF"   — BrouillonRH → EnAttenteValidationDAF (RH)
//    3. "Valider (DAF)"      — EnAttenteValidationDAF → ValideeDAF (DAF)
//    4. "Rejeter (DAF)"      — EnAttenteValidationDAF → BrouillonRH (DAF)
//    5. "Intégrer au bulletin" — ValideeDAF → IntegreeBulletin (RH)
//    6. "Annuler" — disponible dans tous les états avant intégration
//
//  La visibilité de chaque action est conditionnée au Statut courant.
//  Les permissions Rôle RH/DAF sont à gérer via RBAC (en V1.7.2 on
//  fait confiance à l'utilisateur, l'éventail UI guide le bon flux).
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class GratificationController
        : ObjectViewController<ObjectView, Gratification>
    {
        readonly SimpleAction calculerAction;
        readonly SimpleAction soumettreAction;
        readonly SimpleAction validerAction;
        readonly SimpleAction rejeterAction;
        readonly SimpleAction integrerAction;
        readonly SimpleAction annulerAction;

        public GratificationController()
        {
            // ──── 1. Calculer (preview montant en brouillon) ────
            calculerAction = new SimpleAction(this,
                "Gratif_Calculer", PredefinedCategory.RecordEdit)
            {
                Caption = "Calculer (preview)",
                ImageName = "Action_RefreshFile",
                ToolTip = "Recalcule le montant à partir de la base et du " +
                          "multiplicateur, sans changer de statut.",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,BrouillonRH#",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            calculerAction.Execute += CalculerAction_Execute;

            // ──── 2. Soumettre au DAF (RH) ────
            soumettreAction = new SimpleAction(this,
                "Gratif_Soumettre", PredefinedCategory.RecordEdit)
            {
                Caption = "Soumettre au DAF",
                ImageName = "Action_Send",
                ToolTip = "Soumet la gratification au DAF pour validation.",
                ConfirmationMessage =
                    "Cette action va figer le calcul et soumettre la " +
                    "gratification au DAF pour validation. Continuer ?",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,BrouillonRH#",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            soumettreAction.Execute += SoumettreAction_Execute;

            // ──── 3. Valider (DAF) ────
            validerAction = new SimpleAction(this,
                "Gratif_Valider", PredefinedCategory.RecordEdit)
            {
                Caption = "Valider (DAF)",
                ImageName = "State_Validation_Validated",
                ToolTip = "Le DAF valide les montants et autorise l'intégration au bulletin.",
                ConfirmationMessage = "Valider cette gratification ?",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,EnAttenteValidationDAF#",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            validerAction.Execute += ValiderAction_Execute;

            // ──── 4. Rejeter (DAF) ────
            rejeterAction = new SimpleAction(this,
                "Gratif_Rejeter", PredefinedCategory.RecordEdit)
            {
                Caption = "Rejeter (DAF)",
                ImageName = "State_Validation_Rejected",
                ToolTip = "Le DAF rejette la gratification. Elle repasse en Brouillon " +
                          "pour que RH puisse la corriger ou l'annuler.",
                ConfirmationMessage = "Rejeter cette gratification ? " +
                                      "Elle repassera en Brouillon RH.",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,EnAttenteValidationDAF#",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            rejeterAction.Execute += RejeterAction_Execute;

            // ──── 5. Intégrer au bulletin (RH) ────
            integrerAction = new SimpleAction(this,
                "Gratif_Integrer", PredefinedCategory.RecordEdit)
            {
                Caption = "Intégrer au bulletin",
                ImageName = "Action_Document_New",
                ToolTip = "Crée une ligne GRATIF sur le bulletin du mois de paiement.",
                ConfirmationMessage = "Intégrer cette gratification au bulletin ? " +
                                      "Une ligne GRATIF sera ajoutée au bulletin du mois.",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,ValideeDAF#",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            integrerAction.Execute += IntegrerAction_Execute;

            // ──── 6. Annuler (à tout moment avant intégration) ────
            annulerAction = new SimpleAction(this,
                "Gratif_Annuler", PredefinedCategory.RecordEdit)
            {
                Caption = "Annuler",
                ImageName = "Action_Cancel",
                ToolTip = "Annule la gratification. Action irréversible.",
                ConfirmationMessage = "Annuler définitivement cette gratification ?",
                TargetObjectsCriteria =
                    "Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,IntegreeBulletin# " +
                    "AND Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,Payee# " +
                    "AND Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+GratificationStatut,Annule#",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            annulerAction.Execute += AnnulerAction_Execute;
        }

        // ═════════════════════════════════════════════════════════════
        // HANDLERS
        // ═════════════════════════════════════════════════════════════

        void CalculerAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var g = e.CurrentObject as Gratification;
                if (g == null) return;
                GratificationService.RecalculerMontant(g);
                ObjectSpace.CommitChanges();
                Application.ShowViewStrategy?.ShowMessage(
                    $"Montant recalculé : {g.MontantCalcule:N0} FCFA " +
                    $"(base = {g.BaseReference:N0} FCFA × {g.Multiplicateur:N2}).",
                    InformationType.Success, 4000, InformationPosition.Top);
                View.Refresh();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        void SoumettreAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var g = e.CurrentObject as Gratification;
                if (g == null) return;
                GratificationService.Soumettre(ObjectSpace, g);
                Application.ShowViewStrategy?.ShowMessage(
                    $"Gratification soumise au DAF. Montant : " +
                    $"{g.MontantCalcule:N0} FCFA. " +
                    $"En attente de validation.",
                    InformationType.Success, 4000, InformationPosition.Top);
                View.Refresh();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        void ValiderAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var g = e.CurrentObject as Gratification;
                if (g == null) return;
                GratificationService.Valider(ObjectSpace, g);
                Application.ShowViewStrategy?.ShowMessage(
                    $"Gratification validée. RH peut maintenant l'intégrer " +
                    $"au bulletin {g.Annee}/{g.MoisPaiement:00}.",
                    InformationType.Success, 4000, InformationPosition.Top);
                View.Refresh();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        void RejeterAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var g = e.CurrentObject as Gratification;
                if (g == null) return;
                // Motif minimal — pour V1.7.2 on ne demande pas de saisie
                // utilisateur (sinon il faudrait un popup). Le DAF peut
                // précisera dans Commentaire après rejet.
                GratificationService.Rejeter(ObjectSpace, g, "Rejeté par DAF");
                Application.ShowViewStrategy?.ShowMessage(
                    "Gratification rejetée. Repassée en Brouillon RH. " +
                    "Le motif peut être détaillé dans Commentaire.",
                    InformationType.Warning, 5000, InformationPosition.Top);
                View.Refresh();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        void IntegrerAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var g = e.CurrentObject as Gratification;
                if (g == null) return;
                GratificationService.IntegrerAuBulletin(ObjectSpace, g);
                Application.ShowViewStrategy?.ShowMessage(
                    $"✅ Ligne GRATIF de {g.MontantCalcule:N0} FCFA ajoutée " +
                    $"au bulletin {g.Annee}/{g.MoisPaiement:00} de " +
                    $"{g.Salarie.Matricule}.",
                    InformationType.Success, 5000, InformationPosition.Top);
                View.Refresh();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        void AnnulerAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var g = e.CurrentObject as Gratification;
                if (g == null) return;
                GratificationService.Annuler(ObjectSpace, g, "Annulé via UI");
                Application.ShowViewStrategy?.ShowMessage(
                    "Gratification annulée.",
                    InformationType.Info, 4000, InformationPosition.Top);
                View.Refresh();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        void ShowError(Exception ex)
        {
            Application.ShowViewStrategy?.ShowMessage(
                $"❌ {ex.Message}",
                InformationType.Error, 8000, InformationPosition.Top);
        }
    }
}
