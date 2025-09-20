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
using System.Drawing;
using System.Linq;
using System.Text;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    
    // Règle XAF : unicité fonctionnelle (message utilisateur avant l’exception SQL)
    [RuleCombinationOfPropertiesIsUnique(
    "UQ_Categorie_Intitule_Convention",
    DefaultContexts.Save,
    "Intitule;Convention",
    SkipNullOrEmptyValues = false)]


    //[ImageName("BO_Contact")]

    [DefaultProperty("Intitule")]
    //[DefaultListViewOptions(MasterDetailMode.ListViewOnly, false, NewItemRowPosition.None)]
    //[Persistent("DatabaseTableName")]
    // Specify more UI options using a declarative approach (https://docs.devexpress.com/eXpressAppFramework/112701/business-model-design-orm/data-annotations-in-data-model).
    public class Categories : BaseObject
    { // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
        // Use CodeRush to create XPO classes and properties with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/118557
        public Categories(Session session)
            : base(session)
        {
        }
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
        }


      
        string intitule;
        [RuleRequiredField]
        [Size(50)]
        public string Intitule
        {
            get => intitule;
            set => SetPropertyValue(nameof(Intitule), ref intitule, value);
        }

        Convention convention;
        [Association("Convention-Categories")]
        [RuleRequiredField] // recommandé si tu veux éviter les NULL côté FK
        [VisibleInLookupListView(true)]   // → colonne visible dans le lookup
        public Convention Convention
        {
            get => convention;
            set => SetPropertyValue(nameof(Convention), ref convention, value);
        }

        [Association("Categories-Echelons")]
        public XPCollection<Echelons> Echelons
        {
            get
            {
                return GetCollection<Echelons>(nameof(Echelons));
            }
        }
        [Association("Categories-Salaries")]
        public XPCollection<Salarie> Salaries
        {
            get
            {
                return GetCollection<Salarie>(nameof(Salaries));
            }
        }
    }
}