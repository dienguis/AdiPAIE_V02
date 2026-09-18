// =============================================================================
//  InterimaireImportController.cs - V1.9 - Import Excel liste intérimaires
//
//  Ajoute le bouton « Importer liste effectif » dans la barre d'actions de
//  la ListView Interimaire. Ouvre une popup ImportInterimaireRequest (entité
//  persistante = trace de l'import) où le RH charge le fichier .xlsx et
//  clique sur Confirmer (dry-run par défaut).
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Services.Interim;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.Controllers
{
    public class InterimaireImportController : ObjectViewController<ListView, Interimaire>
    {
        private readonly PopupWindowShowAction _action;

        public InterimaireImportController()
        {
            _action = new PopupWindowShowAction(this,
                "Interimaire_ImporterListeEffectif",
                PredefinedCategory.RecordEdit)
            {
                Caption = "Importer liste effectif",
                ImageName = "Action_Import",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Charger un fichier Excel de la liste globale des intérimaires " +
                          "(ex: Liste septembre 2026.xlsx)."
            };
            _action.CustomizePopupWindowParams += CustomizePopupWindowParams;
            _action.Execute += Execute;
        }

        private void CustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var os = Application.CreateObjectSpace(typeof(ImportInterimaireRequest));
            var batch = os.CreateObject<ImportInterimaireRequest>();
            // Valeurs par défaut initialisées par AfterConstruction()

            var view = Application.CreateDetailView(os, batch);
            view.Caption = "Import Excel - Liste intérimaires";
            e.View = view;
            e.DialogController.SaveOnAccept = true;   // commit du batch (trace historique)
            e.DialogController.AcceptAction.Caption = "Analyser / Importer";
            e.DialogController.CancelAction.Caption = "Fermer";
        }

        private void Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var batch = e.PopupWindowView.CurrentObject as ImportInterimaireRequest;
            if (batch == null)
                throw new UserFriendlyException("Formulaire invalide.");
            if (batch.Fichier == null || batch.Fichier.Size == 0)
                throw new UserFriendlyException("Aucun fichier chargé.");

            // Extraire les bytes du FileData
            byte[] bytes;
            string nomFichier;
            using (var ms = new System.IO.MemoryStream())
            {
                batch.Fichier.SaveToStream(ms);
                bytes = ms.ToArray();
                nomFichier = batch.Fichier.FileName ?? "(sans nom)";
            }

            // Object space frais pour les créations d'Interimaire / ContratInterim
            using var osImport = Application.CreateObjectSpace(typeof(Interimaire));

            var result = InterimaireImportService.Importer(
                osImport, bytes,
                batch.CreerContratActif,
                batch.DateFinContratDefaut,
                batch.DryRun);

            // V1.9.1 - DÉTACHER le FileData binaire avant commit du batch.
            // Le binaire n'a aucune raison d'être stocké en base après lecture,
            // et sa persistance provoque parfois "String or binary data would
            // be truncated" (colonne Content mal dimensionnée sur d'anciens schémas).
            // On garde juste le nom du fichier dans FichierNom pour la traçabilité.
            batch.FichierNom = nomFichier;
            batch.Fichier = null;

            // Enregistrer les stats + rapport sur le batch (persisté)
            batch.LignesLues = result.LignesLues;
            batch.NbCrees = result.Crees;
            batch.NbContratsCrees = result.ContratsCrees;
            batch.NbDoublonsFichier = result.SkipDoublonsFichier;
            batch.NbDejaEnBase = result.SkipDejaEnBase;
            batch.NbErreurs = result.Erreurs;
            batch.Rapport = result.GenererRapport();
            e.PopupWindowView.ObjectSpace.CommitChanges();

            Application.ShowViewStrategy?.ShowMessage(
                batch.DryRun
                    ? $"Simulation : {result.Crees} à créer, {result.SkipDejaEnBase} déjà en base, {result.Erreurs} erreurs."
                    : $"Import réel : {result.Crees} intérimaires + {result.ContratsCrees} contrats créés, {result.Erreurs} erreurs.",
                result.Erreurs > 0 ? InformationType.Warning : InformationType.Success,
                8000, InformationPosition.Top);
        }
    }
}
