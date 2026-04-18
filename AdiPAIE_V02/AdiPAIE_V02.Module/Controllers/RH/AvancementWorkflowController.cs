using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Workflow des demandes d'avancement.
    ///
    /// Qui fait quoi :
    ///   RH        → Soumettre, Appliquer, Annuler
    ///   DG        → Approuver, Rejeter
    ///
    /// À l'application :
    ///   1. Fiche salarié mise à jour (Fonction, Département, Échelon, Salaire)
    ///   2. HistoriquePoste créé (ferme l'ancien, ouvre le nouveau)
    ///   3. Notification in-app au salarié
    ///   4. Email RH/DG envoyé
    /// </summary>
    public class AvancementWorkflowController
        : ObjectViewController<DetailView, DemandeAvancement>
    {
        readonly SimpleAction preRemplirAction;
        readonly SimpleAction soumettreAction;
        readonly SimpleAction approuverAction;
        readonly SimpleAction rejeterAction;
        readonly SimpleAction appliquerAction;
        readonly SimpleAction annulerAction;

        public AvancementWorkflowController()
        {
            // ── Pré-remplir depuis salarié ─────────────────────
            preRemplirAction = new SimpleAction(this,
                "Avancement_PreRemplir", PredefinedCategory.Edit)
            {
                Caption = "Charger situation actuelle",
                ImageName = "Action_Refresh",
                ToolTip = "Recharge la situation actuelle depuis la fiche salarié.",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AvancementStatut,Brouillon#"
            };
            preRemplirAction.Execute += (s, e) =>
            {
                var d = (DemandeAvancement)View.CurrentObject;
                if (d.Salarie == null)
                    throw new UserFriendlyException("Sélectionnez d'abord un salarié.");
                d.ChargerSituationActuelle();
                ObjectSpace.CommitChanges();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Situation actuelle rechargée.",
                    InformationType.Success, 2000, InformationPosition.Top);
            };

            // ── Soumettre au DG ───────────────────────────────
            soumettreAction = new SimpleAction(this,
                "Avancement_Soumettre", PredefinedCategory.Edit)
            {
                Caption = "Soumettre pour approbation",
                ImageName = "Action_Forward",
                ConfirmationMessage = "Soumettre cette demande d'avancement à la direction ?",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AvancementStatut,Brouillon#"
            };
            soumettreAction.Execute += (s, e) =>
            {
                var d = (DemandeAvancement)View.CurrentObject;
                if (d.Salarie == null)
                    throw new UserFriendlyException("Salarié obligatoire.");
                if (d.NouveauSalaireBase <= 0 && d.NouvelleFonction == null)
                    throw new UserFriendlyException(
                        "Renseignez au moins la nouvelle fonction ou le nouveau salaire.");

                // Générer la référence si absente
                if (string.IsNullOrWhiteSpace(d.Reference))
                    d.Reference = GenererReference(d);

                d.Soumettre();

                // Notifier le DG par email
                var rhEmails = WorkflowEmailHelper.ExtraireEmailsRH(Application);
                if (rhEmails.Any())
                {
                    var body = WorkflowEmailHelper.HtmlTableau(
                        "Demande d'avancement à approuver",
                        "Une demande d'avancement attend votre approbation.",
                        new[]
                        {
                            ("Référence",    d.Reference),
                            ("Salarié",      d.Salarie?.FullName ?? "—"),
                            ("Type",         d.TypeAvancement.ToString()),
                            ("Date d'effet", d.DateEffet.ToString("dd/MM/yyyy")),
                            ("Fonction actuelle", d.FonctionActuelle?.Intitule ?? "—"),
                            ("Nouvelle fonction", d.NouvelleFonction?.Intitule ?? "—"),
                            ("Salaire actuel",    d.SalaireActuel.ToString("N0") + " FCFA"),
                            ("Nouveau salaire",   d.NouveauSalaireBase.ToString("N0") + " FCFA"),
                            ("Variation",         d.VariationSalairePercent.ToString("N1") + " %"),
                        });
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application, rhEmails,
                        $"[AdiPAIE] Avancement à approuver — {d.Salarie?.FullName}", body);
                }

                AuditService.Enregistrer(Application, "DemandeAvancement", "Soumettre",
                    d.Oid.ToString(), d.Reference ?? d.DisplayAvancement,
                    $"Avancement {d.TypeAvancement} pour {d.Salarie?.FullName}",
                    ancienStatut: AvancementStatut.Brouillon.ToString(),
                    nouveauStatut: AvancementStatut.SoumisRH.ToString());

                ObjectSpace.CommitChanges();
                UpdateActions(); View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Demande soumise à la direction.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Approuver (DG) ────────────────────────────────
            approuverAction = new SimpleAction(this,
                "Avancement_Approuver", PredefinedCategory.Edit)
            {
                Caption = "Approuver",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Approuver cet avancement ?",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AvancementStatut,SoumisRH#"
            };
            approuverAction.Execute += (s, e) =>
            {
                var d = (DemandeAvancement)View.CurrentObject;
                var dg = _GetSalarieConnecte();
                d.ApprouverDG(dg);

                // Notifier le RH
                var rhEmails = WorkflowEmailHelper.ExtraireEmailsRH(Application);
                if (rhEmails.Any())
                {
                    var body = WorkflowEmailHelper.HtmlTableau(
                        "Avancement approuvé — à appliquer",
                        "La demande a été approuvée. Vous pouvez maintenant l'appliquer.",
                        new[]
                        {
                            ("Référence",    d.Reference),
                            ("Salarié",      d.Salarie?.FullName ?? "—"),
                            ("Approuvé par", dg?.FullName ?? "Direction"),
                            ("Date d'effet", d.DateEffet.ToString("dd/MM/yyyy")),
                            ("Nouveau salaire", d.NouveauSalaireBase.ToString("N0") + " FCFA"),
                        });
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application, rhEmails,
                        $"[AdiPAIE] Avancement approuvé — {d.Salarie?.FullName}", body);
                }

                AuditService.Enregistrer(Application, "DemandeAvancement", "Approuver",
                    d.Oid.ToString(), d.Reference ?? d.DisplayAvancement,
                    $"Approuvé par {dg?.FullName ?? "Direction"}",
                    ancienStatut: AvancementStatut.SoumisRH.ToString(),
                    nouveauStatut: AvancementStatut.ApprouveDG.ToString());

                ObjectSpace.CommitChanges();
                UpdateActions(); View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Avancement approuvé. Vous pouvez l'appliquer.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Rejeter (DG) ──────────────────────────────────
            rejeterAction = new SimpleAction(this,
                "Avancement_Rejeter", PredefinedCategory.Edit)
            {
                Caption = "Rejeter",
                ImageName = "Action_Cancel",
                ConfirmationMessage = "Rejeter cette demande d'avancement ?",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AvancementStatut,SoumisRH#"
            };
            rejeterAction.Execute += (s, e) =>
            {
                var d = (DemandeAvancement)View.CurrentObject;
                if (string.IsNullOrWhiteSpace(d.MotifRejet))
                    throw new UserFriendlyException(
                        "Saisissez un motif de rejet dans le champ Motif de rejet avant de rejeter.");
                d.Rejeter(d.MotifRejet);

                AuditService.Enregistrer(Application, "DemandeAvancement", "Rejeter",
                    d.Oid.ToString(), d.Reference ?? d.DisplayAvancement,
                    $"Motif: {d.MotifRejet}",
                    ancienStatut: AvancementStatut.SoumisRH.ToString(),
                    nouveauStatut: AvancementStatut.Rejete.ToString());

                ObjectSpace.CommitChanges();
                UpdateActions(); View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Demande rejetée.",
                    InformationType.Warning, 3000, InformationPosition.Top);
            };

            // ── Appliquer (RH) — MAJ fiche salarié ────────────
            appliquerAction = new SimpleAction(this,
                "Avancement_Appliquer", PredefinedCategory.Edit)
            {
                Caption = "Appliquer sur la fiche salarié",
                ImageName = "Action_Grant",
                ConfirmationMessage =
                    "Appliquer l'avancement ? La fiche salarié sera mise à jour immédiatement.",
                TargetObjectsCriteria =
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AvancementStatut,ApprouveDG#"
            };
            appliquerAction.Execute += AppliquerAction_Execute;

            // ── Annuler ───────────────────────────────────────
            annulerAction = new SimpleAction(this,
                "Avancement_Annuler", PredefinedCategory.Edit)
            {
                Caption = "Annuler",
                ImageName = "Action_Delete",
                ConfirmationMessage = "Annuler cette demande ?",
                TargetObjectsCriteria =
                    "Statut != ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AvancementStatut,Applique# AND " +
                    "Statut != ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AvancementStatut,Annule#"
            };
            annulerAction.Execute += (s, e) =>
            {
                var d = (DemandeAvancement)View.CurrentObject;
                var ancienStatut = d.Statut;
                d.Annuler();

                AuditService.Enregistrer(Application, "DemandeAvancement", "Annuler",
                    d.Oid.ToString(), d.Reference ?? d.DisplayAvancement,
                    "Demande annulée",
                    ancienStatut: ancienStatut.ToString(),
                    nouveauStatut: AvancementStatut.Annule.ToString());

                ObjectSpace.CommitChanges();
                UpdateActions(); View.Refresh();
            };
        }

        // ════════════════════════════════════════════════════════
        // HANDLER APPLIQUER — cœur du module
        // ════════════════════════════════════════════════════════
        void AppliquerAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (DemandeAvancement)View.CurrentObject;
            try
            {
                var sal = d.Salarie
                    ?? throw new UserFriendlyException("Salarié introuvable.");

                // ── 1. Créer l'historique AVANT la mise à jour ────
                HistoriquePoste.CreerDepuisAvancement(ObjectSpace, d);

                // ── 2. Mettre à jour la fiche salarié ─────────────
                if (d.NouvelleFonction != null)
                    sal.Fonction = d.NouvelleFonction;

                if (d.NouveauDepartement != null)
                    sal.Departement = d.NouveauDepartement;

                if (d.NouvelEchelon != null)
                    sal.Echelon = d.NouvelEchelon;
                // L'échelon met à jour SalaireBase + Indemnite automatiquement
                // via les Appearance AllowEdit=False quand Echelon non null.
                // Si pas d'échelon, on force manuellement :
                else
                {
                    if (d.NouveauSalaireBase > 0)
                        sal.SalaireBase = d.NouveauSalaireBase;
                    if (d.NouvelleIndemnite >= 0)
                        sal.IndemniteLogement = d.NouvelleIndemnite;
                }

                // ── 3. Mettre à jour le statut avancement ─────────
                var detailsChangement = $"Fonction: {sal.Fonction?.Intitule ?? "—"}, " +
                    $"Salaire: {sal.SalaireBase:N0} FCFA, " +
                    $"Indemnité: {sal.IndemniteLogement:N0} FCFA, " +
                    $"Département: {sal.Departement?.Nom ?? "—"}, " +
                    $"Échelon: {sal.Echelon?.Code ?? "—"}";

                d.Statut = AvancementStatut.Applique;
                d.DateApplication = DateTime.Now;
                try { d.AppliquePar = SecuritySystem.CurrentUserName; } catch { }

                // ── 4. Notification in-app au salarié ─────────────
                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = sal;
                notif.Titre = $"Votre avancement a été appliqué — {d.TypeAvancement}";
                notif.Corps = $"À compter du {d.DateEffet:dd/MM/yyyy}, votre situation est mise à jour : "
                                + $"Fonction : {sal.Fonction?.Intitule ?? "—"}, "
                                + $"Salaire : {sal.SalaireBase:N0} FCFA.";
                notif.Categorie = "Avancement";
                notif.Priorite = NotificationPriorite.Important;

                AuditService.Enregistrer(Application, "DemandeAvancement", "Appliquer",
                    d.Oid.ToString(), d.Reference ?? d.DisplayAvancement,
                    detailsChangement,
                    ancienStatut: AvancementStatut.ApprouveDG.ToString(),
                    nouveauStatut: AvancementStatut.Applique.ToString());

                ObjectSpace.CommitChanges();
                UpdateActions(); View.Refresh();

                // ── 5. Email au salarié ────────────────────────────
                WorkflowEmailHelper.EnvoyerNotifAsync(Application, notif);

                Application.ShowViewStrategy?.ShowMessage(
                    $"Avancement appliqué. Fiche salarié mise à jour.",
                    InformationType.Success, 4000, InformationPosition.Top);
            }
            catch (UserFriendlyException) { throw; }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }

        // ════════════════════════════════════════════════════════
        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateActions();
            View.CurrentObjectChanged += (_, __) => UpdateActions();
        }

        void UpdateActions()
        {
            var d = View?.CurrentObject as DemandeAvancement;
            if (d == null) return;
            var s = d.Statut;

            preRemplirAction.Active["s"] = s == AvancementStatut.Brouillon;
            soumettreAction.Active["s"] = s == AvancementStatut.Brouillon;
            approuverAction.Active["s"] = s == AvancementStatut.SoumisRH;
            rejeterAction.Active["s"] = s == AvancementStatut.SoumisRH;
            appliquerAction.Active["s"] = s == AvancementStatut.ApprouveDG;
            annulerAction.Active["s"] = s != AvancementStatut.Applique
                                         && s != AvancementStatut.Annule;
        }

        string GenererReference(DemandeAvancement d)
        {
            var prefix = d.TypeAvancement switch
            {
                TypeAvancement.Promotion => "PROM",
                TypeAvancement.AvancementEchelon => "AVCE",
                TypeAvancement.Augmentation => "AUG",
                TypeAvancement.ChangementDept => "DEPT",
                _ => "AVA"
            };
            var annee = DateTime.Today.Year;
            var count = ObjectSpace.GetObjectsQuery<DemandeAvancement>()
                .Count(x => x.TypeAvancement == d.TypeAvancement
                         && x.DateEffet.Year == annee
                         && !string.IsNullOrEmpty(x.Reference));
            return $"{prefix}-{annee}-{(count + 1):D4}";
        }

        Salarie _GetSalarieConnecte()
        {
            try
            {
                var userName = DevExpress.ExpressApp.SecuritySystem.CurrentUserName;
                var user = ObjectSpace.GetObjectsQuery<ApplicationUser>()
                    .FirstOrDefault(u => u.UserName == userName);
                return user?.Salarie;
            }
            catch { return null; }
        }
    }
}
