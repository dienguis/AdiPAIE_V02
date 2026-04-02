// NonPersistent/DeclarationMenu.cs
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;

namespace AdiPAIE_V02.Module.NonPersistent
{
    [DomainComponent]
    [XafDisplayName("Déclarations légales")]
    [ImageName("BO_List")]
    public class DeclarationMenu : NonPersistentBaseObject
    {
        [XafDisplayName("Type")]
        public string Type { get; set; }

        [XafDisplayName("Format")]
        public string Format { get; set; }

        [XafDisplayName("Description")]
        public string Description { get; set; }
    }
}