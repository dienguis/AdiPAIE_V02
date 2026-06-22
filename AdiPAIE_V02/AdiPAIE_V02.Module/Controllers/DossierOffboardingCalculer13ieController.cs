// =============================================================================
//  DossierOffboardingCalculer13ieController.cs — V1.7.2b-bis (#71)
//
//  Action sur le DetailView du DossierOffboarding : "Calculer 13ième prorata STC".
//
//  Workflow ELTON :
//    1. RH initie le dossier de départ d'un salarié sortant en cours d'année
//    2. RH renseigne DateSortie, IndemniteCongés, IndemnitePreavis, etc.
//    3. RH clique "Calculer 13ième prorata STC"
//       → Le service détermine le BR récurrent à la date de sortie
//       → Calcule MoisPresence et le prorata
//       → Renseigne automatiquement Indemnite13iemeMois sur le dossier
//       → Crée un TreiziemeMois (EstSurSTC=true) pour traçabilité
//    4. Le TOTAL SOLDE DE TOUT COMPTE inclut désormais le 13ième prorata
//
//  Le calcul reste annexé au DossierOffboarding (UI principale du STC).
//  Le TreiziemeMois créé est marqué EstSurSTC=true pour ne pas être repris
//  dans le calcul batch de décembre.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class DossierOffboardingCalculer13ieController
        : ObjectViewController<DetailView, DossierOffboarding>
    {
        readonly SimpleAction calculer13Action;

        public DossierOffboardingCalculer13ieController()
        {
            calculer13Action = new SimpleAction(this,
                "Offboarding_Calculer13ieme", PredefinedCategory.Edit)
            {
                Caption = "Calculer 13ième prorata STC",
                ImageName = "BO_Money",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Calcule le 13ième mois prorata pour ce salarié sortant " +
                          "(formule : BR récurrent × Mois de présence ÷ 12) et l'inscrit " +
                          "dans Indemnite13iemeMois. Action idempotente.",
                ConfirmationMessage =
                    "Cette action va calculer le 13ième prorata du salarié " +
                    "et l'ajouter au solde de tout compte. Continuer ?"
            };
            calculer13Action.Execute += Calculer13Action_Execute;
        }

        void Calculer13Action_Execute(object sender, SimpleActionExecuteEventArgs e)
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

                // Statut bloquant : on ne recalcule pas un dossier déjà clôturé
                if (dossier.Statut == OffboardingStatut.Cloture)
                {
                    ShowError("Le dossier est clôturé. Recalcul impossible.");
                    return;
                }

                int annee = dossier.DateSortie.Year;
                int moisReference = dossier.DateSortie.Month;
                var session = ((DevExpress.ExpressApp.Xpo.XPObjectSpace)ObjectSpace).Session;

                // ─── Calcul du BR + MoisPresence ─────────────────────
                // Pour un départ au 30 juin 2026 :
                //   - BR = dernier bulletin avant juin (donc mai)
                //   - MoisPresence = nombre de bulletins de l'année (janv→juin = 6)
                //   - Prorata = BR × 6 / 12
                var br = BrutRecurrentService.GetDernierBrutRecurrent(
                    session, dossier.Salarie, annee, moisReference);

                var moisPresence = BrutRecurrentService.GetMoisPresence(
                    session, dossier.Salarie, annee);

                if (moisPresence == 0)
                {
                    ShowError(
                        $"Aucun bulletin n'existe pour {dossier.Salarie.Matricule} " +
                        $"en {annee}. Impossible de calculer un prorata.");
                    return;
                }

                var montantProrata = Math.Round(
                    br * moisPresence / 12m,
                    0, MidpointRounding.AwayFromZero);

                // ─── Renseigner le dossier ───────────────────────────
                dossier.Indemnite13iemeMois = montantProrata;

                // ─── Créer/mettre à jour le TreiziemeMois (traçabilité) ─
                // Idempotent : un seul (Salarie, Annee) en base.
                var existant = ObjectSpace.FirstOrDefault<TreiziemeMois>(
                    m => m.Salarie.Oid == dossier.Salarie.Oid && m.Annee == annee);

                var m13 = existant ?? ObjectSpace.CreateObject<TreiziemeMois>();
                m13.Annee = annee;
                m13.Salarie = ObjectSpace.GetObject(dossier.Salarie);
                m13.BrutRecurrentReference = br;
                m13.MoisPresence = moisPresence;
                m13.MontantBrut = montantProrata;
                m13.EstSurSTC = true;
                m13.Statut = TreiziemeMoisStatut.Calcule;
                m13.DateCalcul = DateTime.Now;
                try { m13.CalculePar = SecuritySystem.CurrentUserName; } catch { }
                m13.Commentaire =
                    $"Prorata STC calculé depuis dossier offboarding " +
                    $"(départ {dossier.DateSortie:dd/MM/yyyy}).";

                ObjectSpace.CommitChanges();

                Application.ShowViewStrategy?.ShowMessage(
                    $"✅ 13ième prorata calculé : {montantProrata:N0} FCFA " +
                    $"(BR {br:N0} × {moisPresence}/12). " +
                    $"Inscrit dans le STC + TreiziemeMois créé pour traçabilité.",
                    InformationType.Success, 6000, InformationPosition.Top);

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
