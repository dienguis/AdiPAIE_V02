// Services/BulletinModelService.cs
using System;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using AdiPAIE_V02.Module.BusinessObjects;

namespace AdiPAIE_V02.Module.Services
{
    public static class BulletinModelService
    {
        public static Bulletin CreateOrUpdateFromModel(Session session, Salarie salarie, int annee, int mois,
                                                       bool overwriteExistingLines = false, bool onlyIncludeDefault = true)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (salarie == null) throw new ArgumentNullException(nameof(salarie));

            var bulletin = session.FindObject<Bulletin>(
                               CriteriaOperator.Parse("Salarie = ? AND Annee = ? AND Mois = ?", salarie, annee, mois))
                         ?? new Bulletin(session) { Salarie = salarie, Annee = annee, Mois = mois };

            if (!bulletin.DateDebut.HasValue || !bulletin.DateFin.HasValue)
            {
                var first = new DateTime(annee, mois, 1);
                bulletin.DateDebut = first;
                bulletin.DateFin = first.AddMonths(1).AddDays(-1);
            }

            // ✨ centralise la copie + renseigne BulletinModeleSource
            bulletin.CopierDepuisModele(null, overwriteExistingLines, onlyIncludeDefault);

            bulletin.RecalculerTotaux();
            return bulletin;
        }
    }
}
