// =============================================================================
//  RechargerReferentielController.cs — V1.7.2
//
//  Action sur le DetailView de ParametresPaie : "Recharger le référentiel".
//
//  Pourquoi cette action existe
//  ----------------------------
//  Le seed du référentiel paie (conventions, catégories, échelons, rubriques,
//  barèmes) est normalement exécuté par l'Updater XAF dans
//  UpdateDatabaseAfterUpdateSchema(). Mais l'Updater ne tourne que sur
//  DatabaseVersionMismatch (changement de version du module).
//
//  Une fois la base initialisée et la version stockée, cocher
//  "ActiverSeedReferentiel" après coup N'A PAS d'effet visible tant qu'on
//  ne redémarre pas ET qu'on ne provoque pas de mismatch.
//
//  Cette action permet à RH/DAF de forcer le rechargement à la demande,
//  sans toucher à ModuleInfo ni redémarrer l'app pool. Idempotent.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class RechargerReferentielController
        : ObjectViewController<DetailView, ParametresPaie>
    {
        readonly SimpleAction rechargerAction;

        public RechargerReferentielController()
        {
            rechargerAction = new SimpleAction(this,
                "Param_RechargerReferentiel", PredefinedCategory.Edit)
            {
                Caption = "Recharger le référentiel paie",
                ImageName = "Action_RefreshFile",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Force le rechargement complet du référentiel ELTON : " +
                          "conventions, catégories, échelons, plan comptable, " +
                          "groupes, types, rubriques, barèmes IR + TRIMF. " +
                          "Idempotent : ne crée que ce qui manque.",
                ConfirmationMessage =
                    "Cette action va recharger TOUT le référentiel paie ELTON.\n\n" +
                    "Ce qui est créé / mis à jour :\n" +
                    "  • Conventions, Catégories, Échelons (grille salariale)\n" +
                    "  • Plan comptable\n" +
                    "  • Groupes d'impression, Types de rubrique\n" +
                    "  • Rubriques (SB, IPRES, CSS, TRIMF, IR, etc.)\n" +
                    "  • Barèmes IR et TRIMF\n\n" +
                    "Idempotent : les rubriques existantes ne sont pas dupliquées.\n\n" +
                    "Continuer ?"
            };
            rechargerAction.Execute += RechargerAction_Execute;
        }

        void RechargerAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var p = e.CurrentObject as ParametresPaie;
                if (p == null)
                {
                    ShowError("Paramètres de paie introuvables.");
                    return;
                }

                // Compteurs avant pour mesurer l'impact
                int rubAvant = ObjectSpace.GetObjects<Rubrique>().Count;
                int echAvant = ObjectSpace.GetObjects<Echelons>().Count;
                int convAvant = ObjectSpace.GetObjects<Convention>().Count;
                int baremesTrimfAvant = ObjectSpace.GetObjects<BaremeTRIMF>().Count;
                int trimfTranchesAvant = ObjectSpace.GetObjects<BaremeTRIMFTranche>().Count;
                int baremesIRAvant = ObjectSpace.GetObjects<BaremeIR>().Count;
                int irTranchesAvant = ObjectSpace.GetObjects<BaremeIRTranche>().Count;
                int reducFamAvant = ObjectSpace.GetObjects<IRReductionFamille>().Count;

                // ─── Lancement du seed via SeedService ────────────
                // V1.7.2 — Seed COMPLET aligné Updater :
                // - Référentiels (Conv, Cat, Échelons, Comptes, Groupes, Types)
                // - Rubriques (24)
                // - Barèmes TRIMF Mensuel + Annuel + IR DPP
                int annee = DateTime.Today.Year;
                SeedService.Run(ObjectSpace, new SeedOptions
                {
                    Echelons = true,
                    GroupesTypes = true,
                    Comptes = true,
                    Rubriques = true,
                    Baremes = true,           // 🆕 V1.7.2
                    AnneeBaremes = annee,     // 🆕 année courante
                    JeuDemo = false           // pas de salariés démo
                });

                ObjectSpace.CommitChanges();

                // Compteurs après
                int rubApres = ObjectSpace.GetObjects<Rubrique>().Count;
                int echApres = ObjectSpace.GetObjects<Echelons>().Count;
                int convApres = ObjectSpace.GetObjects<Convention>().Count;
                int baremesTrimfApres = ObjectSpace.GetObjects<BaremeTRIMF>().Count;
                int trimfTranchesApres = ObjectSpace.GetObjects<BaremeTRIMFTranche>().Count;
                int baremesIRApres = ObjectSpace.GetObjects<BaremeIR>().Count;
                int irTranchesApres = ObjectSpace.GetObjects<BaremeIRTranche>().Count;
                int reducFamApres = ObjectSpace.GetObjects<IRReductionFamille>().Count;

                Application.ShowViewStrategy?.ShowMessage(
                    $"✅ Référentiel paie rechargé pour {annee} :\n" +
                    $"  • Rubriques : {rubAvant} → {rubApres} " +
                    $"({(rubApres - rubAvant)} créées)\n" +
                    $"  • Échelons : {echAvant} → {echApres} " +
                    $"({(echApres - echAvant)} créés)\n" +
                    $"  • Conventions : {convAvant} → {convApres} " +
                    $"({(convApres - convAvant)} créées)\n" +
                    $"  • Barèmes TRIMF : {baremesTrimfAvant} → {baremesTrimfApres} " +
                    $"({trimfTranchesApres - trimfTranchesAvant} tranches créées)\n" +
                    $"  • Barème IR : {baremesIRAvant} → {baremesIRApres} " +
                    $"({irTranchesApres - irTranchesAvant} tranches créées)\n" +
                    $"  • Réduction familiale IR : {reducFamAvant} → {reducFamApres} " +
                    $"({reducFamApres - reducFamAvant} lignes créées)",
                    InformationType.Success, 12000, InformationPosition.Top);

                View.Refresh();
            }
            catch (Exception ex)
            {
                ShowError($"Erreur rechargement référentiel : {ex.Message}");
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
