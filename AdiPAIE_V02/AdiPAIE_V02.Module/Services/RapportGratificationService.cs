// =============================================================================
//  RapportGratificationService.cs — V1.7.2e
//
//  Agrège les Gratifications persistantes par (Salarié × Année) pour
//  reporting a posteriori. Inclut uniquement les statuts IntegreeBulletin
//  et Payee (= gratifications réellement versées).
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using DevExpress.ExpressApp;
using System;
using System.Collections.Generic;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class RapportGratificationService
    {
        /// <summary>
        /// Calcule le rapport agrégé des gratifications versées dans l'année.
        /// Granularité : 1 ligne par (Salarié × Année).
        /// Filtres : statut IntegreeBulletin OU Payee (= versées effectivement).
        /// </summary>
        public static List<RapportGratification> Calculer(
            IObjectSpace persistentOs,
            IObjectSpace nonPersistentOs,
            int annee)
        {
            if (persistentOs == null) throw new ArgumentNullException(nameof(persistentOs));
            if (nonPersistentOs == null) throw new ArgumentNullException(nameof(nonPersistentOs));

            var resultat = new List<RapportGratification>();
            var session = ((DevExpress.ExpressApp.Xpo.XPObjectSpace)persistentOs).Session;

            // Récupérer toutes les gratifications de l'année réellement
            // versées (statut IntegreeBulletin ou Payee)
            var gratifs = persistentOs.GetObjectsQuery<Gratification>()
                .Where(g => g.Annee == annee
                            && g.Salarie != null
                            && (g.Statut == GratificationStatut.IntegreeBulletin
                                || g.Statut == GratificationStatut.Payee))
                .ToList();

            // Grouper par salarié
            var parSalarie = gratifs
                .GroupBy(g => g.Salarie)
                .ToList();

            foreach (var groupe in parSalarie)
            {
                var salarie = groupe.Key;
                var listeGratifs = groupe.ToList();

                var rapp = nonPersistentOs.CreateObject<RapportGratification>();
                rapp.Annee = annee;
                rapp.Matricule = salarie.Matricule;
                rapp.NomComplet = salarie.FullName;
                rapp.Departement = salarie.Departement?.Nom;
                rapp.Fonction = salarie.Fonction?.Intitule;

                rapp.NombreGratifications = listeGratifs.Count;
                rapp.MontantTotalAnnee = listeGratifs.Sum(g => g.MontantCalcule);

                // Multiplicateur moyen (hors forfaits)
                var avecMulti = listeGratifs
                    .Where(g => g.BaseCalcul != GratificationBaseCalcul.Forfait
                                && g.Multiplicateur > 0)
                    .ToList();
                rapp.MultiplicateurMoyen = avecMulti.Count > 0
                    ? Math.Round(avecMulti.Average(g => g.Multiplicateur), 2)
                    : 0m;

                rapp.DernierMoisVersement = listeGratifs.Max(g => g.MoisPaiement);

                // Cumul brut récurrent annuel du salarié
                rapp.BrutRecurrentAnnuel = BrutRecurrentService
                    .GetCumulBrutRecurrent(session, salarie, annee);

                rapp.PourcentageSurBrutAnnuel = rapp.BrutRecurrentAnnuel > 0
                    ? Math.Round(
                        rapp.MontantTotalAnnee / rapp.BrutRecurrentAnnuel * 100m,
                        1, MidpointRounding.AwayFromZero)
                    : 0m;

                resultat.Add(rapp);
            }

            return resultat;
        }
    }
}
