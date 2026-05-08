using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using System;

namespace AdiPAIE_V02.Module.NonPersistent
{
    /// <summary>
    /// Paramètres de génération des déclarations légales.
    /// Affiché dans un popup depuis le menu GRH - Déclarations.
    /// </summary>
    [DomainComponent]
    public class ParametreDeclaration : NonPersistentBaseObject
    {
        [XafDisplayName("Année")]
        public int Annee { get; set; } = DateTime.Today.Year;

        [XafDisplayName("Mois")]
        public int Mois { get; set; } = DateTime.Today.Month;

        [XafDisplayName("Période")]
        public string PeriodeAffichage =>
            new DateTime(Annee, Math.Clamp(Mois, 1, 12), 1)
                .ToString("MMMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
    }
}
