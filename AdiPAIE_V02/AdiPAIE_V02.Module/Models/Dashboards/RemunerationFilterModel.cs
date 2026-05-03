// =============================================================================
//  RemunerationFilterModel.cs
//  Tableau N°4 (Rémunération — Égalité des salaires).
//
//  Sources de données validées (cf. SPEC_Custom_Dashboard_SQL.sql) :
//    INTERNE :
//      - Masse Brute        = SUM(Bulletin.BrutFiscal)        (filtré Annee)
//      - Net total          = SUM(Bulletin.NetAPayer)
//      - Charges patronales = SUM(BulletinLigne.MontantEmployeur)
//      - Soft-delete        : Bulletin.GCRecord IS NULL
//    EXTERNE :
//      - Coût total         = ContratInterim.TauxJournalier × 22 jours ×
//                             DATEDIFF(month, DateDebut, COALESCE(DateFin, today))
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>
    /// Mode d'affichage des KPI rémunération.
    ///   <list type="bullet">
    ///     <item><c>CoutEmployeur</c> = BrutFiscal + ChargesPatronales (vue employeur)</item>
    ///     <item><c>RemunerationNette</c> = NetAPayer (vue salarié)</item>
    ///   </list>
    /// </summary>
    public enum RemunerationMode
    {
        CoutEmployeur     = 0,
        RemunerationNette = 1
    }

    /// <summary>Filtres pour le Tableau N°4 « Rémunération ».</summary>
    public sealed class RemunerationFilterModel
    {
        public PersonnelType    Personnel { get; set; } = PersonnelType.Interne;
        public RemunerationMode Mode      { get; set; } = RemunerationMode.CoutEmployeur;
        public int              Annee     { get; set; } = DateTime.Today.Year;

        public Guid?   SiteOid       { get; set; }   // Site (INTERNE) / StationService (EXTERNE)
        public Sexe?   Genre         { get; set; }
        public string? Segment       { get; set; }   // Departement.Nom (INTERNE) / BU (EXTERNE)
        public Guid?   CategorieOid  { get; set; }
        public AncienneteBucket? Anciennete { get; set; }

        public string ToCacheKey() =>
            $"remuneration|p={Personnel}|m={Mode}|a={Annee}|site={SiteOid}|g={Genre}|seg={Segment}|cat={CategorieOid}|anc={Anciennete}";
    }
}
