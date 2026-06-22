using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class PeriodePaieHelper
    {
        /// <summary>
        /// V1.7.2 — Retourne la période de paie actuellement OUVERTE (la plus
        /// récente si plusieurs). Utilisée pour initialiser intelligemment
        /// les nouveaux 13ièmes mois / gratifications afin d'éviter qu'un
        /// utilisateur les saisisse sur un mois clôturé ou inexistant.
        ///
        /// Retourne null si aucune période n'est ouverte.
        /// </summary>
        public static PeriodePaie GetPeriodeOuverte(Session session)
        {
            if (session == null) return null;
            return session.Query<PeriodePaie>()
                .Where(p => p.Statut == PeriodePaieStatut.Ouverte)
                .OrderByDescending(p => p.Annee)
                .ThenByDescending(p => p.Mois)
                .FirstOrDefault();
        }


        /// <summary>
        /// Crée (si manquantes) les périodes d'une année.
        /// </summary>
        /// <param name="os">IObjectSpace</param>
        /// <param name="annee">Année cible</param>
        /// <param name="moisDebut">1 par défaut</param>
        /// <param name="moisFin">12 par défaut</param>
        /// <param name="ouvrirActuelle">
        /// Si true, ouvre la période du mois courant (si toutes les précédentes sont clôturées).
        /// </param>
        public static void EnsurePeriodes(IObjectSpace os, int annee, int moisDebut = 1, int moisFin = 12, bool ouvrirActuelle = false)
        {
            if (moisDebut < 1) moisDebut = 1;
            if (moisFin > 12) moisFin = 12;

            for (int m = moisDebut; m <= moisFin; m++)
            {
                var exists = os.FindObject<PeriodePaie>(
                    CriteriaOperator.Parse("Annee = ? AND Mois = ?", annee, m));
                if (exists != null)
                    continue;

                var p = os.CreateObject<PeriodePaie>();
                p.Annee = annee;
                p.Mois = m;
                p.Libelle = $"Période {m:D2}/{annee}";    // ← libellé court
                p.Note = "";                               // optionnel
                p.DateDebut = new DateTime(annee, m, 1);
                p.DateFin = p.DateDebut.Value.AddMonths(1).AddDays(-1);


                // Statut par défaut : Brouillon (recommandé)
            }

            // Ouvrir la période du mois courant si demandé
            if (ouvrirActuelle)
            {
                var now = DateTime.Today;
                if (now.Year == annee)
                {
                    var current = os.FindObject<PeriodePaie>(
                        CriteriaOperator.Parse("Annee = ? AND Mois = ?", annee, now.Month));
                    if (current != null)
                    {
                        // n’ouvre que si toutes les précédentes sont clôturées
                        var prevNotClosed = os.GetObjects<PeriodePaie>(
                            CriteriaOperator.Parse("Annee = ? AND Mois < ? AND Statut <> ?",
                                annee, now.Month, Domain.DomainEnums.PeriodePaieStatut.Cloturee)).Count > 0;

                        if (!prevNotClosed && current.Statut == Domain.DomainEnums.PeriodePaieStatut.Brouillon)
                            current.Statut = Domain.DomainEnums.PeriodePaieStatut.Ouverte;
                    }
                }
            }

            os.CommitChanges(); // facultatif si tu veux engager immédiatement
        }
    }
}
