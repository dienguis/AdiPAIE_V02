// =============================================================================
//  BudgetVsRealiseFilterModel.cs — V1.2 (mai 2026)
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

        /// <summary>Seuil d'alerte écart (%) pour les rubriques. Défaut 5 %.</summary>
        public decimal SeuilAlertePct { get; set; } = 5m;

        public string ToCacheKey() =>
            $"budget-vs-realise|y={Annee}|s={SiteOid}|seuil={SeuilAlertePct}";
    }
}
