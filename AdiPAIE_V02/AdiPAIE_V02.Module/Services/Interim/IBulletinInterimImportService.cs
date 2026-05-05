// =============================================================================
//  IBulletinInterimImportService.cs — V1.3 Sprint 1
// =============================================================================

using System;
using AdiPAIE_V02.Module.Models.Interim;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Interim
{
    public interface IBulletinInterimImportService
    {
        /// <summary>
        /// Étape 1 du wizard : parse le fichier Excel sans rien persister.
        /// Retourne un preview détaillé avec compteurs et lignes parsées.
        /// </summary>
        ImportPreviewDto Preview(
            byte[] fichierXlsxBytes,
            string fichierNom,
            int annee,
            int mois,
            Guid societeInterimOid,
            IObjectSpace os);

        /// <summary>
        /// Étape 2 du wizard : commit le preview précédemment validé.
        /// Crée le batch + les bulletins + les fiches Interimaire manquantes.
        /// </summary>
        ImportResultDto Commit(
            ImportPreviewDto preview,
            string importeParUserName,
            bool ecraserSiBatchExistant,
            IObjectSpace os);
    }
}
