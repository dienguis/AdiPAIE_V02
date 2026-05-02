// =============================================================================
//  BilanSocialDashboardService.cs
//  Tableau N°6 (Bilan Social Mensuel) — implémentation XPO, INTERNE uniquement.
//
//  Construit une vue 12 mois × indicateurs clés :
//    - Effectif fin mois          (Salarie : DateEmbauche/DateSortie)
//    - Embauches                  (Salarie.DateEmbauche dans le mois)
//    - Départs                    (Salarie.DateSortie  dans le mois)
//    - Masse Salariale            (Bulletin.BrutFiscal)
//    - Charges Patronales         (BulletinLigne.MontantEmployeur)
//    - Coût Employeur             (Masse + Charges) — calculé côté DTO
//    - Employés absents           (CongeDemande Accordee — distinct par mois)
//    - Jours d'absence            (CongeDemande Accordee — somme DureeJours par mois)
//    - Ligne TOTAL en queue
//
//  Indicateurs N/A (entités non présentes ou hors périmètre actuel) :
//    Mouvements emplois, Mesures disciplinaires, Accidents (travail/trajet),
//    Maladies professionnelles, Budget Formation, Heures Formation, Employés Formés.
//    → renvoyés en `null` pour distinguer de zéro réel ; le Razor affiche « — ».
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
    /// <inheritdoc cref="IBilanSocialDashboardService"/>
    public sealed class BilanSocialDashboardService : IBilanSocialDashboardService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly DateTime SortieSentinelle = new(1900, 1, 1);
        private const decimal JoursOuvresAnnee = 264m;   // 22 j × 12 mois

        public BilanSocialDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        // ─────────────────────────────────────────────────────────────────────
        public BilanSocialDto GetData(BilanSocialFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var key = filter.ToCacheKey();
            if (_cache.TryGetValue<BilanSocialDto>(key, out var cached) && cached != null)
                return cached;

            var dto = Compute(filter, os);
            _cache.Set(key, dto, CacheTtl);
            return dto;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Calcul principal — 12 mois + ligne Total
        // ═════════════════════════════════════════════════════════════════════
        private static BilanSocialDto Compute(BilanSocialFilterModel filter, IObjectSpace os)
        {
            var fr = CultureInfo.GetCultureInfo("fr-FR");

            // ── 1. Pré-charge des collections (1 fetch chacune) ────────
            var salaries  = os.GetObjectsQuery<Salarie>().ToList();
            if (filter.SiteOid.HasValue)
                salaries = salaries.Where(s => s.Site?.Oid == filter.SiteOid.Value).ToList();

            var bulletins = os.GetObjectsQuery<Bulletin>()
                .ToList()
                .Where(b => b.Annee == filter.Annee && IsActif(b))
                .ToList();
            if (filter.SiteOid.HasValue)
                bulletins = bulletins.Where(b => b.Salarie?.Site?.Oid == filter.SiteOid.Value).ToList();

            // Pour les charges, on charge toutes les BulletinLigne de l'année
            //   et on les indexe par OidBulletin pour grouper par mois.
            var lignesParBulletin = new Dictionary<Guid, decimal>();
            try
            {
                foreach (var bl in os.GetObjectsQuery<BulletinLigne>().ToList())
                {
                    if (bl.Bulletin == null || !IsActif(bl) || !IsActif(bl.Bulletin)) continue;
                    if (bl.Bulletin.Annee != filter.Annee) continue;
                    var oid = bl.Bulletin.Oid;
                    if (!lignesParBulletin.ContainsKey(oid))
                        lignesParBulletin[oid] = 0m;
                    lignesParBulletin[oid] += bl.MontantEmployeur;
                }
            }
            catch { }

            var demandes = os.GetObjectsQuery<CongeDemande>()
                .ToList()
                .Where(c => c.Statut == CongeStatut.Accordee
                         && c.DateDebut.Year <= filter.Annee
                         && c.DateFin.Year   >= filter.Annee)
                .ToList();
            if (filter.SiteOid.HasValue)
                demandes = demandes.Where(c => c.Salarie?.Site?.Oid == filter.SiteOid.Value).ToList();

            // ── 2. Calcul mois par mois ────────────────────────────────
            var mois = new List<MoisBilanSocialDto>(13);

            for (int m = 1; m <= 12; m++)
            {
                var debutMois = new DateTime(filter.Annee, m, 1);
                var finMois   = new DateTime(filter.Annee, m, DateTime.DaysInMonth(filter.Annee, m));

                int effectifFin = salaries.Count(s =>
                    s.DateEmbauche <= finMois &&
                    (s.DateSortie < SortieSentinelle || s.DateSortie > finMois));

                int embauches = salaries.Count(s =>
                    s.DateEmbauche >= debutMois && s.DateEmbauche <= finMois);

                int departs = salaries.Count(s =>
                    s.DateSortie >= SortieSentinelle &&
                    s.DateSortie >= debutMois && s.DateSortie <= finMois);

                var bulletinsMois = bulletins.Where(b => b.Mois == m).ToList();
                decimal masse = bulletinsMois.Sum(b => b.BrutFiscal);
                decimal charges = bulletinsMois
                    .Where(b => lignesParBulletin.ContainsKey(b.Oid))
                    .Sum(b => lignesParBulletin[b.Oid]);

                // Absences : on compte les demandes recouvrant le mois
                var demandesMois = demandes
                    .Where(c => c.DateDebut <= finMois && c.DateFin >= debutMois)
                    .ToList();
                int empAbsents = demandesMois
                    .Where(c => c.Salarie != null)
                    .Select(c => c.Salarie!.Oid).Distinct().Count();
                int joursAbs = (int)Math.Round(demandesMois.Sum(c => ProrataJoursDansMois(c, debutMois, finMois)));

                mois.Add(new MoisBilanSocialDto
                {
                    Mois              = m,
                    Libelle           = CapitalizeFirst(fr.DateTimeFormat.GetAbbreviatedMonthName(m)),
                    EffectifFinMois   = effectifFin,
                    Embauches         = embauches,
                    Departs           = departs,
                    MasseSalariale    = masse,
                    ChargesPatronales = charges,
                    EmployesAbsents   = empAbsents,
                    JoursAbsence      = joursAbs,

                    // Données non disponibles dans le modèle actuel
                    MouvementsEmplois     = null,
                    MesuresDisciplinaires = null,
                    AccidentsTravail      = null,
                    AccidentsTrajet       = null,
                    MaladiesProf          = null,
                    JoursArretTravail     = null,
                    BudgetFormation       = null,
                    HeuresFormation       = null,
                    EmployesFormes        = null
                });
            }

            // ── 3. Ligne TOTAL ─────────────────────────────────────────
            var total = new MoisBilanSocialDto
            {
                Mois              = 0,
                Libelle           = "TOTAL",
                EstTotal          = true,
                EffectifFinMois   = mois.Last().EffectifFinMois,         // effectif au 31/12
                Embauches         = mois.Sum(x => x.Embauches),
                Departs           = mois.Sum(x => x.Departs),
                MasseSalariale    = mois.Sum(x => x.MasseSalariale),
                ChargesPatronales = mois.Sum(x => x.ChargesPatronales),
                EmployesAbsents   = mois.Max(x => x.EmployesAbsents),    // pic
                JoursAbsence      = mois.Sum(x => x.JoursAbsence),
                MouvementsEmplois     = null,
                MesuresDisciplinaires = null,
                AccidentsTravail      = null,
                AccidentsTrajet       = null,
                MaladiesProf          = null,
                JoursArretTravail     = null,
                BudgetFormation       = null,
                HeuresFormation       = null,
                EmployesFormes        = null
            };
            mois.Add(total);

            // ── 4. KPI globaux ─────────────────────────────────────────
            int effectifMoyen = mois.Where(x => !x.EstTotal).Any()
                ? (int)Math.Round(mois.Where(x => !x.EstTotal).Average(x => x.EffectifFinMois))
                : 0;

            decimal denomTaux = effectifMoyen * JoursOuvresAnnee;
            decimal taux = denomTaux > 0
                ? Math.Round(total.JoursAbsence * 100m / denomTaux, 2) : 0m;

            // Complétude data : ratio cellules Effectif/Embauches/Départs/Masse/Charges
            //   non vides sur les 12 mois.
            int cellules = 12 * 5;
            int cellRempli =
                  mois.Take(12).Count(x => x.EffectifFinMois  > 0)
                + mois.Take(12).Count(x => x.Embauches        > 0)
                + mois.Take(12).Count(x => x.Departs          > 0)
                + mois.Take(12).Count(x => x.MasseSalariale   > 0)
                + mois.Take(12).Count(x => x.ChargesPatronales > 0);
            decimal completude = cellules > 0 ? Math.Round(cellRempli * 100m / cellules, 1) : 0m;

            var kpis = new KpiBilanSocialDto
            {
                EffectifMoyenAnnuel        = effectifMoyen,
                EffectifFinAnnee           = total.EffectifFinMois,
                TotalEmbauches             = total.Embauches,
                TotalDeparts               = total.Departs,
                MasseSalarialeAnnuelle     = total.MasseSalariale,
                ChargesPatronalesAnnuelles = total.ChargesPatronales,
                CoutEmployeurAnnuel        = total.CoutEmployeur,
                TotalJoursAbsence          = total.JoursAbsence,
                TauxAbsenteisme            = taux,
                CompletudeData             = completude
            };

            return new BilanSocialDto
            {
                Kpis         = kpis,
                Mois         = mois,
                CalculatedAt = DateTime.Now
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Helpers
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Calcule la part de jours d'absence d'une demande tombant dans
        /// l'intervalle [debutMois, finMois] (au prorata jours calendaires).
        /// </summary>
        private static decimal ProrataJoursDansMois(CongeDemande c, DateTime debutMois, DateTime finMois)
        {
            try
            {
                var d1 = c.DateDebut > debutMois ? c.DateDebut : debutMois;
                var d2 = c.DateFin   < finMois   ? c.DateFin   : finMois;
                if (d2 < d1) return 0m;
                int joursTotal = Math.Max(1, (c.DateFin - c.DateDebut).Days + 1);
                int joursMois  = (d2 - d1).Days + 1;
                return c.DureeJours * joursMois / (decimal)joursTotal;
            }
            catch { return 0m; }
        }

        private static bool IsActif(object o)
        {
            if (o == null) return false;
            try
            {
                var prop = o.GetType().GetProperty("GCRecord");
                if (prop == null) return true;
                return prop.GetValue(o) == null;
            }
            catch { return true; }
        }

        private static string CapitalizeFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Lookups
        // ═════════════════════════════════════════════════════════════════════
        public List<int> GetAnneesDisponibles(IObjectSpace os)
        {
            try
            {
                var anneesBulletin = os.GetObjectsQuery<Bulletin>()
                    .ToList()
                    .Select(b => b.Annee)
                    .Where(a => a > 1900)
                    .Distinct()
                    .OrderByDescending(a => a)
                    .ToList();
                if (anneesBulletin.Count > 0) return anneesBulletin;
            }
            catch { }

            var max = DateTime.Today.Year;
            return Enumerable.Range(max - 7, 8).OrderByDescending(a => a).ToList();
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

        public void InvalidateCache(BilanSocialFilterModel filter)
        {
            if (filter == null) return;
            _cache.Remove(filter.ToCacheKey());
        }
    }
}
