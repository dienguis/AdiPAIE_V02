using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
   // [NavigationItem("GRH - Administration")]
    [ImageName("BO_Setting")]  // V1.1 — icône XAF native (paramètres)
    [XafDisplayName("Centre des constantes paie")]
    public class CentreConstantesPaie : BaseObject
    {
        public CentreConstantesPaie(Session s) : base(s) { }

        [Size(200)]
        public string Note { get; set; }
    }
}
