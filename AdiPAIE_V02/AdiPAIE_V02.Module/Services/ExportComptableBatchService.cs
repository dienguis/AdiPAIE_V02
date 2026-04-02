using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class ExportComptableBatchService
    {
        /// <summary>
        /// Exporte tous les bulletins Validés pour (année, mois).
        /// Crée 1 écriture comptable par bulletin via EcritureFromBulletinService
        /// (version complète avec contrôle d'équilibre débit/crédit).
        /// Bascule le statut du bulletin en Exporte.
        /// Ne fait PAS de Commit : c'est à l'appelant.
        /// </summary>
        public static IList<Ecriture> ExporterMois(IObjectSpace os, int annee, int mois)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));

            var result = new List<Ecriture>();

            // Uniquement les bulletins Validés (pas encore exportés)
            var crit = CriteriaOperator.Parse(
                "Annee = ? AND Mois = ? AND Statut = ?",
                annee, mois, BulletinStatut.Valide);

            var bulletins = os.GetObjects<Bulletin>(crit);

            if (!bulletins.Any())
                throw new UserFriendlyException(
                    $"Aucun bulletin Validé trouvé pour {mois:D2}/{annee}.");

            // Instance du service (prend l'IObjectSpace en paramètre)
            var svc = new EcritureFromBulletinService(os);

            foreach (var b in bulletins)
            {
                // Recalcul préventif pour s'assurer que les totaux sont à jour
                b.RecalculerSurGrilleExistante();

                // Génère l'écriture (avec contrôle d'équilibre débit = crédit)
                var ecr = svc.GenererEcriture(b);
                result.Add(ecr);

                // Marque le bulletin comme exporté en comptabilité
                b.Statut = BulletinStatut.Exporte;
            }

            return result;
        }

        /// <summary>
        /// Variante : export pour le mois courant (date système).
        /// </summary>
        public static IList<Ecriture> ExporterMoisCourant(IObjectSpace os)
        {
            var today = DateTime.Today;
            return ExporterMois(os, today.Year, today.Month);
        }
    }
}
