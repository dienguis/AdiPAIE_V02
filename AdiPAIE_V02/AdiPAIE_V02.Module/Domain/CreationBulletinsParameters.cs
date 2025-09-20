using System;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Validation;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DomainComponent]
    public class CreationBulletinsParameters
    {
        public CreationBulletinsParameters()
        {
            var today = DateTime.Today;
            Annee = today.Year;
            Mois = today.Month;
            OnlyIncludeDefault = true;
            RecalculerApresCreation = true;
        }

        [RuleRange(2000, 2100)]
        public int Annee { get; set; }

        [RuleRange(1, 12)]
        public int Mois { get; set; }

        [ModelDefault("Caption", "Inclure seulement les lignes par défaut du modèle")]
        public bool OnlyIncludeDefault { get; set; }

        [ModelDefault("Caption", "Recalculer après création")]
        public bool RecalculerApresCreation { get; set; }
    }
}
