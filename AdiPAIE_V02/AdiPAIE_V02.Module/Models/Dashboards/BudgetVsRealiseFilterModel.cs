// =============================================================================
//  BudgetVsRealiseFilterModel.cs — V1.2.1 (mai 2026)
//
//  Filtre simplifié : Année + (optionnel) Site.
//  Plus de filtre Mois ni Seuil — la consultation est annuelle.
// =============================================================================

using System;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class BudgetVsRealiseFilterModel
    {
        /// <summary>Année cible. Par défaut année courante.</summary>
        public int Annee { get; set; } = DateTime.Today.Year;

        /// <summary>Filtre site (null = tous sites confondus).</summary>
        public Guid? SiteOid { get; set; }

        public string ToCacheKey() =>
            $"budget-vs-realise|y={Annee}|s={SiteOid}";
    }
}
