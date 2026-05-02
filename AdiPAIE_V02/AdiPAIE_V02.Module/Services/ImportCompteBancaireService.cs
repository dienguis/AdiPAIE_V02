// AdiPAIE_V02.Module/Services/ImportCompteBancaireService.cs
// Import en masse des comptes bancaires depuis un fichier Excel (.xlsx).
// Calqué sur ImportSalarieService / ImportConjointService.
using AdiPAIE_V02.Module.BusinessObjects;
using ClosedXML.Excel;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Importe des comptes bancaires depuis un fichier Excel (.xlsx).
    ///
    /// Colonnes attendues (ligne 1 = en-têtes) :
    ///   MatriculeSalarie | Banque | NumeroCompte
    ///   CodeBanque | CodeGuichet | CleRib | BIC | NomTitulaire
    ///   Mode | Valeur | Actif | Observations
    ///
    /// Règles :
    ///   - MatriculeSalarie, Banque, NumeroCompte sont obligatoires
    ///   - Mode : "MontantFixe", "Pourcentage", "Reliquat" (défaut : Reliquat)
    ///   - Valeur : montant FCFA si MontantFixe, % si Pourcentage, ignoré si Reliquat
    ///   - Idempotent : si un compte avec même NumeroCompte existe déjà pour ce salarié → ignoré
    ///   - NomTitulaire : par défaut FullName du salarié si vide
    ///   - Actif : "Oui"/"Non" (défaut Oui)
    /// </summary>
    public static class ImportCompteBancaireService
    {
        public static ImportResult Importer(IObjectSpace os, byte[] xlsxBytes)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (xlsxBytes == null || xlsxBytes.Length == 0)
                throw new UserFriendlyException("Fichier Excel vide ou introuvable.");

            var result = new ImportResult();

            using var ms = new MemoryStream(xlsxBytes);
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheets.First();

            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            for (int c = 1; c <= lastCol; c++)
            {
                var h = ws.Cell(1, c).GetString()?.Trim();
                if (!string.IsNullOrEmpty(h) && !headers.ContainsKey(h))
                    headers[h] = c;
            }

            if (!headers.ContainsKey("MatriculeSalarie"))
                throw new UserFriendlyException("Colonne 'MatriculeSalarie' introuvable.");
            if (!headers.ContainsKey("Banque"))
                throw new UserFriendlyException("Colonne 'Banque' introuvable.");
            if (!headers.ContainsKey("NumeroCompte"))
                throw new UserFriendlyException("Colonne 'NumeroCompte' introuvable.");

            var salaries = os.GetObjectsQuery<Salarie>()
                .ToDictionary(s => s.Matricule?.Trim() ?? "", s => s, StringComparer.OrdinalIgnoreCase);

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = 2; r <= lastRow; r++)
            {
                try
                {
                    var matricule = GetCell(ws, r, headers, "MatriculeSalarie")?.Trim();
                    if (string.IsNullOrWhiteSpace(matricule))
                    {
                        result.Ignores++;
                        result.Details.Add($"Ligne {r} : matricule vide — ignoré.");
                        continue;
                    }

                    if (!salaries.TryGetValue(matricule, out var sal))
                    {
                        result.Erreurs++;
                        result.Details.Add($"Ligne {r} : matricule '{matricule}' introuvable.");
                        continue;
                    }

                    var banque = GetCell(ws, r, headers, "Banque")?.Trim();
                    var numCompte = GetCell(ws, r, headers, "NumeroCompte")?.Trim();
                    if (string.IsNullOrWhiteSpace(banque) || string.IsNullOrWhiteSpace(numCompte))
                    {
                        result.Erreurs++;
                        result.Details.Add($"Ligne {r} : Banque ou NumeroCompte vide.");
                        continue;
                    }

                    // Idempotence : skipper si compte avec même NumeroCompte existe déjà
                    var dejaExiste = sal.ComptesBancaires
                        .Any(c => string.Equals(c.NumeroCompte, numCompte, StringComparison.OrdinalIgnoreCase));
                    if (dejaExiste)
                    {
                        result.Ignores++;
                        result.Details.Add($"Ligne {r} : compte '{numCompte}' déjà existant pour {matricule} — ignoré.");
                        continue;
                    }

                    var compte = os.CreateObject<CompteBancaireSalarie>();
                    compte.Salarie = sal;
                    compte.Banque = banque;
                    compte.NumeroCompte = numCompte;
                    compte.CodeBanque = GetCell(ws, r, headers, "CodeBanque")?.Trim();
                    compte.CodeGuichet = GetCell(ws, r, headers, "CodeGuichet")?.Trim();
                    compte.CleRib = GetCell(ws, r, headers, "CleRib")?.Trim();
                    compte.BIC = GetCell(ws, r, headers, "BIC")?.Trim();

                    var nomTitulaire = GetCell(ws, r, headers, "NomTitulaire")?.Trim();
                    compte.NomTitulaire = string.IsNullOrWhiteSpace(nomTitulaire)
                        ? sal.FullName
                        : nomTitulaire;

                    // Mode (par défaut Reliquat)
                    var modeStr = GetCell(ws, r, headers, "Mode")?.Trim();
                    compte.Mode = ParseMode(modeStr);

                    // Valeur
                    var valeurStr = GetCell(ws, r, headers, "Valeur");
                    if (compte.Mode == ModeVirement.Reliquat)
                    {
                        compte.Valeur = 0m;  // ignoré pour Reliquat
                    }
                    else if (decimal.TryParse(valeurStr, out var v))
                    {
                        compte.Valeur = v;
                    }

                    // Actif (défaut true)
                    var actifStr = GetCell(ws, r, headers, "Actif")?.Trim();
                    compte.Actif = string.IsNullOrWhiteSpace(actifStr)
                        ? true
                        : ParseBool(actifStr);

                    compte.Observations = GetCell(ws, r, headers, "Observations")?.Trim();

                    result.Crees++;
                }
                catch (Exception ex)
                {
                    result.Erreurs++;
                    result.Details.Add($"Ligne {r} : {ex.Message}");
                }
            }

            if (result.Crees > 0)
                os.CommitChanges();

            return result;
        }

        public static byte[] GenererModele()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Import Comptes Bancaires");

            var headers = new[]
            {
                "MatriculeSalarie", "Banque", "NumeroCompte",
                "CodeBanque", "CodeGuichet", "CleRib", "BIC", "NomTitulaire",
                "Mode", "Valeur", "Actif", "Observations"
            };

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F6E56");
                cell.Style.Font.FontColor = XLColor.White;
            }

            // Exemple 1 : compte unique en mode Reliquat (cas standard)
            var exemple1 = new[] {
                "SAL001", "SGBS ROUME PART", "004002089747",
                "SN011", "1029", "81", "SGSNSNDA", "",
                "Reliquat", "0", "Oui", "Compte principal"
            };
            // Exemple 2 : double compte (50% sur épargne, reste en reliquat)
            var exemple2 = new[] {
                "SAL002", "CBAO HANN MARINAS", "035157380901",
                "SN012", "1284", "52", "", "",
                "Pourcentage", "50", "Oui", "Compte épargne"
            };

            for (int c = 0; c < exemple1.Length; c++)
            {
                ws.Cell(2, c + 1).Value = exemple1[c];
                ws.Cell(3, c + 1).Value = exemple2[c];
                ws.Cell(2, c + 1).Style.Font.Italic = true;
                ws.Cell(3, c + 1).Style.Font.Italic = true;
                ws.Cell(2, c + 1).Style.Font.FontColor = XLColor.Gray;
                ws.Cell(3, c + 1).Style.Font.FontColor = XLColor.Gray;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            using var msOut = new MemoryStream();
            wb.SaveAs(msOut);
            return msOut.ToArray();
        }

        private static ModeVirement ParseMode(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return ModeVirement.Reliquat;
            var v = s.Trim().ToUpperInvariant();
            if (v == "MONTANTFIXE" || v == "MONTANT FIXE" || v == "MF" || v == "FIXE") return ModeVirement.MontantFixe;
            if (v == "POURCENTAGE" || v == "%" || v == "PCT") return ModeVirement.Pourcentage;
            if (v == "RELIQUAT" || v == "REL" || v == "R") return ModeVirement.Reliquat;
            return ModeVirement.Reliquat;
        }

        private static bool ParseBool(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var v = s.Trim().ToUpperInvariant();
            return v == "OUI" || v == "O" || v == "VRAI" || v == "TRUE" || v == "1" || v == "YES" || v == "Y";
        }

        private static string GetCell(IXLWorksheet ws, int row,
            Dictionary<string, int> headers, string colName)
        {
            if (!headers.TryGetValue(colName, out int col)) return null;
            var val = ws.Cell(row, col).Value;
            if (val.IsBlank) return null;
            return val.ToString();
        }

        public class ImportResult
        {
            public int Crees { get; set; }
            public int Ignores { get; set; }
            public int Erreurs { get; set; }
            public List<string> Details { get; set; } = new();
            public string Resume => $"{Crees} créé(s), {Ignores} ignoré(s), {Erreurs} erreur(s).";
        }
    }
}
