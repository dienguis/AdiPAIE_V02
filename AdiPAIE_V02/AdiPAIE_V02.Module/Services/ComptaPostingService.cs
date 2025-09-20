using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Services
{
    public static class ComptaPostingService
    {
        /// <summary>
        /// Ventile une rubrique en lignes d'écritures selon le modèle simplifié.
        /// - Sans taux : Débit = CompteDebitDefaut, Crédit = CompteCreditDefaut
        /// - Taux1 (part salarié) : Débit = ComptePersonnel, Crédit = CompteTaux1
        /// - Taux2 (part employeur) : Débit = CompteChargeEmployeurDefaut, Crédit = CompteTaux2
        /// </summary>
        /// <param name="rub">Rubrique paramétrée</param>
        /// <param name="baseBrutSocial">base pour cotisations (BS), si utile (plafond appliqué)</param>
        /// <param name="montantSansTaux">montant à passer quand il n’y a pas de taux (gains/indemnités/retenues fixes)</param>
        public static void PosterRubrique(Ecriture ecr, Rubrique rub,decimal baseBrutSocial, decimal montantSansTaux,
                                           string referenceLigne = null)
        {
            var session = ecr.Session; // ✅ on la prend ici
            var parms = ParametresCompta.GetOrCreate(session);

            bool aT1 = rub.Taux1.HasValue && rub.CompteTaux1 != null;
            bool aT2 = rub.Taux2.HasValue && rub.CompteTaux2 != null;

            if (aT1 || aT2)
            {
                var baseCot = baseBrutSocial;
                if (rub.Plafond.HasValue && rub.Plafond.Value > 0)
                    baseCot = Math.Min(baseCot, rub.Plafond.Value);

                if (aT1)
                {
                    var m1 = Math.Round(baseCot * rub.Taux1.Value / 100m, 0, MidpointRounding.AwayFromZero);
                    if (m1 != 0) AddLigne(ecr, parms.ComptePersonnel, rub.CompteTaux1, m1, referenceLigne ?? rub.Code + " T1");
                }
                if (aT2)
                {
                    var m2 = Math.Round(baseCot * rub.Taux2.Value / 100m, 0, MidpointRounding.AwayFromZero);
                    if (m2 != 0) AddLigne(ecr, parms.CompteChargeEmployeurDefaut, rub.CompteTaux2, m2, referenceLigne ?? rub.Code + " T2");
                }
                return;
            }

            if (rub.CompteDebitDefaut == null || rub.CompteCreditDefaut == null)
                throw new DevExpress.ExpressApp.UserFriendlyException($"Comptes par défaut manquants sur la rubrique {rub.Code}.");

            if (montantSansTaux > 0)
            {
                AddLigne(ecr, rub.CompteDebitDefaut, rub.CompteCreditDefaut, montantSansTaux, referenceLigne ?? rub.Code);
            }
            else if (montantSansTaux < 0)
            {
                AddLigne(ecr, rub.CompteCreditDefaut, rub.CompteDebitDefaut, Math.Abs(montantSansTaux), referenceLigne ?? rub.Code);
            }
        }

        private static void AddLigne(Ecriture ecr, PlanComptable debit, PlanComptable credit, decimal montant, string reference)
        {
            var s = ecr.Session; 
            _ = new EcritureLigne(s) { Ecriture = ecr, Compte = debit, Sens = SensEcriture.Debit, Montant = montant, Reference = reference };
            _ = new EcritureLigne(s) { Ecriture = ecr, Compte = credit, Sens = SensEcriture.Credit, Montant = montant, Reference = reference };
        }

    }
}
