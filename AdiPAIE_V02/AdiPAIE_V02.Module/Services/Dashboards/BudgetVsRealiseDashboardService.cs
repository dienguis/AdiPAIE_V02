// =============================================================================
//  BudgetVsRealiseDashboardService.cs — V1.2 (mai 2026)
//
//  Compare le budget mensuel saisi (entité BudgetMasseSalariale) avec le
//  RÉALISÉ calculé depuis Bulletin + BulletinLigne :
//    - Réalisé brut (Bulletin.BrutFiscal)
//    - Charges patronales (SUM BulletinLigne.MontantEmployeur)
//    - Coût employeur total = brut + charges
//
//  KPIs calculés :
//    - Mensuel : 12 lignes (budget / réalisé / écart)
//    - Cumul YTD glissant
//    - Décomposition par rubrique BUDGET (le réalisé n'est pas encore
//      ventilable par rubrique côté V1.2 — affiché en global au prorata)
//    - Décomposition par site
//    - Projection annuelle = YTD réalisé + budget restant
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
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
        private static readonly CultureInfo FrCulture = CultureInfo.GetCultureInfo("fr-FR");

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
            // ── 1. Récupération du BUDGET ─────────────────────────────────
            var budgets = os.GetObjectsQuery<BudgetMasseSalariale>()
                .ToList()
                .Where(b => b.Annee == filter.Annee)
                .ToList();

            if (filter.SiteOid.HasValue)
            {
                // On garde les budgets dédiés à ce site + les budgets globaux (Site=null)
                budgets = budgets
                    .Where(b => b.Site == null || b.Site.Oid == filter.SiteOid.Value)
                    .ToList();
            }

            // ── 2. Récupération du RÉALISÉ (Bulletin) ─────────────────────
            var bulletins = os.GetObjectsQuery<Bulletin>()
                .ToList()
                .Where(b => b.Annee == filter.Annee && IsActif(b))
                .ToList();

            if (filter.SiteOid.HasValue)
                bulletins = bulletins.Where(b => b.Salarie?.Site?.Oid == filter.SiteOid.Value).ToList();

            // ── 3. Charges patronales (BulletinLigne.MontantEmployeur) ────
            decimal totalChargesAnnee = 0m;
            var chargesParMois = new Dictionary<int, decimal>();
            try
            {
                var bulletinOids = bulletins.Select(b => b.Oid).ToHashSet();
                var lignes = os.GetObjectsQuery<BulletinLigne>()
                    .ToList()
                    .Where(bl => bl.Bulletin != null
                              && IsActif(bl)
                              && IsActif(bl.Bulletin)
                              && bulletinOids.Contains(bl.Bulletin.Oid))
                    .ToList();

                totalChargesAnnee = lignes.Sum(bl => bl.MontantEmployeur);

                chargesParMois = lignes
                    .GroupBy(bl => bl.Bulletin!.Mois)
                    .ToDictionary(g => g.Key, g => g.Sum(bl => bl.MontantEmployeur));
            }
            catch
            {
                totalChargesAnnee = 0m;
                chargesParMois = new Dictionary<int, decimal>();
            }

            // ── 4. Détermination du dernier mois ayant du réalisé ─────────
            int moisCourant = bulletins.Count > 0 ? bulletins.Max(b => b.Mois) : 0;
            // Si on est sur l'année courante, on borne au mois actuel
            if (filter.Annee == DateTime.Today.Year)
                moisCourant = Math.Min(moisCourant, DateTime.Today.Month);

            // ── 5. Construction du tableau MENSUEL (12 lignes) ────────────
            var mensuel = new List<MoisBudgetDto>(12);
            for (int m = 1; m <= 12; m++)
            {
                var budgetMois = budgets.Where(b => b.Mois == m).Sum(b => b.Montant);

                var bulletinsMois = bulletins.Where(b => b.Mois == m).ToList();
                var brutMois = bulletinsMois.Sum(b => b.BrutFiscal);
                var chargesMois = chargesParMois.TryGetValue(m, out var c) ? c : 0m;
                var realiseMois = brutMois + chargesMois;

                bool realiseDispo = m <= moisCourant && bulletinsMois.Count > 0;

                mensuel.Add(new MoisBudgetDto
                {
                    Mois = m,
                    MoisLibelle = FrCulture.DateTimeFormat.GetMonthName(m),
                    Budget = budgetMois,
                    Realise = realiseDispo ? realiseMois : 0m,
                    RealiseDisponible = realiseDispo
                });
            }

            // ── 6. Cumul YTD glissant ─────────────────────────────────────
            decimal cumulBudget = 0m;
            decimal cumulRealise = 0m;
            var cumulYtd = new List<CumulYtdDto>(12);
            foreach (var ligne in mensuel)
            {
                cumulBudget += ligne.Budget;
                if (ligne.RealiseDisponible)
                    cumulRealise += ligne.Realise;

                cumulYtd.Add(new CumulYtdDto
                {
                    Mois = ligne.Mois,
                    MoisLibelle = ligne.MoisLibelle,
                    BudgetCumule = cumulBudget,
                    RealiseCumule = cumulRealise
                });
            }

            // ── 7. Décomposition par RUBRIQUE (annuelle) ──────────────────
            //   On affiche le budget par rubrique. Le réalisé est ventilé au
            //   prorata du budget de chaque rubrique sur le total budgété
            //   (heuristique V1.2 — sera affinée en V1.3 avec mapping
            //   Rubrique budget ↔ RubriqueTypeRef réelle).
            var totalBudgetAnnuel = budgets.Sum(b => b.Montant);
            var totalBrutAnnuel = bulletins.Sum(b => b.BrutFiscal);
            var totalRealiseAnnuel = totalBrutAnnuel + totalChargesAnnee;

            var rubriques = budgets
                .GroupBy(b => b.Rubrique)
                .Select(g =>
                {
                    var bRub = g.Sum(x => x.Montant);
                    decimal realiseRub = totalBudgetAnnuel > 0
                        ? Math.Round(totalRealiseAnnuel * bRub / totalBudgetAnnuel, 0)
                        : 0m;
                    return new RubriqueEcartDto
                    {
                        Rubrique = g.Key,
                        RubriqueLibelle = LibelleRubrique(g.Key),
                        Budget = bRub,
                        Realise = realiseRub
                    };
                })
                .OrderByDescending(r => r.Budget)
                .ToList();

            // ── 8. Décomposition par SITE ─────────────────────────────────
            //   Budget : SUM par Site (ou "Global" si Site=null)
            //   Réalisé : SUM bulletins du site + charges patronales du site
            var sites = budgets
                .GroupBy(b => b.Site?.Oid)
                .Select(g =>
                {
                    var bSite = g.Sum(x => x.Montant);
                    var siteOid = g.Key;
                    var siteNom = g.First().Site?.Nom ?? "Budget global";

                    var bulletinsSite = siteOid.HasValue
                        ? bulletins.Where(b => b.Salarie?.Site?.Oid == siteOid.Value).ToList()
                        : bulletins;

                    var brutSite = bulletinsSite.Sum(b => b.BrutFiscal);
                    // Pour les charges, on prend au prorata du brut (approximation)
                    decimal chargesSite = totalBrutAnnuel > 0
                        ? Math.Round(totalChargesAnnee * brutSite / totalBrutAnnuel, 0)
                        : 0m;
                    var realiseSite = brutSite + chargesSite;

                    return new SiteEcartDto
                    {
                        SiteOid = siteOid,
                        SiteNom = siteNom,
                        Budget = bSite,
                        Realise = realiseSite
                    };
                })
                .OrderByDescending(s => s.Budget)
                .ToList();

            // ── 9. KPIs synthèse ──────────────────────────────────────────
            var budgetYtd = mensuel.Where(m => m.Mois <= moisCourant).Sum(m => m.Budget);
            var realiseYtd = mensuel.Where(m => m.RealiseDisponible).Sum(m => m.Realise);
            var ecartYtd = realiseYtd - budgetYtd;
            var ecartYtdPct = budgetYtd > 0 ? Math.Round(ecartYtd * 100m / budgetYtd, 2) : 0m;

            var budgetMoisCourant = moisCourant > 0
                ? mensuel.First(m => m.Mois == moisCourant).Budget
                : 0m;
            var realiseMoisCourant = moisCourant > 0
                ? mensuel.First(m => m.Mois == moisCourant).Realise
                : 0m;
            var ecartMoisCourant = realiseMoisCourant - budgetMoisCourant;
            var ecartMoisCourantPct = budgetMoisCourant > 0
                ? Math.Round(ecartMoisCourant * 100m / budgetMoisCourant, 2)
                : 0m;

            // Projection annuelle = YTD réalisé + budget des mois restants
            var budgetMoisRestants = mensuel.Where(m => m.Mois > moisCourant).Sum(m => m.Budget);
            var projection = realiseYtd + budgetMoisRestants;
            var ecartProjection = projection - totalBudgetAnnuel;
            var ecartProjectionPct = totalBudgetAnnuel > 0
                ? Math.Round(ecartProjection * 100m / totalBudgetAnnuel, 2)
                : 0m;

            int nbRubriquesDepasement = rubriques.Count(r =>
                r.EcartPct > filter.SeuilAlertePct);
            int nbSitesDepassement = sites.Count(s =>
                s.EcartPct > filter.SeuilAlertePct);

            var kpis = new KpiBudgetDto
            {
                BudgetAnnuel = totalBudgetAnnuel,
                ProjectionAnnuelle = projection,
                EcartProjection = ecartProjection,
                EcartProjectionPct = ecartProjectionPct,
                BudgetYtd = budgetYtd,
                RealiseYtd = realiseYtd,
                EcartYtd = ecartYtd,
                EcartYtdPct = ecartYtdPct,
                BudgetMois = budgetMoisCourant,
                RealiseMois = realiseMoisCourant,
                EcartMois = ecartMoisCourant,
                EcartMoisPct = ecartMoisCourantPct,
                NombreRubriquesEnDepassement = nbRubriquesDepasement,
                NombreSitesEnDepassement = nbSitesDepassement
            };

            return new BudgetVsRealiseDto
            {
                Kpis = kpis,
                Mensuel = mensuel,
                CumulYtd = cumulYtd,
                ParRubrique = rubriques,
                ParSite = sites,
                Annee = filter.Annee,
                MoisCourant = moisCourant > 0 ? moisCourant : null,
                CalculatedAt = DateTime.Now
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        private static string LibelleRubrique(BudgetRubrique r) => r switch
        {
            BudgetRubrique.SalairesBase => "Salaires de base",
            BudgetRubrique.Primes => "Primes",
            BudgetRubrique.TreiziemeMois => "13e mois",
            BudgetRubrique.Gratifications => "Gratifications",
            BudgetRubrique.Indemnites => "Indemnités",
            BudgetRubrique.ChargesPatronales => "Charges patronales",
            BudgetRubrique.AvantagesNature => "Avantages en nature",
            BudgetRubrique.Formation => "Formation",
            BudgetRubrique.Recrutement => "Recrutement",
            BudgetRubrique.Autre => "Autre",
            _ => r.ToString()
        };

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
