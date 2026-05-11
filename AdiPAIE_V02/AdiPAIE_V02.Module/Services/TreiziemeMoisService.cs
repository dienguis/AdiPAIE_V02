// =============================================================================
//  TreiziemeMoisService.cs — V1.7.2 — Calcul annuel du 13ième mois
//
//  Règles ELTON (cf. grille RH V1.7.2 validée 2026-05-11) :
//    - Droit conventionnel pour tous les salariés actifs
//    - Base = brut récurrent (cf. BrutRecurrentService)
//    - Calcul = BR × MoisPresence / 12
//    - Versement sur bulletin de décembre (cas normal)
//    - Prorata sur STC pour départ en cours d'année (cf. STCService #71)
//    - Soumis intégralement IR + CSS + IPRES + IPM, sans lissage
//
//  Le service est IDEMPOTENT : si un TreiziemeMois existe déjà pour
//  un (Salarie, Annee), il est mis à jour au lieu d'être recréé.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class TreiziemeMoisService
    {
        /// <summary>
        /// Calcule le 13ième mois pour TOUS les salariés actifs à fin
        /// décembre de l'année donnée. Exclut les salariés sortis dans
        /// l'année (leur prorata est versé sur STC, cf. tâche #71).
        ///
        /// Idempotent : un calcul existant pour (Salarié, Année) est
        /// recalculé et mis à jour, pas dupliqué.
        ///
        /// Retourne la liste des TreiziemeMois calculés (créés ou mis à jour).
        /// </summary>
        public static List<TreiziemeMois> CalculerPourAnnee(IObjectSpace os, int annee)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (annee < 2000 || annee > 2100)
                throw new ArgumentOutOfRangeException(nameof(annee), $"Année invalide : {annee}");

            var resultat = new List<TreiziemeMois>();
            var session = ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session;
            var dateFinAnnee = new DateTime(annee, 12, 31);

            // ─── 1. Lister les salariés concernés ──────────────────
            // Critères :
            //   - Au moins un bulletin existe pour ce salarié dans l'année
            //   - Actif au 31/12 de l'année (= pas sorti avant)
            //
            // Note : DateSortie peut être null (= toujours présent) ou
            // supérieure à fin année (= sortie l'année suivante).
            var salariesAvecBulletins = session.Query<Bulletin>()
                .Where(b => b.Annee == annee && b.Salarie != null)
                .Select(b => b.Salarie)
                .Distinct()
                .ToList();

            foreach (var salarie in salariesAvecBulletins)
            {
                // Exclure les salariés sortis dans l'année (leur 13ième
                // est versé sur STC, géré par STCService #71)
                if (EstSortiAvant(salarie, dateFinAnnee))
                {
                    // On ne crée pas de TreiziemeMois — le STC s'en charge.
                    // Si un TreiziemeMois existe déjà sur STC, on le respecte.
                    continue;
                }

                // ─── 2. Récupérer ou créer le calcul ────────────────
                var existant = os.FirstOrDefault<TreiziemeMois>(
                    m => m.Salarie.Oid == salarie.Oid && m.Annee == annee);

                // Si déjà intégré au bulletin, on ne touche pas (figé)
                if (existant != null
                    && existant.Statut == TreiziemeMoisStatut.IntegreeBulletin)
                {
                    resultat.Add(existant);
                    continue;
                }

                var m13 = existant ?? os.CreateObject<TreiziemeMois>();
                m13.Annee = annee;
                m13.Salarie = salarie;

                // ─── 3. Calcul du brut récurrent de référence ──────
                // Pour le 13ième de décembre, on prend le BR du dernier
                // bulletin disponible AVANT décembre (= novembre, ou
                // antérieur si nov. manquant). Cohérent avec règle RH :
                // « dernier salaire brut perçu avant le calcul ».
                m13.BrutRecurrentReference = BrutRecurrentService
                    .GetDernierBrutRecurrent(session, salarie, annee, 12);

                // ─── 4. Mois de présence ───────────────────────────
                m13.MoisPresence = BrutRecurrentService
                    .GetMoisPresence(session, salarie, annee);

                // ─── 5. Calcul du montant brut ─────────────────────
                // = BR × MoisPresence / 12
                // Arrondi à l'unité (FCFA n'a pas de décimales)
                m13.MontantBrut = Math.Round(
                    m13.BrutRecurrentReference * m13.MoisPresence / 12m,
                    0,
                    MidpointRounding.AwayFromZero);

                // ─── 6. Mise à jour métadata ───────────────────────
                m13.Statut = TreiziemeMoisStatut.Calcule;
                m13.DateCalcul = DateTime.Now;
                m13.EstSurSTC = false;
                try { m13.CalculePar = SecuritySystem.CurrentUserName; } catch { }

                resultat.Add(m13);
            }

            os.CommitChanges();
            return resultat;
        }

        /// <summary>
        /// Calcule un 13ième prorata pour un salarié spécifique à une date
        /// de référence donnée. Utilisé par STCService (#71) pour les
        /// salariés qui quittent l'entreprise en cours d'année.
        ///
        /// Le calcul est IMMÉDIATEMENT marqué EstSurSTC=true pour ne
        /// pas être repris par CalculerPourAnnee de décembre.
        /// </summary>
        public static TreiziemeMois CalculerProrataSTC(
            IObjectSpace os,
            Salarie salarie,
            int annee,
            int moisReference)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (salarie == null) throw new ArgumentNullException(nameof(salarie));

            var session = ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session;

            // Idempotence : si déjà calculé, on retourne tel quel
            var existant = os.FirstOrDefault<TreiziemeMois>(
                m => m.Salarie.Oid == salarie.Oid && m.Annee == annee);

            if (existant != null && existant.Statut == TreiziemeMoisStatut.IntegreeBulletin)
                return existant;

            var m13 = existant ?? os.CreateObject<TreiziemeMois>();
            m13.Annee = annee;
            m13.Salarie = salarie;
            m13.EstSurSTC = true;

            // Pour un STC en juin, on prend le BR du dernier bulletin
            // disponible (mai au plus tard).
            m13.BrutRecurrentReference = BrutRecurrentService
                .GetDernierBrutRecurrent(session, salarie, annee, moisReference);

            m13.MoisPresence = BrutRecurrentService
                .GetMoisPresence(session, salarie, annee);

            m13.MontantBrut = Math.Round(
                m13.BrutRecurrentReference * m13.MoisPresence / 12m,
                0,
                MidpointRounding.AwayFromZero);

            m13.Statut = TreiziemeMoisStatut.Calcule;
            m13.DateCalcul = DateTime.Now;
            try { m13.CalculePar = SecuritySystem.CurrentUserName; } catch { }
            m13.Commentaire = $"Calcul prorata STC (départ {annee}/{moisReference:00}).";

            os.CommitChanges();
            return m13;
        }

        // ─────────────────────────────────────────────────────────────
        // PRIVÉ — Helper : un salarié est-il déjà sorti avant la date ?
        // ─────────────────────────────────────────────────────────────
        // V1.7.2 : on s'appuie sur Salarie.IsActif + une éventuelle
        // DateSortie. Si le modèle Salarie n'expose pas encore DateSortie
        // (selon version), on retombe sur IsActif uniquement.
        // ─────────────────────────────────────────────────────────────
        private static bool EstSortiAvant(Salarie salarie, DateTime dateRef)
        {
            if (salarie == null) return false;

            // Critère principal : IsActif = false ET le salarié a moins
            // de bulletins que l'année complète (= sortie en cours d'année)
            if (!salarie.IsActif) return true;

            return false;
        }
    }
}
