using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// V1.7.2a - Service partagé de calcul du « Brut récurrent ».
    ///
    /// Le brut récurrent correspond à la rémunération brute mensuelle
    /// « normale » d'un salarié, hors éléments variables ou exceptionnels.
    /// Il sert de base de calcul pour le 13ième mois (V1.7.2b) et les
    /// gratifications (V1.7.2d).
    ///
    /// Règle métier validée par RH ELTON (cf. Grille V1.7.2 question 4) :
    /// « dernier salaire brut perçu avant le calcul, brut normal compte
    /// non tenu des congés et autres ».
    ///
    /// Composition INCLUSE dans le brut récurrent :
    ///   ✅ Salaire de base
    ///   ✅ Sursalaire
    ///   ✅ Prime d'ancienneté
    ///   ✅ Indemnité de logement
    ///   ✅ Prime de transport
    ///   ✅ Avantage en nature véhicule
    ///   ✅ Toute rubrique CUSTOM (sans code canonique) qui est Gain + BrutFiscal
    ///
    /// Composition EXCLUE :
    ///   ❌ Heures supplémentaires (variable, exceptionnel)
    ///   ❌ Indemnités/allocations de congés payés (CP du mois)
    ///   ❌ Gratifications antérieures
    ///   ❌ 13ième mois antérieur
    ///   ❌ Rappels de salaire
    ///   ❌ Primes ponctuelles exceptionnelles
    ///
    /// Le service est PUREMENT EN LECTURE - il ne modifie aucune entité.
    /// </summary>
    public static class BrutRecurrentService
    {
        // ─────────────────────────────────────────────────────────────
        // Liste blanche stricte des codes canoniques INCLUS dans le BR
        // ─────────────────────────────────────────────────────────────
        private static readonly HashSet<RubriqueCanonique> CodesInclus = new()
        {
            RubriqueCanonique.SalaireDeBase,
            RubriqueCanonique.Sursalaire,
            RubriqueCanonique.PrimeAnciennete,
            RubriqueCanonique.IndemniteLogement,
            RubriqueCanonique.PrimeTransport,
            RubriqueCanonique.AvantageNatureVehicule,
        };

        // ─────────────────────────────────────────────────────────────
        // Liste noire stricte des codes canoniques EXCLUS (variables /
        // exceptionnels - même s'ils sont des gains BrutFiscal)
        // ─────────────────────────────────────────────────────────────
        private static readonly HashSet<RubriqueCanonique> CodesExclus = new()
        {
            RubriqueCanonique.HeuresSupplementaires,
            // V1.7.2 - Bonus auto-exclus du brut récurrent (évite la
            // récursivité : un 13ième mois ne doit pas être inclus dans
            // la base de calcul d'un futur 13ième mois)
            RubriqueCanonique.TreiziemeMois,
            RubriqueCanonique.Gratification,
        };

        // ─────────────────────────────────────────────────────────────
        // Préfixes de codes string (Rubrique.Code) à EXCLURE même si la
        // rubrique n'a pas de RoleCanonique. Sert à filtrer les rubriques
        // custom de congés/gratification créées en cours d'exploitation.
        // ─────────────────────────────────────────────────────────────
        private static readonly string[] PrefixesCodesExclus = new[]
        {
            "CONGE",   // toutes rubriques de congés payés
            "CP_",     // alternative CP_PAIE, CP_ALLOC, etc.
            "ALLOC_CONGE",
            "INDEM_CONGE",
            "GRATIF",  // V1.7.2 - gratification (auto-exclue pour ne pas se recalculer)
            "M13",     // V1.7.2 - 13ième mois (auto-exclu)
            "PRIME_EXC",   // primes exceptionnelles éventuelles
            "RAPPEL",      // rappels de salaire
        };

        /// <summary>
        /// Calcule le brut récurrent d'un bulletin donné.
        /// Somme des montants des lignes éligibles (Gain, BrutFiscal, code OK).
        /// Retourne 0 si le bulletin est null.
        /// </summary>
        public static decimal GetBrutRecurrent(Bulletin bulletin)
        {
            if (bulletin == null) return 0m;

            return bulletin.Lignes
                .Where(EstLigneRecurrente)
                .Sum(l => l.Montant);
        }

        /// <summary>
        /// Récupère le brut récurrent du DERNIER bulletin validé d'un salarié
        /// dans l'année donnée, avant le mois de référence (exclu).
        ///
        /// Utilisation typique :
        ///   - 13ième mois décembre 2026  → GetDernierBrutRecurrent(s, 2026, 12)
        ///     retourne le BR du bulletin de novembre 2026 (ou antérieur si nov.
        ///     manquant).
        ///   - STC départ juin 2026       → GetDernierBrutRecurrent(s, 2026, 6)
        ///     retourne le BR du bulletin de mai 2026.
        ///
        /// Si aucun bulletin trouvé dans l'année, fait un fallback sur l'année
        /// précédente (cas d'embauche en décembre par exemple).
        /// </summary>
        public static decimal GetDernierBrutRecurrent(
            Session session, Salarie salarie, int annee, int moisReference)
        {
            if (session == null || salarie == null) return 0m;

            // 1. Bulletins validés du salarié dans l'année, avant moisReference
            var bulletin = session.Query<Bulletin>()
                .Where(b => b.Salarie.Oid == salarie.Oid
                            && b.Annee == annee
                            && b.Mois < moisReference
                            && (b.Statut == BulletinStatut.Valide
                                || b.Statut == BulletinStatut.Imprime
                                || b.Statut == BulletinStatut.Envoye
                                || b.Statut == BulletinStatut.Comptabilise
                                || b.Statut == BulletinStatut.Cloture))
                .OrderByDescending(b => b.Mois)
                .FirstOrDefault();

            // 2. Fallback année précédente si rien trouvé (embauche tardive)
            bulletin ??= session.Query<Bulletin>()
                .Where(b => b.Salarie.Oid == salarie.Oid
                            && b.Annee == annee - 1
                            && (b.Statut == BulletinStatut.Valide
                                || b.Statut == BulletinStatut.Imprime
                                || b.Statut == BulletinStatut.Envoye
                                || b.Statut == BulletinStatut.Comptabilise
                                || b.Statut == BulletinStatut.Cloture))
                .OrderByDescending(b => b.Mois)
                .FirstOrDefault();

            return GetBrutRecurrent(bulletin);
        }

        /// <summary>
        /// Compte les mois de présence d'un salarié dans l'année donnée :
        /// nombre de bulletins validés qu'il a sur l'année.
        ///
        /// Utilisation : prorata pour le 13ième mois.
        ///   ProrataMois = MoisPresence / 12
        ///
        /// Règle : on compte tous les bulletins (validés ou non clôturés)
        /// car même un bulletin "Brouillon" prouve que le salarié a une
        /// rémunération imputée sur ce mois.
        /// </summary>
        public static int GetMoisPresence(Session session, Salarie salarie, int annee)
        {
            if (session == null || salarie == null) return 0;

            return session.Query<Bulletin>()
                .Count(b => b.Salarie.Oid == salarie.Oid
                            && b.Annee == annee);
        }

        /// <summary>
        /// V1.7.2d - Récupère le NetAPayer du dernier bulletin validé du
        /// salarié dans l'année, avant un mois de référence.
        ///
        /// Utilisé pour la base de calcul "NetRecurrent" des Gratifications :
        /// RH/DAF saisissent un multiple du net mensuel "normal" du salarié.
        ///
        /// Approche pragmatique : on prend le NetAPayer du bulletin de
        /// référence tel quel (sans recalcul). C'est ce que RH ELTON
        /// utilise dans leur saisie quotidienne.
        /// </summary>
        public static decimal GetDernierNetAPayer(
            Session session, Salarie salarie, int annee, int moisReference)
        {
            if (session == null || salarie == null) return 0m;

            var bulletin = session.Query<Bulletin>()
                .Where(b => b.Salarie.Oid == salarie.Oid
                            && b.Annee == annee
                            && b.Mois < moisReference
                            && (b.Statut == BulletinStatut.Valide
                                || b.Statut == BulletinStatut.Imprime
                                || b.Statut == BulletinStatut.Envoye
                                || b.Statut == BulletinStatut.Comptabilise
                                || b.Statut == BulletinStatut.Cloture))
                .OrderByDescending(b => b.Mois)
                .FirstOrDefault();

            bulletin ??= session.Query<Bulletin>()
                .Where(b => b.Salarie.Oid == salarie.Oid
                            && b.Annee == annee - 1
                            && (b.Statut == BulletinStatut.Valide
                                || b.Statut == BulletinStatut.Imprime
                                || b.Statut == BulletinStatut.Envoye
                                || b.Statut == BulletinStatut.Comptabilise
                                || b.Statut == BulletinStatut.Cloture))
                .OrderByDescending(b => b.Mois)
                .FirstOrDefault();

            return bulletin?.NetAPayer ?? 0m;
        }

        /// <summary>
        /// V1.12 - Recupere le "Net reglementaire" du dernier bulletin NORMAL
        /// du salarie, avant un mois de reference.
        ///
        /// "Bulletin normal" = bulletin valide/cloture qui ne contient AUCUNE
        /// ligne dont la Rubrique est marquee EstExceptionnelle (congé, gratif,
        /// 13e, HS, régul). Garantit une base de reference stable.
        ///
        /// Formule RH ELTON :
        ///   NetReglementaire = NetAPayer + sum(retenues optionnelles)
        ///
        /// Ou retenues optionnelles = toutes les lignes Retenue qui ne sont
        /// PAS les cotisations obligatoires (IPRES RG/RC, CSS, IR, TRIMF, CFCE).
        /// Donc : prets, avances, cohabel, entretien vehicule, etc.
        ///
        /// Cette formule inclut automatiquement la Prime de transport (qui
        /// n'est ni dans BrutSocial ni dans BrutFiscal depuis V1.8.4, mais
        /// est bien payee cash dans NetAPayer).
        ///
        /// Verification Abdoulaye DIENG juin 2026 :
        ///   NetAPayer 2 864 040 + retenues (PRET_VEH 86 333 + RET_ENT_VEH 47 000
        ///   + RET_COHABEL 10 000) = 3 007 373 (= Excel RH)
        ///
        /// Fallback mode degrade : si aucun bulletin NORMAL trouve dans l'annee
        /// en cours ni la precedente, retombe sur n'importe quel bulletin valide
        /// (meme avec rubriques exceptionnelles) pour eviter 0m qui bloquerait
        /// la saisie.
        /// </summary>
        public static decimal GetDernierNetReglementaire(
            Session session, Salarie salarie, int annee, int moisReference)
        {
            if (session == null || salarie == null) return 0m;

            // Filtre statut : valide / imprime / envoye / comptabilise / cloture
            bool EstStatutValide(Bulletin b) =>
                b.Statut == BulletinStatut.Valide
                || b.Statut == BulletinStatut.Imprime
                || b.Statut == BulletinStatut.Envoye
                || b.Statut == BulletinStatut.Comptabilise
                || b.Statut == BulletinStatut.Cloture;

            // Un bulletin est "normal" s'il ne contient aucune ligne exceptionnelle.
            // Rubrique.EstExceptionnelle est marque par l'Updater pour :
            // CONGE_PAYE, ICCP, GRATIF, 13EME, HS25/50/100, REGUL_*
            bool EstBulletinNormal(Bulletin b) =>
                b.Lignes.All(l => l.Rubrique == null || !l.Rubrique.EstExceptionnelle);

            // 1. Priorite : dernier bulletin NORMAL dans l'annee en cours
            var bulletinsAnnee = session.Query<Bulletin>()
                .Where(b => b.Salarie.Oid == salarie.Oid
                            && b.Annee == annee
                            && b.Mois < moisReference)
                .OrderByDescending(b => b.Mois)
                .ToList();
            var bulletin = bulletinsAnnee.FirstOrDefault(
                b => EstStatutValide(b) && EstBulletinNormal(b));

            // 2. Fallback : dernier bulletin NORMAL de l'annee precedente
            if (bulletin == null)
            {
                var bulletinsAnneeAvant = session.Query<Bulletin>()
                    .Where(b => b.Salarie.Oid == salarie.Oid
                                && b.Annee == annee - 1)
                    .OrderByDescending(b => b.Mois)
                    .ToList();
                bulletin = bulletinsAnneeAvant.FirstOrDefault(
                    b => EstStatutValide(b) && EstBulletinNormal(b));
            }

            // 3. Mode degrade : n'importe quel bulletin valide
            if (bulletin == null)
            {
                bulletin = bulletinsAnnee.FirstOrDefault(EstStatutValide)
                       ?? session.Query<Bulletin>()
                            .Where(b => b.Salarie.Oid == salarie.Oid
                                        && b.Annee == annee - 1
                                        && (b.Statut == BulletinStatut.Valide
                                            || b.Statut == BulletinStatut.Imprime
                                            || b.Statut == BulletinStatut.Envoye
                                            || b.Statut == BulletinStatut.Comptabilise
                                            || b.Statut == BulletinStatut.Cloture))
                            .OrderByDescending(b => b.Mois)
                            .FirstOrDefault();
            }

            if (bulletin == null) return 0m;

            // NetReglementaire = NetAPayer + retenues optionnelles
            // Les retenues optionnelles = retenues qui ne sont pas obligatoires.
            // Obligatoires = canoniques IPRES_RG, IPRES_RC, CSS_*, IRPP, TRIMF, CFCE
            decimal retenuesOptionnelles = bulletin.Lignes
                .Where(l => l.Rubrique != null
                         && l.Rubrique.TypeCalcul == RubriqueTypeCalcul.Retenue
                         && !EstRetenueObligatoire(l.Rubrique))
                .Sum(l => l.Montant);

            return bulletin.NetAPayer + retenuesOptionnelles;
        }

        /// <summary>
        /// V1.12 - Determine si une rubrique est une retenue obligatoire
        /// (cotisation sociale, IR, TRIMF, CFCE). A exclure du calcul des
        /// retenues optionnelles pour le Net Reglementaire.
        /// </summary>
        private static bool EstRetenueObligatoire(Rubrique rub)
        {
            if (rub == null) return false;
            var c = rub.Canonique;
            return c == RubriqueCanonique.IPRES_RG
                || c == RubriqueCanonique.IPRES_RC
                || c == RubriqueCanonique.CSS_AccidentTravail
                || c == RubriqueCanonique.CSS_AllocationFamiliale
                || c == RubriqueCanonique.IRPP
                || c == RubriqueCanonique.TRIMF
                || c == RubriqueCanonique.CFCE;
        }

        /// <summary>
        /// Cumul du brut récurrent versé à un salarié sur l'année (utile
        /// pour reporting et provision moyenne).
        /// </summary>
        public static decimal GetCumulBrutRecurrent(
            Session session, Salarie salarie, int annee)
        {
            if (session == null || salarie == null) return 0m;

            return session.Query<Bulletin>()
                .Where(b => b.Salarie.Oid == salarie.Oid && b.Annee == annee)
                .ToList()
                .Sum(GetBrutRecurrent);
        }

        // ─────────────────────────────────────────────────────────────
        // PRIVÉ - Critère d'éligibilité d'une ligne au brut récurrent
        // ─────────────────────────────────────────────────────────────
        private static bool EstLigneRecurrente(BulletinLigne ligne)
        {
            if (ligne == null || ligne.Rubrique == null) return false;
            var r = ligne.Rubrique;

            // 1. Doit être un GAIN (pas une retenue)
            if (r.TypeCalcul != RubriqueTypeCalcul.Gain) return false;

            // 2. Doit être BrutFiscal (= entre dans le brut imposable)
            if (!r.BrutFiscal) return false;

            // 3. Cas code canonique connu : whitelist/blacklist stricte
            var code = ligne.RoleCanonique;
            if (code.HasValue)
            {
                if (CodesExclus.Contains(code.Value)) return false;
                if (CodesInclus.Contains(code.Value)) return true;
                // Code canonique connu mais pas dans la whitelist
                // (ex. cotisations, retenues, IR...) → on exclut.
                return false;
            }

            // 4. Cas rubrique custom (sans code canonique) :
            //    exclure par préfixe de code (CONGE_*, CP_*, GRATIF_*, M13_*, etc.)
            var rubriqueCode = r.Code ?? string.Empty;
            foreach (var prefix in PrefixesCodesExclus)
            {
                if (rubriqueCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // 5. Tout autre Gain + BrutFiscal sans préfixe d'exclusion :
            //    on inclut. (Ex. indemnités custom récurrentes paramétrées
            //    par l'admin paie.)
            return true;
        }
    }
}
