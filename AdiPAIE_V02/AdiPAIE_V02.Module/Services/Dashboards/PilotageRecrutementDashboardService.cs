// =============================================================================
//  PilotageRecrutementDashboardService.cs — V1.4 (mai 2026)
//
//  Calcule le Dashboard N°12 Pilotage Recrutement à partir des entités :
//    PosteVacant, Candidat, Candidature, Entretien, OffreEmploi, PeriodeEssai.
//
//  KPIs principaux :
//    - Activité : postes ouverts, pourvus, candidatures actives, embauches YTD
//    - Performance : délai moyen recrutement, taux conversion, taux acceptation
//    - Coût : coût total + coût par embauche (Σ coûtSource × nbCandidats)
//    - Période d'essai : en cours, concluantes, rompues, taux rétention
//
//  Funnel : 7 étapes Reçue → Pré-sélection → Entretien planifié →
//           Entretien fait → Offre proposée → Offre acceptée → Embauché
//
//  Top sources : nb cand / nb embauches / coût moyen / coût par embauche.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.Recrutement;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;
using Microsoft.Extensions.Caching.Memory;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <inheritdoc cref="IPilotageRecrutementDashboardService"/>
    public sealed class PilotageRecrutementDashboardService : IPilotageRecrutementDashboardService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly CultureInfo FrCulture = CultureInfo.GetCultureInfo("fr-FR");

        // Couleurs du funnel (du clair au foncé pour signifier "qualification")
        private static readonly string[] FunnelColors =
        {
            "#94A3B8", // Reçue            (slate-400)
            "#60A5FA", // Pré-sélection    (blue-400)
            "#3B82F6", // Entretien planif (blue-500)
            "#2563EB", // Entretien fait   (blue-600)
            "#1D4ED8", // Offre proposée   (blue-700)
            "#15803D", // Offre acceptée   (green-700)
            "#166534"  // Embauché         (green-800)
        };

        public PilotageRecrutementDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        // ─────────────────────────────────────────────────────────────────────
        public PilotageRecrutementDto GetData(PilotageRecrutementFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var key = filter.ToCacheKey();
            if (_cache.TryGetValue<PilotageRecrutementDto>(key, out var cached) && cached != null)
                return cached;

            var dto = Compute(filter, os);
            _cache.Set(key, dto, CacheTtl);
            return dto;
        }

        public void InvalidateCache()
        {
            // Pas de Clear() sur IMemoryCache — on utilise un compact agressif.
            if (_cache is MemoryCache mc) mc.Compact(1.0);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  COMPUTE — dispatcher Interne / Externe
        // ─────────────────────────────────────────────────────────────────────
        private PilotageRecrutementDto Compute(PilotageRecrutementFilterModel filter, IObjectSpace os)
        {
            return filter.Personnel == PersonnelType.Externe
                ? ComputeExterne(filter, os)
                : ComputeInterne(filter, os);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  COMPUTE EXTERNE — KPIs simplifiés depuis ContratInterim
        // ─────────────────────────────────────────────────────────────────────
        private static PilotageRecrutementDto ComputeExterne(PilotageRecrutementFilterModel filter, IObjectSpace os)
        {
            var dto = new PilotageRecrutementDto
            {
                Annee = filter.Annee,
                CalculatedAt = DateTime.Now
            };

            var anneeStart = new DateTime(filter.Annee, 1, 1);
            var anneeEnd = new DateTime(filter.Annee, 12, 31, 23, 59, 59);

            var allContrats = os.GetObjectsQuery<ContratInterim>().ToList();

            // Filtre Site (Site est sur ContratInterim depuis V1.1, via Association "Site-ContratsInterim")
            if (filter.SiteOids?.Count > 0)
            {
                var ids = new HashSet<Guid>(filter.SiteOids);
                allContrats = allContrats
                    .Where(c => c.Site != null && ids.Contains(c.Site.Oid))
                    .ToList();
            }

            // Contrats créés/débutant dans l'année cible
            var contratsAnnee = allContrats
                .Where(c => c.DateDebut >= anneeStart && c.DateDebut <= anneeEnd)
                .ToList();

            // Intérimaires actifs (contrat en cours, DateFin > today ou pas encore arrivé)
            var today = DateTime.Today;
            var actifs = allContrats
                .Where(c => c.DateDebut <= today && c.DateFin >= today)
                .Select(c => c.Interimaire?.Oid)
                .Where(o => o.HasValue)
                .Distinct()
                .Count();

            // Nouveaux intérimaires (premières missions dans l'année)
            var premieresMissions = contratsAnnee
                .Where(c => c.TypeContrat == ContratInterimType.PremiereMission)
                .Count();

            dto.Kpis = new KpiPilotageRecrutementDto
            {
                NbPostesOuverts = 0,                      // Pas de notion de poste vacant en intérim
                NbPostesPourvus = 0,
                NbCandidaturesActives = 0,
                NbCandidaturesRecuesYTD = 0,
                NbEmbauchesYTD = premieresMissions,
                DelaiMoyenRecrutementJ = 0,
                TauxConversionPct = 0,
                TauxAcceptationOffrePct = 0,
                CoutTotalRecrutementYTD = 0,
                CoutMoyenParEmbauche = 0,
                NbPeriodesEssaiEnCours = actifs,          // Reutilisé : nb intérimaires actifs
                NbPeriodesEssaiConcluantesYTD = 0,
                NbPeriodesEssaiRompuesYTD = 0,
                TauxRetentionPostEssaiPct = 0
            };

            // Évolution mensuelle : nb premières missions par mois
            dto.Evolution = new List<MoisRecrutementDto>(12);
            for (int m = 1; m <= 12; m++)
            {
                int nb = contratsAnnee.Count(c =>
                    c.TypeContrat == ContratInterimType.PremiereMission
                    && c.DateDebut.Month == m);
                int nbContrats = contratsAnnee.Count(c => c.DateDebut.Month == m);

                var libelle = new DateTime(filter.Annee, m, 1)
                    .ToString("MMM yyyy", FrCulture);
                if (libelle.Length > 0)
                    libelle = char.ToUpperInvariant(libelle[0]) + libelle.Substring(1);

                dto.Evolution.Add(new MoisRecrutementDto
                {
                    Annee = filter.Annee, Mois = m, Libelle = libelle,
                    NbOuvertures = 0,
                    NbCandidatures = nbContrats,    // Renommé visuellement : nouveaux contrats
                    NbEmbauches = nb
                });
            }

            return dto;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  COMPUTE INTERNE — vue complète recrutement salariés
        // ─────────────────────────────────────────────────────────────────────
        private PilotageRecrutementDto ComputeInterne(PilotageRecrutementFilterModel filter, IObjectSpace os)
        {
            var dto = new PilotageRecrutementDto
            {
                Annee = filter.Annee,
                CalculatedAt = DateTime.Now
            };

            // ── 1. Charger les données brutes ─────────────────────
            var allPostes = os.GetObjectsQuery<PosteVacant>().ToList();
            var allCandidatures = os.GetObjectsQuery<Candidature>().ToList();
            var allEntretiens = os.GetObjectsQuery<Entretien>().ToList();
            var allOffres = os.GetObjectsQuery<OffreEmploi>().ToList();
            var allPeriodesEssai = os.GetObjectsQuery<PeriodeEssai>().ToList();

            // ── 2. Appliquer les filtres sur les Postes ───────────
            var postes = ApplyPosteFilters(allPostes, filter);
            var posteOidsRetenus = new HashSet<Guid>(postes.Select(p => p.Oid));

            // ── 3. Filtrer les Candidatures via les Postes retenus
            var candidatures = allCandidatures
                .Where(c => c.Poste != null && posteOidsRetenus.Contains(c.Poste.Oid))
                .ToList();
            var candidatureOidsRetenus = new HashSet<Guid>(candidatures.Select(c => c.Oid));

            // ── 4. Sous-ensembles dans l'année cible ──────────────
            var anneeStart = new DateTime(filter.Annee, 1, 1);
            var anneeEnd = new DateTime(filter.Annee, 12, 31, 23, 59, 59);

            var candidaturesAnnee = candidatures
                .Where(c => c.DateSoumission >= anneeStart && c.DateSoumission <= anneeEnd)
                .ToList();

            var entretiens = allEntretiens
                .Where(e => e.Candidature != null && candidatureOidsRetenus.Contains(e.Candidature.Oid))
                .Where(e => e.DateEntretien >= anneeStart && e.DateEntretien <= anneeEnd)
                .ToList();

            var offres = allOffres
                .Where(o => o.Candidature != null && candidatureOidsRetenus.Contains(o.Candidature.Oid))
                .Where(o => o.DateProposition >= anneeStart && o.DateProposition <= anneeEnd)
                .ToList();

            // Périodes d'essai : on garde celles dont DateDebut dans l'année OU encore en cours
            var periodesEssai = allPeriodesEssai
                .Where(pe => (pe.DateDebut >= anneeStart && pe.DateDebut <= anneeEnd)
                          || pe.Statut == PeriodeEssaiStatut.EnCours)
                .ToList();

            // ── 5. Calculer les blocs ──────────────────────────────
            dto.Kpis = ComputeKpis(postes, candidatures, candidaturesAnnee,
                                    entretiens, offres, periodesEssai, filter.Annee);

            dto.Pipeline = ComputePipeline(candidaturesAnnee);
            dto.Sources = ComputeSources(candidaturesAnnee, entretiens);
            dto.DelaiParCategorie = ComputeDelaiParCategorie(postes, filter.Annee);
            dto.PostesOuverts = ComputePostesOuverts(postes, candidatures, entretiens);
            dto.Evolution = ComputeEvolution(postes, candidatures, filter.Annee);
            dto.RefusEntreprise = ComputeRefusEntreprise(candidaturesAnnee);
            dto.RefusCandidat = ComputeRefusCandidat(offres);
            dto.PeriodesEssaiEnCours = ComputePeriodesEssaiEnCours(periodesEssai);

            return dto;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  FILTRES SUR LES POSTES
        // ─────────────────────────────────────────────────────────────────────
        private static List<PosteVacant> ApplyPosteFilters(List<PosteVacant> all, PilotageRecrutementFilterModel filter)
        {
            IEnumerable<PosteVacant> q = all;

            if (filter.SiteOids?.Count > 0)
            {
                var ids = new HashSet<Guid>(filter.SiteOids);
                q = q.Where(p => p.Site != null && ids.Contains(p.Site.Oid));
            }

            if (filter.DepartementOids?.Count > 0)
            {
                var ids = new HashSet<Guid>(filter.DepartementOids);
                q = q.Where(p => p.Departement != null && ids.Contains(p.Departement.Oid));
            }

            if (filter.CategorieOids?.Count > 0)
            {
                var ids = new HashSet<Guid>(filter.CategorieOids);
                q = q.Where(p => p.Categorie != null && ids.Contains(p.Categorie.Oid));
            }

            if (!string.IsNullOrWhiteSpace(filter.TypeContrat)
                && Enum.TryParse<TypeContrat>(filter.TypeContrat, out var tc))
            {
                q = q.Where(p => p.TypeContrat == tc);
            }

            return q.ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  KPIs
        // ─────────────────────────────────────────────────────────────────────
        private static KpiPilotageRecrutementDto ComputeKpis(
            List<PosteVacant> postes,
            List<Candidature> allCandidatures,
            List<Candidature> candidaturesAnnee,
            List<Entretien> entretiens,
            List<OffreEmploi> offres,
            List<PeriodeEssai> periodesEssai,
            int annee)
        {
            // Activité postes
            var statutsOuverts = new[]
            {
                PosteVacantStatut.Valide,
                PosteVacantStatut.Publie,
                PosteVacantStatut.EnRecrutement
            };
            int nbPostesOuverts = postes.Count(p => statutsOuverts.Contains(p.Statut));

            int nbPostesPourvus = postes.Count(p =>
                p.Statut == PosteVacantStatut.Pourvu
                && p.DateCloture != default
                && p.DateCloture.Year == annee);

            // Candidatures actives = pas dans un état terminal
            var statutsTerminaux = new[]
            {
                CandidatureStatut.Refuse,
                CandidatureStatut.Desistement,
                CandidatureStatut.Annule,
                CandidatureStatut.Embauche
            };
            int nbCandActives = allCandidatures.Count(c => !statutsTerminaux.Contains(c.Statut));

            int nbCandRecuesYTD = candidaturesAnnee.Count;

            // Performance
            int nbEmbauches = candidaturesAnnee.Count(c => c.Statut == CandidatureStatut.Embauche);

            decimal delaiMoyenJ = 0;
            var postesPourvusAnnee = postes
                .Where(p => p.Statut == PosteVacantStatut.Pourvu
                         && p.DateCloture != default
                         && p.DateCloture.Year == annee
                         && p.DateOuverture != default)
                .ToList();
            if (postesPourvusAnnee.Count > 0)
            {
                delaiMoyenJ = (decimal)postesPourvusAnnee
                    .Average(p => Math.Max(0, (p.DateCloture - p.DateOuverture).Days));
            }

            decimal tauxConv = nbCandRecuesYTD == 0 ? 0
                : Math.Round((decimal)nbEmbauches / nbCandRecuesYTD * 100m, 1);

            int nbOffresProposees = offres.Count(o =>
                o.Statut == OffreEmploiStatut.Envoyee
                || o.Statut == OffreEmploiStatut.Acceptee
                || o.Statut == OffreEmploiStatut.RefuseeCandidat
                || o.Statut == OffreEmploiStatut.Expiree
                || o.Statut == OffreEmploiStatut.RetireeEntreprise);
            int nbOffresAcceptees = offres.Count(o => o.Statut == OffreEmploiStatut.Acceptee);
            decimal tauxAcceptation = nbOffresProposees == 0 ? 0
                : Math.Round((decimal)nbOffresAcceptees / nbOffresProposees * 100m, 1);

            // Coût recrutement (Σ coûtSource × nbCandidats)
            decimal coutTotal = 0m;
            foreach (var c in candidaturesAnnee)
            {
                var src = c.SourcePourCePoste ?? c.Candidat?.Source;
                if (src != null) coutTotal += src.CoutMoyenParCandidat;
            }
            decimal coutMoyenParEmbauche = nbEmbauches == 0 ? 0
                : Math.Round(coutTotal / nbEmbauches, 0);

            // Périodes d'essai
            int peEnCours = periodesEssai.Count(pe => pe.Statut == PeriodeEssaiStatut.EnCours);
            int peConcluantes = periodesEssai.Count(pe =>
                pe.Statut == PeriodeEssaiStatut.Concluante
                && pe.DateFin != default && pe.DateFin.Year == annee);
            int peRompues = periodesEssai.Count(pe =>
                (pe.Statut == PeriodeEssaiStatut.RompueEmployeur
                 || pe.Statut == PeriodeEssaiStatut.RompueSalarie
                 || pe.Statut == PeriodeEssaiStatut.RuptureAccord)
                && pe.DateFin != default && pe.DateFin.Year == annee);

            int totalPeTerminees = peConcluantes + peRompues;
            decimal tauxRetention = totalPeTerminees == 0 ? 0
                : Math.Round((decimal)peConcluantes / totalPeTerminees * 100m, 1);

            return new KpiPilotageRecrutementDto
            {
                NbPostesOuverts = nbPostesOuverts,
                NbPostesPourvus = nbPostesPourvus,
                NbCandidaturesActives = nbCandActives,
                NbCandidaturesRecuesYTD = nbCandRecuesYTD,
                NbEmbauchesYTD = nbEmbauches,
                DelaiMoyenRecrutementJ = Math.Round(delaiMoyenJ, 1),
                TauxConversionPct = tauxConv,
                TauxAcceptationOffrePct = tauxAcceptation,
                CoutTotalRecrutementYTD = Math.Round(coutTotal, 0),
                CoutMoyenParEmbauche = coutMoyenParEmbauche,
                NbPeriodesEssaiEnCours = peEnCours,
                NbPeriodesEssaiConcluantesYTD = peConcluantes,
                NbPeriodesEssaiRompuesYTD = peRompues,
                TauxRetentionPostEssaiPct = tauxRetention
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PIPELINE (FUNNEL)
        // ─────────────────────────────────────────────────────────────────────
        private static List<EtapePipelineDto> ComputePipeline(List<Candidature> candidaturesAnnee)
        {
            // Étapes du funnel : seuils min de progression
            // Une candidature "atteint l'étape X" si son statut numérique >= seuil X
            // OR si elle est terminée à un état >= X.
            var etapes = new[]
            {
                ("Reçue",              CandidatureStatut.Recue),
                ("Pré-sélection",      CandidatureStatut.PreSelection),
                ("Entretien planifié", CandidatureStatut.EntretienPlanifie),
                ("Entretien fait",     CandidatureStatut.EntretienFait),
                ("Offre proposée",     CandidatureStatut.OffreProposee),
                ("Offre acceptée",     CandidatureStatut.OffreAcceptee),
                ("Embauché",           CandidatureStatut.Embauche)
            };

            int total = candidaturesAnnee.Count;
            var result = new List<EtapePipelineDto>(etapes.Length);
            int? prevCount = null;

            for (int i = 0; i < etapes.Length; i++)
            {
                var (label, seuil) = etapes[i];
                int seuilVal = (int)seuil;

                // Une candidature passe l'étape si son statut numérique
                // >= seuil ET pas dans un état terminal négatif (Refuse/Desistement/Annule)
                int nb = candidaturesAnnee.Count(c =>
                    (int)c.Statut >= seuilVal && (int)c.Statut < 90);

                decimal vsTotal = total == 0 ? 0 : Math.Round((decimal)nb / total * 100m, 1);
                decimal vsPrec = (prevCount == null || prevCount == 0)
                    ? (i == 0 ? 100m : 0m)
                    : Math.Round((decimal)nb / prevCount.Value * 100m, 1);

                result.Add(new EtapePipelineDto
                {
                    Ordre = i + 1,
                    Etape = label,
                    NbCandidatures = nb,
                    TauxVsPrecedentePct = vsPrec,
                    TauxVsTotalPct = vsTotal,
                    CouleurHex = i < FunnelColors.Length ? FunnelColors[i] : "#3B82F6"
                });

                prevCount = nb;
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TOP SOURCES
        // ─────────────────────────────────────────────────────────────────────
        private static List<SourceRecrutementRowDto> ComputeSources(
            List<Candidature> candidaturesAnnee,
            List<Entretien> entretiens)
        {
            // Group by source — on prend SourcePourCePoste si renseignée, sinon Source du Candidat
            var groups = candidaturesAnnee
                .Select(c => new
                {
                    Candidature = c,
                    Source = c.SourcePourCePoste ?? c.Candidat?.Source
                })
                .Where(x => x.Source != null)
                .GroupBy(x => x.Source!.Oid)
                .ToList();

            var entretiensParCandOid = entretiens
                .Where(e => e.Candidature != null)
                .GroupBy(e => e.Candidature.Oid)
                .ToDictionary(g => g.Key, g => g.Count());

            var result = new List<SourceRecrutementRowDto>();
            foreach (var grp in groups)
            {
                var first = grp.First().Source!;
                int nbCand = grp.Count();
                int nbEmbauches = grp.Count(x => x.Candidature.Statut == CandidatureStatut.Embauche);
                int nbEntretiens = grp.Sum(x =>
                    entretiensParCandOid.TryGetValue(x.Candidature.Oid, out var n) ? n : 0);

                decimal coutMoyen = first.CoutMoyenParCandidat;
                decimal coutTotal = coutMoyen * nbCand;
                decimal coutParEmbauche = nbEmbauches == 0 ? 0 : Math.Round(coutTotal / nbEmbauches, 0);
                decimal taux = nbCand == 0 ? 0 : Math.Round((decimal)nbEmbauches / nbCand * 100m, 1);

                result.Add(new SourceRecrutementRowDto
                {
                    SourceOid = first.Oid,
                    Source = first.Libelle ?? first.Code ?? "",
                    NbCandidatures = nbCand,
                    NbEntretiens = nbEntretiens,
                    NbEmbauches = nbEmbauches,
                    TauxConversionPct = taux,
                    CoutMoyenParCandidat = coutMoyen,
                    CoutTotal = Math.Round(coutTotal, 0),
                    CoutParEmbauche = coutParEmbauche
                });
            }

            return result.OrderByDescending(r => r.NbCandidatures).ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DÉLAI PAR CATÉGORIE
        // ─────────────────────────────────────────────────────────────────────
        private static List<DelaiCategorieRowDto> ComputeDelaiParCategorie(
            List<PosteVacant> postes, int annee)
        {
            var pourvus = postes
                .Where(p => p.Statut == PosteVacantStatut.Pourvu
                         && p.DateOuverture != default
                         && p.DateCloture != default
                         && p.DateCloture.Year == annee)
                .Select(p => new
                {
                    Categorie = p.Categorie,
                    Delai = Math.Max(0, (p.DateCloture - p.DateOuverture).Days)
                })
                .ToList();

            return pourvus
                .GroupBy(x => x.Categorie?.Oid ?? Guid.Empty)
                .Select(g =>
                {
                    var first = g.First().Categorie;
                    return new DelaiCategorieRowDto
                    {
                        CategorieOid = first?.Oid,
                        Categorie = first?.Intitule ?? "(Non renseignée)",
                        NbPostesPourvus = g.Count(),
                        DelaiMoyenJ = Math.Round((decimal)g.Average(x => x.Delai), 1),
                        DelaiMinJ = g.Min(x => x.Delai),
                        DelaiMaxJ = g.Max(x => x.Delai)
                    };
                })
                .OrderByDescending(r => r.DelaiMoyenJ)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  POSTES OUVERTS (LISTE DÉTAIL)
        // ─────────────────────────────────────────────────────────────────────
        private static List<PosteOuvertRowDto> ComputePostesOuverts(
            List<PosteVacant> postes,
            List<Candidature> candidatures,
            List<Entretien> entretiens)
        {
            var statutsOuverts = new[]
            {
                PosteVacantStatut.Valide,
                PosteVacantStatut.Publie,
                PosteVacantStatut.EnRecrutement
            };

            var candByPoste = candidatures
                .Where(c => c.Poste != null)
                .GroupBy(c => c.Poste.Oid)
                .ToDictionary(g => g.Key, g => g.ToList());

            var entByCand = entretiens
                .Where(e => e.Candidature != null)
                .GroupBy(e => e.Candidature.Oid)
                .ToDictionary(g => g.Key, g => g.Count());

            return postes
                .Where(p => statutsOuverts.Contains(p.Statut))
                .Select(p =>
                {
                    var cands = candByPoste.TryGetValue(p.Oid, out var lst) ? lst : new List<Candidature>();
                    int nbEnt = cands.Sum(c => entByCand.TryGetValue(c.Oid, out var n) ? n : 0);
                    return new PosteOuvertRowDto
                    {
                        PosteOid = p.Oid,
                        Code = p.Code ?? "",
                        Libelle = p.Libelle ?? "",
                        Departement = p.Departement?.Nom ?? "",
                        Site = p.Site?.Nom ?? "",
                        Categorie = p.Categorie?.Intitule ?? "",
                        TypeContrat = p.TypeContrat.ToString(),
                        Statut = p.Statut.ToString(),
                        DateOuverture = p.DateOuverture,
                        JoursOuverts = p.JoursOuverts,
                        NbCandidatures = cands.Count,
                        NbEntretiens = nbEnt
                    };
                })
                .OrderByDescending(r => r.JoursOuverts)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ÉVOLUTION MENSUELLE
        // ─────────────────────────────────────────────────────────────────────
        private static List<MoisRecrutementDto> ComputeEvolution(
            List<PosteVacant> postes,
            List<Candidature> candidatures,
            int annee)
        {
            var result = new List<MoisRecrutementDto>(12);
            for (int m = 1; m <= 12; m++)
            {
                int nbOuv = postes.Count(p =>
                    p.DateOuverture.Year == annee && p.DateOuverture.Month == m);
                int nbCand = candidatures.Count(c =>
                    c.DateSoumission.Year == annee && c.DateSoumission.Month == m);
                int nbEmb = candidatures.Count(c =>
                    c.Statut == CandidatureStatut.Embauche
                    && c.DateDecision != default
                    && c.DateDecision.Year == annee
                    && c.DateDecision.Month == m);

                var libelle = new DateTime(annee, m, 1)
                    .ToString("MMM yyyy", FrCulture);
                if (libelle.Length > 0)
                    libelle = char.ToUpperInvariant(libelle[0]) + libelle.Substring(1);

                result.Add(new MoisRecrutementDto
                {
                    Annee = annee,
                    Mois = m,
                    Libelle = libelle,
                    NbOuvertures = nbOuv,
                    NbCandidatures = nbCand,
                    NbEmbauches = nbEmb
                });
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MOTIFS DE REFUS
        // ─────────────────────────────────────────────────────────────────────
        private static List<MotifRefusRowDto> ComputeRefusEntreprise(List<Candidature> candidaturesAnnee)
        {
            var refuses = candidaturesAnnee
                .Where(c => c.Statut == CandidatureStatut.Refuse && c.MotifRefusCandidat != null)
                .ToList();
            int total = refuses.Count;
            if (total == 0) return new List<MotifRefusRowDto>();

            return refuses
                .GroupBy(c => c.MotifRefusCandidat!.Oid)
                .Select(g =>
                {
                    var motif = g.First().MotifRefusCandidat!;
                    int nb = g.Count();
                    return new MotifRefusRowDto
                    {
                        Motif = motif.Libelle ?? motif.Code ?? "",
                        Nombre = nb,
                        PourcentageDuTotal = Math.Round((decimal)nb / total * 100m, 1)
                    };
                })
                .OrderByDescending(r => r.Nombre)
                .ToList();
        }

        private static List<MotifRefusRowDto> ComputeRefusCandidat(List<OffreEmploi> offres)
        {
            var refusees = offres
                .Where(o => o.Statut == OffreEmploiStatut.RefuseeCandidat && o.MotifRefus != null)
                .ToList();
            int total = refusees.Count;
            if (total == 0) return new List<MotifRefusRowDto>();

            return refusees
                .GroupBy(o => o.MotifRefus!.Oid)
                .Select(g =>
                {
                    var motif = g.First().MotifRefus!;
                    int nb = g.Count();
                    return new MotifRefusRowDto
                    {
                        Motif = motif.Libelle ?? motif.Code ?? "",
                        Nombre = nb,
                        PourcentageDuTotal = Math.Round((decimal)nb / total * 100m, 1)
                    };
                })
                .OrderByDescending(r => r.Nombre)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PÉRIODES D'ESSAI EN COURS
        // ─────────────────────────────────────────────────────────────────────
        private static List<PeriodeEssaiRowDto> ComputePeriodesEssaiEnCours(List<PeriodeEssai> periodesEssai)
        {
            var today = DateTime.Today;
            return periodesEssai
                .Where(pe => pe.Statut == PeriodeEssaiStatut.EnCours)
                .Select(pe =>
                {
                    var nomComplet = pe.Salarie != null
                        ? $"{pe.Salarie.LastName} {pe.Salarie.FirstName}".Trim()
                        : "?";
                    var poste = pe.CandidatureOrigine?.Poste?.Libelle ?? "";
                    var site = pe.Salarie?.Site?.Nom ?? "";
                    var jEcoul = pe.DateDebut == default ? 0
                        : Math.Max(0, (today - pe.DateDebut).Days);
                    var jRestants = pe.DateFinPrevue == default ? 0
                        : Math.Max(0, (pe.DateFinPrevue - today).Days);

                    return new PeriodeEssaiRowDto
                    {
                        PeriodeEssaiOid = pe.Oid,
                        SalarieOid = pe.Salarie?.Oid,
                        SalarieNom = nomComplet,
                        Poste = poste,
                        Site = site,
                        DateDebut = pe.DateDebut,
                        DateFinPrevue = pe.DateFinPrevue,
                        JoursEcoules = jEcoul,
                        JoursRestants = jRestants,
                        Statut = pe.Statut.ToString()
                    };
                })
                .OrderBy(r => r.JoursRestants)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  LISTES POUR LES FILTRES
        // ─────────────────────────────────────────────────────────────────────
        public List<int> GetAnneesDisponibles(IObjectSpace os)
        {
            // Plage glissante 7 ans (cohérent avec les autres dashboards)
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
                var actifs = os.GetObjectsQuery<Site>().ToList()
                    .Where(s => s.Actif)
                    .OrderBy(s => s.Nom)
                    .ToList();
                return actifs.Count > 0 ? actifs
                    : os.GetObjectsQuery<Site>().ToList()
                        .OrderBy(s => s.Nom).ToList();
            }
            catch { return new List<Site>(); }
        }

        public List<Departement> GetDepartements(IObjectSpace os)
        {
            try
            {
                return os.GetObjectsQuery<Departement>().ToList()
                    .OrderBy(d => d.Nom)
                    .ToList();
            }
            catch { return new List<Departement>(); }
        }

        public List<Categories> GetCategories(IObjectSpace os)
        {
            try
            {
                return os.GetObjectsQuery<Categories>().ToList()
                    .OrderBy(c => c.Intitule)
                    .ToList();
            }
            catch { return new List<Categories>(); }
        }
    }
}
