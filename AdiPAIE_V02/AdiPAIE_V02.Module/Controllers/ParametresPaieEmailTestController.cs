using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;

namespace AdiPAIE_V02.Module.Controllers {
    public class ParametresPaieEmailTestController : ObjectViewController<DetailView, ParametresPaie> {
        public ParametresPaieEmailTestController() {
            var action = new SimpleAction(this, "EnvoyerEmailTest", PredefinedCategory.RecordEdit) {
                Caption = "Email test",
                ConfirmationMessage = "Un email de test va être envoyé avec la configuration SMTP courante.",
                ImageName = "BO_Mail",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage
            };
            action.Execute += Action_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            // V1.5 — Doublon avec TestEmailSMTP (popup) : on masque celui-ci.
            foreach (var a in Actions)
                a.Active.SetItemValue("V15_DoublonEmailTest", false);
        }

        private void Action_Execute(object sender, SimpleActionExecuteEventArgs e) {
            var param = View.CurrentObject as ParametresPaie;
            if (param == null) return;

            if (string.IsNullOrEmpty(param.MailFromAddress)) {
                throw new UserFriendlyException("Veuillez renseigner l'adresse d'expéditeur (SmtpFrom) dans Paramètres Paie.");
            }

            try {
                var senderSvc = param.CreateEmailSender();
                // Email de test envoyé à l'expéditeur lui-même
                senderSvc.Send(param.MailFromAddress, "Test SMTP AdiPAIE", "Ceci est un email de test envoyé depuis AdiPAIE.");
                Application.ShowViewStrategy.ShowMessage("Email de test envoyé à " + param.MailFromAddress, InformationType.Success);
            } catch (System.Exception ex) {
                throw new UserFriendlyException("Erreur lors de l'envoi de l'email de test : " + ex.Message, ex);
            }
        }
    }
}
