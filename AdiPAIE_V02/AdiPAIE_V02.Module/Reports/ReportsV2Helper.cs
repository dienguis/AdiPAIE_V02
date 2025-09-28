using System.IO;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.Persistent.BaseImpl;           // ReportDataV2
using DevExpress.XtraReports.UI;

namespace AdiPAIE_V02.Module.Reports
{
    public static class ReportsV2Helper
    {
        /// <summary>
        /// Crée ou met à jour un ReportDataV2 (ReportsV2, XPO) à partir d'un XtraReport en mémoire.
        /// </summary>
        public static ReportDataV2 SaveToReportsV2(
            IObjectSpace os,
            XtraReport rpt,
            string displayName,
            System.Type dataTypeForCaption // ex: typeof(Bulletin), sert uniquement pour l'UI (caption)
        )
        {
            // 1) sérialise le report en REPX (XML) -> byte[]
            byte[] repxBytes;
            using (var ms = new MemoryStream())
            {
                rpt.SaveLayoutToXml(ms);
                repxBytes = ms.ToArray();
            }

            // 2) cherche un existant par DisplayName
            var existing = os.GetObjectsQuery<ReportDataV2>()
                             .FirstOrDefault(r => r.DisplayName == displayName);

            var rd = existing ?? os.CreateObject<ReportDataV2>();

            // 3) remplit les métadonnées + contenu
            rd.DisplayName = displayName;
            rd.IsInplaceReport = true;            // visible dans le hub Rapports
            //rd.DataTypeCaption = dataTypeForCaption?.Name ?? "Report";
            rd.Content = repxBytes;

            os.CommitChanges();
            return rd;
        }
    }
}
