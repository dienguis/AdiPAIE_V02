using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    public class PeriodePaieCreateYearController : ObjectViewController<ListView, PeriodePaie>
    {
        private readonly PopupWindowShowAction creerAnneeAction;

        public PeriodePaieCreateYearController()
        {
            creerAnneeAction = new PopupWindowShowAction(this, "PER_CreerAnnee", PredefinedCategory.Edit)
            {
                Caption = "Créer l’année…",
                ImageName = "Action_New"
            };
            creerAnneeAction.CustomizePopupWindowParams += OnCustomizePopup;
            creerAnneeAction.Execute += OnExecute;
        }

        private void OnCustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var os = Application.CreateObjectSpace(typeof(PeriodePaie));
            var param = new PeriodeAnneeParam { Annee = DateTime.Today.Year };
            var dv = Application.CreateDetailView(os, param);
            dv.ViewEditMode = DevExpress.ExpressApp.Editors.ViewEditMode.Edit;
            e.View = dv;
        }

        private void OnExecute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var os = e.PopupWindowView?.ObjectSpace ?? Application.CreateObjectSpace(typeof(PeriodePaie));
            var p = (PeriodeAnneeParam)e.PopupWindowView.CurrentObject;

            // Récupérer l'unique Company
            var companies = os.GetObjectsQuery<Company>().Take(2).ToList();
            if (companies.Count == 0)
                throw new UserFriendlyException("Aucune entreprise n’est définie. Créez d’abord la Company.");
            if (companies.Count > 1)
                throw new UserFriendlyException("Plusieurs entreprises trouvées. Le mode mono-entreprise exige une seule Company.");

            var company = companies[0];


            // Avertissement si année ≠ année courante
            if (p.Annee != DateTime.Today.Year)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Attention : vous créez les périodes pour {p.Annee}.", InformationType.Warning, 4000, InformationPosition.Bottom);
            }

            // Création 12 périodes (mois 1..12) si absentes
            var existants = new XPQuery<PeriodePaie>(((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                .Where(x => x.Annee == p.Annee)
                .Select(x => x.Mois)
                .ToList();

            for (int m = 1; m <= 12; m++)
            {
                if (existants.Contains(m)) continue;

                var per = os.CreateObject<PeriodePaie>();
                per.Annee = p.Annee;
                per.Mois = m;
                per.Company = company;
                // bornes correctes par mois
                per.DateDebut = new DateTime(p.Annee, m, 1);
                per.DateFin = per.DateDebut.Value.AddMonths(1).AddDays(-1);
               
                // clé + libellé
                per.Key = $"{p.Annee:D4}-{m:D2}";
                per.Libelle = p.AutoLibelles ? $"{m:D2}/{p.Annee}" : per.Libelle;

                per.Statut = Domain.DomainEnums.PeriodePaieStatut.Brouillon;
            }

            os.CommitChanges();
            Application.ShowViewStrategy.ShowMessage("Année créée / complétée avec succès.", InformationType.Success, 3000, InformationPosition.Bottom);

            // Fermer le popup automatiquement (XAF le fait après Execute). Rafraîchir la liste :
            View?.ObjectSpace?.Refresh();
        }
    }
}
