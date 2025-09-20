using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    // Agit sur le DetailView d’un Bulletin
    public class BulletinCloturePretController : ObjectViewController<DetailView, Bulletin>
    {
        private SimpleAction cloturerAction;
        private SimpleAction reouvrirAction;

        public BulletinCloturePretController()
        {
            // Action Clôturer
            cloturerAction = new SimpleAction(this, "CloturerBulletin", PredefinedCategory.Edit)
            {
                Caption = "Clôturer le bulletin",
                ImageName = "Action_Validation",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            cloturerAction.Execute += CloturerAction_Execute;

            // Action Réouvrir
            reouvrirAction = new SimpleAction(this, "ReouvrirBulletin", PredefinedCategory.Edit)
            {
                Caption = "Réouvrir le bulletin",
                ImageName = "Action_Restore",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            reouvrirAction.Execute += ReouvrirAction_Execute;
        }

        // ===================== CLÔTURE =====================
        private void CloturerAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var os = ObjectSpace;
            var b = (Bulletin)View.CurrentObject;
            if (b == null) return;

            // 1) Sécurités de base
            if (b.Salarie == null)
                throw new UserFriendlyException("Affectez d’abord le salarié.");
            if (b.Annee <= 0 || b.Mois <= 0)
                throw new UserFriendlyException("Période invalide.");

            // 2) Vérifier qu’aucun bulletin précédent du salarié n’est en Brouillon
            bool prevOpen = os.GetObjectsQuery<Bulletin>()
                .Where(x => x.Salarie == b.Salarie
                            && (x.Annee < b.Annee || (x.Annee == b.Annee && x.Mois < b.Mois))
                            && x.Statut == BulletinStatut.Brouillon)
                .Any();
            if (prevOpen)
                throw new UserFriendlyException("Tous les bulletins précédents du salarié doivent être clôturés.");

            // 3) Agréger les échéances de prêts du mois (EnAttente)
            var start = b.MoisStart;
            var end = b.MoisEnd;

            var echeancesDuMois = os.GetObjectsQuery<PretEcheance>()
                .Where(ech => ech.Pret.Salarie == b.Salarie
                              && ech.Statut == PretEcheanceStatut.Prevue 
                              && ech.DateEcheance >= start
                              && ech.DateEcheance <= end)
                .ToList();



            var totalPretMois = echeancesDuMois.Sum(ech => ech.MontantTotal);

            // 4) Poser/mettre à jour la ligne de retenue "Remboursement prêt"
            //    ⚠️ Assure-toi d’avoir RubriqueCanonique.RemboursementPret dans tes rubriques.
            var lignePret = b.EnsureLine(DomainEnums.RubriqueCanonique.RemboursementPret, createIfMissing: true);
            if (lignePret != null)
            {
                lignePret.Base = totalPretMois;
                lignePret.Taux = null; // affichage; si tu veux % mets autre chose
                lignePret.Montant = totalPretMois; // retenue salariale
                lignePret.IsSystem = true;
                if (!lignePret.OrdreCalcul.HasValue)
                    lignePret.OrdreCalcul = lignePret.Rubrique?.OrdreAffichage;
            }

            // 5) Marquer les échéances comme prélevées & lier au bulletin
            foreach (var ech in echeancesDuMois)
            {
                ech.Statut = PretEcheanceStatut.Prelevee ;
                ech.BulletinPreleveur = b;
            }

            // 6) Recalcul des totaux & passage du statut
            b.RecalculerTotaux();

            // Si tu as un statut BulletinStatut.Cloture → remplace la ligne suivante :
            b.Statut = BulletinStatut.Valide;

            os.CommitChanges();

            Application.ShowViewStrategy.ShowMessage(
                $"Bulletin clôturé. {echeancesDuMois.Count} échéance(s) prélevée(s), total {totalPretMois:N0}.",
                InformationType.Success, 3000, InformationPosition.Bottom);

            View.ObjectSpace.Refresh();
        }

        // ===================== RÉOUVERTURE =====================
        private void ReouvrirAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var os = ObjectSpace;
            var b = (Bulletin)View.CurrentObject;
            if (b == null) return;

            // 1) Retrouver les échéances marquées “Prelevee” par CE bulletin
            var echeancesPrelevees = os.GetObjectsQuery<PretEcheance>()
                .Where(ech => ech.BulletinPreleveur == b && ech.Statut == PretEcheanceStatut.Prelevee)
                .ToList();

            // 2) Les remettre “EnAttente” et délier
            foreach (var ech in echeancesPrelevees)
            {
                ech.Statut = PretEcheanceStatut.Prevue;
                ech.BulletinPreleveur = null;
            }

            // 3) Mettre à zéro la retenue de prêt sur la ligne (si elle existe)
            var lignePret = b.Lignes.FirstOrDefault(l => l.Rubrique != null
                                                      && l.Rubrique.Canonique == DomainEnums.RubriqueCanonique.RemboursementPret);
            if (lignePret != null)
            {
                lignePret.Base = 0m;
                lignePret.Montant = 0m;
            }

            // 4) Statut brouillon + recalcul
            b.Statut = BulletinStatut.Brouillon; // ou “EnCours” selon ton workflow
            b.RecalculerTotaux();

            os.CommitChanges();

            Application.ShowViewStrategy.ShowMessage(
                "Bulletin réouvert. Les remboursements de prêts de ce mois ont été remis en attente.",
                InformationType.Info, 3000, InformationPosition.Bottom);

            View.ObjectSpace.Refresh();
        }
    }
}
