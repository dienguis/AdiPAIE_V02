// =============================================================================
//  MouvementsDashboardService.cs
//  Tableau N°3 (Mouvements — Arrivées / Départs) — implémentation XPO.
//
//  INTERNE (Salarie) :
//    Arrivées = `Salarie.DateEmbauche` dans l'année.
//    Départs  = `Salarie.DateSortie` dans l'année (>= 1900-01-01) + `MotifDepart` enum.
//  EXTERNE (Interimaire) :
//    Mouvements = entité `MouvementInterimaire` (TypeMouvement = enum).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;
using Microsoft.Extensions.Caching.Memory;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <inheritdoc cref="IMouvementsDashboardService"/>
    public sealed class MouvementsDashboardService : IMouvementsDashboardService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly DateTime SortieSentinelle = new(1900, 1, 1);

        public MouvementsDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        // ─────────────────────────────────────────────────────────────────────
        public MouvementsDto GetData(MouvementsFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var key = filter.ToCacheKey();
            if (_cache.TryGetValue<MouvementsDto>(key, out var cached) && cached != null)
                return cached;

            var dto = filter.Personnel == PersonnelType.Externe
                ? ComputeForExterne(filter, os)
                : ComputeForInterne(filter, os);

            _cache.Set(key, dto, CacheTtl);
            return dto;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  INTERNE — Salarie
        // ═════════════════════════════════════════════════════════════════════
        private MouvementsDto ComputeForInterne(MouvementsFilterModel filter, IObjectSpace os)
        {
            var debutAnnee = new DateTime(filter.Annee, 1, 1);
            var finAnnee   = new DateTime(filter.Annee, 12, 31);

            // Salariés à filtrer côté SQL (Site / Genre / Catégorie)
            IEnumerable<Salarie> qSql = os.GetObjectsQuery<Salarie>();
            if (filter.SiteOid.HasValue)
                qSql = qSql.Where(s => s.Site != null && s.Site.Oid == filter.SiteOid.Value);
            if (filter.Genre.HasValue)
                qSql = qSql.Where(s => s.Sexe == filter.Genre.Value);
            if (filter.CategorieOid.HasValue)
                qSql = qSql.Where(s => s.Categories != null && s.Categories.Oid == filter.CategorieOid.Value);
            var allSalaries = qSql.ToList();

            // ── Arrivées : Salarié dont DateEmbauche est dans l'année ────────
            var arrivees = allSalaries
                .Where(s => s.DateEmbauche >= debutAnnee && s.DateEmbauche <= finAnnee)
                .ToList();

            // ── Départs : Salarié dont DateSortie est dans l'année + sentinelle filter
            var departs = allSalaries
                .Where(s => s.DateSortie >= SortieSentinelle &&
                            s.DateSortie >= debutAnnee &&
                            s.DateSortie <= finAnnee)
                .ToList();

            // ── Effectif début / fin année (pour les taux) ───────────────────
            int effectifDebut = allSalaries.Count(s =>
                s.DateEmbauche <= debutAnnee &&
                (s.DateSortie < SortieSentinelle || s.DateSortie > debutAnnee));
            int effectifFin = allSalaries.Count(s =>
                s.DateEmbauche <= finAnnee &&
                (s.DateSortie < SortieSentinelle || s.DateSortie > finAnnee));
            decimal effectifMoyen = (effectifDebut + effectifFin) / 2m;

            // ── KPI ──────────────────────────────────────────────────────────
            var kpis = new KpiMouvementsDto
            {
                NbArrivees    = arrivees.Count,
                NbDeparts     = departs.Count,
                EffectifDebut = effectifDebut,
                EffectifFin   = effectifFin,
                TauxArrivees  = effectifMoyen > 0
                    ? Round1((decimal)arrivees.Count * 100m / effectifMoyen) : 0m,
                TauxDeparts   = effectifMoyen > 0
                    ? Round1((decimal)departs.Count * 100m / effectifMoyen) : 0m,
            };

            // ── Bar charts ──────────────────────────────────────────────────
            var arrivParMois  = ComputeParMois(arrivees, s => s.DateEmbauche);
            var departsParMois = ComputeParMois(departs, s => s.DateSortie);

            var arrivParSite      = ComputeBar(arrivees, s => s.Site?.Nom ?? "(Non renseigné)");
            var arrivParCategorie = ComputeBar(arrivees, s => s.Categories?.Intitule ?? "(Non renseignée)");

            var departsParSite      = ComputeBar(departs, s => s.Site?.Nom ?? "(Non renseigné)");
            var departsParCategorie = ComputeBar(departs, s => s.Categories?.Intitule ?? "(Non renseignée)");

            // Motif du départ (enum MotifDepart) — converti en libellé lisible
            var departsParMotif = ComputeBar(departs, s => GetMotifDepartLibelle(s.MotifDepart));

            return new MouvementsDto
            {
                Kpis                = kpis,
                ArriveesParMois     = arrivParMois,
                ArriveesParSite     = arrivParSite,
                ArriveesParCategorie= arrivParCategorie,
                DepartsParMois      = departsParMois,
                DepartsParMotif     = departsParMotif,
                DepartsParSite      = departsParSite,
                DepartsParCategorie = departsParCategorie,
                CalculatedAt        = DateTime.Now
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  EXTERNE — Interimaire / MouvementInterimaire
        // ═════════════════════════════════════════════════════════════════════
        private MouvementsDto ComputeForExterne(MouvementsFilterModel filter, IObjectSpace os)
        {
            var debutAnnee = new DateTime(filter.Annee, 1, 1);
            var finAnnee   = new DateTime(filter.Annee, 12, 31);

            // Tous les MouvementInterimaire de l'année
            var mouvements = os.GetObjectsQuery<MouvementInterimaire>()
                .ToList()
                .Where(m => m.DateMouvement >= debutAnnee && m.DateMouvement <= finAnnee)
                .ToList();

            // Filtre Site (StationService) — on filtre par StationDestination ET StationOrigine
            if (filter.SiteOid.HasValue)
            {
                mouvements = mouvements.Where(m =>
                    m.StationDestination?.Oid == filter.SiteOid.Value ||
                    m.StationOrigine?.Oid == filter.SiteOid.Value).ToList();
            }

            // Heuristique : on classe en arrivée vs départ via TypeMouvement.ToString()
            //   - Si "Affect", "Arriv", "Embauche" → arrivée
            //   - Si "Depart", "Sortie", "Fin"     → départ
            //   - Sinon : on les met en départ par défaut (mouvement quelconque)
            //
            // Cas des CONTRATS : on ajoute aussi
            //   - Arrivée  = ContratInterim.DateDebut dans l'année
            //   - Départ   = ContratInterim.Statut Resilie/Termine + DateFinReelle dans l'année
            var contratsAnnee = os.GetObjectsQuery<ContratInterim>()
                .ToList();

            var nouveauxContrats = contratsAnnee
                .Where(c => c.DateDebut >= debutAnnee && c.DateDebut <= finAnnee)
                .ToList();

            var contratsClotures = contratsAnnee
                .Where(c =>
                    (c.Statut == ContratInterimStatut.Resilie ||
                     c.Statut == ContratInterimStatut.Termine) &&
                    (c.DateFinReelle ?? c.DateFin) >= debutAnnee &&
                    (c.DateFinReelle ?? c.DateFin) <= finAnnee)
                .ToList();

            int effectifDebut = CountInterimsActifs(os, debutAnnee, filter);
            int effectifFin   = CountInterimsActifs(os, finAnnee, filter);

            // ── Dénominateur des taux EXTERNE (logique anti-aberration) ──────
            //   1) ETP pondéré : moyenne sur 13 dates (1er du mois + 31/12).
            //      Donne le bon KPI quand la population est stable.
            //   2) Nb distinct d'intérimaires ayant eu au moins un contrat
            //      actif pendant l'année : « taille effective » de l'équipe
            //      au sens « combien de personnes différentes sont passées ».
            //
            //   On prend le MAX des deux : pour une année de ramp-up où tous
            //   les contrats démarrent en cours d'année (effectif au 01/01 = 0),
            //   l'ETP pondéré est artificiellement bas et fait exploser le taux.
            //   Le « nb distinct » sert alors de plancher cohérent (taux ≈ 100 %
            //   = « renouvellement total de l'équipe »).
            decimal etpPondere     = ComputeEffectifMoyenPondere(os, filter.Annee, filter);
            int     nbInterimsAnnee = CountInterimsAyantContratDansAnnee(os, debutAnnee, finAnnee, filter);
            decimal effectifMoyen   = Math.Max(etpPondere, (decimal)nbInterimsAnnee);

            var kpis = new KpiMouvementsDto
            {
                NbArrivees    = nouveauxContrats.Count,
                NbDeparts     = contratsClotures.Count,
                EffectifDebut = effectifDebut,
                EffectifFin   = effectifFin,
                TauxArrivees  = effectifMoyen > 0
                    ? Round1((decimal)nouveauxContrats.Count * 100m / effectifMoyen) : 0m,
                TauxDeparts   = effectifMoyen > 0
                    ? Round1((decimal)contratsClotures.Count * 100m / effectifMoyen) : 0m,
            };

            var arrivParMois  = ComputeParMois(nouveauxContrats, c => c.DateDebut);
            var departsParMois = ComputeParMois(contratsClotures, c => c.DateFinReelle ?? c.DateFin);

            var arrivParSite      = ComputeBar(nouveauxContrats, c => c.Station?.Nom ?? "(Non renseigné)");
            var arrivParCategorie = ComputeBar(nouveauxContrats, c => c.PosteOccupe?.Libelle ?? "(Non renseigné)");

            var departsParSite      = ComputeBar(contratsClotures, c => c.Station?.Nom ?? "(Non renseigné)");
            var departsParCategorie = ComputeBar(contratsClotures, c => c.PosteOccupe?.Libelle ?? "(Non renseigné)");

            // Pour les motifs : on récupère MouvementInterimaire de type "Départ" et leur Motif
            //   (texte libre). À défaut on regroupe par Statut du contrat clôturé.
            var departsParMotif = ComputeBar(contratsClotures, c => c.Statut.ToString());

            return new MouvementsDto
            {
                Kpis                = kpis,
                ArriveesParMois     = arrivParMois,
                ArriveesParSite     = arrivParSite,
                ArriveesParCategorie= arrivParCategorie,
                DepartsParMois      = departsParMois,
                DepartsParMotif     = departsParMotif,
                DepartsParSite      = departsParSite,
                DepartsParCategorie = departsParCategorie,
                CalculatedAt        = DateTime.Now
            };
        }

        private static int CountInterimsActifs(IObjectSpace os, DateTime atDate, MouvementsFilterModel filter)
        {
            var q = os.GetObjectsQuery<Interimaire>().ToList();
            return q.Count(i =>
            {
                try
                {
                    return i.Contrats != null && i.Contrats.Any(c =>
                        c.DateDebut > SortieSentinelle &&
                        c.DateDebut <= atDate &&
                        (c.DateFin < SortieSentinelle || c.DateFin > atDate) &&
                        (!filter.SiteOid.HasValue || c.Station?.Oid == filter.SiteOid.Value));
                }
                catch { return false; }
            });
        }

        /// <summary>
        /// Compte le nombre distinct d'intérimaires ayant eu **au moins un contrat
        /// actif** entre <paramref name="debut"/> et <paramref name="fin"/>
        /// (recouvrement non vide). Sert de plancher au dénominateur des taux
        /// pour éviter les % aberrants en année de ramp-up.
        /// </summary>
        private static int CountInterimsAyantContratDansAnnee(
            IObjectSpace os, DateTime debut, DateTime fin, MouvementsFilterModel filter)
        {
            var allInterims = os.GetObjectsQuery<Interimaire>().ToList();
            return allInterims.Count(i =>
            {
                try
                {
                    return i.Contrats != null && i.Contrats.Any(c =>
                        // Recouvrement contrat ↔ [debut, fin]
                        c.DateDebut > SortieSentinelle &&
                        c.DateDebut <= fin &&
                        (c.DateFin < SortieSentinelle || c.DateFin >= debut) &&
                        (!filter.SiteOid.HasValue || c.Station?.Oid == filter.SiteOid.Value));
                }
                catch { return false; }
            });
        }

        /// <summary>
        /// Effectif moyen EXTERNE pondéré sur 13 dates clés (1er de chaque mois +
        /// 31/12). Approxime l'« ETP intérimaires » sur l'année — beaucoup plus
        /// fiable que (debut+fin)/2 quand la population varie fortement.
        /// </summary>
        private static decimal ComputeEffectifMoyenPondere(IObjectSpace os, int annee, MouvementsFilterModel filter)
        {
            var dates = new List<DateTime>(13);
            for (int m = 1; m <= 12; m++) dates.Add(new DateTime(annee, m, 1));
            dates.Add(new DateTime(annee, 12, 31));

            // On précharge la liste une seule fois pour éviter 13 requêtes SQL.
            var allInterims = os.GetObjectsQuery<Interimaire>().ToList();

            int total = 0;
            foreach (var d in dates)
            {
                total += allInterims.Count(i =>
                {
                    try
                    {
                        return i.Contrats != null && i.Contrats.Any(c =>
                            c.DateDebut > SortieSentinelle &&
                            c.DateDebut <= d &&
                            (c.DateFin < SortieSentinelle || c.DateFin > d) &&
                            (!filter.SiteOid.HasValue || c.Station?.Oid == filter.SiteOid.Value));
                    }
                    catch { return false; }
                });
            }
            return (decimal)total / dates.Count;   // moyenne sur 13 points
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Helpers communs
        // ═════════════════════════════════════════════════════════════════════
        private static List<BarItemDto> ComputeParMois<T>(IList<T> source, Func<T, DateTime> dateSelector)
        {
            // 12 mois fixes (Jan → Déc)
            var fr = CultureInfo.GetCultureInfo("fr-FR");
            var counts = new int[12];
            int total = 0;
            foreach (var s in source)
            {
                try
                {
                    var d = dateSelector(s);
                    if (d.Year < 1900) continue;
                    counts[d.Month - 1]++;
                    total++;
                }
                catch { }
            }
            return Enumerable.Range(1, 12).Select(m => new BarItemDto
            {
                Libelle    = fr.DateTimeFormat.GetAbbreviatedMonthName(m),
                Valeur     = counts[m - 1],
                Pourcentage = total > 0 ? Round1((decimal)counts[m - 1] * 100m / total) : 0m
            }).ToList();
        }

        private static List<BarItemDto> ComputeBar<T>(IList<T> source, Func<T, string> labelSelector)
        {
            var total = source.Count;
            return source
                .Select(labelSelector)
                .GroupBy(l => l)
                .OrderByDescending(g => g.Count())
                .Select(g => new BarItemDto
                {
                    Libelle    = g.Key,
                    Valeur     = g.Count(),
                    Pourcentage = total > 0 ? Round1((decimal)g.Count() * 100m / total) : 0m
                })
                .ToList();
        }

        private static string GetMotifDepartLibelle(MotifDepart? m) => m switch
        {
            MotifDepart.Demission             => "Démission",
            MotifDepart.Licenciement          => "Licenciement",
            MotifDepart.FinCDD                => "Fin de CDD",
            MotifDepart.Retraite              => "Retraite",
            MotifDepart.Deces                 => "Décès",
            MotifDepart.RuptureConventionnelle=> "Rupture conventionnelle",
            null                              => "(Non renseigné)",
            _                                 => "Autre"
        };

        private static decimal Round1(decimal v) => Math.Round(v, 1);

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
                return actifs.Count > 0 ? actifs
                    : os.GetObjectsQuery<Site>().OrderBy(s => s.Nom).ToList();
            }
            catch { return new List<Site>(); }
        }

        public List<StationService> GetStationsServiceActives(IObjectSpace os)
        {
            try
            {
                var actifs = os.GetObjectsQuery<StationService>().Where(s => s.Actif).OrderBy(s => s.Nom).ToList();
                return actifs.Count > 0 ? actifs
                    : os.GetObjectsQuery<StationService>().OrderBy(s => s.Nom).ToList();
            }
            catch { return new List<StationService>(); }
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

        public void InvalidateCache(MouvementsFilterModel filter)
        {
            if (filter == null) return;
            _cache.Remove(filter.ToCacheKey());
        }
    }
}
