using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Services
{
    public static class ExportComptableService
    {
        /// <summary>
        /// Crée et retourne une Ecriture pour le Bulletin donné, en ventilant toutes ses lignes.
        /// Ne fait pas de Commit : le contrôleur appelant gère le cycle ObjectSpace.
        /// </summary>
        public static Ecriture GenererEcriturePourBulletin(IObjectSpace os, Bulletin b)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (b == null) throw new ArgumentNullException(nameof(b));

            var ecr = os.CreateObject<Ecriture>();
            ecr.DateEcriture = b.DateFin ?? new DateTime(b.Annee, b.Mois, 1).AddMonths(1).AddDays(-1);
            ecr.Libelle = $"Paie {b.Mois:D2}/{b.Annee} - {b.Salarie?.FullName}";

            foreach (var l in b.Lignes)
            {
                if (l.Rubrique == null) continue;
                ComptaPostingService.PosterRubrique(
                    ecr,
                    l.Rubrique,
                    baseBrutSocial: b.BrutSocial,
                    montantSansTaux: l.Montant,
                    referenceLigne: l.Reference
                );
            }

            return ecr;
        }
    }
}
