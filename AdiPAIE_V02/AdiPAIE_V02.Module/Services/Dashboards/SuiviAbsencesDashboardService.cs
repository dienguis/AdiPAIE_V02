// =============================================================================
//  SuiviAbsencesDashboardService.cs — V1.3.2 (mai 2026)
//
//  REFONTE Excel-style. Calcule pour le Tableau N°5 :
//    - 6 KPIs (EffectifMoyen, NbSalariesAbsents, TotalJours, JOPerdus,
//             TauxAbsenteismePct, RespTempsTravailPct)
//    - Décomposition fine par 10 motifs (5 absentéisme + 4 programmées + Autre)
//    - Tableaux Par Motif, Par Ancienneté, Par Catégorie, Par Département
//    - Évolution mensuelle (2 séries : absentéisme + programmées)
//    - Liste détaillée employés avec décomposition par motif
//
//  Mapping motif :
//    - Match prioritaire sur CongeType.Code (ex "MAL", "AT", "CPAYE")
//    - Sinon fallback sur CongeType.Famille (Annuel→CPAYE, Maladie→MAL, etc.)
//    - Maternite : MAT si Sexe=Feminin, sinon PAT
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
        private static readonly CultureInfo FrCulture = CultureInfo.GetCultureInfo("fr-FR");
        private const decimal JoursOuvresParMois = 22m;
        private static readonly DateTime SortieSentinelle = new(1900, 1, 1);

        public SuiviAbsencesDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public SuiviAbsencesDto GetData(SuiviAbsencesFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var key = filter.ToCacheKey();
            if (_cache.TryGetValue<SuiviAbsencesDto>(key, out var cached) && cached != null)
                return cached;

            var dto = filter.Personnel == PersonnelType.Externe
                ? ComputeExterne(filter, os)
                : Compute(filter, os);
            _cache.Set(key, dto, CacheTtl);
            return dto;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EXTERNE — calcul depuis BulletinInterim (30ème de présence)
        // ─────────────────────────────────────────────────────────────────────
        //  Logique :
        //    Pour chaque BulletinInterim, JoursAbsence = (30 - Trentieme).
        //    On ne connaît pas le motif détaillé → on classe en "AUT" (Autre
        //    absentéisme) par défaut (programmées indéterminables côté facture).
        //    Effectif = nb intérimaires distincts ayant au moins un bulletin
        //    sur la période. Resp Tmp Travail = moyenne des Trentieme/30.
        // ─────────────────────────────────────────────────────────────────────
        private SuiviAbsencesDto ComputeExterne(SuiviAbsencesFilterModel filter, IObjectSpace os)
        {
            var annees = filter.Annees?.Distinct().ToList() ?? new List<int>();
            if (annees.Count == 0) annees.Add(DateTime.Today.Year);
            var moisFilter = filter.Mois?.Distinct().ToList() ?? new List<int>();
            var siteFilter = filter.SiteOids?.ToHashSet() ?? new HashSet<Guid>();

            // Récupération des bulletins intérim sur les années/mois demandés
            var bulletins = os.GetObjectsQuery<AdiPAIE_V02.Module.BusinessObjects.Interim.BulletinInterim>()
                .ToList()
                .Where(b => IsActif(b) && annees.Contains(b.Annee))
                .Where(b => moisFilter.Count == 0 || moisFilter.Contains(b.Mois))
                .ToList();

            if (siteFilter.Count > 0)
            {
                // Site via Interimaire.Contrats... ou via SiteAffectation snapshot
                var siteRefs = os.GetObjectsQuery<Site>().ToList()
                    .Where(s => siteFilter.Contains(s.Oid))
                    .Select(s => (s.Nom ?? "").Trim().ToUpperInvariant())
                    .ToHashSet();
                bulletins = bulletins
                    .Where(b => siteRefs.Contains((b.SiteAffectation ?? "").Trim().ToUpperInvariant()))
                    .ToList();
            }

            int nbMoisFiltre = (moisFilter.Count > 0 ? moisFilter.Count : 12) * annees.Count;

            // Effectif moyen = nb intérimaires distincts / nb mois × 1 (par mois moyen)
            int effectif = bulletins
                .Select(b => b.Interimaire?.Oid ?? Guid.NewGuid())
                .Distinct().Count();
            // Approximation effectif moyen mensuel : total bulletins / nb mois
            decimal effMoyen = nbMoisFiltre > 0
                ? Math.Round((decimal)bulletins.Count / nbMoisFiltre, 0) : 0m;

            decimal totalJoursAbs = bulletins.Sum(b => Math.Max(0m, 30m - b.Trentieme));
            decimal totalJoursPresence = bulletins.Sum(b => b.Trentieme);
            decimal denom = totalJoursPresence + totalJoursAbs;
            decimal taux = denom > 0 ? Math.Round(totalJoursAbs * 100m / denom, 2) : 0m;

            var kpis = new KpiAbsencesDto
            {
                EffectifMoyen = effMoyen,
                NbSalariesAbsents = bulletins.Where(b => b.Trentieme < 30m)
                    .Select(b => b.Interimaire?.Oid ?? Guid.Empty).Distinct().Count(),
                TotalJours = totalJoursAbs,
                TauxAbsenteismePct = taux,
                JOPerdus = totalJoursAbs,
                RespTempsTravailPct = Math.Max(0m, 100m - taux),
                NbAbsences = bulletins.Count(b => b.Trentieme < 30m),
                DureeMoyenneJours = bulletins.Count(b => b.Trentieme < 30m) > 0
                    ? Math.Round(totalJoursAbs / bulletins.Count(b => b.Trentieme < 30m), 1) : 0m,
                TopAbsent = "",
                TopAbsentJours = 0m,
                MoisPic = 0,
                MoisPicLibelle = ""
            };

            // Top absent pour EXTERNE
            var topAbs = bulletins
                .Where(b => b.Interimaire != null && b.Trentieme < 30m)
                .GroupBy(b => b.Interimaire!.Oid)
                .Select(g => new {
                    Sal = g.First().Interimaire!,
                    Jours = g.Sum(x => 30m - x.Trentieme)
                })
                .OrderByDescending(x => x.Jours)
                .FirstOrDefault();
            if (topAbs != null)
            {
                kpis.TopAbsent = topAbs.Sal.FullName ?? "";
                kpis.TopAbsentJours = topAbs.Jours;
            }

            // Une seule ligne motif "AUT" (motif inconnu côté facture intérim)
            var parMotif = totalJoursAbs > 0 ? new List<MotifRowDto>
            {
                new()
                {
                    Motif = MotifAbsence.AUT,
                    Code = "AUT",
                    Libelle = "Absence intérim (30ème < 30)",
                    IsAbsenteisme = true,
                    NbJours = totalJoursAbs,
                    NbAbsences = bulletins.Count(b => b.Trentieme < 30m),
                    PourcentageDuTotal = 100m
                }
            } : new();

            // Évolution mensuelle
            var parMois = Enumerable.Range(1, 12).Select(m =>
            {
                var bm = bulletins.Where(x => x.Mois == m).ToList();
                return new MoisAbsenceDto
                {
                    Mois = m,
                    Libelle = FrCulture.DateTimeFormat.GetAbbreviatedMonthName(m),
                    JoursAbsenteisme = bm.Sum(x => Math.Max(0m, 30m - x.Trentieme)),
                    JoursProgrammees = 0m,
                    NbAbsences = bm.Count(x => x.Trentieme < 30m)
                };
            }).ToList();

            // Liste détaillée par intérimaire (Top 100)
            var employes = bulletins
                .Where(b => b.Interimaire != null)
                .GroupBy(b => b.Interimaire!.Oid)
                .Select(g =>
                {
                    var first = g.First();
                    decimal jAbs = g.Sum(x => Math.Max(0m, 30m - x.Trentieme));
                    decimal denomS = 30m * g.Count();
                    decimal tauxS = denomS > 0 ? Math.Round(jAbs * 100m / denomS, 2) : 0m;
                    return new EmployeAbsenceRowDto
                    {
                        SalarieOid = g.Key,
                        Matricule = first.MatriculeOriginal ?? "",
                        NomComplet = first.Interimaire!.FullName ?? first.NomComplet,
                        Site = first.SiteAffectation ?? "",
                        Departement = "",
                        Categorie = "Intérimaire",
                        Fonction = first.Fonction ?? "",
                        AncienneteAnnees = 0m,
                        JoursAbsenteisme = jAbs,
                        JoursProgrammees = 0m,
                        TotalJours = jAbs,
                        RespTempsTravailPct = Math.Max(0m, 100m - tauxS),
                        JoursAUT = jAbs
                    };
                })
                .Where(e => e.TotalJours > 0)
                .OrderByDescending(e => e.TotalJours)
                .ToList();

            return new SuiviAbsencesDto
            {
                Kpis = kpis,
                ParMotif = parMotif,
                ParAnciennete = new(),
                ParCategorie = new(),
                ParDepartement = new(),
                ParMois = parMois,
                Employes = employes,
                CalculatedAt = DateTime.Now
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        private SuiviAbsencesDto Compute(SuiviAbsencesFilterModel filter, IObjectSpace os)
        {
            // ── Normalisation filtres : si Annees vide, prendre l'année courante ──
            var annees = filter.Annees?.Distinct().ToList() ?? new List<int>();
            if (annees.Count == 0) annees.Add(DateTime.Today.Year);

            var moisFilter = filter.Mois?.Distinct().ToList() ?? new List<int>();
            var siteFilter = filter.SiteOids?.ToHashSet() ?? new HashSet<Guid>();
            var genreFilter = filter.GenreSet?.ToHashSet() ?? new HashSet<Sexe>();
            var deptFilter = filter.DepartementsNoms?.Where(d => !string.IsNullOrEmpty(d))
                .Select(d => d.Trim().ToUpperInvariant()).ToHashSet() ?? new HashSet<string>();
            var catFilter = filter.CategorieOids?.ToHashSet() ?? new HashSet<Guid>();
            var motifFilter = filter.MotifsActifs?.ToHashSet() ?? new HashSet<MotifAbsence>();

            // ── 1. Salariés actifs sur la période ─────────────────────────
            int anneeMax = annees.Max();
            var dateRef = new DateTime(anneeMax, 12, 31);
            var dateMinPeriode = new DateTime(annees.Min(), 1, 1);

            var salaries = os.GetObjectsQuery<Salarie>()
                .ToList()
                .Where(s => IsActif(s)
                         && s.DateEmbauche != default
                         && s.DateEmbauche <= dateRef
                         && (s.DateSortie == default
                             || s.DateSortie == SortieSentinelle
                             || s.DateSortie > dateMinPeriode))
                .ToList();

            // Application des filtres dimensionnels sur Salarie
            if (siteFilter.Count > 0)
                salaries = salaries.Where(s => s.Site != null && siteFilter.Contains(s.Site.Oid)).ToList();
            if (genreFilter.Count > 0)
                salaries = salaries.Where(s => genreFilter.Contains(s.Sexe)).ToList();
            if (deptFilter.Count > 0)
                salaries = salaries.Where(s =>
                {
                    var d = SafeDepartement(s);
                    return !string.IsNullOrEmpty(d) && deptFilter.Contains(d.Trim().ToUpperInvariant());
                }).ToList();
            if (catFilter.Count > 0)
                salaries = salaries.Where(s => s.Categories != null && catFilter.Contains(s.Categories.Oid)).ToList();

            var salariesOids = salaries.Select(s => s.Oid).ToHashSet();

            // ── 2. CongeDemande — récupération + filtres ──────────────────
            var allDemandes = os.GetObjectsQuery<CongeDemande>().ToList();

            var demandes = allDemandes
                .Where(d => IsActif(d) && d.Salarie != null
                                       && salariesOids.Contains(d.Salarie.Oid))
                .Where(d =>
                {
                    if (filter.InclureEnAttente)
                        return d.Statut == CongeStatut.Accordee
                            || d.Statut == CongeStatut.Soumise
                            || d.Statut == CongeStatut.EnAttenteN1
                            || d.Statut == CongeStatut.EnAttenteN2;
                    return d.Statut == CongeStatut.Accordee;
                })
                .Where(d => RecouvreAnneeOuMois(d.DateDebut, d.DateFin, annees, moisFilter))
                .ToList();

            // Mapping motif pour chaque demande
            var demandesAvecMotif = demandes
                .Select(d => new
                {
                    Demande = d,
                    Motif = MapMotif(d.Type, d.Salarie?.Sexe ?? Sexe.Masculin)
                })
                .Where(x => motifFilter.Count == 0 || motifFilter.Contains(x.Motif))
                .ToList();

            // ── 3. KPIs ───────────────────────────────────────────────────
            int effectif = salaries.Count;
            int nbMoisFiltre = (moisFilter.Count > 0 ? moisFilter.Count : 12) * annees.Count;
            decimal joursOuvresPeriode = effectif * JoursOuvresParMois * nbMoisFiltre;

            decimal totalJours = demandesAvecMotif.Sum(x => x.Demande.DureeJours);
            decimal joPerdus = demandesAvecMotif
                .Where(x => IsAbsenteisme(x.Motif))
                .Sum(x => x.Demande.DureeJours);
            decimal tauxAbsenteismePct = joursOuvresPeriode > 0
                ? Math.Round(joPerdus * 100m / joursOuvresPeriode, 2)
                : 0m;
            decimal respTempsTravailPct = Math.Max(0m, 100m - tauxAbsenteismePct);

            var topAbsent = demandesAvecMotif
                .Where(x => IsAbsenteisme(x.Motif))
                .GroupBy(x => x.Demande.Salarie!.Oid)
                .Select(g => new { Sal = g.First().Demande.Salarie!, Total = g.Sum(x => x.Demande.DureeJours) })
                .OrderByDescending(g => g.Total)
                .FirstOrDefault();

            // Mois pic (basé sur jours d'absentéisme)
            int moisPic = 0;
            string moisPicLib = "";
            try
            {
                var topMois = Enumerable.Range(1, 12)
                    .Select(m => new { Mois = m, Jours = demandesAvecMotif
                        .Where(x => IsAbsenteisme(x.Motif))
                        .Where(x => DateRecouvreMois(x.Demande.DateDebut, x.Demande.DateFin, m, annees))
                        .Sum(x => JoursDansMois(x.Demande.DateDebut, x.Demande.DateFin, m, annees)) })
                    .Where(x => x.Jours > 0)
                    .OrderByDescending(x => x.Jours)
                    .FirstOrDefault();
                if (topMois != null)
                {
                    moisPic = topMois.Mois;
                    moisPicLib = FrCulture.DateTimeFormat.GetMonthName(moisPic);
                }
            }
            catch { /* défensif */ }

            var kpis = new KpiAbsencesDto
            {
                EffectifMoyen = effectif,
                NbSalariesAbsents = demandesAvecMotif.Where(x => x.Demande.Salarie != null)
                    .Select(x => x.Demande.Salarie!.Oid).Distinct().Count(),
                TotalJours = totalJours,
                TauxAbsenteismePct = tauxAbsenteismePct,
                JOPerdus = joPerdus,
                RespTempsTravailPct = respTempsTravailPct,
                NbAbsences = demandesAvecMotif.Count,
                DureeMoyenneJours = demandesAvecMotif.Count > 0
                    ? Math.Round(totalJours / demandesAvecMotif.Count, 1) : 0m,
                TopAbsent = topAbsent?.Sal.FullName ?? "",
                TopAbsentJours = topAbsent?.Total ?? 0m,
                MoisPic = moisPic,
                MoisPicLibelle = moisPicLib
            };

            // ── 4. Par Motif ──────────────────────────────────────────────
            var parMotif = demandesAvecMotif
                .GroupBy(x => x.Motif)
                .Select(g =>
                {
                    var sumJ = g.Sum(x => x.Demande.DureeJours);
                    return new MotifRowDto
                    {
                        Motif = g.Key,
                        Code = g.Key.ToString(),
                        Libelle = LibelleMotif(g.Key),
                        IsAbsenteisme = IsAbsenteisme(g.Key),
                        NbJours = sumJ,
                        NbAbsences = g.Count(),
                        PourcentageDuTotal = totalJours > 0
                            ? Math.Round(sumJ * 100m / totalJours, 1) : 0m
                    };
                })
                .OrderByDescending(r => r.NbJours)
                .ToList();

            // ── 5. Par Ancienneté ─────────────────────────────────────────
            var trancheDef = new (string Lib, decimal Min, decimal Max)[]
            {
                ("0",      0m,    1m),
                ("1-4",    1m,    5m),
                ("5-9",    5m,    10m),
                ("10-14",  10m,   15m),
                ("15+",    15m,   999m)
            };

            // Calcul ancienneté par salarié actif
            var salariesAvecAnc = salaries
                .Select(s => new { Sal = s, Anc = ComputeAncienneteAnnees(s.DateEmbauche, dateRef) })
                .ToList();

            var parAnc = trancheDef.Select(t =>
            {
                var sub = salariesAvecAnc.Where(s => s.Anc >= t.Min && s.Anc < t.Max).ToList();
                var oids = sub.Select(s => s.Sal.Oid).ToHashSet();
                var jp = demandesAvecMotif
                    .Where(x => x.Demande.Salarie != null && oids.Contains(x.Demande.Salarie.Oid))
                    .Where(x => IsAbsenteisme(x.Motif))
                    .Sum(x => x.Demande.DureeJours);
                decimal denom = sub.Count * JoursOuvresParMois * nbMoisFiltre;
                return new TrancheAncienneteRowDto
                {
                    Tranche = t.Lib,
                    NbSalariesActifs = sub.Count,
                    JOPerdus = jp,
                    TauxAbsenteismePct = denom > 0 ? Math.Round(jp * 100m / denom, 2) : 0m
                };
            })
            .Where(r => r.NbSalariesActifs > 0)
            .ToList();

            // ── 6. Par Catégorie ──────────────────────────────────────────
            var parCat = salaries
                .GroupBy(s => SafeCategorie(s))
                .Select(g =>
                {
                    var oids = g.Select(s => s.Oid).ToHashSet();
                    var jp = demandesAvecMotif
                        .Where(x => x.Demande.Salarie != null && oids.Contains(x.Demande.Salarie.Oid))
                        .Where(x => IsAbsenteisme(x.Motif))
                        .Sum(x => x.Demande.DureeJours);
                    decimal denom = g.Count() * JoursOuvresParMois * nbMoisFiltre;
                    return new DimRowDto
                    {
                        Libelle = g.Key,
                        NbSalariesActifs = g.Count(),
                        JOPerdus = jp,
                        TauxAbsenteismePct = denom > 0 ? Math.Round(jp * 100m / denom, 2) : 0m
                    };
                })
                .OrderByDescending(r => r.JOPerdus)
                .ToList();

            // ── 7. Par Département ────────────────────────────────────────
            var parDept = salaries
                .GroupBy(s => SafeDepartement(s) ?? "(Non renseigné)")
                .Select(g =>
                {
                    var oids = g.Select(s => s.Oid).ToHashSet();
                    var jp = demandesAvecMotif
                        .Where(x => x.Demande.Salarie != null && oids.Contains(x.Demande.Salarie.Oid))
                        .Where(x => IsAbsenteisme(x.Motif))
                        .Sum(x => x.Demande.DureeJours);
                    decimal denom = g.Count() * JoursOuvresParMois * nbMoisFiltre;
                    return new DimRowDto
                    {
                        Libelle = g.Key,
                        NbSalariesActifs = g.Count(),
                        JOPerdus = jp,
                        TauxAbsenteismePct = denom > 0 ? Math.Round(jp * 100m / denom, 2) : 0m
                    };
                })
                .OrderByDescending(r => r.JOPerdus)
                .ToList();

            // ── 8. Par Mois (12 lignes — 2 séries) ────────────────────────
            var parMois = Enumerable.Range(1, 12).Select(m =>
            {
                var demM = demandesAvecMotif
                    .Where(x => DateRecouvreMois(x.Demande.DateDebut, x.Demande.DateFin, m, annees))
                    .Select(x => new {
                        x.Motif,
                        Jours = JoursDansMois(x.Demande.DateDebut, x.Demande.DateFin, m, annees)
                    })
                    .ToList();
                return new MoisAbsenceDto
                {
                    Mois = m,
                    Libelle = FrCulture.DateTimeFormat.GetAbbreviatedMonthName(m),
                    JoursAbsenteisme = demM.Where(x => IsAbsenteisme(x.Motif)).Sum(x => x.Jours),
                    JoursProgrammees = demM.Where(x => !IsAbsenteisme(x.Motif)).Sum(x => x.Jours),
                    NbAbsences = demM.Count
                };
            }).ToList();

            // ── 9. Liste détaillée employés ───────────────────────────────
            var employes = salaries
                .Select(s =>
                {
                    var demS = demandesAvecMotif
                        .Where(x => x.Demande.Salarie != null && x.Demande.Salarie.Oid == s.Oid)
                        .ToList();
                    decimal jAbs = demS.Where(x => IsAbsenteisme(x.Motif)).Sum(x => x.Demande.DureeJours);
                    decimal jProg = demS.Where(x => !IsAbsenteisme(x.Motif)).Sum(x => x.Demande.DureeJours);
                    decimal denomS = JoursOuvresParMois * nbMoisFiltre;
                    decimal tauxS = denomS > 0 ? Math.Round(jAbs * 100m / denomS, 2) : 0m;
                    return new EmployeAbsenceRowDto
                    {
                        SalarieOid = s.Oid,
                        Matricule = s.Matricule ?? "",
                        NomComplet = s.FullName ?? "",
                        Site = s.Site?.Nom ?? "",
                        Departement = SafeDepartement(s) ?? "",
                        Categorie = SafeCategorie(s),
                        Fonction = SafeFonction(s),
                        AncienneteAnnees = Math.Round(ComputeAncienneteAnnees(s.DateEmbauche, dateRef), 1),
                        JoursAbsenteisme = jAbs,
                        JoursProgrammees = jProg,
                        TotalJours = jAbs + jProg,
                        RespTempsTravailPct = Math.Max(0m, 100m - tauxS),
                        JoursAUT = demS.Where(x => x.Motif == MotifAbsence.AUT).Sum(x => x.Demande.DureeJours),
                        JoursNAUT = demS.Where(x => x.Motif == MotifAbsence.NAUT).Sum(x => x.Demande.DureeJours),
                        JoursEVENT = demS.Where(x => x.Motif == MotifAbsence.EVENT).Sum(x => x.Demande.DureeJours),
                        JoursMAL = demS.Where(x => x.Motif == MotifAbsence.MAL).Sum(x => x.Demande.DureeJours),
                        JoursAT = demS.Where(x => x.Motif == MotifAbsence.AT).Sum(x => x.Demande.DureeJours),
                        JoursCPAYE = demS.Where(x => x.Motif == MotifAbsence.CPAYE).Sum(x => x.Demande.DureeJours),
                        JoursFORM = demS.Where(x => x.Motif == MotifAbsence.FORM).Sum(x => x.Demande.DureeJours),
                        JoursMAT = demS.Where(x => x.Motif == MotifAbsence.MAT).Sum(x => x.Demande.DureeJours),
                        JoursPAT = demS.Where(x => x.Motif == MotifAbsence.PAT).Sum(x => x.Demande.DureeJours)
                    };
                })
                .OrderByDescending(e => e.TotalJours)
                .ToList();

            return new SuiviAbsencesDto
            {
                Kpis = kpis,
                ParMotif = parMotif,
                ParAnciennete = parAnc,
                ParCategorie = parCat,
                ParDepartement = parDept,
                ParMois = parMois,
                Employes = employes,
                CalculatedAt = DateTime.Now
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HELPERS — Mapping motif
        // ─────────────────────────────────────────────────────────────────────
        private static MotifAbsence MapMotif(CongeType? type, Sexe sexe)
        {
            if (type == null) return MotifAbsence.Autre;

            // 1. Match prioritaire sur Code (case-insensitive)
            var code = (type.Code ?? "").Trim().ToUpperInvariant();
            switch (code)
            {
                case "AUT":   return MotifAbsence.AUT;
                case "NAUT":  return MotifAbsence.NAUT;
                case "EVENT":
                case "EVT":
                case "EVENEMENT":
                    return MotifAbsence.EVENT;
                case "MAL":
                case "MALADIE":
                    return MotifAbsence.MAL;
                case "AT":
                case "ACCIDENT":
                case "ACCT":
                    return MotifAbsence.AT;
                case "CPAYE":
                case "CP":
                case "ANNUEL":
                case "CONGE_ANNUEL":
                    return MotifAbsence.CPAYE;
                case "FORM":
                case "FORMATION":
                    return MotifAbsence.FORM;
                case "MAT":
                case "MATERNITE":
                    return MotifAbsence.MAT;
                case "PAT":
                case "PATERNITE":
                    return MotifAbsence.PAT;
            }

            // 2. Fallback sur Famille (avec règle MAT/PAT selon Sexe)
            try
            {
                switch (type.Famille)
                {
                    case FamilleConge.Annuel:            return MotifAbsence.CPAYE;
                    case FamilleConge.Maladie:           return MotifAbsence.MAL;
                    case FamilleConge.Maternite:         return sexe == Sexe.Feminin ? MotifAbsence.MAT : MotifAbsence.PAT;
                    case FamilleConge.EvenementFamilial: return MotifAbsence.EVENT;
                    case FamilleConge.SansSolde:         return MotifAbsence.AUT;
                    case FamilleConge.Recuperation:      return MotifAbsence.AUT;
                    default:                             return MotifAbsence.Autre;
                }
            }
            catch { return MotifAbsence.Autre; }
        }

        private static bool IsAbsenteisme(MotifAbsence m) =>
            m == MotifAbsence.AUT || m == MotifAbsence.NAUT
            || m == MotifAbsence.EVENT || m == MotifAbsence.MAL
            || m == MotifAbsence.AT;

        private static string LibelleMotif(MotifAbsence m) => m switch
        {
            MotifAbsence.AUT   => "Autre absentéisme",
            MotifAbsence.NAUT  => "Non Autorisé",
            MotifAbsence.EVENT => "Événement familial",
            MotifAbsence.MAL   => "Maladie",
            MotifAbsence.AT    => "Accident du Travail",
            MotifAbsence.CPAYE => "Congé Payé",
            MotifAbsence.FORM  => "Formation",
            MotifAbsence.MAT   => "Maternité",
            MotifAbsence.PAT   => "Paternité",
            _                  => "Autre"
        };

        // ─────────────────────────────────────────────────────────────────────
        //  HELPERS — Période + recouvrement
        // ─────────────────────────────────────────────────────────────────────
        private static bool RecouvreAnneeOuMois(DateTime debut, DateTime fin, List<int> annees, List<int> mois)
        {
            // Recouvre au moins une année cible
            bool okAnnee = false;
            foreach (var y in annees)
            {
                var dy0 = new DateTime(y, 1, 1);
                var dy1 = new DateTime(y, 12, 31);
                if (debut <= dy1 && fin >= dy0) { okAnnee = true; break; }
            }
            if (!okAnnee) return false;
            if (mois.Count == 0) return true;

            // Si filtre mois : recouvre au moins un mois sélectionné
            foreach (var y in annees)
            {
                foreach (var m in mois)
                {
                    var dm0 = new DateTime(y, m, 1);
                    var dm1 = dm0.AddMonths(1).AddDays(-1);
                    if (debut <= dm1 && fin >= dm0) return true;
                }
            }
            return false;
        }

        private static bool DateRecouvreMois(DateTime debut, DateTime fin, int mois, List<int> annees)
        {
            foreach (var y in annees)
            {
                var d0 = new DateTime(y, mois, 1);
                var d1 = d0.AddMonths(1).AddDays(-1);
                if (debut <= d1 && fin >= d0) return true;
            }
            return false;
        }

        private static decimal JoursDansMois(DateTime debut, DateTime fin, int mois, List<int> annees)
        {
            // Jours calendaires recouvrés dans le mois cible (sur l'ensemble des années).
            decimal total = 0m;
            foreach (var y in annees)
            {
                var d0 = new DateTime(y, mois, 1);
                var d1 = d0.AddMonths(1).AddDays(-1);
                if (debut > d1 || fin < d0) continue;
                var debutEff = debut > d0 ? debut : d0;
                var finEff = fin < d1 ? fin : d1;
                total += (decimal)((finEff - debutEff).TotalDays + 1);
            }
            return total;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HELPERS — Salarié
        // ─────────────────────────────────────────────────────────────────────
        private static decimal ComputeAncienneteAnnees(DateTime dateEmbauche, DateTime dateRef)
        {
            if (dateEmbauche == default || dateEmbauche > dateRef) return 0m;
            var ts = dateRef - dateEmbauche;
            return (decimal)Math.Max(0, ts.TotalDays / 365.25);
        }

        private static string SafeCategorie(Salarie s)
        {
            try { return s.Categories?.Intitule ?? "(non renseignée)"; }
            catch { return "(non renseignée)"; }
        }

        private static string SafeFonction(Salarie s)
        {
            try
            {
                var prop = typeof(Salarie).GetProperty("Fonction");
                if (prop != null)
                {
                    var v = prop.GetValue(s);
                    if (v != null)
                    {
                        var lib = v.GetType().GetProperty("Intitule")?.GetValue(v)
                               ?? v.GetType().GetProperty("Libelle")?.GetValue(v)
                               ?? v.GetType().GetProperty("Nom")?.GetValue(v);
                        if (lib != null) return lib.ToString() ?? "";
                    }
                }
            }
            catch { }
            return "";
        }

        /// <summary>Récupère le nom du Département (via Departement.Nom) en mode défensif.</summary>
        private static string? SafeDepartement(Salarie s)
        {
            try
            {
                var prop = typeof(Salarie).GetProperty("Departement");
                if (prop != null)
                {
                    var dep = prop.GetValue(s);
                    if (dep != null)
                    {
                        var nom = dep.GetType().GetProperty("Nom")?.GetValue(dep)
                               ?? dep.GetType().GetProperty("Libelle")?.GetValue(dep)
                               ?? dep.GetType().GetProperty("Code")?.GetValue(dep);
                        if (nom != null) return nom.ToString();
                    }
                }
            }
            catch { }
            return null;
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

        // ─────────────────────────────────────────────────────────────────────
        //  Méthodes de référentiels (interface ISuiviAbsencesDashboardService)
        // ─────────────────────────────────────────────────────────────────────

        public List<int> GetAnneesDisponibles(IObjectSpace os)
        {
            try
            {
                var demandes = os.GetObjectsQuery<CongeDemande>().ToList()
                    .Where(d => IsActif(d) && d.DateDebut != default)
                    .ToList();
                var annees = demandes.SelectMany(d =>
                {
                    var list = new List<int>();
                    for (int y = d.DateDebut.Year; y <= d.DateFin.Year; y++) list.Add(y);
                    return list;
                }).Distinct().ToList();
                annees.Add(DateTime.Today.Year);
                return annees.Distinct().OrderByDescending(y => y).ToList();
            }
            catch
            {
                return new List<int> { DateTime.Today.Year, DateTime.Today.Year - 1 };
            }
        }

        public List<Site> GetSitesActifs(IObjectSpace os)
        {
            try
            {
                return os.GetObjectsQuery<Site>().ToList()
                    .Where(s => IsActif(s))
                    .OrderBy(s => s.Nom)
                    .ToList();
            }
            catch
            {
                return new List<Site>();
            }
        }

        public List<Departement> GetDepartements(IObjectSpace os)
        {
            try
            {
                return os.GetObjectsQuery<Departement>().ToList()
                    .Where(d => IsActif(d))
                    .OrderBy(d => d.Nom)
                    .ToList();
            }
            catch
            {
                return new List<Departement>();
            }
        }

        public List<Categories> GetCategories(IObjectSpace os)
        {
            try
            {
                return os.GetObjectsQuery<Categories>().ToList()
                    .Where(c => IsActif(c))
                    .OrderBy(c => c.Intitule ?? "")
                    .ToList();
            }
            catch
            {
                return new List<Categories>();
            }
        }

        public void InvalidateCache(SuiviAbsencesFilterModel filter)
        {
            try
            {
                if (filter != null) _cache.Remove(filter.ToCacheKey());
            }
            catch { /* défensif */ }
        }
    }
}
