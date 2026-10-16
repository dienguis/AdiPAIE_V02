// =============================================================================
//  GratificationService.cs - V1.7.2d
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
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class GratificationService
    {
        // Code de la rubrique GRATIF seedée dans Updater.cs
        private const string CodeRubriqueGratif = "GRATIF";

        // ─────────────────────────────────────────────────────────────
        // ÉTAPE 0 - CALCUL DU MONTANT (à la volée)
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

                case GratificationBaseCalcul.NetCibleMois:
                    // V1.12 - Net cible : Multiplicateur = nombre de mois de
                    // net reglementaire a verser en plus au salarie. Le systeme
                    // calcule par iteration le brut qui produit exactement ce
                    // supplement de net sur le bulletin cible.
                    g.BaseReference = BrutRecurrentService.GetDernierNetReglementaire(
                        session, g.Salarie, g.Annee, g.MoisPaiement);
                    if (g.BaseReference <= 0)
                        throw new UserFriendlyException(
                            "Impossible de calculer le net de reference : aucun " +
                            "bulletin valide trouve pour ce salarie dans l'annee " +
                            "en cours ni l'annee precedente.");
                    if (g.Multiplicateur <= 0)
                        throw new UserFriendlyException(
                            "Pour NetCibleMois, le Multiplicateur represente le " +
                            "nombre de mois cible et doit etre superieur a 0.");
                    var netCible = g.BaseReference * g.Multiplicateur;
                    g.MontantCalcule = GrossUpVersBrut(
                        session, g.Salarie, g.Annee, g.MoisPaiement, netCible);
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // V1.12 - GROSS-UP ITERATIF
        // A partir d'un net additionnel cible, trouve le brut gratification
        // X qui, ajoute au bulletin de reference, produit exactement ce net
        // additionnel apres IR progressif.
        //
        // Hypotheses :
        //   - cotisations sociales (IPRES RG+RC) deja plafonnees sur le
        //     bulletin reference -> marginal = 0 sur l'ajout
        //   - TRIMF constante (forfait mensuel invariant)
        //   - IR calcule via le bareme annuel du salarie (annualisation x 12)
        //
        // Convergence : Newton-Raphson avec taux marginal initial 0.6.
        // Typiquement 2 a 4 iterations pour converger a 1 FCFA pres.
        // ─────────────────────────────────────────────────────────────
        private static decimal GrossUpVersBrut(
            Session session, Salarie salarie, int annee, int moisPaiement,
            decimal netAdditionnelCible)
        {
            // Bulletin de reference = dernier bulletin valide
            var bulletinRef = session.Query<Bulletin>()
                .Where(b => b.Salarie.Oid == salarie.Oid
                            && b.Annee == annee
                            && b.Mois < moisPaiement
                            && (b.Statut == BulletinStatut.Valide
                                || b.Statut == BulletinStatut.Imprime
                                || b.Statut == BulletinStatut.Envoye
                                || b.Statut == BulletinStatut.Comptabilise
                                || b.Statut == BulletinStatut.Cloture))
                .OrderByDescending(b => b.Mois)
                .FirstOrDefault()
                ?? session.Query<Bulletin>()
                    .Where(b => b.Salarie.Oid == salarie.Oid
                                && b.Annee == annee - 1
                                && (b.Statut == BulletinStatut.Valide
                                    || b.Statut == BulletinStatut.Imprime
                                    || b.Statut == BulletinStatut.Envoye
                                    || b.Statut == BulletinStatut.Comptabilise
                                    || b.Statut == BulletinStatut.Cloture))
                    .OrderByDescending(b => b.Mois)
                    .FirstOrDefault();

            if (bulletinRef == null)
                throw new UserFriendlyException(
                    "Aucun bulletin valide pour calcul du gross-up.");

            // V1.12 - Deux bases : fiscal (pour IR) et social (pour net cash).
            // Difference = Avantage en nature (fiscal mais pas social).
            var baseFiscalRef   = bulletinRef.BrutFiscal;
            var baseSocialRef   = bulletinRef.BrutSocial;
            var cotisationsRef  = bulletinRef.IPRES_RG_Mois + bulletinRef.IPRES_RC_Mois;
            var irRef           = bulletinRef.IR_Mois;
            var trimfRef        = bulletinRef.TRIMF_Mois;
            // Net reglementaire RH ELTON : BrutSocial moins obligatoires.
            var netRef          = baseSocialRef - cotisationsRef - irRef - trimfRef;

            // Parametres IR
            var p = new XPQuery<ParametresPaie>(session).FirstOrDefault();
            var baremeCode = p?.ResolveCodeBaremeIRAnnuel(annee) ?? $"IR_DPP_{annee}";
            var finMois = new DateTime(annee, moisPaiement, 1).AddMonths(1).AddDays(-1);
            var bareme = new XPQuery<BaremeIR>(session)
                .Where(b => b.Actif
                        && (b.DateDebut == null || b.DateDebut <= finMois)
                        && (b.DateFin == null || finMois <= b.DateFin)
                        && b.Code == baremeCode)
                .FirstOrDefault();
            if (bareme == null)
                throw new UserFriendlyException(
                    $"Bareme IR '{baremeCode}' introuvable pour l'annee {annee}.");

            var parts = Math.Max(1m, salarie?.NombrePartsFiscales ?? 1m);
            var pctImab   = (p?.R_IR_Abattement_TauxPercent ?? 30m) / 100m;
            var plfAnnuel = p?.R_IR_Abattement_PlafondAnnuel ?? 900_000m;
            var plfMensuel = p?.R_IR_Abattement_PlafondMensuel ?? 75_000m;
            var tronquerAuxMille = p?.R_IR_TronquerBaseAuxMille ?? true;

            // Reduction famille
            var rf = new XPQuery<IRReductionFamille>(session)
                .FirstOrDefault(r => r.Actif && r.NbrePart == parts);
            decimal rfPct, rfMinA, rfMaxA;
            if (rf != null)
            {
                rfPct = rf.Taux / 100m;
                rfMinA = rf.MinAnnuel;
                rfMaxA = rf.MaxAnnuel;
            }
            else
            {
                rfPct = (p?.R_IR_ReductionFamille_Pourcentage ?? 0m) / 100m;
                rfMinA = (p?.R_IR_ReductionFamille_MinParPart_Annuel ?? 0m) * parts;
                rfMaxA = (p?.R_IR_ReductionFamille_MaxParPart_Annuel ?? 0m) * parts;
            }

            // Fonction : pour un brut gratif X, retourne le gain de net
            decimal SimulerGainNet(decimal brutGratif)
            {
                var newBaseFiscal = baseFiscalRef + brutGratif;
                var baseMens = tronquerAuxMille
                    ? Math.Floor(newBaseFiscal / 1000m) * 1000m
                    : newBaseFiscal;

                var revenuAn = baseMens * 12m;
                var abattAn = revenuAn * pctImab;
                var imabMens = (abattAn >= plfAnnuel)
                    ? plfMensuel
                    : Math.Round(abattAn / 12m, 0, MidpointRounding.AwayFromZero);

                var dppAn = Math.Max(0m, (baseMens - imabMens) * 12m);

                decimal irAn = 0m;
                foreach (var t in bareme.Tranches.OrderBy(x => x.MontantMin))
                {
                    if (dppAn <= t.MontantMin) break;
                    var sup = Math.Min(dppAn, t.MontantMax);
                    var assiette = Math.Max(0m, sup - t.MontantMin);
                    if (assiette <= 0) continue;
                    irAn += Math.Round(assiette * (t.Taux / 100m), 0,
                                       MidpointRounding.AwayFromZero);
                    if (dppAn <= t.MontantMax) break;
                }

                var reduc = Math.Round(irAn * rfPct, 0, MidpointRounding.AwayFromZero);
                if (rfMinA > 0 && reduc < rfMinA) reduc = rfMinA;
                if (rfMaxA > 0 && reduc > rfMaxA) reduc = rfMaxA;
                var irAnNet = Math.Max(0m, irAn - reduc);
                var newIrMens = Math.Round(irAnNet / 12m, 0, MidpointRounding.AwayFromZero);

                // GRATIF ajoute le meme montant brut au fiscal ET au social
                // (rubrique Gain Brute). Donc baseSocial augmente de brutGratif
                // identique a baseFiscal.
                var newBaseSocial = baseSocialRef + brutGratif;
                var newNet = newBaseSocial - cotisationsRef - newIrMens - trimfRef;
                return newNet - netRef;
            }

            // Newton-Raphson : init a cible / (1 - taux marginal 40%)
            decimal brut = netAdditionnelCible / 0.6m;
            for (int i = 0; i < 20; i++)
            {
                var gain = SimulerGainNet(brut);
                var diff = netAdditionnelCible - gain;
                if (Math.Abs(diff) < 1m) break;
                brut += diff / 0.6m;
                if (brut < 0) brut = netAdditionnelCible; // garde-fou
            }
            return Math.Round(brut, 0, MidpointRounding.AwayFromZero);
        }

        // ─────────────────────────────────────────────────────────────
        // ÉTAPE 1 - SOUMETTRE AU DAF (par RH)
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
        // ÉTAPE 2A - VALIDER (par DAF)
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
        // ÉTAPE 2B - REJETER (par DAF)
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

            // Retour à Brouillon - RH peut corriger et resoumettre
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
        // ÉTAPE 3 - INTÉGRER AU BULLETIN (par RH)
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
                    $"{g.Salarie.Matricule} - {g.Salarie.FullName}. " +
                    $"Créer le bulletin avant intégration.");

            // Vérifier qu'il n'y a pas déjà une ligne GRATIF (idempotence)
            // Note : si plusieurs gratifications dans le même mois, elles
            // créent chacune leur propre ligne (BulletinLigne pas restrictive
            // sur le code) - on les distingue via le montant et la
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
