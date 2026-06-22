// =============================================================================
//  BulletinCongeService.cs — V1.8 (juin 2026)
//
//  Service métier pour la gestion des allocations et indemnités de congés
//  dans le bulletin de paie.
//
//  Trois cas couverts :
//    1. SALARIÉ EN CONGÉ  →  rubrique "CONGE_PAYE" (code 23, libellé "Congés")
//       Formule : (Σ Brut imposable 12 mois / 12) × (JoursAcquis + Bonus) / 24
//       Conforme CCT Sénégal Art. 57 + fichier ELTON Congés.xlsx validé RH.
//
//    2. RACHAT DE CONGÉ (en cours de carrière, sans départ physique)
//       →  rubrique "ICCP" — "Indemnité de congés compensatrice"
//       Même formule que cas 1, ligne ajoutée au bulletin mensuel normal.
//
//    3. ICCP DÉPART (rupture/retraite) → géré directement dans
//       DossierOffboarding.CalculerSoldeToutCompte() — pas dans ce service.
//
//  Mode de bulletin :
//    - BulletinUnique (défaut) : ligne ajoutée au bulletin mensuel existant
//      Le RH ajustera manuellement les rubriques de salaire si besoin
//      (cas du salarié totalement absent ce mois-là).
//
//    - BulletinSepare : crée un bulletin distinct pour le mois du congé.
//      Option future, choix configuré dans ParametresPaie.ModeBulletinConges.
//
//  Modes de calcul du montant :
//    - AUTO : calcul à partir de l'historique des bulletins (≥ 12 mois)
//    - MANUEL : montant saisi par le RH (utilisé pour rétroactif sans historique)
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class BulletinCongeService
    {
        // ─────────────────────────────────────────────────────────────
        //  CONSTANTES — codes rubrique
        // ─────────────────────────────────────────────────────────────
        public const string CODE_RUBRIQUE_CONGE_PAYE = "CONGE_PAYE";
        public const string CODE_RUBRIQUE_ICCP       = "ICCP";

        /// <summary>
        /// Nombre de jours de congés concernés (CCT Art. 57) = 2 j × 12 mois.
        /// Sert de diviseur dans la formule de l'allocation de congé.
        /// </summary>
        private const decimal JoursCongesAnnuels = 24m;

        // ═════════════════════════════════════════════════════════════════
        //  1. CALCUL DE L'ALLOCATION DE CONGÉ
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Calcule l'allocation de congé à partir de l'historique des bulletins
        /// du salarié sur les 12 mois précédant la période de référence.
        ///
        /// Formule CCT Sénégal Art. 57 :
        ///   AllocationNormale = (Σ Brut imposable 12 mois) / 12
        ///   Supplément        = AllocationNormale × (Bonus / 24)
        ///   AllocationTotale  = AllocationNormale + Supplément
        ///                     = (Σ brut 12 mois / 12) × (JoursDus / 24)
        ///
        /// JoursDus = jours pris (typiquement 24 + bonus ancienneté + bonus enfants)
        ///
        /// Si l'historique est < 12 mois, on divise quand même par 12
        /// (réponse RH validée juin 2026 : l'indemnité est proportionnelle
        /// aux droits acquis, eux-mêmes proportionnels à la durée travaillée).
        /// </summary>
        /// <param name="os">ObjectSpace pour lire les bulletins.</param>
        /// <param name="salarie">Salarié concerné.</param>
        /// <param name="joursDus">Nombre de jours dûs (24 + bonus).</param>
        /// <param name="annee">Année de la période de référence (= année du congé).</param>
        /// <param name="mois">Mois de la période de référence (= mois du congé).</param>
        /// <returns>Montant de l'allocation arrondi au franc, et infos de calcul.</returns>
        public static CalculAllocationResult CalculerAllocationAuto(
            IObjectSpace os,
            Salarie salarie,
            decimal joursDus,
            int annee,
            int mois)
        {
            if (salarie == null || joursDus <= 0)
                return CalculAllocationResult.Vide;

            // ── Période de référence : 12 mois rolling AVANT le mois courant
            //    Ex : congé en mai 2026 → période juin 2025 → avril 2026 inclus
            var moisCourant = new DateTime(annee, mois, 1);
            var moisDebut   = moisCourant.AddMonths(-12);
            var moisFinExcl = moisCourant; // exclusif

            int debutCle = moisDebut.Year * 100 + moisDebut.Month;
            int finCle   = moisFinExcl.Year * 100 + moisFinExcl.Month;

            // Cumul du brut imposable sur la période
            var bulletins = new XPQuery<Bulletin>(((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                .Where(b => b.Salarie != null && b.Salarie.Oid == salarie.Oid)
                .ToList()
                .Where(b =>
                {
                    int cle = b.Annee * 100 + b.Mois;
                    return cle >= debutCle && cle < finCle;
                })
                .ToList();

            decimal cumulBrut = 0m;
            int nbMoisTrouves = bulletins.Count;
            foreach (var b in bulletins)
            {
                foreach (var l in b.Lignes)
                {
                    bool estBrutFiscal = l.Rubrique?.BrutFiscal ?? false;
                    if (estBrutFiscal && l.TypeCalcul == RubriqueTypeCalcul.Gain)
                        cumulBrut += l.Montant;
                }
            }

            // V1.8 : on divise toujours par 12, conforme réponse RH validée.
            decimal brutMensuelMoyen = Math.Round(cumulBrut / 12m, 0);
            decimal montant = Math.Round(brutMensuelMoyen * joursDus / JoursCongesAnnuels, 0);

            return new CalculAllocationResult
            {
                Succes              = true,
                JoursDus            = joursDus,
                CumulBrut12Mois     = cumulBrut,
                BrutMensuelMoyen    = brutMensuelMoyen,
                NbMoisHistoriqueTrouves = nbMoisTrouves,
                Montant             = montant,
                FormuleUtilisee     = $"({cumulBrut:N0} / 12) × ({joursDus:N1} / 24) = {montant:N0}",
                HistoriqueComplet   = nbMoisTrouves >= 12
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  2. GÉNÉRATION DE LA LIGNE BULLETIN — CONGÉ PAYÉ
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Ajoute une ligne "Indemnité congés payés" (rubrique CONGE_PAYE)
        /// au bulletin du mois indiqué (créé si absent en mode BulletinUnique,
        /// dédoublé en mode BulletinSepare).
        ///
        /// Cas d'usage : le salarié part en congé.
        ///
        /// IMPORTANT : en mode BulletinUnique, le service n'enlève PAS les
        /// autres rubriques (salaire de base, etc.). Le RH est responsable
        /// d'ajuster manuellement le bulletin si le salarié est en congé tout
        /// le mois (= remplacer rubriques de salaire par la seule rubrique
        /// Congé). Le moteur de paie de juin 2026 ne fait pas encore cette
        /// substitution automatique.
        /// </summary>
        public static BulletinCongeGenerationResult CreerLigneAllocationConge(
            IObjectSpace os,
            Salarie salarie,
            int annee,
            int mois,
            decimal joursDus,
            decimal montant,
            string motif,
            ModeBulletinConges mode = ModeBulletinConges.BulletinUnique,
            string sourceCalcul = null)
        {
            return CreerLigneInterne(os, salarie, annee, mois,
                joursDus, montant, motif,
                CODE_RUBRIQUE_CONGE_PAYE, mode, sourceCalcul,
                anneeOrigineSolde: null);
        }

        // ═════════════════════════════════════════════════════════════════
        //  3. GÉNÉRATION DE LA LIGNE BULLETIN — RACHAT ICCP (cours de carrière)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Ajoute une ligne "Indemnité de congés compensatrice" (rubrique ICCP)
        /// au bulletin mensuel normal. Le salarié continue de travailler mais
        /// touche la compensation monétaire des jours non pris.
        ///
        /// Distinction sémantique vs CONGE_PAYE :
        ///   - CONGE_PAYE = salarié part physiquement en congé
        ///   - ICCP       = compensation monétaire (cours de carrière OU STC)
        ///
        /// Cas typique : le RH décide de payer les jours non pris d'une année
        /// antérieure (anneeOrigineSolde) sur le bulletin courant.
        /// </summary>
        public static BulletinCongeGenerationResult CreerLigneRachatICCP(
            IObjectSpace os,
            Salarie salarie,
            int annee,
            int mois,
            decimal joursRachetes,
            decimal montant,
            string motif,
            int? anneeOrigineSolde = null,
            string sourceCalcul = null)
        {
            // Le rachat est TOUJOURS sur le bulletin mensuel normal,
            // jamais en bulletin séparé.
            return CreerLigneInterne(os, salarie, annee, mois,
                joursRachetes, montant, motif,
                CODE_RUBRIQUE_ICCP, ModeBulletinConges.BulletinUnique,
                sourceCalcul, anneeOrigineSolde);
        }

        // ═════════════════════════════════════════════════════════════════
        //  4. BASCULE BULLETIN MENSUEL → BULLETIN DE CONGÉ
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// V1.8 — Codes des rubriques de salaire normal à supprimer quand on
        /// bascule un bulletin mensuel en bulletin de congé. Inclut :
        ///   - Salaire de base, Sursalaire, Ancienneté
        ///   - 13ème mois, Gratification
        ///   - Indemnités diverses (logement, transport, générique)
        ///   - Primes génériques, Heures supplémentaires
        /// NE contient PAS : avantages en nature (AV_NAT_VEH, AV_TEL),
        /// cotisations (IPRES, CSS, TRIMF, IR, CFCE), retenues (PRET,
        /// AVANCE_SAL), ni les rubriques de congé elles-mêmes
        /// (CONGE_PAYE, ICCP). Ces dernières sont conservées et les
        /// cotisations seront recalculées automatiquement sur la nouvelle base.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string>
            CodesRubriquesSalaireMensuel = new System.Collections.Generic.HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                "SB", "SURSAL", "ANC", "13EME", "GRATIF",
                "LOGT", "TRANS", "INDEM_GEN_IMP", "INDEM_GEN_NON_IMP", "PRIME_GEN",
                "HS25", "HS50", "HS100"
            };

        /// <summary>
        /// Bascule un bulletin mensuel normal en bulletin de congé :
        /// supprime les lignes correspondant aux rubriques de salaire
        /// normal (cf. <see cref="CodesRubriquesSalaireMensuel"/>) tout
        /// en conservant les avantages en nature et les cotisations.
        ///
        /// Cas d'usage : le salarié est en congé tout le mois et son
        /// bulletin ne doit contenir que l'indemnité de congé en
        /// remplacement du salaire (pas en plus).
        /// </summary>
        /// <returns>Nombre de lignes supprimées.</returns>
        public static int NettoyerRubriquesSalaireMensuel(
            IObjectSpace os, Bulletin bulletin)
        {
            if (os == null || bulletin == null) return 0;

            var lignesASupprimer = bulletin.Lignes
                .Where(l => l.Rubrique != null
                         && CodesRubriquesSalaireMensuel.Contains(l.Rubrique.Code))
                .ToList();

            int nb = 0;
            foreach (var l in lignesASupprimer)
            {
                os.Delete(l);
                nb++;
            }
            return nb;
        }

        // ═════════════════════════════════════════════════════════════════
        //  5. MÉTHODE INTERNE DE CRÉATION DE LIGNE
        // ═════════════════════════════════════════════════════════════════

        private static BulletinCongeGenerationResult CreerLigneInterne(
            IObjectSpace os,
            Salarie salarie,
            int annee,
            int mois,
            decimal jours,
            decimal montant,
            string motif,
            string codeRubrique,
            ModeBulletinConges mode,
            string sourceCalcul,
            int? anneeOrigineSolde)
        {
            if (salarie == null)
                return BulletinCongeGenerationResult.Erreur("Salarié non renseigné.");
            if (jours <= 0 || montant <= 0)
                return BulletinCongeGenerationResult.Erreur("Jours et montant doivent être > 0.");

            // 1. Trouver la rubrique
            var rubrique = os.GetObjectsQuery<Rubrique>()
                .FirstOrDefault(r => r.Code == codeRubrique && r.Actif);
            if (rubrique == null)
                return BulletinCongeGenerationResult.Erreur(
                    $"Rubrique {codeRubrique} introuvable. " +
                    "Veuillez relancer le seed du référentiel paie depuis ParametresPaie.");

            // 2. Trouver ou créer le Bulletin destinataire
            Bulletin bulletin;
            if (mode == ModeBulletinConges.BulletinSepare && codeRubrique == CODE_RUBRIQUE_CONGE_PAYE)
            {
                // Bulletin séparé pour le congé — toujours un nouveau bulletin distinct
                bulletin = os.CreateObject<Bulletin>();
                bulletin.Salarie = salarie;
                bulletin.Annee = annee;
                bulletin.Mois = mois;
            }
            else
            {
                // Bulletin mensuel normal — récupérer ou créer
                bulletin = os.GetObjectsQuery<Bulletin>()
                    .FirstOrDefault(b => b.Salarie.Oid == salarie.Oid
                                      && b.Annee == annee
                                      && b.Mois == mois);

                if (bulletin == null)
                {
                    bulletin = os.CreateObject<Bulletin>();
                    bulletin.Salarie = salarie;
                    bulletin.Annee = annee;
                    bulletin.Mois = mois;
                }
            }

            // 3. V1.8 — Unicité (Bulletin, Rubrique)
            //    Une même rubrique ne peut apparaître qu'une seule fois par
            //    bulletin (contrainte XPO RuleCombinationOfPropertiesIsUnique
            //    sur BulletinLigne). On anticipe ici pour donner un message
            //    plus actionnable avant le save plutôt qu'une erreur XAF brute.
            var ligneExistante = bulletin.Lignes.FirstOrDefault(l =>
                l.Rubrique?.Code == codeRubrique);
            if (ligneExistante != null)
            {
                return BulletinCongeGenerationResult.Erreur(
                    $"Une ligne « {rubrique.Libelle} » ({codeRubrique}) existe " +
                    $"déjà sur le bulletin {mois:D2}/{annee} de {salarie.FullName} " +
                    $"(montant actuel : {ligneExistante.Montant:N0} FCFA). " +
                    $"\n\nPour la remplacer, allez dans le bulletin, supprimez la " +
                    $"ligne existante, puis relancez l'action « Saisir un congé »." +
                    $"\nPour modifier juste le montant, éditez directement la " +
                    $"ligne dans le bulletin.");
            }

            // 4. Créer la ligne de bulletin
            var ligne = os.CreateObject<BulletinLigne>();
            ligne.Bulletin = bulletin;
            ligne.Rubrique = rubrique;
            ligne.Base     = jours;        // nb de jours
            ligne.Montant  = montant;      // FCFA
            ligne.Source   = RubriqueSource.SaisieManuelle;

            // 5. Référence pour traçabilité + idempotence
            string refTrace = $"{codeRubrique} {jours:N1}j × {(montant / jours):N0} FCFA/j";
            if (anneeOrigineSolde.HasValue)
                refTrace += $" [solde {anneeOrigineSolde.Value}]";
            if (!string.IsNullOrWhiteSpace(motif))
                refTrace += $" — {motif}";
            ligne.Reference = refTrace;

            // 6. Commentaire de calcul (pour audit DAF)
            if (!string.IsNullOrWhiteSpace(sourceCalcul))
            {
                // Si BulletinLigne a un champ Commentaire/Description on l'utilise.
                // Sinon ça reste dans Reference (suffit pour audit).
            }

            return BulletinCongeGenerationResult.Ok(bulletin, ligne, codeRubrique, montant);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  RÉSULTATS
    // ═════════════════════════════════════════════════════════════════════

    public class CalculAllocationResult
    {
        public bool Succes { get; set; }
        public decimal JoursDus { get; set; }
        public decimal CumulBrut12Mois { get; set; }
        public decimal BrutMensuelMoyen { get; set; }
        public int NbMoisHistoriqueTrouves { get; set; }
        public decimal Montant { get; set; }
        public string FormuleUtilisee { get; set; }
        public bool HistoriqueComplet { get; set; }

        public static CalculAllocationResult Vide => new CalculAllocationResult { Succes = false };
    }

    public class BulletinCongeGenerationResult
    {
        public bool Succes { get; private set; }
        public string Message { get; private set; }
        public Bulletin BulletinCible { get; private set; }
        public BulletinLigne LigneCreee { get; private set; }
        public decimal MontantCree { get; private set; }
        public string CodeRubrique { get; private set; }

        public static BulletinCongeGenerationResult Ok(Bulletin bulletin, BulletinLigne ligne,
                                              string code, decimal montant)
            => new BulletinCongeGenerationResult
            {
                Succes = true,
                BulletinCible = bulletin,
                LigneCreee = ligne,
                CodeRubrique = code,
                MontantCree = montant,
                Message = $"Ligne {code} ajoutée : {montant:N0} FCFA"
            };

        public static BulletinCongeGenerationResult Erreur(string message)
            => new BulletinCongeGenerationResult { Succes = false, Message = message };

        public static BulletinCongeGenerationResult Ignore(string message)
            => new BulletinCongeGenerationResult { Succes = false, Message = message };
    }
}
