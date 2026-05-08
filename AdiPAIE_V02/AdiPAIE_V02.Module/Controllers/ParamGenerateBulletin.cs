using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Controllers
{
    [DomainComponent]
    [XafDisplayName("Période du bulletin")]
    public class ParamGenerateBulletin
    {
        [ModelDefault("DisplayFormat", "####")]
        [RuleRange(1900, 3000, CustomMessageTemplate = "L'année doit être entre 1900 et 3000.")]
        public int Annee { get; set; } = DateTime.Today.Year;

        [RuleRange(1, 12, CustomMessageTemplate = "Le mois doit être entre 1 et 12.")]
        public int Mois { get; set; } = DateTime.Today.Month;
    }
}
