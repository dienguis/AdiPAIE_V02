// =============================================================================
//  ConformiteSenegalDashboardService.cs — V1.2 Sprint 5 (mai 2026)
//
//  Calcule 9 indicateurs de conformité réglementaire Sénégal :
//    1. SMIG respecté (60 000 FCFA / mois)
//    2. Salariés sans contrat scanné dans le SI
//    3. Soldes congés > 30 jours (risque légal)
//    4. CDD au-delà de 2 ans (requalification CDI possible)
//    5. Stagiaires > 6 mois (transformation obligatoire)
//    6. Heures supp > 15 h / semaine (seuil légal)
//    7. Déclarations IPRES du dernier trimestre
//    8. Déclarations CSS du dernier trimestre
//    9. Déclarations IPM du dernier trimestre
//
//  ⚠️ Les indicateurs 7-9 (déclarations) et 6 (heures sup) nécessitent des
//  entités dédiées pas encore présentes en V1.2 — affichés "NonEvalue" pour
//  l'instant. Cf. roadmap V1.2.1 dans MISSION_STATE.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;
using Microsoft.Extensions.Caching.Memory;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <inheritdoc cref="IConformiteSenegalDashboardService"/>
    public sealed class ConformiteSenegalDashboardService : IConformiteSenegalDashboardService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly DateTime SortieSentinelle = new(1900, 1, 1);

        // Seuils légaux Sénégal
        private const decimal SmigMensuel = 60_000m;
        private const int CDD_MaxAnnees = 2;
        private const int Stage_MaxMois = 6;
        private const int Conges_MaxJours = 30;
        private const int HeuresSup_MaxParSemaine = 15;

        public ConformiteSenegalDashboardService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public ConformiteSenegalDto GetData(ConformiteSenegalFilterModel filter, IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(os);

            var key = filter.ToCacheKey();
            if (_cache.TryGetValue<ConformiteSenegalDto>(key, out var cached) && cached != null)
                return cached;

            var dto = Compute(filter, os);
            _cache.Set(key, dto, CacheTtl);
            return dto;
        }

        // ─────────────────────────────────────────────────────────────────────
        private ConformiteSenegalDto Compute(ConformiteSenegalFilterModel filter, IObjectSpace os)
        {
            var dateRef = filter.DateReference;
            var indicateurs = new List<IndicateurConformiteDto>();

            // ── Récupération des salariés actifs ──────────────────────────
            var salaries = os.GetObjectsQuery<Salarie>()
                .ToList()
                .Where(s => IsActif(s)
                         && s.DateEmbauche != default
                         && s.DateEmbauche <= dateRef
                         && (s.DateSortie == default || s.DateSortie == SortieSentinelle || s.DateSortie > dateRef))
                .ToList();

            if (filter.SiteOid.HasValue)
                salaries = salaries.Where(s => s.Site?.Oid == filter.SiteOid.Value).ToList();

            int total = salaries.Count;

            // ── 1. SMIG respecté ──────────────────────────────────────────
            int nbSousSmig = 0;
            try
            {
                // On considère le dernier bulletin de chaque salarié
                var bulletinsRecents = os.GetObjectsQuery<Bulletin>()
                    .ToList()
                    .Where(b => IsActif(b))
                    .GroupBy(b => b.Salarie?.Oid)
                    .Where(g => g.Key != null)
                    .Select(g => g.OrderByDescending(b => b.Annee).ThenByDescending(b => b.Mois).First())
                    .ToList();

                nbSousSmig = bulletinsRecents.Count(b => b.NetAPayer < SmigMensuel && b.NetAPayer > 0);
            }
            catch { nbSousSmig = 0; }

            indicateurs.Add(new IndicateurConformiteDto
            {
                Code = "SMIG",
                Libelle = "SMIG mensuel respecté",
                Description = "Aucun salarié ne doit avoir un net inférieur au SMIG.",
                SeuilLegal = $"{SmigMensuel:N0} FCFA / mois",
                Valeur = $"{nbSousSmig} salarié(s) en dessous",
                NbCasIdentifies = nbSousSmig,
                Statut = nbSousSmig == 0 ? StatutConformite.Conforme : StatutConformite.NonConforme,
                Recommandation = nbSousSmig > 0
                    ? "Régulariser immédiatement les salaires en dessous du SMIG."
                    : ""
            });

            // ── 2. CDD au-delà de 2 ans ───────────────────────────────────
            int nbCDDLong = 0;
            try
            {
                nbCDDLong = salaries.Count(s =>
                {
                    var typeContrat = s.GetType().GetProperty("TypeContrat")?.GetValue(s)?.ToString() ?? "";
                    if (!typeContrat.Equals("CDD", StringComparison.OrdinalIgnoreCase)) return false;
                    var anciennete = (dateRef - s.DateEmbauche).TotalDays / 365.25;
                    return anciennete > CDD_MaxAnnees;
                });
            }
            catch { nbCDDLong = 0; }

            indicateurs.Add(new IndicateurConformiteDto
            {
                Code = "CDD_DUREE",
                Libelle = "CDD inférieurs à 2 ans",
                Description = "Un CDD au-delà de 2 ans peut être requalifié en CDI.",
                SeuilLegal = $"{CDD_MaxAnnees} ans maximum",
                Valeur = $"{nbCDDLong} CDD au-delà",
                NbCasIdentifies = nbCDDLong,
                Statut = nbCDDLong == 0 ? StatutConformite.Conforme :
                         nbCDDLong <= 2 ? StatutConformite.Avertissement :
                                          StatutConformite.NonConforme,
                Recommandation = nbCDDLong > 0
                    ? "Transformer en CDI ou renégocier les contrats concernés."
                    : ""
            });

            // ── 3. Stagiaires > 6 mois ────────────────────────────────────
            int nbStagiairesLong = 0;
            try
            {
                nbStagiairesLong = salaries.Count(s =>
                {
                    var typeContrat = s.GetType().GetProperty("TypeContrat")?.GetValue(s)?.ToString() ?? "";
                    if (!typeContrat.Equals("Stage", StringComparison.OrdinalIgnoreCase)) return false;
                    var moisAnciennete = (dateRef.Year - s.DateEmbauche.Year) * 12
                                       + (dateRef.Month - s.DateEmbauche.Month);
                    return moisAnciennete > Stage_MaxMois;
                });
            }
            catch { nbStagiairesLong = 0; }

            indicateurs.Add(new IndicateurConformiteDto
            {
                Code = "STAGE_DUREE",
                Libelle = "Stages inférieurs à 6 mois",
                Description = "Au-delà de 6 mois, un stage doit être transformé en contrat.",
                SeuilLegal = $"{Stage_MaxMois} mois maximum",
                Valeur = $"{nbStagiairesLong} stagiaire(s) au-delà",
                NbCasIdentifies = nbStagiairesLong,
                Statut = nbStagiairesLong == 0 ? StatutConformite.Conforme : StatutConformite.NonConforme,
                Recommandation = nbStagiairesLong > 0
                    ? "Convertir les stages en CDD/CDI selon le besoin métier."
                    : ""
            });

            // ── 4. Soldes congés > 30 jours ───────────────────────────────
            //   ⚠️ À défaut d'entité SoldeConge mappée en V1.2 initiale,
            //   on calcule un solde théorique via l'ancienneté (2.5 j/mois).
            int nbCongesEleves = 0;
            try
            {
                nbCongesEleves = salaries.Count(s =>
                {
                    var anciennete = (dateRef - s.DateEmbauche).TotalDays / 365.25;
                    var soldeTheoriqueMax = Math.Min(60.0, anciennete * 30.0);
                    return soldeTheoriqueMax > Conges_MaxJours;
                });
            }
            catch { nbCongesEleves = 0; }

            indicateurs.Add(new IndicateurConformiteDto
            {
                Code = "CONGES_SOLDE",
                Libelle = "Soldes congés < 30 jours",
                Description = "Un solde > 30 jours indique un risque social et financier (provision élevée).",
                SeuilLegal = $"{Conges_MaxJours} jours maximum recommandés",
                Valeur = $"{nbCongesEleves} salarié(s) susceptibles",
                NbCasIdentifies = nbCongesEleves,
                Statut = nbCongesEleves == 0 ? StatutConformite.Conforme :
                         nbCongesEleves <= total / 10 ? StatutConformite.Avertissement :
                                                        StatutConformite.NonConforme,
                Recommandation = nbCongesEleves > 0
                    ? "Planifier la prise de congés en accord avec les managers."
                    : ""
            });

            // ── 5. Salariés sans contrat scanné ───────────────────────────
            //   Indicateur estimé via présence d'une PJ "Contrat" sur le salarié.
            //   En V1.2 initiale, faute d'entité PJ mappée, on affiche NonEvalue.
            indicateurs.Add(new IndicateurConformiteDto
            {
                Code = "CONTRAT_SCAN",
                Libelle = "Contrats scannés présents",
                Description = "Tous les salariés doivent avoir leur contrat numérisé dans le SI.",
                SeuilLegal = "100 % des salariés",
                Valeur = "Non évalué (V1.2.1)",
                NbCasIdentifies = 0,
                Statut = StatutConformite.NonEvalue,
                Recommandation = "Module GED contrats à intégrer en V1.2.1."
            });

            // ── 6. Heures supp > 15 h / sem ───────────────────────────────
            indicateurs.Add(new IndicateurConformiteDto
            {
                Code = "HSUP_SEUIL",
                Libelle = "Heures sup. dans la limite légale",
                Description = "Le seuil légal hebdomadaire est de 15 h supplémentaires.",
                SeuilLegal = $"{HeuresSup_MaxParSemaine} h / semaine",
                Valeur = "Non évalué (V1.2.1)",
                NbCasIdentifies = 0,
                Statut = StatutConformite.NonEvalue,
                Recommandation = "Calcul à connecter à MouvementHeuresSup en V1.2.1."
            });

            // ── 7-9. Déclarations IPRES / CSS / IPM ───────────────────────
            //   À défaut d'entité DéclarationSociale mappée, on affiche
            //   NonEvalue. Le DAF doit confirmer manuellement pour l'instant.
            foreach (var dec in new[] { ("IPRES", "IPRES"), ("CSS", "CSS"), ("IPM", "IPM (Mutuelle santé)") })
            {
                indicateurs.Add(new IndicateurConformiteDto
                {
                    Code = $"DECLA_{dec.Item1}",
                    Libelle = $"Déclarations {dec.Item2} à jour",
                    Description = $"Toutes les déclarations {dec.Item1} du dernier trimestre doivent être faites.",
                    SeuilLegal = "100 % du trimestre",
                    Valeur = "Non évalué (V1.2.1)",
                    NbCasIdentifies = 0,
                    Statut = StatutConformite.NonEvalue,
                    Recommandation = $"Module Déclarations sociales à intégrer en V1.2.1."
                });
            }

            // ── KPIs synthèse ─────────────────────────────────────────────
            int nbConformes = indicateurs.Count(i => i.Statut == StatutConformite.Conforme);
            int nbAvert = indicateurs.Count(i => i.Statut == StatutConformite.Avertissement);
            int nbNon = indicateurs.Count(i => i.Statut == StatutConformite.NonConforme);
            int nbEvalues = indicateurs.Count(i => i.Statut != StatutConformite.NonEvalue);
            decimal score = nbEvalues > 0
                ? Math.Round((decimal)nbConformes * 100m / nbEvalues, 1)
                : 0m;

            StatutConformite global = nbNon > 0 ? StatutConformite.NonConforme :
                                       nbAvert > 0 ? StatutConformite.Avertissement :
                                       nbConformes > 0 ? StatutConformite.Conforme :
                                                         StatutConformite.NonEvalue;

            return new ConformiteSenegalDto
            {
                Indicateurs = indicateurs,
                Kpis = new KpiConformiteDto
                {
                    NbIndicateurs = indicateurs.Count,
                    NbConformes = nbConformes,
                    NbAvertissements = nbAvert,
                    NbNonConformes = nbNon,
                    ScoreConformitePct = score,
                    StatutGlobal = global
                },
                CalculatedAt = DateTime.Now
            };
        }

        // ─────────────────────────────────────────────────────────────────────
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
