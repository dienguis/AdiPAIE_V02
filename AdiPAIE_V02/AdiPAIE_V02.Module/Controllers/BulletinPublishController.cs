// =============================================================================
//  BulletinPublishController.cs — V1.4.3
//
//  Trois actions sur la liste/détail Bulletin (côté RH) :
//
//    1. Publier      — passe le bulletin Validé → Envoyé (Publié) :
//                      génère le PDF, l'archive, envoie l'email de notification.
//                      Sélection multiple OK (publication en lot).
//
//    2. Dépublier    — repasse Envoyé → Validé. Le bulletin n'est plus
//                      consultable côté Espace Salarié. Le PDF archivé est
//                      conservé pour traçabilité (ré-utilisé à la republication).
//
//    3. Re-notifier  — renvoie l'email d'information au salarié sans
//                      regénérer le PDF (utile si le salarié a perdu le mail
//                      ou si SMTP était KO au moment de la publication).
//
//  Toute la logique métier est dans BulletinPublicationService — ce controller
//  ne fait que la collecte UI + dispatch vers le service.
// =============================================================================
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class BulletinPublishController
        : ObjectViewController<ObjectView, Bulletin>
    {
        private readonly SimpleAction _publier;
        private readonly SimpleAction _depublier;
        private readonly SimpleAction _renotifier;

        public BulletinPublishController()
        {
            _publier = new SimpleAction(this, "PublierBulletin", PredefinedCategory.Edit)
            {
                Caption = "Publier",
                ImageName = "BO_Mail",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Publie le(s) bulletin(s) validé(s) dans l'Espace Salarié + envoi email d'information.",
                SelectionDependencyType = SelectionDependencyType.Independent,
                ConfirmationMessage = "Publier le(s) bulletin(s) sélectionné(s) ?\n"
                    + "Le PDF sera archivé, le statut passera à Envoyé, et un email "
                    + "de notification sera envoyé à chaque salarié."
            };
            _publier.Execute += OnPublier;

            _depublier = new SimpleAction(this, "DepublierBulletin", PredefinedCategory.Edit)
            {
                Caption = "Dépublier",
                ImageName = "Action_Reset",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Repasse le(s) bulletin(s) du statut Envoyé à Validé. "
                    + "Le salarié ne pourra plus le consulter dans son Espace.",
                SelectionDependencyType = SelectionDependencyType.Independent,
                ConfirmationMessage = "Dépublier le(s) bulletin(s) sélectionné(s) ?"
            };
            _depublier.Execute += OnDepublier;

            _renotifier = new SimpleAction(this, "RenotifierBulletin", PredefinedCategory.Edit)
            {
                Caption = "Notifier",
                ImageName = "BO_Mail",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Envoie (ou renvoie) l'email d'information aux salariés "
                    + "sans regénérer le PDF. Utile si la notification initiale "
                    + "a échoué ou si le salarié a perdu le mail.",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _renotifier.Execute += OnRenotifier;
        }

        // =====================================================================
        // PUBLIER — single ou bulk (async pour ne pas bloquer l'UI Blazor)
        // =====================================================================
        private async void OnPublier(object sender, SimpleActionExecuteEventArgs e)
        {
            var cibles = ResoudreCibles(e);
            if (cibles.Count == 0)
            {
                ShowMessageWarn("Aucun bulletin sélectionné.");
                return;
            }

            var logger = TryGetLogger();
            int publies = 0, ignores = 0, erreurs = 0;
            var details = new List<string>();

            foreach (var b in cibles)
            {
                try
                {
                    if (b.Statut < BulletinStatut.Valide)
                    {
                        ignores++;
                        details.Add($"⚠ {b.Salarie?.Matricule} {b.Salarie?.FullName} : pas encore validé.");
                        continue;
                    }

                    var changed = await BulletinPublicationService.PublierAsync(
                        b, ObjectSpace,
                        currentUserName: SafeCurrentUser(),
                        forceRegenererPdf: false,
                        logger: logger);

                    if (changed) publies++;
                    else ignores++;
                }
                catch (UserFriendlyException ufx)
                {
                    erreurs++;
                    details.Add($"❌ {b.Salarie?.Matricule} : {ufx.Message}");
                }
                catch (Exception ex)
                {
                    erreurs++;
                    details.Add($"❌ {b.Salarie?.Matricule} : {ex.Message}");
                    logger?.LogError(ex, "Échec publication bulletin {Oid}.", b.Oid);
                }
            }

            View?.ObjectSpace?.Refresh();

            var resume = $"{publies} bulletin(s) publié(s)"
                + (ignores > 0 ? $", {ignores} ignoré(s)" : "")
                + (erreurs > 0 ? $", {erreurs} erreur(s)" : "")
                + ".";

            var info = erreurs > 0
                ? InformationType.Warning
                : (publies > 0 ? InformationType.Success : InformationType.Info);

            Application.ShowViewStrategy.ShowMessage(resume, info, 6000, InformationPosition.Top);

            if (details.Count > 0 && erreurs > 0)
            {
                Application.ShowViewStrategy.ShowMessage(
                    string.Join("\n", details.Take(5)),
                    InformationType.Warning, 8000, InformationPosition.Top);
            }
        }

        // =====================================================================
        // DEPUBLIER — single ou bulk
        // =====================================================================
        private void OnDepublier(object sender, SimpleActionExecuteEventArgs e)
        {
            var cibles = ResoudreCibles(e);
            if (cibles.Count == 0)
            {
                ShowMessageWarn("Aucun bulletin sélectionné.");
                return;
            }

            var logger = TryGetLogger();
            int depubliees = 0, ignores = 0, erreurs = 0;

            foreach (var b in cibles)
            {
                try
                {
                    if (b.Statut != BulletinStatut.Envoye)
                    {
                        ignores++;
                        continue;
                    }
                    BulletinPublicationService.Depublier(b, ObjectSpace,
                        currentUserName: SafeCurrentUser(), logger: logger);
                    depubliees++;
                }
                catch (Exception ex)
                {
                    erreurs++;
                    logger?.LogError(ex, "Échec dépublication bulletin {Oid}.", b.Oid);
                }
            }

            View?.ObjectSpace?.Refresh();

            var resume = $"{depubliees} bulletin(s) dépublié(s)"
                + (ignores > 0 ? $", {ignores} ignoré(s)" : "")
                + (erreurs > 0 ? $", {erreurs} erreur(s)" : "")
                + ".";
            var info = erreurs > 0
                ? InformationType.Warning
                : (depubliees > 0 ? InformationType.Success : InformationType.Info);

            Application.ShowViewStrategy.ShowMessage(resume, info, 5000, InformationPosition.Top);
        }

        // =====================================================================
        // NOTIFIER — single ou bulk (async pour ne pas bloquer l'UI Blazor)
        // =====================================================================
        private async void OnRenotifier(object sender, SimpleActionExecuteEventArgs e)
        {
            var cibles = ResoudreCibles(e);
            if (cibles.Count == 0)
            {
                ShowMessageWarn("Aucun bulletin sélectionné.");
                return;
            }

            var logger = TryGetLogger();
            int envoyes = 0, ignores = 0, erreurs = 0;

            foreach (var b in cibles)
            {
                try
                {
                    if (b.Statut < BulletinStatut.Envoye)
                    {
                        ignores++;
                        continue;
                    }
                    var sent = await BulletinPublicationService.EnvoyerNotificationAsync(b, ObjectSpace, logger);
                    if (sent) envoyes++;
                    else ignores++;
                }
                catch (Exception ex)
                {
                    erreurs++;
                    logger?.LogError(ex, "Échec re-notification bulletin {Oid}.", b.Oid);
                }
            }

            var resume = $"{envoyes} email(s) renvoyé(s)"
                + (ignores > 0 ? $", {ignores} ignoré(s)" : "")
                + (erreurs > 0 ? $", {erreurs} erreur(s)" : "")
                + ".";
            var info = erreurs > 0
                ? InformationType.Warning
                : (envoyes > 0 ? InformationType.Success : InformationType.Info);

            Application.ShowViewStrategy.ShowMessage(resume, info, 5000, InformationPosition.Top);
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        private List<Bulletin> ResoudreCibles(SimpleActionExecuteEventArgs e)
        {
            // DetailView : objet courant ; ListView : sélection
            if (View is DetailView)
            {
                return View.CurrentObject is Bulletin b ? new List<Bulletin> { b } : new List<Bulletin>();
            }
            return e.SelectedObjects?.OfType<Bulletin>().ToList() ?? new List<Bulletin>();
        }

        private string SafeCurrentUser()
        {
            try { return SecuritySystem.CurrentUserName ?? "(système)"; }
            catch { return "(système)"; }
        }

        private void ShowMessageWarn(string msg) =>
            Application.ShowViewStrategy.ShowMessage(msg, InformationType.Warning, 4000, InformationPosition.Top);

        private ILogger TryGetLogger()
        {
            try { return Application?.ServiceProvider?.GetService<ILoggerFactory>()?
                .CreateLogger<BulletinPublishController>(); }
            catch { return null; }
        }
    }
}
