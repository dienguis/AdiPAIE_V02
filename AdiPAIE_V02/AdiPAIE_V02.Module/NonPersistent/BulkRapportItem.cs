using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;

namespace AdiPAIE_V02.Module.NonPersistent
{
    [DomainComponent]
    public class BulkRapportItem : NonPersistentBaseObject
    {
        [XafDisplayName("Résultat")]
        public string Titre { get; set; }

        [XafDisplayName("Détail")]
        [FieldSize(FieldSizeAttribute.Unlimited)]
        public string Contenu { get; set; }
    }
}
