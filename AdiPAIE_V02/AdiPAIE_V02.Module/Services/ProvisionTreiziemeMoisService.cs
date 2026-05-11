// =============================================================================
//  ProvisionTreiziemeMoisService.cs — V1.7.2c
//
//  Construit la vue Provision 13ième mois (1 ligne par Salarié × Mois)
//  à partir des bulletins de l'année.
//
//  Règle ELTON :
//    - Pour chaque bulletin existant : BrutRecurrent = somme des gains
//      récurrents (cf. BrutRecurrentService)
//    - ProvisionDuMois = BrutRecurrent / 12 (idéalement 1 douzième du
//      futur 13ième théorique)
//    - CumulProvisionsAnnee = somme cumulative des provisions depuis
//      janvier
//    - MontantFinal13ieme = montant intégré au bulletin de décembre
//      (si déjà calculé via TreiziemeMois.Statut=IntegreeBulletin)
//    - Ecart = MontantFinal - CumulProvisions (= variation entre théorique
//      mensuel et réel intégré)
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using DevExpress.ExpressApp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdiPAIE_V02.Module.Services
{
    public static class ProvisionTreiziemeMoisService
    {
        /// <summary>
        /// Calcule la vue Provision 13ième mois pour une année donnée.
        /// Retourne 1 ligne par (Salarié × Mois) ayant un bulletin.
        /// </summary>
        public static List<ProvisionTreiziemeMois> Calculer(
            IObjectSpace persistentOs,
            IObjectSpace nonPersistentOs,
            int annee)
        {
            if (persistentOs == null) throw new ArgumentNullException(nameof(persistentOs));
            if (nonPersistentOs == null) throw new ArgumentNullException(nameof(nonPersistentOs));

            var resultat = new List<ProvisionTreiziemeMois>();

            // Récupérer tous les bulletins de l'année
            var bulletins = persistentOs.GetObjectsQuery<Bulletin>()
                .Where(b => b.Annee == annee && b.Salarie != null)
                .ToList();

            // Grouper par salarié pour calculer le cumul progressif
            var parSalarie = bulletins
                .GroupBy(b => b.Salarie)
                .ToList();

            foreach (var groupe in parSalarie)
            {
                var salarie = groupe.Key;

                // Trier les bulletins du salarié par mois croissant
                var bulletinsTries = groupe.OrderBy(b => b.Mois).ToList();
                decimal cumul = 0m;

                // 13ième effectivement intégré (cherche TreiziemeMois en
                // base pour ce salarié × année)
                var m13 = persistentOs.FirstOrDefault<TreiziemeMois>(
                    m => m.Salarie.Oid == salarie.Oid && m.Annee == annee);
                decimal? montantFinal = null;
                if (m13 != null &&
                    m13.Statut == AdiPAIE_V02.Module.Domain.DomainEnums
                        .TreiziemeMoisStatut.IntegreeBulletin)
                {
                    montantFinal = m13.MontantBrut;
                }

                foreach (var bulletin in bulletinsTries)
                {
                    var brut = BrutRecurrentService.GetBrutRecurrent(bulletin);
                    var provision = Math.Round(brut / 12m, 0, MidpointRounding.AwayFromZero);
                    cumul += provision;

                    var ligne = nonPersistentOs.CreateObject<ProvisionTreiziemeMois>();
                    ligne.Annee = annee;
                    ligne.Mois = bulletin.Mois;
                    ligne.Matricule = salarie.Matricule;
                    ligne.NomComplet = salarie.FullName;
                    ligne.Departement = salarie.Departement?.Nom;
                    ligne.BrutRecurrent = brut;
                    ligne.ProvisionDuMois = provision;
                    ligne.CumulProvisionsAnnee = cumul;

                    // L'écart n'apparaît qu'en décembre (quand le 13ième
                    // a un montant final connu)
                    if (bulletin.Mois == 12 && montantFinal.HasValue)
                    {
                        ligne.MontantFinal13ieme = montantFinal.Value;
                        ligne.Ecart = montantFinal.Value - cumul;
                    }

                    resultat.Add(ligne);
                }
            }

            return resultat;
        }
    }
}
