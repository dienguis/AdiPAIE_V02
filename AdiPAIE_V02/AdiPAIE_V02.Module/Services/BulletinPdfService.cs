using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.Persistent.BaseImpl;
using DevExpress.XtraReports.UI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace AdiPAIE_V02.Module.Services
{
    public static class BulletinPdfService
    {
        private const string ReportDisplayName = "BulletinPaie"; // ← adapte au nom exact

        public static byte[] BuildPdfByBulletinOid(IObjectSpace osReadOnly, Guid bulletinOid)
        {
            var svc = osReadOnly.ServiceProvider.GetRequiredService<IReportExportService>();

            using XtraReport report = svc.LoadReport<ReportDataV2>(r => r.DisplayName == ReportDisplayName);

            // Critère XAF pour un bulletin précis
            var criteria = CriteriaOperator.Parse("Oid = ?", bulletinOid);
            svc.SetupReport(report, criteria.ToString(), null);

            // Si le report a des paramètres déclarés :
            // report.Parameters["BulletinOid"].Value = bulletinOid;
            // report.Parameters["BulletinOid"].Visible = false;

            using var ms = new MemoryStream();
            report.ExportToPdf(ms);
            return ms.ToArray();
        }
    }
}
