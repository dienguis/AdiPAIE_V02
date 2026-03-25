using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Service de liaison Congé ↔ Paie.
    ///
    /// Pour les congés non payés ou à maintien partiel :
    ///   - Recherche le bulletin du mois du congé
    ///   - Crée une rubrique de retenue "Congé non payé" ou "Maintien partiel"
    ///   - La quantité = DureeJours de la CongeDemande
    ///   - Le taux = SalaireJournalier du salarié
    ///
    /// Rubrique attendue dans le référentiel :
    ///   Code = "CNP" (Congé Non Payé) — retenue complète (seul cas au Sénégal)
    /// </summary>
    public static class CongeImpactPaieService
    {
        private const string CODE_CNP = "CNP";

        /// <summary>
        /// Crée la rubrique de retenue dans le bulletin du mois correspondant.
        /// Ne fait rien si le bulletin n'existe pas encore (sera traité à la génération).
        /// </summary>
        public static bool CreerRetenue(IObjectSpace os, CongeDemande demande)
        {
            try
            {
                if (demande.Salarie == null || demande.Type == null) return false;
                if (demande.Type.ImpactSalaire == CongeImpactSalaire.Paye) return false;

                // Au Sénégal : uniquement Impayé (CNP) — pas de maintien partiel
                var codeRubrique = CODE_CNP;

                // Chercher la rubrique
                var rubrique = os.GetObjectsQuery<Rubrique>()
                    .FirstOrDefault(r => r.Code == codeRubrique && r.Actif);
                if (rubrique == null)
                {
                    Tracing.Tracer.LogWarning(
                        $"CongeImpactPaieService : rubrique {codeRubrique} introuvable.");
                    return false;
                }

                // Chercher le bulletin du mois
                var moisConge = demande.DateDebut.Month;
                var anneeConge = demande.DateDebut.Year;

                var bulletin = os.GetObjectsQuery<Bulletin>()
                    .FirstOrDefault(b =>
                        b.Salarie.Oid == demande.Salarie.Oid
                     && b.Mois == moisConge
                     && b.Annee == anneeConge);

                if (bulletin == null)
                {
                    // Bulletin pas encore généré — stocker pour traitement ultérieur
                    Tracing.Tracer.LogWarning(
                        $"CongeImpactPaieService : bulletin {moisConge}/{anneeConge} " +
                        $"de {demande.Salarie.FullName} introuvable. " +
                        "La retenue sera à saisir manuellement.");
                    return false;
                }

                // Calculer le salaire journalier
                var salaireJournalier = CalculerSalaireJournalier(demande.Salarie);

                // Retenue complète = salaire journalier × nombre de jours
                var montant = Math.Round(
                    demande.DureeJours * salaireJournalier, 0);

                if (montant <= 0) return false;

                // Vérifier si la ligne existe déjà (idempotent)
                var ligneExistante = bulletin.Lignes
                    .FirstOrDefault(l => l.Rubrique?.Code == codeRubrique
                        && l.Reference != null
                        && l.Reference.Contains(demande.Oid.ToString()));

                if (ligneExistante != null) return true; // Déjà créée

                // Créer la ligne de bulletin
                // Base = nb jours, Taux = salaire journalier, Montant = retenue (négatif)
                var ligne = os.CreateObject<BulletinLigne>();
                ligne.Bulletin = bulletin;
                ligne.Rubrique = rubrique;
                ligne.Base = demande.DureeJours;
                ligne.Taux = salaireJournalier;
                ligne.Montant = -montant;
                ligne.Reference = $"CNP {demande.DateDebut:dd/MM}->{demande.DateFin:dd/MM/yyyy} [ref:{demande.Oid}]";

                return true;
            }
            catch (Exception ex)
            {
                Tracing.Tracer.LogError(ex);
                return false;
            }
        }

        // ── Salaire journalier ────────────────────────────────
        private static decimal CalculerSalaireJournalier(Salarie salarie)
        {
            var base30 = salarie.Base30Jour > 0 ? salarie.Base30Jour : 30;
            return base30 > 0
                ? Math.Round(salarie.SalaireBase / base30, 2)
                : 0;
        }

        /// <summary>
        /// Supprime la retenue créée si le congé est annulé.
        /// </summary>
        public static void SupprimerRetenue(IObjectSpace os, CongeDemande demande)
        {
            try
            {
                var moisConge = demande.DateDebut.Month;
                var anneeConge = demande.DateDebut.Year;
                var refId = demande.Oid.ToString();

                var bulletin = os.GetObjectsQuery<Bulletin>()
                    .FirstOrDefault(b =>
                        b.Salarie.Oid == demande.Salarie.Oid
                     && b.Mois == moisConge
                     && b.Annee == anneeConge);

                if (bulletin == null) return;

                var lignes = bulletin.Lignes
                    .Where(l => l.Reference != null
                             && l.Reference.Contains(refId))
                    .ToList();

                foreach (var l in lignes)
                    os.Delete(l);
            }
            catch (Exception ex)
            {
                Tracing.Tracer.LogError(ex);
            }
        }
    }
}
