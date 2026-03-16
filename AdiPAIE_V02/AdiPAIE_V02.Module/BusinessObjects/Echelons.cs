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
using DevExpress.ExpressApp.Editors;

namespace AdiPAIE_V02.Module.BusinessObjects
{
   // [DefaultClassOptions]
    [RuleCombinationOfPropertiesIsUnique(
    "UQ_Echelon_Code_Categories",
    DefaultContexts.Save,
    "Code;Categories",
    SkipNullOrEmptyValues = false)]
        [DefaultClassOptions, XafDisplayName("Echelons")]
    [DefaultProperty(nameof(Libelle))]

    public class Echelons : BaseObject
    { // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
        // Use CodeRush to create XPO classes and properties with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/118557
        public Echelons(Session session)
            : base(session)
        {
        }
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
        }

        Categories categories;
        decimal idemniteLogement;
        decimal tauxHoraire;
        decimal salaireBase;
        string libelle;
        string code;

        [Size(20)]
        [RuleRequiredField]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code, value);
        }

        [Size(100)]
        [RuleRequiredField]
        [XafDisplayName("Intitulé")]
        public string Libelle
        {
            get => libelle;
            set => SetPropertyValue(nameof(Libelle), ref libelle, value);
        }

        [DbType("decimal(18,0)")]
        [EditorAlias(EditorAliases.DecimalPropertyEditor)]
        public decimal SalaireBase
        {
            get => salaireBase;
            set => SetPropertyValue(nameof(SalaireBase), ref salaireBase, value);
        }

        [DbType("decimal(18,3)")]
        [EditorAlias(EditorAliases.DecimalPropertyEditor)]
        public decimal TauxHoraire
        {
            get => tauxHoraire;
            set => SetPropertyValue(nameof(TauxHoraire), ref tauxHoraire, value);
        }

        [DbType("decimal(18,0)")]
        [EditorAlias(EditorAliases.DecimalPropertyEditor)]
        public decimal IdemniteLogement
        {
            get => idemniteLogement;
            set => SetPropertyValue(nameof(IdemniteLogement), ref idemniteLogement, value);
        }
        
        [Association("Categories-Echelons")]
        public Categories Categories
        {
            get => categories;
            set => SetPropertyValue(nameof(Categories), ref categories, value);
        }

        // Echelons.cs (ajoute simplement l’association inverse)
        [Association("Echelons-Salaries")]
        public XPCollection<Salarie> Salaries => GetCollection<Salarie>(nameof(Salaries));






    }
}