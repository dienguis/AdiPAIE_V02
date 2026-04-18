
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    public class BulletinCloturePretController : ObjectViewController<ObjectView, Bulletin>
    {
        private readonly SimpleAction cloturerAction;
        private readonly SimpleAction reouvrirAction;

        public BulletinCloturePretController()
        {
            cloturerAction = new SimpleAction(this, "CloturerBulletin", PredefinedCategory.Edit)
            {
                Caption = "Clôturer",
                ImageName = "Action_Validation",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Clôture le bulletin sélectionné.",
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects,
                ConfirmationMessage = "Clôturer le(s) bulletin(s) sélectionné(s) ?"
            };
            cloturerAction.Execute += CloturerAction_Execute;

            reouvrirAction = new SimpleAction(this, "ReouvrirBulletin", PredefinedCategory.Edit)
            {
                Caption = "Réouvrir",
                ImageName = "Action_Restore",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Repasse le bulletin en Brouillon.",
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects,
                ConfirmationMessage = "Réouvrir le(s) bulletin(s) sélectionné(s) ?"
            };
            reouvrirAction.Execute += ReouvrirAction_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            // Visible uniquement sur ListView
            var isListView = View is ListView;
            cloturerAction.Active["ListViewOnly"] = isListView;
            reouvrirAction.Active["ListViewOnly"] = isListView;
        }

        private void CloturerAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var os = ObjectSpace;
            var b = e.SelectedObjects.OfType<Bulletin>().FirstOrDefault()
                    ?? View.CurrentObject as Bulletin;
            if (b == null) return;

            if (b.Salarie == null)
                throw new UserFriendlyException("Affectez d'abord le salarié.");
            if (b.Annee <= 0 || b.Mois <= 0)
                throw new UserFriendlyException("Période invalide.");

            bool prevOpen = os.GetObjectsQuery<Bulletin>()
                .Where(x => x.Salarie == b.Salarie
                            && (x.Annee < b.Annee || (x.Annee == b.Annee && x.Mois < b.Mois))
                            && x.Statut == BulletinStatut.Brouillon)
                .Any();
            if (prevOpen)
                throw new UserFriendlyException("Tous les bulletins précédents du salarié doivent être clôturés.");

            b.Statut = BulletinStatut.Cloture;
            os.CommitChanges();
            View.ObjectSpace.Refresh();

            AuditService.Enregistrer(Application, "Bulletin", "Clôturer",
                b.Oid.ToString(), b.DisplayName,
                $"Clôture bulletin {b.Salarie?.FullName} — {b.Periode}",
                nouveauStatut: "Clôturé");

            Application.ShowViewStrategy.ShowMessage(
                "Bulletin clôturé.", InformationType.Success, 3000, InformationPosition.Top);
        }

        private void ReouvrirAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var os = ObjectSpace;
            var b = e.SelectedObjects.OfType<Bulletin>().FirstOrDefault()
                    ?? View.CurrentObject as Bulletin;
            if (b == null) return;

            // Remettre les échéances en Prevue
            var echeancesPrelevees = os.GetObjectsQuery<PretEcheance>()
                .Where(ech => ech.BulletinPreleveur == b && ech.Statut == PretEcheanceStatut.Prelevee)
                .ToList();
            foreach (var ech in echeancesPrelevees)
            {
                ech.Statut = PretEcheanceStatut.Prevue;
                ech.BulletinPreleveur = null;
            }

            // Remettre la ligne prêt à zéro
            var lignePret = b.Lignes.FirstOrDefault(l =>
                l.Rubrique?.Canonique == DomainEnums.RubriqueCanonique.RemboursementPret);
            if (lignePret != null)
            {
                lignePret.Base = 0m;
                lignePret.Montant = 0m;
            }

            b.Statut = BulletinStatut.Brouillon;
            b.RecalculerSurGrilleExistante();
            os.CommitChanges();
            View.ObjectSpace.Refresh();

            AuditService.Enregistrer(Application, "Bulletin", "Réouvrir",
                b.Oid.ToString(), b.DisplayName,
                $"Réouverture bulletin {b.Salarie?.FullName} — {b.Periode}",
                nouveauStatut: "Brouillon");

            Application.ShowViewStrategy.ShowMessage(
                "Bulletin réouvert en Brouillon.",
                InformationType.Info, 3000, InformationPosition.Top);
        }
    }
}
