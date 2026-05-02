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
    /// Bouton unique "États / Exports" qui regroupe :
    ///   - Livre de paie
    ///   - État VRS
    ///   - Bordereau IPRES
    ///   - Bordereau CSS
    ///   - État 1024
    ///
    /// Remplace les 5 boutons séparés sur la ListView PeriodePaie.
    /// </summary>
    public class PeriodePaieEtatsController : ObjectViewController<ListView, PeriodePaie>
    {
        private readonly PopupWindowShowAction _etatsAction;

        public PeriodePaieEtatsController()
        {
            _etatsAction = new PopupWindowShowAction(
                this, "PeriodePaie_EtatsExports", PredefinedCategory.Reports)
            {
                Caption = "États / Exports",
                ImageName = "BO_Report",
                ToolTip = "Génère un état ou une déclaration pour la période sélectionnée.",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _etatsAction.CustomizePopupWindowParams += OnCustomizePopup;
            _etatsAction.Execute += OnExecute;
        }

        private void OnCustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var os = Application.CreateObjectSpace(typeof(EtatExportParam));
            var param = os.CreateObject<EtatExportParam>();

            var periode = View?.CurrentObject as PeriodePaie;
            param.Annee = periode?.Annee ?? DateTime.Today.Year;
            param.Mois = periode?.Mois ?? DateTime.Today.Month;
            param.TypeEtat = TypeEtatExport.LivreDePaie;

            e.View = Application.CreateDetailView(os, param);
            e.View.Caption = "Générer un état";
            e.DialogController.AcceptAction.Caption = "Générer";
            e.DialogController.CancelAction.Caption = "Annuler";
        }

        private async void OnExecute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var param = e.PopupWindowViewCurrentObject as EtatExportParam;
            if (param == null) return;

            int annee = param.Annee;
            int mois = param.Mois;

            // Validation (sauf état 1024 qui ne nécessite que l'année)
            if (annee < 2000 || annee > DateTime.Today.Year + 1)
            {
                ShowMsg($"Année invalide : {annee}.", InformationType.Warning);
                return;
            }
            if (param.TypeEtat != TypeEtatExport.Etat1024 && (mois < 1 || mois > 12))
            {
                ShowMsg($"Mois invalide : {mois}.", InformationType.Warning);
                return;
            }

            try
            {
                using var os = Application.CreateObjectSpace(typeof(Bulletin));
                byte[] xlsxBytes;
                string fileName;
                string label;

                switch (param.TypeEtat)
                {
                    case TypeEtatExport.LivreDePaie:
                        xlsxBytes = LivreDePayeService.Generer(os, annee, mois);
                        fileName = $"Livre_de_Paie_{annee}_{mois:D2}.xlsx";
                        label = "Livre de Paie";
                        AuditService.Enregistrer(Application,
                            "PeriodePaie", "Livre de Paie", "-",
                            $"Livre de Paie {mois:D2}/{annee}",
                            $"Export Excel généré ({xlsxBytes.Length / 1024} Ko).");
                        break;

                    case TypeEtatExport.EtatVRS:
                        xlsxBytes = EtatVRSService.Generer(os, annee, mois);
                        fileName = $"Etat_VRS_{annee}_{mois:D2}.xlsx";
                        label = "État VRS";
                        break;

                    case TypeEtatExport.BordereauIPRES:
                        xlsxBytes = DeclarationIPRESService.Generer(os, annee, mois);
                        fileName = $"Bordereau_IPRES_{annee}_{mois:D2}.xlsx";
                        label = "Bordereau IPRES";
                        break;

                    case TypeEtatExport.BordereauCSS:
                        xlsxBytes = DeclarationCSSService.Generer(os, annee, mois);
                        fileName = $"Bordereau_CSS_{annee}_{mois:D2}.xlsx";
                        label = "Bordereau CSS";
                        break;

                    case TypeEtatExport.Etat1024:
                        xlsxBytes = Declaration1024Service.Generer(os, annee);
                        fileName = $"Etat_1024_{annee}.xlsx";
                        label = "État 1024";
                        break;

                    default:
                        return;
                }

                string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                string base64 = Convert.ToBase64String(xlsxBytes);

                var jsRuntime = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (jsRuntime != null)
                    await jsRuntime.InvokeVoidAsync(
                        "AdiPAIE.downloadFile", fileName, mimeType, base64);

                ShowMsg($"{label} généré — {(param.TypeEtat == TypeEtatExport.Etat1024 ? annee.ToString() : $"{mois:D2}/{annee}")} ({xlsxBytes.Length / 1024} Ko)",
                    InformationType.Success);
            }
            catch (UserFriendlyException ex)
            {
                ShowMsg(ex.Message, InformationType.Warning);
            }
            catch (Exception ex)
            {
                ShowMsg($"Erreur : {ex.Message}", InformationType.Error);
            }
        }

        private void ShowMsg(string msg, InformationType type)
        {
            Application.ShowViewStrategy?.ShowMessage(msg, type,
                type == InformationType.Error ? 8000 : 5000, InformationPosition.Top);
        }
    }

    // ── Enum pour le type d'état à générer ──────────────────────────────
    public enum TypeEtatExport
    {
        [XafDisplayName("Livre de Paie")]
        LivreDePaie,
        [XafDisplayName("État VRS")]
        EtatVRS,
        [XafDisplayName("Bordereau IPRES")]
        BordereauIPRES,
        [XafDisplayName("Bordereau CSS")]
        BordereauCSS,
        [XafDisplayName("État 1024 (annuel)")]
        Etat1024
    }

    // ── Paramètre non-persistant ────────────────────────────────────────
    [DomainComponent]
    public class EtatExportParam : INotifyPropertyChanged
    {
        private TypeEtatExport _typeEtat;
        private int _annee;
        private int _mois;

        [XafDisplayName("Type d'état")]
        public TypeEtatExport TypeEtat
        {
            get => _typeEtat;
            set { if (_typeEtat != value) { _typeEtat = value; OnChanged(nameof(TypeEtat)); } }
        }

        [XafDisplayName("Année")]
        public int Annee
        {
            get => _annee;
            set { if (_annee != value) { _annee = value; OnChanged(nameof(Annee)); } }
        }

        [XafDisplayName("Mois (1-12)")]
        public int Mois
        {
            get => _mois;
            set { if (_mois != value) { _mois = value; OnChanged(nameof(Mois)); } }
        }

        private void OnChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
