using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class ExportComptableBatchService
    {
        /// <summary>
        /// Exporte tous les bulletins "Validés" pour (année, mois) -> crée 1 écriture par bulletin,
        /// et bascule leur statut en "Exporte". Ne fait PAS de Commit : c'est à l'appelant.
        /// </summary>
        public static IList<Ecriture> ExporterMois(IObjectSpace os, int annee, int mois)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            var result = new List<Ecriture>();

            // Récupère uniquement les bulletins Validés (donc non exportés)
            var crit = CriteriaOperator.Parse("Annee = ? AND Mois = ? AND Statut = ?", annee, mois, BulletinStatut.Valide);
            var bulletins = os.GetObjects<Bulletin>(crit);

            foreach (var b in bulletins)
            {
                // Option : s'assurer que les totaux sont à jour
                b.RecalculerSurGrilleExistante();

                // Génère l'écriture pour ce bulletin
                var ecr = ExportComptableService.GenererEcriturePourBulletin(os, b);
                result.Add(ecr);

                // Marque le bulletin comme exporté
                b.Statut = BulletinStatut.Exporte;
            }
            return result;
        }

        /// <summary>
        /// Variante : export pour le mois courant (selon la date système).
        /// </summary>
        public static IList<Ecriture> ExporterMoisCourant(IObjectSpace os)
        {
            var today = DateTime.Today;
            return ExporterMois(os, today.Year, today.Month);
        }

    }
}
