// AdiPAIE_V02.Module/Services/ImportSalarieService.cs
// Import en masse des salariés depuis un fichier Excel (.xlsx)
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
    /// Importe des salariés depuis un fichier Excel.
    ///
    /// Colonnes attendues (ligne 1 = en-têtes) :
    ///   Matricule | Nom | Prenom | Sexe | SituationMaritale |
    ///   DateNaissance | DateEmbauche | NombreEnfants |
    ///   SalaireBase | IndemniteLogement | Sursalaire | PrimeTransport |
    ///   PossedeVehicule | AvantageVehicule |
    ///   Echelon | Departement | Fonction |
    ///   Email | Telephone
    ///
    /// Règles :
    ///   - Matricule, Nom, Prenom sont obligatoires
    ///   - Matricule unique → les doublons sont ignorés
    ///   - Si Echelon est renseigné, SalaireBase/IndemniteLogement/Catégorie/Convention
    ///     sont automatiquement alignés depuis l'échelon (via AlignerDepuisEchelon)
    ///   - Les colonnes manquantes ou vides sont ignorées (pas d'erreur)
    /// </summary>
    public static class ImportSalarieService
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

            // Lire les en-têtes (row 1)
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            for (int c = 1; c <= lastCol; c++)
            {
                var h = ws.Cell(1, c).GetString()?.Trim();
                if (!string.IsNullOrEmpty(h) && !headers.ContainsKey(h))
                    headers[h] = c;
            }

            // Vérifier les colonnes obligatoires
            if (!headers.ContainsKey("Matricule"))
                throw new UserFriendlyException(
                    "Colonne 'Matricule' introuvable. Vérifiez les en-têtes (ligne 1).");
            if (!headers.ContainsKey("Nom"))
                throw new UserFriendlyException(
                    "Colonne 'Nom' introuvable. Vérifiez les en-têtes (ligne 1).");
            if (!headers.ContainsKey("Prenom"))
                throw new UserFriendlyException(
                    "Colonne 'Prenom' introuvable. Vérifiez les en-têtes (ligne 1).");

            // Charger les matricules existants
            var matriculesExistants = new HashSet<string>(
                os.GetObjectsQuery<Salarie>().Select(s => s.Matricule),
                StringComparer.OrdinalIgnoreCase);

            // Lookups existants
            var departements = os.GetObjectsQuery<Departement>()
                .ToDictionary(d => d.Nom?.Trim() ?? "", d => d, StringComparer.OrdinalIgnoreCase);
            var fonctions = os.GetObjectsQuery<Fonction>()
                .ToDictionary(f => f.Intitule?.Trim() ?? "", f => f, StringComparer.OrdinalIgnoreCase);
            var echelons = os.GetObjectsQuery<Echelons>()
                .ToDictionary(e => e.Code?.Trim() ?? "", e => e, StringComparer.OrdinalIgnoreCase);
            // Sites : indexés par Code ET par Nom pour souplesse à l'import
            // .AsEnumerable() requis : XPO IQueryable ne sait pas traduire ?. en SQL
            var sitesAll = os.GetObjectsQuery<Site>().AsEnumerable().ToList();
            var sitesParCode = sitesAll
                .Where(s => !string.IsNullOrWhiteSpace(s.Code))
                .ToDictionary(s => s.Code.Trim(), s => s, StringComparer.OrdinalIgnoreCase);
            var sitesParNom = sitesAll
                .Where(s => !string.IsNullOrWhiteSpace(s.Nom))
                .GroupBy(s => s.Nom.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = 2; r <= lastRow; r++)
            {
                try
                {
                    var matricule = GetCell(ws, r, headers, "Matricule")?.Trim();
                    if (string.IsNullOrWhiteSpace(matricule))
                    {
                        result.Ignores++;
                        result.Details.Add($"Ligne {r} : matricule vide — ignoré.");
                        continue;
                    }

                    if (matriculesExistants.Contains(matricule))
                    {
                        result.Ignores++;
                        result.Details.Add($"Ligne {r} : {matricule} existe déjà — ignoré.");
                        continue;
                    }

                    var nom = GetCell(ws, r, headers, "Nom")?.Trim();
                    var prenom = GetCell(ws, r, headers, "Prenom")?.Trim();
                    if (string.IsNullOrWhiteSpace(nom) || string.IsNullOrWhiteSpace(prenom))
                    {
                        result.Erreurs++;
                        result.Details.Add($"Ligne {r} : nom ou prénom vide — erreur.");
                        continue;
                    }

                    var sal = os.CreateObject<Salarie>();
                    sal.Matricule = matricule;
                    sal.LastName = nom;
                    sal.FirstName = prenom;

                    // ── Sexe ──────────────────────────────────────────
                    var sexeStr = GetCell(ws, r, headers, "Sexe")?.Trim();
                    if (!string.IsNullOrWhiteSpace(sexeStr))
                    {
                        var s = sexeStr.ToUpperInvariant();
                        if (s == "M" || s == "MASCULIN" || s == "HOMME")
                            sal.Sexe = Sexe.Masculin;
                        else if (s == "F" || s == "FEMININ" || s == "FÉMININ" || s == "FEMME")
                            sal.Sexe = Sexe.Feminin;
                    }

                    // ── Situation matrimoniale ────────────────────────
                    var sitStr = GetCell(ws, r, headers, "SituationMaritale")?.Trim();
                    if (!string.IsNullOrWhiteSpace(sitStr))
                    {
                        var s = sitStr.ToUpperInvariant();
                        if (s == "CELIBATAIRE" || s == "CÉLIBATAIRE" || s == "C")
                            sal.SatutMarital = SituationMaritale.Celibataire;
                        else if (s == "MARIE" || s == "MARIÉ" || s == "MARIÉE" || s == "M")
                            sal.SatutMarital = SituationMaritale.Marie;
                        else if (s == "DIVORCE" || s == "DIVORCÉ" || s == "DIVORCÉE" || s == "D")
                            sal.SatutMarital = SituationMaritale.Divorce;
                        else if (s == "VEUF" || s == "VEUVE" || s == "V")
                            sal.SatutMarital = SituationMaritale.Veuf;
                    }

                    // ── Nombre d'enfants ──────────────────────────────
                    var enfStr = GetCell(ws, r, headers, "NombreEnfants");
                    if (int.TryParse(enfStr, out var nbEnf))
                        sal.NombreEnfant = nbEnf;

                    // ── Dates ─────────────────────────────────────────
                    var dateNaissStr = GetCell(ws, r, headers, "DateNaissance");
                    if (DateTime.TryParse(dateNaissStr, out var dn))
                        sal.Birthday = dn;

                    var dateEmbStr = GetCell(ws, r, headers, "DateEmbauche");
                    if (DateTime.TryParse(dateEmbStr, out var de))
                        sal.DateEmbauche = de;
                    else
                        sal.DateEmbauche = DateTime.Today;

                    // ── Échelon (lookup par code) ─────────────────────
                    // Si trouvé → AlignerDepuisEchelon() se déclenche automatiquement
                    // et affecte SalaireBase, IndemniteLogement, Categories, Convention
                    var echStr = GetCell(ws, r, headers, "Echelon")?.Trim();
                    if (!string.IsNullOrWhiteSpace(echStr) && echelons.TryGetValue(echStr, out var ech))
                        sal.Echelon = ech;

                    // ── Rémunération (seulement si pas d'échelon) ─────
                    // Si un échelon est affecté, le salaire/logement viennent de l'échelon
                    if (sal.Echelon == null)
                    {
                        var sbStr = GetCell(ws, r, headers, "SalaireBase");
                        if (decimal.TryParse(sbStr, out var sb))
                            sal.SalaireBase = sb;

                        var logtStr = GetCell(ws, r, headers, "IndemniteLogement");
                        if (decimal.TryParse(logtStr, out var logt))
                            sal.IndemniteLogement = logt;
                    }

                    // ── Sursalaire ────────────────────────────────────
                    var surStr = GetCell(ws, r, headers, "Sursalaire");
                    if (decimal.TryParse(surStr, out var sur))
                        sal.Sursalaire = sur;

                    // ── Transport ─────────────────────────────────────
                    var transStr = GetCell(ws, r, headers, "PrimeTransport");
                    if (decimal.TryParse(transStr, out var trans))
                        sal.PrimeTransport = trans;

                    // ── Véhicule ──────────────────────────────────────
                    var vehStr = GetCell(ws, r, headers, "PossedeVehicule")?.Trim();
                    if (!string.IsNullOrWhiteSpace(vehStr))
                    {
                        var v = vehStr.ToUpperInvariant();
                        sal.PossedeVehicule = v == "OUI" || v == "O" || v == "TRUE" || v == "1";
                    }

                    var avVehStr = GetCell(ws, r, headers, "AvantageVehicule");
                    if (decimal.TryParse(avVehStr, out var avVeh) && avVeh > 0)
                        sal.AvantageVehicule = avVeh;

                    // ── Contact ───────────────────────────────────────
                    var email = GetCell(ws, r, headers, "Email")?.Trim();
                    if (!string.IsNullOrWhiteSpace(email))
                        sal.Email = email;

                    var tel = GetCell(ws, r, headers, "Telephone")?.Trim();
                    if (!string.IsNullOrWhiteSpace(tel))
                    {
                        try
                        {
                            var phone = os.CreateObject<DevExpress.Persistent.BaseImpl.PhoneNumber>();
                            phone.Number = tel;
                            sal.PhoneNumbers.Add(phone);
                        }
                        catch { /* PhoneNumber non disponible — ignoré */ }
                    }

                    // ── Département (lookup par nom) ──────────────────
                    var deptStr = GetCell(ws, r, headers, "Departement")?.Trim();
                    if (!string.IsNullOrWhiteSpace(deptStr) && departements.TryGetValue(deptStr, out var dept))
                        sal.Departement = dept;

                    // ── Fonction (lookup par intitulé) ─────────────────
                    var foncStr = GetCell(ws, r, headers, "Fonction")?.Trim();
                    if (!string.IsNullOrWhiteSpace(foncStr) && fonctions.TryGetValue(foncStr, out var fonc))
                        sal.Fonction = fonc;

                    // ── Site (lookup par Code OU par Nom) ──────────────
                    var siteStr = GetCell(ws, r, headers, "Site")?.Trim();
                    if (!string.IsNullOrWhiteSpace(siteStr))
                    {
                        if (sitesParCode.TryGetValue(siteStr, out var siteByCode))
                            sal.Site = siteByCode;
                        else if (sitesParNom.TryGetValue(siteStr, out var siteByNom))
                            sal.Site = siteByNom;
                    }

                    matriculesExistants.Add(matricule);
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
            var ws = wb.Worksheets.Add("Import Salariés");

            var headers = new[]
            {
                "Matricule", "Nom", "Prenom", "Sexe", "SituationMaritale",
                "DateNaissance", "DateEmbauche", "NombreEnfants",
                "Echelon", "SalaireBase", "IndemniteLogement",
                "Sursalaire", "PrimeTransport",
                "PossedeVehicule", "AvantageVehicule",
                "Departement", "Fonction", "Site",
                "Email", "Telephone"
            };

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F6E56");
                cell.Style.Font.FontColor = XLColor.White;
            }

            // Exemple ligne 2
            var exemple = new[] {
                "SAL001", "DIOP", "Moussa", "M", "Marie",
                "15/03/1990", "01/01/2024", "2",
                "E1-A", "300000", "60000",
                "25000", "26000",
                "Non", "0",
                "Comptabilité", "Comptable", "Siège",
                "moussa.diop@exemple.sn", "771234567"
            };
            for (int c = 0; c < exemple.Length; c++)
            {
                ws.Cell(2, c + 1).Value = exemple[c];
                ws.Cell(2, c + 1).Style.Font.Italic = true;
                ws.Cell(2, c + 1).Style.Font.FontColor = XLColor.Gray;
            }

            // Ajuster largeurs
            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            using var msOut = new MemoryStream();
            wb.SaveAs(msOut);
            return msOut.ToArray();
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
