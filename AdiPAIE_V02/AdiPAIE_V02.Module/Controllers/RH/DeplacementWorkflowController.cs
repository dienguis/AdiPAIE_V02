using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.Base;
using DevExpress.ExpressApp.Actions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    public class DeplacementWorkflowController
        : ObjectViewController<DetailView, DemandeDeplacement>
    {
        readonly SimpleAction soumettreAction;
        readonly SimpleAction validerN1Action;
        readonly SimpleAction rejeterN1Action;
        readonly SimpleAction initialiserFraisAction;
        readonly SimpleAction soumettreRHAction;
        readonly SimpleAction approuverRHAction;
        readonly SimpleAction rejeterRHAction;
        readonly SimpleAction genererOrdreAction;
        readonly SimpleAction genererEtatFraisAction;
        readonly SimpleAction validerDAFAction;
        readonly SimpleAction confirmerComptableAction;

        public DeplacementWorkflowController()
        {
            // ── Salarié : Soumettre ───────────────────────────
            soumettreAction = new SimpleAction(this,
                "Deplacement_Soumettre", PredefinedCategory.Edit)
            {
                Caption = "Soumettre",
                ImageName = "Action_Forward",
                ConfirmationMessage = "Soumettre cette demande de déplacement ?"
            };
            soumettreAction.Execute += (s, e) =>
            {
                var d = (DemandeDeplacement)View.CurrentObject;
                d.Soumettre();

                var smtp = ExtraireSmtp();
                var eInfo = ExtraireDemande(d);
                var destEmail = d.ValideurN1?.Email ?? "";
                var destOid = d.ValideurN1?.Oid;

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                if (destOid.HasValue)
                    _Notifier(destOid.Value,
                        $"Demande de déplacement à valider — {eInfo.SalarieNom}",
                        $"{eInfo.SalarieNom} souhaite effectuer un déplacement : "
                        + $"{eInfo.Objet}. Départ : {eInfo.DateDepart}, "
                        + $"Retour : {eInfo.DateRetour} ({eInfo.NombreJours} j).");

                if (!string.IsNullOrWhiteSpace(destEmail))
                    EnvoyerAsync(smtp, destEmail,
                        $"[AdiPAIE] Demande de déplacement à valider — {eInfo.SalarieNom}",
                        CorpsEmail("Demande de déplacement à valider",
                            $"{eInfo.SalarieNom} souhaite effectuer un déplacement : "
                            + $"{eInfo.Objet}. Départ : {eInfo.DateDepart}, "
                            + $"Retour : {eInfo.DateRetour} ({eInfo.NombreJours} j).", eInfo));

                Application.ShowViewStrategy?.ShowMessage(
                    "Demande soumise.", InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── N+1 : Valider ─────────────────────────────────
            validerN1Action = new SimpleAction(this,
                "Deplacement_ValiderN1", PredefinedCategory.Edit)
            {
                Caption = "Valider",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Valider cette demande de déplacement ?"
            };
            validerN1Action.Execute += (s, e) =>
            {
                var d = (DemandeDeplacement)View.CurrentObject;
                d.ValiderN1();

                var smtp = ExtraireSmtp();
                var eInfo = ExtraireDemande(d);
                var valideurNom = d.ValideurN1?.FullName ?? "";
                var rhEmails = ExtraireEmailsRH();

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                foreach (var dest in rhEmails)
                    EnvoyerAsync(smtp, dest,
                        $"[AdiPAIE] Demande à traiter — {eInfo.SalarieNom}",
                        CorpsEmail("Demande de déplacement validée par N+1",
                            $"Demande de {eInfo.SalarieNom} ({eInfo.Objet}) validée par "
                            + $"{valideurNom}. Veuillez préparer la note de frais.", eInfo));

                Application.ShowViewStrategy?.ShowMessage(
                    "Demande validée. L'assistant RH a été notifié.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── N+1 : Rejeter ─────────────────────────────────
            rejeterN1Action = new SimpleAction(this,
                "Deplacement_RejeterN1", PredefinedCategory.Edit)
            {
                Caption = "Rejeter",
                ImageName = "Action_Cancel",
                ConfirmationMessage = "Rejeter cette demande ? Le salarié pourra la modifier."
            };
            rejeterN1Action.Execute += (s, e) =>
            {
                var d = (DemandeDeplacement)View.CurrentObject;
                if (string.IsNullOrWhiteSpace(d.MotifRejet))
                    throw new UserFriendlyException("Veuillez saisir un motif de rejet.");
                d.RejeterN1(d.MotifRejet);

                var smtp = ExtraireSmtp();
                var eInfo = ExtraireDemande(d);
                var motif = d.MotifRejet ?? "";
                var salarieEmail = d.Salarie?.Email ?? "";
                var salarieOid = d.Salarie?.Oid;

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                if (salarieOid.HasValue)
                    _Notifier(salarieOid.Value,
                        "Votre demande de déplacement a été rejetée",
                        $"Votre demande '{eInfo.Objet}' a été rejetée. Motif : {motif}.");

                if (!string.IsNullOrWhiteSpace(salarieEmail))
                    EnvoyerAsync(smtp, salarieEmail,
                        "[AdiPAIE] Votre demande de déplacement a été rejetée",
                        CorpsEmail("Demande de déplacement rejetée",
                            $"Votre demande '{eInfo.Objet}' a été rejetée. "
                            + $"Motif : {motif}. Vous pouvez la modifier et la resoumettre.",
                            eInfo));
            };

            // ── Assistant : Initialiser les frais ─────────────
            initialiserFraisAction = new SimpleAction(this,
                "Deplacement_InitialiserFrais", PredefinedCategory.Edit)
            {
                Caption = "Initialiser les frais",
                ImageName = "Action_Refresh",
                ConfirmationMessage = "Initialiser les lignes de frais depuis le référentiel ?"
            };
            initialiserFraisAction.Execute += InitialiserFraisAction_Execute;

            // ── Assistant : Soumettre au RH ───────────────────
            soumettreRHAction = new SimpleAction(this,
                "Deplacement_SoumettreRH", PredefinedCategory.Edit)
            {
                Caption = "Soumettre au RH",
                ImageName = "Action_Forward",
                ConfirmationMessage = "Soumettre au RH pour approbation ?"
            };
            soumettreRHAction.Execute += (s, e) =>
            {
                var d = (DemandeDeplacement)View.CurrentObject;
                d.SoumettreAuRH();

                var smtp = ExtraireSmtp();
                var eInfo = ExtraireDemande(d);
                var rhEmails = ExtraireEmailsRH();

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                foreach (var dest in rhEmails)
                    EnvoyerAsync(smtp, dest,
                        $"[AdiPAIE] Ordre de mission à approuver — {eInfo.SalarieNom}",
                        CorpsEmail("Ordre de mission à approuver",
                            $"La note de frais de {eInfo.SalarieNom} ({eInfo.Objet}) "
                            + $"est prête. Total : {eInfo.TotalFrais}.", eInfo));

                Application.ShowViewStrategy?.ShowMessage(
                    $"Soumis au RH. Total frais : {eInfo.TotalFrais}.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── RH : Générer l'ordre de mission ───────────────
            genererOrdreAction = new SimpleAction(this,
                "Deplacement_GenererOrdre", PredefinedCategory.View)
            {
                Caption = "Générer l'ordre de mission",
                ImageName = "Action_Export"
            };
            genererOrdreAction.Execute += GenererOrdreAction_Execute;

            // ── RH : Générer l'état de frais ──────────────────
            genererEtatFraisAction = new SimpleAction(this,
                "Deplacement_GenererEtatFrais", PredefinedCategory.View)
            {
                Caption = "Générer l'état de frais",
                ImageName = "Action_Export"
            };
            genererEtatFraisAction.Execute += GenererEtatFraisAction_Execute;

            // ── RH : Approuver ────────────────────────────────
            approuverRHAction = new SimpleAction(this,
                "Deplacement_ApprouverRH", PredefinedCategory.Edit)
            {
                Caption = "Approuver",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Approuver cet ordre de mission ?"
            };
            approuverRHAction.Execute += (s, e) =>
            {
                var d = (DemandeDeplacement)View.CurrentObject;
                d.ApprouverRH();

                var smtp = ExtraireSmtp();
                var eInfo = ExtraireDemande(d);
                var salarieEmail = d.Salarie?.Email ?? "";
                var salarieOid = d.Salarie?.Oid;
                var dafEmails = ExtraireEmailsDAF();

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                // Notification in-app salarié
                if (salarieOid.HasValue)
                    _Notifier(salarieOid.Value,
                        "Votre ordre de mission a été approuvé",
                        $"Votre demande '{eInfo.Objet}' ({eInfo.DateDepart} → "
                        + $"{eInfo.DateRetour}) a été approuvée. "
                        + $"Total frais : {eInfo.TotalFrais}.");

                // Email salarié
                if (!string.IsNullOrWhiteSpace(salarieEmail))
                    EnvoyerAsync(smtp, salarieEmail,
                        "[AdiPAIE] Votre ordre de mission a été approuvé",
                        CorpsEmail("Ordre de mission approuvé",
                            $"Votre demande '{eInfo.Objet}' ({eInfo.DateDepart} → "
                            + $"{eInfo.DateRetour}) a été approuvée. "
                            + $"Total frais : {eInfo.TotalFrais}.", eInfo));

                // Email DAF
                foreach (var dest in dafEmails)
                    EnvoyerAsync(smtp, dest,
                        $"[AdiPAIE] Ordre de mission approuvé — {eInfo.SalarieNom}",
                        CorpsEmail("Ordre de mission approuvé — décaissement requis",
                            $"Veuillez procéder au décaissement pour la mission de "
                            + $"{eInfo.SalarieNom} : {eInfo.Objet}. "
                            + $"Montant : {eInfo.TotalFrais}. N° {eInfo.NumeroOrdre}.", eInfo));

                Application.ShowViewStrategy?.ShowMessage(
                    "Ordre approuvé. Le DAF a été notifié.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── RH : Rejeter ──────────────────────────────────
            rejeterRHAction = new SimpleAction(this,
                "Deplacement_RejeterRH", PredefinedCategory.Edit)
            {
                Caption = "Rejeter",
                ImageName = "Action_Cancel",
                ConfirmationMessage = "Rejeter cet ordre ? Il retournera chez l'assistant."
            };
            rejeterRHAction.Execute += (s, e) =>
            {
                var d = (DemandeDeplacement)View.CurrentObject;
                if (string.IsNullOrWhiteSpace(d.MotifRejet))
                    throw new UserFriendlyException("Veuillez saisir un motif de rejet.");
                d.RejeterRH(d.MotifRejet);

                var smtp = ExtraireSmtp();
                var eInfo = ExtraireDemande(d);
                var motif = d.MotifRejet ?? "";
                var rhEmails = ExtraireEmailsRH();

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                foreach (var dest in rhEmails)
                    EnvoyerAsync(smtp, dest,
                        $"[AdiPAIE] Ordre rejeté — {eInfo.SalarieNom}",
                        CorpsEmail("Ordre de mission rejeté par le RH",
                            $"L'ordre de mission de {eInfo.SalarieNom} a été rejeté. "
                            + $"Motif : {motif}. Veuillez corriger et resoumettre.", eInfo));
            };

            // ── DAF : Valider décaissement ────────────────────
            validerDAFAction = new SimpleAction(this,
                "Deplacement_ValiderDAF", PredefinedCategory.Edit)
            {
                Caption = "Valider le décaissement",
                ImageName = "Action_Approve",
                ConfirmationMessage = "Valider le décaissement ?"
            };
            validerDAFAction.Execute += (s, e) =>
            {
                var d = (DemandeDeplacement)View.CurrentObject;
                d.ValiderDAF();

                var smtp = ExtraireSmtp();
                var eInfo = ExtraireDemande(d);
                var comptableEmails = ExtraireEmailsComptable();

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                foreach (var dest in comptableEmails)
                    EnvoyerAsync(smtp, dest,
                        $"[AdiPAIE] Décaissement validé — {eInfo.SalarieNom}",
                        CorpsEmail("Décaissement validé — opération à enregistrer",
                            $"Veuillez enregistrer l'opération comptable pour la mission de "
                            + $"{eInfo.SalarieNom} : {eInfo.Objet}. "
                            + $"Montant : {eInfo.TotalFrais}. N° {eInfo.NumeroOrdre}.", eInfo));

                Application.ShowViewStrategy?.ShowMessage(
                    "Décaissement validé. Le comptable a été notifié.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Comptable : Confirmer ─────────────────────────
            confirmerComptableAction = new SimpleAction(this,
                "Deplacement_ConfirmerComptable", PredefinedCategory.Edit)
            {
                Caption = "Confirmer l'opération",
                ImageName = "Action_Close",
                ConfirmationMessage = "Confirmer l'opération comptable ?"
            };
            confirmerComptableAction.Execute += (s, e) =>
            {
                var d = (DemandeDeplacement)View.CurrentObject;
                d.ConfirmerComptable();
                _ArchiverDansDossier(d);

                var smtp = ExtraireSmtp();
                var eInfo = ExtraireDemande(d);
                var salarieEmail = d.Salarie?.Email ?? "";
                var salarieOid = d.Salarie?.Oid;

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                if (salarieOid.HasValue)
                    _Notifier(salarieOid.Value,
                        "Votre mission est clôturée",
                        $"L'opération comptable pour votre mission '{eInfo.Objet}' "
                        + $"a été enregistrée. Montant : {eInfo.TotalFrais}.");

                if (!string.IsNullOrWhiteSpace(salarieEmail))
                    EnvoyerAsync(smtp, salarieEmail,
                        "[AdiPAIE] Votre mission est clôturée",
                        CorpsEmail("Mission clôturée",
                            $"L'opération comptable pour votre mission '{eInfo.Objet}' "
                            + $"a été enregistrée. Montant : {eInfo.TotalFrais}.", eInfo));

                Application.ShowViewStrategy?.ShowMessage(
                    "Mission clôturée et archivée.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };
        }

        // ════════════════════════════════════════════════════════
        // HANDLERS
        // ════════════════════════════════════════════════════════

        void InitialiserFraisAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (DemandeDeplacement)View.CurrentObject;
            d.RecalculerNombreJours();

            var categories = ObjectSpace.GetObjectsQuery<CategorieFraisMission>()
                .Where(c => c.Actif)
                .OrderBy(c => c.OrdreAffichage)
                .ToList();

            if (!categories.Any())
                throw new UserFriendlyException(
                    "Aucune catégorie de frais active. "
                    + "Veuillez les configurer dans Paramétrage → Catégories de frais.");

            foreach (var f in d.Frais.ToList())
                ObjectSpace.Delete(f);

            foreach (var cat in categories)
            {
                var ligne = ObjectSpace.CreateObject<LigneFraisMission>();
                ligne.Demande = d;
                ligne.Categorie = ObjectSpace.GetObject(cat);
                ligne.ModeCalcul = cat.ModeCalcul;
                ligne.TauxUnitaire = cat.TauxDefaut;
                ligne.Quantite = cat.ModeCalcul == FraisCalculMode.TauxJournalier
                    ? d.NombreJours
                    : cat.ModeCalcul == FraisCalculMode.Forfait ? 1 : 0;
                ligne.Montant = Math.Round(ligne.Quantite * ligne.TauxUnitaire, 0);
            }

            d.RecalculerTotal();
            ObjectSpace.CommitChanges();
            View.Refresh();

            Application.ShowViewStrategy?.ShowMessage(
                $"{categories.Count} ligne(s) de frais initialisées. "
                + $"Total : {d.TotalFrais:N0} FCFA.",
                InformationType.Success, 4000, InformationPosition.Top);
        }

        void GenererOrdreAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (DemandeDeplacement)View.CurrentObject;
            try
            {
                if (string.IsNullOrWhiteSpace(d.NumeroOrdre))
                    d.NumeroOrdre = GenererNumeroOrdre(d);

                var docxBytes = OrdreMissionSimpleService.Fusionner(d, ObjectSpace);
                var pdfBytes = ConvertirEnPdf(docxBytes);
                var nomFic = $"OrdreMission_{d.NumeroOrdre}_{d.Salarie?.LastName}.pdf";
                var fileData = ObjectSpace
                    .CreateObject<DevExpress.Persistent.BaseImpl.FileData>();
                using var ms = new System.IO.MemoryStream(pdfBytes);
                fileData.LoadFromStream(nomFic, ms);
                d.DocumentOrdre = fileData;

                ObjectSpace.CommitChanges();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    $"Ordre de mission {d.NumeroOrdre} généré.",
                    InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur ordre de mission : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }

        void GenererEtatFraisAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var d = (DemandeDeplacement)View.CurrentObject;
            try
            {
                if (string.IsNullOrWhiteSpace(d.NumeroOrdre))
                    d.NumeroOrdre = GenererNumeroOrdre(d);

                var docxBytes = OrdreMissionTemplateService.Fusionner(d, ObjectSpace);
                var pdfBytes = ConvertirEnPdf(docxBytes);
                var nomFic = $"EtatFrais_{d.NumeroOrdre}_{d.Salarie?.LastName}.pdf";
                var fileData = ObjectSpace
                    .CreateObject<DevExpress.Persistent.BaseImpl.FileData>();
                using var ms = new System.IO.MemoryStream(pdfBytes);
                fileData.LoadFromStream(nomFic, ms);
                d.DocumentOrdre = fileData;

                ObjectSpace.CommitChanges();
                View.Refresh();
                Application.ShowViewStrategy?.ShowMessage(
                    $"État de frais {d.NumeroOrdre} généré.",
                    InformationType.Success, 3000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur état de frais : {ex.Message}",
                    InformationType.Error, 6000, InformationPosition.Top);
            }
        }

        // ════════════════════════════════════════════════════════
        // ACTIVATION / ÉTATS
        // ════════════════════════════════════════════════════════

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateStates();
            View.CurrentObjectChanged += (_, __) => UpdateStates();
        }

        void UpdateStates()
        {
            var d = View?.CurrentObject as DemandeDeplacement;
            if (d == null) return;
            var s = d.Statut;

            soumettreAction.Active["s"] = s == DeplacementStatut.Brouillon;
            validerN1Action.Active["s"] = s == DeplacementStatut.EnAttenteN1;
            rejeterN1Action.Active["s"] = s == DeplacementStatut.EnAttenteN1;
            initialiserFraisAction.Active["s"] = s == DeplacementStatut.SoumiseAssistant;
            soumettreRHAction.Active["s"] = s == DeplacementStatut.SoumiseAssistant;
            genererOrdreAction.Active["s"] = s == DeplacementStatut.SoumiseAssistant
                                                 || s == DeplacementStatut.EnAttenteRH
                                                 || s == DeplacementStatut.ApprouveeRH
                                                 || s == DeplacementStatut.EnAttenteComptable
                                                 || s == DeplacementStatut.Traitee;
            genererEtatFraisAction.Active["s"] = s == DeplacementStatut.SoumiseAssistant
                                                 || s == DeplacementStatut.EnAttenteRH
                                                 || s == DeplacementStatut.ApprouveeRH
                                                 || s == DeplacementStatut.EnAttenteComptable
                                                 || s == DeplacementStatut.Traitee;
            approuverRHAction.Active["s"] = s == DeplacementStatut.EnAttenteRH;
            rejeterRHAction.Active["s"] = s == DeplacementStatut.EnAttenteRH;
            validerDAFAction.Active["s"] = s == DeplacementStatut.ApprouveeRH;
            confirmerComptableAction.Active["s"] = s == DeplacementStatut.EnAttenteComptable;
        }

        // ════════════════════════════════════════════════════════
        // NOTIFICATION IN-APP — via INonSecuredObjectSpaceFactory
        // Bypasse la sécurité XAF pour créer la notification
        // ════════════════════════════════════════════════════════

        private void _Notifier(Guid destOid, string titre, string corps)
        {
            try
            {
                var factory = Application.ServiceProvider
                    .GetRequiredService<INonSecuredObjectSpaceFactory>();

                Task.Run(() =>
                {
                    try
                    {
                        using var os = factory.CreateNonSecuredObjectSpace(
                            typeof(NotificationSalarie));
                        var salarie = os.GetObjectByKey<Salarie>(destOid);
                        if (salarie == null) return;
                        var notif = os.CreateObject<NotificationSalarie>();
                        notif.Salarie = salarie;
                        notif.Titre = titre;
                        notif.Corps = corps;
                        notif.Categorie = "Mission";
                        notif.Priorite = NotificationPriorite.Important;
                        os.CommitChanges();
                    }
                    catch { }
                });
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════
        // EXTRACTION DONNÉES XPO via INonSecuredObjectSpaceFactory
        // ════════════════════════════════════════════════════════

        private class SmtpInfo
        {
            public bool Actif { get; set; }
            public string Host { get; set; } = "";
            public int Port { get; set; } = 587;
            public string User { get; set; } = "";
            public string Password { get; set; } = "";
            public string From { get; set; } = "";
            public string FromName { get; set; } = "";
            public bool Ssl { get; set; }
        }

        private class DemandeInfo
        {
            public string SalarieNom { get; set; } = "";
            public string Objet { get; set; } = "";
            public string DateDepart { get; set; } = "";
            public string DateRetour { get; set; } = "";
            public string NombreJours { get; set; } = "";
            public string TotalFrais { get; set; } = "";
            public string NumeroOrdre { get; set; } = "";
        }

        private SmtpInfo ExtraireSmtp()
        {
            try
            {
                var factory = Application.ServiceProvider
                    .GetRequiredService<INonSecuredObjectSpaceFactory>();
                using var os = factory.CreateNonSecuredObjectSpace(typeof(ParametresPaie));
                var prm = os.GetObjectsQuery<ParametresPaie>().FirstOrDefault();
                if (prm == null) return new SmtpInfo();
                return new SmtpInfo
                {
                    Actif = prm.EmailActif,
                    Host = prm.SmtpHost ?? "",
                    Port = prm.SmtpPort,
                    User = prm.SmtpUserName ?? "",
                    Password = prm.SmtpPassword ?? "",
                    From = prm.MailFromAddress ?? "",
                    FromName = prm.MailFromDisplayName ?? "",
                    Ssl = prm.SmtpUseSsl
                };
            }
            catch { return new SmtpInfo(); }
        }

        private List<string> ExtraireEmailsRH()
        {
            try
            {
                var factory = Application.ServiceProvider
                    .GetRequiredService<INonSecuredObjectSpaceFactory>();
                using var os = factory.CreateNonSecuredObjectSpace(typeof(ParametresPaie));
                var prm = os.GetObjectsQuery<ParametresPaie>().FirstOrDefault();
                return ParseEmails(prm?.EmailsRHAlertes ?? "");
            }
            catch { return new List<string>(); }
        }

        private List<string> ExtraireEmailsDAF()
        {
            try
            {
                var factory = Application.ServiceProvider
                    .GetRequiredService<INonSecuredObjectSpaceFactory>();
                using var os = factory.CreateNonSecuredObjectSpace(typeof(ParametresPaie));
                var prm = os.GetObjectsQuery<ParametresPaie>().FirstOrDefault();
                var emails = prm?.EmailDAF;
                if (string.IsNullOrWhiteSpace(emails)) emails = prm?.EmailsRHAlertes;
                return ParseEmails(emails ?? "");
            }
            catch { return new List<string>(); }
        }

        private List<string> ExtraireEmailsComptable()
        {
            try
            {
                var factory = Application.ServiceProvider
                    .GetRequiredService<INonSecuredObjectSpaceFactory>();
                using var os = factory.CreateNonSecuredObjectSpace(typeof(ParametresPaie));
                var prm = os.GetObjectsQuery<ParametresPaie>().FirstOrDefault();
                var emails = prm?.EmailsComptable;
                if (string.IsNullOrWhiteSpace(emails)) emails = prm?.EmailsRHAlertes;
                return ParseEmails(emails ?? "");
            }
            catch { return new List<string>(); }
        }

        private static DemandeInfo ExtraireDemande(DemandeDeplacement d) =>
            new DemandeInfo
            {
                SalarieNom = d.Salarie?.FullName ?? "—",
                Objet = d.Objet ?? "—",
                DateDepart = d.DateDepart.ToString("dd/MM/yyyy"),
                DateRetour = d.DateRetour.ToString("dd/MM/yyyy"),
                NombreJours = d.NombreJours.ToString(),
                TotalFrais = d.TotalFrais.ToString("N0") + " FCFA",
                NumeroOrdre = d.NumeroOrdre ?? "—",
            };

        // ════════════════════════════════════════════════════════
        // ENVOI EMAIL ASYNC — ZÉRO ACCÈS XPO DANS LE THREAD
        // ════════════════════════════════════════════════════════

        private static void EnvoyerAsync(
            SmtpInfo smtp, string dest, string sujet, string body)
        {
            if (!smtp.Actif) return;
            if (string.IsNullOrWhiteSpace(dest)) return;
            if (string.IsNullOrWhiteSpace(smtp.Host)) return;

            var host = smtp.Host; var port = smtp.Port;
            var user = smtp.User; var pwd = smtp.Password;
            var from = smtp.From; var fromName = smtp.FromName;
            var ssl = smtp.Ssl;

            Task.Run(() =>
            {
                try
                {
                    using var client = new SmtpClient(host, port)
                    {
                        EnableSsl = ssl,
                        Credentials = new System.Net.NetworkCredential(user, pwd)
                    };
                    using var msg = new MailMessage(
                        new MailAddress(from, fromName),
                        new MailAddress(dest))
                    {
                        Subject = sujet,
                        Body = body,
                        IsBodyHtml = true
                    };
                    client.Send(msg);
                }
                catch { }
            });
        }

        // ════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════

        private static string CorpsEmail(
            string titre, string message, DemandeInfo d) =>
            $@"<html><body style='font-family:Segoe UI,Arial;font-size:14px;'>
<h2 style='color:#1F4E79;'>{titre}</h2>
<table style='border-collapse:collapse;'>
  <tr><td style='padding:6px 12px;'><b>Salarié</b></td><td>{d.SalarieNom}</td></tr>
  <tr style='background:#EBF3FB;'><td style='padding:6px 12px;'><b>Objet</b></td>
      <td>{d.Objet}</td></tr>
  <tr><td style='padding:6px 12px;'><b>Période</b></td>
      <td>{d.DateDepart} → {d.DateRetour} ({d.NombreJours} j)</td></tr>
  <tr style='background:#EBF3FB;'><td style='padding:6px 12px;'><b>N° Ordre</b></td>
      <td>{d.NumeroOrdre}</td></tr>
  <tr><td style='padding:6px 12px;'><b>Total frais</b></td>
      <td><b>{d.TotalFrais}</b></td></tr>
</table>
<p style='margin-top:16px;'>{message}</p>
<hr style='border:none;border-top:1px solid #BDD7EE;margin:16px 0;'/>
<p style='color:#888;font-size:12px;'>Ce message a été envoyé automatiquement par AdiPAIE.</p>
</body></html>";

        private static List<string> ParseEmails(string raw)
            => (raw ?? "")
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Contains('@'))
                .ToList();

        private string GenererNumeroOrdre(DemandeDeplacement d)
        {
            var annee = d.DateDepart.Year;
            var count = ObjectSpace.GetObjectsQuery<DemandeDeplacement>()
                .Count(x => x.DateDepart.Year == annee
                         && !string.IsNullOrEmpty(x.NumeroOrdre));
            return $"OM-{annee}-{(count + 1):D4}";
        }

        private void _ArchiverDansDossier(DemandeDeplacement d)
        {
            try
            {
                if (d.Salarie == null) return;
                var dossier = ObjectSpace.GetObjectsQuery<DossierSalarie>()
                    .FirstOrDefault(x => x.Salarie.Oid == d.Salarie.Oid);
                if (dossier == null)
                {
                    dossier = ObjectSpace.CreateObject<DossierSalarie>();
                    dossier.Salarie = d.Salarie;
                }
                var doc = ObjectSpace.CreateObject<DossierDocument>();
                doc.Dossier = dossier;
                doc.Categorie = DossierCategorieDocument.Autre;
                doc.Titre = $"Ordre de mission {d.NumeroOrdre} — {d.Objet}";
                doc.SourceAuto = "Généré automatiquement à la clôture de la mission";
                doc.DateDocument = DateTime.Today;
            }
            catch { }
        }

        private byte[] ConvertirEnPdf(byte[] docxBytes)
        {
            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "AdiPAIE_Mission");
            System.IO.Directory.CreateDirectory(tempDir);
            var docxPath = System.IO.Path.Combine(tempDir,
                $"mission_{Guid.NewGuid():N}.docx");
            System.IO.File.WriteAllBytes(docxPath, docxBytes);
            try
            {
                var chemins = new[] {
                    @"C:\Program Files\LibreOffice\program\soffice.exe",
                    @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
                    "/usr/bin/soffice", "soffice" };
                var soffice = chemins.FirstOrDefault(System.IO.File.Exists) ?? "soffice";
                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = soffice,
                        Arguments = $"--headless --convert-to pdf "
                                        + $"--outdir \"{tempDir}\" \"{docxPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                if (proc.WaitForExit(30_000))
                {
                    var pdfPath = System.IO.Path.ChangeExtension(docxPath, ".pdf");
                    if (System.IO.File.Exists(pdfPath))
                        return System.IO.File.ReadAllBytes(pdfPath);
                }
                return docxBytes;
            }
            finally { try { System.IO.File.Delete(docxPath); } catch { } }
        }
    }
}
