
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
            // V1.8.6 - Boucle sur les objets sélectionnés (clôture en masse).
            // Avant : FirstOrDefault -> un seul bulletin clôturé même si N sélectionnés.
            var os = ObjectSpace;
            var bulletins = e.SelectedObjects?.OfType<Bulletin>().ToList()
                          ?? new System.Collections.Generic.List<Bulletin>();
            if (bulletins.Count == 0 && View.CurrentObject is Bulletin bCur)
                bulletins.Add(bCur);

            if (bulletins.Count == 0)
                throw new UserFriendlyException("Aucun bulletin sélectionné.");

            int nbCloturees = 0;
            var erreurs = new System.Collections.Generic.List<string>();

            foreach (var b in bulletins)
            {
                if (b.Salarie == null)
                {
                    erreurs.Add($"{b.DisplayName} : salarié non affecté");
                    continue;
                }
                if (b.Annee <= 0 || b.Mois <= 0)
                {
                    erreurs.Add($"{b.DisplayName} : période invalide");
                    continue;
                }

                // V1.8.6 - Blocage strict : un bulletin en Brouillon ne peut PAS
                // être clôturé directement. Il doit d'abord être Validé (pour
                // que les remboursements de prêts soient enregistrés).
                // Avant : bulletin Brouillon -> Cloture direct, sans passer par
                // ValiderRemboursementsPrets -> prêts jamais marqués Prelevees.
                if (b.Statut == BulletinStatut.Brouillon)
                {
                    erreurs.Add($"{b.DisplayName} : encore en Brouillon - validez d'abord");
                    continue;
                }
                // Déjà clôturé -> skip silencieux
                if (b.Statut == BulletinStatut.Cloture)
                    continue;

                bool prevOpen = os.GetObjectsQuery<Bulletin>()
                    .Where(x => x.Salarie == b.Salarie
                                && (x.Annee < b.Annee || (x.Annee == b.Annee && x.Mois < b.Mois))
                                && x.Statut == BulletinStatut.Brouillon)
                    .Any();
                if (prevOpen)
                {
                    erreurs.Add($"{b.DisplayName} : bulletins précédents encore en Brouillon");
                    continue;
                }

                b.Statut = BulletinStatut.Cloture;
                nbCloturees++;

                AuditService.Enregistrer(Application, "Bulletin", "Clôturer",
                    b.Oid.ToString(), b.DisplayName,
                    $"Clôture bulletin {b.Salarie?.FullName} - {b.Periode}",
                    nouveauStatut: "Clôturé");
            }

            os.CommitChanges();
            View.ObjectSpace.Refresh();

            var msg = nbCloturees > 0
                ? $"{nbCloturees} bulletin(s) clôturé(s)."
                : "Aucun bulletin clôturé.";
            if (erreurs.Count > 0)
                msg += $"\n{erreurs.Count} refusé(s) :\n* " + string.Join("\n* ", erreurs.Take(10));

            Application.ShowViewStrategy.ShowMessage(
                msg,
                nbCloturees > 0 ? InformationType.Success : InformationType.Warning,
                erreurs.Count > 0 ? 8000 : 3000,
                InformationPosition.Top);
        }

        private void ReouvrirAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            // V1.8.6 - Boucle sur les objets sélectionnés (réouverture en masse).
            var os = ObjectSpace;
            var bulletins = e.SelectedObjects?.OfType<Bulletin>().ToList()
                          ?? new System.Collections.Generic.List<Bulletin>();
            if (bulletins.Count == 0 && View.CurrentObject is Bulletin bCur)
                bulletins.Add(bCur);

            if (bulletins.Count == 0)
                throw new UserFriendlyException("Aucun bulletin sélectionné.");

            int nbRouverts = 0;
            foreach (var b in bulletins)
            {
                // Skip silencieux si déjà en Brouillon
                if (b.Statut == BulletinStatut.Brouillon)
                    continue;

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
                nbRouverts++;

                AuditService.Enregistrer(Application, "Bulletin", "Réouvrir",
                    b.Oid.ToString(), b.DisplayName,
                    $"Réouverture bulletin {b.Salarie?.FullName} - {b.Periode}",
                    nouveauStatut: "Brouillon");
            }

            os.CommitChanges();
            View.ObjectSpace.Refresh();

            var msg = nbRouverts > 0
                ? $"{nbRouverts} bulletin(s) réouvert(s) en Brouillon."
                : "Aucun bulletin réouvert (déjà en Brouillon).";
            Application.ShowViewStrategy.ShowMessage(
                msg,
                nbRouverts > 0 ? InformationType.Success : InformationType.Info,
                3000, InformationPosition.Top);
        }
    }
}
