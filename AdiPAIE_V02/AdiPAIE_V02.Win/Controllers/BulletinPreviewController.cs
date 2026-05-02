using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.XtraReports.UI;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Reports;

namespace AdiPAIE_V02.Module.Win.Controllers
{
    // Supporte DetailView et ListView de Bulletin
    public class BulletinPreviewController : ObjectViewController<ObjectView, Bulletin>
    {
        private readonly SimpleAction previewAction;
        private readonly SimpleAction exportLayoutAction;

        public BulletinPreviewController()
        {
            previewAction = new SimpleAction(this, "PreviewBulletin", PredefinedCategory.Print)
            {
                Caption = "Aperçu",
                ImageName = "Preview", // ou un autre glyph
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            previewAction.Execute += PreviewAction_Execute;

            exportLayoutAction = new SimpleAction(this, "ExportBulletinLayout", PredefinedCategory.View)
            {
                Caption = "Export layout",
                ImageName = "Export",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            exportLayoutAction.Execute += (s, e) =>
            {
                var rpt = ReportTemplates.CreateBulletinA4();
                rpt.SaveLayoutToXml(@"C:\Dev\Bulletin_A4.repx"); // crée/écrase le fichier
                Application.ShowViewStrategy.ShowMessage("Layout exporté dans C:\\Dev\\Bulletin_A4.repx",
                    InformationType.Success, 4000, InformationPosition.Bottom);
            };
        }

        private void PreviewAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            // Récupère le bulletin courant (DetailView) ou sélectionné (ListView)
            var bull = (View is DetailView)
                ? View.CurrentObject as Bulletin
                : View.SelectedObjects.Cast<Bulletin>().FirstOrDefault();

            if (bull == null) return;

            var rpt = ReportTemplates.CreateBulletinA4_Simple();
            var p = rpt.Parameters["BulletinOid"];
            if (p != null) { p.Visible = false; p.Value = bull.Oid; }

            new ReportPrintTool(rpt).ShowRibbonPreviewDialog();
        }


    }
}
