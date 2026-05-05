// =============================================================================
//  BudgetVsRealiseDashboardService.cs — V1.2.1 (mai 2026)
//
//  REFONTE annuelle :
//    - Compare le BRUT annuel saisi (BudgetMasseSalariale.MontantBrutAnnuel)
//      avec le RÉALISÉ = SUM(Bulletin.BrutFiscal) sur l'année cible.
//    - Évolution 5 années glissantes (N-4 → N).
//    - Ventilation par site pour l'année cible.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.Budget;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;
using Microsoft.Extensions.Caching.Memory;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <inheritdoc cref="IBudgetVsRealiseDashboardService"/>
    public sealed class BudgetVsRealiseDashboardService : IBudgetVsRealiseDashboardService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private const int NbAnneesEvolution = 5;

        public BudgetVsRealiseDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public BudgetVsRealiseDto GetData(BudgetVsRealiseFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var key = filter.ToCacheKey();
            if (_cache.TryGetValue<BudgetVsRealiseDto>(key, out var cached) && cached != null)
                return cached;

            var dto = Compute(filter, os);
            _cache.Set(key, dto, CacheTtl);
            return dto;
        }

        // ─────────────────────────────────────────────────────────────────────
        private BudgetVsRealiseDto Compute(BudgetVsRealiseFilterModel filter, IObjectSpace os)
        {
            // ── 1. Tous les budgets disponibles (toutes années, pour l'évolution) ─
            var allBudgets = os.GetObjectsQuery<BudgetMasseSalariale>().ToList();

            if (filter.SiteOid.HasValue)
            {
                // Quand un site est filtré, on garde les lignes de ce site
                // ET les lignes globales (Site=null) — sera additionné côté année
                allBudgets = allBudgets
                    .Where(b => b.Site == null || b.Site.Oid == filter.SiteOid.Value)
                    .ToList();
            }

            // ── 2. Tous les bulletins (toutes années) ─────────────────────
            var allBulletins = os.GetObjectsQuery<Bulletin>()
                .ToList()
                .Where(b => IsActif(b))
                .ToList();

            if (filter.SiteOid.HasValue)
                allBulletins = allBulletins
                    .Where(b => b.Salarie?.Site?.Oid == filter.SiteOid.Value)
                    .ToList();

            // ── 3. Évolution 5 années glissantes ──────────────────────────
            var evolution = new List<EvolutionAnnuelleDto>(NbAnneesEvolution);
            for (int offset = NbAnneesEvolution - 1; offset >= 0; offset--)
            {
                int an = filter.Annee - offset;

                decimal budgetAn = allBudgets
                    .Where(b => b.Annee == an)
                    .Sum(b => b.MontantBrutAnnuel);

                decimal realiseAn = allBulletins
                    .Where(b => b.Annee == an)
                    .Sum(b => b.BrutFiscal);

                evolution.Add(new EvolutionAnnuelleDto
                {
                    Annee = an,
                    Budget = budgetAn,
                    Realise = realiseAn
                });
            }

            // ── 4. Ventilation par site pour l'année cible ────────────────
            var budgetsCible = allBudgets.Where(b => b.Annee == filter.Annee).ToList();
            var bulletinsCible = allBulletins.Where(b => b.Annee == filter.Annee).ToList();

            // Brut total budgété et réalisé sur l'année cible
            decimal totalBudget = budgetsCible.Sum(b => b.MontantBrutAnnuel);
            decimal totalRealise = bulletinsCible.Sum(b => b.BrutFiscal);

            // Liste des sites distincts (pour ventilation) :
            //   - Sites présents dans le budget (Site != null)
            //   - + une ligne "Global" si des budgets Site=null existent
            var sitesAvecBudget = budgetsCible
                .Where(b => b.Site != null)
                .GroupBy(b => b.Site!.Oid)
                .Select(g => new
                {
                    SiteOid = (Guid?)g.Key,
                    SiteNom = g.First().Site!.Nom,
                    Budget = g.Sum(x => x.MontantBrutAnnuel)
                })
                .ToList();

            decimal budgetGlobalNonVentile = budgetsCible
                .Where(b => b.Site == null)
                .Sum(b => b.MontantBrutAnnuel);

            // Pour le réalisé par site : SUM(BrutFiscal) groupé par Site du salarié
            var realiseParSite = bulletinsCible
                .Where(b => b.Salarie?.Site != null)
                .GroupBy(b => b.Salarie!.Site!.Oid)
                .ToDictionary(g => g.Key, g => g.Sum(b => b.BrutFiscal));

            decimal realiseSansSite = bulletinsCible
                .Where(b => b.Salarie?.Site == null)
                .Sum(b => b.BrutFiscal);

            // Construction de la liste finale
            var parSite = sitesAvecBudget
                .Select(g => new SiteEcartDto
                {
                    SiteOid = g.SiteOid,
                    SiteNom = g.SiteNom,
                    Budget = g.Budget,
                    Realise = realiseParSite.TryGetValue(g.SiteOid!.Value, out var r) ? r : 0m
                })
                .OrderByDescending(s => s.Budget)
                .ToList();

            // Ajouter une ligne "Global (non ventilé)" si pertinent
            if (budgetGlobalNonVentile > 0 || realiseSansSite > 0)
            {
                parSite.Add(new SiteEcartDto
                {
                    SiteOid = null,
                    SiteNom = "Global (non ventilé)",
                    Budget = budgetGlobalNonVentile,
                    Realise = realiseSansSite
                });
            }

            // ── 5. KPIs synthèse ──────────────────────────────────────────
            decimal ecart = totalRealise - totalBudget;
            decimal ecartPct = totalBudget > 0 ? Math.Round(ecart * 100m / totalBudget, 2) : 0m;

            int nbSitesBudgetes = sitesAvecBudget.Count + (budgetGlobalNonVentile > 0 ? 1 : 0);
            int nbSitesEnDepassement = parSite.Count(s => s.EcartPct > 5m);   // seuil 5 % en dur

            var kpis = new KpiBudgetDto
            {
                BudgetBrutAnnuel = totalBudget,
                RealiseBrutAnnuel = totalRealise,
                Ecart = ecart,
                EcartPct = ecartPct,
                NbSitesBudgetes = nbSitesBudgetes,
                NbSitesEnDepassement = nbSitesEnDepassement
            };

            return new BudgetVsRealiseDto
            {
                Kpis = kpis,
                Evolution = evolution,
                ParSite = parSite,
                Annee = filter.Annee,
                CalculatedAt = DateTime.Now
            };
        }

        /// <summary>
        /// Vérifie qu'un objet XPO n'est pas soft-deleted (GCRecord IS NULL).
        /// </summary>
        private static bool IsActif(object obj)
        {
            if (obj == null) return false;
            try
            {
                var prop = obj.GetType().GetProperty("GCRecord");
                if (prop == null) return true;
                var val = prop.GetValue(obj);
                return val == null;
            }
            catch
            {
                return true;
            }
        }
    }
}
