using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using System;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DomainComponent, XafDisplayName("Créer l’année")]
    public class PeriodeAnneeParam
    {
        [RuleRange(2000, 2100)]
        public int Annee { get; set; } = DateTime.Today.Year;

        [XafDisplayName("Pré-remplir le libellé (MM/AAAA)")]
        public bool AutoLibelles { get; set; } = true;
    }
}
