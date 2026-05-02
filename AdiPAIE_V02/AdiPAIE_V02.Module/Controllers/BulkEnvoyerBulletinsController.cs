using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Services;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class BulkEnvoyerBulletinsController
        : ObjectViewController<ObjectView, PeriodePaie>
    {
        private readonly SimpleAction _openParams;

        public BulkEnvoyerBulletinsController()
        {
            _openParams = new SimpleAction(this,
                "BulkBulletin_OuvrirParams",
                PredefinedCategory.RecordEdit)
            {
                Caption = "Envoyer bulletins",
                ImageName = "MailMerge",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Envoyer les bulletins du mois par e-mail."
            };
            _openParams.Execute += OnOuvrirParams;
        }

        // ── POPUP 1 : Paramètres ─────────────────────────────────────────
        private void OnOuvrirParams(object sender, SimpleActionExecuteEventArgs e)
        {
            var periode = (View is DetailView)
                ? View.CurrentObject as PeriodePaie
                : View.SelectedObjects?.OfType<PeriodePaie>().SingleOrDefault();

            if (periode == null)
            {
                Application.ShowViewStrategy.ShowMessage(
                    "Sélectionnez une période de paie.",
                    InformationType.Warning, 3000, InformationPosition.Top);
                return;
            }

            var npos = Application.CreateObjectSpace(typeof(BulkSendParams));
            var prm = npos.CreateObject<BulkSendParams>();
            prm.PeriodeOid = periode.Oid;
            prm.PeriodeCaption = periode.DisplayName;
            prm.OnlyValidated = true;
            prm.DryRun = true;
            prm.OverrideEmail = string.Empty;

            var dv = Application.CreateDetailView(npos, prm, true);
            dv.ViewEditMode = DevExpress.ExpressApp.Editors.ViewEditMode.Edit;
            dv.Caption = $"Paramètres d'envoi — {periode.DisplayName}";

            var svp = new ShowViewParameters
            {
                CreatedView = dv,
                TargetWindow = TargetWindow.NewModalWindow
            };

            var dc = Application.CreateController<DialogController>();
            dc.SaveOnAccept = false;
            dc.AcceptAction.Caption = "Aperçu >";
            dc.AcceptAction.Execute += (_, __) =>
            {
                if (prm.DryRun && string.IsNullOrWhiteSpace(prm.OverrideEmail))
                {
                    Application.ShowViewStrategy.ShowMessage(
                        "Renseignez l'e-mail de test (Dry-run activé).",
                        InformationType.Warning, 3000, InformationPosition.Top);
                    return;
                }
                AfficherPopupApercu(prm);
            };

            svp.Controllers.Add(dc);
            Application.ShowViewStrategy.ShowView(
                svp, new ShowViewSource(Frame, null));
        }

        // ── POPUP 2 : Aperçu + envoi ─────────────────────────────────────
        private void AfficherPopupApercu(BulkSendParams prm)
        {
            using var os = Application.CreateObjectSpace(typeof(Bulletin));
            var periode = os.GetObjectByKey<PeriodePaie>(prm.PeriodeOid);
            if (periode == null) return;

            CriteriaOperator crit = CriteriaOperator.Parse(
                "Annee = ? AND Mois = ?", periode.Annee, periode.Mois);

            if (prm.OnlyValidated)
                crit = new GroupOperator(GroupOperatorType.And, crit,
                    new BinaryOperator("Statut",
                        Domain.DomainEnums.BulletinStatut.Valide));

            var bulletins = os.GetObjects<Bulletin>(crit);

            var npos = Application.CreateObjectSpace(typeof(BulkSendPreviewItem));
            var cs = new CollectionSource(npos, typeof(BulkSendPreviewItem));

            foreach (var b in bulletins)
            {
                var item = npos.CreateObject<BulkSendPreviewItem>();
                item.BulletinOid = b.Oid;
                item.Periode = $"{b.Mois:D2}/{b.Annee}";
                item.Matricule = b.Salarie?.Matricule;
                item.FullName = b.Salarie?.FullName;
                item.Email = b.Salarie?.Email;
                item.HasArchive = b.PdfArchive != null && b.PdfArchive.Size > 0;
                item.FileName = $"Bulletin_{b.Periode}_{b.Salarie?.Matricule}.pdf";

                var emailOk = !string.IsNullOrWhiteSpace(b.Salarie?.Email);
                var cleOk = !string.IsNullOrEmpty(b.Salarie?.PayslipKeyEnc);
                item.Selected = emailOk && cleOk;
                item.Note =
                    !emailOk ? "Email manquant" :
                    !cleOk ? "Clé RGPD absente" :
                    item.HasArchive ? "Archive existante" : "À générer";

                cs.Add(item);
            }

            var viewId = Application.FindListViewId(typeof(BulkSendPreviewItem));
            var lv = Application.CreateListView(viewId, cs, false);
            lv.Caption = prm.DryRun
                ? $"Aperçu [DRY-RUN → {prm.OverrideEmail}] — {prm.PeriodeCaption}"
                : $"Aperçu [RÉEL] — {prm.PeriodeCaption}";

            var svp = new ShowViewParameters
            {
                CreatedView = lv,
                TargetWindow = TargetWindow.NewModalWindow
            };

            var dc = Application.CreateController<DialogController>();
            dc.SaveOnAccept = false;

            // AcceptAction renommé "Envoyer" — déclenche l'envoi
            // Le popup se ferme PUIS le rapport s'affiche dans un nouveau popup
            dc.AcceptAction.Caption = prm.DryRun
                ? "Envoyer (Dry-run)"
                : "Envoyer les sélectionnés";

            dc.AcceptAction.Execute += (_, __) =>
            {
                var pick = cs.List.Cast<BulkSendPreviewItem>()
                    .Where(x => x.Selected && !string.IsNullOrWhiteSpace(x.Email))
                    .Select(x => x.BulletinOid)
                    .ToList();

                if (pick.Count == 0)
                {
                    Application.ShowViewStrategy.ShowMessage(
                        "Aucun bulletin sélectionné avec un email valide.",
                        InformationType.Warning, 3000, InformationPosition.Top);
                    return;
                }

                // Fire-and-forget : le popup se ferme, l'envoi continue en arrière-plan
                // Le rapport s'affiche dans un nouveau popup une fois terminé
                _ = Task.Run(async () =>
                {
                    var results = await BulkBulletinSenderService.SendSpecificAsync(
                        Application, pick, prm.DryRun, prm.OverrideEmail,
                        (s, t, info) => Application.ShowViewStrategy.ShowMessage(
                            $"Envoi {s}/{t}… {info}",
                            InformationType.Info, 1000, InformationPosition.Top));

                    var ok = results.Count(r => r.Ok);
                    var ko = results.Where(r => !r.Ok).ToList();
                    var sb = new StringBuilder();
                    sb.AppendLine($"Période : {prm.PeriodeCaption}");
                    sb.AppendLine($"Mode    : {(prm.DryRun ? $"Dry-run → {prm.OverrideEmail}" : "Envoi réel")}");
                    sb.AppendLine($"Succès  : {ok}  /  Échecs : {ko.Count}");
                    if (ko.Any())
                    {
                        sb.AppendLine();
                        sb.AppendLine("Détail des échecs :");
                        foreach (var r in ko.Take(20))
                            sb.AppendLine($"  • {r.Matricule} ({r.To}) : {r.Error}");
                        if (ko.Count > 20)
                            sb.AppendLine($"  … (+{ko.Count - 20} autres)");
                    }

                    // Afficher le rapport dans un popup dédié
                    Application.ShowViewStrategy.ShowMessage(
                        sb.ToString(),
                        ko.Count == 0 ? InformationType.Success : InformationType.Warning,
                        30000,  // 30 secondes — suffisant pour lire le rapport
                        InformationPosition.Top);
                });
            };

            dc.CancelAction.Caption = "Annuler";
            svp.Controllers.Add(dc);

            Application.ShowViewStrategy.ShowView(
                svp, new ShowViewSource(Frame, null));
        }
    }
}
