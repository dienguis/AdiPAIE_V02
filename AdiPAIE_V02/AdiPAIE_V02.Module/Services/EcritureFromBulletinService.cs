// AdiPAIE_V02.Module/Services/EcritureFromBulletinService.cs
using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    public class EcritureFromBulletinService
    {
        private readonly IObjectSpace os;
        private readonly ICompteResolver resolver;

        public EcritureFromBulletinService(IObjectSpace os)
        {
            this.os = os;
            this.resolver = new CompteResolver(os);
        }

        public Ecriture GenererEcriture(Bulletin bulletin)
        {
            if (bulletin == null) throw new ArgumentNullException(nameof(bulletin));
            if (bulletin.Lignes == null || bulletin.Lignes.Count == 0)
                throw new UserFriendlyException("Le bulletin ne contient aucune ligne.");

            // ✅ Résoudre une date sûre pour l’écriture
            var dateEcr = ResolveDateEcriture(bulletin);

            var ecr = os.CreateObject<Ecriture>();
            ecr.DateEcriture = dateEcr;

            // Libellé lisible avec fallback si Annee/Mois non renseignés
            var libPeriode = (bulletin.Annee > 0 && bulletin.Mois > 0)
                ? $"{bulletin.Mois:00}/{bulletin.Annee}"
                : dateEcr.ToString("MM/yyyy");
            ecr.Libelle = $"Paie {libPeriode} - {bulletin.Salarie?.Matricule}";

            foreach (var l in bulletin.Lignes)
            {
                var rub = l.Rubrique;
                if (rub == null || l.Montant == 0) continue;

                // ⚠️ Important : on résout à la date comptable retenue
                var debit = resolver.Resolve(rub, TypeAffectationCompte.Debit, dateEcr.Date);
                var credit = resolver.Resolve(rub, TypeAffectationCompte.Credit, dateEcr.Date);

                var montant = Math.Abs(l.Montant);

                if (debit != null)
                {
                    var ld = os.CreateObject<EcritureLigne>();
                    ld.Ecriture = ecr;
                    ld.Compte = debit;          // 🔒 compte copié (historique figé)
                    ld.Sens = SensEcriture.Debit;
                    ld.Montant = montant;
                    ld.Reference = l.Reference;
                }
                if (credit != null)
                {
                    var lc = os.CreateObject<EcritureLigne>();
                    lc.Ecriture = ecr;
                    lc.Compte = credit;         // 🔒 compte copié (historique figé)
                    lc.Sens = SensEcriture.Credit;
                    lc.Montant = montant;
                    lc.Reference = l.Reference;
                }
            }

            // Contrôle d’équilibre
            var totD = ecr.Lignes.Where(x => x.Sens == SensEcriture.Debit).Sum(x => x.Montant);
            var totC = ecr.Lignes.Where(x => x.Sens == SensEcriture.Credit).Sum(x => x.Montant);
            if (totD != totC)
                throw new UserFriendlyException($"Écriture non équilibrée (Débit {totD} / Crédit {totC}). Vérifie la config des comptes.");

            os.CommitChanges();
            return ecr;
        }

        // --------- Date de comptabilisation (stratégie A) ---------
        private static DateTime ResolveDateEcriture(Bulletin b)
        {
            if (b.DateFin.HasValue) return b.DateFin.Value;

            // si le bulletin expose Annee/Mois
            if (b.Annee > 0 && b.Mois > 0 && b.Mois <= 12)
                return new DateTime(b.Annee, b.Mois, DateTime.DaysInMonth(b.Annee, b.Mois));

            if (b.DateDebut.HasValue) return b.DateDebut.Value;

            return DateTime.Today;
        }
    }
}
