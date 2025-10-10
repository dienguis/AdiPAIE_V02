using System;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Actions "Envoyer la clé" et "Régénérer la clé" sur la DetailView du Salarié.
    /// </summary>
    public sealed class SalarieKeyController : ObjectViewController<DetailView, Salarie>
    {
        private readonly SimpleAction _sendKey;
        private readonly SimpleAction _regenerateKey;

        public SalarieKeyController()
        {
            _sendKey = new SimpleAction(this, "EnvoyerCleSalarie", PredefinedCategory.Edit)
            {
                Caption = "Envoyer la clé",
                ImageName = "Send",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            _sendKey.Execute += OnSendKeyAsync;

            _regenerateKey = new SimpleAction(this, "RegenererCleSalarie", PredefinedCategory.Edit)
            {
                Caption = "Régénérer la clé",
                ImageName = "Reset",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ConfirmationMessage = "Remplacer la clé actuelle et envoyer la nouvelle au salarié ?"
            };
            _regenerateKey.Execute += OnRegenerateKeyAsync;
        }

        private async void OnSendKeyAsync(object sender, SimpleActionExecuteEventArgs e)
        {
            var s = View.CurrentObject as Salarie;
            if (s == null) return;
            if (string.IsNullOrWhiteSpace(s.Email))
                throw new UserFriendlyException("L'e-mail du salarié est vide.");

            var p = ParametresPaie.TryGet(ObjectSpace)
                    ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
            var senderSvc = p.CreateEmailSender();

            var clearKey = EmployeeKeyManager.EnsureKey(ObjectSpace, s);
            var subject = "Votre clé personnelle de déchiffrement (à conserver)";
            var bodyHtml = $@"
<p>Bonjour {s.FullName},</p>
<p>Voici votre <b>clé personnelle</b> pour ouvrir vos bulletins de paie PDF :</p>
<p style='font-size:18px'><b>{clearKey}</b></p>
<p>Conservez-la précieusement et ne la partagez avec personne.</p>";

            await senderSvc.SendAsync(s.Email, subject, bodyHtml, null);

            Application.ShowViewStrategy.ShowMessage(
                "Clé envoyée au salarié.", InformationType.Success, 3000, InformationPosition.Top);
        }

        private async void OnRegenerateKeyAsync(object sender, SimpleActionExecuteEventArgs e)
        {
            var s = View.CurrentObject as Salarie;
            if (s == null) return;
            if (string.IsNullOrWhiteSpace(s.Email))
                throw new UserFriendlyException("L'e-mail du salarié est vide.");

            var newKey = PayslipKeyService.NewKey();
            s.PayslipKeyEnc = LocalSecretProtector.Protect(newKey);
            s.PayslipKeyAssignedOn = DateTime.Now;
            ObjectSpace.CommitChanges();

            var p = ParametresPaie.TryGet(ObjectSpace)
                    ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
            var senderSvc = p.CreateEmailSender();

            var subject = "Nouvelle clé personnelle de déchiffrement (à conserver)";
            var bodyHtml = $@"
<p>Bonjour {s.FullName},</p>
<p>Votre clé personnelle a été <b>régénérée</b>. Voici la nouvelle clé à utiliser :</p>
<p style='font-size:18px'><b>{newKey}</b></p>
<p>L'ancienne clé n'est plus valide. Conservez cette clé précieusement.</p>";

            await senderSvc.SendAsync(s.Email, subject, bodyHtml, null);

            Application.ShowViewStrategy.ShowMessage(
                "Nouvelle clé générée et envoyée.", InformationType.Success, 3000, InformationPosition.Top);
        }
    }
}
