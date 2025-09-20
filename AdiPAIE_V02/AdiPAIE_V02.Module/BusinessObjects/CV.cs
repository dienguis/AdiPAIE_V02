using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Editors;
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
    [FileAttachment(nameof(File))]
   // [DefaultClassOptions, ImageName("BO_Resume")]
    public class CV : BaseObject
    { // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
        // Use CodeRush to create XPO classes and properties with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/118557
        public CV(Session session)
            : base(session)
        {
        }
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
        }

        Salarie salarie;
        private FileData file;
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        public FileData File
        {
            get
            {
                return file;
            }
            set
            {
                SetPropertyValue(nameof(File), ref file, value);
            }
        }

        [RuleRequiredField]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }


        [Aggregated, Association("CV-Dossiers")]
        public XPCollection<Dossier> Portfolio
        {
            get
            {
                return GetCollection<Dossier>(nameof(Portfolio));
            }
        }

        [EditorAlias(EditorAliases.PdfViewerPropertyEditor)]
        public FileData AppercuCV => File;
    }
}