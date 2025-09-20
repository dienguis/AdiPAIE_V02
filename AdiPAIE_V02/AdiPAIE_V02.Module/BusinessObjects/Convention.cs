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
 //   [DefaultClassOptions]
    //[ImageName("BO_Contact")]
  [DefaultProperty("NomConvention")]
    //[DefaultListViewOptions(MasterDetailMode.ListViewOnly, false, NewItemRowPosition.None)]
    //[Persistent("DatabaseTableName")]
    // Specify more UI options using a declarative approach (https://docs.devexpress.com/eXpressAppFramework/112701/business-model-design-orm/data-annotations-in-data-model).
    public class Convention : BaseObject
    { // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
        // Use CodeRush to create XPO classes and properties with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/118557
        public Convention(Session session)
            : base(session)
        {
        }
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
            DateCreation = DateTime.Today;
            // Utilisateur courant si SecuritySystem est actif
            try { CreePar = SecuritySystem.CurrentUserName; } catch { /* hors contexte */ }
        }
        string code;
        [RuleRequiredField, RuleUniqueValue]
        [Size(20)]
        public string CodeConvention
        {
            get => code;
            set => SetPropertyValue(nameof(CodeConvention), ref code, value);
        }

        string nomconvention;
          public string NomConvention
        {
            get => nomconvention;
            set => SetPropertyValue(nameof(NomConvention), ref nomconvention, value);
        }

        DateTime dateCreation;
        [Browsable(false)]                // cache dans toutes les vues (Detail/List/Lookup)
        public DateTime DateCreation
        {
            get => dateCreation;
            set => SetPropertyValue(nameof(DateCreation), ref dateCreation, value);
        }

        string creePar;
        [Size(128)]
        [Browsable(false)]                 // cache dans toutes les vues (Detail/List/Lookup)
        public string CreePar
        {
            get => creePar;
            set => SetPropertyValue(nameof(CreePar), ref creePar, value);
        }

        // [Aggregated]// permet une suppression en cascade
        [Association("ConventionCompany")] // Unique association name

        public XPCollection<Company> Companies
        {
            get { return GetCollection<Company>("Companies"); }
        }


        [Association("Convention-Categories")]
        public XPCollection<Categories> Categories
        {
            get
            {
                return GetCollection<Categories>(nameof(Categories));
            }
        }
        [Association("Convention-Salaries")]
        public XPCollection<Salarie> Salaries
        {
            get
            {
                return GetCollection<Salarie>(nameof(Salaries));
            }
        }
    }
}