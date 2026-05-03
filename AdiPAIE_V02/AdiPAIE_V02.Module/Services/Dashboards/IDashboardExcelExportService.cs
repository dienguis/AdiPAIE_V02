// =============================================================================
//  IDashboardExcelExportService.cs
//  Service partagé d'export Excel (.xlsx) des 6 tableaux de bord RH.
//  Utilise ClosedXML (déjà présent dans le projet).
// =============================================================================

using AdiPAIE_V02.Module.Models.Dashboards;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <summary>
    /// Génère un fichier Excel à partir d'un DTO de dashboard.
    /// Retourne (bytes, suggestedFileName).
    /// </summary>
    public interface IDashboardExcelExportService
    {
        (byte[] Bytes, string FileName) ExportEffectifDetaille(EffectifDetailleDto dto, EffectifDetailleFilterModel filter);
        (byte[] Bytes, string FileName) ExportAnalyseEffectif(AnalyseEffectifDto dto, AnalyseEffectifFilterModel filter);
        (byte[] Bytes, string FileName) ExportMouvements(MouvementsDto dto, MouvementsFilterModel filter);
        (byte[] Bytes, string FileName) ExportRemuneration(RemunerationDto dto, RemunerationFilterModel filter);
        (byte[] Bytes, string FileName) ExportSuiviAbsences(SuiviAbsencesDto dto, SuiviAbsencesFilterModel filter);
        (byte[] Bytes, string FileName) ExportBilanSocial(BilanSocialDto dto, BilanSocialFilterModel filter);
    }
}
