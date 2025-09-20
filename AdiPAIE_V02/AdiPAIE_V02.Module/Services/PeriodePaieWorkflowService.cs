// Module/Services/PeriodePaieWorkflowService.cs
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class PeriodePaieWorkflowService
    {
        // ===== API =====

        public static void ClosePeriod(IObjectSpace os, PeriodePaie per, bool createNextIfMissing = true)
        {
            // Garde-fous
            if (per.Statut != PeriodePaieStatut.Ouverte)
                throw new UserFriendlyException($"La période {per.Note} n’est pas ouverte.");

            // Aucune période antérieure non clôturée
            var previousOpen = os.GetObjectsQuery<PeriodePaie>()
                .Any(p2 => (p2.Annee < per.Annee || (p2.Annee == per.Annee && p2.Mois < per.Mois))
                        && p2.Statut != PeriodePaieStatut.Cloturee);
            if (previousOpen)
                throw new UserFriendlyException("Vous devez d’abord clôturer toutes les périodes antérieures.");

            // Bulletins du mois
            var bulletins = os.GetObjectsQuery<Bulletin>()
                .Where(b => b.Annee == per.Annee && b.Mois == per.Mois)
                .ToList();
            if (bulletins.Count == 0)
                throw new UserFriendlyException($"Aucun bulletin trouvé pour {per.Libelle}.");

            var brouillons = bulletins.Where(b => b.Statut == BulletinStatut.Brouillon).ToList();
            if (brouillons.Any())
                throw new UserFriendlyException($"Il reste {brouillons.Count} bulletin(s) en brouillon sur {per.Libelle}. Validez-les d’abord.");

            // Appliquer les retenues de prêts + marquer les échéances
            foreach (var b in bulletins)
                AppliquerRetenuesPretsPourBulletin(os, b);

            // Figer les bulletins (Clôturé si valeur dispo, sinon Exporte)
            var clotureStatut = TryParseClotureOrExport();
            foreach (var b in bulletins)
                b.Statut = clotureStatut;

            // Clôturer la période
            per.Statut = PeriodePaieStatut.Cloturee;
            per.DateCloture = DateTime.Now;

            // Créer la période suivante si absente
            if (createNextIfMissing)
                CreerPeriodeSuivanteSiAbsente(os, per);
        }

        public static void OpenNextPeriod(IObjectSpace os, PeriodePaie per)
        {
            int an = per.Annee, m = per.Mois;
            if (m == 12) { an += 1; m = 1; } else { m += 1; }

            var next = os.GetObjectsQuery<PeriodePaie>()
                .FirstOrDefault(p => p.Annee == an && p.Mois == m);

            if (next == null)
            {
                next = os.CreateObject<PeriodePaie>();
                next.Annee = an;
                next.Mois = m;
            }

            next.Statut = PeriodePaieStatut.Ouverte;
            if (!next.DateOuverture.HasValue)
                next.DateOuverture = DateTime.Now;
        }

        public static void ReopenPeriod(IObjectSpace os, PeriodePaie per)
        {
            if (per.Statut != PeriodePaieStatut.Cloturee)
                throw new UserFriendlyException($"La période {per.Note} n’est pas clôturée.");

            // Bulletins du mois à repasser en 'Valide'
            var bulletins = os.GetObjectsQuery<Bulletin>()
                .Where(b => b.Annee == per.Annee && b.Mois == per.Mois)
                .ToList();

            // Revenir à 'Valide'
            foreach (var b in bulletins)
                b.Statut = BulletinStatut.Valide;

            // Rebasculer les échéances prélevées → prévues
            var debut = new DateTime(per.Annee, per.Mois, 1);
            var fin = debut.AddMonths(1).AddDays(-1);

            var echPrelevees = os.GetObjectsQuery<PretEcheance>()
                .Where(e => e.Statut == PretEcheanceStatut.Prelevee
                         && e.BulletinPreleveur != null
                         && e.DateEcheance >= debut && e.DateEcheance <= fin)
                .ToList();

            foreach (var e in echPrelevees)
            {
                e.Statut = PretEcheanceStatut.Prevue;
                e.BulletinPreleveur = null;
                // Optionnel : DatePrelevement si présent
                TrySetDatePrelevement(e, null);
                e.Pret?.RecalculerEtat();
            }

            per.Statut = PeriodePaieStatut.Ouverte;
            per.DateCloture = null;
        }

        public static void CloseYear(IObjectSpace os, int year, bool requireCurrentYear = true)
        {
            if (requireCurrentYear && year != DateTime.Today.Year)
                throw new UserFriendlyException("Seule l’année courante peut être clôturée via cette action.");

            var periods = os.GetObjectsQuery<PeriodePaie>()
                .Where(p => p.Annee == year)
                .OrderBy(p => p.Mois)
                .ToList();

            if (periods.Count == 0)
                throw new UserFriendlyException($"Aucune période trouvée pour {year}.");

            foreach (var per in periods.Where(p => p.Statut == PeriodePaieStatut.Ouverte))
                ClosePeriod(os, per, createNextIfMissing: false);
        }

        // ===== Internes =====

        private static BulletinStatut TryParseClotureOrExport()
        {
            try
            {
                if (Enum.TryParse(typeof(BulletinStatut), "Cloture", true, out var v) && v is BulletinStatut ok)
                    return ok;
            }
            catch { }
            return BulletinStatut.Exporte;
        }

        private static void AppliquerRetenuesPretsPourBulletin(IObjectSpace os, Bulletin b)
        {
            var debutMois = new DateTime(b.Annee, b.Mois, 1);
            var finMois = debutMois.AddMonths(1).AddDays(-1);

            var echeances = os.GetObjectsQuery<PretEcheance>()
                .Where(e => e.Pret != null
                         && e.Pret.Salarie == b.Salarie
                         && e.Statut == PretEcheanceStatut.Prevue
                         && e.DateEcheance >= debutMois
                         && e.DateEcheance <= finMois
                         && e.Pret.Statut == PretStatut.EnCours)
                .OrderBy(e => e.DateEcheance).ThenBy(e => e.Oid)
                .ToList();

            var total = echeances.Sum(e => e.MontantTotal);
            if (total <= 0) return;

            var l = b.EnsureLine(Domain.DomainEnums.RubriqueCanonique.RemboursementPret, createIfMissing: true);
            if (l == null)
                throw new UserFriendlyException("La rubrique canonique PRET_Remboursement est manquante/inactive.");

            l.Base = total;
            l.Taux = null;
            l.Montant = total;
            l.IsSystem = true;
            if (!l.OrdreCalcul.HasValue) l.OrdreCalcul = l.Rubrique?.OrdreAffichage;

            foreach (var e in echeances)
            {
                e.Statut = PretEcheanceStatut.Prelevee;
                e.BulletinPreleveur = b;
                TrySetDatePrelevement(e, DateTime.Now);
                e.Pret?.RecalculerEtat();
            }
        }

        private static void CreerPeriodeSuivanteSiAbsente(IObjectSpace os, PeriodePaie source)
        {
            int an = source.Annee, m = source.Mois;
            if (m == 12) { an += 1; m = 1; } else { m += 1; }

            var existe = os.GetObjectsQuery<PeriodePaie>().Any(p => p.Annee == an && p.Mois == m);
            if (!existe)
            {
                var next = os.CreateObject<PeriodePaie>();
                next.Annee = an;
                next.Mois = m;
                next.Statut = PeriodePaieStatut.Ouverte;
                next.DateOuverture = DateTime.Now;
            }
        }

        // Support facultatif d'une propriété DatePrelevement
        private static void TrySetDatePrelevement(PretEcheance e, DateTime? value)
        {
            var prop = e.GetType().GetProperty("DatePrelevement");
            prop?.SetValue(e, value);
        }
    }
}
