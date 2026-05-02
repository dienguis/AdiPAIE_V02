using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Agrège les données intérimaires depuis XPO pour alimenter le TableauBordInterimaire.
    /// Calcule les KPIs (effectif, départs, rotation, ancienneté, H/F, âge)
    /// et les distributions (tranche d'âge, ancienneté, station, fonction, contrat, société).
    /// </summary>
    public static class DashboardInterimaireService
    {
        public static void Populate(TableauBordInterimaire tb, IObjectSpace os)
        {
            // Intérimaires actifs = ceux qui ont un contrat EnCours
            var contrats = os.GetObjectsQuery<ContratInterim>()
                .Where(c => c.Statut == ContratInterimStatut.EnCours)
                .ToList();

            var interimairesActifs = contrats
                .Select(c => c.Interimaire)
                .Where(i => i != null)
                .Distinct()
                .ToList();

            if (interimairesActifs.Count == 0)
            {
                tb.EffectifTotal = 0;
                return;
            }

            // ── KPIs principaux ──
            tb.EffectifTotal = interimairesActifs.Count;

            // Départs = contrats terminés ou résiliés dans les 12 derniers mois
            var dateDepart = DateTime.Today.AddYears(-1);
            var contratsTermines = os.GetObjectsQuery<ContratInterim>()
                .Where(c => (c.Statut == ContratInterimStatut.Termine || c.Statut == ContratInterimStatut.Resilie)
                    && (c.DateFinReelle ?? c.DateFin) >= dateDepart)
                .ToList();
            tb.NbDeparts = contratsTermines
                .Select(c => c.Interimaire?.Oid)
                .Distinct()
                .Count();
            tb.PctDeparts = tb.EffectifTotal > 0
                ? Math.Round(100.0 * tb.NbDeparts / tb.EffectifTotal, 0) : 0;
            tb.TauxRotation = tb.PctDeparts; // rotation ≈ départs / effectif

            // H/F
            // Note: Interimaire n'a pas de champ Sexe — on utilise une heuristique
            // ou on considère tous comme non genrés. Pour l'instant on met 0.
            // TODO: ajouter un champ Sexe sur Interimaire si nécessaire
            tb.NbHommes = interimairesActifs.Count; // par défaut
            tb.NbFemmes = 0;
            tb.PctHommes = 100;
            tb.PctFemmes = 0;

            // Ancienneté (basée sur le premier contrat)
            var anciennetes = interimairesActifs
                .Select(i =>
                {
                    var premierContrat = i.Contrats
                        .OrderBy(c => c.DateDebut)
                        .FirstOrDefault();
                    return premierContrat != null
                        ? (DateTime.Today - premierContrat.DateDebut).TotalDays / 365.25
                        : 0;
                })
                .ToList();
            tb.AncienneteMoyenne = anciennetes.Count > 0
                ? Math.Round(anciennetes.Average(), 1) : 0;

            // Âge moyen (basé sur DateNaissance)
            var ages = interimairesActifs
                .Where(i => i.DateNaissance.HasValue)
                .Select(i => (DateTime.Today - i.DateNaissance.Value).TotalDays / 365.25)
                .ToList();
            tb.AgeMoyen = ages.Count > 0 ? Math.Round(ages.Average(), 0) : 0;

            // ── Distributions ──

            // Par Tranche d'Âge
            FillList(tb.ParTrancheAge, BuildDistribution(interimairesActifs,
                i => TrancheAge(i.DateNaissance)));

            // Par Ancienneté
            FillList(tb.ParAnciennete, BuildDistributionFromValues(
                interimairesActifs.Select(i =>
                {
                    var pc = i.Contrats.OrderBy(c => c.DateDebut).FirstOrDefault();
                    int ans = pc != null ? (int)((DateTime.Today - pc.DateDebut).TotalDays / 365.25) : 0;
                    return TrancheAnciennete(ans);
                }).ToList()));

            // Par Station (Segment)
            FillList(tb.ParStation, BuildDistribution(interimairesActifs,
                i =>
                {
                    var ca = i.ContratActif;
                    if (ca == null) return "(sans affectation)";
                    if (ca.EstDG) return "Direction Générale";
                    return ca.Station?.Nom ?? "(sans station)";
                }));

            // Par Fonction (Top 5)
            var fonctions = BuildDistribution(interimairesActifs,
                i => string.IsNullOrEmpty(i.Fonction) ? "(non renseignée)" : i.Fonction.ToUpper());
            FillList(tb.ParFonction, fonctions.Take(5).ToList());

            // Par Type Contrat
            FillList(tb.ParTypeContrat, BuildDistributionFromValues(
                contrats.Select(c => c.TypeContrat == ContratInterimType.Renouvellement
                    ? "Renouvellement" : "Première mission").ToList()));

            // Par Société Intérim
            FillList(tb.ParSociete, BuildDistribution(interimairesActifs,
                i => i.SocieteInterim?.RaisonSociale ?? "(non renseignée)"));

            // ── Évolution annuelle ──
            FillList(tb.EvolutionAnnuelle, BuildEvolution(os));
        }

        // ── Helpers ──

        private static void FillList<T>(BindingList<T> target, List<T> source)
        {
            target.Clear();
            foreach (var item in source)
                target.Add(item);
        }

        private static List<DistributionItem> BuildDistribution(
            List<Interimaire> items, Func<Interimaire, string> groupBy)
        {
            var total = items.Count;
            return items
                .GroupBy(groupBy)
                .Select(g => new DistributionItem
                {
                    Libelle = g.Key,
                    Effectif = g.Count(),
                    Pourcentage = total > 0 ? Math.Round(100.0 * g.Count() / total, 0) : 0
                })
                .OrderByDescending(x => x.Effectif)
                .ToList();
        }

        private static List<DistributionItem> BuildDistributionFromValues(List<string> values)
        {
            var total = values.Count;
            return values
                .GroupBy(v => v)
                .Select(g => new DistributionItem
                {
                    Libelle = g.Key,
                    Effectif = g.Count(),
                    Pourcentage = total > 0 ? Math.Round(100.0 * g.Count() / total, 0) : 0
                })
                .OrderByDescending(x => x.Effectif)
                .ToList();
        }

        private static List<EvolutionItem> BuildEvolution(IObjectSpace os)
        {
            var anneeActuelle = DateTime.Today.Year;
            var result = new List<EvolutionItem>();

            for (int a = anneeActuelle - 4; a <= anneeActuelle; a++)
            {
                var dateRef = new DateTime(a, 12, 31);
                var count = os.GetObjectsQuery<ContratInterim>()
                    .Count(c => c.DateDebut <= dateRef && c.DateFin >= dateRef
                        && (c.Statut == ContratInterimStatut.EnCours
                            || c.Statut == ContratInterimStatut.Termine));
                result.Add(new EvolutionItem { Annee = a, Effectif = count });
            }

            for (int i = 1; i < result.Count; i++)
            {
                var prev = result[i - 1].Effectif;
                result[i].Variation = prev > 0
                    ? Math.Round(100.0 * (result[i].Effectif - prev) / prev, 0) : 0;
            }

            return result;
        }

        private static string TrancheAge(DateTime? dateNaissance)
        {
            if (!dateNaissance.HasValue) return "(non renseigné)";
            int age = (int)((DateTime.Today - dateNaissance.Value).TotalDays / 365.25);
            if (age < 30) return "<30 ans";
            if (age < 40) return "30-39";
            if (age < 50) return "40-49";
            return ">=50 ans";
        }

        private static string TrancheAnciennete(int ans)
        {
            if (ans < 1) return "<1 an";
            if (ans < 5) return "1-4";
            if (ans < 10) return "5-9";
            if (ans < 15) return "10-14";
            return ">=15 ans";
        }
    }
}
