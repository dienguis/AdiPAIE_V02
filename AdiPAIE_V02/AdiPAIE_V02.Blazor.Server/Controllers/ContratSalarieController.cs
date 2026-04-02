using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;
using System.IO;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Blazor.Server.Controllers
{
    /// <summary>
    /// Controller de gestion des contrats salariés.
    /// Placé dans Blazor.Server pour JSInterop (téléchargement PDF).
    ///
    /// Actions :
    ///   - Pré-remplir      : charge les données salarié dans le contrat
    ///   - Activer          : Brouillon → Actif
    ///   - Générer PDF      : fusionne le template + convertit via LibreOffice
    ///   - Télécharger PDF  : sert l'archive si disponible
    ///   - Résilier         : clôture le contrat
    /// </summary>
    public class ContratSalarieController
        : ObjectViewController<DetailView, ContratSalarie>
    {
        readonly SimpleAction preRemplirAction;
        readonly SimpleAction activerAction;
        readonly SimpleAction genererPdfAction;
        readonly SimpleAction telechargerAction;
        readonly SimpleAction resilierAction;

        public ContratSalarieController()
        {
            preRemplirAction = new SimpleAction(this,
                "Contrat_PreRemplir", PredefinedCategory.Edit)
            {
                Caption = "Charger données salarié",
                ImageName = "Action_Refresh",
                ToolTip = "Charge automatiquement les informations du salarié."
            };
            preRemplirAction.Execute += OnPreRemplir;

            activerAction = new SimpleAction(this,
                "Contrat_Activer", PredefinedCategory.Edit)
            {
                Caption = "Activer le contrat",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Activer ce contrat ? Il sera marqué comme contrat en cours."
            };
            activerAction.Execute += (s, e) =>
            {
                var c = (ContratSalarie)View.CurrentObject;
                c.Statut = ContratSalarieStatut.Actif;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Contrat activé.", InformationType.Success, 3000, InformationPosition.Top);
            };

            genererPdfAction = new SimpleAction(this,
                "Contrat_GenererPdf", PredefinedCategory.Edit)
            {
                Caption = "Générer le PDF",
                ImageName = "Action_Export",
                ToolTip = "Fusionne le template Word et génère le PDF du contrat."
            };
            genererPdfAction.Execute += OnGenererPdf;

            telechargerAction = new SimpleAction(this,
                "Contrat_Telecharger", PredefinedCategory.View)
            {
                Caption = "Télécharger le PDF",
                ImageName = "Action_Export",
                ToolTip = "Télécharge le PDF du contrat."
            };
            telechargerAction.Execute += OnTelecharger;

            resilierAction = new SimpleAction(this,
                "Contrat_Resilier", PredefinedCategory.Edit)
            {
                Caption = "Résilier",
                ImageName = "Action_Cancel",
                ConfirmationMessage = "Résilier ce contrat ? Cette action est irréversible."
            };
            resilierAction.Execute += (s, e) =>
            {
                var c = (ContratSalarie)View.CurrentObject;
                c.Statut = ContratSalarieStatut.Resilie;
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Contrat résilié.", InformationType.Warning, 3000, InformationPosition.Top);
            };
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateStates();
            View.CurrentObjectChanged += (_, __) => UpdateStates();
        }

        void UpdateStates()
        {
            var c = View?.CurrentObject as ContratSalarie;
            if (c == null) return;
            var s = c.Statut;

            preRemplirAction.Active["s"] = s == ContratSalarieStatut.Brouillon;
            activerAction.Active["s"] = s == ContratSalarieStatut.Brouillon;
            genererPdfAction.Active["s"] = s != ContratSalarieStatut.Resilie;
            telechargerAction.Active["s"] = c.DocumentPdf != null && c.DocumentPdf.Size > 0;
            resilierAction.Active["s"] = s == ContratSalarieStatut.Actif
                                         || s == ContratSalarieStatut.Suspendu;
        }

        // ── Pré-remplir depuis la fiche salarié ───────────────────────
        private void OnPreRemplir(object sender, SimpleActionExecuteEventArgs e)
        {
            var c = (ContratSalarie)View.CurrentObject;
            var s = c.Salarie;
            if (s == null)
                throw new UserFriendlyException("Sélectionnez d'abord un salarié.");

            c.SalaireBase = s.SalaireBase;
            c.IndemniteLogement = s.IndemniteLogement;
            c.PrimeTransport = s.PrimeTransport;

            ObjectSpace.SetModified(c);
            View.Refresh();
            Application.ShowViewStrategy?.ShowMessage(
                "Données salarié chargées.",
                InformationType.Success, 2000, InformationPosition.Top);
        }

        // ── Génération PDF ────────────────────────────────────────────
        private void OnGenererPdf(object sender, SimpleActionExecuteEventArgs e)
        {
            var contrat = (ContratSalarie)View.CurrentObject;
            try
            {
                using var os = Application.CreateObjectSpace(typeof(ContratSalarie));
                var c = os.GetObjectByKey<ContratSalarie>(contrat.Oid) ?? contrat;

                // 1. Fusionner le template Word
                var docxBytes = ContratTemplateService.Fusionner(c, os);

                // 2. Convertir en PDF via LibreOffice
                var pdfBytes = ConvertirEnPdf(docxBytes, contrat.Reference);
                if (pdfBytes == null)
                    throw new Exception("Conversion PDF échouée — vérifiez que LibreOffice est installé.");

                // 3. Archiver
                var nomFic = $"Contrat_{contrat.TypeContrat}_{contrat.Salarie?.Matricule}_{contrat.DateDebut:yyyyMM}.pdf";
                if (contrat.DocumentPdf == null)
                    contrat.DocumentPdf = ObjectSpace.CreateObject<FileData>();
                using var ms = new MemoryStream(pdfBytes);
                contrat.DocumentPdf.LoadFromStream(nomFic, ms);

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                Application.ShowViewStrategy?.ShowMessage(
                    $"PDF généré : {nomFic}",
                    InformationType.Success, 4000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur génération : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }

        // ── Téléchargement via JSInterop ──────────────────────────────
        private void OnTelecharger(object sender, SimpleActionExecuteEventArgs e)
        {
            var contrat = (ContratSalarie)View.CurrentObject;
            if (contrat.DocumentPdf == null) return;
            try
            {
                using var ms = new MemoryStream();
                contrat.DocumentPdf.SaveToStream(ms);
                var bytes = ms.ToArray();
                var nomFic = contrat.DocumentPdf.FileName
                    ?? $"Contrat_{contrat.Reference}.pdf";

                var js = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (js == null) return;

                _ = js.InvokeVoidAsync(
                        "AdiPAIE.downloadFile", nomFic,
                        "application/pdf",
                        Convert.ToBase64String(bytes))
                    .AsTask();

                Application.ShowViewStrategy?.ShowMessage(
                    "Téléchargement en cours…",
                    InformationType.Success, 2000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur : {ex.Message}",
                    InformationType.Error, 4000, InformationPosition.Top);
            }
        }

        // ── LibreOffice ───────────────────────────────────────────────
        private static byte[] ConvertirEnPdf(byte[] docxBytes, string nomBase)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AdiPAIE_Contrats");
            Directory.CreateDirectory(tempDir);
            var docxPath = Path.Combine(tempDir, $"{nomBase}_{Guid.NewGuid():N}.docx");
            var pdfPath = Path.ChangeExtension(docxPath, ".pdf");
            File.WriteAllBytes(docxPath, docxBytes);
            try
            {
                var chemins = new[]
                {
                    @"C:\Program Files\LibreOffice\program\soffice.exe",
                    @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
                    "/usr/bin/soffice", "soffice"
                };
                var soffice = chemins.FirstOrDefault(File.Exists) ?? "soffice";
                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = soffice,
                        Arguments = $"--headless --convert-to pdf "
                                        + $"--outdir \"{tempDir}\" \"{docxPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                if (!proc.WaitForExit(30_000)) { proc.Kill(); return null; }
                return File.Exists(pdfPath) ? File.ReadAllBytes(pdfPath) : null;
            }
            catch { return null; }
            finally
            {
                try { File.Delete(docxPath); } catch { }
                try { if (File.Exists(pdfPath)) File.Delete(pdfPath); } catch { }
            }
        }
    }
}
