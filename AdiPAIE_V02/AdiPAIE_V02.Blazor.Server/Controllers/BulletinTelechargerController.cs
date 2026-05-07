using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;
using System.IO;
using System.Linq;

namespace AdiPAIE_V02.Blazor.Server.Controllers
{
    /// <summary>
    /// Permet au salarié de télécharger son bulletin PDF via l'espace salarié.
    /// Placé dans Blazor.Server pour IJSRuntime (adipaie.js → downloadFile).
    ///
    /// Sécurité :
    ///   - Visible uniquement si le compte est lié à une fiche Salarie
    ///   - Le PDF archive est servi directement si disponible
    ///   - Sinon régénéré via BulletinPdfService + clé RGPD du salarié
    ///   - BulletinSalarieFilterController garantit que le salarié
    ///     ne voit que ses propres bulletins
    /// </summary>
    public class BulletinTelechargerController
        : ObjectViewController<ListView, Bulletin>
    {
        private readonly SimpleAction _telecharger;

        public BulletinTelechargerController()
        {
            // V1.4.3 — visible UNIQUEMENT sur la ListView dédiée Espace Salarié.
            // RH n'a plus le bouton "Mon bulletin" sur Bulletin_ListView (où ça
            // n'avait aucun sens) — RH a Imprimer / Publier / Re-notifier à la place.
            TargetViewId = "Bulletin_EspaceSalarie_ListView";

            _telecharger = new SimpleAction(this,
                "Bulletin_Telecharger",
                PredefinedCategory.View)
            {
                Caption = "Télécharger",
                ImageName = "Action_Export",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Télécharger ce bulletin en PDF.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            _telecharger.Execute += OnTelecharger;
        }

        // ─────────────────────────────────────────────────────────────────
        private void OnTelecharger(object sender, SimpleActionExecuteEventArgs e)
        {
            var bulletin = (View is DetailView)
                ? View.CurrentObject as Bulletin
                : View.SelectedObjects.Count == 1
                    ? View.SelectedObjects[0] as Bulletin
                    : null;

            if (bulletin == null) return;

            try
            {
                var fileName = $"Bulletin_{bulletin.Periode}_{bulletin.Salarie?.Matricule}.pdf";

                // V1.4.3 — Le filtre Bulletin_EspaceSalarie_ListView garantit que
                // seuls les bulletins avec DatePublication != null arrivent ici.
                // Et BulletinPublicationService.Publier crée toujours PdfArchive.
                // Donc cette branche null/empty ne devrait pas se produire en
                // pratique. On la garde par sécurité défensive.
                if (bulletin.PdfArchive == null || bulletin.PdfArchive.Size == 0)
                {
                    throw new UserFriendlyException(
                        "Ce bulletin n'est pas disponible. Contactez le service RH.");
                }

                using var ms = new MemoryStream();
                bulletin.PdfArchive.SaveToStream(ms);
                var pdfBytes = ms.ToArray();

                // ── Téléchargement via JSInterop (adipaie.js) ─────────
                var js = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (js == null)
                    throw new UserFriendlyException(
                        "Téléchargement indisponible dans ce contexte.");

                var base64 = Convert.ToBase64String(pdfBytes);
                _ = js.InvokeVoidAsync(
                        "AdiPAIE.downloadFile", fileName, "application/pdf", base64)
                    .AsTask();

                Application.ShowViewStrategy?.ShowMessage(
                    $"Téléchargement de {fileName} en cours…",
                    InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (UserFriendlyException) { throw; }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }
    }
}
