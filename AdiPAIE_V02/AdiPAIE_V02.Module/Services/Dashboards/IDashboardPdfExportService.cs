// =============================================================================
//  IDashboardPdfExportService.cs
//  Service partagé d'export PDF (.pdf) des 6 tableaux de bord RH.
//  Utilise QuestPDF (à ajouter au .csproj — voir CHANGELOG Étape 7.3).
// =============================================================================

using AdiPAIE_V02.Module.Models.Dashboards;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <summary>Génère un PDF A4 paysage à partir d'un DTO de dashboard.</summary>
    public interface IDashboardPdfExportService
    {
        (byte[] Bytes, string FileName) ExportEffectifDetaille(EffectifDetailleDto dto, EffectifDetailleFilterModel filter);
        (byte[] Bytes, string FileName) ExportAnalyseEffectif(AnalyseEffectifDto dto, AnalyseEffectifFilterModel filter);
        (byte[] Bytes, string FileName) ExportMouvements(MouvementsDto dto, MouvementsFilterModel filter);
        (byte[] Bytes, string FileName) ExportRemuneration(RemunerationDto dto, RemunerationFilterModel filter);
        (byte[] Bytes, string FileName) ExportSuiviAbsences(SuiviAbsencesDto dto, SuiviAbsencesFilterModel filter);
        (byte[] Bytes, string FileName) ExportBilanSocial(BilanSocialDto dto, BilanSocialFilterModel filter);
    }
}
