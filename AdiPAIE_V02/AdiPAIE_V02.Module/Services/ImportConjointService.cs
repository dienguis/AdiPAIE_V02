// AdiPAIE_V02.Module/Services/ImportConjointService.cs
// Import en masse des conjoints depuis un fichier Excel (.xlsx)
// Calqué sur ImportSalarieService.
using AdiPAIE_V02.Module.BusinessObjects;
using ClosedXML.Excel;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Importe des conjoints depuis un fichier Excel (.xlsx).
    ///
    /// Colonnes attendues (ligne 1 = en-têtes) :
    ///   MatriculeSalarie | NomComplet | DateMariage | DateFinUnion | Statut | ACharge
    ///
    /// Règles :
    ///   - MatriculeSalarie et NomComplet sont obligatoires
    ///   - Le salarié doit être Marié(e) sinon la ligne est rejetée
    ///   - Idempotent : si un conjoint existe déjà avec le même NomComplet pour ce salarié → ignoré
    ///   - Statut : "Actif", "Inactif", "NonRenseigne" (par défaut Inactif si vide)
    ///   - ACharge : "Oui"/"Non"/"Vrai"/"Faux"/"1"/"0" (par défaut true si Statut=Inactif, sinon false)
    /// </summary>
    public static class ImportConjointService
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
                throw new UserFriendlyException(
                    "Colonne 'MatriculeSalarie' introuvable. Vérifiez les en-têtes (ligne 1).");
            if (!headers.ContainsKey("NomComplet"))
                throw new UserFriendlyException(
                    "Colonne 'NomComplet' introuvable. Vérifiez les en-têtes (ligne 1).");

            // Charger l'index salariés par matricule
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

                    var nomComplet = GetCell(ws, r, headers, "NomComplet")?.Trim();
                    if (string.IsNullOrWhiteSpace(nomComplet))
                    {
                        result.Erreurs++;
                        result.Details.Add($"Ligne {r} : NomComplet vide — erreur.");
                        continue;
                    }

                    // Validation : salarié doit être Marié
                    if (sal.SatutMarital != SituationMaritale.Marie)
                    {
                        result.Erreurs++;
                        result.Details.Add(
                            $"Ligne {r} : {matricule} ({sal.FullName}) n'est pas Marié — conjoint rejeté.");
                        continue;
                    }

                    // Idempotence : skipper si conjoint avec même NomComplet existe déjà
                    var dejaExiste = sal.Conjoints
                        .Any(c => string.Equals(c.NomComplet, nomComplet, StringComparison.OrdinalIgnoreCase));
                    if (dejaExiste)
                    {
                        result.Ignores++;
                        result.Details.Add($"Ligne {r} : conjoint '{nomComplet}' déjà existant pour {matricule} — ignoré.");
                        continue;
                    }

                    var conj = os.CreateObject<Conjoint>();
                    conj.Salarie = sal;
                    conj.NomComplet = nomComplet;

                    // Dates
                    var dateMariageStr = GetCell(ws, r, headers, "DateMariage");
                    if (DateTime.TryParse(dateMariageStr, out var dm))
                        conj.DateMariage = dm;

                    var dateFinStr = GetCell(ws, r, headers, "DateFinUnion");
                    if (DateTime.TryParse(dateFinStr, out var df))
                        conj.DateFinUnion = df;

                    // Statut (par défaut Inactif)
                    var statutStr = GetCell(ws, r, headers, "Statut")?.Trim();
                    conj.Statut = ParseStatut(statutStr);

                    // ACharge (par défaut true si Inactif, sinon false)
                    var aChargeStr = GetCell(ws, r, headers, "ACharge")?.Trim();
                    conj.ACharge = ParseACharge(aChargeStr, conj.Statut);

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

        /// <summary>
        /// Génère un fichier Excel modèle vide avec les en-têtes attendus.
        /// </summary>
        public static byte[] GenererModele()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Import Conjoints");

            var headers = new[]
            {
                "MatriculeSalarie", "NomComplet",
                "DateMariage", "DateFinUnion",
                "Statut", "ACharge"
            };

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F6E56");
                cell.Style.Font.FontColor = XLColor.White;
            }

            // Exemples (ligne 2 et 3)
            var exemple1 = new[] {
                "SAL001", "Khady NDIAYE",
                "15/06/2010", "",
                "Inactif", "Oui"
            };
            var exemple2 = new[] {
                "SAL001", "Awa SECK",
                "20/12/2018", "",
                "Inactif", "Oui"
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

        private static StatutConjoint ParseStatut(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return StatutConjoint.Inactif;
            var v = s.ToUpperInvariant();
            if (v == "ACTIF" || v == "A") return StatutConjoint.Actif;
            if (v == "INACTIF" || v == "I") return StatutConjoint.Inactif;
            if (v == "NONRENSEIGNE" || v == "NON RENSEIGNE" || v == "N" || v == "?") return StatutConjoint.NonRenseigne;
            return StatutConjoint.Inactif;
        }

        private static bool ParseACharge(string s, StatutConjoint statut)
        {
            // Si non renseigné, par défaut : Inactif → true, sinon → false
            if (string.IsNullOrWhiteSpace(s))
                return statut == StatutConjoint.Inactif;
            var v = s.ToUpperInvariant();
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
