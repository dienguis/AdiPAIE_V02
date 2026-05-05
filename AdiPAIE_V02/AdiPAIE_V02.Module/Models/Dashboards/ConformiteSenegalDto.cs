// =============================================================================
//  ConformiteSenegalDto.cs — V1.2 Sprint 5 (mai 2026)
//
//  Dashboard "Conformité Sénégal" (audit-ready).
//  Vérifie 9 indicateurs réglementaires SN avec feux tricolores.
//  Cf. MISSION_STATE 9.5
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class ConformiteSenegalDto
    {
        public List<IndicateurConformiteDto> Indicateurs { get; set; } = new();
        public KpiConformiteDto Kpis { get; set; } = new();
        public DateTime CalculatedAt { get; set; }
    }

    public sealed class KpiConformiteDto
    {
        public int NbIndicateurs { get; set; }
        public int NbConformes { get; set; }
        public int NbAvertissements { get; set; }
        public int NbNonConformes { get; set; }
        public decimal ScoreConformitePct { get; set; }
        public StatutConformite StatutGlobal { get; set; }
    }

    public sealed class IndicateurConformiteDto
    {
        public string Code { get; set; } = "";          // "SMIG", "IPRES", etc.
        public string Libelle { get; set; } = "";
        public string Description { get; set; } = "";
        public StatutConformite Statut { get; set; }
        public string Valeur { get; set; } = "";        // "12 / 247 salariés"
        public string SeuilLegal { get; set; } = "";    // "60 000 FCFA / mois"
        public int NbCasIdentifies { get; set; }
        public string Recommandation { get; set; } = "";
    }

    public enum StatutConformite
    {
        Conforme = 0,        // ✅ Vert
        Avertissement = 1,   // ⚠️ Jaune
        NonConforme = 2,     // ❌ Rouge
        NonEvalue = 3        // ⚪ Gris (données manquantes)
    }

    public sealed class ConformiteSenegalFilterModel
    {
        public DateTime DateReference { get; set; } = DateTime.Today;
        public Guid? SiteOid { get; set; }
        public string ToCacheKey() => $"conformite|d={DateReference:yyyyMMdd}|s={SiteOid}";
    }
}
