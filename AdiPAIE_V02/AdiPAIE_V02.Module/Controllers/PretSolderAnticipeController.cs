using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.NonPersistent;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// V1.8.5 - Action "Solder anticipé" sur un prêt en cours.
    ///
    /// Cas d'usage : le salarié souhaite rembourser en une fois le capital
    /// restant sur un mois donné (typiquement le mois courant), au lieu
    /// d'attendre la fin des mensualités programmées.
    ///
    /// Comportement : crée une PretEcheance exceptionnelle avec
    /// MontantCapital = ResteARegler et DateEcheance = 1er du mois choisi.
    /// Le calcul du bulletin de ce mois ramassera automatiquement cette
    /// échéance (cf. Bulletin.CalculerRetenuePrets) et le prêt passera à
    /// Termine dès que le prélèvement sera validé (cf. Pret.RecalculerEtat).
    ///
    /// Idempotence : garde-fou pour ne pas créer deux échéances de solde
    /// anticipé sur le même mois pour le même prêt.
    /// </summary>
    public class PretSolderAnticipeController : ViewController<DetailView>
    {
        private readonly PopupWindowShowAction _action;

        public PretSolderAnticipeController()
        {
            TargetObjectType = typeof(Pret);

            _action = new PopupWindowShowAction(this,
                "Pret_SolderAnticipe",
                DevExpress.Persistent.Base.PredefinedCategory.RecordEdit)
            {
                Caption = "Solder anticipé",
                ToolTip = "Rembourser en une fois le capital restant sur un bulletin donné.",
                ImageName = "Action_Debug_Start",
                ConfirmationMessage = null,
                TargetObjectsCriteria = "Statut = 'EnCours' AND ResteARegler > 0"
            };
            _action.CustomizePopupWindowParams += OnCustomizePopupWindowParams;
            _action.Execute += OnExecute;
        }

        private void OnCustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var pret = View.CurrentObject as Pret;
            if (pret == null) return;

            var os = Application.CreateObjectSpace(typeof(SoldeAnticipePretRequest));
            var dto = os.CreateObject<SoldeAnticipePretRequest>();
            dto.PretDisplayName = pret.ToString();
            dto.SalarieDisplayName = pret.Salarie?.ToString() ?? "?";
            dto.ResteARegler = pret.ResteARegler;
            dto.AnneeImputation = DateTime.Today.Year;
            dto.MoisImputation = DateTime.Today.Month;
            dto.Reference = "Solde anticipé";

            var detailView = Application.CreateDetailView(os, dto);
            detailView.Caption = "Solder anticipé un prêt";
            detailView.ViewEditMode = DevExpress.ExpressApp.Editors.ViewEditMode.Edit;
            e.View = detailView;
            e.DialogController.SaveOnAccept = false;
            e.DialogController.AcceptAction.Caption = "Confirmer le solde anticipé";
            e.DialogController.CancelAction.Caption = "Annuler";
        }

        private void OnExecute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var pret = View.CurrentObject as Pret;
            if (pret == null)
                throw new UserFriendlyException("Aucun prêt sélectionné.");

            var dto = e.PopupWindowView.CurrentObject as SoldeAnticipePretRequest;
            if (dto == null)
                throw new UserFriendlyException("Formulaire de solde anticipé invalide.");

            // -- Validations ------------------------------------------------
            if (pret.Statut != PretStatut.EnCours)
                throw new UserFriendlyException(
                    $"Le prêt doit être en statut « En cours » (actuel : {pret.Statut}).");

            if (pret.ResteARegler <= 0m)
                throw new UserFriendlyException("Ce prêt n'a plus rien à régler.");

            if (dto.AnneeImputation < 2000 || dto.AnneeImputation > 2100)
                throw new UserFriendlyException("Année d'imputation invalide.");

            if (dto.MoisImputation < 1 || dto.MoisImputation > 12)
                throw new UserFriendlyException("Mois d'imputation invalide (1-12).");

            var debutMois = new DateTime(dto.AnneeImputation, dto.MoisImputation, 1);
            var finMois = debutMois.AddMonths(1).AddDays(-1);

            // -- Garde-fou idempotence -------------------------------------
            // Refuse de créer une 2e échéance "Solde anticipé" sur le même
            // mois pour le même prêt (permet de relancer l'action sans casse).
            var os = ObjectSpace as DevExpress.ExpressApp.Xpo.XPObjectSpace;
            var session = os?.Session;
            if (session == null)
                throw new UserFriendlyException("Session non disponible.");

            var pretSession = session.GetObjectByKey<Pret>(pret.Oid);
            var dejaExiste = new XPQuery<PretEcheance>(session)
                .Any(ec => ec.Pret != null
                        && ec.Pret.Oid == pretSession.Oid
                        && ec.DateEcheance >= debutMois
                        && ec.DateEcheance <= finMois
                        && ec.Reference != null
                        && ec.Reference.Contains("Solde anticipé")
                        && ec.Statut == PretEcheanceStatut.Prevue);
            if (dejaExiste)
                throw new UserFriendlyException(
                    $"Une échéance « Solde anticipé » existe déjà sur {dto.MoisImputation:D2}/{dto.AnneeImputation} pour ce prêt.");

            // -- V1.8.5 (fix) - Capturer le reste à régler AVANT toute modification.
            // Bug identifié : si on ne le capture pas, l'annulation des échéances
            // Prevue ci-dessous déclenche Pret.RecalculerEtat() qui va recalculer
            // ResteARegler = 0 (puisqu'il n'y a plus d'échéances actives), et
            // notre échéance de solde serait créée avec MontantCapital = 0.
            var montantSolde = pretSession.ResteARegler;

            // -- V1.8.5 (fix) - Annuler toutes les échéances Prevue restantes.
            // Bug initial : le solde anticipé s'ajoutait aux mensualités déjà
            // programmées -> prélèvement en double sur le bulletin.
            // Ex: reste 600 000, mensualité 500 000 prévue en janvier
            //     -> sans annulation, bulletin janvier prélevait 500 000 + 600 000 = 1 100 000
            //     -> avec annulation, bulletin janvier prélève uniquement 600 000
            var echeancesPrevues = new XPQuery<PretEcheance>(session)
                .Where(ec => ec.Pret != null
                          && ec.Pret.Oid == pretSession.Oid
                          && ec.Statut == PretEcheanceStatut.Prevue)
                .ToList();

            int nbAnnulees = 0;
            foreach (var ecAnnul in echeancesPrevues)
            {
                ecAnnul.Statut = PretEcheanceStatut.Annulee;
                ecAnnul.Reference = string.IsNullOrWhiteSpace(ecAnnul.Reference)
                    ? "Annulée suite solde anticipé"
                    : ecAnnul.Reference + " (annulée suite solde anticipé)";
                nbAnnulees++;
            }

            // -- Création de l'unique échéance de solde ---------------------
            var ech = new PretEcheance(session)
            {
                Pret = pretSession,
                DateEcheance = debutMois,
                MontantCapital = montantSolde,
                MontantInteret = 0m,
                Statut = PretEcheanceStatut.Prevue,
                Reference = string.IsNullOrWhiteSpace(dto.Reference)
                    ? "Solde anticipé"
                    : dto.Reference.Trim()
            };
            ech.Save();
            // Pret.RecalculerEtat() est appelé automatiquement dans PretEcheance.OnSaving
            // Le prêt reste "EnCours" tant que l'échéance est en statut Prevue ; il
            // basculera à Termine dès qu'un bulletin la marquera Prelevee.

            ObjectSpace.CommitChanges();

            e.ShowViewParameters.CreatedView = null;
            View.ObjectSpace.Refresh();

            var msg = $"Solde anticipé créé : {montantSolde:N0} FCFA sur {dto.MoisImputation:D2}/{dto.AnneeImputation}. " +
                      $"{nbAnnulees} échéance(s) prévue(s) annulée(s). " +
                      $"Le solde sera prélevé automatiquement au prochain calcul du bulletin de {debutMois:MMMM yyyy}.";
            Application.ShowViewStrategy?.ShowMessage(
                msg, InformationType.Success, 8000, InformationPosition.Top);
        }
    }
}
