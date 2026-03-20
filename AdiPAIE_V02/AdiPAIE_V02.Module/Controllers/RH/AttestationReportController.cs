using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.XtraPrinting;
using System;
using System.IO;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    public class AttestationReportController : ViewController
    {
        private readonly SimpleAction _genererAction;
        private readonly SimpleAction _exportPdfAction;

        public AttestationReportController()
        {
            TargetObjectType = typeof(DemandeAttestation);

            _genererAction = new SimpleAction(this, "Attestation_Generer", PredefinedCategory.View)
            {
                Caption = "Générer attestation",
                ImageName = "Action_Export",
                ToolTip = "Génère depuis le template Word et joint à la demande.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                ConfirmationMessage = "Générer l'attestation et la joindre à la demande ?"
            };
            _genererAction.Execute += GenererAction_Execute;

            //_exportPdfAction = new SimpleAction(this, "Attestation_ExportPdf", PredefinedCategory.View)
            //{
            //    Caption = "Aperçu (PDF)",
            //    ImageName = "Action_Print",
            //    ToolTip = "Génère via le report programmatique.",
            //    SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            //};
            //_exportPdfAction.Execute += ExportPdfAction_Execute;
        }

        private void GenererAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var demande = (DemandeAttestation)e.CurrentObject;
            if (demande == null) return;
            try
            {
                var prm = ParametresPaie.TryGet(ObjectSpace);
                var templateBytes = SelectionnerTemplate(prm, demande.Nature ?? AttestationNature.Travail);

                if (templateBytes != null && templateBytes.Length > 0)
                {
                    // var docxBytes = AttestationTemplateService.Fusionner(demande, ObjectSpace);
                    //JointFichier(demande, docxBytes, "docx");
                    var pdfBytes = AttestationTemplateService.FusionnerEtConvertirEnPdf(
                       demande, ObjectSpace);
                    JointFichier(demande, pdfBytes, "pdf");
                    Application.ShowViewStrategy?.ShowMessage("Attestation générée depuis le template Word.", InformationType.Success, 4000, InformationPosition.Top);
                }
                else
                {
                    GenererViaXtraReport(demande);
                    Application.ShowViewStrategy?.ShowMessage("Aucun template Word — report programmatique utilisé.", InformationType.Info, 5000, InformationPosition.Top);
                }

                if (demande.Statut == DemandeStatut.Soumise) demande.PrendreEnCharge();
                ObjectSpace.CommitChanges();
                View.Refresh();
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage($"Erreur : {ex.Message}", InformationType.Error, 7000, InformationPosition.Top);
            }
        }

        //private void ExportPdfAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        //{
        //    var demande = (DemandeAttestation)e.CurrentObject;
        //    if (demande == null) return;
        //    try
        //    {
        //        GenererViaXtraReport(demande);
        //        if (demande.Statut == DemandeStatut.Soumise) demande.PrendreEnCharge();
        //        ObjectSpace.CommitChanges();
        //        View.Refresh();
        //        Application.ShowViewStrategy?.ShowMessage("PDF généré et joint.", InformationType.Success, 4000, InformationPosition.Top);
        //    }
        //    catch (Exception ex)
        //    {
        //        Application.ShowViewStrategy?.ShowMessage($"Erreur : {ex.Message}", InformationType.Error, 6000, InformationPosition.Top);
        //    }
        //}

        private static byte[] SelectionnerTemplate(ParametresPaie prm, AttestationNature nature)
        {
            if (prm == null) return null;
            return nature switch
            {
                AttestationNature.Conge => prm.TemplateAttestationDeConges?.Content,
                AttestationNature.Emploi => prm.TemplateCertificatEmploi?.Content,
                AttestationNature.CessationPaiement => prm.TemplateCessationPaiement?.Content,
                _ => prm.TemplateAttestation?.Content
            };
        }

        private void JointFichier(DemandeAttestation demande, byte[] bytes, string ext)
        {
            if (demande.Document == null)
                demande.Document = ObjectSpace.CreateObject<DevExpress.Persistent.BaseImpl.FileData>();
            var nom = $"Attestation_{demande.Nature}_{demande.Salarie?.LastName}_{DateTime.Today:yyyyMMdd}.{ext}";
            using var ms = new MemoryStream(bytes);
            demande.Document.LoadFromStream(nom, ms);
        }

        private void GenererViaXtraReport(DemandeAttestation demande)
        {
            var report = new AdiPAIE_V02.Module.Reports.AttestationReport(demande);
            using var ms = new MemoryStream();
            report.ExportToPdf(ms, new PdfExportOptions { ConvertImagesToJpeg = false });
            ms.Position = 0;
            JointFichier(demande, ms.ToArray(), "pdf");
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateStates();
            View.SelectionChanged += (_, __) => UpdateStates();
        }
        protected override void OnDeactivated()
        {
            View.SelectionChanged -= (_, __) => UpdateStates();
            base.OnDeactivated();
        }
        void UpdateStates()
        {
            var d = View?.CurrentObject as DemandeAttestation;
            _genererAction.Active["sel"] = d != null && d.Statut != DemandeStatut.Rejetee;
        //    _exportPdfAction.Active["sel"] = d != null;
        }
    }
}
