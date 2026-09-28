// =============================================================================
//  InterimaireSortirController.cs - V1.10 - Sortie intérimaire en 1 clic
//
//  Ajoute l'action "Sortir intérimaire" sur DetailView + ListView Interimaire.
//  Popup avec Date de sortie + Motif + Observations.
//  Fait en 1 clic :
//    1. Interimaire.Statut = Inactif (ou Blackliste si Motif=Blackliste)
//    2. Interimaire.DateSortieAgence + MotifSortie remplis
//    3. Tous les ContratInterim EnCours -> Termine + DateFinReelle = date sortie
//    4. MouvementInterimaire créé (TypeMouvement selon Motif)
//
//  Idempotent : si l'intérimaire est déjà Inactif ou Blackliste,
//  l'action est masquée via TargetObjectsCriteria.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.NonPersistent;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    public class InterimaireSortirController : ObjectViewController<ObjectView, Interimaire>
    {
        private readonly PopupWindowShowAction _action;

        public InterimaireSortirController()
        {
            _action = new PopupWindowShowAction(this,
                "Interimaire_Sortir", PredefinedCategory.RecordEdit)
            {
                Caption = "Sortir intérimaire",
                ImageName = "Action_Cancel",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Marque l'intérimaire comme sorti : ferme les contrats en cours, " +
                          "crée un mouvement de fin de mission, renseigne date+motif de sortie.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                TargetObjectsCriteria = "Statut <> 'Inactif' AND Statut <> 'Blackliste'"
            };
            _action.CustomizePopupWindowParams += CustomizePopupWindowParams;
            _action.Execute += Execute;
        }

        private void CustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var interim = View.CurrentObject as Interimaire;
            if (interim == null) return;

            var os = Application.CreateObjectSpace(typeof(SortirInterimaireRequest));
            var dto = os.CreateObject<SortirInterimaireRequest>();
            dto.InterimaireDisplayName = interim.ToString();
            dto.NbContratsEnCours = interim.Contrats.Count(
                c => c.Statut == ContratInterimStatut.EnCours);
            dto.DateSortie = DateTime.Today;
            dto.Motif = MotifSortieInterim.FinMission;

            var view = Application.CreateDetailView(os, dto);
            view.Caption = $"Sortir intérimaire - {interim.FullName}";
            e.View = view;
            e.DialogController.SaveOnAccept = false;
            e.DialogController.AcceptAction.Caption = "Confirmer la sortie";
            e.DialogController.CancelAction.Caption = "Annuler";
        }

        private void Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var interim = View.CurrentObject as Interimaire;
            if (interim == null)
                throw new UserFriendlyException("Aucun intérimaire sélectionné.");

            var dto = e.PopupWindowView.CurrentObject as SortirInterimaireRequest;
            if (dto == null)
                throw new UserFriendlyException("Formulaire invalide.");
            if (!dto.Motif.HasValue)
                throw new UserFriendlyException("Le motif de sortie est obligatoire.");

            // Recharger l'intérimaire dans un OS frais pour le save propre
            using var osFix = Application.CreateObjectSpace(typeof(Interimaire));
            var interimFix = osFix.GetObjectByKey<Interimaire>(interim.Oid);
            if (interimFix == null)
                throw new UserFriendlyException("Intérimaire introuvable.");

            // 1. Statut Interimaire
            interimFix.Statut = (dto.Motif == MotifSortieInterim.Blackliste)
                ? InterimaireStatut.Blackliste
                : InterimaireStatut.Inactif;
            interimFix.DateSortieAgence = dto.DateSortie;
            interimFix.MotifSortie = dto.Motif;
            if (!string.IsNullOrWhiteSpace(dto.Observations))
            {
                interimFix.Observations = string.IsNullOrWhiteSpace(interimFix.Observations)
                    ? $"[Sortie {dto.DateSortie:dd/MM/yyyy}] {dto.Observations}"
                    : interimFix.Observations + $"\n[Sortie {dto.DateSortie:dd/MM/yyyy}] {dto.Observations}";
            }

            // 2. Terminer tous les contrats EnCours
            int nbContratsTermines = 0;
            foreach (var c in interimFix.Contrats.Where(x => x.Statut == ContratInterimStatut.EnCours).ToList())
            {
                c.Statut = ContratInterimStatut.Termine;
                if (!c.DateFinReelle.HasValue)
                    c.DateFinReelle = dto.DateSortie;
                nbContratsTermines++;
            }

            // 3. Créer un MouvementInterimaire
            var mvt = osFix.CreateObject<MouvementInterimaire>();
            mvt.Interimaire = interimFix;
            mvt.DateMouvement = dto.DateSortie;
            mvt.TypeMouvement = MapMotifVersType(dto.Motif.Value);
            mvt.Motif = string.IsNullOrWhiteSpace(dto.Observations)
                ? $"Sortie automatique - {dto.Motif}"
                : $"Sortie automatique - {dto.Motif} - {dto.Observations}";
            mvt.ValideRH = true;
            mvt.DateValidation = DateTime.Now;
            try { mvt.ValideParNom = SecuritySystem.CurrentUserName; } catch { }

            osFix.CommitChanges();
            View?.ObjectSpace?.Refresh();

            Application.ShowViewStrategy?.ShowMessage(
                $"Intérimaire {interimFix.FullName} sorti le {dto.DateSortie:dd/MM/yyyy}. " +
                $"{nbContratsTermines} contrat(s) terminé(s) + 1 mouvement créé.",
                InformationType.Success, 6000, InformationPosition.Top);
        }

        private static MouvementInterimaireType MapMotifVersType(MotifSortieInterim motif)
        {
            return motif switch
            {
                MotifSortieInterim.FinMission => MouvementInterimaireType.FinMission,
                MotifSortieInterim.Demission => MouvementInterimaireType.Demission,
                MotifSortieInterim.RuptureContrat => MouvementInterimaireType.RuptureContrat,
                MotifSortieInterim.FinCDD => MouvementInterimaireType.FinMission,
                MotifSortieInterim.Blackliste => MouvementInterimaireType.RuptureContrat,
                _ => MouvementInterimaireType.FinMission
            };
        }
    }
}
