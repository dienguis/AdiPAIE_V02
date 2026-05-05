// =============================================================================
//  BulletinInterimImportService.cs — V1.3 Sprint 1 (mai 2026)
//
//  Parse le fichier xlsx "Livre de paie intérim" envoyé par les sociétés
//  d'intérim, et le persiste dans BulletinInterim + ImportBulletinInterimBatch.
//
//  Stratégie de mapping :
//    - L'entête du fichier source est sur la ligne 10 (visuellement) mais
//      peut varier. On cherche dynamiquement la ligne contenant "matricule"
//      ou "n° matricule" (case-insensitive).
//    - On mappe par TITRE de colonne (case-insensitive), pas par position.
//    - Les colonnes critiques (Débours, Commission, HT, TVA, TTC) doivent
//      toutes être présentes pour valider le preview.
//
//  Lookup intérimaire :
//    - Matricule lu sur la ligne courante
//    - Si matricule == "PRESTATAIRE" (ou variante) → status=Prestataire,
//      Interimaire reste null
//    - Sinon, lookup case-insensitive sur Interimaire.Matricule
//    - Si pas trouvé → status=ACreer ; au commit, on créera la fiche auto
//
//  Idempotence : Au commit, si un Batch existe déjà pour (Année, Mois,
//  Société), on supprime ses bulletins et le batch lui-même AVANT de
//  recréer le nouveau (cascade via XPCollection).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects.Interim;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Models.Interim;
using ClosedXML.Excel;
using DevExpress.ExpressApp;

