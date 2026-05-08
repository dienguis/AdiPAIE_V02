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
            var mvt = os.CreateObject<MouvementInterimaire>();
            mvt.Interimaire = d.Interimaire;
            mvt.DateMouvement = d.DateSouhaitee;
            mvt.ValideRH = true;
            mvt.SaisiPar = userName;
            mvt.TypeMouvement = MapTypeToMvtInterim(d.TypeMouvement);
            mvt.Motif = ConstruireMotifMouvement(d);

            // ── 2) Mettre à jour le ContratInterim actif selon TypeMouvement
            var contrat = d.Interimaire.ContratActif;
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
    }
}
