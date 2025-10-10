// Services/BulletinPdfService.cs
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.Persistent.BaseImpl;
using DevExpress.XtraPrinting;              // PdfExportOptions, PdfEncryptionLevel
using DevExpress.XtraReports.UI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Export PDF à partir du report UI (ReportsV2), avec mot de passe d'ouverture uniquement.
    /// Aucune restriction de droits (impression/copie/modifs autorisées).
    /// </summary>
    public static class BulletinPdfService
    {
        private const string ReportDisplayName = "BulletinPaie"; // ← adapte si besoin

        /// <summary>
        /// Exporte le bulletin au format PDF en protégeant l'ouverture par mot de passe (AES256).
        /// </summary>
        public static byte[] BuildPdfByBulletinOid(IObjectSpace osReadOnly, Guid bulletinOid, string openPassword)
        {
            var svc = osReadOnly.ServiceProvider.GetRequiredService<IReportExportService>();
            using XtraReport report = svc.LoadReport<ReportDataV2>(r => r.DisplayName == ReportDisplayName);

            // Paramètre le report avec le critère (ici par Oid)
            var criteria = CriteriaOperator.Parse("Oid = ?", bulletinOid);
            svc.SetupReport(report, criteria.ToString(), null);

            // OPTION A : mot de passe uniquement (aucune restriction de droits)
            var pdf = report.ExportOptions.Pdf;
            pdf.PasswordSecurityOptions.OpenPassword = openPassword;         // mot de passe d'ouverture
            pdf.PasswordSecurityOptions.EncryptionLevel = PdfEncryptionLevel.AES256; // chiffrement fort
            // Ne PAS toucher à PermissionsOptions → tous les droits restent autorisés.

            using var ms = new MemoryStream();
            report.ExportToPdf(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Surchargé : export sans mot de passe (utile pour tests internes).
        /// </summary>
        public static byte[] BuildPdfByBulletinOid(IObjectSpace osReadOnly, Guid bulletinOid)
        {
            var svc = osReadOnly.ServiceProvider.GetRequiredService<IReportExportService>();
            using XtraReport report = svc.LoadReport<ReportDataV2>(r => r.DisplayName == ReportDisplayName);

            var criteria = CriteriaOperator.Parse("Oid = ?", bulletinOid);
            svc.SetupReport(report, criteria.ToString(), null);

            using var ms = new MemoryStream();
            report.ExportToPdf(ms);
            return ms.ToArray();
        }
    }
}
