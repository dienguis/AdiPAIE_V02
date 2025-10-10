using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Envoi de tous les bulletins d'une période depuis PeriodePaie (DetailView ou ListView).
    /// </summary>
    public sealed class BulkEnvoyerBulletinsController
        : ObjectViewController<ObjectView, PeriodePaie>
    {
        private readonly SimpleAction _sendAll;

        public BulkEnvoyerBulletinsController()
        {
            _sendAll = new SimpleAction(this, "EnvoyerTousBulletinsMois", PredefinedCategory.RecordEdit)
            {
                Caption = "Envoyer tous les bulletins (mois)",
                ImageName = "MailMerge",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            _sendAll.Execute += OnExecuteAsync;
        }

        private async void OnExecuteAsync(object s, SimpleActionExecuteEventArgs e)
        {
            // 1) Récupérer la période courante (DetailView) ou la période sélectionnée (ListView)
            PeriodePaie periode = null;

            if (View is DetailView)
            {
                periode = View.CurrentObject as PeriodePaie;
            }
            else // ListView
            {
                // prends exactement UNE période sélectionnée
                periode = View.SelectedObjects?.OfType<PeriodePaie>().SingleOrDefault();
            }

            if (periode == null)
            {
                Application.ShowViewStrategy.ShowMessage(
                    "Sélectionnez une période de paie (une seule) ou ouvrez sa fiche.",
                    InformationType.Warning, 3000, InformationPosition.Top);
                return;
            }

            // 2) Constituer les paramètres (tu peux changer DryRun/OnlyValidated ici)
            var prm = new BulkSendParams
            {
                PeriodeOid = periode.Oid,
                PeriodeCaption = periode.DisplayName,
                OnlyValidated = true,                 // n'envoie que les bulletins 'Validé'
                DryRun = true,                 // ← passe à false en prod
                OverrideEmail = "tests@exemple.com"   // destinataire de test en dry-run
            };

            // 3) Progress UI (toast)
            void Progress(int sent, int tot, string info)
            {
                Application.ShowViewStrategy.ShowMessage(
                    $"Envoi {sent}/{tot}… {info}",
                    InformationType.Info, 1200, InformationPosition.Top);
            }

            // 4) Envoi (tout le mois) via le wrapper
            var results = await BulkBulletinSenderService.SendAllForPeriodAsync(
                Application,
                prm.PeriodeOid,          // ✅ Guid de la période
                prm.OnlyValidated,
                prm.DryRun,
                prm.OverrideEmail,
                Progress);

            // 5) Bilan lisible
            var ok = results.Where(r => r.Ok).ToList();
            var ko = results.Where(r => !r.Ok).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Période : {prm.PeriodeCaption}");
            sb.AppendLine($"Dry-run : {prm.DryRun} {(prm.DryRun ? $"→ {prm.OverrideEmail}" : "")}");
            sb.AppendLine($"Succès : {ok.Count} / Échecs : {ko.Count}");
            if (ko.Count > 0)
            {
                sb.AppendLine("Échecs :");
                foreach (var r in ko.Take(20)) // limite l'affichage
                    sb.AppendLine($"- {r.Matricule} → {r.To}: {r.Error}");
                if (ko.Count > 20) sb.AppendLine($"… (+{ko.Count - 20} autres)");
            }

            Application.ShowViewStrategy.ShowMessage(
                sb.ToString(),
                ko.Count == 0 ? InformationType.Success : InformationType.Warning,
                7000, InformationPosition.Top);
        }
    }
}
