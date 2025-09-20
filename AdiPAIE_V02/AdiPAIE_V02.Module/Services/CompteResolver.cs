using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    public interface ICompteResolver
    {
        PlanComptable Resolve(Rubrique rub, TypeAffectationCompte type, DateTime datePeriode);
    }

    public class CompteResolver : ICompteResolver
    {
        private readonly IObjectSpace os;
        public CompteResolver(IObjectSpace os) => this.os = os;

        public PlanComptable Resolve(Rubrique rub, TypeAffectationCompte type, DateTime d)
        {
            if (rub == null) return null;

            // 1) Override daté au niveau Rubrique
            var rc = os.GetObjectsQuery<RubriqueCompte>()
                .Where(x => x.Rubrique == rub
                            && x.Type == (TypeAffectationCompte?)type    // cast car propriété nullable
                            && x.Actif
                            && (x.DateDebut == null || x.DateDebut <= d)
                            && (x.DateFin == null || x.DateFin >= d))
                .OrderByDescending(x => x.DateDebut)
                .FirstOrDefault();
            if (rc?.Compte != null)
                return rc.Compte;

            // 2) Override global par Rôle canonique
            if (rub.Canonique.HasValue)
            {
                var ov = os.GetObjectsQuery<RubriqueCompteOverride>()
                    .Where(x => x.Canonique == rub.Canonique
                                && x.Type == (TypeAffectationCompte?)type
                                && x.Actif
                                && (x.DateDebut == null || x.DateDebut <= d)
                                && (x.DateFin == null || x.DateFin >= d))
                    .OrderByDescending(x => x.DateDebut)
                    .FirstOrDefault();
                if (ov?.Compte != null)
                    return ov.Compte;
            }

            // 3) Defaults de la Rubrique
            if (type == TypeAffectationCompte.Debit && rub.CompteDebitDefaut != null) return rub.CompteDebitDefaut;
            if (type == TypeAffectationCompte.Credit && rub.CompteCreditDefaut != null) return rub.CompteCreditDefaut;

            // 4) Fallback global
            var prm = os.GetObjectsQuery<ParametresCompta>().FirstOrDefault();
            if (type == TypeAffectationCompte.Credit) return prm?.ComptePersonnel;
            return prm?.CompteChargeEmployeurDefaut ?? prm?.ComptePersonnel;
        }
    }
}
