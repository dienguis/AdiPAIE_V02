// =============================================================================
//  EffectifDetailleDto.cs
//  Tableau N°1 (Effectif détaillé) — DTO renvoyés par le service au Razor.
//
//  Pas de logique métier ici — uniquement des structures de transport.
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>
    /// Réponse complète du service pour le Tableau N°1.
    /// </summary>
    public sealed class EffectifDetailleDto
    {
        public KpiAgeAncienneteDto Kpis { get; set; } = new();

        /// <summary>Évolution effectif sur 3 ans (du plus ancien au plus récent).</summary>
        public List<EvolutionAnneeDto> EvolutionTroisAns { get; set; } = new();

        /// <summary>
        /// Pour chaque tranche d'âge, la liste des cellules
        /// (catégorie professionnelle × effectif × pourcentage de la tranche).
        /// </summary>
        public Dictionary<AgeBucket, List<TrancheCategorieRowDto>> TrancheCategorie { get; set; } = new();

        /// <summary>
        /// Données du bar chart horizontal empilé Femmes / Hommes
        /// par tranche d'âge × catégorie professionnelle.
        /// </summary>
        public List<BarStackPointDto> BarStackHommesFemmes { get; set; } = new();

        /// <summary>Effectif total à la date de référence (somme toutes tranches).</summary>
        public int EffectifTotal { get; set; }

        /// <summary>Date de référence retenue (cf. FilterModel.ResolveDateReference()).</summary>
        public DateTime DateReference { get; set; }

        /// <summary>Horodatage de calcul (utile pour debug et invalidation cache).</summary>
        public DateTime CalculatedAt { get; set; }
    }

    /// <summary>
    /// 6 KPI cartes haut de page (3 âges + 3 anciennetés).
    /// Valeurs en années (décimales, 1 chiffre après la virgule à l'affichage).
    /// </summary>
    public sealed class KpiAgeAncienneteDto
    {
        public decimal AgeMoyenGlobal { get; set; }
        public decimal AgeMoyenHommes { get; set; }
        public decimal AgeMoyenFemmes { get; set; }

        public decimal AncienneteMoyenneGlobal { get; set; }
        public decimal AncienneteMoyenneHommes { get; set; }
        public decimal AncienneteMoyenneFemmes { get; set; }
    }

    /// <summary>
    /// Un point d'évolution annuelle d'effectif.
    /// </summary>
    public sealed class EvolutionAnneeDto
    {
        public int Annee { get; set; }
        public int Effectif { get; set; }

        /// <summary>
        /// Variation en % par rapport à l'année précédente.
        /// Null pour la première année de la série (pas de référence).
        /// </summary>
        public decimal? VariationPourcent { get; set; }
    }

    /// <summary>
    /// Une ligne d'un tableau « tranche d'âge » : catégorie pro + effectif + %.
    /// </summary>
    public sealed class TrancheCategorieRowDto
    {
        public string Categorie { get; set; } = "";
        public int Effectif { get; set; }
        public decimal PourcentageDansTranche { get; set; }

        /// <summary>
        /// Pourcentage déjà formaté pour affichage direct (ex : "12,5 %").
        /// Utilisé tel quel par DxGrid (évite l'usage de DisplayTemplate).
        /// </summary>
        public string PourcentageFormatted =>
            $"{PourcentageDansTranche.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))} %";
    }

    /// <summary>
    /// Un point du bar chart F/H empilé.
    /// Le label combine tranche d'âge × catégorie pro pour l'axe Y.
    /// </summary>
    public sealed class BarStackPointDto
    {
        public AgeBucket Tranche { get; set; }
        public string Categorie { get; set; } = "";
        public int Hommes { get; set; }
        public int Femmes { get; set; }

        /// <summary>Effectif total de la cellule (Hommes + Femmes).</summary>
        public int Total => Hommes + Femmes;

        /// <summary>% Hommes sur le total cellule (0-100).</summary>
        public decimal PourcentageHommes => Total > 0 ? (decimal)Hommes * 100m / Total : 0m;

        /// <summary>% Femmes sur le total cellule (0-100).</summary>
        public decimal PourcentageFemmes => Total > 0 ? (decimal)Femmes * 100m / Total : 0m;

        /// <summary>Libellé d'axe combiné (ex. "25-34 / Cadre").</summary>
        public string Label => $"{AgeBucketHelper.GetLibelle(Tranche)} — {Categorie}";
    }
}
