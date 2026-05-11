// =============================================================================
//  GratificationService.cs — V1.7.2d
//
//  Service métier de la Gratification ad hoc.
//
//  Workflow (cf. grille RH validée 2026-05-11) :
//
//   ┌─ BrouillonRH ─┐  Soumettre        ┌─ EnAttente ─┐  Valider     ┌─ Validee ─┐
//   │ RH saisit     ├──────────────────▶│ ValidationDAF├─────────────▶│ DAF       │
//   │ (calcul auto) │                   │ DAF examine │              │ approuve  │
//   └───────────────┘  ←─Rejeter────────┴─────────────┘              └─────┬─────┘
//                                                                          │
//                                                              Intégrer    │
//                                                                          ▼
//                                                                  ┌─ Integree ─┐
//                                                                  │ RH intègre │
//                                                                  │ au bulletin│
//                                                                  └────────────┘
//
//  Calcul du montant selon la base :
//    - BrutRecurrent : MontantCalcule = BR du mois × Multiplicateur
//    - NetRecurrent  : MontantCalcule = NetAPayer du mois × Multiplicateur
//    - Forfait       : MontantCalcule = MontantForfait
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class GratificationService
    {
        // Code de la rubrique GRATIF seedée dans Updater.cs
        private const string CodeRubriqueGratif = "GRATIF";

        // ─────────────────────────────────────────────────────────────
        // ÉTAPE 0 — CALCUL DU MONTANT (à la volée)
        // Recalcule MontantCalcule + BaseReference selon BaseCalcul.
        // Appelé automatiquement à la soumission. Peut aussi être
        // appelé à tout moment pour preview (avant soumission).
        // ─────────────────────────────────────────────────────────────
        public static void RecalculerMontant(Gratification g)
        {
            if (g == null) throw new ArgumentNullException(nameof(g));
            if (g.Salarie == null) return;

            var session = g.Session;

            switch (g.BaseCalcul)
            {
                case GratificationBaseCalcul.BrutRecurrent:
                    g.BaseReference = BrutRecurrentService.GetDernierBrutRecurrent(
                        session, g.Salarie, g.Annee, g.MoisPaiement);
                    g.MontantCalcule = Math.Round(
                        g.BaseReference * g.Multiplicateur,
                        0, MidpointRounding.AwayFromZero);
                    break;

                case GratificationBaseCalcul.NetRecurrent:
                    g.BaseReference = BrutRecurrentService.GetDernierNetAPayer(
                        session, g.Salarie, g.Annee, g.MoisPaiement);
                    g.MontantCalcule = Math.Round(
                        g.BaseReference * g.Multiplicateur,
                        0, MidpointRounding.AwayFromZero);
                    break;

                case GratificationBaseCalcul.Forfait:
                    g.BaseReference = 0m;
                    g.MontantCalcule = g.MontantForfait;
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // ÉTAPE 1 — SOUMETTRE AU DAF (par RH)
        // BrouillonRH → EnAttenteValidationDAF
        // ─────────────────────────────────────────────────────────────
        public static void Soumettre(IObjectSpace os, Gratification g)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (g == null) throw new ArgumentNullException(nameof(g));

            if (g.Statut != GratificationStatut.BrouillonRH)
                throw new UserFriendlyException(
                    $"Impossible de soumettre une gratification dont le statut " +
                    $"n'est pas Brouillon (actuel : {g.Statut}).");

            if (g.Salarie == null)
                throw new UserFriendlyException(
                    "Salarié obligatoire avant soumission au DAF.");

            // Recalcul du montant (snapshot figé pour validation DAF)
            RecalculerMontant(g);

            if (g.MontantCalcule <= 0)
                throw new UserFriendlyException(
                    "Montant calculé nul ou négatif. Vérifiez la base " +
                    "de calcul, le multiplicateur, ou le bulletin de référence.");

            g.Statut = GratificationStatut.EnAttenteValidationDAF;
            g.DateSoumission = DateTime.Now;

            os.CommitChanges();
        }

        // ─────────────────────────────────────────────────────────────
        // ÉTAPE 2A — VALIDER (par DAF)
        // EnAttenteValidationDAF → ValideeDAF
        // ─────────────────────────────────────────────────────────────
        public static void Valider(IObjectSpace os, Gratification g)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (g == null) throw new ArgumentNullException(nameof(g));

            if (g.Statut != GratificationStatut.EnAttenteValidationDAF)
                throw new UserFriendlyException(
                    $"Impossible de valider une gratification dont le statut " +
                    $"n'est pas EnAttenteValidationDAF (actuel : {g.Statut}).");

            g.Statut = GratificationStatut.ValideeDAF;
            g.DateValidationDAF = DateTime.Now;
            try { g.ValidePar = SecuritySystem.CurrentUserName; } catch { }

            os.CommitChanges();
        }

        // ─────────────────────────────────────────────────────────────
        // ÉTAPE 2B — REJETER (par DAF)
        // EnAttenteValidationDAF → BrouillonRH (avec motif)
        // ─────────────────────────────────────────────────────────────
        public static void Rejeter(IObjectSpace os, Gratification g, string motif)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (g == null) throw new ArgumentNullException(nameof(g));

            if (g.Statut != GratificationStatut.EnAttenteValidationDAF)
                throw new UserFriendlyException(
                    $"Impossible de rejeter une gratification dont le statut " +
                    $"n'est pas EnAttenteValidationDAF (actuel : {g.Statut}).");

            // Retour à Brouillon — RH peut corriger et resoumettre
            g.Statut = GratificationStatut.BrouillonRH;
            g.DateSoumission = null;

            // Préfixer le commentaire avec le motif du rejet
            var prefix = $"[Rejet DAF {DateTime.Now:yyyy-MM-dd}] {motif}";
            g.Commentaire = string.IsNullOrWhiteSpace(g.Commentaire)
                ? prefix
                : $"{prefix}\r\n----\r\n{g.Commentaire}";

            os.CommitChanges();
        }

        // ─────────────────────────────────────────────────────────────
        // ÉTAPE 3 — INTÉGRER AU BULLETIN (par RH)
        // ValideeDAF → IntegreeBulletin
        // Crée une BulletinLigne GRATIF sur le bulletin du mois choisi.
        // ─────────────────────────────────────────────────────────────
        public static void IntegrerAuBulletin(IObjectSpace os, Gratification g)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (g == null) throw new ArgumentNullException(nameof(g));

            if (g.Statut != GratificationStatut.ValideeDAF)
                throw new UserFriendlyException(
                    $"La gratification doit être ValideeDAF pour être " +
                    $"intégrée (actuel : {g.Statut}).");

            // Trouver la rubrique GRATIF
            var rubriqueGratif = os.FirstOrDefault<Rubrique>(
                r => r.Code == CodeRubriqueGratif);
            if (rubriqueGratif == null)
                throw new UserFriendlyException(
                    $"Rubrique '{CodeRubriqueGratif}' introuvable. " +
                    $"L'Updater doit la créer au démarrage.");

            // Trouver le bulletin du mois de paiement
            var bulletin = os.FirstOrDefault<Bulletin>(
                b => b.Salarie.Oid == g.Salarie.Oid
                     && b.Annee == g.Annee
                     && b.Mois == g.MoisPaiement);

            if (bulletin == null)
                throw new UserFriendlyException(
                    $"Bulletin {g.Annee}/{g.MoisPaiement:00} introuvable pour " +
                    $"{g.Salarie.Matricule} – {g.Salarie.FullName}. " +
                    $"Créer le bulletin avant intégration.");

            // Vérifier qu'il n'y a pas déjà une ligne GRATIF (idempotence)
            // Note : si plusieurs gratifications dans le même mois, elles
            // créent chacune leur propre ligne (BulletinLigne pas restrictive
            // sur le code) — on les distingue via le montant et la
            // référence à la Gratification source.
            var ligne = os.CreateObject<BulletinLigne>();
            ligne.Bulletin = bulletin;
            ligne.Rubrique = rubriqueGratif;
            ligne.Base = g.BaseReference;
            ligne.Taux = g.Multiplicateur;
            ligne.Montant = g.MontantCalcule;
            // Référence dans le champ texte de la ligne pour traçabilité
            ligne.Reference = $"Gratif {g.Oid}";

            bulletin.Lignes.Add(ligne);

            // Mettre à jour la Gratification
            g.Statut = GratificationStatut.IntegreeBulletin;
            g.BulletinLie = bulletin;
            g.DateIntegration = DateTime.Now;
            try { g.IntegrePar = SecuritySystem.CurrentUserName; } catch { }

            os.CommitChanges();
        }

        // ─────────────────────────────────────────────────────────────
        // ANNULER (à tout moment avant intégration)
        // BrouillonRH / EnAttenteValidationDAF / ValideeDAF → Annule
        // ─────────────────────────────────────────────────────────────
        public static void Annuler(IObjectSpace os, Gratification g, string motif)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (g == null) throw new ArgumentNullException(nameof(g));

            if (g.Statut == GratificationStatut.IntegreeBulletin
                || g.Statut == GratificationStatut.Payee)
                throw new UserFriendlyException(
                    "Impossible d'annuler une gratification déjà intégrée " +
                    "au bulletin ou payée. Contactez le DAF pour rectification.");

            g.Statut = GratificationStatut.Annule;

            var prefix = $"[Annulation {DateTime.Now:yyyy-MM-dd}] {motif}";
            g.Commentaire = string.IsNullOrWhiteSpace(g.Commentaire)
                ? prefix
                : $"{prefix}\r\n----\r\n{g.Commentaire}";

            os.CommitChanges();
        }
    }
}
