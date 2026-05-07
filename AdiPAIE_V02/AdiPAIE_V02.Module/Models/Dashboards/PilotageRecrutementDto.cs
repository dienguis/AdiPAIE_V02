// =============================================================================
//  PilotageRecrutementDto.cs — V1.4 (mai 2026)
//
//  Dashboard N°12 — Pilotage Recrutement.
//  Vue 360° du processus de recrutement ELTON :
//   - KPIs hauts : postes ouverts, candidatures actives, embauches, délai moyen
//   - Pipeline (funnel) par étape (Reçue → Embauché)
//   - Top sources (efficacité par origine candidat)
//   - Délai moyen recrutement par poste / par catégorie
//   - Coût moyen recrutement (ΣcoûtSource / nbEmbauchés)
//   - Liste postes ouverts avec statut, jours ouverts, nb candidats
//   - Évolution mensuelle (ouvertures vs embauches)
//   - Périodes d'essai en cours
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class PilotageRecrutementDto
    {
        public KpiPilotageRecrutementDto Kpis { get; set; } = new();

        // Pipeline (funnel)
        public List<EtapePipelineDto> Pipeline { get; set; } = new();

        // Top sources (par nb candidats / nb embauches / coût)
        public List<SourceRecrutementRowDto> Sources { get; set; } = new();

        // Délai moyen recrutement par catégorie professionnelle
        public List<DelaiCategorieRowDto> DelaiParCategorie { get; set; } = new();

        // Liste des postes ouverts (sortable par jours ouverts)
        public List<PosteOuvertRowDto> PostesOuverts { get; set; } = new();

        // Évolution mensuelle (ouvertures vs embauches sur 12 mois)
        public List<MoisRecrutementDto> Evolution { get; set; } = new();

        // Motifs de refus (côté entreprise et côté candidat)
        public List<MotifRefusRowDto> RefusEntreprise { get; set; } = new();
        public List<MotifRefusRowDto> RefusCandidat { get; set; } = new();

        // Périodes d'essai en cours
        public List<PeriodeEssaiRowDto> PeriodesEssaiEnCours { get; set; } = new();

        public int Annee { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    /// <summary>KPIs hauts du dashboard N°12.</summary>
    public sealed class KpiPilotageRecrutementDto
    {
        // Activité
        public int NbPostesOuverts { get; set; }            // Statut In(Valide, Publie, EnRecrutement)
        public int NbPostesPourvus { get; set; }            // Statut = Pourvu, dans l'année
        public int NbCandidaturesActives { get; set; }      // Statut < OffreAcceptee, hors Refuse/Desistement/Annule
        public int NbCandidaturesRecuesYTD { get; set; }    // Total reçues sur l'année

        // Performance
        public int NbEmbauchesYTD { get; set; }             // Statut = Embauche dans l'année
        public decimal DelaiMoyenRecrutementJ { get; set; } // moyenne JoursOuverts pour postes pourvus
        public decimal TauxConversionPct { get; set; }      // NbEmbauches / NbCandidaturesRecuesYTD × 100
        public decimal TauxAcceptationOffrePct { get; set; }// OffreAcceptee / OffrePropose × 100

        // Coût
        public decimal CoutTotalRecrutementYTD { get; set; }   // Σ coûtSource × nbCandidatsParSource
        public decimal CoutMoyenParEmbauche { get; set; }      // CoutTotal / NbEmbauches

        // Période d'essai
        public int NbPeriodesEssaiEnCours { get; set; }
        public int NbPeriodesEssaiConcluantesYTD { get; set; }
        public int NbPeriodesEssaiRompuesYTD { get; set; }
        public decimal TauxRetentionPostEssaiPct { get; set; }
    }

    /// <summary>Une étape du funnel de recrutement (avec valeur absolue + taux).</summary>
    public sealed class EtapePipelineDto
    {
        public int Ordre { get; set; }                  // pour le tri
        public string Etape { get; set; } = "";         // libellé étape
        public int NbCandidatures { get; set; }
        public decimal TauxVsPrecedentePct { get; set; } // % qui ont passé l'étape précédente
        public decimal TauxVsTotalPct { get; set; }      // % du total reçu
        public string CouleurHex { get; set; } = "";
    }

    /// <summary>Performance d'une source (LinkedIn, ANEM, ...).</summary>
    public sealed class SourceRecrutementRowDto
    {
        public Guid? SourceOid { get; set; }
        public string Source { get; set; } = "";
        public int NbCandidatures { get; set; }
        public int NbEntretiens { get; set; }
        public int NbEmbauches { get; set; }
        public decimal TauxConversionPct { get; set; }   // Embauches / Candidatures × 100
        public decimal CoutMoyenParCandidat { get; set; }
        public decimal CoutTotal { get; set; }            // CoutMoyen × NbCandidatures
        public decimal CoutParEmbauche { get; set; }      // CoutTotal / NbEmbauches
    }

    /// <summary>Délai recrutement moyen par catégorie professionnelle.</summary>
    public sealed class DelaiCategorieRowDto
    {
        public Guid? CategorieOid { get; set; }
        public string Categorie { get; set; } = "";
        public int NbPostesPourvus { get; set; }
        public decimal DelaiMoyenJ { get; set; }
        public decimal DelaiMinJ { get; set; }
        public decimal DelaiMaxJ { get; set; }
    }

    /// <summary>Une ligne pour la liste des postes ouverts.</summary>
    public sealed class PosteOuvertRowDto
    {
        public Guid PosteOid { get; set; }
        public string Code { get; set; } = "";
        public string Libelle { get; set; } = "";
        public string Departement { get; set; } = "";
        public string Site { get; set; } = "";
        public string Categorie { get; set; } = "";
        public string TypeContrat { get; set; } = "";
        public string Statut { get; set; } = "";
        public DateTime DateOuverture { get; set; }
        public int JoursOuverts { get; set; }
        public int NbCandidatures { get; set; }
        public int NbEntretiens { get; set; }
    }

    /// <summary>Évolution mensuelle ouverture / embauche.</summary>
    public sealed class MoisRecrutementDto
    {
        public int Annee { get; set; }
        public int Mois { get; set; }
        public string Libelle { get; set; } = "";   // "Jan 2026"
        public int NbOuvertures { get; set; }
        public int NbCandidatures { get; set; }
        public int NbEmbauches { get; set; }
    }

    /// <summary>Décomposition des motifs de refus (entreprise ou candidat).</summary>
    public sealed class MotifRefusRowDto
    {
        public string Motif { get; set; } = "";
        public int Nombre { get; set; }
        public decimal PourcentageDuTotal { get; set; }
    }

    /// <summary>Une période d'essai en cours.</summary>
    public sealed class PeriodeEssaiRowDto
    {
        public Guid PeriodeEssaiOid { get; set; }
        public Guid? SalarieOid { get; set; }
        public string SalarieNom { get; set; } = "";
        public string Poste { get; set; } = "";
        public string Site { get; set; } = "";
        public DateTime DateDebut { get; set; }
        public DateTime DateFinPrevue { get; set; }
        public int JoursEcoules { get; set; }
        public int JoursRestants { get; set; }
        public string Statut { get; set; } = "";
    }
}
