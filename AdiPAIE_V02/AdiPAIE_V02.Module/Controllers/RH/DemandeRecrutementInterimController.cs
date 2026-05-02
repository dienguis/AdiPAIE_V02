// ════════════════════════════════════════════════════════════════════════
// FICHIER 1 : DemandeRecrutementInterimController.cs
// Dossier    : AdiPAIE_V02.Module/Controllers/RH/
// ════════════════════════════════════════════════════════════════════════

using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Controllers.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Workflow des demandes de recrutement intérimaire.
    ///
    /// Flux : AC soumet → N+1 valide/rejette → RH valide/rejette
    ///        → RH transmet au RFE → RFE affecte un intérimaire
    ///
    /// Exigence ELTON : aucun intérimaire dans les effectifs sans validation RH.
    /// </summary>
    public class DemandeRecrutementInterimController
        : ObjectViewController<DetailView, DemandeRecrutementInterim>
    {
        readonly SimpleAction soumettreAction;
        readonly SimpleAction validerN1Action;
        readonly SimpleAction rejeterN1Action;
        readonly SimpleAction validerRHAction;
        readonly SimpleAction rejeterRHAction;
        readonly SimpleAction transmettreRFEAction;
        readonly SimpleAction affecterAction;
        readonly SimpleAction annulerAction;

        public DemandeRecrutementInterimController()
        {
            // ── AC : Soumettre ────────────────────────────────────────
            soumettreAction = new SimpleAction(this,
                "DRI_Soumettre", PredefinedCategory.Edit)
            {
                Caption = "Soumettre",
                ImageName = "Action_Forward",
                ConfirmationMessage = "Soumettre cette demande au N+1 ?"
            };
            soumettreAction.Execute += (s, e) =>
            {
                var d = (DemandeRecrutementInterim)View.CurrentObject;
                if (d.Site == null && d.BU == null)
                    throw new UserFriendlyException("Renseignez le site ou la BU.");
                if (d.Poste == null)
                    throw new UserFriendlyException("Le poste souhaité est obligatoire.");
                if (string.IsNullOrWhiteSpace(d.MotifRecours))
                    throw new UserFriendlyException("Le motif du recours est obligatoire.");

                d.SoumettreN1();
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                // Email N+1
                if (d.ValideurN1 != null && !string.IsNullOrWhiteSpace(d.ValideurN1.Email))
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                        new[] { d.ValideurN1.Email },
                        $"[AdiPAIE] Demande recrutement intérimaire à valider — {d.Reference}",
                        WorkflowEmailHelper.HtmlTableau(
                            "Demande de recrutement intérimaire",
                            $"Une demande de recrutement intérimaire attend votre validation.",
                            Lignes(d)));

                Application.ShowViewStrategy?.ShowMessage(
                    "Demande soumise au N+1.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── N+1 : Valider ─────────────────────────────────────────
            validerN1Action = new SimpleAction(this,
                "DRI_ValiderN1", PredefinedCategory.Edit)
            {
                Caption = "Valider",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Valider cette demande et la transmettre au RH ?"
            };
            validerN1Action.Execute += (s, e) =>
            {
                var d = (DemandeRecrutementInterim)View.CurrentObject;
                d.ValiderN1();
                var rhEmails = WorkflowEmailHelper.ExtraireEmailsRH(Application);
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                WorkflowEmailHelper.EnvoyerEmailsAsync(Application, rhEmails,
                    $"[AdiPAIE] Demande recrutement intérimaire — validation RH requise — {d.Reference}",
                    WorkflowEmailHelper.HtmlTableau(
                        "Demande validée par N+1 — validation RH requise",
                        $"La demande {d.Reference} a été validée par le N+1. Elle attend votre approbation.",
                        Lignes(d)));

                Application.ShowViewStrategy?.ShowMessage(
                    "Demande validée. Transmise au RH.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── N+1 : Rejeter ─────────────────────────────────────────
            rejeterN1Action = new SimpleAction(this,
                "DRI_RejeterN1", PredefinedCategory.Edit)
            {
                Caption = "Rejeter",
                ImageName = "Action_Cancel",
                ConfirmationMessage = "Rejeter cette demande ?"
            };
            rejeterN1Action.Execute += (s, e) =>
            {
                var d = (DemandeRecrutementInterim)View.CurrentObject;
                if (string.IsNullOrWhiteSpace(d.MotifRefus))
                    throw new UserFriendlyException("Saisissez un motif de refus.");
                d.RejeterN1(d.MotifRefus);
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                Application.ShowViewStrategy?.ShowMessage(
                    "Demande rejetée.", InformationType.Warning, 3000, InformationPosition.Top);
            };

            // ── RH : Valider ──────────────────────────────────────────
            validerRHAction = new SimpleAction(this,
                "DRI_ValiderRH", PredefinedCategory.Edit)
            {
                Caption = "Approuver",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Approuver cette demande ?"
            };
            validerRHAction.Execute += (s, e) =>
            {
                var d = (DemandeRecrutementInterim)View.CurrentObject;
                d.ValiderRH();
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                Application.ShowViewStrategy?.ShowMessage(
                    "Demande approuvée par le RH.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── RH : Rejeter ──────────────────────────────────────────
            rejeterRHAction = new SimpleAction(this,
                "DRI_RejeterRH", PredefinedCategory.Edit)
            {
                Caption = "Refuser",
                ImageName = "Action_Cancel",
                ConfirmationMessage = "Refuser cette demande ?"
            };
            rejeterRHAction.Execute += (s, e) =>
            {
                var d = (DemandeRecrutementInterim)View.CurrentObject;
                if (string.IsNullOrWhiteSpace(d.MotifRefus))
                    throw new UserFriendlyException("Saisissez un motif de refus.");
                d.RejeterRH(d.MotifRefus);
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                Application.ShowViewStrategy?.ShowMessage(
                    "Demande refusée.", InformationType.Warning, 3000, InformationPosition.Top);
            };

            // ── RH : Transmettre au RFE ───────────────────────────────
            transmettreRFEAction = new SimpleAction(this,
                "DRI_TransmettreRFE", PredefinedCategory.Edit)
            {
                Caption = "Transmettre RFE",
                ImageName = "Action_Forward",
                ConfirmationMessage = "Transmettre cette demande au Responsable Formation / Emploi ?"
            };
            transmettreRFEAction.Execute += (s, e) =>
            {
                var d = (DemandeRecrutementInterim)View.CurrentObject;
                d.TransmettreRFE();
                var rhEmails = WorkflowEmailHelper.ExtraireEmailsRH(Application);
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                Application.ShowViewStrategy?.ShowMessage(
                    "Demande transmise au RFE.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── RFE : Affecter un intérimaire ─────────────────────────
            affecterAction = new SimpleAction(this,
                "DRI_Affecter", PredefinedCategory.Edit)
            {
                Caption = "Affecter intérim.",
                ImageName = "BO_Person",
                ConfirmationMessage = "Confirmer l'affectation de l'intérimaire sélectionné ?"
            };
            affecterAction.Execute += (s, e) =>
            {
                var d = (DemandeRecrutementInterim)View.CurrentObject;
                if (d.InterimaireAffecte == null)
                    throw new UserFriendlyException(
                        "Sélectionnez d'abord un intérimaire dans le champ 'Intérimaire affecté'.");

                // Créer automatiquement le contrat si pas encore fait
                if (d.ContratCree == null)
                {
                    var contrat = ObjectSpace.CreateObject<ContratInterim>();
                    contrat.Interimaire = d.InterimaireAffecte;
                    contrat.Station = d.Site;
                    contrat.BU = d.BU;
                    contrat.PosteOccupe = d.Poste;
                    contrat.MotifRecours = d.MotifRecours;
                    contrat.DateDebut = d.DateDebut;
                    contrat.DateFin = d.DateFin;
                    contrat.TypeContrat = ContratInterimType.PremiereMission;
                    contrat.Statut = ContratInterimStatut.EnCours;
                    d.ContratCree = contrat;
                }

                // Mettre à jour le statut de l'intérimaire
                d.InterimaireAffecte.Statut = InterimaireStatut.EnMission;

                // Créer un mouvement d'affectation
                var mvt = ObjectSpace.CreateObject<MouvementInterimaire>();
                mvt.Interimaire = d.InterimaireAffecte;
                mvt.TypeMouvement = MouvementInterimaireType.Affectation;
                mvt.DateMouvement = DateTime.Today;
                mvt.StationDestination = d.Site;
                mvt.Motif = $"Affectation suite demande {d.Reference}";
                mvt.ValideRH = true;
                mvt.ValideParNom = d.ValideRHPar;
                mvt.DateValidation = d.DateValidationRH;

                d.AffecterInterimaire(d.InterimaireAffecte, d.ContratCree);
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                Application.ShowViewStrategy?.ShowMessage(
                    $"Intérimaire {d.InterimaireAffecte.FullName} affecté. Contrat créé.",
                    InformationType.Success, 4000, InformationPosition.Top);
            };

            // ── Annuler ───────────────────────────────────────────────
            annulerAction = new SimpleAction(this,
                "DRI_Annuler", PredefinedCategory.Edit)
            {
                Caption = "Annuler",
                ImageName = "Action_Cancel",
                ConfirmationMessage = "Annuler définitivement cette demande ?"
            };
            annulerAction.Execute += (s, e) =>
            {
                var d = (DemandeRecrutementInterim)View.CurrentObject;
                d.Statut = DemandeInterimaireStatut.Annulee;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Demande annulée.", InformationType.Warning, 3000, InformationPosition.Top);
            };
        }

        // ── Cycle de vie ──────────────────────────────────────────────
        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateStates();
            View.CurrentObjectChanged += (_, __) => UpdateStates();
        }

        void UpdateStates()
        {
            var d = View?.CurrentObject as DemandeRecrutementInterim;
            if (d == null) return;
            var s = d.Statut;

            soumettreAction.Active["s"] = s == DemandeInterimaireStatut.Brouillon;
            validerN1Action.Active["s"] = s == DemandeInterimaireStatut.EnAttenteN1;
            rejeterN1Action.Active["s"] = s == DemandeInterimaireStatut.EnAttenteN1;
            validerRHAction.Active["s"] = s == DemandeInterimaireStatut.EnAttenteRH;
            rejeterRHAction.Active["s"] = s == DemandeInterimaireStatut.EnAttenteRH;
            transmettreRFEAction.Active["s"] = s == DemandeInterimaireStatut.Acceptee;
            affecterAction.Active["s"] = s == DemandeInterimaireStatut.EnAttenteRFE
                                            || s == DemandeInterimaireStatut.EnCoursAttribution;
            annulerAction.Active["s"] = s != DemandeInterimaireStatut.InterimaireAffecte
                                            && s != DemandeInterimaireStatut.Annulee
                                            && s != DemandeInterimaireStatut.Refusee;
        }

        // ── Helpers ───────────────────────────────────────────────────
        static (string, string)[] Lignes(DemandeRecrutementInterim d) => new[]
        {
            ("Référence",    d.Reference ?? "—"),
            ("Site / BU",    d.Site?.Nom ?? d.BU?.Libelle ?? "—"),
            ("Poste",        d.Poste?.Libelle ?? "—"),
            ("Motif",        d.MotifRecours ?? "—"),
            ("Début",        d.DateDebut.ToString("dd/MM/yyyy")),
            ("Fin",          d.DateFin.ToString("dd/MM/yyyy")),
            ("Nb postes",    d.NombrePostes.ToString()),
            ("Statut",       d.Statut.ToString()),
        };
    }
}
