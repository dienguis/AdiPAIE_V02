// Services/BulletinPdfService.cs
using AdiPAIE_V02.Module.Reports;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.Persistent.BaseImpl;
using DevExpress.XtraPrinting;             // PdfExportOptions, PdfEncryptionLevel
using DevExpress.XtraReports.UI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;


namespace AdiPAIE_V02.Module.Services
{
    public static class BulletinPdfService
    {
        private const string ReportDisplayName = "BulletinPaie";

        /// <summary>
        /// V1.4.3 — Bootstrap auto : si le ReportDataV2 "BulletinPaie" n'existe
        /// pas en base, on le crée :
        ///   1. Priorité au REPX embarqué (Reports/BulletinPaie.repx) — design custom
        ///   2. Fallback au template programmatique simple
        /// L'Updater fait déjà ce seed au démarrage ; cette méthode est un filet
        /// de sécurité pour les cas où le rapport est supprimé en cours de session.
        /// </summary>
        private static XtraReport LoadOrCreateReport(IObjectSpace os, IReportExportService svc)
        {
            var report = svc.LoadReport<ReportDataV2>(r => r.DisplayName == ReportDisplayName);
            if (report != null) return report;

            // Pas de rapport en base → tentative de création
            try
            {
                if (!TrySeedFromEmbeddedRepx(os))
                {
                    // Fallback : template programmatique simple
                    using var rptTemplate = ReportTemplates.CreateBulletinA4_Simple();
                    ReportsV2Helper.SaveToReportsV2(os, rptTemplate, ReportDisplayName,
                        typeof(AdiPAIE_V02.Module.BusinessObjects.Bulletin));
                }
            }
            catch
            {
                // On continue : le LoadReport ci-dessous lèvera une exception claire
            }

            // Recharger après save
            report = svc.LoadReport<ReportDataV2>(r => r.DisplayName == ReportDisplayName);
            if (report == null)
            {
                throw new InvalidOperationException(
                    $"Report '{ReportDisplayName}' introuvable et impossible à créer. "
                    + "Vérifiez que l'ObjectSpace permet l'écriture, ou créez le rapport "
                    + "manuellement via le hub Rapports.");
            }
            return report;
        }

        /// <summary>
        /// V1.4.3 — Crée le ReportDataV2 "BulletinPaie" depuis la ressource
        /// embarquée Reports/BulletinPaie.repx (design custom). Retourne true
        /// si le seed a réussi, false si la ressource est absente.
        /// </summary>
        private static bool TrySeedFromEmbeddedRepx(IObjectSpace os)
        {
            const string resourceName = "AdiPAIE_V02.Module.Reports.BulletinPaie.repx";
            var asm = typeof(BulletinPdfService).Assembly;
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return false;

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var repxBytes = ms.ToArray();

            var rd = os.CreateObject<ReportDataV2>();
            rd.DisplayName = ReportDisplayName;
            rd.IsInplaceReport = true;
            // DataTypeName n'est pas settable directement — il dérive du Content REPX
            rd.Content = repxBytes;
            os.CommitChanges();
            return true;
        }

        public static byte[] BuildPdfByBulletinOid(IObjectSpace osReadOnly, Guid bulletinOid, string openPassword)
        {
            if (osReadOnly == null) throw new ArgumentNullException(nameof(osReadOnly));
            if (bulletinOid == Guid.Empty) throw new ArgumentException("Oid invalide.", nameof(bulletinOid));
            if (string.IsNullOrWhiteSpace(openPassword))
                throw new ArgumentException("Le mot de passe PDF est obligatoire.", nameof(openPassword));

            var svc = osReadOnly.ServiceProvider.GetRequiredService<IReportExportService>();

            using XtraReport report = LoadOrCreateReport(osReadOnly, svc);

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
            using XtraReport report = LoadOrCreateReport(osReadOnly, svc);

            var criteria = CriteriaOperator.Parse("Oid = ?", bulletinOid);
            svc.SetupReport(report, criteria.ToString(), null);

            using var ms = new MemoryStream();
            report.ExportToPdf(ms);
            return ms.ToArray();
        }


    }
}
