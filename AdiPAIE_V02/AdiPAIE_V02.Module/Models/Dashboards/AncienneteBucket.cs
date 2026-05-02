// =============================================================================
//  AncienneteBucket.cs
//  Tableau N°2 (Analyse de l'Effectif) — tranches d'ancienneté.
//
//  Tranches définies par la mission :
//    <1 an / 1-4 ans / 5-9 ans / 10-14 ans / ≥15 ans / vide
// =============================================================================

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>Tranche d'ancienneté d'un salarié à une date d'observation.</summary>
    public enum AncienneteBucket
    {
        Moins1An       = 0,
        Tranche1a4     = 1,
        Tranche5a9     = 2,
        Tranche10a14   = 3,
        QuinzeAnsPlus  = 4,
        NonRenseigne   = 5
    }

    public static class AncienneteBucketHelper
    {
        public static AncienneteBucket FromAnnees(int? annees) => annees switch
        {
            null  => AncienneteBucket.NonRenseigne,
            < 1   => AncienneteBucket.Moins1An,
            < 5   => AncienneteBucket.Tranche1a4,
            < 10  => AncienneteBucket.Tranche5a9,
            < 15  => AncienneteBucket.Tranche10a14,
            _     => AncienneteBucket.QuinzeAnsPlus
        };

        public static string GetLibelle(AncienneteBucket b) => b switch
        {
            AncienneteBucket.Moins1An      => "<1 an",
            AncienneteBucket.Tranche1a4    => "1-4 ans",
            AncienneteBucket.Tranche5a9    => "5-9 ans",
            AncienneteBucket.Tranche10a14  => "10-14 ans",
            AncienneteBucket.QuinzeAnsPlus => "≥15 ans",
            AncienneteBucket.NonRenseigne  => "Vide",
            _                              => "?"
        };

        public static readonly IReadOnlyList<AncienneteBucket> All = new[]
        {
            AncienneteBucket.Moins1An,
            AncienneteBucket.Tranche1a4,
            AncienneteBucket.Tranche5a9,
            AncienneteBucket.Tranche10a14,
            AncienneteBucket.QuinzeAnsPlus,
            AncienneteBucket.NonRenseigne
        };
    }

    /// <summary>
    /// Tranches d'âge spécifiques au Tableau N°2 (différentes de Tableau N°1) :
    /// &lt;30 / 30-39 / 40-49 / ≥50 / vide.
    /// </summary>
    public enum AgeBucketAnalyse
    {
        Moins30      = 0,
        Tranche30_39 = 1,
        Tranche40_49 = 2,
        CinqAnsPlus  = 3,
        NonRenseigne = 4
    }

    public static class AgeBucketAnalyseHelper
    {
        public static AgeBucketAnalyse FromAge(int? age) => age switch
        {
            null  => AgeBucketAnalyse.NonRenseigne,
            < 30  => AgeBucketAnalyse.Moins30,
            < 40  => AgeBucketAnalyse.Tranche30_39,
            < 50  => AgeBucketAnalyse.Tranche40_49,
            _     => AgeBucketAnalyse.CinqAnsPlus
        };

        public static string GetLibelle(AgeBucketAnalyse b) => b switch
        {
            AgeBucketAnalyse.Moins30      => "<30",
            AgeBucketAnalyse.Tranche30_39 => "30-39",
            AgeBucketAnalyse.Tranche40_49 => "40-49",
            AgeBucketAnalyse.CinqAnsPlus  => "≥50",
            AgeBucketAnalyse.NonRenseigne => "Vide",
            _                             => "?"
        };

        public static readonly IReadOnlyList<AgeBucketAnalyse> All = new[]
        {
            AgeBucketAnalyse.Moins30,
            AgeBucketAnalyse.Tranche30_39,
            AgeBucketAnalyse.Tranche40_49,
            AgeBucketAnalyse.CinqAnsPlus,
            AgeBucketAnalyse.NonRenseigne
        };
    }
}
