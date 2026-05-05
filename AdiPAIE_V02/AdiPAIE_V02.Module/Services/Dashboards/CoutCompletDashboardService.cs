// =============================================================================
//  CoutCompletDashboardService.cs — V1.2 Sprint 4 (mai 2026)
//
//  Calcule le coût complet (Fully Loaded Cost) par salarié sur une année.
//  Décomposition :
//    - Salaire brut (Bulletin.BrutFiscal)
//    - Salaire net  (Bulletin.NetAPayer)
//    - Cotisations salariales = Brut - Net
//    - Charges patronales (BulletinLigne.MontantEmployeur)
//    - Avantages en nature (BulletinLigne famille AV_NATURE_*)
//    - Formation (à défaut d'entité Formation.CoutTotal mappée, valeur 0
//      pour V1.2 initiale — sera complété en V1.2.1)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;
using Microsoft.Extensions.Caching.Memory;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <inheritdoc cref="ICoutCompletDashboardService"/>
    public sealed class CoutCompletDashboardService : ICoutCompletDashboardService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        public CoutCompletDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public CoutCompletDto GetData(CoutCompletFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var key = filter.ToCacheKey();
            if (_cache.TryGetValue<CoutCompletDto>(key, out var cached) && cached != null)
                return cached;

            var dto = Compute(filter, os);
            _cache.Set(key, dto, CacheTtl);
            return dto;
        }

        // ─────────────────────────────────────────────────────────────────────
        private CoutCompletDto Compute(CoutCompletFilterModel filter, IObjectSpace os)
        {
            // ── 1. Bulletins de l'année ───────────────────────────────────
            var bulletins = os.GetObjectsQuery<Bulletin>()
                .ToList()
                .Where(b => b.Annee == filter.Annee && IsActif(b))
                .ToList();

            if (filter.SiteOid.HasValue)
                bulletins = bulletins.Where(b => b.Salarie?.Site?.Oid == filter.SiteOid.Value).ToList();
            if (filter.CategorieOid.HasValue)
                bulletins = bulletins.Where(b => b.Salarie?.Categories?.Oid == filter.CategorieOid.Value).ToList();

            // ── 2. Charges patronales par bulletin ────────────────────────
            var bulletinOids = bulletins.Select(b => b.Oid).ToHashSet();
            var chargesParBulletin = new Dictionary<Guid, decimal>();
            var avantagesParBulletin = new Dictionary<Guid, decimal>();

            try
            {
                var lignes = os.GetObjectsQuery<BulletinLigne>()
                    .ToList()
                    .Where(bl => bl.Bulletin != null
                              && IsActif(bl)
                              && IsActif(bl.Bulletin)
                              && bulletinOids.Contains(bl.Bulletin.Oid))
                    .ToList();

                chargesParBulletin = lignes
                    .GroupBy(bl => bl.Bulletin!.Oid)
                    .ToDictionary(g => g.Key, g => g.Sum(bl => bl.MontantEmployeur));

                // Avantages nature : on essaie de détecter via le code de la rubrique
                avantagesParBulletin = lignes
                    .Where(bl => IsAvantageNature(bl))
                    .GroupBy(bl => bl.Bulletin!.Oid)
                    .ToDictionary(g => g.Key, g => g.Sum(bl => bl.Montant));
            }
            catch
            {
                // ignore — on continue avec valeurs nulles
            }

            // ── 3. Agrégation par salarié ─────────────────────────────────
            var salaries = bulletins
                .Where(b => b.Salarie != null)
                .GroupBy(b => b.Salarie!.Oid)
                .Select(g =>
                {
                    var sal = g.First().Salarie!;
                    decimal brut = g.Sum(b => b.BrutFiscal);
                    decimal net = g.Sum(b => b.NetAPayer);
                    decimal charges = g.Sum(b => chargesParBulletin.GetValueOrDefault(b.Oid));
                    decimal avantages = g.Sum(b => avantagesParBulletin.GetValueOrDefault(b.Oid));
                    decimal cotisations = Math.Max(0m, brut - net);

                    return new CoutSalarieDto
                    {
                        SalarieOid = sal.Oid,
                        Matricule = sal.Matricule ?? "",
                        NomComplet = sal.FullName ?? "",
                        Categorie = SafeCategorie(sal),
                        Site = sal.Site?.Nom ?? "(non assigné)",
                        SalaireBrutAnnuel = brut,
                        SalaireNetAnnuel = net,
                        CotisationsSalariales = cotisations,
                        ChargesPatronales = charges,
                        AvantagesNature = avantages,
                        Formation = 0m   // TODO V1.2.1
                    };
                })
                .OrderByDescending(s => s.CoutTotalAnnuel)
                .ToList();

            // ── 4. KPIs globaux ───────────────────────────────────────────
            decimal totalBrut = salaries.Sum(s => s.SalaireBrutAnnuel);
            decimal totalNet = salaries.Sum(s => s.SalaireNetAnnuel);
            decimal totalCharges = salaries.Sum(s => s.ChargesPatronales);
            decimal totalAvantages = salaries.Sum(s => s.AvantagesNature);
            decimal totalFormation = salaries.Sum(s => s.Formation);
            decimal totalCout = salaries.Sum(s => s.CoutTotalAnnuel);

            var kpis = new KpiCoutCompletDto
            {
                CoutTotalAnnuel = totalCout,
                CoutMoyenSalarie = salaries.Count > 0 ? Math.Round(totalCout / salaries.Count, 0) : 0m,
                SalaireBrutTotal = totalBrut,
                ChargesPatronalesTotal = totalCharges,
                AvantagesNatureTotal = totalAvantages,
                FormationTotal = totalFormation,
                NbSalaries = salaries.Count,
                MultiplicateurNetVersTotal = totalNet > 0 ? Math.Round(totalCout / totalNet, 2) : 0m,
                TauxChargesPatronalesPct = totalBrut > 0 ? Math.Round(totalCharges * 100m / totalBrut, 2) : 0m
            };

            // ── 5. Décomposition (pour donut) ─────────────────────────────
            decimal totalCotisations = salaries.Sum(s => s.CotisationsSalariales);
            var decomposition = new List<DecompositionCoutDto>
            {
                new() { Composante = "Salaire net", Montant = totalNet, CouleurHex = "#1A73B5" },
                new() { Composante = "Cotisations salariales", Montant = totalCotisations, CouleurHex = "#5C9CC9" },
                new() { Composante = "Charges patronales", Montant = totalCharges, CouleurHex = "#F18A1C" },
                new() { Composante = "Avantages nature", Montant = totalAvantages, CouleurHex = "#7B5EA7" },
                new() { Composante = "Formation", Montant = totalFormation, CouleurHex = "#2EA75B" }
            };
            decimal sum = decomposition.Sum(d => d.Montant);
            foreach (var d in decomposition)
                d.PourcentageDuTotal = sum > 0 ? Math.Round(d.Montant * 100m / sum, 2) : 0m;

            // ── 6. Par catégorie ──────────────────────────────────────────
            var parCategorie = salaries
                .GroupBy(s => s.Categorie)
                .Select(g => new CoutCategorieDto
                {
                    Categorie = g.Key,
                    NbSalaries = g.Count(),
                    CoutTotal = g.Sum(x => x.CoutTotalAnnuel),
                    CoutMoyen = Math.Round(g.Average(x => x.CoutTotalAnnuel), 0),
                    SalaireBrutMoyen = Math.Round(g.Average(x => x.SalaireBrutAnnuel), 0)
                })
                .OrderByDescending(c => c.CoutMoyen)
                .ToList();

            // ── 7. Par site ───────────────────────────────────────────────
            var parSite = salaries
                .GroupBy(s => s.Site)
                .Select(g => new CoutSiteDto
                {
                    SiteOid = null,
                    SiteNom = g.Key,
                    NbSalaries = g.Count(),
                    CoutTotal = g.Sum(x => x.CoutTotalAnnuel),
                    CoutMoyen = Math.Round(g.Average(x => x.CoutTotalAnnuel), 0)
                })
                .OrderByDescending(s => s.CoutTotal)
                .ToList();

            return new CoutCompletDto
            {
                Kpis = kpis,
                Salaries = salaries,
                Decomposition = decomposition,
                ParCategorie = parCategorie,
                ParSite = parSite,
                Annee = filter.Annee,
                CalculatedAt = DateTime.Now
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        private static string SafeCategorie(Salarie s)
        {
            try { return s.Categories?.Intitule ?? "(non renseignée)"; }
            catch { return "(non renseignée)"; }
        }

        /// <summary>
        /// Détecte si une ligne de bulletin correspond à un avantage en nature
        /// via le code RubriqueTypeRef (AV_NATURE_*).
        /// La nav property côté Rubrique s'appelle <c>TypeRef</c>
        /// (cf. Rubrique.cs ligne 80, association "TypeRef-Rubriques").
        /// </summary>
        private static bool IsAvantageNature(BulletinLigne bl)
        {
            try
            {
                var typeCode = bl.Rubrique?.TypeRef?.Code ?? "";
                return typeCode.StartsWith("AV_NATURE", StringComparison.OrdinalIgnoreCase)
                    || typeCode.StartsWith("AVNAT", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

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
