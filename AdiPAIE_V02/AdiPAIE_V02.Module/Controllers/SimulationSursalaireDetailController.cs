// Controllers/SimulationSursalaireDetailController.cs
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;
using System.Text;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Uniquement sur la DetailView de SimulationSursalaire.
    /// - Calculer : lance le calcul et remplit les champs (pas de commit auto).
    /// - Appliquer au salarié : pousse le SursalaireCalcule sur le Salarie lié (commit).
    /// </summary>
    public sealed class SimulationSursalaireDetailController
        : ObjectViewController<DetailView, AdiPAIE_V02.Module.BusinessObjects.SimulationSursalaire>
    {
        private readonly SimpleAction _calculer;
        private readonly SimpleAction _appliquer;

        public SimulationSursalaireDetailController()
        {
            // Action 1 : Calculer
            _calculer = new SimpleAction(this, "SimCalcSursalaire", PredefinedCategory.Edit)
            {
                Caption = "Calculer",
                ImageName = "BO_Calculator",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Calcule le sursalaire pour atteindre le net cible"
            };
            _calculer.Execute += OnCalculer;

            // Action 2 : Appliquer au salarié (commit)
            _appliquer = new SimpleAction(this, "SimApplyToEmployee", PredefinedCategory.RecordEdit)
            {
                Caption = "Appliquer au salarié",
                ImageName = "BO_Person",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Copie le sursalaire calculé dans la fiche du salarié et enregistre"
            };
            _appliquer.Execute += OnAppliquer;
            _appliquer.ConfirmationMessage = "Appliquer ce sursalaire au salarié lié ?";
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateEnableState();
            View.CurrentObjectChanged += View_CurrentObjectChanged;
            View.ObjectSpace.ObjectChanged += ObjectSpace_ObjectChanged;
        }

        protected override void OnDeactivated()
        {
            View.CurrentObjectChanged -= View_CurrentObjectChanged;
            View.ObjectSpace.ObjectChanged -= ObjectSpace_ObjectChanged;
            base.OnDeactivated();
        }

        private void View_CurrentObjectChanged(object sender, EventArgs e) => UpdateEnableState();

        private void ObjectSpace_ObjectChanged(object sender, ObjectChangedEventArgs e)
        {
            if (ReferenceEquals(e.Object, View.CurrentObject))
                UpdateEnableState();
        }

        private void UpdateEnableState()
        {
            var sim = View.CurrentObject as AdiPAIE_V02.Module.BusinessObjects.SimulationSursalaire;
            bool hasSim = sim != null;
            bool canCalc = hasSim && sim.NetCible > 0 && sim.Salarie != null;
            bool canApply = hasSim && sim.Salarie != null && sim.SursalaireCalcule > 0;

            _calculer.Enabled.SetItemValue("CanCalc", canCalc);
            _appliquer.Enabled.SetItemValue("CanApply", canApply);
        }

        private void OnCalculer(object sender, SimpleActionExecuteEventArgs e)
        {
            var sim = View.CurrentObject as AdiPAIE_V02.Module.BusinessObjects.SimulationSursalaire;
            if (sim == null) return;

            if (sim.Salarie == null)
                throw new UserFriendlyException("Sélectionnez un salarié.");
            if (sim.NetCible <= 0)
                throw new UserFriendlyException("Saisissez un net cible > 0.");

            try
            {
                AdiPAIE_V02.Module.BusinessObjects.SimulationSursalaireHelper.CalculerSursalaire(sim);

                var sb = new StringBuilder();
                sb.AppendLine("✓ Calcul terminé");
                sb.AppendLine($"Sursalaire : {sim.SursalaireCalcule:N0} FCFA");
                sb.AppendLine($"Net obtenu : {sim.NetObtenu:N0} FCFA (écart {sim.EcartNet:N0})");

                Application.ShowViewStrategy.ShowMessage(
                    sb.ToString(), InformationType.Success, 5000, InformationPosition.Top);

                // pas de commit automatique – l’utilisateur garde la main
                UpdateEnableState();
                View.Refresh();
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur de calcul : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }

        private void OnAppliquer(object sender, SimpleActionExecuteEventArgs e)
        {
            var sim = View.CurrentObject as AdiPAIE_V02.Module.BusinessObjects.SimulationSursalaire;
            if (sim == null) return;

            if (sim.Salarie == null)
                throw new UserFriendlyException("Aucun salarié lié.");
            if (sim.SursalaireCalcule <= 0)
                throw new UserFriendlyException("Calculez d’abord le sursalaire.");

            try
            {
                // appliquer sur le salarié lié
                var os = View.ObjectSpace;
                var sal = os.GetObject(sim.Salarie);
                sal.Sursalaire = sim.SursalaireCalcule;

                // on peut sauvegarder la simulation aussi (historique)
                os.CommitChanges();

                Application.ShowViewStrategy.ShowMessage(
                    $"Sursalaire appliqué : {sim.SursalaireCalcule:N0} FCFA",
                    InformationType.Success, 4000, InformationPosition.Top);

                View.Refresh();
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur d’application : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
            finally
            {
                UpdateEnableState();
            }
        }
    }
}
