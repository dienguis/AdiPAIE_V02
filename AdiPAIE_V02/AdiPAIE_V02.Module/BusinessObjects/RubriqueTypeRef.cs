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
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
   // [DefaultClassOptions]
   // [NavigationItem("Référentiel")]
    [DefaultProperty("Libelle")]
    //[ImageName("BO_Contact")]
    //[DefaultProperty("DisplayMemberNameForLookupEditorsOfThisType")]
    //[DefaultListViewOptions(MasterDetailMode.ListViewOnly, false, NewItemRowPosition.None)]
    //[Persistent("DatabaseTableName")]
    // Specify more UI options using a declarative approach (https://docs.devexpress.com/eXpressAppFramework/112701/business-model-design-orm/data-annotations-in-data-model).
    public class RubriqueTypeRef : BaseObject
    { // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
        // Use CodeRush to create XPO classes and properties with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/118557
        public RubriqueTypeRef(Session session)
            : base(session)
        {
        }
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
        }
   
      string code;
        [RuleRequiredField, Size(50)]
        [Indexed(Unique = true)]
        [RuleRegularExpression(@"^[A-Z0-9_]{2,20}$",
       CustomMessageTemplate = "Code en MAJUSCULES, 2–20 caractères, chiffres et underscore autorisés.")]
       public string Code { get => code; set => SetPropertyValue(nameof(Code), ref code, value); }


        [RuleRequiredField, Size(120)]
        public string Libelle { get => lib; set => SetPropertyValue(nameof(Libelle), ref lib, value); }
        string lib;

        // Regroupement choisi via lookup => plus de saisie libre
        [RuleRequiredField]
        [Association("GroupeImpressionRef-Types")]
        public GroupeImpressionRef Groupe { get => grp; set => SetPropertyValue(nameof(Groupe), ref grp, value); }
        GroupeImpressionRef grp;

        // Défauts hérités par les rubriques
         bool bf;
          bool bs;
        RubriqueTypeCalcul calc = RubriqueTypeCalcul.Gain;

        [ModelDefault("ImmediatePostData", "True")]
        public bool BruteFiscal { get => bf; set => SetPropertyValue(nameof(BruteFiscal), ref bf, value); }

        [ModelDefault("ImmediatePostData", "True")]
        public bool BruteSocial { get => bs; set => SetPropertyValue(nameof(BruteSocial), ref bs, value); }

        [ModelDefault("ImmediatePostData", "True")]
        public RubriqueTypeCalcul DefaultTypeCalcul { get => calc; set => SetPropertyValue(nameof(DefaultTypeCalcul), ref calc, value); }



        public SensAssiette DefaultSens { get => sens; set => SetPropertyValue(nameof(DefaultSens), ref sens, value); }
        SensAssiette sens = SensAssiette.Plus;

        public bool Actif { get => actif; set => SetPropertyValue(nameof(Actif), ref actif, value); }
        bool actif = true;

        [Association("TypeRef-Rubriques")]
        public XPCollection<Rubrique> Rubriques => GetCollection<Rubrique>(nameof(Rubriques));

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted)
            {
                Code = (Code ?? "").Trim();
                Libelle = (Libelle ?? "").Trim();
            }
        }


    }
}