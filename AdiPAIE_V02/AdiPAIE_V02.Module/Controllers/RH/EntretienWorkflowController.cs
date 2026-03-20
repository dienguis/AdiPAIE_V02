using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Workflow entretien annuel — nouveau circuit RH 2026 :
    ///
    ///   Brouillon → [RH : Planifier] → PlanifiéRH
    ///   PlanifiéRH → [RH : Lancer évaluation] → SaisieManager  (notifie N+1)
    ///   SaisieManager → [N+1 : Soumettre au salarié] → SaisieSalarie  (notifie salarié)
    ///   SaisieSalarie → [Salarié : Soumettre observations] → ValidationN1  (notifie N+1)
    ///   ValidationN1 → [N+1 : Valider observations] → EnAttenteN2 ou SoumiseRH
    ///   EnAttenteN2 → [N+2 : Valider] → SoumiseRH
    ///               → [N+2 : Rejeter] → SaisieManager  (notifie N+1)
    ///   SoumiseRH → [RH : Clôturer] → Clôturé
    ///
    /// Verrous :
    ///   SaisieManager  → N+1 modifie tout sauf observations salarié
    ///   SaisieSalarie  → salarié modifie uniquement CommentairesCollaborateur + EvolutionSouhaitee
    ///   ValidationN1   → lecture seule (N+1 consulte et valide)
    ///   SoumiseRH      → RH modifie NotesDecisionRH uniquement
    ///   Clôturé        → tout verrouillé
    /// </summary>
    public class EntretienWorkflowController
        : ObjectViewController<DetailView, EntretienAnnuel>
    {
        // ── Boutons RH ───────────────────────────────────────
        readonly SimpleAction planifierAction;
        readonly SimpleAction lancerEvaluationAction;
        readonly SimpleAction cloturerAction;

        // ── Boutons N+1 ──────────────────────────────────────
        readonly SimpleAction soumettreAuSalarieAction;
        readonly SimpleAction validerObservationsAction;

        // ── Boutons Salarié ───────────────────────────────────
        readonly SimpleAction soumettreObservationsAction;

        // ── Boutons N+2 ──────────────────────────────────────
        readonly SimpleAction validerN2Action;
        readonly SimpleAction rejeterN2Action;

        // ── Utilitaires ───────────────────────────────────────
        readonly SimpleAction recalculerScoreAction;
        readonly SimpleAction genererFicheAction;

        public EntretienWorkflowController()
        {
            // ── RH : Planifier ────────────────────────────────
            planifierAction = new SimpleAction(this,
                "Entretien_Planifier", PredefinedCategory.Edit)
            {
                Caption = "Planifier",
                ImageName = "Action_New",
                ToolTip = "Fixe la date et passe en état Planifié.",
            };
            planifierAction.Execute += (s, e) =>
            {
                var en = (EntretienAnnuel)View.CurrentObject;
                var date = en.DatePlanifiee ?? DateTime.Today.AddDays(7);
                en.Planifier(date);
                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();
            };

            // ── RH : Lancer évaluation ────────────────────────
            lancerEvaluationAction = new SimpleAction(this,
                "Entretien_LancerEvaluation", PredefinedCategory.Edit)
            {
                Caption = "Lancer l'évaluation",
                ImageName = "Action_Send",
                ToolTip = "Envoie le formulaire au N+1 pour évaluation.",
                ConfirmationMessage = "Lancer l'évaluation ? Le N+1 sera notifié."
            };
            lancerEvaluationAction.Execute += LancerEvaluationAction_Execute;

            // ── N+1 : Soumettre au salarié ────────────────────
            soumettreAuSalarieAction = new SimpleAction(this,
                "Entretien_SoumettreAuSalarie", PredefinedCategory.Edit)
            {
                Caption = "Soumettre au salarié",
                ImageName = "Action_Forward",
                ToolTip = "Transmet l'évaluation au salarié pour observations.",
                ConfirmationMessage = "Soumettre au salarié pour observations ?"
            };
            soumettreAuSalarieAction.Execute += SoumettreAuSalarieAction_Execute;

            // ── Salarié : Soumettre observations ──────────────
            soumettreObservationsAction = new SimpleAction(this,
                "Entretien_SoumettreObservations", PredefinedCategory.Edit)
            {
                Caption = "Soumettre mes observations",
                ImageName = "Action_Forward",
                ToolTip = "Soumet vos observations. Le N+1 sera notifié.",
                ConfirmationMessage = "Soumettre vos observations ? Elles ne pourront plus être modifiées."
            };
            soumettreObservationsAction.Execute += SoumettreObservationsAction_Execute;

            // ── N+1 : Valider les observations ────────────────
            validerObservationsAction = new SimpleAction(this,
                "Entretien_ValiderObservations", PredefinedCategory.Edit)
            {
                Caption = "Valider les observations",
                ImageName = "Action_Approve",
                ToolTip = "Valide les observations du salarié et transmet au N+2 ou au RH.",
                ConfirmationMessage = "Valider les observations du salarié ?"
            };
            validerObservationsAction.Execute += ValiderObservationsAction_Execute;

            // ── N+2 : Valider ─────────────────────────────────
            validerN2Action = new SimpleAction(this,
                "Entretien_ValiderN2", PredefinedCategory.Edit)
            {
                Caption = "Valider (N+2)",
                ImageName = "Action_Approve",
                ToolTip = "Valide l'entretien. Il sera transmis au RH pour clôture.",
                ConfirmationMessage = "Valider cet entretien ?"
            };
            validerN2Action.Execute += ValiderN2Action_Execute;

            // ── N+2 : Rejeter ─────────────────────────────────
            rejeterN2Action = new SimpleAction(this,
                "Entretien_RejeterN2", PredefinedCategory.Edit)
            {
                Caption = "Rejeter (N+2)",
                ImageName = "Action_Cancel",
                ToolTip = "Rejette l'entretien. Retour en saisie N+1.",
                ConfirmationMessage = "Rejeter cet entretien ? Le N+1 devra le corriger."
            };
            rejeterN2Action.Execute += RejeterN2Action_Execute;

            // ── RH : Clôturer ─────────────────────────────────
            cloturerAction = new SimpleAction(this,
                "Entretien_Cloturer", PredefinedCategory.Edit)
            {
                Caption = "Clôturer",
                ImageName = "Action_Close",
                ToolTip = "Archive définitivement l'entretien.",
                ConfirmationMessage = "Clôturer cet entretien ? Il passera en lecture seule."
            };
            cloturerAction.Execute += CloturerAction_Execute;

            // ── Recalculer score ──────────────────────────────
            recalculerScoreAction = new SimpleAction(this,
                "Entretien_RecalculerScore", PredefinedCategory.View)
            {
                Caption = "Recalculer le score",
                ImageName = "Action_Refresh",
                ToolTip = "Recalcule le score pondéré à partir des notes.",
            };
            recalculerScoreAction.Execute += (s, e) =>
            {
                var en = (EntretienAnnuel)View.CurrentObject;
                en.RecalculerScore();
                ObjectSpace.CommitChanges();
                View.Refresh();
            };

            // ── Générer fiche ─────────────────────────────────
            genererFicheAction = new SimpleAction(this,
                "Entretien_GenererFiche", PredefinedCategory.View)
            {
                Caption = "Générer la fiche",
                ImageName = "Action_Export",
                ToolTip = "Génère la fiche d'entretien Word/PDF depuis le template.",
            };
            genererFicheAction.Execute += GenererFicheAction_Execute;
        }

        // ── Handlers ─────────────────────────────────────────

        void LancerEvaluationAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var en = (EntretienAnnuel)View.CurrentObject;
            en.LancerEvaluation();

            // Initialise aptitudes si encadrant
            if (en.EstEnSituationEncadrement)
                EntretienManagementInitializer.Initialiser(en,
                    ((DevExpress.ExpressApp.Xpo.XPObjectSpace)ObjectSpace).Session);

            ObjectSpace.CommitChanges();

            // Notifie N+1
            _Notifier(en, en.Evaluateur,
                $"Évaluation à réaliser — {en.Salarie?.FullName}",
                $"L'évaluation annuelle {en.Campagne?.Annee} de {en.Salarie?.FullName} "
                + $"vous a été transmise. Date prévue : {en.DatePlanifiee:dd/MM/yyyy}. "
                + "Connectez-vous sur AdiPAIE pour remplir l'évaluation.");

            UpdateStates(); View.Refresh();
            Application.ShowViewStrategy?.ShowMessage(
                $"Évaluation lancée. {en.Evaluateur?.FullName} a été notifié.",
                InformationType.Success, 3000, InformationPosition.Top);
        }

        void SoumettreAuSalarieAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var en = (EntretienAnnuel)View.CurrentObject;

            if (!en.NoteGlobaleManager.HasValue)
                throw new UserFriendlyException(
                    "Veuillez renseigner la note globale avant de soumettre au salarié.");

            en.SoumettreAuSalarie();
            ObjectSpace.CommitChanges();

            // Notifie le salarié
            _Notifier(en, en.Salarie,
                "Votre évaluation annuelle est disponible",
                $"Votre évaluation annuelle {en.Campagne?.Annee} a été complétée par "
                + $"{en.Evaluateur?.FullName}. Connectez-vous sur AdiPAIE pour consulter "
                + "l'évaluation et saisir vos observations.");

            UpdateStates(); View.Refresh();
            Application.ShowViewStrategy?.ShowMessage(
                $"Évaluation transmise à {en.Salarie?.FullName} pour observations.",
                InformationType.Success, 3000, InformationPosition.Top);
        }

        void SoumettreObservationsAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var en = (EntretienAnnuel)View.CurrentObject;
            en.SoumettreObservations();
            ObjectSpace.CommitChanges();

            // Notifie N+1
            _Notifier(en, en.Evaluateur,
                $"Observations reçues — {en.Salarie?.FullName}",
                $"{en.Salarie?.FullName} a soumis ses observations sur son évaluation annuelle "
                + $"{en.Campagne?.Annee}. Connectez-vous sur AdiPAIE pour les consulter et valider.");

            UpdateStates(); View.Refresh();
            Application.ShowViewStrategy?.ShowMessage(
                "Vos observations ont été soumises. Votre responsable en a été informé.",
                InformationType.Success, 3000, InformationPosition.Top);
        }

        void ValiderObservationsAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var en = (EntretienAnnuel)View.CurrentObject;
            en.ValiderObservationsN1();
            ObjectSpace.CommitChanges();

            if (en.Statut == EntretienStatut.EnAttenteN2)
            {
                _Notifier(en, en.ValideurN2,
                    $"Évaluation en attente de votre validation (N+2) — {en.Salarie?.FullName}",
                    $"L'évaluation annuelle {en.Campagne?.Annee} de {en.Salarie?.FullName} "
                    + $"a été validée par {en.Evaluateur?.FullName} et attend votre validation finale.");

                Application.ShowViewStrategy?.ShowMessage(
                    $"Observations validées. Transmis à {en.ValideurN2?.FullName} (N+2).",
                    InformationType.Success, 3000, InformationPosition.Top);
            }
            else
            {
                // SoumiseRH — notifie RH par email
                _NotifierRHEmail(en);

                Application.ShowViewStrategy?.ShowMessage(
                    "Observations validées. L'entretien est transmis au RH pour clôture.",
                    InformationType.Success, 3000, InformationPosition.Top);
            }

            UpdateStates(); View.Refresh();
        }

        void ValiderN2Action_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var en = (EntretienAnnuel)View.CurrentObject;
            en.ValiderN2();
            ObjectSpace.CommitChanges();

            _NotifierRHEmail(en);

            // Notifie N+1 aussi
            _Notifier(en, en.Evaluateur,
                $"Évaluation validée par N+2 — {en.Salarie?.FullName}",
                $"L'évaluation de {en.Salarie?.FullName} a été validée par "
                + $"{en.ValideurN2?.FullName}. Elle est maintenant transmise au RH.");

            UpdateStates(); View.Refresh();
            Application.ShowViewStrategy?.ShowMessage(
                "Évaluation validée. Transmise au RH pour clôture.",
                InformationType.Success, 3000, InformationPosition.Top);
        }

        void RejeterN2Action_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var en = (EntretienAnnuel)View.CurrentObject;

            if (string.IsNullOrWhiteSpace(en.MotifRejetN2))
                throw new UserFriendlyException(
                    "Veuillez saisir un motif de rejet dans le champ 'Motif de rejet N+2'.");

            en.RejeterN2(en.MotifRejetN2);
            ObjectSpace.CommitChanges();

            // Notifie N+1
            _Notifier(en, en.Evaluateur,
                $"Évaluation rejetée par N+2 — {en.Salarie?.FullName}",
                $"L'évaluation de {en.Salarie?.FullName} a été rejetée par "
                + $"{en.ValideurN2?.FullName}. Motif : {en.MotifRejetN2}. "
                + "Veuillez corriger et resoumettre.");

            UpdateStates(); View.Refresh();
            Application.ShowViewStrategy?.ShowMessage(
                "Évaluation rejetée. Le N+1 a été notifié pour correction.",
                InformationType.Warning, 4000, InformationPosition.Top);
        }

        void CloturerAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var en = (EntretienAnnuel)View.CurrentObject;
            en.Cloturer();
            _ArchiverDansDossier(en);
            ObjectSpace.CommitChanges();

            // Notifie le salarié
            _Notifier(en, en.Salarie,
                "Votre entretien annuel a été clôturé",
                $"Votre entretien annuel {en.Campagne?.Annee} a été clôturé. "
                + $"Score final : {en.ScoreGlobal:N2}/5. "
                + "La fiche est disponible dans votre dossier RH.");

            UpdateStates(); View.Refresh();
            Application.ShowViewStrategy?.ShowMessage(
                "Entretien clôturé et archivé dans le dossier salarié.",
                InformationType.Success, 4000, InformationPosition.Top);
        }

        void GenererFicheAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var en = (EntretienAnnuel)View.CurrentObject;
            try
            {
                var docxBytes = EntretienTemplateService.Fusionner(en, ObjectSpace);
                var pdfBytes = ConvertirEnPdf(docxBytes);
                ArchiverFiche(en, pdfBytes);
                ObjectSpace.CommitChanges();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    "Fiche générée et archivée dans le dossier salarié.",
                    InformationType.Success, 4000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur génération : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }

        // ── Activation / verrous ──────────────────────────────

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateStates();
            AppliquerVerrous();
            View.CurrentObjectChanged += (_, __) => { UpdateStates(); AppliquerVerrous(); };
        }

        void UpdateStates()
        {
            var en = View?.CurrentObject as EntretienAnnuel;
            if (en == null) return;

            var s = en.Statut;
            bool estSal = EstSalarieConnecte(en);
            bool estN1 = EstN1Connecte(en);
            bool estN2 = EstN2Connecte(en);
            bool clos = s == EntretienStatut.Cloture;

            // RH
            planifierAction.Active["s"] = s == EntretienStatut.Brouillon;
            lancerEvaluationAction.Active["s"] = s == EntretienStatut.PlanifieRH;
            cloturerAction.Active["s"] = s == EntretienStatut.SoumiseRH;

            // N+1
            soumettreAuSalarieAction.Active["s"] = s == EntretienStatut.SaisieManager && (estN1 || EstRH());
            validerObservationsAction.Active["s"] = s == EntretienStatut.ValidationN1 && (estN1 || EstRH());

            // Salarié
            soumettreObservationsAction.Active["s"] = s == EntretienStatut.SaisieSalarie && estSal;

            // N+2
            validerN2Action.Active["s"] = s == EntretienStatut.EnAttenteN2 && (estN2 || EstRH());
            rejeterN2Action.Active["s"] = s == EntretienStatut.EnAttenteN2 && (estN2 || EstRH());

            // Utilitaires
            recalculerScoreAction.Active["s"] = !clos;
            genererFicheAction.Active["s"] = s != EntretienStatut.Brouillon
                                             && s != EntretienStatut.PlanifieRH;
        }

        void AppliquerVerrous()
        {
            var en = View?.CurrentObject as EntretienAnnuel;
            if (en == null) return;

            var s = en.Statut;
            bool clos = s == EntretienStatut.Cloture;

            // Champs N+1 — modifiables uniquement en SaisieManager
            bool n1Mod = s == EntretienStatut.SaisieManager && !clos;
            foreach (var champ in new[] {
                nameof(EntretienAnnuel.NoteGlobaleManager),
                nameof(EntretienAnnuel.NoteGlobaleService),
                nameof(EntretienAnnuel.CommentairesHierarchie),
                nameof(EntretienAnnuel.CommentaireManager),
                nameof(EntretienAnnuel.EstEnSituationEncadrement),
                nameof(EntretienAnnuel.ConclusionGenerale),
                nameof(EntretienAnnuel.PromotionProposee),
                nameof(EntretienAnnuel.AugmentationProposee),
                nameof(EntretienAnnuel.FormationIdentifiee),
                nameof(EntretienAnnuel.NiveauInstruction),
            })
                SetEditable(champ, n1Mod);

            // Champs salarié — modifiables uniquement en SaisieSalarie
            bool salMod = s == EntretienStatut.SaisieSalarie
                       && EstSalarieConnecte(en) && !clos;
            SetEditable(nameof(EntretienAnnuel.CommentairesCollaborateur), salMod);
            SetEditable(nameof(EntretienAnnuel.EvolutionSouhaitee), salMod);

            // MotifRejetN2 — éditable uniquement par N+2 en EnAttenteN2
            bool n2Mod = s == EntretienStatut.EnAttenteN2 && (EstN2Connecte(en) || EstRH()) && !clos;
            SetEditable(nameof(EntretienAnnuel.MotifRejetN2), n2Mod);

            // NotesDecisionRH — toujours éditable par RH sauf clôturé
            SetEditable(nameof(EntretienAnnuel.NotesDecisionRH), !clos && EstRH());
        }

        void SetEditable(string memberName, bool editable)
        {
            try
            {
                if (View?.FindItem(memberName) is PropertyEditor pe)
                    pe.AllowEdit.SetItemValue("workflow", editable);
            }
            catch { }
        }

        // ── Helpers profil connecté ───────────────────────────

        private Salarie GetSalarieConnecte()
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

        private bool EstSalarieConnecte(EntretienAnnuel en)
        {
            var sal = GetSalarieConnecte();
            return sal != null && sal.Oid == en.Salarie?.Oid;
        }

        private bool EstN1Connecte(EntretienAnnuel en)
        {
            var sal = GetSalarieConnecte();
            return sal != null && sal.Oid == en.Evaluateur?.Oid;
        }

        private bool EstN2Connecte(EntretienAnnuel en)
        {
            var sal = GetSalarieConnecte();
            return sal != null && sal.Oid == en.ValideurN2?.Oid;
        }

        private bool EstRH()
        {
            // RH = pas de fiche salarié liée au compte
            return GetSalarieConnecte() == null;
        }

        // ── Notifications ─────────────────────────────────────

        private void _Notifier(EntretienAnnuel en, Salarie dest,
            string titre, string corps)
        {
            try
            {
                if (dest == null) return;
                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = dest;
                notif.Titre = titre;
                notif.Corps = corps;
                notif.Categorie = "Évaluation";
                notif.Priorite = NotificationPriorite.Important;
                ObjectSpace.CommitChanges();

                var notifOid = notif.Oid;
                var osFactory = Application.ServiceProvider
                    .GetRequiredService<IObjectSpaceFactory>();
                Task.Run(() =>
                {
                    try
                    {
                        using var os = osFactory.CreateObjectSpace(typeof(NotificationSalarie));
                        var n = os.GetObjectByKey<NotificationSalarie>(notifOid);
                        if (n != null) NotificationEmailService.Envoyer(n, os);
                    }
                    catch { }
                });
            }
            catch { }
        }

        private void _NotifierRHEmail(EntretienAnnuel en)
        {
            try
            {
                var prm = ParametresPaie.TryGet(ObjectSpace);
                if (prm == null || !prm.EmailActif
                    || string.IsNullOrWhiteSpace(prm.EmailsRHAlertes)) return;

                var destinataires = prm.EmailsRHAlertes
                    .Split(new[] { ';', ',' },
                        System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.Contains('@'))
                    .ToList();

                if (!destinataires.Any()) return;

                var sender = prm.CreateEmailSender();
                var sujet = $"[AdiPAIE] Évaluation prête à clôturer — {en.Salarie?.FullName}";
                var body = $@"<html><body style='font-family:Segoe UI,Arial;font-size:14px;'>
<h2 style='color:#1F4E79;'>Entretien annuel — prêt à clôturer</h2>
<table style='border-collapse:collapse;'>
  <tr><td style='padding:6px 12px;'><b>Salarié</b></td><td>{en.Salarie?.FullName}</td></tr>
  <tr style='background:#EBF3FB;'><td style='padding:6px 12px;'><b>Campagne</b></td><td>{en.Campagne?.Annee}</td></tr>
  <tr><td style='padding:6px 12px;'><b>Note globale</b></td><td>{en.NoteGlobaleManager}</td></tr>
  <tr style='background:#EBF3FB;'><td style='padding:6px 12px;'><b>Score</b></td><td>{en.ScoreGlobal:N2} / 5</td></tr>
</table>
<p>Connectez-vous sur AdiPAIE pour clôturer l'entretien.</p>
</body></html>";

                foreach (var dest in destinataires)
                    sender.Send(dest, sujet, body);
            }
            catch { }
        }

        // ── Archivage et génération fiche ─────────────────────

        private void _ArchiverDansDossier(EntretienAnnuel en)
        {
            try
            {
                if (en.Salarie == null) return;
                var dossier = ObjectSpace.GetObjectsQuery<DossierSalarie>()
                    .FirstOrDefault(d => d.Salarie.Oid == en.Salarie.Oid);
                if (dossier == null)
                {
                    dossier = ObjectSpace.CreateObject<DossierSalarie>();
                    dossier.Salarie = en.Salarie;
                }
                var doc = ObjectSpace.CreateObject<DossierDocument>();
                doc.Dossier = dossier;
                doc.Categorie = DossierCategorieDocument.Evaluation;
                doc.Titre = $"Entretien annuel {en.Campagne?.Annee} — Score : {en.ScoreGlobal:N2}/5";
                doc.SourceAuto = "Généré à la clôture";
                doc.DateDocument = DateTime.Today;
            }
            catch { }
        }

        private byte[] ConvertirEnPdf(byte[] docxBytes)
        {
            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "AdiPAIE_Entretien");
            System.IO.Directory.CreateDirectory(tempDir);
            var docxPath = System.IO.Path.Combine(tempDir,
                $"entretien_{Guid.NewGuid():N}.docx");
            System.IO.File.WriteAllBytes(docxPath, docxBytes);
            try
            {
                var soffice = TrouverSoffice();
                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = soffice,
                        Arguments = $"--headless --convert-to pdf " +
                                    $"--outdir \"{tempDir}\" \"{docxPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                bool ok = proc.WaitForExit(30_000);
                var pdfPath = System.IO.Path.ChangeExtension(docxPath, ".pdf");
                if (ok && System.IO.File.Exists(pdfPath))
                    return System.IO.File.ReadAllBytes(pdfPath);
                return docxBytes;
            }
            finally { try { System.IO.File.Delete(docxPath); } catch { } }
        }

        private string TrouverSoffice()
        {
            var chemins = new[]
            {
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
                "/usr/bin/soffice", "/usr/lib/libreoffice/program/soffice", "soffice"
            };
            return chemins.FirstOrDefault(System.IO.File.Exists) ?? "soffice";
        }

        private void ArchiverFiche(EntretienAnnuel en, byte[] fileBytes)
        {
            try
            {
                if (en.Salarie == null) return;
                var dossier = ObjectSpace.GetObjectsQuery<DossierSalarie>()
                    .FirstOrDefault(d => d.Salarie.Oid == en.Salarie.Oid);
                if (dossier == null)
                {
                    dossier = ObjectSpace.CreateObject<DossierSalarie>();
                    dossier.Salarie = en.Salarie;
                }
                var ext = fileBytes.Length > 4 && fileBytes[0] == 0x25 ? "pdf" : "docx";
                var nomFic = $"Entretien_{en.Campagne?.Annee}_{en.Salarie?.LastName}" +
                             $"_{DateTime.Today:yyyyMMdd}.{ext}";
                var doc = ObjectSpace.CreateObject<DossierDocument>();
                doc.Dossier = dossier;
                doc.Categorie = DossierCategorieDocument.Evaluation;
                doc.Titre = $"Fiche entretien annuel {en.Campagne?.Annee}";
                doc.SourceAuto = "Généré depuis template Word";
                doc.DateDocument = DateTime.Today;
                var pj = ObjectSpace.CreateObject<DossierPieceJointe>();
                pj.DossierDocument = doc;
                pj.Titre = nomFic;
                pj.Fichier = ObjectSpace
                    .CreateObject<DevExpress.Persistent.BaseImpl.FileData>();
                using var ms = new System.IO.MemoryStream(fileBytes);
                pj.Fichier.LoadFromStream(nomFic, ms);
            }
            catch { }
        }
    }
}
