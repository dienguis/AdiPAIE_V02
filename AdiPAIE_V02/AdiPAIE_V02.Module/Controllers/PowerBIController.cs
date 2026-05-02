// AdiPAIE_V02.Module/Controllers/PowerBIController.cs
// Ouvre un rapport Power BI Service dans un nouvel onglet.
// Si plusieurs rapports sont configurés, propose un choix via SingleChoiceAction.
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    public class PowerBIController : WindowController
    {
        private SingleChoiceAction acOuvrirPowerBI;

        public PowerBIController()
        {
            TargetWindowType = WindowType.Main;
            acOuvrirPowerBI = new SingleChoiceAction(this, "OuvrirRapportPowerBI", "Rapports")
            {
                Caption = "Rapport Power BI",
                ImageName = "BO_Chart",
                ToolTip = "Ouvrir un rapport Power BI",
                ItemType = SingleChoiceActionItemType.ItemIsOperation
            };
            acOuvrirPowerBI.Execute += AcOuvrirPowerBI_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            ChargerRapports();
        }

        private void ChargerRapports()
        {
            try
            {
                acOuvrirPowerBI.Items.Clear();

                using var os = Application.CreateObjectSpace(typeof(RapportPowerBI));
                var rapports = os.GetObjects<RapportPowerBI>(
                    DevExpress.Data.Filtering.CriteriaOperator.Parse("IsActif = True"))
                    .OrderBy(r => r.Ordre)
                    .ThenBy(r => r.Nom)
                    .ToList();

                if (rapports.Count == 0)
                {
                    // Fallback : lire l'ancien champ unique dans ParametresPaie
                    using var osParam = Application.CreateObjectSpace(typeof(ParametresPaie));
                    var param = ParametresPaie.TryGet(osParam);
                    if (param != null && param.PowerBIActif
                        && !string.IsNullOrWhiteSpace(param.PowerBI_ReportUrl))
                    {
                        acOuvrirPowerBI.Items.Add(
                            new ChoiceActionItem("Rapport Power BI", param.PowerBI_ReportUrl));
                        acOuvrirPowerBI.Active["PowerBIConfigured"] = true;
                    }
                    else
                    {
                        acOuvrirPowerBI.Active["PowerBIConfigured"] = false;
                    }
                    return;
                }

                foreach (var r in rapports)
                {
                    var caption = string.IsNullOrWhiteSpace(r.Categorie)
                        ? r.Nom
                        : $"{r.Categorie} — {r.Nom}";
                    var item = new ChoiceActionItem(caption, r.Url);
                    if (!string.IsNullOrWhiteSpace(r.Description))
                        item.ToolTip = r.Description;
                    acOuvrirPowerBI.Items.Add(item);
                }
                acOuvrirPowerBI.Active["PowerBIConfigured"] = true;
            }
            catch
            {
                acOuvrirPowerBI.Active["PowerBIConfigured"] = false;
            }
        }

        private async void AcOuvrirPowerBI_Execute(object sender, SingleChoiceActionExecuteEventArgs e)
        {
            var url = e.SelectedChoiceActionItem?.Data as string;
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new UserFriendlyException(
                    "Aucune URL configurée pour ce rapport.\n"
                    + "Allez dans Tableaux de bord → Rapports Power BI pour configurer les URLs.");
            }

            var jsRuntime = Application.ServiceProvider?.GetService<IJSRuntime>();
            if (jsRuntime != null)
            {
                await jsRuntime.InvokeVoidAsync("open", url, "_blank");
            }
            else
            {
                throw new UserFriendlyException(
                    "Impossible d'ouvrir le navigateur. URL : " + url);
            }
        }
    }
}
