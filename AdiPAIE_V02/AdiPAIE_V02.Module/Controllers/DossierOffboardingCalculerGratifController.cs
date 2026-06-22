// =============================================================================
//  DossierOffboardingCalculerGratifController.cs — V1.7.2b-bis (#71)
//
//  Action sur le DetailView du DossierOffboarding :
//  "Calculer gratifications prorata STC".
//
//  Logique métier ELTON (cf. clarification user 2026-05-11) :
//    - Le DG décide périodiquement de gratifications sur résultats N-1
//    - RH saisit + DAF valide (statut ValideeDAF)
//    - Si le salarié quitte l'entreprise AVANT l'intégration au bulletin :
//        → Ses gratifications validées sont proratisées sur les mois de présence
//        → Versées sur le STC
//
//  Formule :  Σ Gratification.MontantCalcule (statut ValideeDAF) × MoisPresence / 12
//
//  Effet de bord : les gratifications concernées sont marquées comme
//  IntegreeBulletin avec un commentaire indiquant l'intégration au STC.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class DossierOffboardingCalculerGratifController
        : ObjectViewController<DetailView, DossierOffboarding>
    {
        readonly SimpleAction calculerGratifAction;

        public DossierOffboardingCalculerGratifController()
        {
            calculerGratifAction = new SimpleAction(this,
                "Offboarding_CalculerGratifications", PredefinedCategory.Edit)
            {
                Caption = "Calculer gratifications prorata STC",
                ImageName = "BO_Money",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Prorate les gratifications validées DAF non encore " +
                          "intégrées du salarié et les ajoute au STC. " +
                          "Action idempotente.",
                ConfirmationMessage =
                    "Cette action va proratiser les gratifications validées DAF " +
                    "(non encore intégrées) du salarié et les intégrer au STC. " +
                    "Les gratifications passeront en statut IntegreeBulletin. " +
                    "Continuer ?"
            };
            calculerGratifAction.Execute += CalculerGratifAction_Execute;
        }

        void CalculerGratifAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var dossier = e.CurrentObject as DossierOffboarding;
                if (dossier == null) return;

                if (dossier.Salarie == null)
                {
                    ShowError("Le dossier n'a pas de salarié renseigné.");
                    return;
                }

                if (dossier.DateSortie == default)
                {
                    ShowError("La date de sortie doit être renseignée avant le calcul.");
                    return;
                }

                if (dossier.Statut == OffboardingStatut.Cloture)
                {
                    ShowError("Le dossier est clôturé. Recalcul impossible.");
                    return;
                }

                int annee = dossier.DateSortie.Year;
                var session = ((DevExpress.ExpressApp.Xpo.XPObjectSpace)ObjectSpace).Session;

                // ─── Mois de présence (= nb bulletins de l'année) ───
                var moisPresence = BrutRecurrentService.GetMoisPresence(
                    session, dossier.Salarie, annee);

                if (moisPresence == 0)
                {
                    ShowError(
                        $"Aucun bulletin n'existe pour {dossier.Salarie.Matricule} " +
                        $"en {annee}. Impossible de proratiser les gratifications.");
                    return;
                }

                // ─── Lister les gratifications validées DAF non intégrées ──
                var gratifsAValiderProrata = ObjectSpace.GetObjects<Gratification>()
                    .Cast<Gratification>()
                    .Where(g => g.Salarie != null
                                && g.Salarie.Oid == dossier.Salarie.Oid
                                && g.Annee == annee
                                && g.Statut == GratificationStatut.ValideeDAF)
                    .ToList();

                if (gratifsAValiderProrata.Count == 0)
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        $"Aucune gratification validée DAF (non intégrée) trouvée " +
                        $"pour {dossier.Salarie.Matricule} en {annee}. " +
                        $"Indemnité gratifications STC mise à 0.",
                        InformationType.Info, 5000, InformationPosition.Top);
                    dossier.IndemniteGratifications = 0m;
                    ObjectSpace.CommitChanges();
                    View.Refresh();
                    return;
                }

                // ─── Calcul prorata pour chaque gratification ───────
                // Formule : Σ MontantCalcule × MoisPresence / 12
                decimal totalProrata = 0m;
                var detailLignes = new List<string>();

                foreach (var g in gratifsAValiderProrata)
                {
                    var prorata = Math.Round(
                        g.MontantCalcule * moisPresence / 12m,
                        0, MidpointRounding.AwayFromZero);
                    totalProrata += prorata;
                    detailLignes.Add(
                        $"{g.MoisPaiement:00}/{g.Annee} : " +
                        $"{g.MontantCalcule:N0} × {moisPresence}/12 = {prorata:N0} FCFA");

                    // Marquer la gratification comme intégrée au STC
                    g.Statut = GratificationStatut.IntegreeBulletin;
                    g.DateIntegration = DateTime.Now;
                    try { g.IntegrePar = SecuritySystem.CurrentUserName; } catch { }
                    var note = $"[STC {DateTime.Now:yyyy-MM-dd}] Intégrée au solde " +
                               $"de tout compte du dossier offboarding " +
                               $"(prorata {moisPresence}/12 = {prorata:N0} FCFA).";
                    g.Commentaire = string.IsNullOrWhiteSpace(g.Commentaire)
                        ? note
                        : $"{note}\r\n----\r\n{g.Commentaire}";
                }

                dossier.IndemniteGratifications = totalProrata;
                ObjectSpace.CommitChanges();

                var rapport = $"✅ {gratifsAValiderProrata.Count} gratification(s) " +
                              $"proratisée(s) → {totalProrata:N0} FCFA dans le STC.\n";
                foreach (var ligne in detailLignes)
                    rapport += $"  • {ligne}\n";

                Application.ShowViewStrategy?.ShowMessage(
                    rapport,
                    InformationType.Success, 10000, InformationPosition.Top);

                View.Refresh();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        void ShowError(string message)
        {
            Application.ShowViewStrategy?.ShowMessage(
                $"❌ {message}",
                InformationType.Error, 8000, InformationPosition.Top);
        }
    }
}
