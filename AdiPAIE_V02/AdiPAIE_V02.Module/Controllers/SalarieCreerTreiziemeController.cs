// =============================================================================
//  SalarieCreerTreiziemeController.cs — V1.7.2b
//
//  Action sur la ListView des salariés : "Nouveau 13ième mois".
//
//  Pattern symétrique à SalarieCreerGratificationController :
//    1. RH ouvre la liste des salariés
//    2. Sélectionne 1 salarié
//    3. Clique "Nouveau 13ième mois"
//    4. Le service calcule automatiquement le prorata
//    5. DetailView du résultat ouvert (read-only sauf statut)
//
//  Pour les calculs en MASSE (tous les salariés en décembre), utiliser plutôt
//  l'action "Calculer 13ième mois" depuis la ListView TreiziemeMois.
//
//  Pré-requis :
//    - Période de paie (annee, 12) doit être Ouverte
//    - Le salarié doit avoir au moins un bulletin dans l'année
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class SalarieCreerTreiziemeController
        : ObjectViewController<ListView, Salarie>
    {
        readonly SimpleAction creerTreiziemeAction;

        public SalarieCreerTreiziemeController()
        {
            // V1.7.2 — Catégorie Edit (toolbar) au lieu de RecordEdit
            // (qui rend aussi en row-link redondant avec la toolbar).
            creerTreiziemeAction = new SimpleAction(this,
                "Salarie_CreerTreizieme", PredefinedCategory.Edit)
            {
                Caption = "Nouveau 13ième mois",
                ImageName = "BO_Money",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Calcule et crée le 13ième mois prorata pour ce salarié, " +
                          "sur la période de décembre actuellement ouverte. " +
                          "Pour le calcul en masse de tous les salariés, " +
                          "utiliser la ListView 13ièmes mois → Calculer.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            creerTreiziemeAction.Execute += CreerTreiziemeAction_Execute;
        }

        void CreerTreiziemeAction_Execute(object sender, SimpleActionExecuteEventArgs e)
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
                        "Aucune période de paie n'est OUVERTE. " +
                        "Ouvrez la période avant de calculer le 13ième mois.",
                        InformationType.Warning, 8000, InformationPosition.Top);
                    return;
                }

                // ─── Le 13ième se verse sur décembre uniquement (cas normal).
                // Si la période courante n'est pas décembre, on prévient.
                int annee = periodeOuverte.Annee;
                var periodeDecembre = session.Query<PeriodePaie>()
                    .FirstOrDefault(p => p.Annee == annee && p.Mois == 12);

                if (periodeDecembre == null)
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        $"Aucune période de paie pour décembre {annee}. " +
                        $"Créez et ouvrez la période avant le calcul.",
                        InformationType.Warning, 8000, InformationPosition.Top);
                    return;
                }

                if (periodeDecembre.Statut != PeriodePaieStatut.Ouverte)
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        $"La période de décembre {annee} doit être OUVERTE " +
                        $"(statut actuel : {periodeDecembre.Statut}). " +
                        $"Ouvrez la période avant le calcul.",
                        InformationType.Warning, 8000, InformationPosition.Top);
                    return;
                }

                // ─── Vérifier que le salarié a des bulletins dans l'année ──
                var nbBulletins = session.Query<Bulletin>()
                    .Count(b => b.Salarie.Oid == salarie.Oid && b.Annee == annee);

                if (nbBulletins == 0)
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        $"Aucun bulletin n'existe pour {salarie.Matricule} – " +
                        $"{salarie.FullName} en {annee}. " +
                        $"Impossible de calculer un 13ième mois (prorata = 0).",
                        InformationType.Warning, 8000, InformationPosition.Top);
                    return;
                }

                // ─── Vérifier qu'un 13ième n'existe pas déjà pour ce salarié ──
                var existant = ObjectSpace.FirstOrDefault<TreiziemeMois>(
                    m => m.Salarie.Oid == salarie.Oid && m.Annee == annee);

                if (existant != null)
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        $"Un 13ième mois existe déjà pour {salarie.Matricule} – " +
                        $"{salarie.FullName} en {annee} " +
                        $"(statut : {existant.Statut}). " +
                        $"Utilisez la ListView 13ièmes mois pour le consulter.",
                        InformationType.Warning, 8000, InformationPosition.Top);
                    return;
                }

                // ─── Créer le calcul via le service (réutilise la logique batch) ──
                var detailOs = Application.CreateObjectSpace(typeof(TreiziemeMois));
                var salarieInDetailOs = detailOs.GetObject(salarie);
                var detailSession = ((DevExpress.ExpressApp.Xpo.XPObjectSpace)detailOs).Session;

                var m13 = detailOs.CreateObject<TreiziemeMois>();
                m13.Salarie = salarieInDetailOs;
                m13.Annee = annee;
                m13.EstSurSTC = false;

                // Calcul auto via le service
                m13.BrutRecurrentReference = BrutRecurrentService
                    .GetDernierBrutRecurrent(detailSession, salarieInDetailOs, annee, 12);
                m13.MoisPresence = BrutRecurrentService
                    .GetMoisPresence(detailSession, salarieInDetailOs, annee);
                m13.MontantBrut = Math.Round(
                    m13.BrutRecurrentReference * m13.MoisPresence / 12m,
                    0, MidpointRounding.AwayFromZero);
                m13.Statut = TreiziemeMoisStatut.Calcule;

                // ─── Confirmation + ouvrir DetailView ───────────────────
                Application.ShowViewStrategy?.ShowMessage(
                    $"✅ 13ième mois calculé : {m13.MontantBrut:N0} FCFA " +
                    $"(BR {m13.BrutRecurrentReference:N0} × {m13.MoisPresence}/12). " +
                    $"Statut : Calculé. Reste à intégrer au bulletin de décembre.",
                    InformationType.Success, 6000, InformationPosition.Top);

                e.ShowViewParameters.CreatedView =
                    Application.CreateDetailView(detailOs, m13);
                e.ShowViewParameters.TargetWindow = TargetWindow.Default;
                e.ShowViewParameters.Context = TemplateContext.View;
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"❌ Erreur création 13ième mois : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }
    }
}
