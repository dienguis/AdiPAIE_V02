using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.NonPersistent
{
    /// <summary>
    /// V1.8.5 - DTO non persistant pour la popup de solde anticipé d'un prêt.
    ///
    /// Cas d'usage : sur un prêt de 5 000 000 avec 750 000 restant à régler
    /// au 30 mai, le RH clique "Solder anticipé" sur le prêt et choisit
    /// "juin 2026" -> une échéance de 750 000 est créée pour juin,
    /// automatiquement prélevée sur le bulletin de juin, et le prêt passe
    /// à Termine.
    /// </summary>
    [DomainComponent]
    [XafDisplayName("Solder anticipé un prêt")]
    public class SoldeAnticipePretRequest
    {
        [XafDisplayName("Prêt")]
        [ModelDefault("AllowEdit", "False")]
        public string PretDisplayName { get; set; }

        [XafDisplayName("Salarié")]
        [ModelDefault("AllowEdit", "False")]
        public string SalarieDisplayName { get; set; }

        [XafDisplayName("Capital restant à régler")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal ResteARegler { get; set; }

        [XafDisplayName("Année d'imputation")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public int AnneeImputation { get; set; } = DateTime.Today.Year;

        [XafDisplayName("Mois d'imputation")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public int MoisImputation { get; set; } = DateTime.Today.Month;

        [XafDisplayName("Référence")]
        public string Reference { get; set; } = "Solde anticipé";
    }
}