namespace AdiPAIE_V02.Module.Services.Interim
{
    /// <inheritdoc cref="IBulletinInterimImportService"/>
    public sealed class BulletinInterimImportService : IBulletinInterimImportService
    {
        // ─────────────────────────────────────────────────────────────────────
        //  PREVIEW
        // ─────────────────────────────────────────────────────────────────────
        public ImportPreviewDto Preview(
            byte[] fichierXlsxBytes,
            string fichierNom,
            int annee,
            int mois,
            Guid societeInterimOid,
            IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(fichierXlsxBytes);
            ArgumentNullException.ThrowIfNull(os);

            var preview = new ImportPreviewDto
            {
                Annee = annee,
                Mois = mois,
                SocieteOid = societeInterimOid,
                FichierSource = fichierNom ?? ""
            };

            // ── Validation période + société ──────────────────────────────
            if (annee < 2000 || annee > 2100) { preview.Erreurs.Add($"Année invalide : {annee}."); return preview; }
            if (mois < 1 || mois > 12) { preview.Erreurs.Add($"Mois invalide : {mois}."); return preview; }

            var societe = os.GetObjectByKey<SocieteInterim>(societeInterimOid);
            if (societe == null) { preview.Erreurs.Add("Société d'intérim introuvable."); return preview; }
            preview.SocieteNom = societe.RaisonSociale ?? "";

            // ── Parse Excel ───────────────────────────────────────────────
            XLWorkbook wb;
            try
            {
                using var stream = new MemoryStream(fichierXlsxBytes);
                wb = new XLWorkbook(stream);
            }
            catch (Exception ex)
            {
                preview.Erreurs.Add($"Fichier illisible : {ex.Message}");
                return preview;
            }

            using (wb)
            {
                var ws = wb.Worksheets.FirstOrDefault();
                if (ws == null) { preview.Erreurs.Add("Aucune feuille de calcul dans le fichier."); return preview; }

                // ── Localisation de la ligne d'entête ────────────────────
                int? headerRow = LocateHeaderRow(ws);
                if (headerRow == null)
                {
                    preview.Erreurs.Add("Impossible de localiser la ligne d'entête (recherche de 'matricule').");
                    return preview;
                }

                // ── Mapping titre → numéro de colonne ────────────────────
                var headers = BuildHeaderMap(ws, headerRow.Value);

                // ── Vérification colonnes critiques ──────────────────────
                var colonnesObligatoires = new[] { "matricule", "noms", "prenoms", "debours", "commission", "tva", "ttc" };
                foreach (var c in colonnesObligatoires)
                {
                    if (!FindColumn(headers, c).HasValue)
                    {
                        preview.Erreurs.Add($"Colonne obligatoire absente : '{c}' (recherche fuzzy).");
                    }
                }
                if (preview.Erreurs.Count > 0) return preview;

                // ── Idempotence : batch existant ? ───────────────────────
                preview.BatchDejaExistant = os.GetObjectsQuery<ImportBulletinInterimBatch>()
                    .ToList()
                    .Any(b => b.Annee == annee && b.Mois == mois
                           && b.Societe?.Oid == societeInterimOid);
                if (preview.BatchDejaExistant)
                {
                    preview.Warnings.Add(
                        $"Un batch existe déjà pour {mois:D2}/{annee} de '{societe.RaisonSociale}'. " +
                        "Au commit, l'ancien sera remplacé.");
                }

                // ── Précharger tous les intérimaires actifs (lookup matricule) ─
                var interimsByMatricule = os.GetObjectsQuery<Interimaire>()
                    .ToList()
                    .Where(i => !string.IsNullOrEmpty(i.Matricule))
                    .GroupBy(i => i.Matricule!.Trim().ToUpperInvariant())
                    .ToDictionary(g => g.Key, g => g.First());

                // ── Itération sur les lignes de données ──────────────────
                int dataStart = headerRow.Value + 1;
                int lastRow = ws.LastRowUsed()?.RowNumber() ?? dataStart;

                int colMatricule = FindColumn(headers, "matricule")!.Value;
                int colNoms      = FindColumn(headers, "noms")!.Value;
                int colPrenoms   = FindColumn(headers, "prenoms")!.Value;
                int colSexe      = FindColumn(headers, "sexe") ?? -1;
                int colFonction  = FindColumn(headers, "fonction") ?? -1;
                int colSite      = FindColumn(headers, "site") ?? -1;
                int colTrente    = FindColumn(headers, "30eme", "30ème", "30 eme") ?? -1;
                int colSalBase   = FindColumn(headers, "salaire de base") ?? -1;
                int colBrut      = FindColumn(headers, "brut imposable") ?? -1;
                int colIpresSal  = FindColumn(headers, "ipres gen sal", "ipres sal") ?? -1;
                int colIpresPat  = FindColumn(headers, "ipres gen pat", "ipres pat") ?? -1;
                int colCssAll    = FindColumn(headers, "css all") ?? -1;
                int colCssAcc    = FindColumn(headers, "css acc") ?? -1;
                int colIpmSal    = FindColumn(headers, "ipm sal") ?? -1;
                int colIpmPat    = FindColumn(headers, "ipm pat") ?? -1;
                int colCfce      = FindColumn(headers, "cfce") ?? -1;
                int colIR        = FindColumn(headers, "retenue ir") ?? -1;
                int colTRIMF     = FindColumn(headers, "retenue trimf", "trimf") ?? -1;
                int colTransport = FindColumn(headers, "prime de transport", "transport") ?? -1;
                int colPanier    = FindColumn(headers, "prime de panier", "panier") ?? -1;
                int colIndemDiv  = FindColumn(headers, "indemnites diverses", "indemnités diverses") ?? -1;
                int colNet       = FindColumn(headers, "net a payer", "net à payer") ?? -1;
                int colDebours   = FindColumn(headers, "debours", "débours")!.Value;
                int colCommAg    = FindColumn(headers, "commission agence", "commissions agence")!.Value;
                int colHT        = FindColumn(headers, "montant ht", "ht") ?? -1;
                int colTVA       = FindColumn(headers, "tva")!.Value;
                int colTTC       = FindColumn(headers, "ttc")!.Value;

                if (colHT < 0)
                {
                    // Fallback : chercher un header exact "ht"
                    colHT = FindColumn(headers, "ht") ?? -1;
                }

                for (int r = dataStart; r <= lastRow; r++)
                {
                    string matriculeStr = SafeStr(ws.Cell(r, colMatricule));
                    string nom = SafeStr(ws.Cell(r, colNoms));
                    string prenom = SafeStr(ws.Cell(r, colPrenoms));

                    // Skip lignes vides / total
                    if (string.IsNullOrWhiteSpace(matriculeStr) && string.IsNullOrWhiteSpace(nom)) continue;
                    if (string.IsNullOrWhiteSpace(matriculeStr)) continue;
                    if (matriculeStr.Equals("total", StringComparison.OrdinalIgnoreCase)) continue;

                    var ligne = new LignePreviewDto
                    {
                        LigneFichier = r,
                        MatriculeOriginal = matriculeStr.Trim(),
                        Nom = nom.Trim().ToUpperInvariant(),
                        Prenom = prenom.Trim(),
                        Sexe = colSexe > 0 ? SafeStr(ws.Cell(r, colSexe)).Trim() : "",
                        Fonction = colFonction > 0 ? SafeStr(ws.Cell(r, colFonction)).Trim() : "",
                        SiteAffectation = colSite > 0 ? SafeStr(ws.Cell(r, colSite)).Trim() : "",

                        Trentieme = colTrente > 0 ? SafeDecimal(ws.Cell(r, colTrente)) : 0m,
                        SalaireBase = colSalBase > 0 ? SafeDecimal(ws.Cell(r, colSalBase)) : 0m,
                        BrutImposable = colBrut > 0 ? SafeDecimal(ws.Cell(r, colBrut)) : 0m,

                        IpresSal = colIpresSal > 0 ? SafeDecimal(ws.Cell(r, colIpresSal)) : 0m,
                        IpresPat = colIpresPat > 0 ? SafeDecimal(ws.Cell(r, colIpresPat)) : 0m,
                        CssAll = colCssAll > 0 ? SafeDecimal(ws.Cell(r, colCssAll)) : 0m,
                        CssAcc = colCssAcc > 0 ? SafeDecimal(ws.Cell(r, colCssAcc)) : 0m,
                        IpmSal = colIpmSal > 0 ? SafeDecimal(ws.Cell(r, colIpmSal)) : 0m,
                        IpmPat = colIpmPat > 0 ? SafeDecimal(ws.Cell(r, colIpmPat)) : 0m,

                        CFCE = colCfce > 0 ? SafeDecimal(ws.Cell(r, colCfce)) : 0m,
                        RetenueIR = colIR > 0 ? SafeDecimal(ws.Cell(r, colIR)) : 0m,
                        RetenueTRIMF = colTRIMF > 0 ? SafeDecimal(ws.Cell(r, colTRIMF)) : 0m,

                        PrimeTransport = colTransport > 0 ? SafeDecimal(ws.Cell(r, colTransport)) : 0m,
                        PrimePanier = colPanier > 0 ? SafeDecimal(ws.Cell(r, colPanier)) : 0m,
                        IndemnitesDiverses = colIndemDiv > 0 ? SafeDecimal(ws.Cell(r, colIndemDiv)) : 0m,

                        NetAPayer = colNet > 0 ? SafeDecimal(ws.Cell(r, colNet)) : 0m,
                        Debours = SafeDecimal(ws.Cell(r, colDebours)),
                        CommissionAgence = SafeDecimal(ws.Cell(r, colCommAg)),
                        MontantHT = colHT > 0 ? SafeDecimal(ws.Cell(r, colHT)) : 0m,
                        TVA = SafeDecimal(ws.Cell(r, colTVA)),
                        TTC = SafeDecimal(ws.Cell(r, colTTC))
                    };

                    // ── Détermination du statut ──
                    if (IsPrestataireMatricule(ligne.MatriculeOriginal))
                    {
                        ligne.Statut = LignePreviewStatut.Prestataire;
                    }
                    else
                    {
                        var matricKey = ligne.MatriculeOriginal.ToUpperInvariant();
                        if (interimsByMatricule.TryGetValue(matricKey, out var existant))
                        {
                            ligne.Statut = LignePreviewStatut.OK;
                            ligne.InterimaireExistantOid = existant.Oid;
                        }
                        else
                        {
                            ligne.Statut = LignePreviewStatut.ACreer;
                        }
                    }

                    preview.Lignes.Add(ligne);
                }

                // ── Compteurs synthèse ────────────────────────────────────
                preview.NbLignesOk = preview.Lignes.Count(l => l.Statut == LignePreviewStatut.OK);
                preview.NbLignesACreer = preview.Lignes.Count(l => l.Statut == LignePreviewStatut.ACreer);
                preview.NbLignesPrestataire = preview.Lignes.Count(l => l.Statut == LignePreviewStatut.Prestataire);
                preview.NbLignesErreur = preview.Lignes.Count(l => l.Statut == LignePreviewStatut.Erreur);

                preview.TotalDebours = preview.Lignes.Sum(l => l.Debours);
                preview.TotalCommissionAgence = preview.Lignes.Sum(l => l.CommissionAgence);
                preview.TotalHT = preview.Lignes.Sum(l => l.MontantHT);
                preview.TotalTVA = preview.Lignes.Sum(l => l.TVA);
                preview.TotalTTC = preview.Lignes.Sum(l => l.TTC);
                preview.TotalBrutImposable = preview.Lignes.Sum(l => l.BrutImposable);
            }

            return preview;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  COMMIT
        // ─────────────────────────────────────────────────────────────────────
        public ImportResultDto Commit(
            ImportPreviewDto preview,
            string importeParUserName,
            bool ecraserSiBatchExistant,
            IObjectSpace os)
        {
            ArgumentNullException.ThrowIfNull(preview);
            ArgumentNullException.ThrowIfNull(os);

            var result = new ImportResultDto();

            if (!preview.PeutCommiter)
                throw new InvalidOperationException("Le preview contient des erreurs et ne peut pas être commit.");

            var societe = os.GetObjectByKey<SocieteInterim>(preview.SocieteOid);
            if (societe == null)
                throw new InvalidOperationException("Société d'intérim introuvable au commit.");

            // ── Si batch existant : suppression ou refus ─────────────────
            var existingBatch = os.GetObjectsQuery<ImportBulletinInterimBatch>()
                .ToList()
                .FirstOrDefault(b => b.Annee == preview.Annee && b.Mois == preview.Mois
                                   && b.Societe?.Oid == preview.SocieteOid);
            if (existingBatch != null)
            {
                if (!ecraserSiBatchExistant)
                {
                    throw new InvalidOperationException(
                        $"Un batch existe déjà pour {preview.Mois:D2}/{preview.Annee} de '{societe.RaisonSociale}'. " +
                        "Cocher 'Écraser' pour remplacer.");
                }

                // ★ Stratégie défensive : supprimer TOUS les batches matching
                //   la clé (Année, Mois, Société), pas seulement le premier.
                //   Ça couvre le cas (rare) de doublons accumulés par anciens
                //   tests avant la mise en place de l'index unique.
                var allMatchingBatches = os.GetObjectsQuery<ImportBulletinInterimBatch>()
                    .ToList()
                    .Where(b => b.Annee == preview.Annee && b.Mois == preview.Mois
                              && b.Societe?.Oid == preview.SocieteOid)
                    .ToList();

                int nbAnciens = allMatchingBatches.Sum(b => b.Bulletins?.Count ?? 0);

                foreach (var oldBatch in allMatchingBatches)
                {
                    if (oldBatch.Bulletins != null)
                    {
                        foreach (var anc in oldBatch.Bulletins.ToList())
                            os.Delete(anc);
                    }
                    os.Delete(oldBatch);
                }

                // ★ FLUSH immédiat de la suppression en BDD AVANT d'insérer le nouveau
                //   batch. Sans ce commit intermédiaire, XPO accumule DELETE + INSERT
                //   dans la même unité de travail et l'index unique
                //   UX_Batch_Annee_Mois_Societe voit les deux lignes simultanément
                //   → violation de contrainte unique au commit final.
                try
                {
                    os.CommitChanges();
                    // Force le purge des objets supprimés en cache XPO
                    // Purge XPO des objets supprimés (cast vers XPObjectSpace pour accès Session)
                    try
                    {
                        if (os is DevExpress.ExpressApp.Xpo.XPObjectSpace xpoOs)
                            xpoOs.Session.PurgeDeletedObjects();
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Impossible de supprimer l'ancien batch avant le ré-import : "
                        + (ex.GetBaseException()?.Message ?? ex.Message), ex);
                }

                // ★ Vérification post-suppression : confirmer qu'aucun batch
                //   matching la clé ne reste en base. Si c'est le cas, message
                //   d'erreur précis (rare mais bon pour le débogage).
                var stillThere = os.GetObjectsQuery<ImportBulletinInterimBatch>()
                    .ToList()
                    .Where(b => b.Annee == preview.Annee && b.Mois == preview.Mois
                              && b.Societe?.Oid == preview.SocieteOid)
                    .ToList();

                if (stillThere.Count > 0)
                {
                    // Tentative de seconde suppression (ceinture + bretelles)
                    foreach (var dup in stillThere)
                    {
                        if (dup.Bulletins != null)
                            foreach (var b in dup.Bulletins.ToList()) os.Delete(b);
                        os.Delete(dup);
                    }
                    try
                    {
                        os.CommitChanges();
                        // Purge XPO des objets supprimés (cast vers XPObjectSpace pour accès Session)
                    try
                    {
                        if (os is DevExpress.ExpressApp.Xpo.XPObjectSpace xpoOs)
                            xpoOs.Session.PurgeDeletedObjects();
                    }
                    catch { }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Impossible de supprimer {stillThere.Count} batch(s) résiduels en base. " +
                            "Supprimer manuellement via Intérimaires → Lots d'import. " +
                            "Détail : " + (ex.GetBaseException()?.Message ?? ex.Message), ex);
                    }
                }

                result.BatchEcrase = true;
                result.NbBulletinsAnciensSupprimes = nbAnciens;
            }

            // ── Création du batch ─────────────────────────────────────────
            var batch = os.CreateObject<ImportBulletinInterimBatch>();
            batch.Annee = preview.Annee;
            batch.Mois = preview.Mois;
            batch.Societe = societe;
            batch.DateImport = DateTime.Now;
            batch.FichierSource = preview.FichierSource;
            batch.ImportePar = importeParUserName ?? "";
            batch.NbLignesImportees = preview.NbLignesTotal;
            batch.NbFichesCreees = preview.NbLignesACreer;
            batch.NbPrestataires = preview.NbLignesPrestataire;
            batch.TotalTTC = preview.TotalTTC;
            batch.TotalDebours = preview.TotalDebours;
            batch.TotalCommissionAgence = preview.TotalCommissionAgence;
            batch.TotalTVA = preview.TotalTVA;

            // ── Création / lookup des intérimaires + bulletins ───────────
            // Re-précharger pour bénéficier des fiches nouvellement créées dans la même transaction
            var interimsByMatricule = os.GetObjectsQuery<Interimaire>()
                .ToList()
                .Where(i => !string.IsNullOrEmpty(i.Matricule))
                .GroupBy(i => i.Matricule!.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var ligne in preview.Lignes)
            {
                Interimaire? interim = null;

                if (ligne.Statut == LignePreviewStatut.OK && ligne.InterimaireExistantOid.HasValue)
                {
                    interim = os.GetObjectByKey<Interimaire>(ligne.InterimaireExistantOid);
                }
                else if (ligne.Statut == LignePreviewStatut.ACreer)
                {
                    var matricKey = ligne.MatriculeOriginal.ToUpperInvariant();
                    if (interimsByMatricule.TryGetValue(matricKey, out var dejaCree))
                    {
                        interim = dejaCree;
                    }
                    else
                    {
                        // Création auto de la fiche intérimaire avec le minimum requis.
                        // ⚠️ Interimaire n'a pas de propriété Sexe (uniquement Salarie l'a).
                        // Le RH devra compléter manuellement les champs optionnels (CNI,
                        // date naissance, adresse, etc.) après l'import.
                        interim = os.CreateObject<Interimaire>();
                        interim.Matricule = ligne.MatriculeOriginal;
                        interim.Nom = ligne.Nom;
                        interim.Prenom = ligne.Prenom;
                        interim.SocieteInterim = societe;
                        if (!string.IsNullOrWhiteSpace(ligne.Fonction))
                            interim.Fonction = ligne.Fonction;

                        interimsByMatricule[matricKey] = interim;
                        result.NbInterimairesCreesAuto++;
                        result.InterimairesCreesNoms.Add($"{ligne.MatriculeOriginal} — {ligne.Nom} {ligne.Prenom}");
                    }
                }
                // Statut Prestataire → interim reste null

                var blt = os.CreateObject<BulletinInterim>();
                blt.Annee = preview.Annee;
                blt.Mois = preview.Mois;
                blt.SocieteEmettrice = societe;
                blt.Interimaire = interim;
                blt.MatriculeOriginal = ligne.MatriculeOriginal;
                blt.NomComplet = $"{ligne.Nom} {ligne.Prenom}".Trim();
                blt.Fonction = ligne.Fonction;
                blt.SiteAffectation = ligne.SiteAffectation;
                blt.Trentieme = ligne.Trentieme;
                blt.SalaireBase = ligne.SalaireBase;
                blt.BrutImposable = ligne.BrutImposable;
                blt.IpresSal = ligne.IpresSal;
                blt.IpresPat = ligne.IpresPat;
                blt.CssAll = ligne.CssAll;
                blt.CssAcc = ligne.CssAcc;
                blt.IpmSal = ligne.IpmSal;
                blt.IpmPat = ligne.IpmPat;
                blt.CFCE = ligne.CFCE;
                blt.RetenueIR = ligne.RetenueIR;
                blt.RetenueTRIMF = ligne.RetenueTRIMF;
                blt.PrimeTransport = ligne.PrimeTransport;
                blt.PrimePanier = ligne.PrimePanier;
                blt.IndemnitesDiverses = ligne.IndemnitesDiverses;
                blt.NetAPayer = ligne.NetAPayer;
                blt.Debours = ligne.Debours;
                blt.CommissionAgence = ligne.CommissionAgence;
                blt.MontantHT = ligne.MontantHT;
                blt.TVA = ligne.TVA;
                blt.TTC = ligne.TTC;
                blt.DateImport = DateTime.Now;
                blt.FichierSource = preview.FichierSource;
                blt.ImportePar = importeParUserName ?? "";
                blt.Batch = batch;
                blt.Statut = ligne.Statut switch
                {
                    LignePreviewStatut.OK => BulletinInterimStatut.ImporteOk,
                    LignePreviewStatut.ACreer => BulletinInterimStatut.FicheCreeeAuto,
                    LignePreviewStatut.Prestataire => BulletinInterimStatut.Prestataire,
                    _ => BulletinInterimStatut.Erreur
                };

                result.NbBulletinsCrees++;
                if (ligne.Statut == LignePreviewStatut.Prestataire) result.NbPrestataires++;
            }

            // ── Commit transactionnel avec gestion d'erreur lisible ──
            try
            {
                os.CommitChanges();
            }
            catch (Exception ex)
            {
                // Capture des erreurs SQL/XPO les plus fréquentes pour les
                // traduire en messages métier compréhensibles par l'utilisateur.
                var typeName = ex.GetType().Name;
                var msg = ex.Message ?? "";
                var fullMsg = (ex.InnerException?.Message ?? "") + " " + msg;

                // Erreur 2601/2627 = violation index unique côté SQL Server
                if (fullMsg.Contains("2601") || fullMsg.Contains("2627")
                    || typeName.Contains("ConstraintViolation")
                    || fullMsg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                    || fullMsg.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                {
                    string detail = "";
                    if (fullMsg.Contains("UX_Batch_Annee_Mois_Societe", StringComparison.OrdinalIgnoreCase))
                        detail = " Un lot d'import existe déjà pour cette période et cette société. " +
                                 "Cocher 'Écraser le batch existant' à l'étape Preview ou supprimer manuellement le lot précédent.";
                    else if (fullMsg.Contains("Matricule", StringComparison.OrdinalIgnoreCase))
                        detail = " Un intérimaire avec ce matricule existe déjà mais n'a pas été détecté à l'étape Preview. " +
                                 "Vérifier les fiches Intérimaires et relancer.";
                    else
                        detail = " Cause probable : un enregistrement avec une clé unique (matricule, batch, société) " +
                                 "existe déjà en base.";

                    throw new InvalidOperationException(
                        "Conflit de contrainte unique lors de l'enregistrement." + detail, ex);
                }

                // Erreur 547 = violation FK (référence vers entité inexistante)
                if (fullMsg.Contains("547") || fullMsg.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Référence manquante : un objet lié (Société, Intérimaire) a été supprimé entre " +
                        "la preview et le commit. Recommencer l'import.", ex);
                }

                // Erreur générique : on remonte un message court (pas la stack)
                throw new InvalidOperationException(
                    $"Erreur lors de l'enregistrement en base : {msg}", ex);
            }

            result.BatchOid = batch.Oid;
            result.TotalTTC = batch.TotalTTC;
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────────────
        private static int? LocateHeaderRow(IXLWorksheet ws)
        {
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;

            for (int r = 1; r <= Math.Min(lastRow, 30); r++)
            {
                for (int c = 1; c <= Math.Min(lastCol, 60); c++)
                {
                    var v = ws.Cell(r, c).Value.ToString();
                    if (!string.IsNullOrWhiteSpace(v) &&
                        (v.IndexOf("matricule", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return r;
                    }
                }
            }
            return null;
        }

        private static Dictionary<int, string> BuildHeaderMap(IXLWorksheet ws, int headerRow)
        {
            var dict = new Dictionary<int, string>();
            int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
            for (int c = 1; c <= lastCol; c++)
            {
                var v = ws.Cell(headerRow, c).Value.ToString();
                if (!string.IsNullOrWhiteSpace(v))
                    dict[c] = v.Trim();
            }
            return dict;
        }

        private static int? FindColumn(Dictionary<int, string> headers, params string[] keywords)
        {
            foreach (var kv in headers)
            {
                // Normalisation : minuscules + suppression des accents pour matching tolérant
                // ("Prénoms" ≡ "prenoms", "débours" ≡ "debours", "Indemnités" ≡ "indemnites").
                var t = RemoveAccents(kv.Value.ToLowerInvariant());
                foreach (var kw in keywords)
                {
                    if (t.Contains(RemoveAccents(kw.ToLowerInvariant()))) return kv.Key;
                }
            }
            return null;
        }

        /// <summary>
        /// Retire les accents d'une chaîne en utilisant la normalisation Unicode FormD
        /// puis en filtrant les caractères de catégorie NonSpacingMark.
        /// </summary>
        private static string RemoveAccents(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        private static bool IsPrestataireMatricule(string matricule)
        {
            if (string.IsNullOrWhiteSpace(matricule)) return false;
            var m = matricule.Trim().ToUpperInvariant();
            return m == "PRESTATAIRE" || m == "PREST" || m.StartsWith("PRESTATAIRE");
        }

        /// <summary>
        /// Lit le contenu textuel d'une cellule ClosedXML en gérant tous les types
        /// (texte, nombre, date, blank, formule, erreur).
        /// </summary>
        private static string SafeStr(IXLCell? cell)
        {
            if (cell == null) return "";
            try
            {
                // Si la cellule a une formule, on essaie d'utiliser CachedValue
                // (la dernière valeur calculée sauvegardée par Excel).
                var val = cell.HasFormula ? cell.CachedValue : cell.Value;
                if (val.IsBlank) return "";
                if (val.IsError) return "";
                if (val.IsText) return val.GetText() ?? "";
                if (val.IsNumber) return val.GetNumber().ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (val.IsDateTime) return val.GetDateTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                if (val.IsBoolean) return val.GetBoolean().ToString();
                return val.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Lit la valeur numérique d'une cellule ClosedXML SANS passer par ToString()
        /// (qui interprétait "252379,45" comme 25 237 945 à cause de la virgule prise
        /// comme séparateur de milliers en InvariantCulture). Utilise directement
        /// XLCellValue.GetNumber() qui retourne un double natif.
        ///
        /// Gère aussi les formules : si la cellule a une formule (=SUM, =A1*5%, etc.),
        /// on lit la valeur cachée (CachedValue) calculée par Excel à la dernière sauvegarde.
        /// Si la formule est invalide ou non calculée, retourne 0.
        ///
        /// Robuste aux cellules formatées en entier visuellement mais stockées en
        /// décimal (ex affichage "252 379" mais valeur réelle 252379.45) — la valeur
        /// brute est toujours lue intégralement.
        /// </summary>
        private static decimal SafeDecimal(IXLCell? cell)
        {
            if (cell == null) return 0m;
            try
            {
                // Pour les cellules avec formule, utiliser CachedValue plutôt que Value
                // (Value peut tenter d'évaluer la formule et lancer une exception si
                // l'environnement n'a pas le moteur de calcul Excel).
                var val = cell.HasFormula ? cell.CachedValue : cell.Value;
                if (val.IsBlank) return 0m;
                if (val.IsError) return 0m;
                if (val.IsNumber) return (decimal)val.GetNumber();
                if (val.IsBoolean) return val.GetBoolean() ? 1m : 0m;
                if (!val.IsText) return 0m;

                var s = val.GetText() ?? "";
                if (string.IsNullOrWhiteSpace(s)) return 0m;
            // Tolérer espaces/séparateurs
            s = s.Replace(" ", "").Replace(" ", "").Replace("FCFA", "", StringComparison.OrdinalIgnoreCase);
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v1)) return v1;
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR"), out var v2)) return v2;
                return 0m;
            }
            catch
            {
                return 0m;
            }
        }

    }
}
