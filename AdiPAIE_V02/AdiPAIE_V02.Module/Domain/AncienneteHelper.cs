using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Domain
{
    public static class AncienneteHelper
    {
          /// <summary>Ancienneté en nombre total de mois.</summary>
        public static int TotalMois(DateTime dateEmbauche, DateTime asOf)
        {
            if (dateEmbauche == default || asOf <= dateEmbauche) return 0;
            int months = (asOf.Year - dateEmbauche.Year) * 12 + asOf.Month - dateEmbauche.Month;
            if (asOf.Day < dateEmbauche.Day) months--;
            return Math.Max(months, 0);
        }


        public static int NombreAnnee(DateTime dateEmbauche, DateTime? referenceDate = null)
        {
            var refDate = (referenceDate ?? DateTime.Today).Date;
            var start = dateEmbauche.Date;
            if (refDate <= start) return 0;

            int years = refDate.Year - start.Year;
            if (refDate.Month < start.Month || (refDate.Month == start.Month && refDate.Day < start.Day))
            {
                years--;
            }
            return Math.Max(0, years);
        }

        public static (int taux, decimal gain) CalcAnciennete(decimal @base, DateTime dateEmbauche, DateTime dateRef)
        {
            int anneesPleines = NombreAnnee(dateEmbauche, dateRef);
            if (anneesPleines >= 2 && anneesPleines <= 25)
            {
                var taux = anneesPleines;                 // ex: 7 (%)
                var gain = (@base * taux) / 100m;
                return (taux, gain);
            }
            return (0, 0m);
        }

    }

}

