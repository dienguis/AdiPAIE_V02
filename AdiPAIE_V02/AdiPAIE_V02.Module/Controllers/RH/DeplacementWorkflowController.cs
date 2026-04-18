using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base;
using DevExpress.ExpressApp.Actions;
using System;
using System.Linq;
using System.Threading.Tasks;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    /// <summary>
    /// Workflow des demandes de déplacement / missions.
    ///
    /// Refactorisé : toutes les notifications email passent par WorkflowEmailHelper
    /// qui gère correctement INonSecuredObjectSpaceFactory sur le thread UI.
    /// Les méthodes locales ExtraireSender / ExtraireEmailsRH / EnvoyerAsync
    /// ont été supprimées — elles dupliquaient WorkflowEmailHelper et causaient
    /// des erreurs en Blazor.
    ///
    /// Règle fondamentale conservée :
    ///   - _Notifier()     → thread UI, ObjectSpace courant (synchrone)
    ///   - WorkflowEmailHelper.EnvoyerEmailsAsync() → string uniquement dans Task.Run
    /// </summary>
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
        readonly SimpleAction calculerDistancesAction;

        public DeplacementWorkflowController()
        {
            // ── Salarié : Soumettre ───────────────────────────────────
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
                var oldStatut = d.Statut;
                d.Soumettre();

                var info = Info(d);

                // Notification in-app N+1
                if (d.ValideurN1?.Oid is Guid oidN1)
                    _Notifier(oidN1,
                        $"Demande de déplacement à valider — {info.Salarie}",
                        $"{info.Salarie} souhaite effectuer un déplacement : {info.Objet}. "
                        + $"Départ : {info.Depart}, Retour : {info.Retour} ({info.Jours} j).");

                AuditService.Enregistrer(Application, "DemandeDeplacement", "Soumettre",
                    d.Oid.ToString(), d.DisplayName,
                    "Demande soumise pour validation N+1",
                    ancienStatut: oldStatut.ToString(), nouveauStatut: d.Statut.ToString());

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                // Email N+1
                if (!string.IsNullOrWhiteSpace(d.ValideurN1?.Email))
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                        new[] { d.ValideurN1.Email },
                        $"[AdiPAIE] Demande de déplacement à valider — {info.Salarie}",
                        WorkflowEmailHelper.HtmlTableau(
                            "Demande de déplacement à valider",
                            $"{info.Salarie} souhaite effectuer un déplacement : {info.Objet}.",
                            Lignes(info)));

                Application.ShowViewStrategy?.ShowMessage(
                    "Demande soumise.", InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── N+1 : Valider ─────────────────────────────────────────
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
                var oldStatut = d.Statut;
                var nom1 = d.ValideurN1?.FullName ?? "";
                d.ValiderN1();

                var info = Info(d);
                var rhEmails = WorkflowEmailHelper.ExtraireEmailsRH(Application);

                AuditService.Enregistrer(Application, "DemandeDeplacement", "ValiderN1",
                    d.Oid.ToString(), d.DisplayName,
                    $"Validée par {nom1}",
                    ancienStatut: oldStatut.ToString(), nouveauStatut: d.Statut.ToString());

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                WorkflowEmailHelper.EnvoyerEmailsAsync(Application, rhEmails,
                    $"[AdiPAIE] Demande à traiter — {info.Salarie}",
                    WorkflowEmailHelper.HtmlTableau(
                        "Demande de déplacement validée par N+1",
                        $"Demande de {info.Salarie} ({info.Objet}) validée par {nom1}. "
                        + "Veuillez préparer la note de frais.",
                        Lignes(info)));

                Application.ShowViewStrategy?.ShowMessage(
                    "Demande validée. L'assistant RH a été notifié.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── N+1 : Rejeter ─────────────────────────────────────────
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

                var oldStatut = d.Statut;
                var motif = d.MotifRejet;
                d.RejeterN1(motif);

                var info = Info(d);

                // Notification in-app salarié
                if (d.Salarie?.Oid is Guid oidSal)
                    _Notifier(oidSal,
                        "Votre demande de déplacement a été rejetée",
                        $"Votre demande '{info.Objet}' a été rejetée. Motif : {motif}.");

                AuditService.Enregistrer(Application, "DemandeDeplacement", "RejeterN1",
                    d.Oid.ToString(), d.DisplayName,
                    $"Rejetée. Motif : {motif}",
                    ancienStatut: oldStatut.ToString(), nouveauStatut: d.Statut.ToString());

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                // Email salarié
                if (!string.IsNullOrWhiteSpace(d.Salarie?.Email))
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                        new[] { d.Salarie.Email },
                        "[AdiPAIE] Votre demande de déplacement a été rejetée",
                        WorkflowEmailHelper.HtmlTableau(
                            "Demande de déplacement rejetée",
                            $"Votre demande '{info.Objet}' a été rejetée. "
                            + $"Motif : {motif}. Vous pouvez la modifier et la resoumettre.",
                            Lignes(info)));

                Application.ShowViewStrategy?.ShowMessage(
                    "Demande rejetée.", InformationType.Warning, 3000, InformationPosition.Top);
            };

            // ── Assistant : Initialiser les frais ─────────────────────
            initialiserFraisAction = new SimpleAction(this,
                "Deplacement_InitialiserFrais", PredefinedCategory.Edit)
            {
                Caption = "Initialiser les frais",
                ImageName = "Action_Refresh",
                ConfirmationMessage = "Initialiser les lignes de frais depuis le référentiel ?"
            };
            initialiserFraisAction.Execute += InitialiserFraisAction_Execute;

            // ── Assistant : Soumettre au RH ───────────────────────────
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
                var oldStatut = d.Statut;
                d.SoumettreAuRH();

                var info = Info(d);
                var rhEmails = WorkflowEmailHelper.ExtraireEmailsRH(Application);

                AuditService.Enregistrer(Application, "DemandeDeplacement", "SoumettreRH",
                    d.Oid.ToString(), d.DisplayName,
                    $"Soumise au RH. Total frais : {info.Frais}",
                    ancienStatut: oldStatut.ToString(), nouveauStatut: d.Statut.ToString());

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                WorkflowEmailHelper.EnvoyerEmailsAsync(Application, rhEmails,
                    $"[AdiPAIE] Ordre de mission à approuver — {info.Salarie}",
                    WorkflowEmailHelper.HtmlTableau(
                        "Ordre de mission à approuver",
                        $"La note de frais de {info.Salarie} ({info.Objet}) est prête. "
                        + $"Total : {info.Frais}.",
                        Lignes(info)));

                Application.ShowViewStrategy?.ShowMessage(
                    $"Soumis au RH. Total frais : {info.Frais}.",
                    InformationType.Success, 4000, InformationPosition.Top);
            };

            // ── RH : Approuver ────────────────────────────────────────
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
                var oldStatut = d.Statut;
                d.ApprouverRH();

                var info = Info(d);
                var dafEmails = WorkflowEmailHelper.ExtraireEmailsDAF(Application);

                // Générer les documents si pas encore fait
                if (string.IsNullOrWhiteSpace(d.NumeroOrdre))
                    d.NumeroOrdre = GenererNumeroOrdre(d);

                AuditService.Enregistrer(Application, "DemandeDeplacement", "ApprouverRH",
                    d.Oid.ToString(), d.DisplayName,
                    $"Approuvée par RH. Montant : {info.Frais}. N° {info.Ordre}",
                    ancienStatut: oldStatut.ToString(), nouveauStatut: d.Statut.ToString());

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                WorkflowEmailHelper.EnvoyerEmailsAsync(Application, dafEmails,
                    $"[AdiPAIE] Ordre de mission approuvé — {info.Salarie}",
                    WorkflowEmailHelper.HtmlTableau(
                        "Ordre de mission approuvé — décaissement requis",
                        $"Veuillez procéder au décaissement pour la mission de {info.Salarie} : "
                        + $"{info.Objet}. Montant : {info.Frais}. N° {info.Ordre}.",
                        Lignes(info)));

                Application.ShowViewStrategy?.ShowMessage(
                    "Ordre approuvé. Le DAF a été notifié.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── RH : Rejeter ──────────────────────────────────────────
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

                var oldStatut = d.Statut;
                var motif = d.MotifRejet;
                var rhEmails = WorkflowEmailHelper.ExtraireEmailsRH(Application);
                d.RejeterRH(motif);

                var info = Info(d);

                AuditService.Enregistrer(Application, "DemandeDeplacement", "RejeterRH",
                    d.Oid.ToString(), d.DisplayName,
                    $"Rejetée par RH. Motif : {motif}",
                    ancienStatut: oldStatut.ToString(), nouveauStatut: d.Statut.ToString());

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                WorkflowEmailHelper.EnvoyerEmailsAsync(Application, rhEmails,
                    $"[AdiPAIE] Ordre rejeté — {info.Salarie}",
                    WorkflowEmailHelper.HtmlTableau(
                        "Ordre de mission rejeté par le RH",
                        $"L'ordre de mission de {info.Salarie} a été rejeté. "
                        + $"Motif : {motif}. Veuillez corriger et resoumettre.",
                        Lignes(info)));

                Application.ShowViewStrategy?.ShowMessage(
                    "Ordre rejeté.", InformationType.Warning, 3000, InformationPosition.Top);
            };

            // ── Générer ordre de mission ──────────────────────────────
            genererOrdreAction = new SimpleAction(this,
                "Deplacement_GenererOrdre", PredefinedCategory.Edit)
            {
                Caption = "Générer l'ordre de mission",
                ImageName = "Action_Export"
            };
            genererOrdreAction.Execute += GenererOrdreAction_Execute;

            // ── Générer état de frais ─────────────────────────────────
            genererEtatFraisAction = new SimpleAction(this,
                "Deplacement_GenererEtatFrais", PredefinedCategory.Edit)
            {
                Caption = "Générer l'état de frais",
                ImageName = "Action_Export"
            };
            genererEtatFraisAction.Execute += GenererEtatFraisAction_Execute;

            // ── DAF : Valider décaissement ────────────────────────────
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
                var oldStatut = d.Statut;
                d.ValiderDAF();

                var info = Info(d);
                var comptableEmails = WorkflowEmailHelper.ExtraireEmailsComptable(Application);

                AuditService.Enregistrer(Application, "DemandeDeplacement", "ValiderDAF",
                    d.Oid.ToString(), d.DisplayName,
                    $"Décaissement validé. Montant : {info.Frais}",
                    ancienStatut: oldStatut.ToString(), nouveauStatut: d.Statut.ToString());

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                // Email comptable (sans pièce jointe — IEmailSender ne supporte pas
                // les PJ en async cross-thread, les documents sont accessibles dans l'appli)
                WorkflowEmailHelper.EnvoyerEmailsAsync(Application, comptableEmails,
                    $"[AdiPAIE] Décaissement validé — {info.Salarie}",
                    WorkflowEmailHelper.HtmlTableau(
                        "Décaissement validé — opération à enregistrer",
                        $"Veuillez enregistrer l'opération comptable pour la mission de "
                        + $"{info.Salarie} : {info.Objet}. Montant : {info.Frais}. "
                        + $"N° {info.Ordre}.",
                        Lignes(info)));

                Application.ShowViewStrategy?.ShowMessage(
                    "Décaissement validé. Le comptable a été notifié.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };

            // ── Comptable : Confirmer ─────────────────────────────────
            confirmerComptableAction = new SimpleAction(this,
                "Deplacement_ConfirmerComptable", PredefinedCategory.Edit)
            {
                Caption = "Confirmer l'opération",
                ImageName = "Action_Close",
                ConfirmationMessage = "Confirmer l'opération comptable ?"
            };
            // ── Calculer distances via Google Maps ───────────────────
            calculerDistancesAction = new SimpleAction(this,
                "Deplacement_CalculerDistances", PredefinedCategory.Edit)
            {
                Caption = "Calculer les distances",
                ImageName = "Action_Refresh",
                ToolTip = "Calcule automatiquement les distances du circuit via Google Maps.",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            calculerDistancesAction.Execute += async (s, e) =>
            {
                var d = (DemandeDeplacement)View.CurrentObject;
                if (!d.Circuit.Any())
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        "Aucune étape dans le circuit.",
                        InformationType.Warning, 3000, InformationPosition.Top);
                    return;
                }

                // Récupérer la clé API
                string apiKey;
                try
                {
                    using var os = Application.CreateObjectSpace(typeof(ParametresPaie));
                    var prm = ParametresPaie.TryGet(os);
                    apiKey = prm?.GoogleMapsApiKey;
                }
                catch { apiKey = null; }

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Application.ShowViewStrategy?.ShowMessage(
                        "Clé API Google Maps non configurée. "
                        + "Renseignez-la dans Paramètres de paie → Intégrations.",
                        InformationType.Warning, 5000, InformationPosition.Top);
                    return;
                }

                var etapes = d.Circuit.OrderBy(c => c.Ordre).ToList();
                int nbOk = 0;
                int nbErreur = 0;

                foreach (var etape in etapes)
                {
                    if (etape.VilleDepart == null || etape.VilleArrivee == null)
                        continue;
                    try
                    {
                        var km = await GoogleMapsService.GetDistanceKmAsync(
                            etape.VilleDepart?.Nom, etape.VilleArrivee?.Nom, apiKey);
                        if (km.HasValue)
                        {
                            etape.DistanceKm = km.Value;
                            nbOk++;
                        }
                        else
                        {
                            nbErreur++;
                            DevExpress.Persistent.Base.Tracing.Tracer.LogWarning(
                                $"Distance non trouvée : {etape.VilleDepart?.Nom} → {etape.VilleArrivee?.Nom}");
                        }
                    }
                    catch (Exception ex)
                    {
                        nbErreur++;
                        DevExpress.Persistent.Base.Tracing.Tracer.LogError(nameof(DeplacementWorkflowController) + " : " + ex.Message);
                    }
                }

                ObjectSpace.CommitChanges();
                View.Refresh();

                var msg = nbErreur == 0
                    ? $"{nbOk} distance(s) calculée(s) avec succès."
                    : $"{nbOk} calculée(s), {nbErreur} non trouvée(s).";
                Application.ShowViewStrategy?.ShowMessage(
                    msg,
                    nbErreur == 0 ? InformationType.Success : InformationType.Warning,
                    4000, InformationPosition.Top);
            };

            confirmerComptableAction.Execute += (s, e) =>
            {
                var d = (DemandeDeplacement)View.CurrentObject;
                var oldStatut = d.Statut;
                d.ConfirmerComptable();
                _ArchiverDansDossier(d);

                var info = Info(d);

                // Notification in-app salarié
                if (d.Salarie?.Oid is Guid oidSal)
                    _Notifier(oidSal,
                        "Votre mission est clôturée",
                        $"L'opération comptable pour votre mission '{info.Objet}' "
                        + $"a été enregistrée. Montant : {info.Frais}.");

                AuditService.Enregistrer(Application, "DemandeDeplacement", "ConfirmerComptable",
                    d.Oid.ToString(), d.DisplayName,
                    $"Opération comptable enregistrée et archivée. Montant : {info.Frais}",
                    ancienStatut: oldStatut.ToString(), nouveauStatut: d.Statut.ToString());

                ObjectSpace.CommitChanges();
                UpdateStates(); View.Refresh();

                // Email salarié
                if (!string.IsNullOrWhiteSpace(d.Salarie?.Email))
                    WorkflowEmailHelper.EnvoyerEmailsAsync(Application,
                        new[] { d.Salarie.Email },
                        "[AdiPAIE] Votre mission est clôturée",
                        WorkflowEmailHelper.HtmlTableau(
                            "Mission clôturée",
                            $"L'opération comptable pour votre mission '{info.Objet}' "
                            + $"a été enregistrée. Montant : {info.Frais}.",
                            Lignes(info)));

                Application.ShowViewStrategy?.ShowMessage(
                    "Mission clôturée et archivée.",
                    InformationType.Success, 3000, InformationPosition.Top);
            };
        }

        // ════════════════════════════════════════════════════════════════
        // CYCLE DE VIE
        // ════════════════════════════════════════════════════════════════

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
            approuverRHAction.Active["s"] = s == DeplacementStatut.EnAttenteRH;
            rejeterRHAction.Active["s"] = s == DeplacementStatut.EnAttenteRH;
            genererOrdreAction.Active["s"] = s == DeplacementStatut.SoumiseAssistant
                                                 || s == DeplacementStatut.EnAttenteRH
                                                 || s == DeplacementStatut.ApprouveeRH;
            genererEtatFraisAction.Active["s"] = s == DeplacementStatut.SoumiseAssistant
                                                 || s == DeplacementStatut.EnAttenteRH
                                                 || s == DeplacementStatut.ApprouveeRH;
            validerDAFAction.Active["s"] = s == DeplacementStatut.ApprouveeRH;
            confirmerComptableAction.Active["s"] = s == DeplacementStatut.EnAttenteComptable;
        }

        // ════════════════════════════════════════════════════════════════
        // HANDLERS GÉNÉRATION DOCUMENTS
        // ════════════════════════════════════════════════════════════════

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

                var fileData = ObjectSpace.CreateObject<DevExpress.Persistent.BaseImpl.FileData>();
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

                var fileData = ObjectSpace.CreateObject<DevExpress.Persistent.BaseImpl.FileData>();
                using var ms = new System.IO.MemoryStream(pdfBytes);
                fileData.LoadFromStream(nomFic, ms);
                d.DocumentFrais = fileData;

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
                    + "Configurez-les dans Paramétrage → Catégories de frais.");

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

        // ════════════════════════════════════════════════════════════════
        // HELPERS PRIVÉS
        // ════════════════════════════════════════════════════════════════

        // ── Notification in-app (thread UI uniquement) ────────────────
        private void _Notifier(Guid destOid, string titre, string corps)
        {
            try
            {
                var salarie = ObjectSpace.GetObjectByKey<Salarie>(destOid);
                if (salarie == null) return;
                var notif = ObjectSpace.CreateObject<NotificationSalarie>();
                notif.Salarie = salarie;
                notif.Titre = titre;
                notif.Corps = corps;
                notif.Categorie = "Mission";
                notif.Priorite = NotificationPriorite.Important;
                // Pas de CommitChanges ici — l'appelant commite juste après
            }
            catch (Exception ex) { DevExpress.Persistent.Base.Tracing.Tracer.LogError("Erreur controller deplacement : " + ex.Message); }
        }

        // ── Données de la demande (primitives — safe pour Task.Run) ───
        private record DemandeInfo(
            string Salarie, string Objet,
            string Depart, string Retour, string Jours,
            string Frais, string Ordre);

        private static DemandeInfo Info(DemandeDeplacement d) => new(
            Salarie: d.Salarie?.FullName ?? "—",
            Objet: d.Objet ?? "—",
            Depart: d.DateDepart.ToString("dd/MM/yyyy"),
            Retour: d.DateRetour.ToString("dd/MM/yyyy"),
            Jours: d.NombreJours.ToString(),
            Frais: d.TotalFrais.ToString("N0") + " FCFA",
            Ordre: d.NumeroOrdre ?? "—");

        private static (string, string)[] Lignes(DemandeInfo i) => new[]
        {
            ("Salarié",    i.Salarie),
            ("Objet",      i.Objet),
            ("Départ",     i.Depart),
            ("Retour",     i.Retour),
            ("Jours",      i.Jours),
            ("N° Ordre",   i.Ordre),
            ("Total frais",i.Frais),
        };

        // ── Numéro d'ordre ────────────────────────────────────────────
        private string GenererNumeroOrdre(DemandeDeplacement d)
        {
            var annee = d.DateDepart.Year;
            var count = ObjectSpace.GetObjectsQuery<DemandeDeplacement>()
                .Count(x => x.DateDepart.Year == annee
                          && !string.IsNullOrEmpty(x.NumeroOrdre));
            return $"OM-{annee}-{(count + 1):D4}";
        }

        // ── Archivage dossier salarié ─────────────────────────────────
        private void _ArchiverDansDossier(DemandeDeplacement d)
        {
            try
            {
                if (d.Salarie == null) return;
                var dossier = ObjectSpace.GetObjectsQuery<DossierSalarie>()
                    .FirstOrDefault(x => x.Salarie.Oid == d.Salarie.Oid)
                    ?? ObjectSpace.CreateObject<DossierSalarie>();
                if (dossier.Salarie == null) dossier.Salarie = d.Salarie;

                var doc = ObjectSpace.CreateObject<DossierDocument>();
                doc.Dossier = dossier;
                doc.Categorie = DossierCategorieDocument.Autre;
                doc.Titre = $"Ordre de mission {d.NumeroOrdre} — {d.Objet}";
                doc.SourceAuto = "Généré automatiquement à la clôture de la mission";
                doc.DateDocument = DateTime.Today;
            }
            catch { }
        }

        // ── Conversion PDF via LibreOffice ────────────────────────────
        private byte[] ConvertirEnPdf(byte[] docxBytes)
        {
            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "AdiPAIE_Mission");
            System.IO.Directory.CreateDirectory(tempDir);

            var docxPath = System.IO.Path.Combine(tempDir, $"mission_{Guid.NewGuid():N}.docx");
            var pdfPath = System.IO.Path.ChangeExtension(docxPath, ".pdf");
            System.IO.File.WriteAllBytes(docxPath, docxBytes);
            try
            {
                var chemins = new[]
                {
                    @"C:\Program Files\LibreOffice\program\soffice.exe",
                    @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
                    "/usr/bin/soffice", "soffice"
                };
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
                if (!proc.WaitForExit(30_000)) { proc.Kill(); return null; }
                return System.IO.File.Exists(pdfPath)
                    ? System.IO.File.ReadAllBytes(pdfPath)
                    : null;
            }
            catch { return null; }
            finally
            {
                try { System.IO.File.Delete(docxPath); } catch { }
                try { if (System.IO.File.Exists(pdfPath)) System.IO.File.Delete(pdfPath); } catch { }
            }
        }
    }
}
