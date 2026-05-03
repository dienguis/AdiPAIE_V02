// AdiPAIE_V02.Blazor.Server/Controllers/RapportPowerBIViewController.cs
// Bouton "Ouvrir le rapport" sur la DetailView et la ListView de RapportPowerBI.
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;

namespace AdiPAIE_V02.Blazor.Server.Controllers
{
    public class RapportPowerBIDetailController : ObjectViewController<DetailView, RapportPowerBI>
    {
        private SimpleAction acOuvrir;

        public RapportPowerBIDetailController()
        {
            acOuvrir = new SimpleAction(this, "OuvrirCeRapportPBI", PredefinedCategory.View)
            {
                Caption = "Ouvrir le rapport",
                ImageName = "BO_Chart",
                ToolTip = "Ouvrir ce rapport Power BI dans un nouvel onglet"
            };
            acOuvrir.Execute += AcOuvrir_Execute;
        }

        private async void AcOuvrir_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var rapport = ViewCurrentObject;
            if (rapport == null || string.IsNullOrWhiteSpace(rapport.Url))
            {
                throw new UserFriendlyException("L'URL du rapport n'est pas renseignée.");
            }

            var js = Application.ServiceProvider?.GetService<IJSRuntime>();
            if (js != null)
            {
                await js.InvokeVoidAsync("open", rapport.Url, "_blank");
            }
        }
    }

    public class RapportPowerBIListController : ObjectViewController<ListView, RapportPowerBI>
    {
        private SimpleAction acOuvrir;

        public RapportPowerBIListController()
        {
            acOuvrir = new SimpleAction(this, "OuvrirRapportPBIDepuisListe", PredefinedCategory.View)
            {
                Caption = "Ouvrir le rapport",
                ImageName = "BO_Chart",
                ToolTip = "Ouvrir le rapport sélectionné dans un nouvel onglet",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            acOuvrir.Execute += AcOuvrir_Execute;
        }

        private async void AcOuvrir_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var rapport = View.CurrentObject as RapportPowerBI;
            if (rapport == null || string.IsNullOrWhiteSpace(rapport.Url))
            {
                throw new UserFriendlyException("Sélectionnez un rapport avec une URL valide.");
            }

            var js = Application.ServiceProvider?.GetService<IJSRuntime>();
            if (js != null)
            {
                await js.InvokeVoidAsync("open", rapport.Url, "_blank");
            }
        }
    }
}
