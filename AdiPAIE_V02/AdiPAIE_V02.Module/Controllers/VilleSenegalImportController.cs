using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Controller d'import des villes sénégalaises depuis GeoNames.
    /// Placé sur la ListView VilleSenegal.
    ///
    /// Action : "Importer depuis GeoNames"
    ///   → Appelle GeoNamesService.GetVillesSenegalAsync()
    ///   → Crée les VilleSenegal manquantes (idempotent — pas de doublons)
    ///   → Affiche le nombre de villes importées
    /// </summary>
    public class VilleSenegalImportController
        : ObjectViewController<ListView, VilleSenegal>
    {
        readonly SimpleAction importerAction;

        public VilleSenegalImportController()
        {
            importerAction = new SimpleAction(this,
                "VilleSenegal_Importer", PredefinedCategory.View)
            {
                Caption = "🌍 Import GeoNames",
                ImageName = "State_Confirmed",
                ConfirmationMessage = "Importer toutes les villes du Sénégal depuis GeoNames ? "
                                    + "Les villes existantes ne seront pas écrasées.",
                ToolTip = "Récupère la liste complète des localités sénégalaises "
                                    + "depuis la base GeoNames (gratuit). "
                                    + "Prérequis : GeoNames Username configuré dans Paramètres de paie.",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            importerAction.Execute += OnImporter;
        }

        private async void OnImporter(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                // Récupérer le username GeoNames
                string username;
                using (var os = Application.CreateObjectSpace(typeof(ParametresPaie)))
                {
                    var prm = ParametresPaie.TryGet(os);
                    username = prm?.GeoNamesUsername;
                }

                if (string.IsNullOrWhiteSpace(username))
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        "GeoNames Username non configuré. "
                        + "Renseignez-le dans Paramètres de paie → GeoNames Username.",
                        InformationType.Warning, 5000, InformationPosition.Top);
                    return;
                }

                Application.ShowViewStrategy?.ShowMessage(
                    "Importation en cours… (peut prendre quelques secondes)",
                    InformationType.Info, 4000, InformationPosition.Top);

                // Appel API
                var villes = await GeoNamesService.GetVillesSenegalAsync(username);

                if (villes == null || villes.Count == 0)
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        "Aucune ville récupérée depuis GeoNames. "
                        + "Vérifiez le username et que le webservice est activé.",
                        InformationType.Warning, 5000, InformationPosition.Top);
                    return;
                }

                // Charger les noms déjà en base
                var nomsExistants = new System.Collections.Generic.HashSet<string>(
                    ObjectSpace.GetObjectsQuery<VilleSenegal>()
                        .Select(x => x.Nom),
                    StringComparer.OrdinalIgnoreCase);

                // HashSet pour éviter les doublons dans le batch GeoNames
                var nomsDansBatch = new System.Collections.Generic.HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

                int nbCrees = 0;
                int nbIgnores = 0;

                foreach (var v in villes)
                {
                    // Ignorer si déjà en base OU déjà créé dans ce batch
                    if (nomsExistants.Contains(v.Nom)
                     || nomsDansBatch.Contains(v.Nom))
                    {
                        nbIgnores++;
                        continue;
                    }

                    var ville = ObjectSpace.CreateObject<VilleSenegal>();
                    ville.Nom = v.Nom;
                    ville.Region = v.Region;
                    ville.Actif = true;

                    nomsDansBatch.Add(v.Nom);
                    nbCrees++;
                }

                ObjectSpace.CommitChanges();
                View.Refresh();

                Application.ShowViewStrategy?.ShowMessage(
                    $"{nbCrees} ville(s) importée(s), {nbIgnores} déjà existante(s).",
                    InformationType.Success, 5000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur import GeoNames : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }
    }
}
