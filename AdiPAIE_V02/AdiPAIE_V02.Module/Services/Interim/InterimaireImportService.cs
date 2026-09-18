// =============================================================================
//  InterimaireImportService.cs - V1.9 - Import Excel liste effectif intérimaires
//
//  Charge la "Liste globale préso-réseau" fournie mensuellement par le RH
//  (ex: "Liste septembre 2026.xlsx" - 747 lignes, 14 colonnes).
//
//  Colonnes attendues (ordre) :
//    A: Nbr | B: Matricule (agence) | C: Prénoms | D: Noms | E: Sexe | F: Catégorie
//    G: Date naissance | H: Fonction | I: Business unit | J: Agence d'intérim
//    K: Site d'affectation | L: Date d'entrée | M: Ancienneté (ignoré, recalculé)
//    N: Nationalité
//
//  Normalisations appliquées :
//    - Agence : "SEN INTÉRIM" / "SEN. INTERIM" -> "SEN INTERIM"
//    - BU : "E.SERVICES" / "E-SERVICES" / "E. SERVICES" -> "E-SERVICES"
//           "DEPÔT" -> "DEPOT" ; "CONTRÔLE" -> "CONTROLE"
//    - Catégorie : casse et espaces normalisés (7EMEA / 7-EME A / 7èmeA -> "7ème A")
//    - Fonction : Trim + majuscule initiale
//
//  Doublons :
//    - Intra-fichier : le premier occ est créé, les suivants sont skippés (rapport)
//    - Intra-BDD (MatriculeAgence déjà présent) : skip silencieux (mise à jour ?)
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using ClosedXML.Excel;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services.Interim
{
    public class ResultatImportInterimaires
    {
        public int LignesLues { get; set; }
        public int Crees { get; set; }
        public int SkipDoublonsFichier { get; set; }
        public int SkipDejaEnBase { get; set; }
        public int ContratsCrees { get; set; }
        public int Erreurs { get; set; }

        public List<string> AgencesInconnues { get; } = new();
        public List<string> SitesCreesAuto { get; } = new();
        public List<string> BUsCreesAuto { get; } = new();
        public List<string> DetailErreurs { get; } = new();
        public List<string> DetailDoublons { get; } = new();

        public bool DryRun { get; set; }
        public TimeSpan Duree { get; set; }

        public string GenererRapport()
        {
            var sb = new StringBuilder();
            sb.AppendLine(DryRun ? "=== SIMULATION (dry-run) - RIEN N'A ÉTÉ CRÉÉ ===" : "=== IMPORT RÉEL EFFECTUÉ ===");
            sb.AppendLine();
            sb.AppendLine($"Durée : {Duree.TotalSeconds:N1} s");
            sb.AppendLine($"Lignes lues : {LignesLues}");
            sb.AppendLine($"  ✓ Intérimaires à créer / créés : {Crees}");
            sb.AppendLine($"  ✓ Contrats actifs créés : {ContratsCrees}");
            sb.AppendLine($"  ⏭ Doublons intra-fichier ignorés : {SkipDoublonsFichier}");
            sb.AppendLine($"  ⏭ Déjà en base (skip) : {SkipDejaEnBase}");
            sb.AppendLine($"  ✗ Erreurs : {Erreurs}");
            sb.AppendLine();

            if (AgencesInconnues.Any())
            {
                sb.AppendLine($"⚠ Agences inconnues (créées auto avec RaisonSociale) : {AgencesInconnues.Count}");
                foreach (var a in AgencesInconnues.Take(10)) sb.AppendLine($"    * {a}");
                if (AgencesInconnues.Count > 10) sb.AppendLine($"    …et {AgencesInconnues.Count - 10} autres");
                sb.AppendLine();
            }
            if (SitesCreesAuto.Any())
            {
                sb.AppendLine($"⚠ Sites créés automatiquement : {SitesCreesAuto.Count}");
                foreach (var s in SitesCreesAuto.Take(10)) sb.AppendLine($"    * {s}");
                if (SitesCreesAuto.Count > 10) sb.AppendLine($"    …et {SitesCreesAuto.Count - 10} autres");
                sb.AppendLine();
            }
            if (BUsCreesAuto.Any())
            {
                sb.AppendLine($"⚠ Unités organisationnelles (BU) créées auto : {BUsCreesAuto.Count}");
                foreach (var b in BUsCreesAuto.Take(10)) sb.AppendLine($"    * {b}");
                if (BUsCreesAuto.Count > 10) sb.AppendLine($"    …et {BUsCreesAuto.Count - 10} autres");
                sb.AppendLine();
            }
            if (DetailDoublons.Any())
            {
                sb.AppendLine($"-- Doublons ({DetailDoublons.Count}) --");
                foreach (var d in DetailDoublons.Take(20)) sb.AppendLine($"    * {d}");
                if (DetailDoublons.Count > 20) sb.AppendLine($"    …et {DetailDoublons.Count - 20} autres");
                sb.AppendLine();
            }
            if (DetailErreurs.Any())
            {
                sb.AppendLine($"-- Erreurs ({DetailErreurs.Count}) --");
                foreach (var e in DetailErreurs.Take(30)) sb.AppendLine($"    ✗ {e}");
                if (DetailErreurs.Count > 30) sb.AppendLine($"    …et {DetailErreurs.Count - 30} autres");
            }
            return sb.ToString();
        }
    }

    public static class InterimaireImportService
    {
        // ==================================================================
        //  API publique
        // ==================================================================
        public static ResultatImportInterimaires Importer(
            IObjectSpace os, byte[] fichierExcel,
            bool creerContratActif, DateTime dateFinContratDefaut, bool dryRun)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (fichierExcel == null || fichierExcel.Length == 0)
                throw new UserFriendlyException("Fichier vide.");

            var result = new ResultatImportInterimaires { DryRun = dryRun };
            var chrono = System.Diagnostics.Stopwatch.StartNew();

            var xpOs = os as DevExpress.ExpressApp.Xpo.XPObjectSpace
                       ?? throw new UserFriendlyException("Object space incompatible.");
            var session = xpOs.Session;

            // -- 1. Parser Excel ------------------------------------------
            List<LigneImport> lignes;
            using (var ms = new MemoryStream(fichierExcel))
            using (var wb = new XLWorkbook(ms))
            {
                var ws = wb.Worksheets.First();
                lignes = ParseFeuille(ws, result);
            }
            result.LignesLues = lignes.Count;

            // -- 2. Doublons intra-fichier (MatriculeAgence) --------------
            var vusDansFichier = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in lignes)
            {
                if (string.IsNullOrWhiteSpace(l.MatriculeAgence)) continue;
                if (!vusDansFichier.Add(l.MatriculeAgence))
                {
                    l.SkipDoublonFichier = true;
                    result.SkipDoublonsFichier++;
                    result.DetailDoublons.Add(
                        $"L{l.LigneExcel} : matricule '{l.MatriculeAgence}' déjà vu plus haut dans le fichier.");
                }
            }

            // -- 3. Cache des matricules déjà en base ---------------------
            var matriculesEnBase = new HashSet<string>(
                new XPQuery<Interimaire>(session)
                    .Where(i => i.MatriculeAgence != null)
                    .Select(i => i.MatriculeAgence)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

            // -- V1.9.2 - Caches locaux pour éviter les doublons d'entités
            // référentielles (Site, BU, Agence) créées dans la même session.
            // Sans cache, XPQuery peut ne pas voir les objets non-encore-commités
            // -> crée un 2e Site DIOURBEL avec le même code -> collision.
            var cacheSites = new Dictionary<string, Site>(StringComparer.OrdinalIgnoreCase);
            var cacheBUs = new Dictionary<string, UniteOrganisationnelle>(StringComparer.OrdinalIgnoreCase);
            var cacheAgences = new Dictionary<string, SocieteInterim>(StringComparer.OrdinalIgnoreCase);
            var codesSitesUtilises = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var codesBUsUtilises = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Précharger les codes existants en base
            foreach (var c in new XPQuery<Site>(session).Where(x => x.Code != null).Select(x => x.Code).ToList())
                codesSitesUtilises.Add(c);
            foreach (var c in new XPQuery<UniteOrganisationnelle>(session).Where(x => x.Code != null).Select(x => x.Code).ToList())
                codesBUsUtilises.Add(c);

            // -- 4. Traitement ligne par ligne ----------------------------
            foreach (var l in lignes)
            {
                if (l.SkipDoublonFichier) continue;

                try
                {
                    if (string.IsNullOrWhiteSpace(l.MatriculeAgence)
                        || string.IsNullOrWhiteSpace(l.Nom))
                    {
                        result.Erreurs++;
                        result.DetailErreurs.Add(
                            $"L{l.LigneExcel} : matricule ou nom vide - ignoré.");
                        continue;
                    }

                    if (matriculesEnBase.Contains(l.MatriculeAgence))
                    {
                        result.SkipDejaEnBase++;
                        continue;
                    }

                    if (!dryRun)
                    {
                        var interimaire = CreerInterimaire(session, l, result, cacheAgences);

                        if (creerContratActif)
                        {
                            CreerContrat(session, interimaire, l, dateFinContratDefaut, result,
                                cacheSites, cacheBUs, codesSitesUtilises, codesBUsUtilises);
                        }
                    }
                    matriculesEnBase.Add(l.MatriculeAgence);
                    result.Crees++;
                }
                catch (Exception ex)
                {
                    result.Erreurs++;
                    result.DetailErreurs.Add(
                        $"L{l.LigneExcel} ({l.MatriculeAgence}/{l.Nom}) : {ex.Message}");
                }
            }

            if (!dryRun)
            {
                os.CommitChanges();
            }

            chrono.Stop();
            result.Duree = chrono.Elapsed;
            return result;
        }

        // ==================================================================
        //  Parsing
        // ==================================================================
        private class LigneImport
        {
            public int LigneExcel;
            public string MatriculeAgence;
            public string Prenom;
            public string Nom;
            public Sexe? SexeVal;
            public string Categorie;
            public DateTime? DateNaissance;
            public string Fonction;
            public string BusinessUnit;
            public string Agence;
            public string Site;
            public DateTime? DateEntree;
            public string Nationalite;
            public bool SkipDoublonFichier;
        }

        private static List<LigneImport> ParseFeuille(IXLWorksheet ws, ResultatImportInterimaires r)
        {
            var lignes = new List<LigneImport>();
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int rn = 2; rn <= lastRow; rn++)
            {
                var row = ws.Row(rn);
                var matriculeCell = row.Cell(2).Value;
                var nomCell = row.Cell(4).Value;
                if (matriculeCell.IsBlank && nomCell.IsBlank) continue;

                var l = new LigneImport
                {
                    LigneExcel = rn,
                    MatriculeAgence = CellToStr(row.Cell(2)),
                    Prenom = NormaliserNomPrenom(CellToStr(row.Cell(3))),
                    Nom = NormaliserNomPrenom(CellToStr(row.Cell(4)))?.ToUpperInvariant(),
                    SexeVal = ParseSexe(CellToStr(row.Cell(5))),
                    Categorie = NormaliserCategorie(CellToStr(row.Cell(6))),
                    DateNaissance = CellToDate(row.Cell(7)),
                    Fonction = NormaliserFonction(CellToStr(row.Cell(8))),
                    BusinessUnit = NormaliserBU(CellToStr(row.Cell(9))),
                    Agence = NormaliserAgence(CellToStr(row.Cell(10))),
                    Site = NormaliserSite(CellToStr(row.Cell(11))),
                    DateEntree = CellToDate(row.Cell(12)),
                    Nationalite = CellToStr(row.Cell(14))
                };
                lignes.Add(l);
            }
            return lignes;
        }

        private static string CellToStr(IXLCell c)
        {
            if (c == null || c.IsEmpty()) return null;
            var v = c.Value.ToString()?.Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }

        private static DateTime? CellToDate(IXLCell c)
        {
            if (c == null || c.IsEmpty()) return null;
            try
            {
                if (c.Value.IsDateTime) return c.GetDateTime();
                var s = c.Value.ToString()?.Trim();
                if (string.IsNullOrEmpty(s)) return null;
                // formats FR possibles : dd/MM/yyyy, dd-MM-yyyy
                if (DateTime.TryParseExact(s, new[] { "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy" },
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var dt))
                    return dt;
                if (DateTime.TryParse(s, out var dt2)) return dt2;
            }
            catch { }
            return null;
        }

        // ==================================================================
        //  Normalisations (mapping en dur)
        // ==================================================================
        private static string NormaliserNomPrenom(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return Regex.Replace(s.Trim(), @"\s+", " ");
        }

        private static Sexe? ParseSexe(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = s.Trim().ToUpperInvariant();
            if (t.StartsWith("H") || t.StartsWith("M")) return Sexe.Masculin;
            if (t.StartsWith("F")) return Sexe.Feminin;
            return null;
        }

        private static string NormaliserAgence(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = Regex.Replace(s.Trim().ToUpperInvariant(), @"\s+", " ");
            // Retirer les accents pour normalisation
            var sansAccent = new string(t.Normalize(NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray());
            // Mapping connu ELTON
            return sansAccent switch
            {
                "SEN INTERIM" or "SEN. INTERIM" or "SENINTERIM" => "SEN INTERIM",
                "ASSI" => "ASSI",
                "TECTRA" => "TECTRA",
                _ => sansAccent
            };
        }

        private static string NormaliserBU(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = Regex.Replace(s.Trim().ToUpperInvariant(), @"\s+", " ");
            var sansAccent = new string(t.Normalize(NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray());
            // Rapprocher les variantes E-services
            var noSpaceNoDot = sansAccent.Replace(" ", "").Replace(".", "").Replace("-", "");
            return noSpaceNoDot switch
            {
                "ESERVICES" => "E-SERVICES",
                "DEPOT" => "DEPOT",
                "CONTROLE" => "CONTROLE",
                _ => sansAccent
            };
        }

        private static string NormaliserSite(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = Regex.Replace(s.Trim().ToUpperInvariant(), @"\s+", " ");
            return new string(t.Normalize(NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray());
        }

        private static string NormaliserFonction(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return Regex.Replace(s.Trim().ToUpperInvariant(), @"\s+", " ");
        }

        private static string NormaliserCategorie(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = s.Trim().ToUpperInvariant()
                .Replace(" ", "").Replace("-", "").Replace("È", "E").Replace("É", "E");
            // Extraire nombre + suffixe éventuel A/B
            var m = Regex.Match(t, @"^(\d+)EM?E?([AB])?$");
            if (!m.Success) return s.Trim();
            var n = m.Groups[1].Value;
            var suf = m.Groups[2].Value;
            return string.IsNullOrEmpty(suf) ? $"{n}ème" : $"{n}ème {suf}";
        }

        // ==================================================================
        //  Création des entités
        // ==================================================================
        private static Interimaire CreerInterimaire(Session s, LigneImport l,
            ResultatImportInterimaires r,
            Dictionary<string, SocieteInterim> cacheAgences)
        {
            var interim = new Interimaire(s);
            interim.MatriculeAgence = l.MatriculeAgence;
            interim.Nom = l.Nom;
            interim.Prenom = l.Prenom;
            interim.SexeInterim = l.SexeVal;
            interim.Categorie = l.Categorie;
            interim.DateNaissance = l.DateNaissance;
            interim.Fonction = l.Fonction;
            interim.Nationalite = l.Nationalite;
            interim.DateEntreeAgence = l.DateEntree;
            interim.Statut = InterimaireStatut.Disponible;

            // Agence obligatoire (RuleRequiredField)
            interim.SocieteInterim = ResoudreAgence(s, l.Agence, r, cacheAgences);
            return interim;
        }

        private static SocieteInterim ResoudreAgence(Session s, string nomAgence,
            ResultatImportInterimaires r,
            Dictionary<string, SocieteInterim> cache)
        {
            if (string.IsNullOrWhiteSpace(nomAgence))
                throw new UserFriendlyException("Agence d'intérim vide (colonne obligatoire).");

            // 1) Cache local (déjà vu dans cet import)
            if (cache.TryGetValue(nomAgence, out var cached)) return cached;

            // 2) BDD
            var agence = new XPQuery<SocieteInterim>(s)
                .FirstOrDefault(a => a.RaisonSociale != null
                                  && a.RaisonSociale.ToUpper() == nomAgence);
            if (agence == null)
            {
                agence = new SocieteInterim(s) { RaisonSociale = nomAgence };
                if (!r.AgencesInconnues.Contains(nomAgence))
                    r.AgencesInconnues.Add(nomAgence);
            }
            cache[nomAgence] = agence;
            return agence;
        }

        private static void CreerContrat(Session s, Interimaire interim, LigneImport l,
            DateTime dateFinDefaut, ResultatImportInterimaires r,
            Dictionary<string, Site> cacheSites,
            Dictionary<string, UniteOrganisationnelle> cacheBUs,
            HashSet<string> codesSitesUtilises,
            HashSet<string> codesBUsUtilises)
        {
            var contrat = new ContratInterim(s)
            {
                Interimaire = interim,
                Statut = ContratInterimStatut.EnCours,
                TypeContrat = ContratInterimType.PremiereMission,
                DateDebut = l.DateEntree ?? DateTime.Today,
                DateFin = dateFinDefaut > (l.DateEntree ?? DateTime.Today)
                        ? dateFinDefaut : (l.DateEntree ?? DateTime.Today).AddMonths(3),
                MotifRecours = "Import initial liste effectif"
            };

            // Site (V1.1) - cache local + création avec Code unique
            if (!string.IsNullOrWhiteSpace(l.Site))
            {
                Site site;
                if (!cacheSites.TryGetValue(l.Site, out site))
                {
                    site = new XPQuery<Site>(s)
                        .FirstOrDefault(x => x.Nom != null && x.Nom.ToUpper() == l.Site);
                    if (site == null)
                    {
                        var code = GenererCodeUnique(l.Site, isSite: true, codesSitesUtilises);
                        site = new Site(s) { Nom = l.Site, Code = code, Actif = true };
                        codesSitesUtilises.Add(code);
                        if (!r.SitesCreesAuto.Contains(l.Site))
                            r.SitesCreesAuto.Add(l.Site);
                    }
                    cacheSites[l.Site] = site;
                }
                contrat.Site = site;
            }

            // Unité organisationnelle (BU) - cache local + Site parent obligatoire
            if (!string.IsNullOrWhiteSpace(l.BusinessUnit) && contrat.Site != null)
            {
                // Clé cache combinant BU + Site (une même BU peut exister sur plusieurs sites)
                var cleBU = $"{l.BusinessUnit}|{contrat.Site.Oid}";
                UniteOrganisationnelle bu;
                if (!cacheBUs.TryGetValue(cleBU, out bu))
                {
                    var siteOid = contrat.Site.Oid;
                    bu = new XPQuery<UniteOrganisationnelle>(s)
                        .FirstOrDefault(x => x.Nom != null && x.Nom.ToUpper() == l.BusinessUnit
                                          && x.Site != null && x.Site.Oid == siteOid);
                    if (bu == null)
                    {
                        var code = GenererCodeUnique(l.BusinessUnit, isSite: false, codesBUsUtilises);
                        bu = new UniteOrganisationnelle(s)
                        {
                            Nom = l.BusinessUnit,
                            Code = code,
                            Site = contrat.Site
                        };
                        codesBUsUtilises.Add(code);
                        if (!r.BUsCreesAuto.Contains(l.BusinessUnit))
                            r.BUsCreesAuto.Add(l.BusinessUnit);
                    }
                    cacheBUs[cleBU] = bu;
                }
                contrat.Unites.Add(bu);
            }

            r.ContratsCrees++;
        }

        // ==================================================================
        //  Génération d'un code unique à partir d'un nom
        //  Format : IMP-<slug>[-N] tronqué à la longueur max.
        //  Utilise un HashSet des codes déjà pris (BDD + créés en session).
        // ==================================================================
        private static string GenererCodeUnique(string nom, bool isSite,
            HashSet<string> codesDejaPris)
        {
            var maxLen = isSite ? 20 : 60;
            var slug = Regex.Replace(
                nom.ToUpperInvariant().Trim().Normalize(NormalizationForm.FormD)
                   .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                       != System.Globalization.UnicodeCategory.NonSpacingMark)
                   .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c)).ToString(),
                @"[^A-Z0-9]+", "-").Trim('-');
            var basePrefix = "IMP-";
            var maxSlugLen = maxLen - basePrefix.Length - 4;
            if (slug.Length > maxSlugLen) slug = slug.Substring(0, maxSlugLen);
            var code = basePrefix + slug;

            int suffixe = 0;
            var candidate = code;
            while (codesDejaPris.Contains(candidate))
            {
                suffixe++;
                candidate = $"{code}-{suffixe}";
                if (candidate.Length > maxLen)
                    candidate = candidate.Substring(0, maxLen);
            }
            return candidate;
        }
    }
}
