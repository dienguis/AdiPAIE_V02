// =============================================================================
//  TreiziemeMoisIntegrationService.cs — V1.7.2
//
//  Intègre les TreiziemeMois calculés au bulletin de paie correspondant
//  (décembre dans le cas normal, ou bulletin de STC pour les départs).
//
//  Crée une BulletinLigne de rubrique 13EME (canon TreiziemeMois = 700)
//  avec le montant calculé. Idempotent : si la ligne existe déjà sur le
//  bulletin, elle est mise à jour.
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
    public static class TreiziemeMoisIntegrationService
    {
        // Code de la rubrique 13ième mois seedée dans Updater.cs
        private const string CodeRubrique13EME = "13EME";

        /// <summary>
        /// Intègre tous les TreiziemeMois "Calculé" de l'année donnée
        /// dans le bulletin de décembre de chaque salarié.
        ///
        /// Pré-requis :
        ///   - Les TreiziemeMois doivent avoir été calculés via
        ///     TreiziemeMoisService.CalculerPourAnnee()
        ///   - Le bulletin de décembre doit exister (Statut quelconque,
        ///     y compris Brouillon) pour chaque salarié concerné
        ///
        /// Retourne un rapport texte du déroulement (nb intégrés, nb skip,
        /// nb erreurs avec détail).
        /// </summary>
        public static string IntegrerAuBulletinDecembre(IObjectSpace os, int annee)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));

            // Charger la rubrique 13EME une seule fois
            var rubrique13 = os.FirstOrDefault<Rubrique>(
                r => r.Code == CodeRubrique13EME);

            if (rubrique13 == null)
                return $"❌ Rubrique '{CodeRubrique13EME}' introuvable. " +
                       $"L'Updater doit la créer au démarrage de l'application.";

            // Tous les 13ièmes calculés cette année, non encore intégrés,
            // non sur STC (les STC sont gérés à part)
            var calculs = os.GetObjects<TreiziemeMois>().Cast<TreiziemeMois>()
                .Where(m => m.Annee == annee
                            && m.Statut == TreiziemeMoisStatut.Calcule
                            && !m.EstSurSTC)
                .ToList();

            int nbIntegres = 0;
            int nbSkipBulletinManquant = 0;
            int nbSkipDejaIntegree = 0;
            var erreurs = new List<string>();

            foreach (var m13 in calculs)
            {
                try
                {
                    if (m13.Salarie == null)
                    {
                        erreurs.Add($"13ième sans salarié : Oid {m13.Oid}");
                        continue;
                    }

                    // Trouver le bulletin de décembre
                    var bulletinDecembre = os.FirstOrDefault<Bulletin>(
                        b => b.Salarie.Oid == m13.Salarie.Oid
                             && b.Annee == annee
                             && b.Mois == 12);

                    if (bulletinDecembre == null)
                    {
                        nbSkipBulletinManquant++;
                        erreurs.Add(
                            $"Bulletin décembre {annee} manquant pour " +
                            $"{m13.Salarie.Matricule} – {m13.Salarie.FullName}. " +
                            $"Créer le bulletin avant intégration.");
                        continue;
                    }

                    // Vérifier qu'on ne ré-intègre pas (idempotence)
                    var ligneExistante = bulletinDecembre.Lignes
                        .FirstOrDefault(l => l.Rubrique != null
                                             && l.Rubrique.Code == CodeRubrique13EME);

                    if (ligneExistante != null)
                    {
                        // Mise à jour du montant si différent
                        if (ligneExistante.Montant != m13.MontantBrut)
                        {
                            ligneExistante.Montant = m13.MontantBrut;
                            ligneExistante.Base = m13.BrutRecurrentReference;
                            ligneExistante.Taux = m13.MoisPresence; // mois présence
                        }
                        nbSkipDejaIntegree++;
                    }
                    else
                    {
                        // Créer la ligne de bulletin
                        var ligne = os.CreateObject<BulletinLigne>();
                        ligne.Bulletin = bulletinDecembre;
                        ligne.Rubrique = rubrique13;
                        ligne.Base = m13.BrutRecurrentReference;
                        ligne.Taux = m13.MoisPresence; // mois présence (traçabilité)
                        ligne.Montant = m13.MontantBrut;

                        bulletinDecembre.Lignes.Add(ligne);
                        nbIntegres++;
                    }

                    // Mettre à jour le 13ième : statut + traçabilité
                    m13.Statut = TreiziemeMoisStatut.IntegreeBulletin;
                    m13.BulletinLie = bulletinDecembre;
                    m13.DateIntegration = DateTime.Now;
                }
                catch (Exception ex)
                {
                    erreurs.Add(
                        $"Erreur sur {m13.Salarie?.Matricule ?? "?"} : {ex.Message}");
                }
            }

            os.CommitChanges();

            // Rapport
            var rapport = new System.Text.StringBuilder();
            rapport.AppendLine($"✅ Intégration 13ième mois — Année {annee}");
            rapport.AppendLine($"───────────────────────────────");
            rapport.AppendLine($"  Bulletins intégrés     : {nbIntegres}");
            rapport.AppendLine($"  Déjà intégrés (skip)   : {nbSkipDejaIntegree}");
            rapport.AppendLine($"  Bulletin manquant      : {nbSkipBulletinManquant}");
            if (erreurs.Count > 0)
            {
                rapport.AppendLine();
                rapport.AppendLine($"⚠️ {erreurs.Count} alerte(s) :");
                foreach (var e in erreurs.Take(20))
                    rapport.AppendLine($"  • {e}");
                if (erreurs.Count > 20)
                    rapport.AppendLine($"  ... et {erreurs.Count - 20} de plus.");
            }
            return rapport.ToString();
        }

        /// <summary>
        /// Intègre un TreiziemeMois prorata sur le bulletin de sortie
        /// (STC = Solde de Tout Compte) d'un salarié qui quitte
        /// l'entreprise en cours d'année.
        ///
        /// Utilisé par STCService (#71) au moment de la préparation du STC.
        /// </summary>
        public static void IntegrerSurSTC(
            IObjectSpace os, TreiziemeMois m13, Bulletin bulletinSTC)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (m13 == null) throw new ArgumentNullException(nameof(m13));
            if (bulletinSTC == null) throw new ArgumentNullException(nameof(bulletinSTC));

            var rubrique13 = os.FirstOrDefault<Rubrique>(
                r => r.Code == CodeRubrique13EME);
            if (rubrique13 == null)
                throw new InvalidOperationException(
                    $"Rubrique '{CodeRubrique13EME}' introuvable.");

            // Idempotence
            var existante = bulletinSTC.Lignes
                .FirstOrDefault(l => l.Rubrique?.Code == CodeRubrique13EME);

            if (existante == null)
            {
                var ligne = os.CreateObject<BulletinLigne>();
                ligne.Bulletin = bulletinSTC;
                ligne.Rubrique = rubrique13;
                ligne.Base = m13.BrutRecurrentReference;
                ligne.Taux = m13.MoisPresence; // mois présence (traçabilité)
                ligne.Montant = m13.MontantBrut;
                bulletinSTC.Lignes.Add(ligne);
            }
            else
            {
                existante.Montant = m13.MontantBrut;
                existante.Base = m13.BrutRecurrentReference;
                existante.Taux = m13.MoisPresence; // mois présence (traçabilité)
            }

            m13.Statut = TreiziemeMoisStatut.IntegreeBulletin;
            m13.BulletinLie = bulletinSTC;
            m13.DateIntegration = DateTime.Now;
            m13.EstSurSTC = true;

            os.CommitChanges();
        }
    }
}
