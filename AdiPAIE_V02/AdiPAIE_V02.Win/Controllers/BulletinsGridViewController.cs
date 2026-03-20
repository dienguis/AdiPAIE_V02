using DevExpress.ExpressApp.Win.Editors;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Grid.Drawing;
using DevExpress.ExpressApp;
using System.Drawing;

namespace AdiPAIE_V02.Win.Controllers
{
    public class BulletinsGridViewController
        : ObjectViewController<ListView, AdiPAIE_V02.Module.BusinessObjects.Bulletin>
    {
        protected override void OnViewControlsCreated()
        {
            base.OnViewControlsCreated();
            var gridEditor = View.Editor as GridListEditor;
            var gridView = gridEditor?.GridView;
            if (gridView == null) return;

            gridView.FocusRectStyle = DrawFocusRectStyle.RowFullFocus;
            gridView.OptionsSelection.EnableAppearanceFocusedRow = true;
            gridView.OptionsSelection.MultiSelect = true;
            gridView.Appearance.FocusedRow.BackColor = Color.FromArgb(255, 235, 140);
        }
    }
}