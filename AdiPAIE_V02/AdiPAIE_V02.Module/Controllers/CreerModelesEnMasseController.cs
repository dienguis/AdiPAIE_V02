// AdiPAIE_V02.Module/Controllers/CreerModelesEnMasseController.cs
//
// Action en masse "Créer modèles bulletin" sur la liste des Salariés.
// Crée un BulletinModele par défaut pour chaque salarié sélectionné
// (ou pour tous les salariés actifs si aucun n'est sélectionné).
//
// Réutilise le service BulletinModeleService → cohérence avec le Save & Close.
// Idempotent : skip les salariés qui ont déjà un BulletinModele actif.
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    public class CreerModelesEnMasseController
        : ObjectViewController<ListView, Salarie>
    {
        private readonly SimpleAction action;

        public CreerModelesEnMasseController()
        {
            action = new SimpleAction(this,
                "Salarie_CreerModelesEnMasse", PredefinedCategory.Edit)
            {
                Caption = "Créer modèles",
                ImageName = "BO_Resume",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.Caption,
                ToolTip = "Crée un BulletinModele par défaut pour les salariés sélectionnés "
                        + "(ou tous les salariés actifs si aucune sélection). "
                        + "Idempotent : ne touche pas aux salariés ayant déjà un modèle actif.",
                ConfirmationMessage = "Créer les modèles de bulletin manquants pour les salariés "
                                    + "sélectionnés (ou tous les actifs si aucune sélection) ?",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            action.Execute += OnExecute;
        }

        private void OnExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                // Sélection : objets cochés ou, à défaut, tous les salariés actifs
                List<Salarie> cibles;
                if (e.SelectedObjects != null && e.SelectedObjects.Count > 0)
                {
                    cibles = e.SelectedObjects.OfType<Salarie>().ToList();
                }
                else
                {
                    cibles = ObjectSpace.GetObjectsQuery<Salarie>()
                                        .Where(s => s.IsActif)
                                        .ToList();
                }

                if (cibles.Count == 0)
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        "Aucun salarié à traiter.",
                        InformationType.Warning, 4000, InformationPosition.Top);
                    return;
                }

                int crees = 0;
                int existants = 0;
                int desactiveParParam = 0;
                int erreurs = 0;
                var details = new List<string>();

                // Charger ParametresPaie une fois
                var parametres = ObjectSpace.GetObjectsQuery<ParametresPaie>().FirstOrDefault();

                foreach (var s in cibles)
                {
                    try
                    {
                        var resultat = BulletinModeleService.CreerParDefaut(
                            ObjectSpace, s, parametres);

                        switch (resultat)
                        {
                            case BulletinModeleService.ResultatCreation.Cree:
                                crees++;
                                break;
                            case BulletinModeleService.ResultatCreation.ExistantConserve:
                                existants++;
                                break;
                            case BulletinModeleService.ResultatCreation.DesactiveParParametre:
                                desactiveParParam++;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        erreurs++;
                        details.Add($"❌ {s.Matricule} ({s.FullName}) : {ex.Message}");
                    }
                }

                if (crees > 0)
                {
                    ObjectSpace.CommitChanges();
                    View?.Refresh();
                }

                // ── Audit (best effort) ──────────────────────────────────
                try
                {
                    AuditService.Enregistrer(Application,
                        "BulletinModele",
                        "Création en masse",
                        "-",
                        $"{crees} créé(s)",
                        $"Sélection={cibles.Count} | Créés={crees} | Déjà existants={existants} | "
                        + $"Désactivé par paramètre={desactiveParParam} | Erreurs={erreurs}\n"
                        + (details.Count > 0 ? string.Join("\n", details) : ""));
                }
                catch { /* audit non bloquant */ }

                // ── Message de résultat ──────────────────────────────────
                string resume;
                if (desactiveParParam > 0 && crees == 0)
                {
                    resume = $"Création désactivée par ParametresPaie.ModeleAuto_CreerAuSave (false). "
                           + $"{desactiveParParam} salarié(s) ignoré(s).";
                }
                else
                {
                    resume = $"{crees} modèle(s) créé(s)"
                           + (existants > 0 ? $", {existants} déjà existant(s)" : "")
                           + (erreurs > 0 ? $", {erreurs} erreur(s)" : "")
                           + ".";
                }

                var infoType = erreurs > 0
                    ? InformationType.Warning
                    : (crees > 0 ? InformationType.Success : InformationType.Info);

                Application.ShowViewStrategy?.ShowMessage(
                    resume, infoType, 6000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur lors de la création des modèles : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }
    }
}
