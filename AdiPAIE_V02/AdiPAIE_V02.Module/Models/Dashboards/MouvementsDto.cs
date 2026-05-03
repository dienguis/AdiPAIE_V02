// =============================================================================
//  MouvementsDto.cs
//  Tableau N°3 (Mouvements) — DTO renvoyés par le service au Razor.
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class MouvementsDto
    {
        public KpiMouvementsDto Kpis { get; set; } = new();

        // Section ARRIVÉES
        public List<BarItemDto> ArriveesParMois      { get; set; } = new();   // 12 valeurs (Jan..Déc)
        public List<BarItemDto> ArriveesParSite      { get; set; } = new();   // Site / StationService
        public List<BarItemDto> ArriveesParCategorie { get; set; } = new();   // Categories / Poste

        // Section DÉPARTS
        public List<BarItemDto> DepartsParMois       { get; set; } = new();   // 12 valeurs
        public List<BarItemDto> DepartsParMotif      { get; set; } = new();   // MotifDepart enum / TypeMouvement
        public List<BarItemDto> DepartsParSite       { get; set; } = new();
        public List<BarItemDto> DepartsParCategorie  { get; set; } = new();

        public DateTime CalculatedAt { get; set; }
    }

    public sealed class KpiMouvementsDto
    {
        public int     NbArrivees     { get; set; }
        public int     NbDeparts      { get; set; }
        public int     Solde          => NbArrivees - NbDeparts;
        public decimal TauxArrivees   { get; set; }   // % sur effectif moyen
        public decimal TauxDeparts    { get; set; }   // % sur effectif moyen
        public int     EffectifDebut  { get; set; }
        public int     EffectifFin    { get; set; }
    }
}
