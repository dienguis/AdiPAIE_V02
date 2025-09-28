using System;
using System.Net.Mail;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.Data.Filtering;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.Services;
using ParametresPaie = AdiPAIE_V02.Module.BusinessObjects.ParametresPaie;
using DevExpress.ExpressApp.Templates;

namespace AdiPAIE_V02.Module.Controllers
{
    // Action disponible sur la DetailView de ParametresPaie
    public class ParametresPaieTestEmailController : ObjectViewController<DetailView, BusinessObjects.ParametresPaie>
    {
        private readonly PopupWindowShowAction testEmailAction;

        public ParametresPaieTestEmailController() {
            testEmailAction = new PopupWindowShowAction(this, "TestEmailSMTP", PredefinedCategory.Edit) {
                Caption = "Envoyer email de test",
                ImageName = "Mail_16x16",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            testEmailAction.CustomizePopupWindowParams += OnCustomizePopupWindowParams;
            testEmailAction.Execute += OnExecute;
        }

        private void OnCustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e) {
            var os = Application.CreateObjectSpace(typeof(TestEmailParams));
            var prm = os.CreateObject<TestEmailParams>();

            // Valeur par défaut = From ou User des paramètres
            var p = (ParametresPaie)View.CurrentObject;
            prm.To = string.IsNullOrWhiteSpace(p?.MailFromAddress) ? p?.SmtpUserName : p?.MailFromAddress;

            var dv = Application.CreateDetailView(os, prm);
            dv.ViewEditMode = DevExpress.ExpressApp.Editors.ViewEditMode.Edit;
            e.View = dv;
            e.DialogController.SaveOnAccept = true;
        }

        private void OnExecute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var prm = e.PopupWindowViewCurrentObject as TestEmailParams;
            if (prm == null) return;

            var p = (ParametresPaie)View.CurrentObject;
            if (p == null) throw new UserFriendlyException("Paramètres e-mail introuvables.");
            if (string.IsNullOrWhiteSpace(prm.To)) throw new UserFriendlyException("Veuillez renseigner le destinataire.");

            try
            {
                var senderSvc = p.CreateEmailSender();
                Application.ShowViewStrategy.ShowMessage("Envoi en cours...", InformationType.Info, 1500, InformationPosition.Top);

                // ⚠️ Envoi en tâche pour éviter tout blocage UI
                System.Threading.Tasks.Task.Run(() => {
                    senderSvc.Send(
                        prm.To,
                        prm.Subject ?? "Test AdiPAIE",
                        prm.BodyHtml ?? "<b>Ça fonctionne !</b>"
                    );
                })
                .ContinueWith(t => {
                    if (t.Exception != null)
                    {
                        var ex = t.Exception.GetBaseException();
                        Application.ShowViewStrategy.ShowMessage("Échec : " + ex.Message, InformationType.Error, 5000, InformationPosition.Top);
                        throw new UserFriendlyException("Échec de l'envoi : " + ex.Message, ex);
                    }
                    else
                    {
                        Application.ShowViewStrategy.ShowMessage("Email de test envoyé.", InformationType.Success, 3000, InformationPosition.Top);
                    }
                }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                throw new UserFriendlyException("Erreur lors de l'envoi : " + msg, ex);
            }
        }

    }
}
