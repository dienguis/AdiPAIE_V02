using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;

namespace AdiPAIE_V02.Blazor.Server.Controllers
{
    public class DeclarationController
        : ObjectViewController<ListView, DeclarationMenu>
    {
        private readonly SimpleAction _ipresPdf;
        private readonly SimpleAction _ipresXlsx;
        private readonly SimpleAction _cssPdf;
        private readonly SimpleAction _cssXlsx;

        public DeclarationController()
        {
            // SelectionDependencyType.Independent → visible même si liste vide
            _ipresPdf = new SimpleAction(this, "Decl_IPRES_PDF", PredefinedCategory.Edit)
            {
                Caption = "IPRES — PDF",
                ImageName = "Action_Print",
                ToolTip = "Générer la déclaration IPRES en PDF.",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _ipresPdf.Execute += (_, __) => OuvrirPopup("IPRES", "pdf");

            _ipresXlsx = new SimpleAction(this, "Decl_IPRES_XLSX", PredefinedCategory.Edit)
            {
                Caption = "IPRES — Excel",
                ImageName = "Action_Export",
                ToolTip = "Générer la déclaration IPRES en Excel.",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _ipresXlsx.Execute += (_, __) => OuvrirPopup("IPRES", "xlsx");

            _cssPdf = new SimpleAction(this, "Decl_CSS_PDF", PredefinedCategory.Edit)
            {
                Caption = "CSS — PDF",
                ImageName = "Action_Print",
                ToolTip = "Générer la déclaration CSS en PDF.",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _cssPdf.Execute += (_, __) => OuvrirPopup("CSS", "pdf");

            _cssXlsx = new SimpleAction(this, "Decl_CSS_XLSX", PredefinedCategory.Edit)
            {
                Caption = "CSS — Excel",
                ImageName = "Action_Export",
                ToolTip = "Générer la déclaration CSS en Excel.",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _cssXlsx.Execute += (_, __) => OuvrirPopup("CSS", "xlsx");
        }

        // ─────────────────────────────────────────────────────────────────
        private void OuvrirPopup(string type, string format)
        {
            var npos = Application.CreateObjectSpace(typeof(ParametreDeclaration));
            var prm = npos.CreateObject<ParametreDeclaration>();
            var ref_ = DateTime.Today.AddMonths(-1);
            prm.Annee = ref_.Year;
            prm.Mois = ref_.Month;

            var dv = Application.CreateDetailView(npos, prm, true);
            dv.ViewEditMode = ViewEditMode.Edit;
            dv.Caption = $"Déclaration {type} — {format.ToUpper()}";

            var svp = new ShowViewParameters
            {
                CreatedView = dv,
                TargetWindow = TargetWindow.NewModalWindow
            };

            var dc = Application.CreateController<DialogController>();
            dc.SaveOnAccept = false;
            dc.AcceptAction.Caption = "Générer";
            dc.AcceptAction.Execute += (_, __) =>
            {
                if (prm.Mois < 1 || prm.Mois > 12 || prm.Annee < 2000)
                {
                    Application.ShowViewStrategy.ShowMessage(
                        "Année (≥ 2000) et mois (1-12) requis.",
                        InformationType.Warning, 3000, InformationPosition.Top);
                    return;
                }
                GenererEtTelecharger(type, format, prm.Annee, prm.Mois);
            };

            svp.Controllers.Add(dc);
            Application.ShowViewStrategy.ShowView(
                svp, new ShowViewSource(Frame, null));
        }

        // ─────────────────────────────────────────────────────────────────
        private void GenererEtTelecharger(
            string type, string format, int annee, int mois)
        {
            try
            {
                using var os = Application.CreateObjectSpace(typeof(Bulletin));

                byte[] contenu = (type, format) switch
                {
                    ("IPRES", "pdf") => DeclarationService.GenererIPRES_PDF(os, annee, mois),
                    ("IPRES", "xlsx") => DeclarationService.GenererIPRES_XLSX(os, annee, mois),
                    ("CSS", "pdf") => DeclarationService.GenererCSS_PDF(os, annee, mois),
                    ("CSS", "xlsx") => DeclarationService.GenererCSS_XLSX(os, annee, mois),
                    _ => null
                };

                if (contenu == null || contenu.Length == 0)
                {
                    Application.ShowViewStrategy.ShowMessage(
                        "Génération échouée — vérifiez que LibreOffice est installé " +
                        "et que des bulletins existent pour cette période.",
                        InformationType.Error, 6000, InformationPosition.Top);
                    return;
                }

                var nomFichier = $"Decl_{type}_{annee}_{mois:D2}.{format}";
                var mimeType = format == "pdf"
                    ? "application/pdf"
                    : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                var js = Application.ServiceProvider?.GetService<IJSRuntime>();
                if (js != null)
                {
                    _ = js.InvokeVoidAsync(
                            "AdiPAIE.downloadFile", nomFichier, mimeType,
                            Convert.ToBase64String(contenu))
                        .AsTask();

                    Application.ShowViewStrategy.ShowMessage(
                        $"Déclaration {type} ({format.ToUpper()}) générée : {nomFichier}",
                        InformationType.Success, 4000, InformationPosition.Top);
                }
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Erreur : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }
    }
}
