using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [ImageName("BO_FileAttachment")]
    public class Dossier : BaseObject
    { // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
        // Use CodeRush to create XPO classes and properties with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/118557
        public Dossier(Session session)
            : base(session)
        {
        }
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
        }

       CV cv;

        [Association("CV-Dossiers")]
        public CV Cv
        {
            get => cv;
            set => SetPropertyValue(nameof(Cv), ref cv, value);
        }

        DocumentTypeRef _DocumentType;
        [DataSourceCriteria("Actif = true")] // ne proposer que les valeurs actives
        public DocumentTypeRef DocumentType
        {
            get => _DocumentType;
            set => SetPropertyValue(nameof(DocumentType), ref _DocumentType, value);
        }
    }
}