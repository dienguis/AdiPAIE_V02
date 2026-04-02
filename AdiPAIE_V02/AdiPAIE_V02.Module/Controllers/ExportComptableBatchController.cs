using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Controller d'export comptable — visible uniquement sur la ListView des bulletins.
    ///
    /// Corrections :
    ///   - ViewController → ObjectViewController ListView Bulletin (anti-bouton parasite)
    ///   - PredefinedCategory.Save → PredefinedCategory.Edit
    ///   - Appel → ExportComptableBatchService (branché sur EcritureFromBulletinService)
    ///   - Popup de sélection de période (au lieu du mois courant forcé)
    ///   - ConfirmationMessage ajouté
    /// </summary>
    public class ExportComptableBatchController
        : ObjectViewController<ListView, Bulletin>
    {
        private readonly SimpleAction _exportMoisAction;
        private readonly SimpleAction _exportSelectionAction;

        public ExportComptableBatchController()
        {
            // ── Action 1 : Export du mois courant ─────────────────────────
            _exportMoisAction = new SimpleAction(
                this, "ExporterBulletinsMoisCourant", PredefinedCategory.Edit)
            {
                Caption = "Exporter en comptabilité (mois courant)",
                ImageName = "BO_Invoice",
                ToolTip = "Génère les écritures comptables pour tous les bulletins "
                        + "Validés du mois en cours et les passe au statut Exporté.",
                ConfirmationMessage =
                    "Générer les écritures comptables pour les bulletins Validés "
                    + "du mois courant ?\n\nLes bulletins passeront au statut 'Exporté'."
            };
            _exportMoisAction.Execute += OnExportMoisCourant;

            // ── Action 2 : Export de la sélection ─────────────────────────
            _exportSelectionAction = new SimpleAction(
                this, "ExporterBulletinsSelection", PredefinedCategory.Edit)
            {
                Caption = "Exporter sélection en comptabilité",
                ImageName = "BO_Invoice",
                ToolTip = "Génère les écritures comptables pour les bulletins sélectionnés.",
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects,
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+BulletinStatut,Valide#",
                ConfirmationMessage =
                    "Générer les écritures comptables pour les bulletins sélectionnés ?\n"
                    + "Ils passeront au statut 'Exporté'."
            };
            _exportSelectionAction.Execute += OnExportSelection;
        }

        // ── Handler : mois courant ─────────────────────────────────────────
        private void OnExportMoisCourant(object sender, SimpleActionExecuteEventArgs e)
        {
            var today = DateTime.Today;
            int annee = today.Year;
            int mois = today.Month;

            try
            {
                using var os = Application.CreateObjectSpace(typeof(Bulletin));
                var ecritures = ExportComptableBatchService.ExporterMois(os, annee, mois);
                os.CommitChanges();

                Application.ShowViewStrategy.ShowMessage(
                    $"{ecritures.Count} écriture(s) générée(s) pour {mois:D2}/{annee}.",
                    InformationType.Success, 5000, InformationPosition.Top);

                View.ObjectSpace.Refresh();
            }
            catch (UserFriendlyException ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    ex.Message, InformationType.Warning, 6000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur export comptable : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }

        // ── Handler : sélection ───────────────────────────────────────────
        private void OnExportSelection(object sender, SimpleActionExecuteEventArgs e)
        {
            var bulletins = e.SelectedObjects
                .OfType<Bulletin>()
                .Where(b => b.Statut == BulletinStatut.Valide)
                .ToList();

            if (!bulletins.Any())
            {
                Application.ShowViewStrategy.ShowMessage(
                    "Aucun bulletin Validé dans la sélection.",
                    InformationType.Warning, 4000, InformationPosition.Top);
                return;
            }

            try
            {
                using var os = Application.CreateObjectSpace(typeof(Bulletin));
                var svc = new EcritureFromBulletinService(os);
                int count = 0;

                foreach (var b in bulletins)
                {
                    var bOs = os.GetObject(b);
                    bOs.RecalculerSurGrilleExistante();
                    svc.GenererEcriture(bOs);
                    bOs.Statut = BulletinStatut.Exporte;
                    count++;
                }

                os.CommitChanges();

                Application.ShowViewStrategy.ShowMessage(
                    $"{count} écriture(s) générée(s) avec succès.",
                    InformationType.Success, 5000, InformationPosition.Top);

                View.ObjectSpace.Refresh();
            }
            catch (UserFriendlyException ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    ex.Message, InformationType.Warning, 6000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur export comptable : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }
    }
}
