// AdiPAIE_V02.Module/Controllers/LivreDePayeController.cs
// Génération et téléchargement du Livre de Paie mensuel
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
    /// Bouton « Livre de Paie » sur la ListView des périodes de paie.
    ///
    /// Ouvre un popup année/mois, génère l'Excel via LivreDePayeService
    /// et le télécharge via JSInterop.
    /// </summary>
    public class LivreDePayeController : ObjectViewController<ListView, PeriodePaie>
    {
        private readonly PopupWindowShowAction _livreAction;

        public LivreDePayeController()
        {
            _livreAction = new PopupWindowShowAction(
                this, "GenererLivreDePaye", PredefinedCategory.Reports)
            {
                Caption = "Livre de Paie",
                ImageName = "BO_Report",
                ToolTip = "Génère le livre de paie mensuel (journal de paie) au format Excel.",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _livreAction.CustomizePopupWindowParams += OnCustomizePopup;
            _livreAction.Execute += OnExecute;
        }

        private void OnCustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var os = Application.CreateObjectSpace(typeof(LivreDePayeParam));
            var param = os.CreateObject<LivreDePayeParam>();

            // Pré-remplir depuis la période sélectionnée
            var periode = View?.CurrentObject as PeriodePaie;
            param.Annee = periode?.Annee ?? DateTime.Today.Year;
            param.Mois = periode?.Mois ?? DateTime.Today.Month;

            e.View = Application.CreateDetailView(os, param);
            e.View.Caption = "Générer le Livre de Paie";
            e.DialogController.AcceptAction.Caption = "Générer";
            e.DialogController.CancelAction.Caption = "Annuler";
        }

        private async void OnExecute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var param = e.PopupWindowViewCurrentObject as LivreDePayeParam;
            if (param == null) return;

            int annee = param.Annee;
            int mois = param.Mois;

            if (annee < 2000 || annee > DateTime.Today.Year + 1 || mois < 1 || mois > 12)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Période invalide : {mois:D2}/{annee}.",
                    InformationType.Warning, 4000, InformationPosition.Top);
                return;
            }

            try
            {
                using var os = Application.CreateObjectSpace(typeof(Bulletin));
                byte[] xlsxBytes = LivreDePayeService.Generer(os, annee, mois);
                string fileName = $"Livre_de_Paie_{annee}_{mois:D2}.xlsx";
                string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                string base64 = Convert.ToBase64String(xlsxBytes);

                var jsRuntime = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (jsRuntime != null)
                    await jsRuntime.InvokeVoidAsync(
                        "AdiPAIE.downloadFile", fileName, mimeType, base64);

                // Audit
                AuditService.Enregistrer(Application,
                    "PeriodePaie", "Livre de Paie",
                    "-", $"Livre de Paie {mois:D2}/{annee}",
                    $"Export Excel généré ({xlsxBytes.Length / 1024} Ko).");

                Application.ShowViewStrategy?.ShowMessage(
                    $"Livre de Paie généré — {mois:D2}/{annee} ({xlsxBytes.Length / 1024} Ko)",
                    InformationType.Success, 5000, InformationPosition.Top);
            }
            catch (UserFriendlyException ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    ex.Message, InformationType.Warning, 6000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur génération livre de paie : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }
    }

    // ── Paramètre non-persistant pour la saisie année + mois ────────────
    [DomainComponent]
    [XafDefaultProperty(nameof(DisplayText))]
    public class LivreDePayeParam : INotifyPropertyChanged
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
