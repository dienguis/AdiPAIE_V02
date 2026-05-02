// AdiPAIE_V02.Module/BusinessObjects/PowerBIReportView.cs
// Objet non-persistant pour afficher le rapport Power BI embarqué dans l'application.
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DomainComponent]
    [DefaultClassOptions]
    [ModelDefault("Caption", "Rapport Power BI")]
    [ModelDefault("ImageName", "BO_Chart")]
    public class PowerBIReportView : IXafEntityObject, IObjectSpaceLink
    {
        private IObjectSpace _objectSpace;

        [DevExpress.ExpressApp.Data.Key]
        [VisibleInListView(false)]
        [VisibleInDetailView(false)]
        public string Id { get; set; } = "PowerBI";

        /// <summary>URL du rapport Power BI (lue depuis ParametresPaie).</summary>
        [ModelDefault("AllowEdit", "False")]
        public string ReportUrl { get; set; }

        /// <summary>Indique si Power BI est activé.</summary>
        [VisibleInDetailView(false)]
        [VisibleInListView(false)]
        public bool PowerBIActif { get; set; }

        /// <summary>Message affiché si non configuré.</summary>
        [VisibleInDetailView(false)]
        [VisibleInListView(false)]
        public string Message { get; set; }

        // ── IXafEntityObject ──
        public void OnCreated() { }
        public void OnSaving() { }
        public void OnLoaded() { }

        // ── IObjectSpaceLink ──
        public IObjectSpace ObjectSpace
        {
            get => _objectSpace;
            set => _objectSpace = value;
        }
    }
}
