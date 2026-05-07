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
            RemboursementAvance =501,
            HeuresSupplementaires = 600
        }

        public enum TypeHeureSupplementaire
        {
            [XafDisplayName("Jour ouvrable (15%)")]
            JourOuvrable = 0,

            [XafDisplayName("Nuit (40%)")]
            Nuit = 1,

            [XafDisplayName("Dimanche / Jour férié (60%)")]
            DimancheFerie = 2,

            [XafDisplayName("Nuit dimanche / Jour férié (100%)")]
            NuitDimancheFerie = 3,
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
            // V1.4.3 - Sémantique mise à jour : "Envoye" = bulletin PUBLIÉ dans
            // l'Espace Salarié (PDF archivé sur Bulletin.PdfArchive + email
            // de notification envoyé au salarié). Le PDF n'est plus envoyé
            // en pièce jointe ni protégé par clé : le salarié le télécharge
            // depuis l'Espace Salarié authentifié.
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
       
        public enum CongeImpactSalaire { Paye = 0, Impaye = 1 } // Partiel = maintien % (ex: maladie)
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
            Autre = 8,
                [XafDisplayName("Attestation")]
           Attestation = 9,

            [XafDisplayName("Bulletin de paie")]
            BulletinPaie = 10,
        }

        // ===== Évaluation / Entretiens annuels =====

        public enum EntretienStatut
        {
            [XafDisplayName("Brouillon")]
            Brouillon = 0,   // Créé par RH, non encore envoyé

            [XafDisplayName("Planifié RH")]
            PlanifieRH = 1,   // Date fixée, prêt à lancer

            [XafDisplayName("Saisie manager")]
            SaisieManager = 2,   // N+1 remplit l'évaluation complète

            [XafDisplayName("Observations salarié")]
            SaisieSalarie = 3,   // Salarié remplit ses observations uniquement

            [XafDisplayName("Validation N+1")]
            ValidationN1 = 4,   // N+1 prend connaissance des observations et valide

            [XafDisplayName("En attente N+2")]
            EnAttenteN2 = 5,   // Soumis au N+2 pour validation finale

            [XafDisplayName("Soumise RH")]
            SoumiseRH = 6,   // Validé par toute la hiérarchie — RH peut clôturer

            [XafDisplayName("Clôturé")]
            Cloture = 7,   // Archivé — lecture seule totale
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
            Conge = 1,   // Attestation de congé payé
            Emploi = 2,   // Certificat d'emploi (fin de contrat)
            CessationPaiement= 3,    // Attestation de Cessation de Paitement
            DomiciliationSalaire =4

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
        public enum CongeStatut
        {
            // ── Salarié ────────────────────────────────────────
            [XafDisplayName("Brouillon")]
            Brouillon = 0,   // Salarié peut modifier librement

            // ── Circuit hiérarchique ───────────────────────────
            [XafDisplayName("En attente N+1")]
            EnAttenteN1 = 1,   // Soumis, attend validation N+1

            [XafDisplayName("En attente N+2")]
            EnAttenteN2 = 2,   // Validé N+1, attend validation N+2

            // ── Circuit RH ────────────────────────────────────
            [XafDisplayName("Soumise")]
            Soumise = 10,  // Validée par hiérarchie, visible RH

            [XafDisplayName("Accordée")]
            Accordee = 20,  // RH a accordé le congé

            [XafDisplayName("Refusée")]
            Refusee = 30,  // RH a refusé

            [XafDisplayName("Annulée")]
            Annulee = 40,  // RH a annulé après accord
        }

        /// <summary>Note globale A+ à F (synthèse évaluation ELTON)</summary>
        public enum NoteGlobale
        {
            [XafDisplayName("A+ — Excellent")] APlus = 0,
            [XafDisplayName("A — Très bien")] A = 1,
            [XafDisplayName("B — Bien")] B = 2,
            [XafDisplayName("C — Satisfaisant")] C = 3,
            [XafDisplayName("D — Passable")] D = 4,
            [XafDisplayName("E — Insuffisant")] E = 5,
            [XafDisplayName("F — Très insuffisant")] F = 6,
        }

        /// <summary>Note pour les missions/responsabilités (Partie I-A)</summary>
        public enum NoteMission
        {
            [XafDisplayName("S/O")] SO = 0,
            [XafDisplayName("Insuffisant")] Insuffisant = 1,
            [XafDisplayName("Passable")] Passable = 2,
            [XafDisplayName("Satisfait")] Satisfait = 3,
            [XafDisplayName("Supérieur")] Superieur = 4,
        }

        /// <summary>Niveau d'atteinte des objectifs</summary>
        public enum NiveauAtteinte
        {
            [XafDisplayName("Non réalisé")] NonRealise = 0,
            [XafDisplayName("En partie atteint")] EnPartieAtteint = 1,
            [XafDisplayName("Atteint")] Atteint = 2,
            [XafDisplayName("Dépassé")] Depasse = 3,
            [XafDisplayName("Supérieur")] Superieur = 4,
        }

        /// <summary>Niveau de maîtrise pour les aptitudes management (Partie III)</summary>
        public enum NiveauMaitrise
        {
            [XafDisplayName("À acquérir")] AAccquerir = 0,
            [XafDisplayName("À développer")] ADevelopper = 1,
            [XafDisplayName("Maîtrise")] Maitrise = 2,
            [XafDisplayName("Excellente maîtrise")] ExcellenteMaitrise = 3,
        }

        /// <summary>Statut de la demande de déplacement</summary>
        public enum DeplacementStatut
        {
            [XafDisplayName("Brouillon")]
            Brouillon = 0,   // Salarié peut modifier

            [XafDisplayName("En attente N+1")]
            EnAttenteN1 = 1,   // Soumis au responsable

            [XafDisplayName("En attente assistant RH")]
            SoumiseAssistant = 10,  // Validé N+1, assistant prépare l'ordre

            [XafDisplayName("En attente RH")]
            EnAttenteRH = 11,  // Assistant a soumis au RH

            [XafDisplayName("Approuvée RH")]
            ApprouveeRH = 20,  // RH a approuvé, DAF notifié

            [XafDisplayName("En attente comptable")]
            EnAttenteComptable = 21,  // DAF a validé, comptable notifié

            [XafDisplayName("Traitée")]
            Traitee = 30,  // Comptable a confirmé, archivé

            [XafDisplayName("Rejetée")]
            Rejetee = 40,  // Rejet à n'importe quelle étape
        }

        /// <summary>Type de calcul pour une catégorie de frais</summary>
        public enum FraisCalculMode
        {
            [XafDisplayName("Taux journalier (montant × jours)")]
            TauxJournalier = 0,

            [XafDisplayName("Forfait (montant fixe)")]
            Forfait = 1,

            [XafDisplayName("Kilométrique (montant × km)")]
            Kilometrique = 2,
        }

        // ── Formation ─────────────────────────────────────────────
        public enum PlanFormationStatut
        {
            Brouillon = 0,
            Soumis = 1,
            Approuve = 2,
            EnCours = 3,
            Cloture = 4,
            Annule = 5
        }

        public enum SessionFormationStatut
        {
            Planifiee = 0,
            Confirmee = 1,
            EnCours = 2,
            Terminee = 3,
            Annulee = 4
        }

        public enum InscriptionStatut
        {
            EnAttente = 0,
            Confirmee = 1,
            Annulee = 2,
            Absente = 3   // salarié ne s'est pas présenté
        }

        public enum FormationModalite
        {
            Presentiel = 0,
            Distanciel = 1,
            Mixte = 2,
            ELearning = 3
        }

        public enum FormationCategorie
        {
            Technique = 0,
            Management = 1,
            Commercial = 2,
            Reglementaire = 3,
            SecuriteHygiene = 4,
            Informatique = 5,
            Langue = 6,
            Autre = 7
        }

        public enum EvaluationFormationNote
        {
            TresInsatisfaisant = 1,
            Insatisfaisant = 2,
            Satisfaisant = 3,
            Bien = 4,
            Excellent = 5
        }

        // ── Congés avancés ────────────────────────────────────────
        public enum FamilleConge
        {
            [XafDisplayName("Congé annuel")]
            Annuel = 0,
            [XafDisplayName("Maladie")]
            Maladie = 1,
            [XafDisplayName("Maternité / Paternité")]
            Maternite = 2,
            [XafDisplayName("Événement familial")]
            EvenementFamilial = 3,
            [XafDisplayName("Sans solde")]
            SansSolde = 4,
            [XafDisplayName("Récupération")]
            Recuperation = 5,
            [XafDisplayName("Autre")]
            Autre = 6,
        }

        public enum MouvementSoldeType
        {
            [XafDisplayName("Acquisition mensuelle")]
            AcquisitionMensuelle = 0,
            [XafDisplayName("Prise de congé")]
            PriseCongé = 1,
            [XafDisplayName("Report N-1")]
            Report = 2,
            [XafDisplayName("Ajustement manuel")]
            AjustementManuel = 3,
            [XafDisplayName("Annulation congé")]
            AnnulationCongé = 4,
            [XafDisplayName("Initialisation")]
            Initialisation = 5,
        }

        public enum SoldeCongeStatut
        {
            Actif = 0,
            Archive = 1,
        }


        // ── Avancements / Promotions ──────────────────────────────
        public enum AvancementStatut
        {
            [XafDisplayName("Brouillon")]
            Brouillon = 0,
            [XafDisplayName("Soumis RH")]
            SoumisRH = 1,
            [XafDisplayName("Approuvé DG")]
            ApprouveDG = 2,
            [XafDisplayName("Appliqué")]
            Applique = 3,   // Fiche salarié mise à jour
            [XafDisplayName("Rejeté")]
            Rejete = 4,
            [XafDisplayName("Annulé")]
            Annule = 5,
        }

        public enum TypeAvancement
        {
            [XafDisplayName("Promotion (changement de fonction)")]
            Promotion = 0,
            [XafDisplayName("Avancement d'échelon")]
            AvancementEchelon = 1,
            [XafDisplayName("Augmentation de salaire")]
            Augmentation = 2,
            [XafDisplayName("Changement de département")]
            ChangementDept = 3,
            [XafDisplayName("Revalorisation globale")]
            Revalorisation = 4,
        }

        // ── Évaluation à froid — Enums formulaire ────────────────
        public enum BesoinFormationReponse
        {
            [XafDisplayName("Oui")]
            Oui = 0,
            [XafDisplayName("Non")]
            Non = 1,
            [XafDisplayName("Je ne sais pas")]
            NSP = 2,
        }

        public enum AdequationFormationReponse
        {
            [XafDisplayName("Oui, parfaitement")]
            OuiParfaitement = 0,
            [XafDisplayName("Oui, partiellement")]
            OuiPartiellement = 1,
            [XafDisplayName("Non")]
            Non = 2,
            [XafDisplayName("Je ne sais pas")]
            NSP = 3,
        }

        public enum InitiativeFormationReponse
        {
            [XafDisplayName("Le manager")]
            Manager = 0,
            [XafDisplayName("Le collaborateur")]
            Collaborateur = 1,
            [XafDisplayName("Les deux")]
            LesDeux = 2,
        }

        public enum MisePratiqueReponse
        {
            [XafDisplayName("Oui")]
            Oui = 0,
            [XafDisplayName("Oui, partiellement")]
            OuiPartiellement = 1,
            [XafDisplayName("Non")]
            Non = 2,
        }

        public enum FrequencePratiqueReponse
        {
            [XafDisplayName("Régulièrement")]
            Regulierement = 0,
            [XafDisplayName("Occasionnellement")]
            Occasionnellement = 1,
        }

        public enum ResultatAtteintReponse
        {
            [XafDisplayName("En totalité")]
            EnTotalite = 0,
            [XafDisplayName("Partiellement")]
            Partiellement = 1,
            [XafDisplayName("Non")]
            Non = 2,
        }

        // ── Intérimaires ──────────────────────────────────────────────────────

        public enum InterimaireStatut
        {
            [XafDisplayName("Actif")] Actif = 0,
            [XafDisplayName("En mission")] EnMission = 1,
            [XafDisplayName("Disponible")] Disponible = 2,
            [XafDisplayName("Inactif")] Inactif = 3,
            [XafDisplayName("Blacklisté")] Blackliste = 4,
        }

        public enum ContratInterimType
        {
            [XafDisplayName("Première mission")] PremiereMission = 0,
            [XafDisplayName("Renouvellement")] Renouvellement = 1,
        }

        public enum ContratInterimStatut
        {
            [XafDisplayName("Brouillon")] Brouillon = 0,
            [XafDisplayName("En cours")] EnCours = 1,
            [XafDisplayName("Terminé")] Termine = 2,
            [XafDisplayName("Résilié")] Resilie = 3,
        }

        public enum DemandeInterimaireStatut
        {
            [XafDisplayName("Brouillon")] Brouillon = 0,
            [XafDisplayName("En attente N+1")] EnAttenteN1 = 1,
            [XafDisplayName("En attente RH")] EnAttenteRH = 2,
            [XafDisplayName("En attente RFE")] EnAttenteRFE = 3,
            [XafDisplayName("Acceptée")] Acceptee = 10,
            [XafDisplayName("En cours d'attribution")] EnCoursAttribution = 11,
            [XafDisplayName("Intérimaire affecté")] InterimaireAffecte = 12,
            [XafDisplayName("Refusée")] Refusee = 20,
            [XafDisplayName("Annulée")] Annulee = 21,
        }

        public enum MouvementInterimaireType
        {
            [XafDisplayName("Affectation")] Affectation = 0,
            [XafDisplayName("Réaffectation")] Reaffectation = 1,
            [XafDisplayName("Mutation interne")] MutationInterne = 2,
            [XafDisplayName("Fin de mission")] FinMission = 3,
            [XafDisplayName("Démission")] Demission = 4,
            [XafDisplayName("Rupture contrat")] RuptureContrat = 5,
        }

        public enum AlerteInterimaireType
        {
            [XafDisplayName("Mission expirée")] MissionExpiree = 0,
            [XafDisplayName("Intérimaire sans affectation")] SansAffectation = 1,
            [XafDisplayName("Doublon sur station")] DoublonStation = 2,
            [XafDisplayName("Sureffectif")] Sureffectif = 3,
            [XafDisplayName("Prolongation non enregistrée")] ProlongationNonEnregistree = 4,
            [XafDisplayName("Mouvement non validé")] MouvementNonValide = 5,
            [XafDisplayName("Mouvement tardif")] MouvementTardif = 6,
            [XafDisplayName("Incohérence données")] IncoherenceDonnees = 7,
        }

        public enum AlerteInterimaireNiveau
        {
            [XafDisplayName("Info")] Info = 0,
            [XafDisplayName("Alerte")] Alerte = 1,
            [XafDisplayName("Urgent")] Urgent = 2,
        }

        public enum TypeContrat
        {
            [XafDisplayName("CDI — Contrat à Durée Indéterminée")]
            CDI = 0,
            [XafDisplayName("CDD — Contrat à Durée Déterminée")]
            CDD = 1,
            [XafDisplayName("Contrat de stage")]
            Stage = 2,
        }

        public enum ContratSalarieStatut
        {
            [XafDisplayName("Brouillon")] Brouillon = 0,
            [XafDisplayName("Actif")] Actif = 1,
            [XafDisplayName("Suspendu")] Suspendu = 2,
            [XafDisplayName("Expiré")] Expire = 3,
            [XafDisplayName("Résilié")] Resilie = 4,
        }

        public enum MotifCDD
        {
            [XafDisplayName("Accroissement temporaire d'activité")] AccroissementActivite = 0,
            [XafDisplayName("Remplacement d'un salarié absent")] Remplacement = 1,
            [XafDisplayName("Emploi saisonnier")] Saisonnier = 2,
            [XafDisplayName("Autre motif")] Autre = 3,
        }
        public enum MotifDepart
        {
            [XafDisplayName("Démission")] Demission = 0,
            [XafDisplayName("Licenciement")] Licenciement = 1,
            [XafDisplayName("Retraite")] Retraite = 2,
            [XafDisplayName("Fin de CDD")] FinCDD = 3,
            [XafDisplayName("Rupture conventionnelle")] RuptureConventionnelle = 4,
            [XafDisplayName("Décès")] Deces = 5,
            [XafDisplayName("Autre")] Autre = 6,
        }

        public enum OffboardingStatut
        {
            [XafDisplayName("Initié")] Initie = 0,
            [XafDisplayName("En cours")] EnCours = 1,
            [XafDisplayName("Solde calculé")] SoldeCalcule = 2,
            [XafDisplayName("Validé RH")] ValideRH = 3,
            [XafDisplayName("Validé DAF")] ValidéDAF = 4,
            [XafDisplayName("Clôturé")] Cloture = 5,
        }

        // ── Disciplinaire ─────────────────────────────────────────

        public enum DisciplinaireStatut
        {
            [XafDisplayName("Initié")] Initie = 0,
            [XafDisplayName("Notifié au salarié")] Notifie = 1,
            [XafDisplayName("Audition programmée")] AuditionProgrammee = 2,
            [XafDisplayName("Audition réalisée")] AuditionRealisee = 3,
            [XafDisplayName("Sanction prononcée")] SanctionPrononcee = 4,
            [XafDisplayName("Clôturé")] Cloture = 5,
        }

        public enum CategorieFaute
        {
            [XafDisplayName("Faute simple")] Simple = 0,
            [XafDisplayName("Faute grave")] Grave = 1,
            [XafDisplayName("Faute lourde")] Lourde = 2,
        }

        public enum TypeSanction
        {
            [XafDisplayName("Avertissement")] Avertissement = 0,
            [XafDisplayName("Blâme")] Blame = 1,
            [XafDisplayName("Mise à pied disciplinaire")] MiseAPied = 2,
            [XafDisplayName("Rétrogradation")] Retrogradation = 3,
            [XafDisplayName("Licenciement pour faute")] LicenciementFaute = 4,
            [XafDisplayName("Licenciement pour faute lourde")] LicenciementFauteLourde = 5,
        }

    }
}
