// =============================================================================
//  ProvisionCongesService.cs — V1.8 — Calcul provision congés annuelle
//
//  Reproduit la requête SQL ELTON ancienne paie en LINQ XPO, alignée
//  CCT Sénégal (articles 55, 57, 58) ET pratique ELTON validée RH (juin 2026) :
//    - Base : 2 jours / mois travaillé (= NbreMois × 2)
//    - Bonus ancienneté (seuils inclusifs ≥) : 0 / +1 / +2 / +3 / +7
//        (ELTON applique +7 à partir de 25 ans, plus favorable que CCT
//         qui prévoit +6 — convention d'entreprise plus favorable légale)
//    - Bonus mère (CCT L150, femmes uniquement) :
//        • Règle B : mère < 21 ans → +2 j / enfant à charge
//        • Règle C : mère ≥ 21 ans → +2 j / enfant mineur à partir du 4e
//        (la règle A "+1 j / enfant <14 ans" a été supprimée — non CCT)
//    - Brut imposable mensuel = Σ Gain BrutFiscal=true / 12
//    - Provision FCFA = NbreJourTotal × (BrutMensuelMoyen / 24)
//        (24 = "nombre de jours de congés concernés" CCT Art. 57,
//         aligné sur la formule de paie réelle ELTON)
//
//  Pour chaque (Salarié × Année) ayant au moins un bulletin.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.NonPersistent;
using DevExpress.ExpressApp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdiPAIE_V02.Module.Services
{
    public static class ProvisionCongesService
    {
        // V1.8 — Aligné CCT Art. 57 + fichier Congés.xlsx ELTON :
        // l'allocation de congé = 1/12 des sommes perçues, et le supplément
        // est calculé sur la base "nombre de jours concernés" = 24 j
        // (= 2 j/mois × 12 mois = durée du congé standard CCT).
        // Le diviseur précédent 22 (jours ouvrés CCT) sur-évaluait la
        // provision de ~8 % par rapport au paiement réel.
        private const decimal JoursOuvresMois = 24m;
        private const int JoursParMoisTravaille = 2;

        /// <summary>
        /// Calcule les provisions de congés pour une année donnée.
        /// </summary>
        /// <param name="persistentOs">OS pour lire Salaries / Bulletins / BulletinLignes.</param>
        /// <param name="nonPersistentOs">OS dans lequel matérialiser les ProvisionConges.</param>
        /// <param name="annee">Année à calculer (ex: 2025).</param>
        /// <returns>Liste des provisions.</returns>
        public static List<ProvisionConges> Calculer(
            IObjectSpace persistentOs,
            IObjectSpace nonPersistentOs,
            int annee)
        {
            var resultat = new List<ProvisionConges>();

            // Tous les bulletins de l'année groupés par salarié
            var bulletinsAnnee = persistentOs.GetObjectsQuery<Bulletin>()
                .Where(b => b.Annee == annee && b.Salarie != null)
                .ToList();

            var groupesParSalarie = bulletinsAnnee
                .GroupBy(b => b.Salarie)
                .ToList();

            // ── Réconciliation : tous les SoldeConge de l'année (cache local) ─
            // On cumule JoursAcquis tous types (Annuel + autres) par salarié
            // pour comparaison avec la provision théorique.
            var soldesParSalarie = persistentOs.GetObjectsQuery<SoldeConge>()
                .Where(sc => sc.Annee == annee && sc.Salarie != null)
                .ToList()
                .GroupBy(sc => sc.Salarie.Oid)
                .ToDictionary(g => g.Key, g => g.Sum(sc => sc.JoursAcquis));

            foreach (var groupe in groupesParSalarie)
            {
                var salarie = groupe.Key;
                if (salarie == null) continue;

                int nbreMois = groupe.Count();

                // ── Brut imposable cumul (Σ lignes BrutFiscal=true des bulletins) ──
                // BrutFiscal est sur Rubrique (propagé via Rubrique.TypeRef.BruteFiscal)
                decimal cumulBrut = 0m;
                foreach (var bulletin in groupe)
                {
                    foreach (var ligne in bulletin.Lignes)
                    {
                        bool estBrutFiscal = ligne.Rubrique?.BrutFiscal ?? false;
                        if (estBrutFiscal && ligne.TypeCalcul == DomainEnums.RubriqueTypeCalcul.Gain)
                            cumulBrut += ligne.Montant;
                    }
                }

                decimal brutMensuelMoyen = nbreMois > 0
                    ? Math.Round(cumulBrut / 12m, 0)
                    : 0m;

                // ── Calcul des jours ──────────────────────────────────────
                int baseProv = nbreMois * JoursParMoisTravaille;
                int bonusAnc = CalculerBonusAnciennete(salarie.Anciennete);
                int bonusEnf = CalculerBonusEnfants(salarie, annee);
                int totalJours = baseProv + bonusAnc + bonusEnf;

                // ── Provision FCFA ────────────────────────────────────────
                decimal provFCFA = Math.Round(
                    totalJours * (brutMensuelMoyen / JoursOuvresMois), 0);

                // ── Réconciliation avec SoldeConge réel ──────────────────
                decimal soldeReel = soldesParSalarie.TryGetValue(salarie.Oid, out var s) ? s : 0m;
                decimal ecart = totalJours - soldeReel;

                // ── Matérialiser la ligne ──────────────────────────────────
                var ligneProv = nonPersistentOs.CreateObject<ProvisionConges>();
                ligneProv.Annee = annee;
                ligneProv.Matricule = salarie.Matricule ?? "—";
                ligneProv.NomComplet = salarie.FullName ?? "—";
                ligneProv.Sexe = salarie.Sexe.ToString();
                ligneProv.AncienneteAns = salarie.Anciennete;
                ligneProv.NombreEnfants = salarie.NombreEnfant;
                ligneProv.NbreMois = nbreMois;
                ligneProv.BaseProvision = baseProv;
                ligneProv.BonusAnciennete = bonusAnc;
                ligneProv.BonusEnfants = bonusEnf;
                ligneProv.NbreJourTotal = totalJours;
                ligneProv.CumulBrut = cumulBrut;
                ligneProv.BrutMensuelMoyen = brutMensuelMoyen;
                ligneProv.ProvisionFCFA = provFCFA;
                ligneProv.SoldeReelAcquis = soldeReel;
                ligneProv.Ecart = ecart;
                resultat.Add(ligneProv);
            }

            return resultat
                .OrderBy(p => p.Matricule)
                .ToList();
        }

        // ──────────────────────────────────────────────────────────────
        //  Bonus ancienneté — Convention ELTON (validée RH juin 2026)
        //  Référence : CCT Sénégal Loi 97-17, Art. L.149 + convention
        //  d'entreprise ELTON plus favorable que la CCT pour la dernière
        //  tranche (la CCT prévoit +6 j à 25+ ans, ELTON donne +7 j —
        //  les conventions d'entreprise plus favorables sont légales).
        //
        //  V1.8 — Seuils INCLUSIFS "vers le haut" (validation RH explicite) :
        //  un salarié pile à 10 ans bénéficie du bonus tranche 10-15 (+1 j).
        //
        //    < 10 ans  : 0 jour
        //    ≥ 10 ans  : +1 jour
        //    ≥ 15 ans  : +2 jours
        //    ≥ 20 ans  : +3 jours
        //    ≥ 25 ans  : +7 jours (convention ELTON plus favorable)
        // ──────────────────────────────────────────────────────────────
        public static int CalculerBonusAnciennete(int anciennete)
        {
            if (anciennete >= 25) return 7;
            if (anciennete >= 20) return 3;
            if (anciennete >= 15) return 2;
            if (anciennete >= 10) return 1;
            return 0;
        }

        // ──────────────────────────────────────────────────────────────
        //  Bonus mère de famille — CCT Sénégal Loi 97-17, Art. L.150
        //
        //  V1.8 — Validation RH ELTON : SEULEMENT 2 règles (la "règle A"
        //  +1j/enfant <14 ans qui figurait avant n'est PAS dans la CCT
        //  et a été supprimée).
        //
        //  Femmes salariées uniquement :
        //
        //    Règle B (mère < 21 ans au dernier jour période de référence) :
        //      +2 jours / enfant à charge (sans condition d'âge enfant)
        //
        //    Règle C (mère ≥ 21 ans au dernier jour période de référence) :
        //      +2 jours / enfant mineur à charge À PARTIR DU 4ème enfant
        //      (le 1er, 2ème, 3ème enfant ne donnent pas la règle C)
        // ──────────────────────────────────────────────────────────────
        public static int CalculerBonusEnfants(Salarie salarie, int annee)
        {
            if (salarie == null) return 0;
            if (salarie.Sexe != Sexe.Feminin) return 0;

            int totalBonus = 0;
            var dernierJourPeriode = new DateTime(annee, 12, 31);

            // Construit la liste des enfants à charge avec leur âge à fin période
            var enfantsACharge = new List<(DateTime? DN, int? Age)>();
            try
            {
                foreach (var e in salarie.Enfants)
                {
                    if (!e.ACharge) continue;
                    int? ageEnfant = null;
                    if (e.DateNaissance.HasValue)
                    {
                        var a = dernierJourPeriode.Year - e.DateNaissance.Value.Year;
                        if (e.DateNaissance.Value.Date > dernierJourPeriode.AddYears(-a)) a--;
                        ageEnfant = a < 0 ? 0 : a;
                    }
                    enfantsACharge.Add((e.DateNaissance, ageEnfant));
                }
            }
            catch { /* collection inaccessible — laisse vide */ }

            // Fallback : si pas de collection Enfants détaillée mais NombreEnfant > 0,
            // on suppose tous les enfants à charge mais sans âge connu
            // → seule la règle B (mère < 21 ans) peut s'appliquer prudemment
            //   sur la base de NombreEnfant, et la règle C à partir du 4ème.
            if (enfantsACharge.Count == 0 && salarie.NombreEnfant > 0)
            {
                for (int i = 0; i < salarie.NombreEnfant; i++)
                    enfantsACharge.Add((null, null));
            }

            // Calculer l'âge de la mère au dernier jour de la période de référence
            int? ageMere = null;
            if (salarie.Birthday != default && salarie.Birthday > new DateTime(1900, 1, 1))
            {
                var a = dernierJourPeriode.Year - salarie.Birthday.Year;
                if (salarie.Birthday.Date > dernierJourPeriode.AddYears(-a)) a--;
                ageMere = a < 0 ? 0 : a;
            }

            // ── V1.8 : Règle A SUPPRIMÉE (n'existe pas dans CCT L150) ─

            // ── RÈGLE B / C selon âge de la mère ──────────────────────
            if (!ageMere.HasValue)
            {
                // Birthday inconnue : on ne peut pas appliquer B/C → 0
                return 0;
            }

            if (ageMere.Value < 21)
            {
                // RÈGLE B : +2 jours / enfant à charge (toutes catégories)
                totalBonus += enfantsACharge.Count * 2;
            }
            else
            {
                // RÈGLE C : +2 jours / enfant mineur à partir du 4ème
                int enfantsMineurs = enfantsACharge
                    .Where(e => e.Age.HasValue && e.Age.Value < 18)
                    .Count();
                int enfantsAuDelaDe3 = Math.Max(0, enfantsMineurs - 3);
                totalBonus += enfantsAuDelaDe3 * 2;
            }

            return totalBonus;
        }
    }
}
