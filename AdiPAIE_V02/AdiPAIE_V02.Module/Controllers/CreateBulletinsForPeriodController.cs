using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.SystemModule;

using DevExpress.Persistent.Base;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    public class CreateBulletinsForPeriodController : ObjectViewController<ListView, Salarie>
    {
        private readonly PopupWindowShowAction createAction;
        private readonly SimpleAction openBulletinsAction; // <-- NOUVEAU

        public CreateBulletinsForPeriodController()
        {
            // Afficher ces actions UNIQUEMENT sur la vue dédiée paie
            TargetViewId = "Salarie_Paie_ListView";

            // 1) Créer bulletins (période) — déjà existant
            createAction = new PopupWindowShowAction(this, "CreateBulletinsForPeriod", PredefinedCategory.Edit)
            {
                Caption = "Créer bulletins",
                ImageName = "BO_Resume",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.Caption,
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects,
                TargetObjectsCriteria = "IsActif = True",
                TargetObjectsCriteriaMode = TargetObjectsCriteriaMode.TrueForAll
            };
            createAction.CustomizePopupWindowParams += CreateAction_CustomizePopupWindowParams;
            createAction.Execute += CreateAction_Execute;

            // 2) Voir bulletins du salarié — NOUVEAU
            openBulletinsAction = new SimpleAction(this, "OpenEmployeeBulletins", PredefinedCategory.View)
            {
                Caption = "Voir bulletins",
                ImageName = "BO_Invoice",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.Caption,
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject // besoin d'1 salarié
            };
            openBulletinsAction.Execute += OpenBulletinsAction_Execute;
        }

        // ====== Handler "Voir bulletins du salarié" ======
        private void OpenBulletinsAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var salFromView = View.SelectedObjects.Cast<Salarie>().FirstOrDefault();
            if (salFromView == null)
                throw new UserFriendlyException("Sélectionnez un salarié.");

            // Récupérer l'Oid AVANT de créer un autre ObjectSpace (thread-safe)
            var salarieOid = salFromView.Oid;
            var salarieNom = salFromView.FullName;

            // Ouvrir une ListView<Bulletin> filtrée sur ce salarié
            var osBul = Application.CreateObjectSpace(typeof(Bulletin));
            var collSrc = Application.CreateCollectionSource(osBul, typeof(Bulletin), "Bulletin_ListView");

            // Filtre par Oid (plus fiable qu'une référence objet cross-ObjectSpace)
            collSrc.Criteria["ByEmployee"] =
                CriteriaOperator.Parse("Salarie.Oid = ?", salarieOid);

            var lv = Application.CreateListView("Bulletin_ListView", collSrc, true);
            lv.Caption = $"Bulletins — {salarieNom}";

            // Ouvrir dans le document courant (onglet, pas popup)
            var svp = new ShowViewParameters(lv)
            {
                TargetWindow = TargetWindow.Current,
                CreatedView = lv
            };
            Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(Frame, openBulletinsAction));
        }

        // ====== le reste de ton contrôleur inchangé (création des bulletins) ======
        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateActionActive();
            View.ObjectSpace.Committed += ObjectSpace_Committed;
        }

        protected override void OnDeactivated()
        {
            View.ObjectSpace.Committed -= ObjectSpace_Committed;
            base.OnDeactivated();
        }

        private void ObjectSpace_Committed(object sender, EventArgs e) => UpdateActionActive();

        private void UpdateActionActive()
        {
            using var os = Application.CreateObjectSpace(typeof(PeriodePaie));
            var oneOpen = os.GetObjectsQuery<PeriodePaie>()
                            .Count(p => p.Statut == PeriodePaieStatut.Ouverte) == 1;
            createAction.Active["OneOpenPeriod"] = oneOpen;
        }

        private void CreateAction_CustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var osNP = Application.CreateObjectSpace(typeof(CreationBulletinsParameters));
            var prm = osNP.CreateObject<CreationBulletinsParameters>();

            using var os = Application.CreateObjectSpace(typeof(PeriodePaie));
            var per = os.GetObjectsQuery<PeriodePaie>()
                        .FirstOrDefault(p => p.Statut == PeriodePaieStatut.Ouverte);
            if (per != null) { prm.Annee = per.Annee; prm.Mois = per.Mois; }
            else { var t = DateTime.Today; prm.Annee = t.Year; prm.Mois = t.Month; }

            var dv = Application.CreateDetailView(osNP, prm);
            dv.ViewEditMode = ViewEditMode.Edit;
            e.View = dv;
            e.DialogController.SaveOnAccept = false;
        }

        private void CreateAction_Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var prm = e.PopupWindowViewCurrentObject as CreationBulletinsParameters;
            if (prm == null) return;

            var os = View.ObjectSpace;
            var per = os.GetObjectsQuery<PeriodePaie>()
                        .FirstOrDefault(p => p.Annee == prm.Annee && p.Mois == prm.Mois);
            if (per == null)
                throw new UserFriendlyException($"La période {prm.Mois:00}/{prm.Annee} n'existe pas.");
            if (per.Statut != PeriodePaieStatut.Ouverte)
                throw new UserFriendlyException($"La période {prm.Mois:00}/{prm.Annee} n'est pas Ouverte (statut : {per.Statut}).");

            var selection = View.SelectedObjects.Cast<Salarie>().ToList();
            if (selection.Count == 0)
                throw new UserFriendlyException("Sélectionnez au moins un salarié.");

            int created = 0, skipped = 0;
            Bulletin lastBulletin = null; IObjectSpace lastOS = null;
            var createdKeys = new List<object>();

            foreach (var salFromView in selection)
            {
                var sal = os.GetObject(salFromView);

                var osBull = Application.CreateObjectSpace(typeof(Bulletin));
                var perBull = osBull.GetObjectsQuery<PeriodePaie>()
                                    .FirstOrDefault(p => p.Statut == PeriodePaieStatut.Ouverte
                                                      && p.Annee == per.Annee
                                                      && p.Mois == per.Mois);
                if (perBull == null) continue;

                var s = osBull.GetObject(sal);

                // Contrôle : pas de bulletin avant la date d'embauche
                if (s.DateEmbauche != default)
                {
                    var finMois = new DateTime(perBull.Annee, perBull.Mois, 1)
                                      .AddMonths(1).AddDays(-1);
                    if (s.DateEmbauche > finMois)
                    {
                        skipped++;
                        continue; // Salarié pas encore recruté pour cette période
                    }
                }

                var b = osBull.GetObjectsQuery<Bulletin>()
                              .FirstOrDefault(bb => bb.Salarie == s && bb.Annee == perBull.Annee && bb.Mois == perBull.Mois);
                if (b != null) { skipped++; continue; }

                b = osBull.CreateObject<Bulletin>();
                b.Salarie = s;
                b.Annee = perBull.Annee;
                b.Mois = perBull.Mois;
                b.DateDebut = perBull.DateDebut;
                b.DateFin = perBull.DateFin;

                b.CopierDepuisModele(null, overwriteExistingLines: false, onlyIncludeDefault: true);

                if (prm.RecalculerApresCreation)
                b.RecalculerDepuisParametrage();

                osBull.CommitChanges();

                created++;
                lastBulletin = b; lastOS = osBull;
                try { createdKeys.Add(osBull.GetKeyValue(b)); } catch { }
            }

            os.CommitChanges();

            // Audit trail for batch creation
            AuditService.Enregistrer(Application, "Bulletin", "BatchCreate",
                per.Oid.ToString(), $"Période {prm.Mois:00}/{prm.Annee}",
                $"{created} bulletin(s) créé(s) pour {prm.Mois:00}/{prm.Annee}");

            Application.ShowViewStrategy.ShowMessage(
                $"Bulletins {prm.Mois:00}/{prm.Annee} : {created} créé(s), {skipped} déjà présent(s).",
                InformationType.Success, 4000, InformationPosition.Bottom);

            if (selection.Count == 1 && lastBulletin != null)
            {
                var dv = Application.CreateDetailView(lastOS, lastBulletin);
                dv.ViewEditMode = ViewEditMode.Edit;
                var svp = new ShowViewParameters(dv) { TargetWindow = TargetWindow.NewWindow };
                Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(Frame, null));
            }
            else if (createdKeys.Count > 1)
            {
                var osBulList = Application.CreateObjectSpace(typeof(Bulletin));
                var lv = Application.CreateListView(osBulList, typeof(Bulletin), false);
                lv.CollectionSource.Criteria["CreatedSet"] = new InOperator("Oid", createdKeys);

                var svp = new ShowViewParameters(lv) { TargetWindow = TargetWindow.NewWindow };
                Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(Frame, null));
            }

            View.ObjectSpace.Refresh();
        }
    }
    public class SalariePaie_NoDetailController : ObjectViewController<ListView, AdiPAIE_V02.Module.BusinessObjects.Salarie>
    {
        public SalariePaie_NoDetailController() { TargetViewId = "Salarie_Paie_ListView"; }
        protected override void OnActivated()
        {
            base.OnActivated();
            var proc = Frame.GetController<ListViewProcessCurrentObjectController>();
            if (proc != null) proc.Active["NoOpenDetailHere"] = false;
            var newCtl = Frame.GetController<NewObjectViewController>();
            if (newCtl != null) newCtl.Active["NoNewHere"] = false;
            var editCtl = Frame.GetController<ModificationsController>();
            if (editCtl != null) editCtl.Active["NoEditHere"] = false;
        }
    }

    public class SalariePaie_HighlightSelection_WinController
       : ObjectViewController<ListView, AdiPAIE_V02.Module.BusinessObjects.Salarie>
    {

        public SalariePaie_HighlightSelection_WinController()
        {
            TargetViewId = "Salarie_Paie_ListView";
        }

        //protected override void OnViewControlsCreated()
        //{
        //    base.OnViewControlsCreated();
        //    var gridEditor = View.Editor as GridListEditor;
        //    var gridView = gridEditor?.GridView as GridView;
        //    if (gridView == null) return;

        //    // Full row focus + clearer highlight
        //    gridView.FocusRectStyle = DrawFocusRectStyle.RowFullFocus;
        //    gridView.OptionsSelection.EnableAppearanceFocusedRow = true;
        //    gridView.OptionsSelection.MultiSelect = true; // si tu sélectionnes plusieurs salariés

        //    // Couleurs visibles (adapte si besoin)
        //    gridView.Appearance.FocusedRow.BackColor = Color.FromArgb(255, 235, 140); // jaune doux
        //    gridView.Appearance.FocusedRow.ForeColor = Color.Black;
        //    gridView.Appearance.SelectedRow.BackColor = Color.FromArgb(255, 235, 140);
        //    gridView.Appearance.SelectedRow.ForeColor = Color.Black;
        //    gridView.Appearance.HideSelectionRow.BackColor = Color.FromArgb(255, 245, 200); // quand la grille perd le focus
        //    gridView.Appearance.HideSelectionRow.ForeColor = Color.Black;

        //    // Optionnel: survol lisible
        //    gridView.Appearance.HotTrackedRow.BackColor = Color.FromArgb(230, 245, 255);
        //    gridView.Appearance.HotTrackedRow.ForeColor = Color.Black;
        //}
    }
}
