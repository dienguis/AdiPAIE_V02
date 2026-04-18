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
        : ObjectViewController<ObjectView, Bulletin>
    {
        private readonly SimpleAction _telecharger;

        public BulletinTelechargerController()
        {
            _telecharger = new SimpleAction(this,
                "Bulletin_Telecharger",
                PredefinedCategory.View)
            {
                Caption = "Télécharger mon bulletin",
                ImageName = "Action_Export",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Télécharger ce bulletin en PDF.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            _telecharger.Execute += OnTelecharger;
        }

        protected override void OnActivated()
        {
            base.OnActivated();

            // Visible uniquement si compte lie a une fiche salarie
            try
            {
                _telecharger.Active["estSalarie"] =
                    AdiPAIE_V02.Module.Controllers.EspaceSalarieHelper
                        .EstSalarieConnecte(ObjectSpace);
            }
            catch
            {
                _telecharger.Active["estSalarie"] = false;
            }
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
                byte[] pdfBytes;
                var fileName = $"Bulletin_{bulletin.Periode}_{bulletin.Salarie?.Matricule}.pdf";

                // ── Archive disponible → servir directement ───────────
                if (bulletin.PdfArchive != null && bulletin.PdfArchive.Size > 0)
                {
                    using var ms = new MemoryStream();
                    bulletin.PdfArchive.SaveToStream(ms);
                    pdfBytes = ms.ToArray();
                }
                else
                {
                    // ── Régénération à la volée ───────────────────────
                    var keyEnc = bulletin.Salarie?.PayslipKeyEnc;
                    if (string.IsNullOrWhiteSpace(keyEnc))
                        throw new UserFriendlyException(
                            "Votre bulletin n'est pas encore disponible en PDF. "
                            + "Contactez le service RH.");

                    var pwd = LocalSecretProtector.Unprotect(keyEnc);

                    using var osRead = Application.CreateObjectSpace(typeof(Bulletin));
                    pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(
                        osRead, bulletin.Oid, pwd);

                    if (pdfBytes == null || pdfBytes.Length == 0)
                        throw new UserFriendlyException(
                            "Impossible de générer le PDF. Contactez le service RH.");

                    // Archiver pour les prochains téléchargements
                    if (bulletin.PdfArchive == null)
                        bulletin.PdfArchive = ObjectSpace
                            .CreateObject<DevExpress.Persistent.BaseImpl.FileData>();
                    using var msArc = new MemoryStream(pdfBytes);
                    bulletin.PdfArchive.LoadFromStream(fileName, msArc);
                    ObjectSpace.CommitChanges();
                }

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
