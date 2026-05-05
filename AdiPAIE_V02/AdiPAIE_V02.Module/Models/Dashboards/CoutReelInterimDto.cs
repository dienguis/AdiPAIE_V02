// =============================================================================
//  CoutReelInterimDto.cs — V1.3 Sprint 1 (mai 2026)
//
//  Dashboard N°11 — Coût Réel Intérimaires.
//  Compare le coût réel facturé (BulletinInterim.TTC) avec le coût théorique
//  contrat (TauxJournalier × 22 jours × nbMois). Ventilé par intérimaire,
//  par site, par société d'intérim, sur 12 mois glissants.
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class CoutReelInterimDto
    {
        public KpiCoutReelDto Kpis { get; set; } = new();

        // Décomposition financière (donut)
        public List<DecompositionFacturationDto> Decomposition { get; set; } = new();

        // Top 10 intérimaires les plus coûteux (TTC)
        public List<CoutInterimaireDto> Top10 { get; set; } = new();

        // Évolution sur 12 mois glissants (TTC mensuel)
        public List<EvolutionTTCDto> Evolution { get; set; } = new();

        // Comparaison contrat (théorique) vs réel (TTC) — détail par intérimaire
        public List<EcartContratReelDto> EcartContratReel { get; set; } = new();

        // Coût par société d'intérim
        public List<CoutSocieteDto> ParSociete { get; set; } = new();

        public int Annee { get; set; }
        public int? Mois { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    public sealed class KpiCoutReelDto
    {
        // Mois cible
        public decimal TTCMois { get; set; }
        public decimal BrutMois { get; set; }
        public decimal MultiplicateurMois { get; set; }
        public int NbBulletinsMois { get; set; }
        public decimal CoutMoyenInterimaire { get; set; }

        // Année cible (cumul YTD)
        public decimal TTCAnneeYTD { get; set; }
        public decimal BrutAnneeYTD { get; set; }

        // Variation mois précédent
        public decimal VariationTTCMoisPrec { get; set; }
        public decimal VariationTTCMoisPrecPct { get; set; }

        // Décomposition globale du mois
        public decimal TotalDeboursMois { get; set; }
        public decimal TotalCommissionMois { get; set; }
        public decimal TotalTVAMois { get; set; }

        // Part de la commission sur le HT (la marge moyenne des sociétés d'intérim)
        public decimal TauxCommissionMoyenPct { get; set; }
    }

    public sealed class DecompositionFacturationDto
    {
        public string Composante { get; set; } = "";
        public decimal Montant { get; set; }
        public decimal PourcentageDuTotal { get; set; }
        public string CouleurHex { get; set; } = "";
    }

    public sealed class CoutInterimaireDto
    {
        public Guid? InterimaireOid { get; set; }
        public string Matricule { get; set; } = "";
        public string NomComplet { get; set; } = "";
        public string Fonction { get; set; } = "";
        public string Site { get; set; } = "";
        public string SocieteInterim { get; set; } = "";
        public bool IsPrestataire { get; set; }
        public decimal Trentieme { get; set; }
        public decimal BrutImposable { get; set; }
        public decimal NetAPayer { get; set; }
        public decimal TTC { get; set; }
        public decimal Multiplicateur { get; set; }
    }

    public sealed class EvolutionTTCDto
    {
        public int Annee { get; set; }
        public int Mois { get; set; }
        public string Libelle { get; set; } = "";   // "Jan 2026"
        public decimal TTC { get; set; }
        public decimal Brut { get; set; }
        public int NbBulletins { get; set; }
    }

    public sealed class EcartContratReelDto
    {
        public Guid? InterimaireOid { get; set; }
        public string Matricule { get; set; } = "";
        public string NomComplet { get; set; } = "";
        public decimal CoutContratTheorique { get; set; }   // TauxJournalier × 22 (× nb mois)
        public decimal CoutReelTTC { get; set; }
        public decimal Ecart { get; set; }                   // Réel - Théorique
        public decimal EcartPct { get; set; }                // % du théorique
        public bool ContratIntrouvable { get; set; }
    }

    public sealed class CoutSocieteDto
    {
        public Guid? SocieteOid { get; set; }
        public string SocieteNom { get; set; } = "";
        public int NbBulletins { get; set; }
        public decimal TotalDebours { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal TotalTTC { get; set; }
        public decimal TauxCommissionPct { get; set; }   // commission / HT × 100
    }

    public sealed class CoutReelInterimFilterModel
    {
        public int Annee { get; set; } = DateTime.Today.Year;
        public int? Mois { get; set; } = DateTime.Today.Month;     // null = vue annuelle
        public Guid? SiteOid { get; set; }
        public Guid? SocieteInterimOid { get; set; }

        public string ToCacheKey() =>
            $"cout-reel-interim|y={Annee}|m={Mois}|s={SiteOid}|so={SocieteInterimOid}";
    }
}
