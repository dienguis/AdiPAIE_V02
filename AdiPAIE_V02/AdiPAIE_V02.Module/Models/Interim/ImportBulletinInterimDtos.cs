// =============================================================================
//  ImportBulletinInterimDtos.cs — V1.3 Sprint 1 (mai 2026)
//
//  DTOs intermédiaires pour le wizard d'import :
//    - LignePreviewDto    : 1 ligne du fichier après parsing (avant commit)
//    - ImportPreviewDto   : résultat global de l'analyse pré-commit
//    - ImportResultDto    : résultat du commit (objets persistés)
// =============================================================================

using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Models.Interim
{
    /// <summary>
    /// Résultat d'une étape de preview : le service a parsé le fichier mais
    /// n'a RIEN persisté. L'utilisateur valide ensuite ce preview pour commit.
    /// </summary>
    public sealed class ImportPreviewDto
    {
        public int Annee { get; set; }
        public int Mois { get; set; }
        public Guid SocieteOid { get; set; }
        public string SocieteNom { get; set; } = "";
        public string FichierSource { get; set; } = "";

        // Lignes parsées
        public List<LignePreviewDto> Lignes { get; set; } = new();

        // Compteurs synthèse
        public int NbLignesTotal => Lignes.Count;
        public int NbLignesOk { get; set; }              // Match par matricule
        public int NbLignesACreer { get; set; }          // Création auto Interimaire
        public int NbLignesPrestataire { get; set; }     // PRESTATAIRE
        public int NbLignesErreur { get; set; }          // Données invalides

        // Totaux financiers (pour confirmation utilisateur)
        public decimal TotalDebours { get; set; }
        public decimal TotalCommissionAgence { get; set; }
        public decimal TotalHT { get; set; }
        public decimal TotalTVA { get; set; }
        public decimal TotalTTC { get; set; }

        // Multiplicateur global Brut → TTC pour ce batch
        public decimal TotalBrutImposable { get; set; }
        public decimal MultiplicateurBrutTTC =>
            TotalBrutImposable > 0 ? Math.Round(TotalTTC / TotalBrutImposable, 2) : 0m;

        // Idempotence : true si un batch existe déjà pour (Année, Mois, Société)
        public bool BatchDejaExistant { get; set; }

        // Erreurs globales (entête manquant, format invalide…)
        public List<string> Erreurs { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        /// <summary>True si on peut commiter (pas d'erreur globale).</summary>
        public bool PeutCommiter => Erreurs.Count == 0 && NbLignesTotal > 0;
    }

    /// <summary>
    /// Une ligne du fichier Excel après parsing.
    /// </summary>
    public sealed class LignePreviewDto
    {
        public int LigneFichier { get; set; }                  // numéro de ligne dans le fichier source
        public LignePreviewStatut Statut { get; set; }
        public string MatriculeOriginal { get; set; } = "";
        public string Nom { get; set; } = "";
        public string Prenom { get; set; } = "";
        public string Sexe { get; set; } = "";
        public string Fonction { get; set; } = "";
        public string SiteAffectation { get; set; } = "";

        public Guid? InterimaireExistantOid { get; set; }      // null si à créer ou prestataire

        public decimal Trentieme { get; set; }
        public decimal SalaireBase { get; set; }
        public decimal BrutImposable { get; set; }

        public decimal IpresSal { get; set; }
        public decimal IpresPat { get; set; }
        public decimal CssAll { get; set; }
        public decimal CssAcc { get; set; }
        public decimal IpmSal { get; set; }
        public decimal IpmPat { get; set; }

        public decimal CFCE { get; set; }
        public decimal RetenueIR { get; set; }
        public decimal RetenueTRIMF { get; set; }

        public decimal PrimeTransport { get; set; }
        public decimal PrimePanier { get; set; }
        public decimal IndemnitesDiverses { get; set; }

        public decimal NetAPayer { get; set; }

        public decimal Debours { get; set; }
        public decimal CommissionAgence { get; set; }
        public decimal MontantHT { get; set; }
        public decimal TVA { get; set; }
        public decimal TTC { get; set; }

        public string Erreur { get; set; } = "";
    }

    public enum LignePreviewStatut
    {
        OK = 0,                    // Match par matricule existant
        ACreer = 1,                // Pas de match → fiche Interimaire à créer auto
        Prestataire = 2,           // Matricule = "PRESTATAIRE"
        Erreur = 99
    }

    /// <summary>
    /// Résultat du commit : objets persistés et statistiques finales.
    /// </summary>
    public sealed class ImportResultDto
    {
        public Guid BatchOid { get; set; }
        public int NbBulletinsCrees { get; set; }
        public int NbInterimairesCreesAuto { get; set; }
        public int NbPrestataires { get; set; }
        public decimal TotalTTC { get; set; }
        public List<string> InterimairesCreesNoms { get; set; } = new();
        public bool BatchEcrase { get; set; }
        public int NbBulletinsAnciensSupprimes { get; set; }
    }
}
