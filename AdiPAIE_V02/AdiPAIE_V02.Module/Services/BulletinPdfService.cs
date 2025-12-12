// Services/BulletinPdfService.cs
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.Persistent.BaseImpl;
using DevExpress.XtraPrinting;             // PdfExportOptions, PdfEncryptionLevel
using DevExpress.XtraReports.UI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;


namespace AdiPAIE_V02.Module.Services
{
    public static class BulletinPdfService
    {
        private const string ReportDisplayName = "BulletinPaie";

        public static byte[] BuildPdfByBulletinOid(IObjectSpace osReadOnly, Guid bulletinOid, string openPassword)
        {
            if (osReadOnly == null) throw new ArgumentNullException(nameof(osReadOnly));
            if (bulletinOid == Guid.Empty) throw new ArgumentException("Oid invalide.", nameof(bulletinOid));
            if (string.IsNullOrWhiteSpace(openPassword))
                throw new ArgumentException("Le mot de passe PDF est obligatoire.", nameof(openPassword));

            var svc = osReadOnly.ServiceProvider.GetRequiredService<IReportExportService>();

            using XtraReport report =
                svc.LoadReport<ReportDataV2>(r => r.DisplayName == ReportDisplayName)
                ?? throw new InvalidOperationException($"Report '{ReportDisplayName}' introuvable.");

            // Critère (adapter si ton report utilise un paramètre plutôt que FilterString)
            var criteria = CriteriaOperator.Parse("Oid = ?", bulletinOid);
            svc.SetupReport(report, criteria.ToString(), null);

            // IMPORTANT : options passées explicitement à ExportToPdf
            var pdfOpts = new PdfExportOptions
            {
                // qualité/visuels au besoin…
            };
            pdfOpts.PasswordSecurityOptions.OpenPassword = openPassword;               // mot de passe d'ouverture
            pdfOpts.PasswordSecurityOptions.EncryptionLevel = PdfEncryptionLevel.AES256; // chiffrement fort

            using var ms = new MemoryStream();
            report.ExportToPdf(ms, pdfOpts); // ← on passe pdfOpts explicitement
            return ms.ToArray();
        }

        public static byte[] BuildPdfByBulletinOid(IObjectSpace osReadOnly, Guid bulletinOid)
        {
            var svc = osReadOnly.ServiceProvider.GetRequiredService<IReportExportService>();
            using XtraReport report =
                svc.LoadReport<ReportDataV2>(r => r.DisplayName == ReportDisplayName)
                ?? throw new InvalidOperationException($"Report '{ReportDisplayName}' introuvable.");

            var criteria = CriteriaOperator.Parse("Oid = ?", bulletinOid);
            svc.SetupReport(report, criteria.ToString(), null);

            using var ms = new MemoryStream();
            report.ExportToPdf(ms);
            return ms.ToArray();
        }

    
    }
}
