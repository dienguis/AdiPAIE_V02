using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Agrège les données RH depuis XPO pour alimenter le TableauBordEffectif.
    /// </summary>
    public static class DashboardEffectifService
    {
        public static void Populate(TableauBordEffectif tb, IObjectSpace os)
        {
            var salaries = os.GetObjectsQuery<Salarie>()
                .Where(s => s.IsActif)
                .ToList();

            if (salaries.Count == 0)
            {
                tb.EffectifTotal = 0;
                return;
            }

            var hommes = salaries.Where(s => s.Sexe == Sexe.Masculin).ToList();
            var femmes = salaries.Where(s => s.Sexe == Sexe.Feminin).ToList();

            // ── KPIs principaux ──
            tb.EffectifTotal = salaries.Count;
            tb.NbHommes = hommes.Count;
            tb.NbFemmes = femmes.Count;

            // ── Ancienneté ──
            tb.AncienneteMoyenne = salaries.Average(s => (double)s.Anciennete);
            tb.AncienneteMoyHommes = hommes.Count > 0 ? hommes.Average(s => (double)s.Anciennete) : 0;
            tb.AncienneteMoyFemmes = femmes.Count > 0 ? femmes.Average(s => (double)s.Anciennete) : 0;

            // ── Salaire ──
            tb.SalaireMoyen = salaries.Average(s => s.SalaireBase);
            tb.SalaireMoyHommes = hommes.Count > 0 ? hommes.Average(s => s.SalaireBase) : 0;
            tb.SalaireMoyFemmes = femmes.Count > 0 ? femmes.Average(s => s.SalaireBase) : 0;

            // ── Distributions ──
            FillList(tb.ParSexe, BuildDistribution(salaries, s => s.Sexe == Sexe.Masculin ? "Homme" : "Femme"));
            FillList(tb.ParDepartement, BuildDistribution(salaries, s => s.Departement?.Nom ?? "(non affecté)"));
            FillList(tb.ParFonction, BuildDistribution(salaries, s => s.Fonction?.Intitule ?? "(non affectée)"));
            FillList(tb.ParCategorie, BuildDistribution(salaries, s => s.Categories?.Intitule ?? "(non affectée)"));
            FillList(tb.ParAnciennete, BuildDistribution(salaries, s => TrancheAnciennete(s.Anciennete)));

            // ── Évolution annuelle ──
            FillList(tb.EvolutionAnnuelle, BuildEvolution(os));

            // ── Distribution croisée Catégorie × Sexe ──
            FillList(tb.ParCategorieSexe, BuildCroisee(salaries));
        }

        // ── Helpers ──

        private static void FillList<T>(BindingList<T> target, List<T> source)
        {
            target.Clear();
            foreach (var item in source)
                target.Add(item);
        }

        private static List<DistributionItem> BuildDistribution(
            List<Salarie> salaries, Func<Salarie, string> groupBy)
        {
            var total = salaries.Count;
            return salaries
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

        private static List<EvolutionItem> BuildEvolution(IObjectSpace os)
        {
            var anneeActuelle = DateTime.Today.Year;
            var result = new List<EvolutionItem>();

            for (int a = anneeActuelle - 4; a <= anneeActuelle; a++)
            {
                var dateRef = new DateTime(a, 12, 31);
                var count = os.GetObjectsQuery<Salarie>()
                    .Count(s => s.DateEmbauche <= dateRef
                        && (s.IsActif || s.DateSortie > dateRef));
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

        private static List<DistributionCroiseeItem> BuildCroisee(List<Salarie> salaries)
        {
            return salaries
                .GroupBy(s => s.Categories?.Intitule ?? "(non affectée)")
                .Select(g =>
                {
                    var h = g.Count(s => s.Sexe == Sexe.Masculin);
                    var f = g.Count(s => s.Sexe == Sexe.Feminin);
                    var total = g.Count();
                    return new DistributionCroiseeItem
                    {
                        Categorie = g.Key,
                        Hommes = h,
                        Femmes = f,
                        PctHommes = total > 0 ? Math.Round(100.0 * h / total, 0) : 0,
                        PctFemmes = total > 0 ? Math.Round(100.0 * f / total, 0) : 0
                    };
                })
                .OrderByDescending(x => x.Hommes + x.Femmes)
                .ToList();
        }

        private static string TrancheAnciennete(int ans)
        {
            if (ans < 2) return "< 2 ans";
            if (ans < 5) return "2 - 4 ans";
            if (ans < 10) return "5 - 9 ans";
            if (ans < 20) return "10 - 19 ans";
            return "20 ans et +";
        }
    }
}
