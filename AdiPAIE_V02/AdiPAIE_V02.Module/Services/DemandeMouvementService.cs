// =============================================================================
//  DemandeMouvementService.cs — V1.5
//
//  Orchestration des transitions de statut sur DemandeMouvementInterim,
//  appliquées par les controllers (BulletinPublishController-style).
//
//  Workflow :
//
//    Brouillon
//      └─[Soumettre (AC)]─→ SoumiseAssistantRH
//                            ├─[ValiderAssistantRH]─→ ValideeAssistantRH
//                            │                          ├─[ValiderRH]─→ ValideeRH
//                            │                          │                ├─[(option) ApprouverDAF]─→ ValideeDAF
//                            │                          │                │                            └─[Appliquer]─→ Appliquee
//                            │                          │                └─[Appliquer]─→ Appliquee
//                            │                          └─[RejeterRH (motif)]─→ RejeteeRH (retour Brouillon)
//                            ├─[RejeterAssistantRH (motif)]─→ RejeteeAssistantRH (retour Brouillon)
//                            └─[ValiderRH (court-circuit, si AssistantRH absent)]─→ ValideeRH
//
//    Annulation possible par initiateur ou RH à toute étape avant Appliquee.
//
//  Effets de Appliquer :
//    - Création MouvementInterimaire (ValideRH = true, déjà validé)
//    - Mise à jour du ContratInterim actif selon TypeMouvement
//    - Email RFE si OptionNotifierRFE
// =============================================================================
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class DemandeMouvementService
    {
        // ─────────────────────────────────────────────────────────────────
        // SOUMETTRE — Brouillon → SoumiseAssistantRH
        // ─────────────────────────────────────────────────────────────────
        public static void Soumettre(
            DemandeMouvementInterim d,
            IObjectSpace os,
            string userName,
            ILogger logger = null)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            if (d.Statut != DemandeMouvementStatut.Brouillon)
                throw new UserFriendlyException(
                    $"Seule une demande en Brouillon peut être soumise (statut actuel : {d.Statut}).");

            ValiderPreconditionsSoumission(d);

            d.Statut = DemandeMouvementStatut.SoumiseAssistantRH;
            os.CommitChanges();
            Audit(os, d, "Soumettre", userName, "Demande soumise à l'Assistant RH.");
            logger?.LogInformation("Demande {Ref} soumise par {User}.", d.Reference, userName);
        }

        // ─────────────────────────────────────────────────────────────────
        // VALIDER ASSISTANT RH — SoumiseAssistantRH → ValideeAssistantRH
        // ─────────────────────────────────────────────────────────────────
        public static void ValiderAssistantRH(
            DemandeMouvementInterim d,
            IObjectSpace os,
            string userName,
            string commentaire,
            ILogger logger = null)
        {
            if (d.Statut != DemandeMouvementStatut.SoumiseAssistantRH)
                throw new UserFriendlyException(
                    $"Seule une demande soumise à l'Assistant RH peut être validée à ce stade.");

            d.Statut = DemandeMouvementStatut.ValideeAssistantRH;
            d.AssistantRHValidationUser = userName;
            d.AssistantRHDate = DateTime.Now;
            d.AssistantRHCommentaire = commentaire;
            os.CommitChanges();
            Audit(os, d, "ValiderAssistantRH", userName, commentaire);
            NotifierAcFireForget(d, os,
                "Demande validée par l'Assistant RH — en attente RH",
                commentaire, logger);
        }

        public static void RejeterAssistantRH(
            DemandeMouvementInterim d,
            IObjectSpace os,
            string userName,
            string motifRejet,
            ILogger logger = null)
        {
            if (d.Statut != DemandeMouvementStatut.SoumiseAssistantRH)
                throw new UserFriendlyException(
                    "Seule une demande soumise à l'Assistant RH peut être rejetée à ce stade.");
            if (string.IsNullOrWhiteSpace(motifRejet))
                throw new UserFriendlyException("Le motif de rejet est obligatoire.");

            d.Statut = DemandeMouvementStatut.RejeteeAssistantRH;
            d.AssistantRHValidationUser = userName;
            d.AssistantRHDate = DateTime.Now;
            d.AssistantRHCommentaire = motifRejet;
            os.CommitChanges();
            Audit(os, d, "RejeterAssistantRH", userName, motifRejet);
            NotifierAcFireForget(d, os,
                "Demande rejetée par l'Assistant RH",
                motifRejet, logger);
        }

        // ─────────────────────────────────────────────────────────────────
        // VALIDER RH (court-circuit possible)
        // → Soit après ValideeAssistantRH (cas normal)
        // → Soit direct depuis SoumiseAssistantRH (court-circuit si AssistantRH absent)
        // ─────────────────────────────────────────────────────────────────
        public static void ValiderRH(
            DemandeMouvementInterim d,
            IObjectSpace os,
            string userName,
            string commentaire,
            bool courtCircuit = false,
            ILogger logger = null)
        {
            bool transitionOK =
                d.Statut == DemandeMouvementStatut.ValideeAssistantRH ||
                (courtCircuit && d.Statut == DemandeMouvementStatut.SoumiseAssistantRH);

            if (!transitionOK)
                throw new UserFriendlyException(
                    $"Transition invalide vers ValideeRH (statut actuel : {d.Statut}, courtCircuit={courtCircuit}).");

            d.Statut = DemandeMouvementStatut.ValideeRH;
            d.RHValidationUser = userName;
            d.RHDate = DateTime.Now;
            d.RHCommentaire = commentaire;
            os.CommitChanges();
            Audit(os, d, courtCircuit ? "ValiderRH (court-circuit)" : "ValiderRH",
                  userName, commentaire);
            NotifierAcFireForget(d, os,
                d.OptionApprobationDAF
                    ? "Demande validée par RH — en attente DAF"
                    : "Demande validée par RH — prête à être appliquée",
                commentaire, logger);
        }

        public static void RejeterRH(
            DemandeMouvementInterim d,
            IObjectSpace os,
            string userName,
            string motifRejet,
            ILogger logger = null)
        {
            if (d.Statut != DemandeMouvementStatut.ValideeAssistantRH &&
                d.Statut != DemandeMouvementStatut.SoumiseAssistantRH)
                throw new UserFriendlyException(
                    "Seule une demande validée Assistant RH (ou soumise en court-circuit) peut être rejetée par RH.");
            if (string.IsNullOrWhiteSpace(motifRejet))
                throw new UserFriendlyException("Le motif de rejet est obligatoire.");

            d.Statut = DemandeMouvementStatut.RejeteeRH;
            d.RHValidationUser = userName;
            d.RHDate = DateTime.Now;
            d.RHCommentaire = motifRejet;
            os.CommitChanges();
            Audit(os, d, "RejeterRH", userName, motifRejet);
            NotifierAcFireForget(d, os, "Demande rejetée par RH", motifRejet, logger);
        }

        // ─────────────────────────────────────────────────────────────────
        // APPROUVER DAF (si OptionApprobationDAF) — ValideeRH → ValideeDAF
        // ─────────────────────────────────────────────────────────────────
        public static void ApprouverDAF(
            DemandeMouvementInterim d,
            IObjectSpace os,
            string userName,
            string commentaire,
            ILogger logger = null)
        {
            if (!d.OptionApprobationDAF)
                throw new UserFriendlyException(
                    "Cette demande ne requiert pas d'approbation DAF. Passez directement à Appliquer.");
            if (d.Statut != DemandeMouvementStatut.ValideeRH)
                throw new UserFriendlyException(
                    $"L'approbation DAF nécessite le statut ValideeRH (actuel : {d.Statut}).");

            d.Statut = DemandeMouvementStatut.ValideeDAF;
            d.DAFValidationUser = userName;
            d.DAFDate = DateTime.Now;
            d.DAFCommentaire = commentaire;
            os.CommitChanges();
            Audit(os, d, "ApprouverDAF", userName, commentaire);
            NotifierAcFireForget(d, os,
                "Demande approuvée par DAF — prête à être appliquée",
                commentaire, logger);
        }

        public static void RejeterDAF(
            DemandeMouvementInterim d,
            IObjectSpace os,
            string userName,
            string motifRejet,
            ILogger logger = null)
        {
            if (d.Statut != DemandeMouvementStatut.ValideeRH)
                throw new UserFriendlyException(
                    "Seule une demande ValideeRH peut être rejetée par DAF.");
            if (string.IsNullOrWhiteSpace(motifRejet))
                throw new UserFriendlyException("Le motif de rejet est obligatoire.");

            d.Statut = DemandeMouvementStatut.RejeteeDAF;
            d.DAFValidationUser = userName;
            d.DAFDate = DateTime.Now;
            d.DAFCommentaire = motifRejet;
            os.CommitChanges();
            Audit(os, d, "RejeterDAF", userName, motifRejet);
            NotifierAcFireForget(d, os, "Demande rejetée par DAF", motifRejet, logger);
        }

        // ─────────────────────────────────────────────────────────────────
        // APPLIQUER — création MouvementInterimaire + maj ContratInterim
        // → Depuis ValideeRH ou ValideeDAF (selon option)
        // ─────────────────────────────────────────────────────────────────
        public static async Task AppliquerAsync(
            DemandeMouvementInterim d,
            IObjectSpace os,
            string userName,
            ILogger logger = null)
        {
            // Pré-condition : statut compatible
            bool peutAppliquer =
                d.Statut == DemandeMouvementStatut.ValideeRH ||
                d.Statut == DemandeMouvementStatut.ValideeDAF;
            if (!peutAppliquer)
                throw new UserFriendlyException(
                    $"L'application nécessite ValideeRH (ou ValideeDAF si option DAF active). Statut actuel : {d.Statut}.");

            // Si OptionApprobationDAF + pas encore validé DAF → bloquer
            if (d.OptionApprobationDAF && d.Statut != DemandeMouvementStatut.ValideeDAF)
                throw new UserFriendlyException(
                    "Cette demande requiert l'approbation DAF avant application.");

            if (d.Interimaire == null)
                throw new UserFriendlyException("Intérimaire manquant.");

            // ── 1) Créer MouvementInterimaire (déjà validé RH) ──────────
            // IMPORTANT : on capture l'AVANT (origine) avant toute modification
            // du contrat, pour préserver l'historique complet sur le dashboard
            // et la fiche intérimaire.
            var contrat = d.Interimaire.ContratActif;
            var stationAvant = d.StationOrigine ?? contrat?.Station;
            var siteAvantV1 = contrat?.Site;

            var mvt = os.CreateObject<MouvementInterimaire>();
            mvt.Interimaire = d.Interimaire;
            mvt.DateMouvement = d.DateSouhaitee;
            mvt.ValideRH = true;
            mvt.SaisiPar = userName;
            mvt.TypeMouvement = MapTypeToMvtInterim(d.TypeMouvement);
            mvt.Motif = ConstruireMotifMouvement(d);

            // ── 1b) Capture historique : Origine ─────────────────────────
            // Toujours rempli pour permettre la reconstruction de la chrono.
            mvt.StationOrigine = stationAvant;
            mvt.SiteOrigineV1 = siteAvantV1;

            // ── 1c) Capture historique : Destination ─────────────────────
            // Selon TypeMouvement, la destination peut être identique
            // (ChangementPoste = pas de changement de station) ou différente.
            switch (d.TypeMouvement)
            {
                case TypeMouvementInterim.ChangementStation:
                    mvt.StationDestination = d.StationDestination;
                    break;
                case TypeMouvementInterim.RemplacementTemporaire:
                    // Remplacement = nouveau site temporaire si renseigné,
                    // sinon on reste sur la station d'origine
                    mvt.StationDestination = d.StationDestination ?? stationAvant;
                    break;
                case TypeMouvementInterim.ChangementPoste:
                case TypeMouvementInterim.FinMissionAnticipee:
                case TypeMouvementInterim.Autre:
                default:
                    // Pas de changement de station → destination = origine
                    mvt.StationDestination = stationAvant;
                    break;
            }

            // ── 2) Mettre à jour le ContratInterim actif selon TypeMouvement
            if (contrat != null)
            {
                switch (d.TypeMouvement)
                {
                    case TypeMouvementInterim.ChangementStation:
                        if (d.StationDestination != null)
                            contrat.Station = d.StationDestination;
                        break;

                    case TypeMouvementInterim.ChangementPoste:
                        if (d.PosteSouhaite != null)
                            contrat.PosteOccupe = d.PosteSouhaite;
                        break;

                    case TypeMouvementInterim.FinMissionAnticipee:
                        contrat.Statut = ContratInterimStatut.Termine;
                        contrat.DateFinReelle = d.DateSouhaitee;
                        break;

                    case TypeMouvementInterim.RemplacementTemporaire:
                        // Pas de modification du contrat — un MouvementInterimaire
                        // de type "Affectation temporaire" est suffisant
                        break;

                    case TypeMouvementInterim.Autre:
                        // Pas de mise à jour automatique — RH doit ajuster manuellement
                        break;
                }
            }

            // ── 3) Lien demande → mouvement
            d.MouvementGenere = mvt;
            d.Statut = DemandeMouvementStatut.Appliquee;
            os.CommitChanges();
            Audit(os, d, "Appliquer", userName,
                  $"Mouvement {mvt.TypeMouvement} appliqué le {d.DateSouhaitee:dd/MM/yyyy}.");

            // ── 4) Notification email RFE (best-effort, non bloquant)
            if (d.OptionNotifierRFE)
            {
                try
                {
                    await EnvoyerNotificationRfeAsync(d, os, logger);
                }
                catch (Exception emailEx)
                {
                    logger?.LogWarning(emailEx,
                        "Échec notification RFE pour demande {Ref} — application conservée.",
                        d.Reference);
                }
            }

            // ── 5) Notification email AC initiateur (best-effort)
            await NotifierAcAsync(d, os,
                "Demande appliquée — mouvement effectif",
                $"Mouvement {d.TypeMouvement} effectif au {d.DateSouhaitee:dd/MM/yyyy}.",
                logger);
        }

        // ─────────────────────────────────────────────────────────────────
        // ANNULER — à tout moment avant Appliquee
        // ─────────────────────────────────────────────────────────────────
        public static void Annuler(
            DemandeMouvementInterim d,
            IObjectSpace os,
            string userName,
            string motif,
            ILogger logger = null)
        {
            if (d.Statut == DemandeMouvementStatut.Appliquee)
                throw new UserFriendlyException(
                    "Une demande appliquée ne peut plus être annulée. Créez une nouvelle demande pour défaire le mouvement.");
            if (d.Statut == DemandeMouvementStatut.Annulee)
                return; // idempotent

            d.Statut = DemandeMouvementStatut.Annulee;
            os.CommitChanges();
            Audit(os, d, "Annuler", userName, motif ?? "Annulé sans motif");

            // V1.5.2 — Notif AC seulement si annulation par quelqu'un d'autre
            // (sinon l'AC s'enverrait un email à lui-même → bruit inutile)
            var emailAc = d.Initiateur?.Email?.Trim();
            bool autoAnnulation = !string.IsNullOrWhiteSpace(emailAc)
                && string.Equals(emailAc, userName, StringComparison.OrdinalIgnoreCase);
            if (!autoAnnulation)
            {
                NotifierAcFireForget(d, os,
                    "Demande annulée",
                    motif ?? "Annulée sans motif", logger);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Helpers internes
        // ─────────────────────────────────────────────────────────────────

        private static void ValiderPreconditionsSoumission(DemandeMouvementInterim d)
        {
            if (d.Interimaire == null)
                throw new UserFriendlyException("Intérimaire concerné est obligatoire.");
            if (d.TypeMouvement == null)
                throw new UserFriendlyException("Type de mouvement est obligatoire.");
            if (string.IsNullOrWhiteSpace(d.Motif))
                throw new UserFriendlyException("Motif est obligatoire.");

            switch (d.TypeMouvement)
            {
                case TypeMouvementInterim.ChangementStation:
                    if (d.StationDestination == null)
                        throw new UserFriendlyException(
                            "Station destination obligatoire pour un changement de station.");
                    break;

                case TypeMouvementInterim.ChangementPoste:
                    if (d.PosteSouhaite == null)
                        throw new UserFriendlyException(
                            "Poste souhaité obligatoire pour un changement de poste.");
                    break;

                case TypeMouvementInterim.RemplacementTemporaire:
                    if (d.TitulaireRemplace == null)
                        throw new UserFriendlyException(
                            "Salarié titulaire à remplacer obligatoire.");
                    if (d.DureeRemplacementJours <= 0)
                        throw new UserFriendlyException(
                            "Durée de remplacement (jours) doit être > 0.");
                    break;

                case TypeMouvementInterim.Autre:
                    if (string.IsNullOrWhiteSpace(d.MotifAutre))
                        throw new UserFriendlyException(
                            "Précision obligatoire pour le type « Autre ».");
                    break;
            }
        }

        private static MouvementInterimaireType MapTypeToMvtInterim(TypeMouvementInterim? t) =>
            t switch
            {
                TypeMouvementInterim.ChangementStation => MouvementInterimaireType.MutationInterne,
                TypeMouvementInterim.ChangementPoste => MouvementInterimaireType.Reaffectation,
                TypeMouvementInterim.FinMissionAnticipee => MouvementInterimaireType.FinMission,
                TypeMouvementInterim.RemplacementTemporaire => MouvementInterimaireType.Affectation,
                _ => MouvementInterimaireType.Reaffectation
            };

        private static string ConstruireMotifMouvement(DemandeMouvementInterim d)
        {
            var prefix = $"[{d.Reference}] {d.TypeMouvement}";
            var suffix = !string.IsNullOrWhiteSpace(d.MotifAutre)
                ? $" ({d.MotifAutre})"
                : "";
            return $"{prefix}{suffix} : {d.Motif}";
        }

        private static void Audit(IObjectSpace os, DemandeMouvementInterim d,
            string action, string userName, string details)
        {
            try
            {
                AuditService.EnregistrerDansSession(
                    session: ((XPObjectSpace)os).Session,
                    nomEntite: nameof(DemandeMouvementInterim),
                    action: action,
                    objectId: d.Oid.ToString(),
                    objectLabel: d.Reference,
                    details: details,
                    nouveauStatut: d.Statut.ToString());
            }
            catch { /* audit best-effort */ }
        }

        // ─────────────────────────────────────────────────────────────────
        // NOTIFICATION EMAIL RFE (best-effort)
        // ─────────────────────────────────────────────────────────────────
        private static async Task EnvoyerNotificationRfeAsync(
            DemandeMouvementInterim d, IObjectSpace os, ILogger logger)
        {
            // L'intérimaire a une SocieteInterim associée directement.
            // On récupère l'email renseigné côté SocieteInterim.
            var societe = d.Interimaire?.SocieteInterim;
            var emailRfe = societe?.Email?.Trim();
            if (string.IsNullOrWhiteSpace(emailRfe))
            {
                logger?.LogInformation(
                    "Demande {Ref} : pas d'email RFE renseigné — notification ignorée.",
                    d.Reference);
                return;
            }

            var prm = ParametresPaie.TryGet(os);
            if (prm == null || !prm.EmailActif) return;

            IEmailSender sender;
            try { sender = prm.CreateEmailSender(); }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Demande {Ref} : impossible de créer EmailSender.", d.Reference);
                return;
            }

            var sujet = $"[SunuPaie] Mouvement intérimaire — {d.Interimaire.FullName} — {d.TypeMouvement}";
            var body = BuildEmailRfeHtml(d);

            try
            {
                await sender.SendAsync(emailRfe, sujet, body, attachment: null);
                logger?.LogInformation(
                    "Demande {Ref} : email RFE envoyé à {Email}.", d.Reference, emailRfe);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex,
                    "Demande {Ref} : échec envoi email RFE à {Email}.", d.Reference, emailRfe);
            }
        }

        private static string BuildEmailRfeHtml(DemandeMouvementInterim d)
        {
            var dest = d.StationDestination?.ToString() ?? "—";
            var poste = d.PosteSouhaite?.ToString() ?? d.PosteActuel?.ToString() ?? "—";
            return $@"
<html>
<body style='font-family:Calibri,sans-serif; font-size:14px; color:#222;'>
<p>Bonjour,</p>

<p>Nous vous informons d'un mouvement validé concernant l'intérimaire
<strong>{System.Net.WebUtility.HtmlEncode(d.Interimaire.FullName)}</strong>
({System.Net.WebUtility.HtmlEncode(d.Interimaire.Matricule ?? "—")}) :</p>

<table style='border-collapse:collapse;'>
  <tr><td style='padding:4px 12px 4px 0;'><strong>Type de mouvement</strong></td><td>{d.TypeMouvement}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;'><strong>Date d'effet</strong></td><td>{d.DateSouhaitee:dd/MM/yyyy}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;'><strong>Station destination</strong></td><td>{System.Net.WebUtility.HtmlEncode(dest)}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;'><strong>Poste</strong></td><td>{System.Net.WebUtility.HtmlEncode(poste)}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;'><strong>Référence demande</strong></td><td>{d.Reference}</td></tr>
</table>

<p style='margin-top:16px;'><strong>Motif :</strong><br/>
{System.Net.WebUtility.HtmlEncode(d.Motif)}</p>

<p style='color:#666; font-size:12px;'>
— Service Paie / RH ELTON Oil Company
</p>
</body>
</html>";
        }

        // ═════════════════════════════════════════════════════════════════
        // V1.5.2 — Notification AC sur changement de statut de SA demande
        // L'initiateur (AC) reçoit un email à chaque transition (validée,
        // rejetée, appliquée). Best-effort, non bloquant.
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Envoie un email à l'AC initiateur de la demande pour le tenir
        /// informé du changement de statut. Async + non bloquant.
        /// </summary>
        private static async Task NotifierAcAsync(
            DemandeMouvementInterim d, IObjectSpace os,
            string transitionLibelle, string commentaire, ILogger logger = null)
        {
            try
            {
                var emailAc = d.Initiateur?.Email?.Trim();
                if (string.IsNullOrWhiteSpace(emailAc))
                {
                    logger?.LogInformation(
                        "Demande {Ref} : initiateur sans email — notification AC ignorée.",
                        d.Reference);
                    return;
                }

                var prm = ParametresPaie.TryGet(os);
                if (prm == null || !prm.EmailActif) return;

                IEmailSender sender;
                try { sender = prm.CreateEmailSender(); }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Demande {Ref} : EmailSender KO.", d.Reference);
                    return;
                }

                var sujet = $"[SunuPaie] Votre demande {d.Reference} — {transitionLibelle}";
                var body = BuildEmailAcHtml(d, transitionLibelle, commentaire);

                await sender.SendAsync(emailAc, sujet, body, attachment: null);
                logger?.LogInformation(
                    "Demande {Ref} : notification AC envoyée à {Email} ({Transition}).",
                    d.Reference, emailAc, transitionLibelle);
            }
            catch (Exception ex)
            {
                // Best-effort : un échec email ne doit pas casser la transition
                logger?.LogWarning(ex,
                    "Demande {Ref} : échec notification AC (non bloquant).", d?.Reference);
            }
        }

        /// <summary>
        /// Variante fire-and-forget pour les transitions synchrones.
        /// Avale toutes les exceptions pour ne pas crasher le thread Blazor.
        /// </summary>
        private static void NotifierAcFireForget(
            DemandeMouvementInterim d, IObjectSpace os,
            string transitionLibelle, string commentaire, ILogger logger = null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await NotifierAcAsync(d, os, transitionLibelle, commentaire, logger);
                }
                catch { /* swallowed — déjà loggé par NotifierAcAsync */ }
            });
        }

        private static string BuildEmailAcHtml(
            DemandeMouvementInterim d, string transitionLibelle, string commentaire)
        {
            var prenomAc = d.Initiateur?.FirstName ?? d.Initiateur?.FullName ?? "";
            var interimaireNom = d.Interimaire?.FullName ?? "—";
            var matricule = d.Interimaire?.Matricule ?? "—";
            var commentaireBlock = string.IsNullOrWhiteSpace(commentaire)
                ? ""
                : $@"<p style='margin-top:12px;'><strong>Commentaire :</strong><br/>
                     {System.Net.WebUtility.HtmlEncode(commentaire)}</p>";

            return $@"
<html>
<body style='font-family:Calibri,sans-serif; font-size:14px; color:#222;'>
<p>Bonjour <strong>{System.Net.WebUtility.HtmlEncode(prenomAc)}</strong>,</p>

<p>Le statut de votre demande de mouvement intérim a été mis à jour :</p>

<div style='background:#F1F5F9; border-left:4px solid #0F6E56; padding:10px 14px; margin:10px 0; border-radius:4px;'>
  <strong>{System.Net.WebUtility.HtmlEncode(transitionLibelle)}</strong>
</div>

<table style='border-collapse:collapse;'>
  <tr><td style='padding:4px 12px 4px 0;'><strong>Référence</strong></td><td>{d.Reference}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;'><strong>Intérimaire</strong></td><td>{System.Net.WebUtility.HtmlEncode(interimaireNom)} ({System.Net.WebUtility.HtmlEncode(matricule)})</td></tr>
  <tr><td style='padding:4px 12px 4px 0;'><strong>Type de mouvement</strong></td><td>{d.TypeMouvement}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;'><strong>Date souhaitée</strong></td><td>{d.DateSouhaitee:dd/MM/yyyy}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;'><strong>Statut actuel</strong></td><td>{d.Statut}</td></tr>
</table>

{commentaireBlock}

<p style='margin-top:14px;'>
Connectez-vous à votre Espace Salarié SunuPaie → menu
<em>Mes demandes mouvement intérim</em> pour consulter le détail.
</p>

<p style='color:#666; font-size:12px;'>
— Service Paie / RH ELTON Oil Company
</p>
</body>
</html>";
        }
    }
}
