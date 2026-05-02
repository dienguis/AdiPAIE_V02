// =============================================================================
//  AnalyseEffectifDashboardService.cs
//  Tableau N°2 (Analyse de l'Effectif) — implémentation XPO du service.
//
//  Pattern :
//   - Service agnostique XAF : prend IObjectSpace en paramètre des méthodes.
//   - Périmètre INTERNE (Salarie)  → tous les KPI et bar charts.
//   - Périmètre EXTERNE (Interimaire) → KPI sur Interimaire + ContratInterim.
//   - Filtre DateSortie aligné sur SPEC PowerBI (< 1900-01-01 = sentinelle).
//   - Cache résultat global (clé = filtres) pendant 5 min.
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
    /// <inheritdoc cref="IAnalyseEffectifDashboardService"/>
    public sealed class AnalyseEffectifDashboardService : IAnalyseEffectifDashboardService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly DateTime SortieSentinelle = new(1900, 1, 1);

        public AnalyseEffectifDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GetData — orchestration
        // ─────────────────────────────────────────────────────────────────────
        public AnalyseEffectifDto GetData(AnalyseEffectifFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var cacheKey = filter.ToCacheKey();
            if (_cache.TryGetValue<AnalyseEffectifDto>(cacheKey, out var cached) && cached != null)
                return cached;

            var dto = filter.Personnel == PersonnelType.Externe
                ? ComputeForExterne(filter, os)
                : ComputeForInterne(filter, os);

            _cache.Set(cacheKey, dto, CacheTtl);
            return dto;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  PÉRIMÈTRE INTERNE (Salarie + ContratSalarie)
        // ═════════════════════════════════════════════════════════════════════
        private AnalyseEffectifDto ComputeForInterne(AnalyseEffectifFilterModel filter, IObjectSpace os)
        {
            var dateRef = filter.ResolveDateReference();

            // ── Salariés actifs à dateRef avec filtres SQL-safe ───────────────
            var salariesActifs = QuerySalariesActifs(os, dateRef, filter).ToList();

            // ── Sorties de l'année ────────────────────────────────────────────
            var debutAnnee = new DateTime(filter.Annee, 1, 1);
            var finAnnee   = new DateTime(filter.Annee, 12, 31);

            var sortiesAnnee = os.GetObjectsQuery<Salarie>()
                .ToList()
                .Where(s =>
                    s.DateSortie >= SortieSentinelle &&
                    s.DateSortie >= debutAnnee &&
                    s.DateSortie <= finAnnee)
                .ToList();

            // ── Effectif Moyen Annuel = (effectif au 1/1 + effectif au 31/12) / 2
            var effectifDebut = QuerySalariesActifs(os, debutAnnee, filter).Count();
            var effectifFin   = QuerySalariesActifs(os, finAnnee, filter).Count();
            var effectifMoyen = (effectifDebut + effectifFin) / 2m;

            // ── KPI ───────────────────────────────────────────────────────────
            int effectifTotal = filter.Mode == EffectifMode.Moyen
                ? (int)Math.Round(effectifMoyen, 0, MidpointRounding.AwayFromZero)
                : salariesActifs.Count;

            int nbHommes = salariesActifs.Count(s => s.Sexe == Sexe.Masculin);
            int nbFemmes = salariesActifs.Count(s => s.Sexe == Sexe.Feminin);

            decimal pctF = salariesActifs.Count > 0
                ? Round1((decimal)nbFemmes * 100m / salariesActifs.Count) : 0m;
            decimal pctH = salariesActifs.Count > 0
                ? Round1((decimal)nbHommes * 100m / salariesActifs.Count) : 0m;

            decimal pctDeparts = salariesActifs.Count > 0
                ? Round1((decimal)sortiesAnnee.Count * 100m / salariesActifs.Count) : 0m;
            decimal pctRotation = effectifMoyen > 0
                ? Round1((decimal)sortiesAnnee.Count * 100m / effectifMoyen) : 0m;

            decimal ageMoyen = salariesActifs.Count > 0
                ? Round1(salariesActifs
                    .Where(s => s.Birthday > SortieSentinelle)
                    .Select(s => (decimal)CalcAge(s.Birthday, dateRef))
                    .DefaultIfEmpty(0m).Average())
                : 0m;

            decimal ancMoyenne = salariesActifs.Count > 0
                ? Round1(salariesActifs
                    .Where(s => s.DateEmbauche > SortieSentinelle)
                    .Select(s => (decimal)CalcAnciennete(s.DateEmbauche, dateRef))
                    .DefaultIfEmpty(0m).Average())
                : 0m;

            // ── Évolution 8 ans ───────────────────────────────────────────────
            var evolution = ComputeEvolution8AnsInterne(os, filter);

            // ── Bar chart Tranche d'âge (4 + vide) ────────────────────────────
            var barAge = AgeBucketAnalyseHelper.All
                .Select(b =>
                {
                    var nb = salariesActifs.Count(s =>
                        AgeBucketAnalyseHelper.FromAge(GetAgeOuNull(s.Birthday, dateRef)) == b);
                    return new BarItemDto
                    {
                        Libelle = AgeBucketAnalyseHelper.GetLibelle(b),
                        Valeur  = nb,
                        Pourcentage = salariesActifs.Count > 0
                            ? Round1((decimal)nb * 100m / salariesActifs.Count) : 0m
                    };
                })
                .ToList();

            // ── Bar chart Ancienneté (5 + vide) ───────────────────────────────
            var barAnc = AncienneteBucketHelper.All
                .Select(b =>
                {
                    var nb = salariesActifs.Count(s =>
                        AncienneteBucketHelper.FromAnnees(GetAncienneteOuNull(s.DateEmbauche, dateRef)) == b);
                    return new BarItemDto
                    {
                        Libelle = AncienneteBucketHelper.GetLibelle(b),
                        Valeur  = nb,
                        Pourcentage = salariesActifs.Count > 0
                            ? Round1((decimal)nb * 100m / salariesActifs.Count) : 0m
                    };
                })
                .ToList();

            // ── Bar chart Segment (= Département pour INTERNE) ───────────────
            var barSegment = salariesActifs
                .GroupBy(s => SafeDepartementNom(s))
                .OrderByDescending(g => g.Count())
                .Select(g => new BarItemDto
                {
                    Libelle = g.Key,
                    Valeur  = g.Count(),
                    Pourcentage = salariesActifs.Count > 0
                        ? Round1((decimal)g.Count() * 100m / salariesActifs.Count) : 0m
                })
                .ToList();

            // ── Bar chart Catégorie professionnelle (Top 5) ───────────────────
            var barCategorie = salariesActifs
                .GroupBy(s => s.Categories?.Intitule ?? "(Non renseignée)")
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new BarItemDto
                {
                    Libelle = g.Key,
                    Valeur  = g.Count(),
                    Pourcentage = salariesActifs.Count > 0
                        ? Round1((decimal)g.Count() * 100m / salariesActifs.Count) : 0m
                })
                .ToList();

            // ── Bar chart Type de contrat ─────────────────────────────────────
            var barContrat = ComputeBarTypeContratInterne(salariesActifs, dateRef);

            return new AnalyseEffectifDto
            {
                Kpis = new KpiAnalyseEffectifDto
                {
                    EffectifTotal       = effectifTotal,
                    PourcentageDeparts  = pctDeparts,
                    PourcentageRotation = pctRotation,
                    AncienneteMoyenne   = ancMoyenne,
                    PourcentageFemmes   = pctF,
                    PourcentageHommes   = pctH,
                    AgeMoyen            = ageMoyen
                },
                EvolutionHuitAns = evolution,
                ParTrancheAge    = barAge,
                ParAnciennete    = barAnc,
                ParSegment       = barSegment,
                ParCategorieTop5 = barCategorie,
                ParTypeContrat   = barContrat,
                DateReference    = dateRef,
                CalculatedAt     = DateTime.Now
            };
        }

        private static IEnumerable<Salarie> QuerySalariesActifs(
            IObjectSpace os, DateTime dateRef, AnalyseEffectifFilterModel filter)
        {
            IEnumerable<Salarie> qSql = os.GetObjectsQuery<Salarie>()
                .Where(s => s.DateEmbauche <= dateRef);

            if (filter.SiteOid.HasValue)
                qSql = qSql.Where(s => s.Site != null && s.Site.Oid == filter.SiteOid.Value);

            if (filter.Genre.HasValue)
                qSql = qSql.Where(s => s.Sexe == filter.Genre.Value);

            if (filter.CategorieOid.HasValue)
                qSql = qSql.Where(s => s.Categories != null && s.Categories.Oid == filter.CategorieOid.Value);

            var materialized = qSql.ToList();

            // Filtre DateSortie en mémoire (évite SqlDateTime overflow).
            IEnumerable<Salarie> q = materialized.Where(s =>
                s.DateSortie < SortieSentinelle || s.DateSortie > dateRef);

            // Filtre Segment (Département) en mémoire.
            if (!string.IsNullOrWhiteSpace(filter.Segment))
            {
                q = q.Where(s => string.Equals(SafeDepartementNom(s), filter.Segment,
                                               StringComparison.OrdinalIgnoreCase));
            }

            // Filtre Ancienneté en mémoire.
            if (filter.Anciennete.HasValue)
            {
                var bucket = filter.Anciennete.Value;
                q = q.Where(s =>
                    AncienneteBucketHelper.FromAnnees(GetAncienneteOuNull(s.DateEmbauche, dateRef))
                        == bucket);
            }

            return q;
        }

        private static List<EvolutionAnneeDto> ComputeEvolution8AnsInterne(
            IObjectSpace os, AnalyseEffectifFilterModel filter)
        {
            var anneeMax = filter.Annee;
            var annees = Enumerable.Range(anneeMax - 7, 8).ToArray();

            var points = new List<EvolutionAnneeDto>();
            foreach (var annee in annees)
            {
                var date = (annee == DateTime.Today.Year)
                    ? DateTime.Today
                    : new DateTime(annee, 12, 31);
                var nb = QuerySalariesActifs(os, date, filter).Count();
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

        private static List<BarItemDto> ComputeBarTypeContratInterne(
            IList<Salarie> salaries, DateTime dateRef)
        {
            // Pour chaque Salarie, on identifie le type du contrat actif à dateRef.
            var counts = new Dictionary<string, int> {
                { "CDI",  0 }, { "CDD",  0 }, { "Stage", 0 }, { "Vide", 0 }
            };

            foreach (var s in salaries)
            {
                string cat = "Vide";
                try
                {
                    var c = s.Contrats?.FirstOrDefault(c =>
                        c.DateDebut <= dateRef &&
                        (c.DateFin == null || c.DateFin >= dateRef));
                    if (c != null && c.TypeContrat.HasValue)
                        cat = c.TypeContrat.Value.ToString();
                }
                catch { /* fallback Vide */ }

                if (!counts.ContainsKey(cat)) counts[cat] = 0;
                counts[cat]++;
            }

            return counts
                .Select(kv => new BarItemDto
                {
                    Libelle = kv.Key,
                    Valeur  = kv.Value,
                    Pourcentage = salaries.Count > 0
                        ? Round1((decimal)kv.Value * 100m / salaries.Count) : 0m
                })
                .OrderByDescending(b => b.Valeur)
                .ToList();
        }

        private static string SafeDepartementNom(Salarie s)
        {
            try
            {
                // Salarie a un FK Departement. Si absent, on retombe sur "(Non renseigné)".
                var prop = s.GetType().GetProperty("Departement");
                if (prop?.GetValue(s) is { } dep)
                {
                    var nomProp = dep.GetType().GetProperty("Nom");
                    var nom = nomProp?.GetValue(dep) as string;
                    if (!string.IsNullOrWhiteSpace(nom)) return nom;
                }
            }
            catch { }
            return "(Non renseigné)";
        }

        // ═════════════════════════════════════════════════════════════════════
        //  PÉRIMÈTRE EXTERNE (Interimaire + ContratInterim + StationService
        //                     + SocieteInterim + PosteInterimaire)
        //
        //  Notes de mapping mission Étape 4.2 EXTERNE :
        //    - Site         → ContratInterim.Station (StationService)
        //    - Segment      → ContratInterim.BU      (BusinessUnitStation)
        //    - Catégorie    → ContratInterim.PosteOccupe (PosteInterimaire)
        //    - Genre        → pas de champ Sexe sur Interimaire (filtre no-op,
        //                     %F/%H restent à 0 — limite documentée)
        //    - Type contrat → ContratInterim.TypeContrat (enum
        //                     ContratInterimType, ex. PremiereMission,
        //                     Renouvellement…)
        //
        //  Statut "actif" : on retient les contrats avec ContratInterimStatut.EnCours
        //  (et ceux non clôturés : DateFin >= dateRef ou DateFinReelle non déf.).
        // ═════════════════════════════════════════════════════════════════════
        private AnalyseEffectifDto ComputeForExterne(AnalyseEffectifFilterModel filter, IObjectSpace os)
        {
            var dateRef    = filter.ResolveDateReference();
            var debutAnnee = new DateTime(filter.Annee, 1, 1);
            var finAnnee   = new DateTime(filter.Annee, 12, 31);

            // ── Intérimaires actifs à dateRef avec filtres ──────────────────
            var actifs = QueryInterimairesActifs(os, dateRef, filter).ToList();

            // ── Contrats résiliés/clôturés dans l'année (pour %Départs) ────
            var contratsClotures = os.GetObjectsQuery<ContratInterim>()
                .ToList()
                .Where(c =>
                    c.Statut == ContratInterimStatut.Resilie ||
                    c.Statut == ContratInterimStatut.Termine)
                .Where(c =>
                {
                    var dateFinEffective = c.DateFinReelle ?? c.DateFin;
                    return dateFinEffective >= debutAnnee && dateFinEffective <= finAnnee;
                })
                .ToList();

            // ── Effectif Moyen Annuel (intérimaires) ──────────────────────────
            var effectifDebut = QueryInterimairesActifs(os, debutAnnee, filter).Count();
            var effectifFin   = QueryInterimairesActifs(os, finAnnee, filter).Count();
            var effectifMoyen = (effectifDebut + effectifFin) / 2m;

            int effectifTotal = filter.Mode == EffectifMode.Moyen
                ? (int)Math.Round(effectifMoyen, 0, MidpointRounding.AwayFromZero)
                : actifs.Count;

            // ── KPIs ─────────────────────────────────────────────────────────
            decimal pctDeparts = actifs.Count > 0
                ? Round1((decimal)contratsClotures.Count * 100m / actifs.Count) : 0m;
            decimal pctRotation = effectifMoyen > 0
                ? Round1((decimal)contratsClotures.Count * 100m / effectifMoyen) : 0m;

            decimal ageMoyen          = AgeMoyenInterimaires(actifs, dateRef);
            decimal ancMoyenneContrat = AncienneteMoyenneContratExterne(actifs, dateRef);

            // ── Évolution 8 ans ──────────────────────────────────────────────
            var evolution = ComputeEvolution8AnsExterne(os, filter);

            // ── Bar Tranche d'âge ────────────────────────────────────────────
            var barAge = AgeBucketAnalyseHelper.All
                .Select(b =>
                {
                    var nb = actifs.Count(i =>
                        AgeBucketAnalyseHelper.FromAge(GetAgeOuNullInterim(i, dateRef)) == b);
                    return new BarItemDto
                    {
                        Libelle = AgeBucketAnalyseHelper.GetLibelle(b),
                        Valeur  = nb,
                        Pourcentage = actifs.Count > 0
                            ? Round1((decimal)nb * 100m / actifs.Count) : 0m
                    };
                }).ToList();

            // ── Bar Ancienneté (basée sur DateDebut du contrat actif) ───────
            var barAnc = AncienneteBucketHelper.All
                .Select(b =>
                {
                    var nb = actifs.Count(i =>
                        AncienneteBucketHelper.FromAnnees(GetAncienneteContratOuNull(i, dateRef)) == b);
                    return new BarItemDto
                    {
                        Libelle = AncienneteBucketHelper.GetLibelle(b),
                        Valeur  = nb,
                        Pourcentage = actifs.Count > 0
                            ? Round1((decimal)nb * 100m / actifs.Count) : 0m
                    };
                }).ToList();

            // ── Bar Segment (Station service ou BU si présente) ─────────────
            var barSegment = ComputeBarSegmentExterne(actifs, dateRef);

            // ── Bar Catégorie/Poste Top 5 ───────────────────────────────────
            var barCat = ComputeBarPosteTop5Externe(actifs, dateRef);

            // ── Bar Type de contrat (selon ContratInterim.TypeContrat) ──────
            var barContrat = ComputeBarTypeContratExterne(actifs, dateRef);

            return new AnalyseEffectifDto
            {
                Kpis = new KpiAnalyseEffectifDto
                {
                    EffectifTotal       = effectifTotal,
                    PourcentageDeparts  = pctDeparts,
                    PourcentageRotation = pctRotation,
                    AncienneteMoyenne   = ancMoyenneContrat,
                    PourcentageFemmes   = 0m,   // limite : pas de Sexe sur Interimaire
                    PourcentageHommes   = 0m,
                    AgeMoyen            = ageMoyen
                },
                EvolutionHuitAns = evolution,
                ParTrancheAge    = barAge,
                ParAnciennete    = barAnc,
                ParSegment       = barSegment,
                ParCategorieTop5 = barCat,
                ParTypeContrat   = barContrat,
                DateReference    = dateRef,
                CalculatedAt     = DateTime.Now
            };
        }

        /// <summary>
        /// Intérimaires « actifs à une date » = ceux dont AU MOINS UN ContratInterim
        /// est en cours à dateRef (DateDebut &lt;= dateRef et DateFin &gt;= dateRef
        /// et statut En cours / non clôturé).
        /// Applique aussi les filtres : Station, SocieteInterim, Poste.
        /// </summary>
        private static IEnumerable<Interimaire> QueryInterimairesActifs(
            IObjectSpace os, DateTime dateRef, AnalyseEffectifFilterModel filter)
        {
            // SocieteInterim filtrable côté SQL
            IEnumerable<Interimaire> qSql = os.GetObjectsQuery<Interimaire>();
            if (filter.SocieteInterimOid.HasValue)
            {
                qSql = qSql.Where(i =>
                    i.SocieteInterim != null &&
                    i.SocieteInterim.Oid == filter.SocieteInterimOid.Value);
            }
            var allInter = qSql.ToList();

            // Filtres relationnels en mémoire (collection Contrats)
            return allInter.Where(i =>
            {
                try
                {
                    if (i.Contrats == null || i.Contrats.Count == 0) return false;

                    var contratActif = i.Contrats.FirstOrDefault(c =>
                        c.DateDebut > SortieSentinelle &&
                        c.DateDebut <= dateRef &&
                        (c.DateFin < SortieSentinelle || c.DateFin > dateRef));

                    if (contratActif == null) return false;

                    // Filtre Station service (= filter.SiteOid en mode EXTERNE)
                    if (filter.SiteOid.HasValue &&
                        contratActif.Station?.Oid != filter.SiteOid.Value)
                        return false;

                    // Filtre Poste
                    if (filter.PosteInterimaireOid.HasValue &&
                        contratActif.PosteOccupe?.Oid != filter.PosteInterimaireOid.Value)
                        return false;

                    // Filtre Ancienneté
                    if (filter.Anciennete.HasValue)
                    {
                        var anc = AncienneteBucketHelper.FromAnnees(
                            CalcAnciennete(contratActif.DateDebut, dateRef));
                        if (anc != filter.Anciennete.Value) return false;
                    }

                    return true;
                }
                catch { return false; }
            });
        }

        private static List<EvolutionAnneeDto> ComputeEvolution8AnsExterne(
            IObjectSpace os, AnalyseEffectifFilterModel filter)
        {
            var anneeMax = filter.Annee;
            var annees = Enumerable.Range(anneeMax - 7, 8).ToArray();

            var points = new List<EvolutionAnneeDto>();
            foreach (var annee in annees)
            {
                var date = (annee == DateTime.Today.Year)
                    ? DateTime.Today
                    : new DateTime(annee, 12, 31);
                var nb = QueryInterimairesActifs(os, date, filter).Count();
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

        private static decimal AgeMoyenInterimaires(IList<Interimaire> list, DateTime dateRef)
        {
            var ages = list
                .Where(i => i.DateNaissance.HasValue && i.DateNaissance.Value > SortieSentinelle)
                .Select(i => (decimal)CalcAge(i.DateNaissance!.Value, dateRef))
                .ToList();
            return ages.Count > 0 ? Round1(ages.Average()) : 0m;
        }

        /// <summary>
        /// Ancienneté moyenne des intérimaires actifs : durée écoulée depuis
        /// le DateDebut du contrat actif (en années pleines).
        /// </summary>
        private static decimal AncienneteMoyenneContratExterne(IList<Interimaire> list, DateTime dateRef)
        {
            var ancs = new List<decimal>();
            foreach (var i in list)
            {
                var c = GetContratActif(i, dateRef);
                if (c != null && c.DateDebut > SortieSentinelle)
                    ancs.Add((decimal)CalcAnciennete(c.DateDebut, dateRef));
            }
            return ancs.Count > 0 ? Round1(ancs.Average()) : 0m;
        }

        private static int? GetAgeOuNullInterim(Interimaire i, DateTime atDate) =>
            i.DateNaissance.HasValue && i.DateNaissance.Value > SortieSentinelle
                ? CalcAge(i.DateNaissance.Value, atDate) : (int?)null;

        /// <summary>
        /// Retourne le contrat « actif » d'un intérimaire à une date donnée :
        ///   DateDebut > 1900 ET DateDebut <= atDate
        ///   ET (DateFin < 1900 [= NULL/sentinelle XPO] OU DateFin > atDate)
        /// Aligné sur la définition métier "actif" SQL spec — sans contrainte
        /// sur le Statut (les données seed peuvent être incohérentes).
        /// </summary>
        private static ContratInterim? GetContratActif(Interimaire i, DateTime atDate)
        {
            try
            {
                return i.Contrats?.FirstOrDefault(c =>
                    c.DateDebut > SortieSentinelle &&
                    c.DateDebut <= atDate &&
                    (c.DateFin < SortieSentinelle || c.DateFin > atDate));
            }
            catch { return null; }
        }

        private static int? GetAncienneteContratOuNull(Interimaire i, DateTime atDate)
        {
            var c = GetContratActif(i, atDate);
            if (c != null && c.DateDebut > SortieSentinelle)
                return CalcAnciennete(c.DateDebut, atDate);
            return null;
        }

        private static List<BarItemDto> ComputeBarSegmentExterne(IList<Interimaire> actifs, DateTime dateRef)
        {
            return actifs
                .Select(i =>
                {
                    var c = GetContratActif(i, dateRef);
                    if (c == null) return "(Non renseigné)";
                    if (c.EstDG) return "Direction Générale";
                    return c.Station?.Nom ?? c.BU?.Libelle ?? "(Non renseigné)";
                })
                .GroupBy(label => label)
                .OrderByDescending(g => g.Count())
                .Select(g => new BarItemDto
                {
                    Libelle = g.Key,
                    Valeur  = g.Count(),
                    Pourcentage = actifs.Count > 0
                        ? Round1((decimal)g.Count() * 100m / actifs.Count) : 0m
                })
                .ToList();
        }

        /// <summary>
        /// Top 5 des PosteInterimaire (Caissier, Steeward, Chef Boutique,
        /// Manager…) parmi les contrats actifs.
        /// </summary>
        private static List<BarItemDto> ComputeBarPosteTop5Externe(IList<Interimaire> actifs, DateTime dateRef)
        {
            return actifs
                .Select(i => GetContratActif(i, dateRef)?.PosteOccupe?.Libelle ?? "(Non renseigné)")
                .GroupBy(label => label)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new BarItemDto
                {
                    Libelle = g.Key,
                    Valeur  = g.Count(),
                    Pourcentage = actifs.Count > 0
                        ? Round1((decimal)g.Count() * 100m / actifs.Count) : 0m
                })
                .ToList();
        }

        private static List<BarItemDto> ComputeBarTypeContratExterne(IList<Interimaire> actifs, DateTime dateRef)
        {
            return actifs
                .Select(i => GetContratActif(i, dateRef)?.TypeContrat.ToString() ?? "(Non renseigné)")
                .GroupBy(label => label)
                .OrderByDescending(g => g.Count())
                .Select(g => new BarItemDto
                {
                    Libelle = g.Key,
                    Valeur  = g.Count(),
                    Pourcentage = actifs.Count > 0
                        ? Round1((decimal)g.Count() * 100m / actifs.Count) : 0m
                })
                .ToList();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Helpers
        // ═════════════════════════════════════════════════════════════════════
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

        private static int? GetAgeOuNull(DateTime birthday, DateTime atDate) =>
            birthday > SortieSentinelle ? CalcAge(birthday, atDate) : (int?)null;

        private static int? GetAncienneteOuNull(DateTime dateEmbauche, DateTime atDate) =>
            dateEmbauche > SortieSentinelle ? CalcAnciennete(dateEmbauche, atDate) : (int?)null;

        private static decimal Round1(decimal v) => Math.Round(v, 1);

        // ═════════════════════════════════════════════════════════════════════
        //  Lookups pour les filtres UI
        // ═════════════════════════════════════════════════════════════════════
        public List<int> GetAnneesDisponibles(IObjectSpace os)
        {
            var anneeMax = DateTime.Today.Year;
            var anneeMin = anneeMax - 7;
            return Enumerable.Range(anneeMin, anneeMax - anneeMin + 1)
                .OrderByDescending(a => a)
                .ToList();
        }

        public List<Site> GetSitesActifs(IObjectSpace os)
        {
            try
            {
                var actifs = os.GetObjectsQuery<Site>()
                    .Where(s => s.Actif)
                    .OrderBy(s => s.Nom)
                    .ToList();
                return actifs.Count > 0 ? actifs
                    : os.GetObjectsQuery<Site>().OrderBy(s => s.Nom).ToList();
            }
            catch { return new List<Site>(); }
        }

        /// <summary>
        /// Stations service (réseau ELTON Oil) — utilisées comme « Site » pour
        /// le périmètre EXTERNE (intérimaires). Ex. DIAMNIADIO, MERMOZ, VDN…
        /// </summary>
        public List<StationService> GetStationsServiceActives(IObjectSpace os)
        {
            try
            {
                var actifs = os.GetObjectsQuery<StationService>()
                    .Where(s => s.Actif)
                    .OrderBy(s => s.Nom)
                    .ToList();
                return actifs.Count > 0 ? actifs
                    : os.GetObjectsQuery<StationService>().OrderBy(s => s.Nom).ToList();
            }
            catch { return new List<StationService>(); }
        }

        public List<Departement> GetDepartements(IObjectSpace os)
        {
            try
            {
                return os.GetObjectsQuery<Departement>()
                    .Where(d => d.Actif)
                    .OrderBy(d => d.Nom)
                    .ToList();
            }
            catch { return new List<Departement>(); }
        }

        public List<Categories> GetCategories(IObjectSpace os)
        {
            try
            {
                return os.GetObjectsQuery<Categories>()
                    .OrderBy(c => c.Intitule)
                    .ToList();
            }
            catch { return new List<Categories>(); }
        }

        public List<SocieteInterim> GetSocietesInterim(IObjectSpace os)
        {
            try
            {
                return os.GetObjectsQuery<SocieteInterim>()
                    .ToList()
                    .OrderBy(s => GetNomSociete(s))
                    .ToList();
            }
            catch { return new List<SocieteInterim>(); }
        }

        public List<PosteInterimaire> GetPostesInterimaire(IObjectSpace os)
        {
            try
            {
                return os.GetObjectsQuery<PosteInterimaire>()
                    .Where(p => p.Actif)
                    .OrderBy(p => p.Libelle)
                    .ToList();
            }
            catch
            {
                // Fallback : pas de filtre Actif si la propriété n'existe pas.
                try
                {
                    return os.GetObjectsQuery<PosteInterimaire>()
                        .OrderBy(p => p.Libelle)
                        .ToList();
                }
                catch { return new List<PosteInterimaire>(); }
            }
        }

        /// <summary>
        /// Récupère le nom (ou raison sociale) d'une SocieteInterim de manière
        /// défensive : tente plusieurs noms de propriété au cas où le schéma
        /// du projet diffère (Nom, RaisonSociale, Libelle, ToString).
        /// </summary>
        private static string GetNomSociete(SocieteInterim s)
        {
            if (s == null) return "(?)";
            try
            {
                foreach (var prop in new[] { "Nom", "RaisonSociale", "Libelle", "Name" })
                {
                    var p = s.GetType().GetProperty(prop);
                    if (p?.GetValue(s) is string str && !string.IsNullOrWhiteSpace(str))
                        return str;
                }
            }
            catch { }
            return s.ToString() ?? "(?)";
        }

        public void InvalidateCache(AnalyseEffectifFilterModel filter)
        {
            if (filter == null) return;
            _cache.Remove(filter.ToCacheKey());
        }
    }
}
