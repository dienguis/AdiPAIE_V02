// AdiPAIE_V02.Module/Services/RapportCEOData.cs
// DTO 100 % primitifs — thread-safe, aucun objet XPO.
using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Données agrégées pour le Rapport Exécutif CEO.
    /// Tous les champs sont des primitifs ou des listes de sous-DTO
    /// pour garantir la sécurité inter-threads (pas de Session XPO).
    /// </summary>
    public class RapportCEOData
    {
        // ── Contexte ──────────────────────────────────────────────
        public int Annee { get; set; }
        public int Mois { get; set; }
        public string PeriodeLibelle => $"{Mois:D2}/{Annee}";
        public string EntrepriseNom { get; set; } = "";
        public string EntrepriseNINEA { get; set; } = "";
        public byte[] LogoImage { get; set; }
        public DateTime DateGeneration { get; set; } = DateTime.Now;

        // ── 1. KPIs Effectif ──────────────────────────────────────
        public int EffectifTotal { get; set; }
        public int EffectifActif { get; set; }
        public int EffectifInactif { get; set; }
        public int NbHommes { get; set; }
        public int NbFemmes { get; set; }
        public double PctHommes { get; set; }
        public double PctFemmes { get; set; }
        public double AgeMoyen { get; set; }
        public double AncienneteMoyenne { get; set; }

        // ── 2. Masse salariale ────────────────────────────────────
        public decimal MasseSalarialeBrute { get; set; }
        public decimal MasseSalarialeNette { get; set; }
        public decimal TotalCotisationsPatronales { get; set; }
        public decimal TotalCotisationsSalariales { get; set; }
        public decimal TotalRetenusFiscales { get; set; }
        public decimal CoutTotalEmployeur { get; set; }
        public decimal SalaireMoyenBrut { get; set; }
        public decimal SalaireMoyenNet { get; set; }
        public decimal SalaireMedianBrut { get; set; }
        // Variation M-1
        public decimal MasseSalarialeBruteMoisPrecedent { get; set; }
        public double VariationMasseSalarialePct { get; set; }

        // ── 3. Turnover ───────────────────────────────────────────
        public int Entrees { get; set; }
        public int Sorties { get; set; }
        public double TauxTurnover { get; set; } // (Entrées+Sorties)/(2×Effectif)
        public double TauxTurnoverAnnuel { get; set; } // cumul annuel
        public List<MotifDepartItem> RepartitionMotifDepart { get; set; } = new();
        // Entrées du mois
        public int EntreesCDI { get; set; }
        public int EntreesCDD { get; set; }
        public int EntreesStage { get; set; }

        // ── 4. Congés & Absences ──────────────────────────────────
        public int CongesEnCours { get; set; }
        public int CongesAccordesMois { get; set; }
        public int CongesRefusesMois { get; set; }
        public int CongesEnAttente { get; set; }
        public double TauxAbsenteisme { get; set; }
        public int JoursAbsenceTotalMois { get; set; }
        public List<CongeParTypeItem> RepartitionCongesParType { get; set; } = new();

        // ── 5. Répartitions ───────────────────────────────────────
        public List<RepartitionItem> ParDepartement { get; set; } = new();
        public List<RepartitionItem> ParTypeContrat { get; set; } = new();
        public List<RepartitionItem> ParCategorie { get; set; } = new();
        public List<RepartitionItem> ParTrancheAge { get; set; } = new();
        public List<RepartitionItem> ParTrancheAnciennete { get; set; } = new();

        // ── 6. Évolution mensuelle (12 mois glissants) ────────────
        public List<EvolutionMensuelleItem> EvolutionEffectif { get; set; } = new();
        public List<EvolutionMensuelleItem> EvolutionMasseSalariale { get; set; } = new();

        // ── 7. Contrats ───────────────────────────────────────────
        public int NbCDI { get; set; }
        public int NbCDD { get; set; }
        public int NbStage { get; set; }
        public int CDDExpirantSous30Jours { get; set; }
        public int CDDExpirantSous60Jours { get; set; }

        // ── 8. Alertes ────────────────────────────────────────────
        public List<AlerteItem> Alertes { get; set; } = new();
        public double SeuilTurnover { get; set; }
        public double SeuilAbsenteisme { get; set; }
        public decimal SeuilMasseSalariale { get; set; }

        // ── 9. Top salaires (anonymisé ou non selon config) ───────
        public List<TopSalaireItem> TopSalaires { get; set; } = new();
    }

    // ── Sous-DTO ──────────────────────────────────────────────────

    public class RepartitionItem
    {
        public string Libelle { get; set; } = "";
        public int Effectif { get; set; }
        public double Pourcentage { get; set; }
        public decimal MasseSalariale { get; set; }
    }

    public class EvolutionMensuelleItem
    {
        public int Annee { get; set; }
        public int Mois { get; set; }
        public string Periode => $"{Mois:D2}/{Annee}";
        public int Effectif { get; set; }
        public decimal MasseSalarialeBrute { get; set; }
        public int Entrees { get; set; }
        public int Sorties { get; set; }
    }

    public class MotifDepartItem
    {
        public string Motif { get; set; } = "";
        public int Nombre { get; set; }
        public double Pourcentage { get; set; }
    }

    public class CongeParTypeItem
    {
        public string TypeConge { get; set; } = "";
        public int NbDemandes { get; set; }
        public decimal JoursTotaux { get; set; }
        public double Pourcentage { get; set; }
    }

    public class AlerteItem
    {
        public string Niveau { get; set; } = "Info"; // Info, Warning, Danger
        public string Titre { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class TopSalaireItem
    {
        public string Departement { get; set; } = "";
        public decimal SalaireMoyenBrut { get; set; }
        public int Effectif { get; set; }
    }
}
