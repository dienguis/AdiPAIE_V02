using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using System;
using System.Collections.Generic;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Service de calcul des soldes de congés.
    ///
    /// Responsabilités :
    ///   1. AcquerirMensuel()    — crédite les jours acquis pour un mois donné
    ///   2. AcquerirTousSalaries() — batch mensuel pour tous les salariés actifs
    ///   3. Reporter()           — clôture d'exercice N → report vers N+1
    ///   4. VerifierSolde()      — contrôle avant accord d'un congé
    ///   5. ReserverJours()      — réserve lors de la soumission
    ///   6. DebiterJours()       — débite lors de l'accord
    ///   7. AnnulerDebite()      — recrédite lors de l'annulation
    ///   8. OuvrirNouvelExercice() — initialise les soldes pour une nouvelle année
    /// </summary>
    public static class SoldeCongeCalculService
    {
        // ════════════════════════════════════════════════════════
        // 1. ACQUISITION MENSUELLE — 1 salarié
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Crédite les jours acquis pour un salarié, un type de congé et un mois donné.
        /// Idempotent : si le mois a déjà été traité, ne fait rien.
        /// </summary>
        public static AcquisitionResult AcquerirMensuel(
            IObjectSpace os,
            Salarie salarie,
            CongeType typeConge,
            int annee,
            int mois)
        {
            if (!typeConge.AlimenteSolde || typeConge.AcquisitionMensuelle <= 0)
                return AcquisitionResult.NonApplicable(
                    $"Type {typeConge.Code} non soumis à acquisition mensuelle.");

            // Vérifier ancienneté minimale
            if (typeConge.AncienneteMinMois > 0)
            {
                var ancienneteActuelle = AncienneteHelper.TotalMois(
                    salarie.DateEmbauche, new DateTime(annee, mois, 1));
                if (ancienneteActuelle < typeConge.AncienneteMinMois)
                    return AcquisitionResult.NonApplicable(
                        $"Ancienneté insuffisante ({ancienneteActuelle} mois / " +
                        $"{typeConge.AncienneteMinMois} requis).");
            }

            var solde = GetOrCreateSolde(os, salarie, typeConge, annee);

            // Idempotence : déjà traité pour ce mois ?
            var dejaTraite = solde.Mouvements.Any(m =>
                m.TypeMouvement == MouvementSoldeType.AcquisitionMensuelle
                && m.MoisConcerne == mois
                && m.AnneeConcernee == annee);

            if (dejaTraite)
                return AcquisitionResult.Ignoré(
                    $"Mois {mois}/{annee} déjà acquis pour {salarie.FullName}.");

            var jours = typeConge.AcquisitionMensuelle;
            MouvementSolde.Creer(os, solde,
                MouvementSoldeType.AcquisitionMensuelle, jours,
                reference: $"ACQ-{annee}{mois:D2}",
                commentaire: $"Acquisition {mois:D2}/{annee}",
                mois: mois, annee: annee);

            solde.DateDerniereAcquisition = DateTime.Now;
            solde.MoisDerniereAcquisition = mois;

            return AcquisitionResult.Succès(salarie.FullName, jours, solde.SoldeDisponible);
        }

        // ════════════════════════════════════════════════════════
        // 2. BATCH MENSUEL — tous les salariés actifs
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Lance l'acquisition mensuelle pour tous les salariés actifs.
        /// À appeler en début de mois (ou via BackgroundService).
        /// Retourne un résumé : nb traités, nb ignorés, nb erreurs.
        /// </summary>
        public static BatchResult AcquerirTousSalaries(
            IObjectSpace os, int annee, int mois)
        {
            var result = new BatchResult { Annee = annee, Mois = mois };

            var salaries = os.GetObjectsQuery<Salarie>()
                .Where(s => s.IsActif)
                .ToList();

            var typesActifs = os.GetObjectsQuery<CongeType>()
                .Where(t => t.AlimenteSolde && t.AcquisitionMensuelle > 0)
                .ToList();

            foreach (var salarie in salaries)
            {
                foreach (var type in typesActifs)
                {
                    try
                    {
                        var r = AcquerirMensuel(os, salarie, type, annee, mois);
                        if (r.Succes) result.NbTraites++;
                        else result.NbIgnores++;
                    }
                    catch (Exception ex)
                    {
                        result.Erreurs.Add(
                            $"{salarie.FullName} / {type.Code} : {ex.Message}");
                    }
                }
            }

            os.CommitChanges();
            return result;
        }

        // ════════════════════════════════════════════════════════
        // 3. REPORT DE FIN D'EXERCICE
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Reporte les soldes de l'année N vers N+1 dans la limite du plafond.
        /// À appeler en fin d'année (ou début janvier).
        /// </summary>
        public static BatchResult Reporter(IObjectSpace os, int anneeSource)
        {
            var result = new BatchResult
            { Annee = anneeSource + 1, Mois = 0 };

            var soldesSource = os.GetObjectsQuery<SoldeConge>()
                .Where(s => s.Annee == anneeSource
                         && s.Statut == SoldeCongeStatut.Actif)
                .ToList();

            foreach (var solde in soldesSource)
            {
                try
                {
                    var plafond = solde.TypeConge?.PlafondReportJours ?? 0;
                    var aReporter = Math.Min(solde.SoldeDisponible, plafond);

                    if (aReporter <= 0)
                    {
                        result.NbIgnores++;
                        continue;
                    }

                    // Clôturer l'ancien solde
                    solde.Statut = SoldeCongeStatut.Archive;

                    // Créer/ouvrir solde N+1
                    var soldeNp1 = GetOrCreateSolde(
                        os, solde.Salarie, solde.TypeConge, anneeSource + 1);

                    MouvementSolde.Creer(os, soldeNp1,
                        MouvementSoldeType.Report, aReporter,
                        reference: $"REPORT-{anneeSource}->{anneeSource + 1}",
                        commentaire: $"Report exercice {anneeSource} " +
                                     $"(plafond : {plafond}j, solde : {solde.SoldeDisponible:N1}j)",
                        annee: anneeSource + 1);

                    result.NbTraites++;
                }
                catch (Exception ex)
                {
                    result.Erreurs.Add(
                        $"{solde.Salarie?.FullName} / {solde.TypeConge?.Code} : {ex.Message}");
                }
            }

            os.CommitChanges();
            return result;
        }

        // ════════════════════════════════════════════════════════
        // 4. VÉRIFICATION SOLDE AVANT ACCORD
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Vérifie si le salarié dispose des jours suffisants pour la demande.
        /// Retourne null si OK, sinon le message d'erreur.
        /// </summary>
        public static string VerifierSolde(
            IObjectSpace os,
            Salarie salarie,
            CongeType typeConge,
            decimal joursdemandes,
            int annee)
        {
            // Congés non soumis à solde (maladie, maternité)
            if (!typeConge.AlimenteSolde)
                return null;

            var solde = os.GetObjectsQuery<SoldeConge>()
                .FirstOrDefault(s => s.Salarie.Oid == salarie.Oid
                                  && s.TypeConge.Oid == typeConge.Oid
                                  && s.Annee == annee
                                  && s.Statut == SoldeCongeStatut.Actif);

            if (solde == null)
                return $"Aucun solde ouvert pour {typeConge.Libelle} en {annee}. " +
                       "Veuillez initialiser le solde du salarié.";

            if (solde.SoldeReel < joursdemandes)
                return $"Solde insuffisant pour {typeConge.Libelle}. " +
                       $"Disponible : {solde.SoldeReel:N1}j — Demandé : {joursdemandes:N1}j.";

            return null; // OK
        }

        // ════════════════════════════════════════════════════════
        // 5. RÉSERVATION (à la soumission)
        // ════════════════════════════════════════════════════════

        public static void ReserverJours(
            IObjectSpace os, Salarie salarie,
            CongeType typeConge, decimal jours, int annee)
        {
            if (!typeConge.AlimenteSolde) return;
            var solde = GetOrCreateSolde(os, salarie, typeConge, annee);
            solde.Reserver(jours);
        }

        public static void LibererReservation(
            IObjectSpace os, Salarie salarie,
            CongeType typeConge, decimal jours, int annee)
        {
            if (!typeConge.AlimenteSolde) return;
            var solde = os.GetObjectsQuery<SoldeConge>()
                .FirstOrDefault(s => s.Salarie.Oid == salarie.Oid
                                  && s.TypeConge.Oid == typeConge.Oid
                                  && s.Annee == annee);
            solde?.LibererReservation(jours);
        }

        // ════════════════════════════════════════════════════════
        // 6. DÉBIT À L'ACCORD
        // ════════════════════════════════════════════════════════

        public static void DebiterJours(
            IObjectSpace os, CongeDemande demande)
        {
            if (demande.Type == null || !demande.Type.AlimenteSolde) return;
            var annee = demande.DateDebut.Year;
            var solde = GetOrCreateSolde(
                os, demande.Salarie, demande.Type, annee);

            MouvementSolde.Creer(os, solde,
                MouvementSoldeType.PriseCongé,
                demande.DureeJours,
                reference: $"CONGE-{demande.Oid}",
                commentaire: $"Congé accordé : {demande.DateDebut:dd/MM} " +
                             $"→ {demande.DateFin:dd/MM/yyyy}");
        }

        // ════════════════════════════════════════════════════════
        // 7. ANNULATION → RECRÉDITEMENT
        // ════════════════════════════════════════════════════════

        public static void AnnulerDebit(
            IObjectSpace os, CongeDemande demande)
        {
            if (demande.Type == null || !demande.Type.AlimenteSolde) return;
            var annee = demande.DateDebut.Year;
            var solde = os.GetObjectsQuery<SoldeConge>()
                .FirstOrDefault(s => s.Salarie.Oid == demande.Salarie.Oid
                                  && s.TypeConge.Oid == demande.Type.Oid
                                  && s.Annee == annee);
            if (solde == null) return;

            MouvementSolde.Creer(os, solde,
                MouvementSoldeType.AnnulationCongé,
                demande.DureeJours,
                reference: $"ANNUL-CONGE-{demande.Oid}",
                commentaire: $"Annulation congé {demande.DateDebut:dd/MM} " +
                             $"→ {demande.DateFin:dd/MM/yyyy}");
        }

        // ════════════════════════════════════════════════════════
        // 8. OUVERTURE NOUVEL EXERCICE
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Initialise un solde à zéro pour un salarié + type + année.
        /// Utile pour les nouvelles recrues ou un nouvel exercice.
        /// </summary>
        public static SoldeConge OuvrirSolde(
            IObjectSpace os, Salarie salarie,
            CongeType typeConge, int annee,
            decimal soldeInitial = 0)
        {
            var solde = GetOrCreateSolde(os, salarie, typeConge, annee);
            if (soldeInitial != 0)
            {
                MouvementSolde.Creer(os, solde,
                    MouvementSoldeType.Initialisation, soldeInitial,
                    commentaire: $"Initialisation solde {annee}");
            }
            return solde;
        }

        // ════════════════════════════════════════════════════════
        // HELPER — GetOrCreate
        // ════════════════════════════════════════════════════════

        public static SoldeConge GetOrCreateSolde(
            IObjectSpace os, Salarie salarie,
            CongeType typeConge, int annee)
        {
            var solde = os.GetObjectsQuery<SoldeConge>()
                .FirstOrDefault(s => s.Salarie.Oid == salarie.Oid
                                  && s.TypeConge.Oid == typeConge.Oid
                                  && s.Annee == annee
                                  && s.Statut == SoldeCongeStatut.Actif);

            if (solde != null) return solde;

            solde = os.CreateObject<SoldeConge>();
            solde.Salarie = salarie;
            solde.TypeConge = typeConge;
            solde.Annee = annee;
            return solde;
        }
    }

    // ════════════════════════════════════════════════════════════
    // RÉSULTATS
    // ════════════════════════════════════════════════════════════

    public class AcquisitionResult
    {
        public bool Succes { get; private set; }
        public string Message { get; private set; }
        public decimal JoursCredites { get; private set; }
        public decimal NouveauSolde { get; private set; }

        public static AcquisitionResult Succès(string nom, decimal jours, decimal solde)
            => new AcquisitionResult
            {
                Succes = true,
                Message = $"{nom} : +{jours:N2}j → Solde {solde:N2}j",
                JoursCredites = jours,
                NouveauSolde = solde
            };

        public static AcquisitionResult Ignoré(string raison)
            => new AcquisitionResult { Succes = false, Message = raison };

        public static AcquisitionResult NonApplicable(string raison)
            => new AcquisitionResult { Succes = false, Message = raison };
    }

    public class BatchResult
    {
        public int Annee { get; set; }
        public int Mois { get; set; }
        public int NbTraites { get; set; }
        public int NbIgnores { get; set; }
        public List<string> Erreurs { get; } = new List<string>();
        public bool HasErrors => Erreurs.Count > 0;
        public override string ToString() =>
            $"Traités : {NbTraites} | Ignorés : {NbIgnores}" +
            (HasErrors ? $" | ⚠ {Erreurs.Count} erreur(s)" : "");
    }
}
