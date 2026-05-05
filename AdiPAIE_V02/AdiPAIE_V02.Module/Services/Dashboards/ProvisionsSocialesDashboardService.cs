// =============================================================================
//  ProvisionsSocialesDashboardService.cs — V1.2 (mai 2026)
//
//  Calcule la provision IDR selon le barème CCI Sénégal :
//    < 5 ans   : 25 % du salaire mensuel × ancienneté en années
//    5-10 ans  : 30 % du salaire mensuel × ancienneté en années
//    > 10 ans  : 40 % du salaire mensuel × ancienneté en années
//
//  Calcule la provision Congés Payés acquis non pris :
//    Solde acquis × (Salaire mensuel / 22 jours)
//
//  ⚠️ Le solde de congés acquis nécessite l'entité SoldeConge (ou équivalent).
//  En l'absence d'entité dédiée dans la V1.2 initiale, le calcul des congés
//  est laissé à 0 (à compléter en V1.2.1 quand le module Congés sera couvert).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;
using Microsoft.Extensions.Caching.Memory;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <inheritdoc cref="IProvisionsSocialesDashboardService"/>
    public sealed class ProvisionsSocialesDashboardService : IProvisionsSocialesDashboardService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly CultureInfo FrCulture = CultureInfo.GetCultureInfo("fr-FR");
        private static readonly DateTime SortieSentinelle = new(1900, 1, 1);
        private const decimal JoursOuvresParMois = 22m;

        public ProvisionsSocialesDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public ProvisionsSocialesDto GetData(ProvisionsSocialesFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var key = filter.ToCacheKey();
            if (_cache.TryGetValue<ProvisionsSocialesDto>(key, out var cached) && cached != null)
                return cached;

            var dto = Compute(filter, os);
            _cache.Set(key, dto, CacheTtl);
            return dto;
        }

        // ─────────────────────────────────────────────────────────────────────
        private ProvisionsSocialesDto Compute(ProvisionsSocialesFilterModel filter, IObjectSpace os)
        {
            var dateRef = filter.DateReference;

            // ── 1. Salariés actifs à la date de référence ─────────────────
            var salaries = os.GetObjectsQuery<Salarie>()
                .ToList()
                .Where(s => IsActif(s)
                         && s.DateEmbauche != default
                         && s.DateEmbauche <= dateRef
                         && (s.DateSortie == default || s.DateSortie == SortieSentinelle || s.DateSortie > dateRef))
                .ToList();

            if (filter.SiteOid.HasValue)
                salaries = salaries.Where(s => s.Site?.Oid == filter.SiteOid.Value).ToList();

            // ── 2. Calcul provision IDR par salarié ───────────────────────
            var details = new List<ProvisionSalarieDto>(salaries.Count);
            foreach (var s in salaries)
            {
                decimal anciennete = ComputeAncienneteAnnees(s.DateEmbauche, dateRef);
                decimal salaireRef = ComputeSalaireMensuelMoyen(os, s, dateRef);
                decimal taux = ComputeTauxIDR(anciennete);
                decimal provIDR = Math.Round(salaireRef * taux * anciennete, 0);

                // Congés payés : à défaut d'entité SoldeConge présente dans
                // la V1.2 initiale, on estime le solde théorique acquis
                // (2.5 jours / mois travaillé, plafonné à 30 jours).
                decimal soldeCongeEstime = Math.Min(30m, anciennete * 30m % 30m + 15m);
                decimal provCP = Math.Round(soldeCongeEstime * (salaireRef / JoursOuvresParMois), 0);

                details.Add(new ProvisionSalarieDto
                {
                    SalarieOid = s.Oid,
                    Matricule = s.Matricule ?? "",
                    NomComplet = s.FullName ?? "",
                    DateEmbauche = s.DateEmbauche,
                    AncienneteAnnees = Math.Round(anciennete, 2),
                    SalaireMensuelReference = salaireRef,
                    TauxIDR = taux,
                    ProvisionIDR = provIDR,
                    SoldeCongesAcquis = soldeCongeEstime,
                    ProvisionCongesPayes = provCP
                });
            }

            // ── 3. Top 10 ─────────────────────────────────────────────────
            var top10 = details.OrderByDescending(d => d.ProvisionIDR).Take(10).ToList();

            // ── 4. Ventilation par tranche d'ancienneté ───────────────────
            var trancheDef = new (string Lib, decimal Min, decimal Max, decimal Taux)[]
            {
                ("< 5 ans",   0m,    5m,    0.25m),
                ("5-10 ans",  5m,    10m,   0.30m),
                ("> 10 ans",  10m,   999m,  0.40m)
            };
            var parTranche = trancheDef.Select(t =>
            {
                var sub = details.Where(d => d.AncienneteAnnees >= t.Min && d.AncienneteAnnees < t.Max).ToList();
                return new TrancheAncienneteProvisionDto
                {
                    Tranche = t.Lib,
                    NombreSalaries = sub.Count,
                    TauxApplique = t.Taux,
                    ProvisionIDR = sub.Sum(d => d.ProvisionIDR),
                    ProvisionMoyenne = sub.Count > 0 ? Math.Round(sub.Average(d => d.ProvisionIDR), 0) : 0m
                };
            }).ToList();

            // ── 5. Évolution mensuelle 12 mois glissants ──────────────────
            var evolution = new List<EvolutionProvisionDto>(12);
            for (int i = 11; i >= 0; i--)
            {
                var d = new DateTime(dateRef.Year, dateRef.Month, 1).AddMonths(-i);
                // Approximation : on applique la même méthodo à la date du mois
                decimal sumIDR = 0m;
                decimal sumCP = 0m;
                foreach (var s in salaries)
                {
                    if (s.DateEmbauche > d) continue;
                    decimal anc = ComputeAncienneteAnnees(s.DateEmbauche, d);
                    decimal salRef = ComputeSalaireMensuelMoyen(os, s, d);
                    decimal taux = ComputeTauxIDR(anc);
                    sumIDR += Math.Round(salRef * taux * anc, 0);
                    sumCP += Math.Round(15m * (salRef / JoursOuvresParMois), 0);
                }
                evolution.Add(new EvolutionProvisionDto
                {
                    Annee = d.Year,
                    Mois = d.Month,
                    Libelle = $"{FrCulture.DateTimeFormat.GetAbbreviatedMonthName(d.Month)} {d.Year}",
                    ProvisionIDR = sumIDR,
                    ProvisionCongesPayes = sumCP
                });
            }

            // ── 6. KPIs ───────────────────────────────────────────────────
            decimal totalIDR = details.Sum(d => d.ProvisionIDR);
            decimal totalCP = details.Sum(d => d.ProvisionCongesPayes);
            decimal totalJours = details.Sum(d => d.SoldeCongesAcquis);

            // Variation vs mois précédent
            decimal variation = 0m;
            decimal variationPct = 0m;
            if (evolution.Count >= 2)
            {
                var current = evolution[^1];
                var previous = evolution[^2];
                variation = (current.ProvisionIDR + current.ProvisionCongesPayes)
                          - (previous.ProvisionIDR + previous.ProvisionCongesPayes);
                var prevTotal = previous.ProvisionIDR + previous.ProvisionCongesPayes;
                variationPct = prevTotal > 0 ? Math.Round(variation * 100m / prevTotal, 2) : 0m;
            }

            var kpis = new KpiProvisionsDto
            {
                ProvisionIDRTotale = totalIDR,
                NombreSalariesConcernes = details.Count(d => d.ProvisionIDR > 0),
                ProvisionIDRMoyenneParSalarie = details.Count > 0
                    ? Math.Round(totalIDR / details.Count, 0) : 0m,
                ProvisionCongesPayesTotale = totalCP,
                NombreJoursCongesAcquis = Math.Round(totalJours, 1),
                ProvisionTotale = totalIDR + totalCP,
                VariationMois = variation,
                VariationMoisPct = variationPct
            };

            return new ProvisionsSocialesDto
            {
                Kpis = kpis,
                TopProvisionsIDR = top10,
                Details = details.OrderByDescending(d => d.ProvisionIDR).ToList(),
                EvolutionIDR = evolution,
                ParTrancheAnciennete = parTranche,
                DateReference = dateRef,
                CalculatedAt = DateTime.Now
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        private static decimal ComputeAncienneteAnnees(DateTime dateEmbauche, DateTime dateRef)
        {
            if (dateEmbauche == default || dateEmbauche > dateRef) return 0m;
            var ts = dateRef - dateEmbauche;
            return (decimal)Math.Max(0, ts.TotalDays / 365.25);
        }

        private static decimal ComputeTauxIDR(decimal ancienneteAnnees)
        {
            if (ancienneteAnnees < 5m) return 0.25m;
            if (ancienneteAnnees < 10m) return 0.30m;
            return 0.40m;
        }

        /// <summary>
        /// Salaire mensuel de référence = moyenne des BrutFiscal des 12 derniers mois.
        /// Si aucun bulletin disponible, retourne 0.
        /// </summary>
        private static decimal ComputeSalaireMensuelMoyen(IObjectSpace os, Salarie s, DateTime dateRef)
        {
            try
            {
                var dateMin = dateRef.AddMonths(-12);
                var bulletins = os.GetObjectsQuery<Bulletin>()
                    .ToList()
                    .Where(b => b.Salarie?.Oid == s.Oid
                             && IsActif(b)
                             && new DateTime(b.Annee, b.Mois, 1) >= new DateTime(dateMin.Year, dateMin.Month, 1)
                             && new DateTime(b.Annee, b.Mois, 1) <= new DateTime(dateRef.Year, dateRef.Month, 1))
                    .ToList();

                if (bulletins.Count == 0) return 0m;
                return Math.Round(bulletins.Average(b => b.BrutFiscal), 0);
            }
            catch
            {
                return 0m;
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
