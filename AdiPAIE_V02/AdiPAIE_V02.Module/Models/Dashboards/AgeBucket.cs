// =============================================================================
//  AgeBucket.cs
//  Tableau N°1 (Effectif détaillé) — tranches d'âge utilisées par les axes
//  des tableaux et du bar chart empilé Femmes / Hommes.
//
//  Tranches définies par la mission (cf. spec Tableau N°1) :
//    - <25      (ajoutée par décision utilisateur, salariés <25 ans)
//    - 25-34
//    - 35-44
//    - 45-54
//    - 55+
// =============================================================================

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>
    /// Tranche d'âge d'un salarié à une date d'observation.
    /// </summary>
    public enum AgeBucket
    {
        Moins25 = 0,
        Tranche25_34 = 1,
        Tranche35_44 = 2,
        Tranche45_54 = 3,
        CinquanteCinqPlus = 4
    }

    /// <summary>
    /// Helpers de conversion âge → tranche et libellé d'affichage.
    /// </summary>
    public static class AgeBucketHelper
    {
        /// <summary>
        /// Retourne la tranche d'âge correspondant à un âge donné en années pleines.
        /// </summary>
        public static AgeBucket FromAge(int age) => age switch
        {
            < 25 => AgeBucket.Moins25,
            < 35 => AgeBucket.Tranche25_34,
            < 45 => AgeBucket.Tranche35_44,
            < 55 => AgeBucket.Tranche45_54,
            _    => AgeBucket.CinquanteCinqPlus
        };

        /// <summary>
        /// Libellé d'affichage standard d'une tranche.
        /// </summary>
        public static string GetLibelle(AgeBucket bucket) => bucket switch
        {
            AgeBucket.Moins25            => "<25 ans",
            AgeBucket.Tranche25_34       => "25-34 ans",
            AgeBucket.Tranche35_44       => "35-44 ans",
            AgeBucket.Tranche45_54       => "45-54 ans",
            AgeBucket.CinquanteCinqPlus  => "55 ans et +",
            _                            => "?"
        };

        /// <summary>
        /// Liste ordonnée des tranches dans leur ordre d'affichage.
        /// </summary>
        public static readonly IReadOnlyList<AgeBucket> All = new[]
        {
            AgeBucket.Moins25,
            AgeBucket.Tranche25_34,
            AgeBucket.Tranche35_44,
            AgeBucket.Tranche45_54,
            AgeBucket.CinquanteCinqPlus
        };
    }
}
