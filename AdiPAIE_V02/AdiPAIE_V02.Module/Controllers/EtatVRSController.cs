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
    /// Génère l'état VRS mensuel (Versement Retenue à la Source).
    /// Visible sur Paie → Périodes de paie (ListView PeriodePaie).
    /// Bouton "Générer état VRS" dans la toolbar.
    /// </summary>
    /// <remarks>DÉSACTIVÉ — remplacé par PeriodePaieEtatsController (bouton unique "États / Exports").</remarks>
    public class EtatVRSController : ObjectViewController<ListView, PeriodePaie>
    {
        private PopupWindowShowAction _exportAction;

        public EtatVRSController()
        {
            Active["Consolidated"] = false; // Remplacé par PeriodePaieEtatsController
            return;
#pragma warning disable CS0162 // Code historique conservé pour référence (DÉSACTIVÉ)
            _exportAction = new PopupWindowShowAction(
                this, "ExporterEtatVRS", PredefinedCategory.Edit)
            {
                Caption = "État VRS",
                ImageName = "BO_Report",
                ToolTip = "Génère l'état mensuel VRS (Versement Retenue à la Source).",
            };
            _exportAction.CustomizePopupWindowParams += OnCustomizePopup;
            _exportAction.Execute += OnExecute;
#pragma warning restore CS0162
        }

        // ── Popup : saisie de l'année et du mois ─────────────────────────
        private void OnCustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var os = Application.CreateObjectSpace(typeof(EtatVRSParam));
            var param = os.CreateObject<EtatVRSParam>();

            // Valeurs par défaut = période sélectionnée dans la liste
            var periode = View?.CurrentObject as PeriodePaie;
            param.Annee = periode?.Annee ?? DateTime.Today.Year;
            param.Mois = periode?.Mois ?? DateTime.Today.Month;

            e.View = Application.CreateDetailView(os, param);
            e.View.Caption = "Période de l'état VRS";

            e.DialogController.AcceptAction.Caption = "Générer";
            e.DialogController.CancelAction.Caption = "Annuler";
        }

        // ── Génération du fichier Excel ──────────────────────────────────
        private async void OnExecute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var param = e.PopupWindowViewCurrentObject as EtatVRSParam;
            if (param == null) return;

            int annee = param.Annee;
            int mois = param.Mois;

            if (annee < 2000 || annee > DateTime.Today.Year + 1)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Année invalide : {annee}.",
                    InformationType.Warning, 4000, InformationPosition.Top);
                return;
            }

            if (mois < 1 || mois > 12)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Mois invalide : {mois}. Saisir un mois entre 1 et 12.",
                    InformationType.Warning, 4000, InformationPosition.Top);
                return;
            }

            try
            {
                // 1. Générer le fichier Excel
                using var os = Application.CreateObjectSpace(typeof(Bulletin));
                byte[] xlsxBytes = EtatVRSService.Generer(os, annee, mois);

                // 2. Téléchargement via AdiPAIE.downloadFile (adipaie.js)
                string fileName = $"Etat_VRS_{annee}_{mois:D2}.xlsx";
                string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                string base64 = Convert.ToBase64String(xlsxBytes);

                var jsRuntime = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (jsRuntime != null)
                    await jsRuntime.InvokeVoidAsync(
                        "AdiPAIE.downloadFile", fileName, mimeType, base64);

                // 3. Confirmation
                Application.ShowViewStrategy.ShowMessage(
                    $"État VRS généré — {mois:D2}/{annee} ({xlsxBytes.Length / 1024} Ko)",
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
                    $"Erreur génération état VRS : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }
    }

    // ── Objet NonPersistent pour la saisie année + mois ──────────────────
    [DomainComponent]
    [XafDefaultProperty(nameof(DisplayText))]
    public class EtatVRSParam : INotifyPropertyChanged
    {
        private int _annee;
        private int _mois;

        [XafDisplayName("Année")]
        public int Annee
        {
            get => _annee;
            set
            {
                if (_annee != value)
                {
                    _annee = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Annee)));
                }
            }
        }

        [XafDisplayName("Mois (1-12)")]
        public int Mois
        {
            get => _mois;
            set
            {
                if (_mois != value)
                {
                    _mois = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mois)));
                }
            }
        }

        [System.ComponentModel.Browsable(false)]
        public string DisplayText => $"{Mois:D2}/{Annee}";

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
