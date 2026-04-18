using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Controllers;
using AdiPAIE_V02.Module.Services;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;


namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Validation hiérarchique des demandes de congé.
    /// Boutons visibles uniquement pour le valideur attendu.
    /// En cas de rejet → repasse en Brouillon (salarié peut modifier + resoumettre).
    /// </summary>
    public class CongeHierarchieWorkflowController
        : ObjectViewController<ListView, CongeDemande>
    {
        readonly SimpleAction validerAction;
        readonly SimpleAction rejeterAction;

        public CongeHierarchieWorkflowController()
        {
            validerAction = new SimpleAction(this, "Conge_Hierarchie_Valider", PredefinedCategory.Edit)
            {
                Caption = "Valider (hiérarchie)",
                ImageName = "Action_Approve",
                ToolTip = "Valide et transmet au niveau suivant ou au RH.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                ConfirmationMessage = "Valider cette demande de congé ?"
            };
            validerAction.Execute += ValiderAction_Execute;

            rejeterAction = new SimpleAction(this, "Conge_Hierarchie_Rejeter", PredefinedCategory.Edit)
            {
                Caption = "Rejeter (hiérarchie)",
                ImageName = "Action_Cancel",
                ToolTip = "Rejette et repasse en Brouillon — le salarié peut modifier.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                ConfirmationMessage = "Rejeter cette demande ? Le salarié pourra la modifier et resoumettre."
            };
            rejeterAction.Execute += RejeterAction_Execute;
        }

        // ── Handlers ─────────────────────────────────────────

        void ValiderAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (CongeDemande)e.CurrentObject;
            var ancienStatut = d.Statut.ToString();

            if (d.Statut == CongeStatut.EnAttenteN1)
            {
                d.ValiderN1();
                if (d.Statut == CongeStatut.EnAttenteN2)
                    _NotifierValideurN2(d);
                else
                    _NotifierRH(d);
            }
            else if (d.Statut == CongeStatut.EnAttenteN2)
            {
                d.ValiderN2();
                _NotifierRH(d);
            }

            AuditService.Enregistrer(Application, "CongeDemande", "Valider",
                d.Oid.ToString(), d.Salarie?.FullName,
                $"Demande du {d.DateDebut:dd/MM/yyyy} au {d.DateFin:dd/MM/yyyy} validée",
                ancienStatut: ancienStatut, nouveauStatut: d.Statut.ToString());
            ObjectSpace.CommitChanges();
            View.Refresh();
        }

        void RejeterAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (CongeDemande)e.CurrentObject;
            var ancienStatut = d.Statut.ToString();

            // Libérer la réservation de solde si elle avait été faite
            if (d.SoldeVerifie && d.Type != null && d.Salarie != null)
            {
                SoldeCongeCalculService.LibererReservation(
                    ObjectSpace, d.Salarie, d.Type,
                    d.DureeJours, d.DateDebut.Year);
                d.SoldeVerifie = false;
            }

            d.RejeterHierarchie(d.MotifRejet);

            // Notifie le salarié que sa demande est rejetée mais modifiable
            _NotifierSalarie(d,
                "Votre demande de congé a été rejetée — vous pouvez la modifier",
                $"Votre demande du {d.DateDebut:dd/MM/yyyy} au {d.DateFin:dd/MM/yyyy} "
                + $"a été rejetée par {d.RejeteParNom}. "
                + (string.IsNullOrWhiteSpace(d.MotifRejet) ? "" : "Motif : " + d.MotifRejet + ". ")
                + "Vous pouvez la modifier et la resoumettre depuis votre espace salarié.");

            AuditService.Enregistrer(Application, "CongeDemande", "Rejeter",
                d.Oid.ToString(), d.Salarie?.FullName,
                $"Demande du {d.DateDebut:dd/MM/yyyy} au {d.DateFin:dd/MM/yyyy} rejetée — Motif : {d.MotifRejet ?? "aucun motif enregistré"}",
                ancienStatut: ancienStatut, nouveauStatut: d.Statut.ToString());
            ObjectSpace.CommitChanges();
            PlanningCongeController.MettreAJourEvenement(ObjectSpace, d);
            View.Refresh();
        }

        // ── Activation conditionnelle ─────────────────────────

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
            var d = View?.CurrentObject as CongeDemande;
            var salConn = GetSalarieConnecte();

            if (d == null || salConn == null)
            {
                validerAction.Active["sel"] = false;
                rejeterAction.Active["sel"] = false;
                return;
            }

            bool estValideurN1 = d.Statut == CongeStatut.EnAttenteN1
                              && d.ValideurN1?.Oid == salConn.Oid;

            bool estValideurN2 = d.Statut == CongeStatut.EnAttenteN2
                              && d.ValideurN2?.Oid == salConn.Oid;

            bool peutValider = estValideurN1 || estValideurN2;

            validerAction.Active["sel"] = peutValider;
            rejeterAction.Active["sel"] = peutValider;
        }

        // ── Helpers ──────────────────────────────────────────

        private Salarie GetSalarieConnecte()
        {
            try
            {
                // Utilise le helper centralise (3 strategies de detection)
                // pour etre coherent avec EspaceSalarieHelper
                return EspaceSalarieHelper.GetSalarieConnecte(ObjectSpace);
            }
            catch { return null; }
        }

        private void _NotifierValideurN2(CongeDemande demande)
        {
            try
            {
                if (demande.ValideurN2 == null) return;
                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = demande.ValideurN2;
                notif.Titre = "Demande de congé en attente de votre validation (N+2)";
                notif.Corps = $"{demande.Salarie?.FullName} souhaite prendre un congé "
                                + $"du {demande.DateDebut:dd/MM/yyyy} au {demande.DateFin:dd/MM/yyyy} "
                                + $"({demande.DureeJours:n1} jour(s)). "
                                + $"Validée par {demande.ValideurN1?.FullName}.";
                notif.Categorie = "Congé";
                notif.Priorite = NotificationPriorite.Important;
                ObjectSpace.CommitChanges();
                _EnvoyerEmailAsync(notif);
            }
            catch { }
        }

        private void _NotifierRH(CongeDemande demande)
        {
            try
            {
                var prm = ParametresPaie.TryGet(ObjectSpace);
                if (prm == null || !prm.EmailActif) return;

                var destinataires = prm.EmailsRHAlertes?
                    .Split(new[] { ';', ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim()).Where(e => e.Contains('@')).ToList();

                if (destinataires == null || !destinataires.Any()) return;

                var sender = prm.CreateEmailSender();
                var sujet = $"[AdiPAIE] Demande de congé validée — {demande.Salarie?.FullName}";
                var body = $@"<html><body style='font-family:Segoe UI,Arial;font-size:14px;'>
<h2 style='color:#1F4E79;'>Demande de congé — validée par la hiérarchie</h2>
<p>La demande suivante est disponible pour traitement :</p>
<table style='border-collapse:collapse;'>
  <tr><td style='padding:6px 12px;'><b>Salarié</b></td><td>{demande.Salarie?.FullName}</td></tr>
  <tr style='background:#EBF3FB;'><td style='padding:6px 12px;'><b>Type</b></td><td>{demande.Type?.Libelle}</td></tr>
  <tr><td style='padding:6px 12px;'><b>Période</b></td><td>{demande.DateDebut:dd/MM/yyyy} → {demande.DateFin:dd/MM/yyyy} ({demande.DureeJours:n1} j)</td></tr>
</table>
</body></html>";

                foreach (var dest in destinataires)
                    sender.Send(dest, sujet, body);
            }
            catch { }
        }

        private void _NotifierSalarie(CongeDemande demande, string titre, string corps)
        {
            try
            {
                if (demande.Salarie == null) return;
                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = demande.Salarie;
                notif.Titre = titre;
                notif.Corps = corps;
                notif.Categorie = "Congé";
                notif.Priorite = NotificationPriorite.Important;
                ObjectSpace.CommitChanges();
                _EnvoyerEmailAsync(notif);
            }
            catch { }
        }

        private void _EnvoyerEmailAsync(NotificationSalarie notif)
        {
            var notifOid = notif.Oid;
            var osFactory = Application.ServiceProvider
      .GetRequiredService<IObjectSpaceFactory>();

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using var os = osFactory.CreateObjectSpace(typeof(NotificationSalarie));
                    var n = os.GetObjectByKey<NotificationSalarie>(notifOid);
                    if (n != null)
                        AdiPAIE_V02.Module.Services.NotificationEmailService.Envoyer(n, os);
                }
                catch { }
            });
        }
    }
}
