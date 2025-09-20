using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class PretClotureService
    {
        public static void EntérinerRemboursementsPour(Bulletin b)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (b.Statut != BulletinStatut.Valide && b.Statut != BulletinStatut.Cloture)
                throw new UserFriendlyException("Le bulletin doit être validé avant la clôture des remboursements.");

            // Montant attendu sur la ligne “RemboursementPret”
            var ligne = b.Lignes.FirstOrDefault(x => x.Rubrique?.Canonique == RubriqueCanonique.RemboursementPret);
            var montantLigne = ligne?.Montant ?? 0m;

            // Échéances du mois
              var debutMois = new DateTime(b.Annee, b.Mois, 1);
            var finMois = debutMois.AddMonths(1).AddDays(-1);

            var echeances = b.Session.Query<PretEcheance>()
                .Where(e => e.Pret != null
                         && e.Pret.Salarie == b.Salarie
                         && e.Statut == PretEcheanceStatut.Prevue
                         && e.DateEcheance >= debutMois
                         && e.DateEcheance <= finMois)
                .OrderBy(e => e.DateEcheance)        // ⬅️ au lieu de .OrderBy(e => e.Ordre)
                .ThenBy(e => e.Oid)                  // détermine l’ordre si même date
                .ToList();

            var totalEch = echeances.Sum(e => e.MontantTotal);


            // Sécurité : on peut imposer l’égalité (ou à minima avertir)
            if (montantLigne != totalEch)
            {
                // Selon ta politique : lever une erreur, ou accepter et prélever au réel des échéances
                // Ici : on prélève au réel et on aligne la ligne si elle existe
                if (ligne != null)
                {
                    ligne.Montant = totalEch;
                }
            }
            // Marquage “définitif”
            var now = DateTime.Now;
            foreach (var e in echeances)
            {
                if (e.Statut != PretEcheanceStatut.Prevue) continue;

                e.Statut = PretEcheanceStatut.Prelevee;
                e.DatePrelevement = now;          // ← propriété qu’on a ajoutée dans PretEcheance
                e.BulletinPreleveur = b;          // ← bon nom de la propriété d’association
                          }


            // Rafraîchir l’état des prêts concernés
            foreach (var pr in echeances.Select(x => x.Pret).Distinct())
            {
                pr.RecalculerEtat();   // mettra le prêt à Terminé si plus d’échéances prévues, etc.
            }

        }
    }

}
