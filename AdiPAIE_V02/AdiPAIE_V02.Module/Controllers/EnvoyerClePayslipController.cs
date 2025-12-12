using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.ExpressApp.Utils;
using DevExpress.Persistent.Base;
using System;
using System.Threading; // <-- pour SynchronizationContext

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class EnvoyerClePayslipController
        : ObjectViewController<DetailView, Salarie>
    {
        private readonly SimpleAction _sendKey;

        public EnvoyerClePayslipController()
        {
            _sendKey = new SimpleAction(this, "EnvoyerClePDF", PredefinedCategory.RecordEdit)
            {
                Caption = "Envoyer la clé PDF (séparé)",
                ImageName = "BO_Security_Permission",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            _sendKey.Execute += OnExecuteAsync; // handler async
        }

        // IMPORTANT: async void ok pour un handler d’événement UI
        private async void OnExecuteAsync(object evtSender, SimpleActionExecuteEventArgs e)
        {
            _sendKey.Active["Busy"] = false;

            try
            {
                var os = View?.ObjectSpace ?? throw new UserFriendlyException("ObjectSpace indisponible.");
                var sal = View.CurrentObject as Salarie ?? throw new UserFriendlyException("Aucun salarié en contexte.");

                if (string.IsNullOrWhiteSpace(sal.Email))
                    throw new UserFriendlyException("Le salarié n'a pas d'adresse e-mail.");

                // 1) (Re)générer la clé puis COMMIT AVANT ENVOI
                string clearKey;
                if (string.IsNullOrWhiteSpace(sal.PayslipKeyEnc))
                {
                    clearKey = PasswordGenerator(10);
                    sal.PayslipKeyEnc = LocalSecretProtector.Protect(clearKey);
                    sal.PayslipKeyAssignedOn = DateTime.Now;
                }
                else
                {
                    clearKey = LocalSecretProtector.Unprotect(sal.PayslipKeyEnc);
                }
                os.SetModified(sal);
                os.CommitChanges();

                // 2) Récupérer service d’envoi
                var p = ParametresPaie.TryGet(os)
                        ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
                var mailer = p.CreateEmailSender()
                          ?? throw new UserFriendlyException("Service d'envoi d'e-mails indisponible.");

                var subject = "Votre clé de protection des bulletins (confidentiel)";
                var bodyHtml = $@"
<p>Bonjour {sal.FullName},</p>
<p>Voici votre clé confidentielle pour ouvrir vos bulletins de paie :</p>
<p style=""font-size:16px;font-weight:bold"">{clearKey}</p>
<p><i>Conservez-la précieusement. Ne la partagez pas.</i></p>
<p>Cordialement,<br/>{p.MailFromDisplayName}</p>";

                // Capture le contexte UI avant l'attente
                var uiContext = SynchronizationContext.Current;

                // 3) ENVOI **ASYNC** (pas de ConfigureAwait(false))
                await mailer.SendAsync(sal.Email, subject, bodyHtml, attachment: null);

                // 4) Notification succès — reposte sur le thread UI si nécessaire
                void ShowToast() =>
                    Application.ShowViewStrategy.ShowMessage(
                        "Clé envoyée par e-mail et enregistrée.",
                        InformationType.Success, 3000, InformationPosition.Top);

                if (uiContext != null)
                    uiContext.Post(_ => ShowToast(), null);
                else
                    ShowToast();
            }
            catch (UserFriendlyException) { throw; }
            catch (Exception ex)
            {
                Tracing.Tracer.LogError(ex);
                throw new UserFriendlyException("Échec de l'envoi de l'e-mail : " + ex.Message);
            }
            finally
            {
                _sendKey.Active.RemoveItem("Busy");
            }
        }

        // Générateur de clé
        private static string PasswordGenerator(int len)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[len];
            rng.GetBytes(bytes);
            var sb = new System.Text.StringBuilder(len);
            for (int i = 0; i < len; i++) sb.Append(alphabet[bytes[i] % alphabet.Length]);
            return sb.ToString();
        }
    }
}
