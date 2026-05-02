// AdiPAIE_V02.Module/BusinessObjects/CentreImports.cs
//
// Page singleton qui regroupe toutes les actions d'import en masse :
//   - Import Salariés (Excel)
//   - Import Conjoints (Excel)
//   - Import Comptes bancaires (Excel)
//   - Téléchargement des modèles (3)
//
// Pattern identique à CentreConstantesPaie : un BO simple, un nav item
// dans Paramétrage qui pointe vers le DetailView, et un controller qui
// pose les actions dans une catégorie personnalisée ("ImportsHub").
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [XafDisplayName("Centre d'imports")]
    [ImageName("Action_Export")]
    public class CentreImports : BaseObject
    {
        public CentreImports(Session s) : base(s) { }

        [Size(1000)]
        [XafDisplayName("Notes")]
        public string Note { get; set; }
    }
}
