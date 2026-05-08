// =============================================================================
//  DemandeMouvementInterimController.cs — V1.5
//
//  Controller workflow pour DemandeMouvementInterim (DetailView).
//  Pattern repris de DemandeRecrutementInterimController.
//
//  Toutes les actions sont visibles par défaut, mais activées seulement quand
//  la transition est compatible avec le statut courant. Les permissions XAF
//  côté RBAC garantissent que seul le bon rôle peut effectivement cliquer.
// =============================================================================
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    public sealed class DemandeMouvementInterimController
        : ObjectViewController<DetailView, DemandeMouvementInterim>
    {
        private readonly SimpleAction _soumettre;
        private readonly SimpleAction _validerAssistantRH;
        private readonly SimpleAction _rejeterAssistantRH;
        private readonly SimpleAction _validerRH;
        private readonly SimpleAction _rejeterRH;
        private readonly SimpleAction _approuverDAF;
        private readonly SimpleAction _rejeterDAF;
        private readonly SimpleAction _appliquer;
        private readonly SimpleAction _annuler;

        public DemandeMouvementInterimController()
        {
            // ── 1. Soumettre (AC) ────────────────────────────────────
            _soumettre = NewAction("DMI_Soumettre", "Soumettre",
                "Action_Forward", "Soumettre cette demande à l'Assistant RH ?");
            _soumettre.Execute += (s, e) => Run(d =>
            {
                DemandeMouvementService.Soumettre(d, ObjectSpace, SafeUser(), Logger);
                Toast("Demande soumise à l'Assistant RH.", InformationType.Success);
            });

            // ── 2. Valider Assistant RH ──────────────────────────────
            _validerAssistantRH = NewAction("DMI_ValiderAssistantRH", "Valider (Asst RH)",
                "Action_Approve", "Valider cette demande au niveau Assistant RH ?");
            _validerAssistantRH.Execute += (s, e) => Run(d =>
            {
                DemandeMouvementService.ValiderAssistantRH(
                    d, ObjectSpace, SafeUser(), d.AssistantRHCommentaire, Logger);
                Toast("Demande validée par l'Assistant RH.", InformationType.Success);
            });

            // ── 3. Rejeter Assistant RH ──────────────────────────────
            _rejeterAssistantRH = NewAction("DMI_RejeterAssistantRH", "Rejeter (Asst RH)",
                "Action_Cancel", "Rejeter cette demande ?");
            _rejeterAssistantRH.Execute += (s, e) => Run(d =>
            {
                if (string.IsNullOrWhiteSpace(d.AssistantRHCommentaire))
                    throw new UserFriendlyException(
                        "Saisissez un motif dans « Commentaire Assistant RH » avant de rejeter.");
                DemandeMouvementService.RejeterAssistantRH(
                    d, ObjectSpace, SafeUser(), d.AssistantRHCommentaire, Logger);
                Toast("Demande rejetée.", InformationType.Warning);
            });

            // ── 4. Valider RH (avec court-circuit possible) ──────────
            _validerRH = NewAction("DMI_ValiderRH", "Valider RH",
                "Action_Approve",
                "Valider cette demande au niveau RH ?\n"
                + "(Si la demande n'a pas été validée par l'Assistant RH, "
                + "ce sera un court-circuit RH.)");
            _validerRH.Execute += (s, e) => Run(d =>
            {
                bool courtCircuit = d.Statut == DemandeMouvementStatut.SoumiseAssistantRH;
                DemandeMouvementService.ValiderRH(
                    d, ObjectSpace, SafeUser(), d.RHCommentaire, courtCircuit, Logger);
                Toast(
                    courtCircuit
                        ? "Demande validée par RH (court-circuit Assistant RH)."
                        : "Demande validée par RH.",
                    InformationType.Success);
            });

            // ── 5. Rejeter RH ────────────────────────────────────────
            _rejeterRH = NewAction("DMI_RejeterRH", "Rejeter RH",
                "Action_Cancel", "Rejeter cette demande ?");
            _rejeterRH.Execute += (s, e) => Run(d =>
            {
                if (string.IsNullOrWhiteSpace(d.RHCommentaire))
                    throw new UserFriendlyException(
                        "Saisissez un motif dans « Commentaire RH » avant de rejeter.");
                DemandeMouvementService.RejeterRH(
                    d, ObjectSpace, SafeUser(), d.RHCommentaire, Logger);
                Toast("Demande rejetée par RH.", InformationType.Warning);
            });

            // ── 6. Approuver DAF (option) ────────────────────────────
            _approuverDAF = NewAction("DMI_ApprouverDAF", "Approuver DAF",
                "Action_Approve", "Approuver cette demande au niveau DAF ?");
            _approuverDAF.Execute += (s, e) => Run(d =>
            {
                DemandeMouvementService.ApprouverDAF(
                    d, ObjectSpace, SafeUser(), d.DAFCommentaire, Logger);
                Toast("Demande approuvée par DAF.", InformationType.Success);
            });

            // ── 7. Rejeter DAF ───────────────────────────────────────
            _rejeterDAF = NewAction("DMI_RejeterDAF", "Rejeter DAF",
                "Action_Cancel", "Rejeter cette demande ?");
            _rejeterDAF.Execute += (s, e) => Run(d =>
            {
                if (string.IsNullOrWhiteSpace(d.DAFCommentaire))
                    throw new UserFriendlyException(
                        "Saisissez un motif dans « Commentaire DAF » avant de rejeter.");
                DemandeMouvementService.RejeterDAF(
                    d, ObjectSpace, SafeUser(), d.DAFCommentaire, Logger);
                Toast("Demande rejetée par DAF.", InformationType.Warning);
            });

            // ── 8. Appliquer (RH) ────────────────────────────────────
            _appliquer = NewAction("DMI_Appliquer", "Appliquer",
                "Action_Run",
                "Appliquer cette demande ?\n"
                + "Cela créera un mouvement intérimaire validé et mettra à jour le contrat.");
            _appliquer.Execute += async (s, e) =>
            {
                try
                {
                    var d = ViewCurrentObject;
                    if (d == null) return;
                    await DemandeMouvementService.AppliquerAsync(
                        d, ObjectSpace, SafeUser(), Logger);
                    UpdateStates(); View.Refresh();
                    Toast("Mouvement appliqué avec succès.", InformationType.Success);
                }
                catch (UserFriendlyException) { throw; }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Échec application demande mouvement.");
                    Toast($"Erreur : {ex.Message}", InformationType.Error);
                }
            };

            // ── 9. Annuler (initiateur ou RH) ────────────────────────
            _annuler = NewAction("DMI_Annuler", "Annuler",
                "Action_Stop", "Annuler cette demande ?");
            _annuler.Execute += (s, e) => Run(d =>
            {
                DemandeMouvementService.Annuler(d, ObjectSpace, SafeUser(),
                    motif: "Annulée depuis l'UI", Logger);
                Toast("Demande annulée.", InformationType.Info);
            });
        }

        // =====================================================================
        // OnActivated / OnDeactivated
        // =====================================================================
        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateStates();
            if (View != null)
                View.CurrentObjectChanged += View_CurrentObjectChanged;
        }

        protected override void OnDeactivated()
        {
            if (View != null)
                View.CurrentObjectChanged -= View_CurrentObjectChanged;
            base.OnDeactivated();
        }

        private void View_CurrentObjectChanged(object sender, EventArgs e) => UpdateStates();

        // =====================================================================
        // UpdateStates — active/désactive les actions selon le statut courant
        // =====================================================================
        private void UpdateStates()
        {
            var d = ViewCurrentObject;
            if (d == null)
            {
                AllOff();
                return;
            }

            const string Key = "DMI_StatusGate";
            var s = d.Statut;

            _soumettre.Active.SetItemValue(Key, s == DemandeMouvementStatut.Brouillon);

            _validerAssistantRH.Active.SetItemValue(Key,
                s == DemandeMouvementStatut.SoumiseAssistantRH);
            _rejeterAssistantRH.Active.SetItemValue(Key,
                s == DemandeMouvementStatut.SoumiseAssistantRH);

            // RH peut court-circuiter depuis SoumiseAssistantRH
            _validerRH.Active.SetItemValue(Key,
                s == DemandeMouvementStatut.ValideeAssistantRH ||
                s == DemandeMouvementStatut.SoumiseAssistantRH);
            _rejeterRH.Active.SetItemValue(Key,
                s == DemandeMouvementStatut.ValideeAssistantRH ||
                s == DemandeMouvementStatut.SoumiseAssistantRH);

            // DAF actif uniquement si option et statut ValideeRH
            bool dafActif = d.OptionApprobationDAF &&
                            s == DemandeMouvementStatut.ValideeRH;
            _approuverDAF.Active.SetItemValue(Key, dafActif);
            _rejeterDAF.Active.SetItemValue(Key, dafActif);

            // Appliquer : si DAF requis → ValideeDAF, sinon ValideeRH
            bool peutAppliquer = d.OptionApprobationDAF
                ? s == DemandeMouvementStatut.ValideeDAF
                : s == DemandeMouvementStatut.ValideeRH;
            _appliquer.Active.SetItemValue(Key, peutAppliquer);

            // Annuler : à toute étape sauf Appliquee/déjà annulée/déjà rejetée
            bool peutAnnuler =
                s != DemandeMouvementStatut.Appliquee &&
                s != DemandeMouvementStatut.Annulee &&
                s != DemandeMouvementStatut.RejeteeAssistantRH &&
                s != DemandeMouvementStatut.RejeteeRH &&
                s != DemandeMouvementStatut.RejeteeDAF;
            _annuler.Active.SetItemValue(Key, peutAnnuler);
        }

        private void AllOff()
        {
            const string Key = "DMI_StatusGate";
            foreach (var a in new[] { _soumettre, _validerAssistantRH, _rejeterAssistantRH,
                _validerRH, _rejeterRH, _approuverDAF, _rejeterDAF, _appliquer, _annuler })
            {
                a.Active.SetItemValue(Key, false);
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        private SimpleAction NewAction(string id, string caption, string image,
                                       string confirmation)
        {
            return new SimpleAction(this, id, PredefinedCategory.Edit)
            {
                Caption = caption,
                ImageName = image,
                PaintStyle = ActionItemPaintStyle.Caption,
                ConfirmationMessage = confirmation,
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
        }

        private void Run(Action<DemandeMouvementInterim> handler)
        {
            try
            {
                var d = ViewCurrentObject;
                if (d == null) return;
                handler(d);
                UpdateStates();
                View.Refresh();
            }
            catch (UserFriendlyException) { throw; }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Échec transition demande mouvement.");
                Toast($"Erreur : {ex.Message}", InformationType.Error);
            }
        }

        private void Toast(string msg, InformationType info) =>
            Application.ShowViewStrategy?.ShowMessage(msg, info, 3500, InformationPosition.Top);

        private string SafeUser()
        {
            try { return SecuritySystem.CurrentUserName ?? "(système)"; }
            catch { return "(système)"; }
        }

        private ILogger Logger
        {
            get
            {
                try
                {
                    return Application?.ServiceProvider?
                        .GetService<ILoggerFactory>()?
                        .CreateLogger<DemandeMouvementInterimController>();
                }
                catch { return null; }
            }
        }
    }
}
