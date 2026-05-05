// =============================================================================
//  CoutReelInterimDashboardService.cs — V1.3 Sprint 1 (mai 2026)
//
//  Calcule le coût réel des intérimaires depuis les BulletinInterim importés
//  des fichiers de facturation des sociétés d'intérim.
//
//  KPIs :
//    - TTC mois cible / TTC YTD
//    - Multiplicateur Brut → TTC (typiquement ×1.8 à ×2.2)
//    - Évolution 12 mois glissants
//    - Top 10 intérimaires les plus coûteux
//    - Décomposition Brut/Charges/Commission/TVA
//    - Comparaison contrat théorique (TauxJournalier × 22) vs réel TTC
//    - Coût par société d'intérim avec taux de commission
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.Interim;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;
using Microsoft.Extensions.Caching.Memory;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <inheritdoc cref="ICoutReelInterimDashboardService"/>
    public sealed class CoutReelInterimDashboardService : ICoutReelInterimDashboardService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly CultureInfo FrCulture = CultureInfo.GetCultureInfo("fr-FR");
        private static readonly DateTime SortieSentinelle = new(1900, 1, 1);
        private const int JoursOuvresParMois = 22;

        public CoutReelInterimDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public CoutReelInterimDto GetData(CoutReelInterimFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var key = filter.ToCacheKey();
            if (_cache.TryGetValue<CoutReelInterimDto>(key, out var cached) && cached != null)
                return cached;

            var dto = Compute(filter, os);
            _cache.Set(key, dto, CacheTtl);
            return dto;
        }

        // ─────────────────────────────────────────────────────────────────────
        private CoutReelInterimDto Compute(CoutReelInterimFilterModel filter, IObjectSpace os)
        {
            // ── 1. Tous les bulletins (matérialisation pour LINQ mémoire) ─
            var allBulletins = os.GetObjectsQuery<BulletinInterim>()
                .ToList()
                .Where(b => IsActif(b))
                .ToList();

            if (filter.SiteOid.HasValue)
            {
                // Interimaire n'a pas de FK Site directe (le Site est sur ContratInterim).
                // On filtre via SiteAffectation (string snapshot du fichier Excel)
                // en comparant au Nom du Site sélectionné.
                var siteRef = os.GetObjectByKey<Site>(filter.SiteOid.Value);
                var siteNom = siteRef?.Nom?.Trim().ToUpperInvariant() ?? "";
                if (!string.IsNullOrEmpty(siteNom))
                {
                    allBulletins = allBulletins
                        .Where(b => (b.SiteAffectation ?? "").Trim().ToUpperInvariant() == siteNom)
                        .ToList();
                }
            }
            if (filter.SocieteInterimOid.HasValue)
            {
                allBulletins = allBulletins
                    .Where(b => b.SocieteEmettrice?.Oid == filter.SocieteInterimOid.Value)
                    .ToList();
            }

            // ── 2. Bulletins du mois cible (si filtre mois) et de l'année ─
            int moisCible = filter.Mois ?? DateTime.Today.Month;
            var bulletinsMois = allBulletins
                .Where(b => b.Annee == filter.Annee && b.Mois == moisCible)
                .ToList();
            var bulletinsAnnee = allBulletins
                .Where(b => b.Annee == filter.Annee && b.Mois <= moisCible)
                .ToList();

            // ── 3. KPIs synthèse ──────────────────────────────────────────
            decimal ttcMois = bulletinsMois.Sum(b => b.TTC);
            decimal brutMois = bulletinsMois.Sum(b => b.BrutImposable);
            decimal deboursMois = bulletinsMois.Sum(b => b.Debours);
            decimal commMois = bulletinsMois.Sum(b => b.CommissionAgence);
            decimal htMois = bulletinsMois.Sum(b => b.MontantHT);
            decimal tvaMois = bulletinsMois.Sum(b => b.TVA);

            decimal ttcYtd = bulletinsAnnee.Sum(b => b.TTC);
            decimal brutYtd = bulletinsAnnee.Sum(b => b.BrutImposable);

            // Variation vs mois précédent
            int moisPrec = moisCible - 1;
            int anneePrec = filter.Annee;
            if (moisPrec < 1) { moisPrec = 12; anneePrec = filter.Annee - 1; }
            decimal ttcMoisPrec = allBulletins
                .Where(b => b.Annee == anneePrec && b.Mois == moisPrec)
                .Sum(b => b.TTC);
            decimal varTTC = ttcMois - ttcMoisPrec;
            decimal varTTCPct = ttcMoisPrec > 0 ? Math.Round(varTTC * 100m / ttcMoisPrec, 2) : 0m;

            var kpis = new KpiCoutReelDto
            {
                TTCMois = ttcMois,
                BrutMois = brutMois,
                MultiplicateurMois = brutMois > 0 ? Math.Round(ttcMois / brutMois, 2) : 0m,
                NbBulletinsMois = bulletinsMois.Count,
                CoutMoyenInterimaire = bulletinsMois.Count > 0
                    ? Math.Round(ttcMois / bulletinsMois.Count, 0) : 0m,
                TTCAnneeYTD = ttcYtd,
                BrutAnneeYTD = brutYtd,
                VariationTTCMoisPrec = varTTC,
                VariationTTCMoisPrecPct = varTTCPct,
                TotalDeboursMois = deboursMois,
                TotalCommissionMois = commMois,
                TotalTVAMois = tvaMois,
                TauxCommissionMoyenPct = htMois > 0 ? Math.Round(commMois * 100m / htMois, 2) : 0m
            };

            // ── 4. Décomposition (donut) ──────────────────────────────────
            //    Le débours contient déjà brut + charges patronales + indemnités
            //    Pour un visuel clair, on décompose en :
            //      Brut imposable / Reste du débours (charges + indemnités) / Commission / TVA
            decimal resteDebours = Math.Max(0m, deboursMois - brutMois);
            var decomposition = new List<DecompositionFacturationDto>
            {
                new() { Composante = "Brut imposable",                Montant = brutMois,    CouleurHex = "#1A73B5" },
                new() { Composante = "Charges + indemnités sociétés", Montant = resteDebours, CouleurHex = "#5C9CC9" },
                new() { Composante = "Commission agence",             Montant = commMois,     CouleurHex = "#F18A1C" },
                new() { Composante = "TVA",                            Montant = tvaMois,      CouleurHex = "#7B5EA7" }
            };
            decimal sumDecomp = decomposition.Sum(d => d.Montant);
            foreach (var d in decomposition)
                d.PourcentageDuTotal = sumDecomp > 0 ? Math.Round(d.Montant * 100m / sumDecomp, 2) : 0m;

            // ── 5. Top 10 intérimaires du mois ────────────────────────────
            var top10 = bulletinsMois
                .OrderByDescending(b => b.TTC)
                .Take(10)
                .Select(b => new CoutInterimaireDto
                {
                    InterimaireOid = b.Interimaire?.Oid,
                    Matricule = b.MatriculeOriginal,
                    NomComplet = b.Interimaire?.FullName ?? b.NomComplet,
                    Fonction = b.Fonction,
                    Site = b.SiteAffectation,
                    SocieteInterim = b.SocieteEmettrice?.RaisonSociale ?? "",
                    IsPrestataire = b.Statut == BulletinInterimStatut.Prestataire,
                    Trentieme = b.Trentieme,
                    BrutImposable = b.BrutImposable,
                    NetAPayer = b.NetAPayer,
                    TTC = b.TTC,
                    Multiplicateur = b.BrutImposable > 0
                        ? Math.Round(b.TTC / b.BrutImposable, 2) : 0m
                })
                .ToList();

            // ── 6. Évolution sur 12 mois glissants (jusqu'au mois cible) ──
            var evolution = new List<EvolutionTTCDto>(12);
            for (int offset = 11; offset >= 0; offset--)
            {
                var d = new DateTime(filter.Annee, moisCible, 1).AddMonths(-offset);
                int an = d.Year;
                int mo = d.Month;
                var pts = allBulletins.Where(b => b.Annee == an && b.Mois == mo).ToList();
                evolution.Add(new EvolutionTTCDto
                {
                    Annee = an,
                    Mois = mo,
                    Libelle = $"{FrCulture.DateTimeFormat.GetAbbreviatedMonthName(mo)} {an}",
                    TTC = pts.Sum(b => b.TTC),
                    Brut = pts.Sum(b => b.BrutImposable),
                    NbBulletins = pts.Count
                });
            }

            // ── 7. Comparaison contrat théorique vs réel (du mois cible) ─
            //   Coût théorique = TauxJournalier × 22 jours (1 mois)
            //   On lookup le contrat actif de l'intérimaire à la date du mois.
            var contrats = os.GetObjectsQuery<ContratInterim>()
                .ToList()
                .Where(c => IsActif(c))
                .ToList();

            var dateRefMois = new DateTime(filter.Annee, moisCible, 15); // milieu de mois
            var ecartContratReel = bulletinsMois
                .Where(b => b.Interimaire != null) // Prestataires exclus (pas de contrat)
                .GroupBy(b => b.Interimaire!.Oid)
                .Select(g =>
                {
                    var first = g.First();
                    decimal ttc = g.Sum(x => x.TTC);
                    var contratActif = contrats.FirstOrDefault(c =>
                        c.Interimaire?.Oid == g.Key
                        && c.DateDebut <= dateRefMois
                        && (c.DateFin == default
                            || c.DateFin == SortieSentinelle
                            || c.DateFin >= dateRefMois));

                    decimal coutTheorique = 0m;
                    bool contratIntrouvable = contratActif == null;
                    if (contratActif != null)
                    {
                        coutTheorique = contratActif.TauxJournalier * JoursOuvresParMois;
                    }
                    decimal ecart = ttc - coutTheorique;
                    decimal ecartPct = coutTheorique > 0
                        ? Math.Round(ecart * 100m / coutTheorique, 2) : 0m;

                    return new EcartContratReelDto
                    {
                        InterimaireOid = g.Key,
                        Matricule = first.MatriculeOriginal,
                        NomComplet = first.Interimaire?.FullName ?? first.NomComplet,
                        CoutContratTheorique = coutTheorique,
                        CoutReelTTC = ttc,
                        Ecart = ecart,
                        EcartPct = ecartPct,
                        ContratIntrouvable = contratIntrouvable
                    };
                })
                .OrderByDescending(e => Math.Abs(e.Ecart))
                .ToList();

            // ── 8. Coût par société d'intérim (du mois cible) ────────────
            var parSociete = bulletinsMois
                .GroupBy(b => b.SocieteEmettrice?.Oid)
                .Where(g => g.Key != null)
                .Select(g =>
                {
                    var first = g.First();
                    decimal htSoc = g.Sum(x => x.MontantHT);
                    decimal commSoc = g.Sum(x => x.CommissionAgence);
                    return new CoutSocieteDto
                    {
                        SocieteOid = g.Key,
                        SocieteNom = first.SocieteEmettrice?.RaisonSociale ?? "?",
                        NbBulletins = g.Count(),
                        TotalDebours = g.Sum(x => x.Debours),
                        TotalCommission = commSoc,
                        TotalTTC = g.Sum(x => x.TTC),
                        TauxCommissionPct = htSoc > 0
                            ? Math.Round(commSoc * 100m / htSoc, 2) : 0m
                    };
                })
                .OrderByDescending(s => s.TotalTTC)
                .ToList();

            return new CoutReelInterimDto
            {
                Kpis = kpis,
                Decomposition = decomposition,
                Top10 = top10,
                Evolution = evolution,
                EcartContratReel = ecartContratReel,
                ParSociete = parSociete,
                Annee = filter.Annee,
                Mois = filter.Mois,
                CalculatedAt = DateTime.Now
            };
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
