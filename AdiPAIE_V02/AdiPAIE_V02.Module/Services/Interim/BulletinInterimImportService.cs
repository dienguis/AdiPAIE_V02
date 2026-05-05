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
                    string matriculeStr = SafeStr(ws.Cell(r, colMatricule).Value);
                    string nom = SafeStr(ws.Cell(r, colNoms).Value);
                    string prenom = SafeStr(ws.Cell(r, colPrenoms).Value);

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
                        Sexe = colSexe > 0 ? SafeStr(ws.Cell(r, colSexe).Value).Trim() : "",
                        Fonction = colFonction > 0 ? SafeStr(ws.Cell(r, colFonction).Value).Trim() : "",
                        SiteAffectation = colSite > 0 ? SafeStr(ws.Cell(r, colSite).Value).Trim() : "",

                        Trentieme = colTrente > 0 ? SafeDecimal(ws.Cell(r, colTrente).Value) : 0m,
                        SalaireBase = colSalBase > 0 ? SafeDecimal(ws.Cell(r, colSalBase).Value) : 0m,
                        BrutImposable = colBrut > 0 ? SafeDecimal(ws.Cell(r, colBrut).Value) : 0m,

                        IpresSal = colIpresSal > 0 ? SafeDecimal(ws.Cell(r, colIpresSal).Value) : 0m,
                        IpresPat = colIpresPat > 0 ? SafeDecimal(ws.Cell(r, colIpresPat).Value) : 0m,
                        CssAll = colCssAll > 0 ? SafeDecimal(ws.Cell(r, colCssAll).Value) : 0m,
                        CssAcc = colCssAcc > 0 ? SafeDecimal(ws.Cell(r, colCssAcc).Value) : 0m,
                        IpmSal = colIpmSal > 0 ? SafeDecimal(ws.Cell(r, colIpmSal).Value) : 0m,
                        IpmPat = colIpmPat > 0 ? SafeDecimal(ws.Cell(r, colIpmPat).Value) : 0m,

                        CFCE = colCfce > 0 ? SafeDecimal(ws.Cell(r, colCfce).Value) : 0m,
                        RetenueIR = colIR > 0 ? SafeDecimal(ws.Cell(r, colIR).Value) : 0m,
                        RetenueTRIMF = colTRIMF > 0 ? SafeDecimal(ws.Cell(r, colTRIMF).Value) : 0m,

                        PrimeTransport = colTransport > 0 ? SafeDecimal(ws.Cell(r, colTransport).Value) : 0m,
                        PrimePanier = colPanier > 0 ? SafeDecimal(ws.Cell(r, colPanier).Value) : 0m,
                        IndemnitesDiverses = colIndemDiv > 0 ? SafeDecimal(ws.Cell(r, colIndemDiv).Value) : 0m,

                        NetAPayer = colNet > 0 ? SafeDecimal(ws.Cell(r, colNet).Value) : 0m,
                        Debours = SafeDecimal(ws.Cell(r, colDebours).Value),
                        CommissionAgence = SafeDecimal(ws.Cell(r, colCommAg).Value),
                        MontantHT = colHT > 0 ? SafeDecimal(ws.Cell(r, colHT).Value) : 0m,
                        TVA = SafeDecimal(ws.Cell(r, colTVA).Value),
                        TTC = SafeDecimal(ws.Cell(r, colTTC).Value)
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

                int nbAnciens = existingBatch.Bulletins?.Count ?? 0;
                if (existingBatch.Bulletins != null)
                {
                    foreach (var anc in existingBatch.Bulletins.ToList())
                        os.Delete(anc);
                }
                os.Delete(existingBatch);
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
                    interim = os.GetObjectByKey<Interimaire>(ligne.InterimaireExistantOid.Value);
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

            os.CommitChanges();
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
                var t = kv.Value.ToLowerInvariant();
                foreach (var kw in keywords)
                {
                    if (t.Contains(kw.ToLowerInvariant())) return kv.Key;
                }
            }
            return null;
        }

        private static bool IsPrestataireMatricule(string matricule)
        {
            if (string.IsNullOrWhiteSpace(matricule)) return false;
            var m = matricule.Trim().ToUpperInvariant();
            return m == "PRESTATAIRE" || m == "PREST" || m.StartsWith("PRESTATAIRE");
        }

        private static string SafeStr(object? value) => value?.ToString() ?? "";

        private static decimal SafeDecimal(object? value)
        {
            if (value == null) return 0m;
            if (value is double d) return (decimal)d;
            if (value is float f) return (decimal)f;
            if (value is decimal de) return de;
            if (value is int i) return i;
            if (value is long l) return l;
            var s = value.ToString();
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            // Tolérer espaces/séparateurs
            s = s.Replace(" ", "").Replace(" ", "").Replace("FCFA", "", StringComparison.OrdinalIgnoreCase);
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v1)) return v1;
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR"), out var v2)) return v2;
            return 0m;
        }

    }
}
