using DevExpress.ExpressApp.DC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Domain
{
    public static class DomainEnums
    {
        public enum RubriqueTypeCalcul { Gain = 0, Retenue = 1}
        public enum SensAssiette { Plus = 1, Moins = -1 }
        public enum RubriqueSource
        {
            SaisieManuelle = 0,
            DepuisModele = 1,
            Calcul = 2,
            Import = 3
        }
        public enum SituationMaritale
        {
            Celibataire = 0,
            Marie = 1,
            Divorce = 2,
            Veuf = 3
        }
        public enum StatutConjoint
        {
            NonRenseigne = 0,
            Actif = 1,
            Inactif = 2
        }

             public enum RubriqueCanonique
        {
            // Gains / indemnités
            SalaireDeBase = 1,
            Sursalaire = 20,
            AvantageNatureVehicule = 180,
            PrimeAnciennete = 30,
            IndemniteLogement = 60,         
            PrimeTransport=83,           
            // Cotisations / retenues
            IPRES_RG = 200,  // Régime Général
            IPRES_RC = 210,  // Régime Cadre
            CSS_AccidentTravail = 220,
            CSS_AllocationFamiliale = 230,
            TRIMF = 300,  // Mensuel (barémisé + régul intégrée)
            IRPP = 310,   // Impôt sur le revenu (barémisé) - à paramétrer
            CFCE = 311,
            RemboursementPret =500,
            RemboursementAvance =501
        }

        public enum TypeAffectationCompte
        {
            Debit,      // charge (ou produit) côté rubrique
            Credit,     // dette / tiers / produit
            Taux1,      // compte porté par Taux1 (ex: part salarié)
            Taux2,      // compte porté par Taux2 (ex: part employeur)
            Employeur   // pour charges patronales si tu veux dissocier
        }

        public enum PeriodiciteBareme 
        { Mensuel = 0,
        Annuel = 1
        }
        public enum TrimfNature { Mensuel = 0, Annuel = 1 }

        public enum BulletinStatut
        {
            Brouillon = 0,
            Valide = 1,
            Exporte = 2,  // (héritage; peut servir de compat. pour "Comptabilisé")
            Imprime = 3,
            Envoye = 4,
            Comptabilise = 5,
            Cloture = 6
        }

        // ===== Prêts =====
        public enum PretStatut
        {
            Brouillon = 0,
            EnCours = 1,
            Termine = 2,
            Suspendu = 3
        }
        public enum PretEcheanceStatut
        {
            Prevue = 0,  // (ex "EnAttente")
            Prelevee = 1,
            Annulee = 2
        }

        public enum PretAmortissement
        {
            PrincipalConstant = 0,
            AnnuiteConstante = 1
        }

        public enum PeriodePaieStatut { Brouillon = 0, Ouverte = 1, Cloturee = 2 }

        public enum CongePaiementMode
        {
            IntegreAuBulletin = 0,
            BulletinCongeDedie = 1,
            AvanceConge = 2
        }

        public enum CongeDemandeStatut
        {
            Brouillon = 0,
            Validee = 1,
            Annulee = 2
        }

        public enum CongePaieStatut
        {
            ARegler = 0,
            Payee = 1
        }
        public enum CongeStatut { Brouillon = 0, Soumis = 1, Approuve = 2, Rejete = 3, Annule = 4 }
        public enum CongeImpactSalaire { Paye = 0, Impaye = 1, Partiel = 2 } // Partiel = maintien % (ex: maladie)
        public enum PretNature { Pret = 0, AvanceSalaire = 1 }

        public enum DossierCategorieDocument
        {
            [XafDisplayName("Administratif")]
            Administratif = 0,

            [XafDisplayName("Contractuel")]
            Contractuel = 1,

            [XafDisplayName("Médical")]
            Medical = 2,

            [XafDisplayName("Disciplinaire")]
            Disciplinaire = 3,

            [XafDisplayName("Formation / Diplômes")]
            Formation = 4,

            [XafDisplayName("Évaluation")]
            Evaluation = 5,

            [XafDisplayName("Paie")]
            Paie = 6,

            [XafDisplayName("Congé / Absence")]
            CongeAbsence = 7,

            [XafDisplayName("Autre")]
            Autre = 8
        }

        // ===== Évaluation / Entretiens annuels =====

        public enum EntretienStatut
        {
            Brouillon = 0,   // Créé par RH, non encore envoyé
            PlanifieRH = 1,   // Date fixée, convocation envoyée
            SaisieSalarie = 2,   // Formulaire auto-évaluation transmis au salarié
            EnCours = 3,   // Entretien réalisé, saisie manager en cours
            ValideManager = 4,   // Validé par le manager N+1
            Cloture = 5    // Archivé - lecture seule
        }
        public enum NoteEvaluation
        {
            NonEvalue = 0,
            Insuffisant = 1,
            AProgresser = 2,
            Satisfaisant = 3,
            Bien = 4,
            TresBien = 5
        }

        public enum ObjectifStatut
        {
            NonAtteint = 0,
            PartiellemEntAtteint = 1,
            Atteint = 2,
            Depasse = 3
        }

        public enum CampagneStatut
        {
            Brouillon = 0,
            Ouverte = 1,   // Entretiens en cours de création/envoi
            EnCours = 2,   // Tous les entretiens lancés
            Cloturee = 3    // Campagne archivée
        }

        public enum TypeCritere
        {
            Competence = 0,   // Savoir-faire technique
            Comportement = 1,   // Savoir-être
            ObjectifQuant = 2,   // Résultat mesurable
            ObjectifQual = 3    // Résultat qualitatif
        }

        // ===== Espace salarié self-service =====

        public enum AttestationNature
        {
            Travail = 0,   // Attestation de travail
            Salaire = 1,   // Attestation de salaire (avec montants)
            Conge = 2,   // Attestation de congé payé
            Emploi = 3,   // Certificat d'emploi (fin de contrat)
            Prise_En_Charge = 4    // Prise en charge assurance / logement
        }

        public enum DemandeStatut
        {
            // ── Niveaux hiérarchiques (avant RH) ──
            EnAttenteN1 = 1,   // En attente de validation du manager N+1
            EnAttenteN2 = 2,   // En attente de validation du manager N+2

            // ── Circuit RH ────────────────────────
            Soumise = 10,  // Validée par la hiérarchie (ou pas de hiérarchie) — visible RH
            EnTraitement = 11,  // RH a pris en charge
            Traitee = 20,  // Attestation générée et remise
            Rejetee = 30   // Rejetée à n'importe quel niveau
        }

        public enum NotificationPriorite
        {
            Info = 0,
            Important = 1,
            Urgent = 2
        }

        public enum NotificationStatut
        {
            NonLue = 0,
            Lue = 1,
            Archivee = 2
        }


    }
}
