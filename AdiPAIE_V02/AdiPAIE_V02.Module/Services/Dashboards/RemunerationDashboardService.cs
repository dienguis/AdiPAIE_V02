// =============================================================================
//  RemunerationDashboardService.cs
//  Tableau N°4 (Rémunération — Égalité des salaires) — implémentation XPO.
//
//  INTERNE :
//    Source = Bulletin (filtré par Annee + GCRecord IS NULL)
//    Mode CoutEmployeur     = SUM(BrutFiscal) + SUM(BulletinLigne.MontantEmployeur)
//    Mode RemunerationNette = SUM(NetAPayer)
//
//  EXTERNE :
//    Source = ContratInterim (intersect avec l'année)
//    Coût   = TauxJournalier × 22 jours × NbMois (DateDebut..min(DateFin, finAnnee))
//    Le mode Rémunération Nette n'a pas de sens pour intérimaires : on
//    affiche le même Coût (les filtres détaillés H/F sont également no-op
//    car Interimaire n'a pas de Sexe).
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
    /// <inheritdoc cref="IRemunerationDashboardService"/>
    public sealed class RemunerationDashboardService : IRemunerationDashboardService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly DateTime SortieSentinelle = new(1900, 1, 1);
        private const int JoursOuvresParMois = 22;

        public RemunerationDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        // ─────────────────────────────────────────────────────────────────────
        public RemunerationDto GetData(RemunerationFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var key = filter.ToCacheKey();
            if (_cache.TryGetValue<RemunerationDto>(key, out var cached) && cached != null)
                return cached;

            var dto = filter.Personnel == PersonnelType.Externe
                ? ComputeForExterne(filter, os)
                : ComputeForInterne(filter, os);

            _cache.Set(key, dto, CacheTtl);
            return dto;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  INTERNE — Bulletin / BulletinLigne / Salarie
        // ═════════════════════════════════════════════════════════════════════
        private RemunerationDto ComputeForInterne(RemunerationFilterModel filter, IObjectSpace os)
        {
            // ── 1. Récupère tous les bulletins de l'année (GCRecord IS NULL) ─
            //   Note : les bulletins XPO peuvent porter une propriété GCRecord
            //   nullable (soft-delete). On filtre côté mémoire pour rester
            //   défensif si la propriété change de nom.
            var allBulletins = os.GetObjectsQuery<Bulletin>()
                .ToList()
                .Where(b => b.Annee == filter.Annee && IsActif(b))
                .ToList();

            // ── 2. Filtres optionnels via Salarie ─────────────────────────
            if (filter.SiteOid.HasValue)
                allBulletins = allBulletins.Where(b => b.Salarie?.Site?.Oid == filter.SiteOid.Value).ToList();
            if (filter.Genre.HasValue)
                allBulletins = allBulletins.Where(b => b.Salarie?.Sexe == filter.Genre.Value).ToList();
            if (filter.CategorieOid.HasValue)
                allBulletins = allBulletins.Where(b => b.Salarie?.Categories?.Oid == filter.CategorieOid.Value).ToList();
            if (filter.EchelonOid.HasValue) // V1.5
                allBulletins = allBulletins.Where(b => b.Salarie?.Echelon?.Oid == filter.EchelonOid.Value).ToList();
            if (!string.IsNullOrWhiteSpace(filter.Segment))
                allBulletins = allBulletins.Where(b => SafeDepartementNom(b.Salarie) == filter.Segment).ToList();

            // ── 3. Charges patronales globales (SUM(BulletinLigne.MontantEmployeur)) ─
            decimal totalCharges = 0m;
            try
            {
                totalCharges = os.GetObjectsQuery<BulletinLigne>()
                    .ToList()
                    .Where(bl => bl.Bulletin != null
                              && bl.Bulletin.Annee == filter.Annee
                              && IsActif(bl)
                              && IsActif(bl.Bulletin)
                              && allBulletins.Any(b => b.Oid == bl.Bulletin.Oid))
                    .Sum(bl => bl.MontantEmployeur);
            }
            catch { totalCharges = 0m; }

            // ── 4. Choix de la métrique selon le Mode ────────────────────
            //   - CoutEmployeur     = BrutFiscal + (chargesPatronales prorata)
            //   - RemunerationNette = NetAPayer
            decimal SelectMontant(Bulletin b) => filter.Mode switch
            {
                RemunerationMode.RemunerationNette => b.NetAPayer,
                _                                  => b.BrutFiscal
            };

            // ── 5. KPI globaux ───────────────────────────────────────────
            var montants = allBulletins.Select(SelectMontant).ToList();
            decimal totalBrut = montants.Sum();
            decimal totalCoutEmployeur = totalBrut + totalCharges;
            decimal total = filter.Mode == RemunerationMode.CoutEmployeur ? totalCoutEmployeur : totalBrut;

            int nbSalariesDistincts = allBulletins
                .Where(b => b.Salarie != null)
                .Select(b => b.Salarie!.Oid).Distinct().Count();
            int nbBulletins = allBulletins.Count;

            var kpis = new KpiRemunerationDto
            {
                Total               = total,
                SalaireMin          = montants.Count > 0 ? montants.Min() : 0m,
                SalaireMax          = montants.Count > 0 ? montants.Max() : 0m,
                SalaireMoyen        = montants.Count > 0 ? montants.Average() : 0m,
                CoutMoyenSalarie    = nbSalariesDistincts > 0 ? Math.Round(total / nbSalariesDistincts, 0) : 0m,
                NbSalariesDistincts = nbSalariesDistincts,
                NbBulletins         = nbBulletins
            };

            // ── 6. Tableau « Égalité par Segment » ───────────────────────
            //   Un segment = Salarie.Departement.Nom (cf. Tab 2).
            var rowsSegment = allBulletins
                .GroupBy(b => SafeDepartementNom(b.Salarie) ?? "(Non renseigné)")
                .Select(g => BuildEgaliteRow(g.Key, g.ToList(), SelectMontant, totalBrut))
                .OrderByDescending(r => r.Total)
                .ToList();

            // ── 7. Tableau « Égalité par Catégorie » ─────────────────────
            var rowsCategorie = allBulletins
                .GroupBy(b => b.Salarie?.Categories?.Intitule ?? "(Non renseignée)")
                .Select(g => BuildEgaliteRow(g.Key, g.ToList(), SelectMontant, totalBrut))
                .OrderByDescending(r => r.Total)
                .ToList();

            // ── 7b. V1.5 — Tableau « Égalité par Échelon » ──────────────
            var rowsEchelon = allBulletins
                .GroupBy(b => b.Salarie?.Echelon?.DisplayName ?? "(Non renseigné)")
                .Select(g => BuildEgaliteRow(g.Key, g.ToList(), SelectMontant, totalBrut))
                .OrderByDescending(r => r.Total)
                .ToList();

            // ── 8. Évolution mensuelle (12 mois Jan→Déc) ─────────────────
            var fr = CultureInfo.GetCultureInfo("fr-FR");
            var evolution = Enumerable.Range(1, 12).Select(m =>
            {
                var subset = allBulletins.Where(b => b.Mois == m).ToList();
                return new MasseMensuelleDto
                {
                    Mois        = m,
                    Libelle     = fr.DateTimeFormat.GetAbbreviatedMonthName(m),
                    Masse       = subset.Sum(SelectMontant),
                    NbBulletins = subset.Count
                };
            }).ToList();

            // ── 9. Décomposition par famille de rubrique ─────────────────
            var decomposition = ComputeDecompositionRubriques(os, filter.Annee, allBulletins);

            return new RemunerationDto
            {
                Kpis              = kpis,
                ParSegment        = rowsSegment,
                ParCategorie      = rowsCategorie,
                ParEchelon        = rowsEchelon, // V1.5
                EvolutionMensuelle = evolution,
                ParFamilleRubrique = decomposition,
                CalculatedAt       = DateTime.Now
            };
        }

        /// <summary>
        /// Construit une ligne « Égalité » à partir d'un groupe de bulletins.
        /// </summary>
        private static EgaliteSalaireRowDto BuildEgaliteRow(
            string libelle, List<Bulletin> bulletins, Func<Bulletin, decimal> select, decimal totalGlobal)
        {
            var montants = bulletins.Select(select).ToList();
            var nbDistincts = bulletins.Where(b => b.Salarie != null)
                                       .Select(b => b.Salarie!.Oid).Distinct().Count();
            var hommes = bulletins.Where(b => b.Salarie?.Sexe == Sexe.Masculin).Select(select).ToList();
            var femmes = bulletins.Where(b => b.Salarie?.Sexe == Sexe.Feminin ).Select(select).ToList();
            var sum = montants.Sum();

            return new EgaliteSalaireRowDto
            {
                Libelle         = libelle,
                Total           = sum,
                CoutMoyen       = nbDistincts > 0 ? Math.Round(sum / nbDistincts, 0) : 0m,
                Min             = montants.Count > 0 ? montants.Min() : 0m,
                Max             = montants.Count > 0 ? montants.Max() : 0m,
                Moyenne         = montants.Count > 0 ? Math.Round(montants.Average(), 0) : 0m,
                MoyHomme        = hommes.Count > 0 ? Math.Round(hommes.Average(), 0) : (decimal?)null,
                MoyFemme        = femmes.Count > 0 ? Math.Round(femmes.Average(), 0) : (decimal?)null,
                NbSalaries      = nbDistincts,
                PourcentageMasse = totalGlobal > 0 ? Math.Round(sum * 100m / totalGlobal, 1) : 0m
            };
        }

        /// <summary>Décomposition en 7 familles macro selon RubriqueTypeRef.Code.</summary>
        private static List<DecompositionRubriqueDto> ComputeDecompositionRubriques(
            IObjectSpace os, int annee, List<Bulletin> bulletinsFiltres)
        {
            try
            {
                var lignes = os.GetObjectsQuery<BulletinLigne>()
                    .ToList()
                    .Where(bl => bl.Bulletin != null
                              && bl.Bulletin.Annee == annee
                              && IsActif(bl) && IsActif(bl.Bulletin)
                              && bulletinsFiltres.Any(b => b.Oid == bl.Bulletin.Oid))
                    .ToList();

                return lignes
                    .GroupBy(bl => GetFamilleMacro(bl))
                    .Select(g => new DecompositionRubriqueDto
                    {
                        FamilleMacro    = g.Key,
                        TotalSalarial   = g.Sum(x => x.Montant),
                        TotalEmployeur  = g.Sum(x => x.MontantEmployeur),
                        TotalCombined   = g.Sum(x => x.Montant + x.MontantEmployeur)
                    })
                    .OrderBy(d => d.FamilleMacro)
                    .ToList();
            }
            catch { return new List<DecompositionRubriqueDto>(); }
        }

        /// <summary>Mapping des codes RubriqueTypeRef vers 7 familles macro.</summary>
        private static string GetFamilleMacro(BulletinLigne bl)
        {
            string code = "";
            try { code = bl.Rubrique?.TypeRef?.Code ?? ""; }
            catch { code = ""; }

            return code switch
            {
                "BRUTE"                                                  => "1. Salaire de base & primes",
                "INDEM_IMPOSA"                                           => "2. Indemnités imposables",
                "INDEM_NON_IMPOSA"                                       => "3. Indemnités non imposables",
                "AV_NATURE_IMPOSABLE" or "AvNatImpos"
                or "AV_NATURE_NON_IMPOSABLE"                             => "4. Avantages en nature",
                "COTSOC"                                                 => "5. Cotisations sociales",
                "COTFISC"                                                => "6. Cotisations fiscales",
                "RETENUE"                                                => "7. Retenues diverses",
                _                                                        => "8. (Non défini)"
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  EXTERNE — ContratInterim
        //  V1.1 (Sprint 1C) : utilise le nouveau modèle Site + Unités au lieu
        //  de Station + BU. Les contrats sans Site V1.1 ne sont pas comptés
        //  (le seed démo + les nouvelles saisies utilisent forcément Site V1.1).
        // ═════════════════════════════════════════════════════════════════════
        private RemunerationDto ComputeForExterne(RemunerationFilterModel filter, IObjectSpace os)
        {
            var debutAnnee = new DateTime(filter.Annee, 1, 1);
            var finAnnee   = new DateTime(filter.Annee, 12, 31);
            var aujourdhui = DateTime.Today;

            // ── 1. Récupère les contrats qui se chevauchent avec l'année ─
            //   V1.1 : on filtre uniquement les contrats avec un Site V1.1
            //   défini. Les contrats avec Station legacy (sans Site V1.1)
            //   apparaîtront vides dans le dashboard tant qu'ils ne sont
            //   pas migrés.
            var contrats = os.GetObjectsQuery<ContratInterim>().ToList()
                .Where(c => c.Site != null
                         && c.DateDebut > SortieSentinelle
                         && c.DateDebut <= finAnnee
                         && (c.DateFin < SortieSentinelle || c.DateFin >= debutAnnee))
                .ToList();

            // ── 2. Filtres V1.1 (Site + Unite au lieu de Station + Categorie) ─
            if (filter.SiteOid.HasValue)
                contrats = contrats.Where(c => c.Site?.Oid == filter.SiteOid.Value).ToList();
            // CategorieOid réutilisé pour filtrer par UniteOrganisationnelle
            if (filter.CategorieOid.HasValue)
                contrats = contrats.Where(c => c.Unites != null
                                            && c.Unites.Any(u => u.Oid == filter.CategorieOid.Value))
                                   .ToList();

            // ── 3. Calcul du coût par contrat (formule validée user) ─────
            //   coût = TauxJournalier × 22 × DATEDIFF(month, DateDebut, COALESCE(DateFin, today))
            decimal CoutContrat(ContratInterim c)
            {
                var dEnd = (c.DateFin > SortieSentinelle ? c.DateFin : aujourdhui);
                if (dEnd > finAnnee) dEnd = finAnnee;
                var dStart = c.DateDebut < debutAnnee ? debutAnnee : c.DateDebut;
                int nbMois = Math.Max(0, ((dEnd.Year - dStart.Year) * 12) + dEnd.Month - dStart.Month + 1);
                return c.TauxJournalier * JoursOuvresParMois * nbMois;
            }

            var couts = contrats.Select(CoutContrat).ToList();

            // ── 4. KPI globaux ───────────────────────────────────────────
            int nbInterimDistincts = contrats
                .Where(c => c.Interimaire != null)
                .Select(c => c.Interimaire!.Oid).Distinct().Count();

            decimal total = couts.Sum();
            var kpis = new KpiRemunerationDto
            {
                Total               = total,
                SalaireMin          = couts.Count > 0 ? couts.Min() : 0m,
                SalaireMax          = couts.Count > 0 ? couts.Max() : 0m,
                SalaireMoyen        = couts.Count > 0 ? Math.Round(couts.Average(), 0) : 0m,
                CoutMoyenSalarie    = nbInterimDistincts > 0 ? Math.Round(total / nbInterimDistincts, 0) : 0m,
                NbSalariesDistincts = nbInterimDistincts,
                NbBulletins         = contrats.Count
            };

            // ── 5. Tableau Segment = Site (V1.1) ────────────────────────
            //   Avant : station service uniquement. Maintenant : tous les
            //   sites (Stations + Siège + Dépôts) avec leur libellé enrichi
            //   du Type entre crochets pour distinguer.
            var rowsSegment = contrats
                .GroupBy(c => SiteLibelle(c.Site))
                .Select(g => BuildEgaliteRowExterne(g.Key, g.ToList(), CoutContrat, total))
                .OrderByDescending(r => r.Total)
                .ToList();

            // ── 6. Tableau Catégorie = Unité organisationnelle (V1.1) ───
            //   Multi-affectation : un contrat peut être lié à plusieurs unités
            //   → on EXPLOSE les contrats par unité (un contrat compte dans
            //   chaque unité à laquelle il est rattaché).
            //   Les contrats sans unité tombent dans "(Non renseignée)".
            var contratsParUnite = contrats
                .SelectMany(c =>
                {
                    if (c.Unites == null || c.Unites.Count == 0)
                        return new[] { (Unite: (string)null, Contrat: c) };
                    return c.Unites.Select(u => (Unite: u.Nom, Contrat: c));
                })
                .ToList();

            var rowsCategorie = contratsParUnite
                .GroupBy(x => x.Unite ?? "(Non renseignée)")
                .Select(g => BuildEgaliteRowExterne(
                    g.Key,
                    g.Select(x => x.Contrat).ToList(),
                    CoutContrat, total))
                .OrderByDescending(r => r.Total)
                .ToList();

            // ── 7. Évolution mensuelle (répartit le coût sur les mois actifs) ─
            var fr = CultureInfo.GetCultureInfo("fr-FR");
            var evolution = Enumerable.Range(1, 12).Select(mois =>
            {
                var debutMois = new DateTime(filter.Annee, mois, 1);
                var finMois   = new DateTime(filter.Annee, mois, DateTime.DaysInMonth(filter.Annee, mois));
                decimal masseMois = contrats
                    .Where(c => c.DateDebut <= finMois
                             && (c.DateFin < SortieSentinelle || c.DateFin >= debutMois))
                    .Sum(c => c.TauxJournalier * JoursOuvresParMois);
                return new MasseMensuelleDto
                {
                    Mois        = mois,
                    Libelle     = fr.DateTimeFormat.GetAbbreviatedMonthName(mois),
                    Masse       = masseMois,
                    NbBulletins = contrats.Count(c => c.DateDebut <= finMois
                                                   && (c.DateFin < SortieSentinelle || c.DateFin >= debutMois))
                };
            }).ToList();

            return new RemunerationDto
            {
                Kpis              = kpis,
                ParSegment        = rowsSegment,
                ParCategorie      = rowsCategorie,
                EvolutionMensuelle = evolution,
                ParFamilleRubrique = new List<DecompositionRubriqueDto>(),
                CalculatedAt       = DateTime.Now
            };
        }

        /// <summary>
        /// Libellé enrichi d'un Site avec emoji typé devant le nom :
        ///   🏪 BANDIA  /  🏢 Siège ELTON  /  📦 Dépôt Dakar
        /// Permet à la lecture du dashboard de distinguer immédiatement
        /// le type de site sans devoir aller voir la fiche.
        /// </summary>
        private static string SiteLibelle(Site site)
        {
            if (site == null) return "(Non renseigné)";
            string emoji = site.Type switch
            {
                TypeSite.StationService => "🏪",
                TypeSite.Siege          => "🏢",
                TypeSite.Depot          => "📦",
                _                       => "🏭"
            };
            return $"{emoji} {site.Nom}";
        }

        private static EgaliteSalaireRowDto BuildEgaliteRowExterne(
            string libelle, List<ContratInterim> contrats, Func<ContratInterim, decimal> select, decimal totalGlobal)
        {
            var montants = contrats.Select(select).ToList();
            int nbDistincts = contrats.Where(c => c.Interimaire != null)
                                      .Select(c => c.Interimaire!.Oid).Distinct().Count();
            var sum = montants.Sum();

            return new EgaliteSalaireRowDto
            {
                Libelle          = libelle,
                Total            = sum,
                CoutMoyen        = nbDistincts > 0 ? Math.Round(sum / nbDistincts, 0) : 0m,
                Min              = montants.Count > 0 ? montants.Min() : 0m,
                Max              = montants.Count > 0 ? montants.Max() : 0m,
                Moyenne          = montants.Count > 0 ? Math.Round(montants.Average(), 0) : 0m,
                MoyHomme         = null,    // Interimaire n'a pas de Sexe
                MoyFemme         = null,
                NbSalaries       = nbDistincts,
                PourcentageMasse = totalGlobal > 0 ? Math.Round(sum * 100m / totalGlobal, 1) : 0m
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Helpers
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Filtre soft-delete XPO : retourne true si l'objet n'est pas marqué
        /// supprimé (GCRecord IS NULL). Lookup par réflexion pour rester
        /// compatible avec ou sans la propriété.
        /// </summary>
        private static bool IsActif(object o)
        {
            if (o == null) return false;
            try
            {
                var prop = o.GetType().GetProperty("GCRecord");
                if (prop == null) return true;
                var v = prop.GetValue(o);
                return v == null;
            }
            catch { return true; }
        }

        /// <summary>Accès défensif au Département (Salarie) — propriété optionnelle.</summary>
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

            // Fallback : 8 dernières années
            var max = DateTime.Today.Year;
            return Enumerable.Range(max - 7, 8).OrderByDescending(a => a).ToList();
        }

        /// <summary>
        /// V1.1 — retourne tous les sites actifs triés par type
        /// (Stations en premier, puis Siège, puis Dépôts) pour faciliter
        /// la navigation dans les dropdowns dashboards.
        /// </summary>
        public List<Site> GetSitesActifs(IObjectSpace os)
        {
            try
            {
                // V1.1 — tri par TypeSite (Stations en 1er, puis Siège, puis Dépôts) puis Nom
                var actifs = os.GetObjectsQuery<Site>().Where(s => s.Actif)
                    .ToList()
                    .OrderBy(s => (int)s.Type)        // StationService=0, Siege=1, Depot=2, Autre=99
                    .ThenBy(s => s.Nom)
                    .ToList();
                if (actifs.Count > 0) return actifs;
                return os.GetObjectsQuery<Site>()
                    .ToList()
                    .OrderBy(s => (int)s.Type).ThenBy(s => s.Nom).ToList();
            }
            catch { return new List<Site>(); }
        }

        public List<StationService> GetStationsServiceActives(IObjectSpace os)
        {
            try
            {
                var actifs = os.GetObjectsQuery<StationService>().Where(s => s.Actif).OrderBy(s => s.Nom).ToList();
                return actifs.Count > 0 ? actifs : os.GetObjectsQuery<StationService>().OrderBy(s => s.Nom).ToList();
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

        public List<PosteInterimaire> GetPostesInterimaire(IObjectSpace os)
        {
            try { return os.GetObjectsQuery<PosteInterimaire>().OrderBy(p => p.Libelle).ToList(); }
            catch { return new List<PosteInterimaire>(); }
        }

        /// <summary>
        /// V1.1 — retourne les unités organisationnelles. Si siteOid donné,
        /// filtre uniquement celles rattachées à ce site. Sinon retourne tout.
        /// </summary>
        public List<UniteOrganisationnelle> GetUnitesPourSite(IObjectSpace os, Guid? siteOid)
        {
            try
            {
                var q = os.GetObjectsQuery<UniteOrganisationnelle>().Where(u => u.Actif);
                if (siteOid.HasValue)
                    q = q.Where(u => u.Site != null && u.Site.Oid == siteOid.Value);
                return q.OrderBy(u => u.Ordre).ThenBy(u => u.Nom).ToList();
            }
            catch { return new List<UniteOrganisationnelle>(); }
        }

        public void InvalidateCache(RemunerationFilterModel filter)
        {
            if (filter == null) return;
            _cache.Remove(filter.ToCacheKey());
        }
    }
}
