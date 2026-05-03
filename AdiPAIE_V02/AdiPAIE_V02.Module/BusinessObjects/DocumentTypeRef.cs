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
    [DefaultClassOptions]
    [ImageName("BO_Type")]  // V1.1 — icône XAF native (type/référentiel)
    [DefaultProperty(nameof(Nom))]
    public class DocumentTypeRef : BaseObject
    { // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
        // Use CodeRush to create XPO classes and properties with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/118557
        public DocumentTypeRef(Session session)
            : base(session)
        {
        }
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
        }
        string _Code;
        [RuleRequiredField, Size(20)]
        [Indexed(Unique = true, Name = "UX_DocType_Code")]
        public string Code
        {
            get => _Code;
            set => SetPropertyValue(nameof(Code), ref _Code, value);
        }

        string _Nom;
        [RuleRequiredField, Size(100)]
        [VisibleInLookupListView(true)]
        public string Nom
        {
            get => _Nom;
            set => SetPropertyValue(nameof(Nom), ref _Nom, value);
        }

        bool _Actif = true;
        [VisibleInLookupListView(true)]
        public bool Actif
        {
            get => _Actif;
            set => SetPropertyValue(nameof(Actif), ref _Actif, value);
        }

        int _Ordre;
        [VisibleInLookupListView(true)]
        [RuleUniqueValue(DefaultContexts.Save)]
        [Indexed(Name = "IX_DocType_Ordre")]
        public int Ordre
        {
            get => _Ordre;
            set => SetPropertyValue(nameof(Ordre), ref _Ordre, value);
        }
    }
}