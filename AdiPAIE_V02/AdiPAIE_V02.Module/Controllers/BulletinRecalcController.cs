// =============================================================================
//  BulletinRecalcController.cs — V1.8 (juin 2026)
//
//  Expose 2 actions distinctes sur le DetailView d'un Bulletin :
//
//   1. "Recharger bulletin"         (ex-"Recalculer", action lourde)
//      → b.RecalculerDepuisParametrage()
//      → Recalcule SB, LOGT, ANC, SURSAL, TRANSPORT depuis le profil
//        salarié + toutes les cotisations.
//      → À utiliser quand on veut RÉINITIALISER le bulletin (cas standard
//        d'un bulletin mensuel "normal").
//
//   2. "Recalculer cotisations"     (nouvelle action V1.8)
//      → b.RecalculerSurGrilleExistante()
//      → Recalcule UNIQUEMENT les cotisations (IPRES/CSS/IR/TRIMF/CFCE)
//        et les totaux. NE TOUCHE PAS aux rubriques de gain existantes.
//      → À utiliser quand on a personnalisé manuellement le bulletin
//        (bulletin de congé + avantage nature ajouté à la main, par ex.)
//        et qu'on veut juste actualiser les cotisations sans tout casser.
//
//  AVANT V1.8 : un seul bouton "Recalculer" qui appelait toujours la
//  méthode "lourde", ce qui ramenait les rubriques de salaire normal
//  sur les bulletins de congé personnalisés. Le RH a remonté le ticket.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.Controllers
{
    public partial class BulletinRecalcController : ObjectViewController<ObjectView, Bulletin>
    {
        readonly SimpleAction rechargerAction;
        readonly SimpleAction recalcCotisationsAction;

        public BulletinRecalcController()
        {
            InitializeComponent(); // Designer.cs existe encore — garder

            // ─── Action 1 : Recharger bulletin (action lourde) ──────────
            rechargerAction = new SimpleAction(this,
                "Bulletin_RecalculerMaintenant",  // ID conservé pour compat XAFML existant
                PredefinedCategory.Edit)
            {
                Caption = "Recharger bulletin",
                ImageName = "btn_recalculer",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Réinitialise le bulletin depuis le profil salarié : " +
                          "rajoute les rubriques de salaire (SB, Sursalaire, " +
                          "Indemnité logement, Prime transport, Ancienneté) puis " +
                          "recalcule toutes les cotisations. " +
                          "⚠ ATTENTION : écrase les personnalisations manuelles " +
                          "sur les rubriques de gain. Pour un bulletin de congé " +
                          "personnalisé, utiliser plutôt « Recalculer cotisations ».",
                ConfirmationMessage = "Ce bouton va RÉINITIALISER le bulletin " +
                                      "depuis le profil salarié.\n\n" +
                                      "Les rubriques de salaire (SB, Sursalaire, " +
                                      "Indemnité logement, Prime transport) seront " +
                                      "RAJOUTÉES si absentes ou réinitialisées.\n\n" +
                                      "Pour un bulletin de congé personnalisé, " +
                                      "utilisez plutôt « Recalculer cotisations ».\n\n" +
                                      "Continuer ?",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            rechargerAction.Execute += RechargerAction_Execute;

            // ─── Action 2 : Recalculer cotisations (action légère) ──────
            recalcCotisationsAction = new SimpleAction(this,
                "Bulletin_RecalculerCotisations",
                PredefinedCategory.Edit)
            {
                Caption = "Recalculer cotisations",
                ImageName = "Action_Refresh",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Recalcule UNIQUEMENT les cotisations sociales " +
                          "(IPRES, CSS) et fiscales (CFCE, TRIMF, IR) ainsi " +
                          "que les totaux du bulletin. Les rubriques de gain " +
                          "existantes (SB, Sursalaire, lignes manuelles) NE " +
                          "SONT PAS modifiées. " +
                          "Idéal pour les bulletins personnalisés (bulletin de " +
                          "congé + avantage nature ajouté manuellement, etc.).",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            recalcCotisationsAction.Execute += RecalcCotisationsAction_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            // Visibles uniquement en DetailView
            bool isDetail = View is DetailView;
            rechargerAction.Active["DetailViewOnly"] = isDetail;
            recalcCotisationsAction.Active["DetailViewOnly"] = isDetail;
        }

        // ─── Recharger bulletin (méthode lourde) ────────────────────────
        private void RechargerAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var b = View.CurrentObject as Bulletin;
            if (b == null) return;

            try
            {
                // Recalcul complet depuis le paramétrage salarié :
                // recalcule SB, LOGT, ANC, SURSAL depuis le profil, puis toutes les cotisations.
                b.RecalculerDepuisParametrage();
                ObjectSpace.CommitChanges();
                View.ObjectSpace.Refresh();
                Application.ShowViewStrategy.ShowMessage(
                    "✅ Bulletin rechargé depuis le profil salarié.",
                    InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (DevExpress.Xpo.DB.Exceptions.LockingException)
            {
                Application.ShowViewStrategy.ShowMessage(
                    "Ce bulletin a été modifié par un autre utilisateur. " +
                    "Veuillez rafraîchir la vue (F5) et réessayer.",
                    InformationType.Warning, 6000, InformationPosition.Top);
                View.ObjectSpace.Refresh();
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur rechargement : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }

        // ─── Recalculer cotisations (méthode légère) — V1.8 ─────────────
        private void RecalcCotisationsAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var b = View.CurrentObject as Bulletin;
            if (b == null) return;

            try
            {
                // Recalcul "léger" : ne touche pas aux rubriques de gain existantes,
                // recalcule juste IPRES/CSS/IR/TRIMF/CFCE et les totaux.
                b.RecalculerSurGrilleExistante();
                ObjectSpace.CommitChanges();
                View.ObjectSpace.Refresh();
                Application.ShowViewStrategy.ShowMessage(
                    "✅ Cotisations recalculées sur la grille existante " +
                    "(rubriques de gain préservées).",
                    InformationType.Success, 4000, InformationPosition.Top);
            }
            catch (DevExpress.Xpo.DB.Exceptions.LockingException)
            {
                Application.ShowViewStrategy.ShowMessage(
                    "Ce bulletin a été modifié par un autre utilisateur. " +
                    "Veuillez rafraîchir la vue (F5) et réessayer.",
                    InformationType.Warning, 6000, InformationPosition.Top);
                View.ObjectSpace.Refresh();
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur recalcul cotisations : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }

        protected override void OnDeactivated() => base.OnDeactivated();
    }
}
