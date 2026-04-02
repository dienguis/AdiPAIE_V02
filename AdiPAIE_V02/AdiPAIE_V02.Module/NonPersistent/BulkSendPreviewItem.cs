using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.NonPersistent
{
    [DomainComponent]
    public class BulkSendPreviewItem : NonPersistentBaseObject
    {
        [VisibleInDetailView(false)]
        [VisibleInListView(false)]
        [VisibleInLookupListView(false)]
        public Guid BulletinOid { get; set; }

        [XafDisplayName("Sélectionner")]
        public bool Selected { get; set; } = true;

        [XafDisplayName("Période")]
        public string Periode { get; set; }

        [XafDisplayName("Matricule")]
        public string Matricule { get; set; }

        [XafDisplayName("Nom complet")]
        public string FullName { get; set; }

        [XafDisplayName("Email")]
        public string Email { get; set; }

        [XafDisplayName("Archive PDF")]
        public bool HasArchive { get; set; }

        [VisibleInListView(false)]
        [VisibleInDetailView(false)]
        public string FileName { get; set; }

        [XafDisplayName("Statut")]
        public string Note { get; set; }
    }
}
