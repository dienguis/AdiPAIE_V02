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

    // [DefaultClassOptions, NavigationItem("Référentiel")]
    // Specify more UI options using a declarative approach (https://docs.devexpress.com/eXpressAppFramework/112701/business-model-design-orm/data-annotations-in-data-model).
    public class ParametresCompta : BaseObject
    { // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
        // Use CodeRush to create XPO classes and properties with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/118557
        public ParametresCompta(Session session)
            : base(session)
        {
        }
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
        }
        // ex: 421100 - Rémunérations dues (débité pour les retenues salarié)
        PlanComptable comptePersonnel;
        [RuleRequiredField]
        public PlanComptable ComptePersonnel
        {
            get => comptePersonnel; set => SetPropertyValue(nameof(ComptePersonnel), ref comptePersonnel, value);
        }
        // ex: 645xxx - Charges de sécurité sociale (débit pour part employeur)
        PlanComptable compteChargeEmployeurDefaut;
        [RuleRequiredField]
        public PlanComptable CompteChargeEmployeurDefaut
        {
            get => compteChargeEmployeurDefaut; set => SetPropertyValue(nameof(CompteChargeEmployeurDefaut), ref compteChargeEmployeurDefaut, value);
        }

        public static ParametresCompta GetOrCreate(Session s)
        {
            var p = s.FindObject<ParametresCompta>(null);
            if (p == null) p = new ParametresCompta(s);
            return p;
        }


    }
}