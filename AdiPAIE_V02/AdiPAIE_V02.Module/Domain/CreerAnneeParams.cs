using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using System;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DomainComponent]
    public class CreerAnneeParams : NonPersistentBaseObject
    {
        // IMPORTANT : NonPersistentBaseObject -> ctor Guid
        public CreerAnneeParams() : base(Guid.NewGuid()) { }

        [RuleRange(2000, 2100)]
        public int Annee { get; set; } = DateTime.Today.Year;

        // Option : autoriser une année ≠ année courante (par défaut : non)
        public bool AutoriserAnneeHorsCourante { get; set; } = false;

        // Option : recréer les mois existants ? (par défaut : non)
        public bool EcraserSiExiste { get; set; } = false;
    }
}
