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
    public enum SensEcriture { Debit, Credit }

   
    // Specify more UI options using a declarative approach (https://docs.devexpress.com/eXpressAppFramework/112701/business-model-design-orm/data-annotations-in-data-model).
    public class Ecriture : BaseObject
    { // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
        // Use CodeRush to create XPO classes and properties with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/118557
        public Ecriture(Session session)
            : base(session)
        {
        }
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
        }
        DateTime dateEcriture = DateTime.Today;
        public DateTime DateEcriture { get => dateEcriture; set => SetPropertyValue(nameof(DateEcriture), ref dateEcriture, value); }

        [Size(120)]
        public string Libelle { get => lib; set => SetPropertyValue(nameof(Libelle), ref lib, value); }
        string lib;

        [Association("Ecriture-Lignes")]
        public XPCollection<EcritureLigne> Lignes => GetCollection<EcritureLigne>(nameof(Lignes));

    }


    public class EcritureLigne : BaseObject
    {
        public EcritureLigne(Session s) : base(s) { }

        [Association("Ecriture-Lignes"), RuleRequiredField]
        public Ecriture Ecriture { get => ecriture; set => SetPropertyValue(nameof(Ecriture), ref ecriture, value); }
        Ecriture ecriture;

        [RuleRequiredField]
        public PlanComptable Compte { get => compte; set => SetPropertyValue(nameof(Compte), ref compte, value); }
        PlanComptable compte;

        public SensEcriture Sens { get => sens; set => SetPropertyValue(nameof(Sens), ref sens, value); }
        SensEcriture sens;

        public decimal Montant { get => montant; set => SetPropertyValue(nameof(Montant), ref montant, value); }
        decimal montant;

        [Size(100)]
        public string Reference { get => reference; set => SetPropertyValue(nameof(Reference), ref reference, value); }
        string reference;
    }
}