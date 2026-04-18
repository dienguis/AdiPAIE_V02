using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Boutons "Générer bordereau IPRES" et "Générer bordereau CSS"
    /// sur la ListView des périodes de paie.
    /// </summary>
    public class DeclarationSocialeController : ObjectViewController<ListView, PeriodePaie>
    {
        private readonly PopupWindowShowAction _ipresAction;
        private readonly PopupWindowShowAction _cssAction;

        public DeclarationSocialeController()
        {
            _ipresAction = new PopupWindowShowAction(
                this, "ExporterBordereauIPRES", PredefinedCategory.Edit)
            {
                Caption = "Générer bordereau IPRES",
                ImageName = "BO_Report",
                ToolTip = "Génère le bordereau mensuel IPRES (Régime Général + Cadre).",
            };
            _ipresAction.CustomizePopupWindowParams += (s, e) => CustomizePopup(e, "IPRES");
            _ipresAction.Execute += (s, e) => OnExecute(e, "IPRES");

            _cssAction = new PopupWindowShowAction(
                this, "ExporterBordereauCSS", PredefinedCategory.Edit)
            {
                Caption = "Générer bordereau CSS",
                ImageName = "BO_Report",
                ToolTip = "Génère le bordereau mensuel CSS (Accident Travail + Allocation Familiale).",
            };
            _cssAction.CustomizePopupWindowParams += (s, e) => CustomizePopup(e, "CSS");
            _cssAction.Execute += (s, e) => OnExecute(e, "CSS");
        }

        private void CustomizePopup(CustomizePopupWindowParamsEventArgs e, string type)
        {
            var os = Application.CreateObjectSpace(typeof(DeclarationSocialeParam));
            var param = os.CreateObject<DeclarationSocialeParam>();

            var periode = View?.CurrentObject as PeriodePaie;
            param.Annee = periode?.Annee ?? DateTime.Today.Year;
            param.Mois = periode?.Mois ?? DateTime.Today.Month;

            e.View = Application.CreateDetailView(os, param);
            e.View.Caption = $"Période du bordereau {type}";
            e.DialogController.AcceptAction.Caption = "Générer";
            e.DialogController.CancelAction.Caption = "Annuler";
        }

        private async void OnExecute(PopupWindowShowActionExecuteEventArgs e, string type)
        {
            var param = e.PopupWindowViewCurrentObject as DeclarationSocialeParam;
            if (param == null) return;

            int annee = param.Annee;
            int mois = param.Mois;

            if (annee < 2000 || annee > DateTime.Today.Year + 1 || mois < 1 || mois > 12)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Période invalide : {mois:D2}/{annee}.",
                    InformationType.Warning, 4000, InformationPosition.Top);
                return;
            }

            try
            {
                using var os = Application.CreateObjectSpace(typeof(Bulletin));
                byte[] xlsxBytes;
                string fileName;

                if (type == "IPRES")
                {
                    xlsxBytes = DeclarationIPRESService.Generer(os, annee, mois);
                    fileName = $"Bordereau_IPRES_{annee}_{mois:D2}.xlsx";
                }
                else
                {
                    xlsxBytes = DeclarationCSSService.Generer(os, annee, mois);
                    fileName = $"Bordereau_CSS_{annee}_{mois:D2}.xlsx";
                }

                string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                string base64 = Convert.ToBase64String(xlsxBytes);

                var jsRuntime = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (jsRuntime != null)
                    await jsRuntime.InvokeVoidAsync(
                        "AdiPAIE.downloadFile", fileName, mimeType, base64);

                Application.ShowViewStrategy.ShowMessage(
                    $"Bordereau {type} généré — {mois:D2}/{annee} ({xlsxBytes.Length / 1024} Ko)",
                    InformationType.Success, 5000, InformationPosition.Top);
            }
            catch (UserFriendlyException ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    ex.Message, InformationType.Warning, 6000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur génération bordereau {type} : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }
    }

    // ── Paramètre non-persistant pour la saisie année + mois ────────────
    [DomainComponent]
    [XafDefaultProperty(nameof(DisplayText))]
    public class DeclarationSocialeParam : INotifyPropertyChanged
    {
        private int _annee;
        private int _mois;

        [XafDisplayName("Année")]
        public int Annee
        {
            get => _annee;
            set { if (_annee != value) { _annee = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Annee))); } }
        }

        [XafDisplayName("Mois (1-12)")]
        public int Mois
        {
            get => _mois;
            set { if (_mois != value) { _mois = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mois))); } }
        }

        [Browsable(false)]
        public string DisplayText => $"{Mois:D2}/{Annee}";

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
