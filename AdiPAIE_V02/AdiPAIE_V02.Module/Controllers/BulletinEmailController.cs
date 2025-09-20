using AdiPAIE_V02.Module.Reports;
using System.IO;
using System.Net.Mail;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.XtraReports.UI;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;

namespace AdiPAIE_V02.Module.Controllers
{
    public class BulletinEmailController : ObjectViewController<ObjectView, Bulletin>
    {
        public BulletinEmailController() {
            var action = new SimpleAction(this, "EnvoyerBulletinEmail", PredefinedCategory.ObjectsCreation) {
                Caption = "Envoyer par email",
                ImageName = "BO_Mail"
            };
            action.Execute += OnExecute;
        }

        private void OnExecute(object sender, SimpleActionExecuteEventArgs e) {
            foreach (Bulletin b in View.SelectedObjects) {
                if (b?.Salarie?.Email == null) {
                    throw new UserFriendlyException("Aucune adresse email pour ce salarié.");
                }
                // TODO: Remplacer par génération réelle du PDF si vous avez XtraReport pour le bulletin
                Attachment att = null;
                // Génération PDF du bulletin via XtraReport
                var report = new BulletinReport();
                report.DataSource = new[] { b }; // DataSource minimale
                using (var ms = new System.IO.MemoryStream()) {
                    report.ExportToPdf(ms);
                    ms.Position = 0;
                    att = new Attachment(ms, $"Bulletin_{b.Salarie?.Matricule}_{b.Mois}_{b.Annee}.pdf", "application/pdf");
                }
                var senderSvc = ParametresPaie.GetOrCreate(Application.CreateObjectSpace()).CreateEmailSender();
                senderSvc.Send(b.Salarie.Email, $"Bulletin {b.Mois}/{b.Annee}", "Veuillez trouver votre bulletin en pièce jointe.", att);
            }
        }
    }
}
