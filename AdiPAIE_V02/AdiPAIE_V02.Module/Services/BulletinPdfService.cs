using System;
using System.IO;
using System.Linq;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.XtraReports.UI;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Génère le PDF du rapport XRpt_Bulletin (créé en UI via ReportsV2),
    /// sans paramètre – filtrage par FilterString.
    /// </summary>
    public static class BulletinPdfService
    {
        /// <summary>
        /// Filtre par Oid du Bulletin (le plus sûr).
        /// </summary>
        public static byte[] BuildPdfByBulletinOid(IObjectSpace os, Frame frame, Guid bulletinOid,
            string reportName = "BulletinPaie")
        {
            var report = LoadReportByName(os, reportName);

            // Pas de paramètre : filtrage via FilterString (DataType du rapport = Bulletin)
            report.FilterString = $"[Oid] = '{bulletinOid}'";

            PrepareAndExport(frame, report, out var pdf);
            return pdf;
        }

        /// <summary>
        /// Variante : filtrer par triplet (Salarie, Année, Mois), toujours sans paramètre.
        /// </summary>
        public static byte[] BuildPdfBySAM(IObjectSpace os, Frame frame, Guid salarieOid, int annee, int mois,
            string reportName = "BulletinPaie")
        {
            var report = LoadReportByName(os, reportName);

            report.FilterString =
                $"[Salarie.Oid] = '{salarieOid}' And [Annee] = {annee} And [Mois] = {mois}";

            PrepareAndExport(frame, report, out var pdf);
            return pdf;
        }

        // --- Helpers ---

        private static XtraReport LoadReportByName(IObjectSpace os, string reportName)
        {
            // Le rapport est celui que vous avez créé depuis l’UI (ReportsV2).
            var rd = os.GetObjectsQuery<IReportDataV2>()
                       .FirstOrDefault(r => r.DisplayName == reportName );
            if (rd == null)
                throw new UserFriendlyException($"Rapport '{reportName}' introuvable dans ReportsV2.");

            var report = ReportDataProvider.ReportsStorage.LoadReport(rd);
            if (report == null)
                throw new UserFriendlyException($"Impossible de charger le rapport '{reportName}'.");
            return report;
        }

        private static void PrepareAndExport(Frame frame, XtraReport report, out byte[] pdfBytes)
        {
            // Branche la data source XAF correctement (pas besoin de ReportParametersObjectBase)
            var svc = frame?.GetController<ReportServiceController>()
                      ?? throw new UserFriendlyException("ReportServiceController introuvable (appelez depuis un Controller XAF avec Frame valide).");

            svc.SetupBeforePrint(report);

            using var ms = new MemoryStream();
            report.ExportToPdf(ms); // CreateDocument() implicite
            pdfBytes = ms.ToArray();
        }
    }
}
