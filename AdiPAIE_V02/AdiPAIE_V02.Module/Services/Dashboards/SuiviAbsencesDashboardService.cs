// =============================================================================
//  SuiviAbsencesDashboardService.cs
//  Tableau N°5 (Suivi des Absences) — implémentation XPO, INTERNE uniquement.
//
//  Source : entité `CongeDemande`
//    - filtré par défaut sur Statut == Accordee (20) (vue « absences prises »)
//    - intersection avec l'année (DateDebut..DateFin recouvre [01/01..31/12])
//    - répartition par CongeType.Famille (Annuel / Maladie / etc.)
//    - jours = somme des `DureeJours` (jours ouvrables comptés) au prorata
//      du recouvrement avec l'année cible
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;
using Microsoft.Extensions.Caching.Memory;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <inheritdoc cref="ISuiviAbsencesDashboardService"/>
    public sealed class SuiviAbsencesDashboardService : ISuiviAbsencesDashboardService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        // Convention métier : 22 jours ouvrables / mois × 12 = 264 j ouvrables / an.
        private const decimal JoursOuvresAnnee = 264m;

        public SuiviAbsencesDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        // ─────────────────────────────────────────────────────────────────────
        public SuiviAbsencesDto GetData(SuiviAbsencesFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var key = filter.ToCacheKey();
            if (_cache.TryGetValue<SuiviAbsencesDto>(key, out var cached) && cached != null)
                return cached;

            var dto = Compute(filter, os);
            _cache.Set(key, dto, CacheTtl);
            return dto;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Calcul principal
        // ═════════════════════════════════════════════════════════════════════
        private static SuiviAbsencesDto Compute(SuiviAbsencesFilterModel filter, IObjectSpace os)
        {
            var debutAnnee = new DateTime(filter.Annee,  1,  1);
            var finAnnee   = new DateTime(filter.Annee, 12, 31);
            var fr         = CultureInfo.GetCultureInfo("fr-FR");

            // ── 1. Récupération brute des CongeDemande ─────────────────
            var allDemandes = os.GetObjectsQuery<CongeDemande>().ToList();

            // Filtre statut (Accordee par défaut, sinon Soumise + Accordee + EnAttente)
            allDemandes = filter.InclureEnAttente
                ? allDemandes.Where(c => c.Statut == CongeStatut.EnAttenteN1
                                      || c.Statut == CongeStatut.EnAttenteN2
                                      || c.Statut == CongeStatut.Soumise
                                      || c.Statut == CongeStatut.Accordee).ToList()
                : allDemandes.Where(c => c.Statut == CongeStatut.Accordee).ToList();

            // Filtre intersection avec l'année
            allDemandes = allDemandes
                .Where(c => c.DateDebut <= finAnnee && c.DateFin >= debutAnnee)
                .ToList();

            // Filtres facultatifs via Salarie / CongeType
            if (filter.SiteOid.HasValue)
                allDemandes = allDemandes.Where(c => c.Salarie?.Site?.Oid == filter.SiteOid.Value).ToList();
            if (filter.Genre.HasValue)
                allDemandes = allDemandes.Where(c => c.Salarie?.Sexe == filter.Genre.Value).ToList();
            if (filter.CategorieOid.HasValue)
                allDemandes = allDemandes.Where(c => c.Salarie?.Categories?.Oid == filter.CategorieOid.Value).ToList();
            if (!string.IsNullOrWhiteSpace(filter.Segment))
                allDemandes = allDemandes.Where(c => SafeDepartementNom(c.Salarie) == filter.Segment).ToList();
            if (filter.Famille.HasValue)
                allDemandes = allDemandes.Where(c => c.Type?.Famille == filter.Famille.Value).ToList();

            // ── 2. KPI globaux ──────────────────────────────────────────
            int nbAbsences = allDemandes.Count;
            decimal totalJours = allDemandes.Sum(c => c.DureeJours);
            decimal duree = nbAbsences > 0 ? Math.Round(totalJours / nbAbsences, 1) : 0m;

            int nbJustifs = allDemandes.Count(c => c.JustificatifFourni);
            decimal pctJustifies = nbAbsences > 0 ? Math.Round(nbJustifs * 100m / nbAbsences, 1) : 0m;

            int nbSalariesAbsents = allDemandes
                .Where(c => c.Salarie != null)
                .Select(c => c.Salarie!.Oid).Distinct().Count();

            // Effectif total (pour taux d'absentéisme) — salariés actifs au 31/12
            var effectif = CountSalariesActifs(os, finAnnee, filter);

            decimal denominateur = effectif * JoursOuvresAnnee;
            decimal taux = denominateur > 0 ? Math.Round(totalJours * 100m / denominateur, 2) : 0m;

            // Top absent
            var topAbsentGroup = allDemandes
                .Where(c => c.Salarie != null)
                .GroupBy(c => c.Salarie!)
                .Select(g => new { Salarie = g.Key, Jours = g.Sum(x => x.DureeJours), Nb = g.Count() })
                .OrderByDescending(x => x.Jours)
                .FirstOrDefault();

            // Mois pic
            var moisPicData = allDemandes
                .GroupBy(c => MonthOfMid(c))
                .Select(g => new { Mois = g.Key, Jours = g.Sum(x => x.DureeJours) })
                .OrderByDescending(x => x.Jours)
                .FirstOrDefault();

            // Coût estimé impayé (familles SansSolde)
            //   On approxime salaire jour moyen via la dernière paie connue du salarié si dispo
            decimal coutImpaye = 0m;
            try
            {
                var demandesImpayees = allDemandes
                    .Where(c => c.Type?.Famille == FamilleConge.SansSolde)
                    .ToList();

                if (demandesImpayees.Count > 0)
                {
                    var salaireParSalarie = os.GetObjectsQuery<Bulletin>()
                        .ToList()
                        .Where(b => b.Annee == filter.Annee && b.Salarie != null)
                        .GroupBy(b => b.Salarie!.Oid)
                        .ToDictionary(g => g.Key, g => g.Average(b => b.BrutFiscal) / 22m);

                    coutImpaye = demandesImpayees.Sum(c =>
                    {
                        if (c.Salarie == null) return 0m;
                        return salaireParSalarie.TryGetValue(c.Salarie.Oid, out var taxJ)
                            ? taxJ * c.DureeJours : 0m;
                    });
                    coutImpaye = Math.Round(coutImpaye, 0);
                }
            }
            catch { coutImpaye = 0m; }

            var kpis = new KpiAbsencesDto
            {
                NbAbsences          = nbAbsences,
                TotalJours          = totalJours,
                TauxAbsenteisme     = taux,
                DureeMoyenneJours   = duree,
                PourcentageJustifie = pctJustifies,
                TopAbsent           = topAbsentGroup?.Salarie.FullName ?? "—",
                TopAbsentJours      = topAbsentGroup?.Jours ?? 0m,
                MoisPic             = moisPicData?.Mois ?? 0,
                MoisPicLibelle      = moisPicData != null
                                      ? fr.DateTimeFormat.GetMonthName(moisPicData.Mois) : "—",
                CoutEstimeImpaye    = coutImpaye,
                NbSalariesAbsents   = nbSalariesAbsents,
                NbSalariesEffectif  = effectif
            };

            // ── 3. Charts par famille / catégorie / segment ────────────
            int totalAbsences = nbAbsences;
            var parFamille = allDemandes
                .GroupBy(c => c.Type?.Famille.ToString() ?? "(Non définie)")
                .Select(g => new BarItemDto
                {
                    Libelle    = LabelFamille(g.Key),
                    Valeur     = (int)g.Sum(x => x.DureeJours),
                    Pourcentage = totalJours > 0 ? Math.Round(g.Sum(x => x.DureeJours) * 100m / totalJours, 1) : 0m
                })
                .OrderByDescending(b => b.Valeur)
                .ToList();

            var parCategorie = allDemandes
                .GroupBy(c => c.Salarie?.Categories?.Intitule ?? "(Non renseignée)")
                .Select(g => new BarItemDto
                {
                    Libelle     = g.Key,
                    Valeur      = (int)g.Sum(x => x.DureeJours),
                    Pourcentage = totalJours > 0 ? Math.Round(g.Sum(x => x.DureeJours) * 100m / totalJours, 1) : 0m
                })
                .OrderByDescending(b => b.Valeur)
                .Take(10)
                .ToList();

            var parSegment = allDemandes
                .GroupBy(c => SafeDepartementNom(c.Salarie) ?? "(Non renseigné)")
                .Select(g => new BarItemDto
                {
                    Libelle     = g.Key,
                    Valeur      = (int)g.Sum(x => x.DureeJours),
                    Pourcentage = totalJours > 0 ? Math.Round(g.Sum(x => x.DureeJours) * 100m / totalJours, 1) : 0m
                })
                .OrderByDescending(b => b.Valeur)
                .ToList();

            // ── 4. Évolution mensuelle (12 mois) ───────────────────────
            var parMois = Enumerable.Range(1, 12).Select(m =>
            {
                var subset = allDemandes
                    .Where(c => MonthOfMid(c) == m)
                    .ToList();
                return new MoisAbsenceDto
                {
                    Mois        = m,
                    Libelle     = fr.DateTimeFormat.GetAbbreviatedMonthName(m),
                    NbJours     = subset.Sum(c => c.DureeJours),
                    NbAbsences  = subset.Count
                };
            }).ToList();

            // ── 5. Top 10 absents ──────────────────────────────────────
            var top10 = allDemandes
                .Where(c => c.Salarie != null)
                .GroupBy(c => c.Salarie!)
                .Select(g =>
                {
                    var fam = g.GroupBy(x => x.Type?.Famille.ToString() ?? "(Non définie)")
                               .OrderByDescending(x => x.Sum(y => y.DureeJours))
                               .First().Key;
                    return new TopAbsentRowDto
                    {
                        NomSalarie         = g.Key.FullName ?? "—",
                        Categorie          = g.Key.Categories?.Intitule ?? "—",
                        Segment            = SafeDepartementNom(g.Key) ?? "—",
                        NbAbsences         = g.Count(),
                        TotalJours         = g.Sum(x => x.DureeJours),
                        FamillePrincipale  = LabelFamille(fam)
                    };
                })
                .OrderByDescending(r => r.TotalJours)
                .Take(10)
                .ToList();

            return new SuiviAbsencesDto
            {
                Kpis           = kpis,
                ParFamille     = parFamille,
                ParCategorie   = parCategorie,
                ParSegment     = parSegment,
                ParMois        = parMois,
                Top10Absents   = top10,
                CalculatedAt   = DateTime.Now
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Helpers
        // ═════════════════════════════════════════════════════════════════════
        private static int MonthOfMid(CongeDemande c)
        {
            // Mois "représentatif" = mois du milieu de l'absence
            try
            {
                var mid = c.DateDebut.AddDays((c.DateFin - c.DateDebut).TotalDays / 2.0);
                if (mid.Year < 1900) return c.DateDebut.Month;
                return mid.Month;
            }
            catch { return c.DateDebut.Month; }
        }

        private static int CountSalariesActifs(IObjectSpace os, DateTime atDate, SuiviAbsencesFilterModel filter)
        {
            var sentinelle = new DateTime(1900, 1, 1);
            var all = os.GetObjectsQuery<Salarie>().ToList();
            return all.Count(s =>
                s.DateEmbauche <= atDate &&
                (s.DateSortie < sentinelle || s.DateSortie > atDate) &&
                (!filter.SiteOid.HasValue || s.Site?.Oid == filter.SiteOid.Value) &&
                (!filter.Genre.HasValue   || s.Sexe == filter.Genre.Value) &&
                (!filter.CategorieOid.HasValue || s.Categories?.Oid == filter.CategorieOid.Value) &&
                (string.IsNullOrWhiteSpace(filter.Segment) || SafeDepartementNom(s) == filter.Segment));
        }

        private static string LabelFamille(string code) => code switch
        {
            "Annuel"             => "Congé annuel",
            "Maladie"            => "Maladie",
            "Maternite"          => "Maternité / Paternité",
            "EvenementFamilial"  => "Événement familial",
            "SansSolde"          => "Sans solde",
            "Recuperation"       => "Récupération",
            _                    => code
        };

        private static string? SafeDepartementNom(Salarie? s)
        {
            if (s == null) return null;
            try
            {
                var prop = s.GetType().GetProperty("Departement");
                var dep = prop?.GetValue(s);
                if (dep == null) return null;
                var nomProp = dep.GetType().GetProperty("Nom");
                return nomProp?.GetValue(dep) as string;
            }
            catch { return null; }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Lookups
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
                var actifs = os.GetObjectsQuery<Site>().Where(s => s.Actif).OrderBy(s => s.Nom).ToList();
                return actifs.Count > 0 ? actifs : os.GetObjectsQuery<Site>().OrderBy(s => s.Nom).ToList();
            }
            catch { return new List<Site>(); }
        }

        public List<Departement> GetDepartements(IObjectSpace os)
        {
            try { return os.GetObjectsQuery<Departement>().Where(d => d.Actif).OrderBy(d => d.Nom).ToList(); }
            catch { return new List<Departement>(); }
        }

        public List<Categories> GetCategories(IObjectSpace os)
        {
            try { return os.GetObjectsQuery<Categories>().OrderBy(c => c.Intitule).ToList(); }
            catch { return new List<Categories>(); }
        }

        public void InvalidateCache(SuiviAbsencesFilterModel filter)
        {
            if (filter == null) return;
            _cache.Remove(filter.ToCacheKey());
        }
    }
}
