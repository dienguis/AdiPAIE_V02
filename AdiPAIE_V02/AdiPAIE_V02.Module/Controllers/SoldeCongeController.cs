using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    // ════════════════════════════════════════════════════════════
    // CONTROLLER SOLDE — Actions RH sur la fiche SoldeConge
    // ════════════════════════════════════════════════════════════
    public class SoldeCongeController
        : ObjectViewController<DetailView, SoldeConge>
    {
        readonly ParametrizedAction ajusterAction;
        readonly SimpleAction acquirirMoisAction;

        public SoldeCongeController()
        {
            // ── Ajustement manuel (ParametrizedAction) ───────
            ajusterAction = new ParametrizedAction(this,
                "SoldeConge_Ajuster", PredefinedCategory.Edit, typeof(string))
            {
                Caption = "Ajustement manuel",
                ImageName = "Action_Refresh",
                NullValuePrompt = "Ex : +5 ou -3 (puis Entrée)",
                ToolTip = "Saisissez la quantité (+5 pour créditer, -3 pour débiter) et appuyez sur Entrée."
            };
            ajusterAction.Execute += AjusterAction_Execute;

            // ── Acquérir mois courant ─────────────────────────
            acquirirMoisAction = new SimpleAction(this,
                "SoldeConge_AcquerirMois", PredefinedCategory.Edit)
            {
                Caption = "Acquérir mois courant",
                ImageName = "Action_Forward",
                ToolTip = "Crédite les jours acquis pour le mois en cours.",
                ConfirmationMessage = "Créditer les jours acquis pour ce mois ?"
            };
            acquirirMoisAction.Execute += (s, e) =>
            {
                var solde = (SoldeConge)View.CurrentObject;
                var now = DateTime.Now;
                var result = SoldeCongeCalculService.AcquerirMensuel(
                    ObjectSpace, solde.Salarie, solde.TypeConge,
                    now.Year, now.Month);
                ObjectSpace.CommitChanges();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    result.Message,
                    result.Succes ? InformationType.Success : InformationType.Warning,
                    4000, InformationPosition.Top);
            };
        }

        // ── Handler ajustement ───────────────────────────────
        void AjusterAction_Execute(object sender, ParametrizedActionExecuteEventArgs e)
        {
            var solde = View?.CurrentObject as SoldeConge;
            if (solde == null) return;

            var input = e.ParameterCurrentValue?.ToString()?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                Application.ShowViewStrategy?.ShowMessage(
                    "Saisissez une quantité (+5 pour créditer, -3 pour débiter).",
                    InformationType.Warning, 3000, InformationPosition.Top);
                return;
            }

            // Accepte "+5", "-3", "5", "2,5", "2.5"
            var normalized = input.Replace("+", "").Replace(",", ".");
            if (!decimal.TryParse(normalized,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal quantite) || quantite == 0)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Valeur invalide : '{input}'. Exemples : +5, -3, 2.5",
                    InformationType.Error, 4000, InformationPosition.Top);
                return;
            }

            var soldeAvant = solde.SoldeDisponible;

            MouvementSolde.Creer(ObjectSpace, solde,
                MouvementSoldeType.AjustementManuel,
                quantite,
                commentaire: $"Ajustement manuel par {SecuritySystem.CurrentUserName}");

            ObjectSpace.CommitChanges();
            View.Refresh();

            var sens = quantite > 0 ? "crédité" : "débité";
            var msg = $"Solde {sens} de {Math.Abs(quantite):N2}j — Avant: {soldeAvant:N2}j | Apres: {solde.SoldeDisponible:N2}j";
            Application.ShowViewStrategy?.ShowMessage(
                msg, InformationType.Success, 5000, InformationPosition.Top);
        }
    }

    // ════════════════════════════════════════════════════════════
    // CONTROLLER BATCH — Acquisition mensuelle tous salariés
    // Sur une vue liste fictive ou depuis un écran de lancement
    // ════════════════════════════════════════════════════════════
    public class AcquisitionMensuelleController
        : ObjectViewController<ListView, SoldeConge>
    {
        readonly SimpleAction lancerBatchAction;
        readonly SimpleAction reporterExerciceAction;

        public AcquisitionMensuelleController()
        {
            lancerBatchAction = new SimpleAction(this,
                "Batch_AcquisitionMensuelle", PredefinedCategory.Edit)
            {
                Caption = "Lancer acquisition mensuelle",
                ImageName = "Action_RunDiagram",
                ConfirmationMessage =
                    "Créditer les jours acquis du mois en cours pour TOUS les salariés actifs ?"
            };
            lancerBatchAction.Execute += (s, e) =>
            {
                var now = DateTime.Now;
                var result = SoldeCongeCalculService.AcquerirTousSalaries(
                    ObjectSpace, now.Year, now.Month);
                View.Refresh();

                var msg = $"Acquisition {now:MM/yyyy} terminée — {result}";
                Application.ShowViewStrategy?.ShowMessage(
                    msg,
                    result.HasErrors ? InformationType.Warning : InformationType.Success,
                    5000, InformationPosition.Top);
            };

            reporterExerciceAction = new SimpleAction(this,
                "Batch_ReporterExercice", PredefinedCategory.Edit)
            {
                Caption = "Reporter exercice N → N+1",
                ImageName = "Action_Close",
                ConfirmationMessage =
                    "Reporter les soldes de l'année précédente vers cette année (dans la limite des plafonds) ?"
            };
            reporterExerciceAction.Execute += (s, e) =>
            {
                var anneeSource = DateTime.Today.Year - 1;
                var result = SoldeCongeCalculService.Reporter(
                    ObjectSpace, anneeSource);
                View.Refresh();

                var msg = $"Report {anneeSource}→{anneeSource + 1} terminé — {result}";
                Application.ShowViewStrategy?.ShowMessage(
                    msg,
                    result.HasErrors ? InformationType.Warning : InformationType.Success,
                    5000, InformationPosition.Top);
            };
        }
    }
}
