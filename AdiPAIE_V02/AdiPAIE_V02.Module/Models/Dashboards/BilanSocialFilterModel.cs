// =============================================================================
//  BilanSocialFilterModel.cs
//  Tableau N°6 (Bilan Social Mensuel) — INTERNE.
//
//  Synthèse mensuelle 12 mois × N indicateurs, alimentée par :
//    - Salarie       (effectif, embauches, départs)
//    - Bulletin      (masse salariale mensuelle)
//    - BulletinLigne (charges patronales mensuelles)
//    - CongeDemande  (jours d'absence, employés absents)
//
//  Filtres : Année (slicer), Site (optionnel).
// =============================================================================

using System;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>Filtres pour le Tableau N°6 « Bilan Social Mensuel ».</summary>
    public sealed class BilanSocialFilterModel
    {
        public int   Annee   { get; set; } = DateTime.Today.Year;
        public Guid? SiteOid { get; set; }

        public string ToCacheKey() =>
            $"bilansocial|a={Annee}|site={SiteOid}";
    }
}
