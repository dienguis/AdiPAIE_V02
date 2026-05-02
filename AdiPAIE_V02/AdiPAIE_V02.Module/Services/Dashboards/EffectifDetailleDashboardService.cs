// =============================================================================
//  EffectifDetailleDashboardService.cs
//  Tableau N°1 (Effectif détaillé) — implémentation XPO du service.
//
//  Pattern :
//   - Service agnostique XAF : prend IObjectSpace en paramètre des méthodes.
//   - L'appelant (Razor page) crée l'IObjectSpace via INonSecuredObjectSpaceFactory
//     (DevExpress.ExpressApp.Blazor.Services).
//   - Requêtes XPO matérialisées en mémoire puis agrégations LINQ.
//   - Cache résultat global (clé = filtres) pendant 5 min, géré ici.
//
//  Sécurité : on attend du caller qu'il fournisse un NonSecuredObjectSpace
//  (les agrégations doivent voir l'intégralité des Salarie). Le périmètre
//  est borné par la permission XAF sur DashboardsRHMenu + le rôle RH_Manager.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;
using Microsoft.Extensions.Caching.Memory;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <inheritdoc cref="IEffectifDetailleDashboardService"/>
    public sealed class EffectifDetailleDashboardService : IEffectifDetailleDashboardService
    {
        private readonly IMemoryCache _cache;

        // TTL du cache mémoire — 5 min par défaut (suffisant pour usage dashboard).
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        public EffectifDetailleDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GetData — orchestration KPIs / Évolution / Tableaux / Bar chart
        // ─────────────────────────────────────────────────────────────────────
        public EffectifDetailleDto GetData(
            EffectifDetailleFilterModel filter,
            IObjectSpace objectSpace)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(objectSpace);

            var cacheKey = filter.ToCacheKey();
            if (_cache.TryGetValue<EffectifDetailleDto>(cacheKey, out var cached) && cached != null)
                return cached;

            var dateRef = filter.ResolveDateReference();

            var salaries = QuerySalariesActifs(objectSpace, dateRef, filter).ToList();

            var dto = new EffectifDetailleDto
            {
                EffectifTotal = salaries.Count,
                DateReference = dateRef,
                CalculatedAt = DateTime.Now,
                Kpis = ComputeKpis(salaries, dateRef),
                EvolutionTroisAns = ComputeEvolution(objectSpace, dateRef, filter),
                TrancheCategorie = ComputeTrancheCategorie(salaries, dateRef),
                BarStackHommesFemmes = ComputeBarStack(salaries, dateRef),
            };

            _cache.Set(cacheKey, dto, CacheTtl);
            return dto;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Requête de base : Salariés actifs à une date avec filtres
        //
        //  Note SQL Server : DateTime.MinValue (01/01/0001) est en dehors de
        //  la plage de SqlDateTime (1753-9999). Si on l'envoie comme
        //  paramètre dans une requête XPO traduite en SQL, on obtient
        //  « SqlDateTime overflow ». Pour contourner :
        //   1) On matérialise la base (DateEmbauche <= dateRef + filtres
        //      indexables Site / Sexe) en SQL pur.
        //   2) On applique le filtre DateSortie en mémoire (LINQ-to-Objects)
        //      qui n'envoie plus rien à SQL — DateTime.MinValue y est OK.
        //   3) On applique aussi le filtre TypeContrat en mémoire (déjà
        //      le cas avant ce fix).
        // ─────────────────────────────────────────────────────────────────────
        private static IEnumerable<Salarie> QuerySalariesActifs(
            IObjectSpace os, DateTime dateRef, EffectifDetailleFilterModel filter)
        {
            // ── Étape 1 : filtres SQL-safe (DateEmbauche, Site, Sexe) ──────
            IEnumerable<Salarie> qSql = os.GetObjectsQuery<Salarie>()
                .Where(s => s.DateEmbauche <= dateRef);

            if (filter.SiteOid.HasValue)
                qSql = qSql.Where(s => s.Site != null && s.Site.Oid == filter.SiteOid.Value);

            if (filter.Genre.HasValue)
                qSql = qSql.Where(s => s.Sexe == filter.Genre.Value);

            // Matérialisation — la suite est en mémoire.
            var materialized = qSql.ToList();

            // ── Étape 2 : filtre DateSortie en mémoire (évite SqlDateTime
            //              overflow sur le sentinelle DateTime.MinValue) ──
            IEnumerable<Salarie> q = materialized.Where(s =>
                s.DateSortie == DateTime.MinValue || s.DateSortie > dateRef);

            // ── Étape 3 : filtre TypeContrat en mémoire (via Salarie.Contrats) ──
            if (filter.TypeContrat.HasValue)
            {
                var typeFiltre = filter.TypeContrat.Value;
                q = q.Where(s => HasContratActifDeType(s, dateRef, typeFiltre));
            }

            return q;
        }

        /// <summary>
        /// Indique si le salarié possède un ContratSalarie actif (en cours)
        /// à la date donnée et de type spécifié.
        /// </summary>
        private static bool HasContratActifDeType(Salarie s, DateTime dateRef, TypeContrat type)
        {
            try
            {
                if (s.Contrats == null || s.Contrats.Count == 0)
                    return false;
                return s.Contrats.Any(c =>
                    c.TypeContrat == type &&
                    c.DateDebut <= dateRef &&
                    (c.DateFin == null || c.DateFin >= dateRef));
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  KPIs : âge moyen + ancienneté moyenne (global / hommes / femmes)
        // ─────────────────────────────────────────────────────────────────────
        private static KpiAgeAncienneteDto ComputeKpis(
            IList<Salarie> salaries, DateTime dateRef)
        {
            if (salaries.Count == 0)
                return new KpiAgeAncienneteDto();

            decimal ageMoyen(IEnumerable<Salarie> set) =>
                Round1(set.Where(s => s.Birthday > DateTime.MinValue)
                          .Select(s => (decimal)CalcAge(s.Birthday, dateRef))
                          .DefaultIfEmpty(0m).Average());

            decimal ancMoyen(IEnumerable<Salarie> set) =>
                Round1(set.Where(s => s.DateEmbauche > DateTime.MinValue)
                          .Select(s => (decimal)CalcAnciennete(s.DateEmbauche, dateRef))
                          .DefaultIfEmpty(0m).Average());

            var hommes = salaries.Where(s => s.Sexe == Sexe.Masculin).ToList();
            var femmes = salaries.Where(s => s.Sexe == Sexe.Feminin).ToList();

            return new KpiAgeAncienneteDto
            {
                AgeMoyenGlobal           = ageMoyen(salaries),
                AgeMoyenHommes           = ageMoyen(hommes),
                AgeMoyenFemmes           = ageMoyen(femmes),
                AncienneteMoyenneGlobal  = ancMoyen(salaries),
                AncienneteMoyenneHommes  = ancMoyen(hommes),
                AncienneteMoyenneFemmes  = ancMoyen(femmes),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Évolution effectif sur 3 ans
        //   - Année en cours : effectif au DateReference (aujourd'hui ou 31/12)
        //   - Années passées : effectif au 31/12 de chaque année
        // ─────────────────────────────────────────────────────────────────────
        private List<EvolutionAnneeDto> ComputeEvolution(
            IObjectSpace os, DateTime dateRef, EffectifDetailleFilterModel filter)
        {
            var anneeRef = dateRef.Year;
            var annees = Enumerable.Range(anneeRef - 2, 3).ToArray();

            var points = new List<EvolutionAnneeDto>();
            foreach (var annee in annees)
            {
                var dateMesure = (annee == DateTime.Today.Year && dateRef >= DateTime.Today)
                    ? DateTime.Today
                    : new DateTime(annee, 12, 31);

                var nb = QuerySalariesActifs(os, dateMesure, filter).Count();
                points.Add(new EvolutionAnneeDto { Annee = annee, Effectif = nb });
            }

            for (int i = 1; i < points.Count; i++)
            {
                var prev = points[i - 1].Effectif;
                if (prev > 0)
                    points[i].VariationPourcent =
                        Round1((decimal)(points[i].Effectif - prev) * 100m / prev);
            }

            return points;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Matrice tranche d'âge × catégorie professionnelle
        // ─────────────────────────────────────────────────────────────────────
        private static Dictionary<AgeBucket, List<TrancheCategorieRowDto>>
            ComputeTrancheCategorie(IList<Salarie> salaries, DateTime dateRef)
        {
            var result = new Dictionary<AgeBucket, List<TrancheCategorieRowDto>>();

            var enriched = salaries
                .Where(s => s.Birthday > DateTime.MinValue)
                .Select(s => new
                {
                    Salarie   = s,
                    Bucket    = AgeBucketHelper.FromAge(CalcAge(s.Birthday, dateRef)),
                    Categorie = s.Categories?.Intitule ?? "(Non renseignée)"
                })
                .ToList();

            foreach (var bucket in AgeBucketHelper.All)
            {
                var dansLaTranche = enriched.Where(e => e.Bucket == bucket).ToList();
                var totalTranche  = dansLaTranche.Count;

                var rows = dansLaTranche
                    .GroupBy(e => e.Categorie)
                    .OrderBy(g => g.Key)
                    .Select(g => new TrancheCategorieRowDto
                    {
                        Categorie  = g.Key,
                        Effectif   = g.Count(),
                        PourcentageDansTranche = totalTranche > 0
                            ? Round1((decimal)g.Count() * 100m / totalTranche)
                            : 0m
                    })
                    .ToList();

                result[bucket] = rows;
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Bar chart horizontal empilé F / H par tranche × catégorie
        // ─────────────────────────────────────────────────────────────────────
        private static List<BarStackPointDto> ComputeBarStack(
            IList<Salarie> salaries, DateTime dateRef)
        {
            return salaries
                .Where(s => s.Birthday > DateTime.MinValue)
                .Select(s => new
                {
                    Bucket    = AgeBucketHelper.FromAge(CalcAge(s.Birthday, dateRef)),
                    Categorie = s.Categories?.Intitule ?? "(Non renseignée)",
                    Sexe      = s.Sexe
                })
                .GroupBy(x => new { x.Bucket, x.Categorie })
                .OrderBy(g => g.Key.Bucket)
                .ThenBy(g => g.Key.Categorie)
                .Select(g => new BarStackPointDto
                {
                    Tranche   = g.Key.Bucket,
                    Categorie = g.Key.Categorie,
                    Hommes    = g.Count(x => x.Sexe == Sexe.Masculin),
                    Femmes    = g.Count(x => x.Sexe == Sexe.Feminin),
                })
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────
        private static int CalcAge(DateTime birthday, DateTime atDate)
        {
            var age = atDate.Year - birthday.Year;
            if (atDate < birthday.AddYears(age)) age--;
            return Math.Max(0, age);
        }

        private static int CalcAnciennete(DateTime dateEmbauche, DateTime atDate)
        {
            var anc = atDate.Year - dateEmbauche.Year;
            if (atDate < dateEmbauche.AddYears(anc)) anc--;
            return Math.Max(0, anc);
        }

        private static decimal Round1(decimal v) => Math.Round(v, 1);

        // ─────────────────────────────────────────────────────────────────────
        //  Lookups pour les filtres UI
        // ─────────────────────────────────────────────────────────────────────
        public List<int> GetAnneesDisponibles(IObjectSpace os)
        {
            const string key = "effectif_detaille_annees";
            if (_cache.TryGetValue<List<int>>(key, out var cached) && cached != null)
                return cached;

            // ── Bornes pragmatiques ─────────────────────────────────────────
            // On essaie d'inférer l'année minimum à partir des DateEmbauche en
            // base, mais on borne entre N-15 et N+0 quoi qu'il arrive. Si la
            // requête min() échoue (DB vide, schéma incomplet), on retombe
            // sur les 6 dernières années par défaut — comme ça l'utilisateur
            // a TOUJOURS un choix non vide.
            int anneeMin;
            int anneeMax = DateTime.Today.Year;
            try
            {
                // On matérialise les DateEmbauche pour calculer le min en
                // mémoire (évite des problèmes de SqlDateTime sur sentinelles).
                var dates = os.GetObjectsQuery<Salarie>()
                    .Select(s => s.DateEmbauche)
                    .ToList()
                    .Where(d => d > new DateTime(1900, 1, 1) && d.Year <= anneeMax)
                    .ToList();

                if (dates.Count == 0)
                    anneeMin = anneeMax - 5;
                else
                    anneeMin = Math.Max(dates.Min().Year, anneeMax - 15);
            }
            catch
            {
                // Fallback : 6 dernières années.
                anneeMin = anneeMax - 5;
            }

            var list = Enumerable.Range(anneeMin, anneeMax - anneeMin + 1)
                .OrderByDescending(a => a)
                .ToList();

            _cache.Set(key, list, TimeSpan.FromHours(1));
            return list;
        }

        public List<Site> GetSitesActifs(IObjectSpace os)
        {
            try
            {
                // Tente d'abord les sites marqués actifs.
                var actifs = os.GetObjectsQuery<Site>()
                    .Where(s => s.Actif)
                    .OrderBy(s => s.Nom)
                    .ToList();

                if (actifs.Count > 0)
                    return actifs;

                // Sinon : tous les sites (utile si Actif n'a jamais été coché
                // après la migration de schéma).
                return os.GetObjectsQuery<Site>()
                    .OrderBy(s => s.Nom)
                    .ToList();
            }
            catch
            {
                // Schéma absent / table vide : liste vide propre.
                return new List<Site>();
            }
        }

        public IReadOnlyList<Sexe> GetGenresDisponibles() =>
            new[] { Sexe.Masculin, Sexe.Feminin };

        public IReadOnlyList<TypeContrat> GetTypesContratDisponibles() =>
            new[] { TypeContrat.CDI, TypeContrat.CDD, TypeContrat.Stage };

        public void InvalidateCache(EffectifDetailleFilterModel filter)
        {
            if (filter == null) return;
            _cache.Remove(filter.ToCacheKey());
        }
    }
}
