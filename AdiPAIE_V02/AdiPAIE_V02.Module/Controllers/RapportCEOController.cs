// AdiPAIE_V02.Module/Controllers/RapportCEOController.cs
// Bouton « Rapport CEO » accessible depuis GRH → Tableaux de bord (ou tout écran).
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// WindowController global avec PopupWindowShowAction pour le Rapport CEO.
    ///
    /// ⚠️ V1.1 (mai 2026) — DÉPRÉCIÉ
    /// Cette fonctionnalité est REMPLACÉE par les 6 dashboards RH analytiques
    /// (cf. Module Tableaux de Bord RH, route /dashboards). L'action est
    /// masquée partout (Active = false via clé "Deprecated_ReplacedByDashboards")
    /// mais le code est CONSERVÉ pour éviter toute régression et permettre
    /// une éventuelle réactivation future.
    /// Suppression définitive prévue dans un Sprint cleanup ultérieur.
    /// </summary>
    public class RapportCEOController : WindowController
    {
        private const string HideKey = "RapportCEO_HiddenOnListViews";
        // V1.1 — clé pour signaler la dépréciation et masquer partout
        private const string DeprecatedKey = "Deprecated_ReplacedByDashboards";

        private PopupWindowShowAction _rapportAction;

        public RapportCEOController()
        {
            _rapportAction = new PopupWindowShowAction(
                this, "GenererRapportCEO", PredefinedCategory.Reports)
            {
                Caption = "Rapport CEO",
                ImageName = "BO_Report",
                ToolTip = "Génère le rapport exécutif CEO (PDF et/ou Excel)."
            };
            _rapportAction.CustomizePopupWindowParams += OnCustomizePopup;
            _rapportAction.Execute += OnExecute;
        }

        // ── Activation/désactivation selon le type de View ───────
        protected override void OnActivated()
        {
            base.OnActivated();
            // V1.1 — Action désactivée partout (remplacée par les dashboards RH)
            // Le code reste fonctionnel mais l'action XAF n'apparaît plus en UI.
            if (_rapportAction != null)
                _rapportAction.Active.SetItemValue(DeprecatedKey, false);

            // Code legacy conservé (ne s'exécute plus car action désactivée) :
            // if (Frame != null)
            // {
            //     Frame.ViewChanged += OnFrameViewChanged;
            //     UpdateActiveState(Frame.View);
            // }
        }

        protected override void OnDeactivated()
        {
            // if (Frame != null) Frame.ViewChanged -= OnFrameViewChanged;
            base.OnDeactivated();
        }

        private void OnFrameViewChanged(object sender, EventArgs e)
            => UpdateActiveState(Frame?.View);

        private void UpdateActiveState(View view)
        {
            // Masqué sur toutes les ListView, visible ailleurs (legacy V1.0)
            bool isListView = view is ListView;
            _rapportAction.Active.SetItemValue(HideKey, !isListView);
        }

        // ── Popup : paramètres année/mois/options ────────────────

        private void OnCustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var os = Application.CreateObjectSpace(typeof(ParamRapportCEO));
            var param = os.CreateObject<ParamRapportCEO>();

            param.Annee = DateTime.Today.Year;
            param.Mois = DateTime.Today.Month;

            e.View = Application.CreateDetailView(os, param);
            e.View.Caption = "Rapport Exécutif CEO";
            e.DialogController.AcceptAction.Caption = "Générer";
            e.DialogController.CancelAction.Caption = "Annuler";
        }

        // ── Exécution : collecte + génération + téléchargement ───

        private async void OnExecute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var param = e.PopupWindowViewCurrentObject as ParamRapportCEO;
            if (param == null) return;

            int annee = param.Annee;
            int mois = param.Mois;
            bool pdf = param.GenererPdf;
            bool excel = param.GenererExcel;
            bool email = param.EnvoyerEmail;

            // Validation
            if (annee < 2000 || annee > DateTime.Today.Year + 1 || mois < 1 || mois > 12)
            {
                ShowMsg($"Période invalide : {mois:D2}/{annee}.", InformationType.Warning);
                return;
            }

            if (!pdf && !excel)
            {
                ShowMsg("Veuillez sélectionner au moins un format (PDF ou Excel).", InformationType.Warning);
                return;
            }

            try
            {
                // 1. Collecter les données
                using var os = Application.CreateObjectSpace(typeof(Bulletin));
                var data = RapportCEODataService.Collecter(os, annee, mois);

                // 2. Générer les fichiers
                byte[] pdfBytes = null;
                byte[] xlsxBytes = null;

                if (pdf)
                    pdfBytes = RapportCEOPdfGenerator.Generer(data);

                if (excel)
                    xlsxBytes = RapportCEOExcelGenerator.Generer(data);

                // 3. Téléchargement via JSInterop
                var jsRuntime = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (jsRuntime != null)
                {
                    if (pdfBytes != null)
                    {
                        string pdfName = $"Rapport_CEO_{annee}_{mois:D2}.pdf";
                        string pdfB64 = Convert.ToBase64String(pdfBytes);
                        await jsRuntime.InvokeVoidAsync(
                            "AdiPAIE.downloadFile", pdfName,
                            "application/pdf", pdfB64);
                    }

                    if (xlsxBytes != null)
                    {
                        string xlsxName = $"Rapport_CEO_{annee}_{mois:D2}.xlsx";
                        string xlsxB64 = Convert.ToBase64String(xlsxBytes);
                        await jsRuntime.InvokeVoidAsync(
                            "AdiPAIE.downloadFile", xlsxName,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            xlsxB64);
                    }
                }

                // 4. Envoi email (si demandé)
                if (email)
                {
                    try
                    {
                        await RapportCEOEmailService.EnvoyerAsync(
                            Application, data, pdfBytes, xlsxBytes);
                        ShowMsg($"Rapport envoyé par email.", InformationType.Success);
                    }
                    catch (Exception exMail)
                    {
                        ShowMsg($"Rapport généré mais erreur email : {exMail.Message}",
                            InformationType.Warning);
                    }
                }

                // 5. Audit
                AuditService.Enregistrer(Application,
                    "RapportCEO", "Rapport Exécutif CEO",
                    "-", $"Rapport CEO {mois:D2}/{annee}",
                    $"PDF={pdf}, Excel={excel}, Email={email}");

                // 6. Confirmation
                var formats = (pdf ? "PDF" : "") + (pdf && excel ? " + " : "") + (excel ? "Excel" : "");
                ShowMsg($"Rapport CEO généré ({formats}) — {mois:D2}/{annee}.",
                    InformationType.Success);
            }
            catch (UserFriendlyException ex)
            {
                ShowMsg(ex.Message, InformationType.Warning);
            }
            catch (Exception ex)
            {
                ShowMsg($"Erreur génération rapport CEO : {ex.Message}", InformationType.Error);
            }
        }

        private void ShowMsg(string msg, InformationType type)
        {
            Application.ShowViewStrategy?.ShowMessage(msg, type, 6000, InformationPosition.Top);
        }
    }
}
