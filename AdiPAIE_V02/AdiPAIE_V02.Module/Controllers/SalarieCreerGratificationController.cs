// =============================================================================
//  SalarieCreerGratificationController.cs — V1.7.2d
//
//  Action sur la ListView des salariés actifs : "Nouvelle gratification".
//
//  Pattern utilisateur cible (cf. UX bulletins de paie ELTON) :
//    1. RH ouvre la liste des salariés
//    2. Sélectionne 1 salarié
//    3. Clique "Nouvelle gratification"
//    4. Popup confirme : "Période ouverte : MM/YYYY. Créer la gratification ?"
//    5. Si confirmation → nouveau DetailView Gratification pré-rempli avec :
//       - Salarie = sélectionné
//       - Annee + MoisPaiement = période ouverte courante
//       - BaseCalcul / Multiplicateur restent à saisir
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Variante ListView (sur la liste des salariés actifs).
    /// </summary>
    public sealed class SalarieCreerGratificationController
        : ObjectViewController<ListView, Salarie>
    {
        readonly SimpleAction creerGratifAction;

        public SalarieCreerGratificationController()
        {
            // V1.7.2 — Catégorie Edit (toolbar) au lieu de RecordEdit
            // (qui rend aussi en row-link redondant avec la toolbar).
            creerGratifAction = new SimpleAction(this,
                "Salarie_CreerGratification", PredefinedCategory.Edit)
            {
                Caption = "Nouvelle gratification",
                ImageName = "BO_Money",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Crée une gratification ad hoc pour ce salarié, " +
                          "automatiquement positionnée sur la période ouverte.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            creerGratifAction.Execute += CreerGratifAction_Execute;
        }

        void CreerGratifAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var salarie = e.CurrentObject as Salarie;
                if (salarie == null) return;

                // ─── Vérifier qu'une période est ouverte ──────────────
                var session = ((DevExpress.ExpressApp.Xpo.XPObjectSpace)ObjectSpace).Session;
                var periodeOuverte = PeriodePaieHelper.GetPeriodeOuverte(session);

                if (periodeOuverte == null)
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        "Aucune période de paie n'est actuellement OUVERTE. " +
                        "Ouvrez la période du mois en cours avant de créer une gratification.",
                        InformationType.Warning, 8000, InformationPosition.Top);
                    return;
                }

                // ─── Créer la gratification dans un nouvel ObjectSpace ──
                // (pour ouvrir un DetailView modifiable sans toucher au ListView)
                var detailOs = Application.CreateObjectSpace(typeof(Gratification));

                // Récupérer le salarié dans le nouvel OS
                var salarieInDetailOs = detailOs.GetObject(salarie);

                // Créer la gratification
                var gratif = detailOs.CreateObject<Gratification>();
                gratif.Salarie = salarieInDetailOs;
                gratif.Annee = periodeOuverte.Annee;
                gratif.MoisPaiement = periodeOuverte.Mois;
                // AfterConstruction a déjà initialisé Statut=BrouillonRH,
                // BaseCalcul=BrutRecurrent, Multiplicateur=1m, DateCreation, CreePar.

                // ─── Afficher confirmation + ouvrir le DetailView ──────
                Application.ShowViewStrategy?.ShowMessage(
                    $"📅 Période ouverte : {periodeOuverte.Mois:00}/{periodeOuverte.Annee}. " +
                    $"Renseignez la base de calcul + multiplicateur, " +
                    $"puis cliquez « Soumettre au DAF ».",
                    InformationType.Info, 6000, InformationPosition.Top);

                e.ShowViewParameters.CreatedView =
                    Application.CreateDetailView(detailOs, gratif);
                e.ShowViewParameters.TargetWindow = TargetWindow.Default;
                e.ShowViewParameters.Context = TemplateContext.View;
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"❌ Erreur création gratification : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }
    }
}
