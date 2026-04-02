
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
    /// Génère l'état 1024 (déclaration annuelle DGID Sénégal).
    /// Visible uniquement sur Paie → Périodes de paie (ListView PeriodePaie).
    /// </summary>
    public class Declaration1024Controller : ObjectViewController<ListView, PeriodePaie>
    {
        private readonly PopupWindowShowAction _exportAction;

        public Declaration1024Controller()
        {
            _exportAction = new PopupWindowShowAction(
                this, "ExporterDeclaration1024", PredefinedCategory.Edit)
            {
                Caption = "Générer état 1024",
                ImageName = "BO_Report",
                ToolTip = "Génère la déclaration annuelle des salaires "
                          + "(état 1024 DGID Sénégal).",
            };
            _exportAction.CustomizePopupWindowParams += OnCustomizePopup;
            _exportAction.Execute += OnExecute;
        }

        // ── Popup : saisie de l'année ─────────────────────────────────────
        private void OnCustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var os = Application.CreateObjectSpace(typeof(Annee1024Param));
            var param = os.CreateObject<Annee1024Param>();
            // Année par défaut = année de la période sélectionnée
            var periode = View?.CurrentObject as PeriodePaie;
            param.Annee = periode?.Annee ?? (DateTime.Today.Year - 1);

            e.View = Application.CreateDetailView(os, param);
            e.View.Caption = "Sélectionner l'année";

            // Libellés des boutons du dialog
            e.DialogController.AcceptAction.Caption = "Générer";
            e.DialogController.CancelAction.Caption = "Annuler";
        }

        // ── Génération du fichier Excel ───────────────────────────────────
        private async void OnExecute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var param = e.PopupWindowViewCurrentObject as Annee1024Param;
            if (param == null) return;

            int annee = param.Annee;

            if (annee < 2000 || annee > DateTime.Today.Year)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Année invalide : {annee}. Saisir une année entre 2000 et {DateTime.Today.Year}.",
                    InformationType.Warning, 4000, InformationPosition.Top);
                return;
            }

            try
            {
                // 1. Générer le fichier Excel
                using var os = Application.CreateObjectSpace(typeof(Bulletin));
                byte[] xlsxBytes = Declaration1024Service.Generer(os, annee);

                // 2. Téléchargement via AdiPAIE.downloadFile (adipaie.js)
                string fileName = $"Etat_1024_{annee}.xlsx";
                string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                string base64 = Convert.ToBase64String(xlsxBytes);

                var jsRuntime = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (jsRuntime != null)
                    await jsRuntime.InvokeVoidAsync("AdiPAIE.downloadFile", fileName, mimeType, base64);

                // 3. Confirmation
                Application.ShowViewStrategy.ShowMessage(
                    $"État 1024 généré — {annee} ({xlsxBytes.Length / 1024} Ko)",
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
                    $"Erreur génération état 1024 : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }
    }

    // ── Objet NonPersistent pour la saisie de l'année ─────────────────────
    [DomainComponent]
    [XafDefaultProperty(nameof(Annee))]
    public class Annee1024Param : INotifyPropertyChanged
    {
        private int _annee;

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

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
