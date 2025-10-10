using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class SalarieKeyBulkController : ObjectViewController<ListView, Salarie>
    {
        private readonly SimpleAction _sendKeys;
        private readonly SimpleAction _regenKeys;

        public SalarieKeyBulkController()
        {
            _sendKeys = new SimpleAction(this, "EnvoyerClesSelection", PredefinedCategory.Edit)
            {
                Caption = "Envoyer clé (sélection)",
                ImageName = "MailSend",
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects
            };
            _sendKeys.Execute += OnSendKeysAsync;

            _regenKeys = new SimpleAction(this, "RegenererClesSelection", PredefinedCategory.Edit)
            {
                Caption = "Régénérer clés (sélection)",
                ImageName = "Reset",
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects,
                ConfirmationMessage = "Régénérer la clé de TOUS les salariés sélectionnés et envoyer la nouvelle ?"
            };
            _regenKeys.Execute += OnRegenKeysAsync;
        }

        private async void OnSendKeysAsync(object sender, SimpleActionExecuteEventArgs e)
        {
            var selected = View.SelectedObjects.Cast<Salarie>().ToList();
            if (selected.Count == 0) return;

            var p = ParametresPaie.TryGet(ObjectSpace)
                    ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
            var senderSvc = p.CreateEmailSender();

            int ok = 0; var fails = new List<string>(); int i = 0; int n = selected.Count;

            foreach (var s in selected)
            {
                i++;
                Application.ShowViewStrategy.ShowMessage($"Envoi clé {i}/{n} – {s.FullName}", InformationType.Info, 1200, InformationPosition.Top);

                try
                {
                    if (string.IsNullOrWhiteSpace(s.Email))
                        throw new UserFriendlyException("Email manquant");

                    // crée si absente, sinon renvoie la clé existante (déchiffrée)
                    var clearKey = EmployeeKeyManager.EnsureKey(ObjectSpace, s);

                    var subject = "Votre clé personnelle de déchiffrement (à conserver)";
                    var bodyHtml = $@"
<p>Bonjour {s.FullName},</p>
<p>Voici votre <b>clé personnelle</b> pour ouvrir vos bulletins de paie PDF :</p>
<p style='font-size:18px'><b>{clearKey}</b></p>
<p>Conservez-la précieusement et ne la partagez avec personne.</p>";

                    await senderSvc.SendAsync(s.Email, subject, bodyHtml, null);
                    ok++;
                }
                catch (Exception ex)
                {
                    fails.Add($"{s.FullName} : {ex.Message}");
                }
            }

            Application.ShowViewStrategy.ShowMessage(
                BuildSummary("Envoi clés", ok, n, fails), fails.Count == 0 ? InformationType.Success : InformationType.Warning, 7000, InformationPosition.Top);
        }

        private async void OnRegenKeysAsync(object sender, SimpleActionExecuteEventArgs e)
        {
            var selected = View.SelectedObjects.Cast<Salarie>().ToList();
            if (selected.Count == 0) return;

            var p = ParametresPaie.TryGet(ObjectSpace)
                    ?? throw new UserFriendlyException("Paramètres de paie introuvables.");
            var senderSvc = p.CreateEmailSender();

            int ok = 0; var fails = new List<string>(); int i = 0; int n = selected.Count;

            foreach (var s in selected)
            {
                i++;
                Application.ShowViewStrategy.ShowMessage($"Régénération {i}/{n} – {s.FullName}", InformationType.Info, 1200, InformationPosition.Top);

                try
                {
                    if (string.IsNullOrWhiteSpace(s.Email))
                        throw new UserFriendlyException("Email manquant");

                    var newKey = PayslipKeyService.NewKey();
                    s.PayslipKeyEnc = LocalSecretProtector.Protect(newKey);
                    s.PayslipKeyAssignedOn = DateTime.Now;
                    ObjectSpace.CommitChanges();

                    var subject = "Nouvelle clé personnelle de déchiffrement (à conserver)";
                    var bodyHtml = $@"
<p>Bonjour {s.FullName},</p>
<p>Votre clé personnelle a été <b>régénérée</b>. Voici la nouvelle clé à utiliser pour ouvrir vos bulletins PDF :</p>
<p style='font-size:18px'><b>{newKey}</b></p>
<p>L'ancienne clé n'est plus valide. Conservez cette clé précieusement.</p>";

                    await senderSvc.SendAsync(s.Email, subject, bodyHtml, null);
                    ok++;
                }
                catch (Exception ex)
                {
                    fails.Add($"{s.FullName} : {ex.Message}");
                }
            }

            Application.ShowViewStrategy.ShowMessage(
                BuildSummary("Régénération clés", ok, n, fails), fails.Count == 0 ? InformationType.Success : InformationType.Warning, 7000, InformationPosition.Top);
        }

        private static string BuildSummary(string title, int ok, int total, List<string> fails)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{title} : {ok}/{total} réussis");
            if (fails.Count > 0)
            {
                sb.AppendLine("Échecs :");
                foreach (var f in fails.Take(15)) sb.AppendLine($"- {f}");
                if (fails.Count > 15) sb.AppendLine($"… (+{fails.Count - 15} autres)");
            }
            return sb.ToString();
        }
    }
}
