// AdiPAIE_V02.Module/Services/EtatVRSService.cs
// Génère l'état VRS mensuel (Versement Retenue à la Source)
// Format conforme à la déclaration mensuelle DGID Sénégal
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
    /// Génère l'état VRS (Versement Retenue à la Source) mensuel.
    /// Colonnes : Matricule, Statut, Nom, BRUT, IR, TRIMF, CFCE,
    ///            IPRES (RG salarié/employeur, RC salarié/employeur),
    ///            CSS (Accident Travail, Cotisation Familiale), NET.
    /// </summary>
    public static class EtatVRSService
    {
        // ── Couleurs ──────────────────────────────────────────────────────────
        private static readonly XLColor BG_HEADER = XLColor.FromHtml("#0F6E56");
        private static readonly XLColor BG_SUBHEAD = XLColor.FromHtml("#D6F0E8");
        private static readonly XLColor BG_GROUP = XLColor.FromHtml("#E8F5E9");
        private static readonly XLColor BG_TOTAL = XLColor.FromHtml("#FFF2CC");
        private static readonly XLColor BG_SUBTOTAL = XLColor.FromHtml("#F0F4C3");
        private static readonly XLColor FG_WHITE = XLColor.White;

        /// <summary>
        /// Génère l'état VRS pour une année et un mois donnés.
        /// Retourne les bytes du fichier XLSX.
        /// </summary>
        public static byte[] Generer(IObjectSpace os, int annee, int mois)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (mois < 1 || mois > 12)
                throw new UserFriendlyException($"Mois invalide : {mois}.");

            // ── 1. Données société ────────────────────────────────────────────
            var company = os.GetObjectsQuery<Company>().FirstOrDefault();
            var raisonSociale = company?.RaisonSociale ?? "";
            var ninea = company?.NINEA ?? "";

            // ── 2. Charger les bulletins du mois ──────────────────────────────
            var statuts = new[]
            {
                BulletinStatut.Valide, BulletinStatut.Exporte,
                BulletinStatut.Envoye, BulletinStatut.Cloture,
                BulletinStatut.Comptabilise
            };

            var bulletins = os.GetObjectsQuery<Bulletin>()
                .Where(b => b.Annee == annee && b.Mois == mois
                         && statuts.Contains(b.Statut))
                .ToList();

            if (!bulletins.Any())
                throw new UserFriendlyException(
                    $"Aucun bulletin validé trouvé pour {mois:D2}/{annee}.");

            // ── 3. Charger toutes les lignes en une seule requête ─────────────
            var bulletinOids = bulletins.Select(b => b.Oid).ToList();
            var toutesLesLignes = os.GetObjectsQuery<BulletinLigne>()
                .Where(l => bulletinOids.Contains(l.Bulletin.Oid))
                .ToList()
                .Select(l => new LigneFlat
                {
                    SalarieOid = l.Bulletin?.Salarie?.Oid ?? Guid.Empty,
                    Montant = l.Montant,
                    MontantEmployeur = l.MontantEmployeur,
                    Canonique = l.Rubrique?.Canonique,
                    EstCFCE = l.Rubrique?.Canonique == RubriqueCanonique.CFCE
                               || (l.Rubrique?.Code ?? "").ToUpper() == "CFCE",
                })
                .ToList();

            var lignesParSalarie = toutesLesLignes
                .GroupBy(l => l.SalarieOid)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ── 4. Agréger par salarié ────────────────────────────────────────
            var lignes = BuildLignes(bulletins, lignesParSalarie);

            // ── 5. Construire le fichier Excel ────────────────────────────────
            using var wb = new XLWorkbook();
            var nomMois = NomMoisFr(mois);
            var ws = wb.Worksheets.Add($"VRS {nomMois} {annee}");

            EcrireEnTete(ws, raisonSociale, ninea, annee, mois);
            EcrireColonnes(ws);
            int ligneData = EcrireDonnees(ws, lignes);
            EcrireTotaux(ws, ligneData, lignes);
            AppliquerMiseEnPage(ws);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  CONSTRUCTION DES LIGNES
        // ═════════════════════════════════════════════════════════════════════

        private static List<LigneVRS> BuildLignes(
            IList<Bulletin> bulletins,
            Dictionary<Guid, List<LigneFlat>> lignesParSalarie)
        {
            return bulletins
                .Where(b => b.Salarie != null)
                .Select(b =>
                {
                    var sal = b.Salarie;
                    var oid = sal.Oid;
                    var ll = lignesParSalarie.TryGetValue(oid, out var list)
                        ? list : new List<LigneFlat>();

                    // IPRES RG : part salarié = Montant, part employeur = MontantEmployeur
                    var ipresRgSal = ll
                        .Where(l => l.Canonique == RubriqueCanonique.IPRES_RG)
                        .Sum(l => l.Montant);
                    var ipresRgEmp = ll
                        .Where(l => l.Canonique == RubriqueCanonique.IPRES_RG)
                        .Sum(l => l.MontantEmployeur);

                    // IPRES RC : part salarié = Montant, part employeur = MontantEmployeur
                    var ipresRcSal = ll
                        .Where(l => l.Canonique == RubriqueCanonique.IPRES_RC)
                        .Sum(l => l.Montant);
                    var ipresRcEmp = ll
                        .Where(l => l.Canonique == RubriqueCanonique.IPRES_RC)
                        .Sum(l => l.MontantEmployeur);

                    // CSS
                    var cssAt = ll
                        .Where(l => l.Canonique == RubriqueCanonique.CSS_AccidentTravail)
                        .Sum(l => l.MontantEmployeur);
                    var cssAf = ll
                        .Where(l => l.Canonique == RubriqueCanonique.CSS_AllocationFamiliale)
                        .Sum(l => l.MontantEmployeur);

                    // CFCE (patronale uniquement)
                    var cfce = ll
                        .Where(l => l.EstCFCE)
                        .Sum(l => l.MontantEmployeur);

                    return new LigneVRS
                    {
                        Matricule = sal.Matricule ?? "",
                        Statut = sal.Categories?.Intitule ?? "",
                        NomPrenom = sal.FullName?.ToUpper() ?? "",
                        Brut = b.BrutFiscal,
                        IR = b.IR_Mois,
                        TRIMF = b.TRIMF_Mois,
                        CFCE = cfce,
                        IPRES_RG_Sal = ipresRgSal,
                        IPRES_RG_Emp = ipresRgEmp,
                        IPRES_RC_Sal = ipresRcSal,
                        IPRES_RC_Emp = ipresRcEmp,
                        CSS_AT = cssAt,
                        CSS_AF = cssAf,
                        Net = b.NetAPayer,
                    };
                })
                .OrderBy(l => l.NomPrenom)
                .ToList();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  EN-TÊTE
        // ═════════════════════════════════════════════════════════════════════

        private static void EcrireEnTete(IXLWorksheet ws,
            string raisonSociale, string ninea, int annee, int mois)
        {
            var nomMois = NomMoisFr(mois);

            // Ligne 1 : Titre
            ws.Cell(1, 1).Value = $"ÉTAT VRS — {nomMois.ToUpper()} {annee}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.FontColor = BG_HEADER;
            ws.Range(1, 1, 1, 14).Merge();

            // Ligne 2 : Société
            ws.Cell(2, 1).Value = $"Société : {raisonSociale}";
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 7).Value = $"NINEA : {ninea}";
            ws.Cell(2, 7).Style.Font.Bold = true;

            // Ligne 3 : Période
            ws.Cell(3, 1).Value = $"Période : {nomMois} {annee}";
            ws.Cell(3, 1).Style.Font.Italic = true;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  COLONNES (2 lignes d'en-tête : groupes + détail)
        // ═════════════════════════════════════════════════════════════════════

        private static void EcrireColonnes(IXLWorksheet ws)
        {
            int r1 = 5; // Ligne groupes
            int r2 = 6; // Ligne détail

            // ── Ligne 1 : groupes fusionnés ──────────────────────────────────

            // Colonnes 1-4 : pas de groupe (Matricule, Statut, Nom, BRUT)
            // VRS : colonnes 5-7
            ws.Cell(r1, 5).Value = "VRS";
            ws.Range(r1, 5, r1, 7).Merge();
            StyleGroupe(ws.Range(r1, 5, r1, 7));

            // IPRES : colonnes 8-11
            ws.Cell(r1, 8).Value = "IPRES";
            ws.Range(r1, 8, r1, 11).Merge();
            StyleGroupe(ws.Range(r1, 8, r1, 11));

            // CSS : colonnes 12-13
            ws.Cell(r1, 12).Value = "CSS";
            ws.Range(r1, 12, r1, 13).Merge();
            StyleGroupe(ws.Range(r1, 12, r1, 13));

            // Colonnes sans groupe : 1-4 et 14
            foreach (var col in new[] { 1, 2, 3, 4, 14 })
            {
                ws.Cell(r1, col).Style.Fill.BackgroundColor = BG_HEADER;
                ws.Cell(r1, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // ── Ligne 2 : en-têtes détaillés ─────────────────────────────────
            var headers = new (int col, string label)[]
            {
                (1,  "Matricule"),
                (2,  "Statut"),
                (3,  "Nom"),
                (4,  "BRUT"),
                (5,  "IR"),
                (6,  "TRIMF"),
                (7,  "CFCE"),
                (8,  "Régime\nGénéral"),
                (9,  "Employeur"),
                (10, "Régime\nCadre"),
                (11, "Employeur"),
                (12, "Accid.\nTravail"),
                (13, "Cotisa.\nFamil."),
                (14, "NET"),
            };

            foreach (var (col, label) in headers)
            {
                var cell = ws.Cell(r2, col);
                cell.Value = label;
                cell.Style.Fill.BackgroundColor = BG_HEADER;
                cell.Style.Font.FontColor = FG_WHITE;
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 9;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            ws.Row(r2).Height = 35;
        }

        private static void StyleGroupe(IXLRange range)
        {
            range.Style.Fill.BackgroundColor = BG_GROUP;
            range.Style.Font.Bold = true;
            range.Style.Font.FontSize = 10;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  DONNÉES
        // ═════════════════════════════════════════════════════════════════════

        private static int EcrireDonnees(IXLWorksheet ws, List<LigneVRS> lignes)
        {
            int r = 7; // Première ligne de données (après les 2 lignes d'en-tête)

            foreach (var l in lignes)
            {
                ws.Cell(r, 1).Value = l.Matricule;
                ws.Cell(r, 2).Value = l.Statut;
                ws.Cell(r, 3).Value = l.NomPrenom;
                ws.Cell(r, 4).Value = (double)l.Brut;
                ws.Cell(r, 5).Value = (double)l.IR;
                ws.Cell(r, 6).Value = (double)l.TRIMF;
                ws.Cell(r, 7).Value = (double)l.CFCE;
                ws.Cell(r, 8).Value = (double)l.IPRES_RG_Sal;
                ws.Cell(r, 9).Value = (double)l.IPRES_RG_Emp;
                ws.Cell(r, 10).Value = (double)l.IPRES_RC_Sal;
                ws.Cell(r, 11).Value = (double)l.IPRES_RC_Emp;
                ws.Cell(r, 12).Value = (double)l.CSS_AT;
                ws.Cell(r, 13).Value = (double)l.CSS_AF;
                ws.Cell(r, 14).Value = (double)l.Net;

                // Format nombres
                for (int c = 4; c <= 14; c++)
                    ws.Cell(r, c).Style.NumberFormat.Format = "#,##0";

                // Style ligne
                for (int c = 1; c <= 14; c++)
                {
                    var cell = ws.Cell(r, c);
                    cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#CCCCCC");
                    cell.Style.Font.FontSize = 9;
                }

                // Alternance fond
                if (r % 2 == 0)
                    ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F7F7F7");

                r++;
            }

            return r; // Prochaine ligne disponible
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TOTAUX & SOUS-TOTAUX
        // ═════════════════════════════════════════════════════════════════════

        private static void EcrireTotaux(IXLWorksheet ws, int r, List<LigneVRS> lignes)
        {
            // ── Ligne TOTAL ──────────────────────────────────────────────────
            ws.Cell(r, 1).Value = "TOTAL";
            ws.Cell(r, 1).Style.Font.Bold = true;

            var totaux = new (int col, double val)[]
            {
                (4,  (double)lignes.Sum(l => l.Brut)),
                (5,  (double)lignes.Sum(l => l.IR)),
                (6,  (double)lignes.Sum(l => l.TRIMF)),
                (7,  (double)lignes.Sum(l => l.CFCE)),
                (8,  (double)lignes.Sum(l => l.IPRES_RG_Sal)),
                (9,  (double)lignes.Sum(l => l.IPRES_RG_Emp)),
                (10, (double)lignes.Sum(l => l.IPRES_RC_Sal)),
                (11, (double)lignes.Sum(l => l.IPRES_RC_Emp)),
                (12, (double)lignes.Sum(l => l.CSS_AT)),
                (13, (double)lignes.Sum(l => l.CSS_AF)),
                (14, (double)lignes.Sum(l => l.Net)),
            };

            foreach (var (col, val) in totaux)
            {
                var cell = ws.Cell(r, col);
                cell.Value = val;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = BG_TOTAL;
                cell.Style.NumberFormat.Format = "#,##0";
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Style ligne TOTAL
            for (int c = 1; c <= 14; c++)
            {
                ws.Cell(r, c).Style.Fill.BackgroundColor = BG_TOTAL;
                ws.Cell(r, c).Style.Font.Bold = true;
                ws.Cell(r, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            ws.Cell(r, 2).Value = $"{lignes.Count} salarié(s)";
            ws.Cell(r, 2).Style.Font.Bold = true;

            // ── Ligne sous-totaux par groupe ─────────────────────────────────
            r++;
            ws.Cell(r, 4).Value = "Sous-totaux :";
            ws.Cell(r, 4).Style.Font.Bold = true;
            ws.Cell(r, 4).Style.Font.Italic = true;

            // VRS (IR + TRIMF + CFCE)
            var totalVRS = lignes.Sum(l => l.IR + l.TRIMF + l.CFCE);
            ws.Cell(r, 5).Value = (double)totalVRS;
            ws.Range(r, 5, r, 7).Merge();
            StyleSousTotal(ws.Cell(r, 5), BG_SUBTOTAL);

            // IPRES (RG sal + RG emp + RC sal + RC emp)
            var totalIPRES = lignes.Sum(l => l.IPRES_RG_Sal + l.IPRES_RG_Emp
                                           + l.IPRES_RC_Sal + l.IPRES_RC_Emp);
            ws.Cell(r, 8).Value = (double)totalIPRES;
            ws.Range(r, 8, r, 11).Merge();
            StyleSousTotal(ws.Cell(r, 8), BG_SUBTOTAL);

            // CSS (AT + AF)
            var totalCSS = lignes.Sum(l => l.CSS_AT + l.CSS_AF);
            ws.Cell(r, 12).Value = (double)totalCSS;
            ws.Range(r, 12, r, 13).Merge();
            StyleSousTotal(ws.Cell(r, 12), BG_SUBTOTAL);
        }

        private static void StyleSousTotal(IXLCell cell, XLColor bg)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = bg;
            cell.Style.NumberFormat.Format = "#,##0";
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MISE EN PAGE
        // ═════════════════════════════════════════════════════════════════════

        private static void AppliquerMiseEnPage(IXLWorksheet ws)
        {
            ws.Column(1).Width = 12;  // Matricule
            ws.Column(2).Width = 14;  // Statut
            ws.Column(3).Width = 26;  // Nom
            ws.Column(4).Width = 16;  // BRUT
            ws.Column(5).Width = 14;  // IR
            ws.Column(6).Width = 12;  // TRIMF
            ws.Column(7).Width = 12;  // CFCE
            ws.Column(8).Width = 14;  // IPRES RG Sal
            ws.Column(9).Width = 14;  // IPRES RG Emp
            ws.Column(10).Width = 14; // IPRES RC Sal
            ws.Column(11).Width = 14; // IPRES RC Emp
            ws.Column(12).Width = 14; // CSS AT
            ws.Column(13).Width = 14; // CSS AF
            ws.Column(14).Width = 16; // NET

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A3Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(5, 6);
            ws.SheetView.FreezeRows(6);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private static string NomMoisFr(int mois) => mois switch
        {
            1 => "Janvier", 2 => "Février", 3 => "Mars",
            4 => "Avril", 5 => "Mai", 6 => "Juin",
            7 => "Juillet", 8 => "Août", 9 => "Septembre",
            10 => "Octobre", 11 => "Novembre", 12 => "Décembre",
            _ => mois.ToString()
        };

        // ═════════════════════════════════════════════════════════════════════
        //  DTO INTERNE
        // ═════════════════════════════════════════════════════════════════════

        // Type anonyme aplati pour la requête LINQ
        private class LigneFlat
        {
            public Guid SalarieOid { get; set; }
            public decimal Montant { get; set; }
            public decimal MontantEmployeur { get; set; }
            public RubriqueCanonique? Canonique { get; set; }
            public bool EstCFCE { get; set; }
        }

        private class LigneVRS
        {
            public string Matricule { get; set; }
            public string Statut { get; set; }
            public string NomPrenom { get; set; }
            public decimal Brut { get; set; }
            public decimal IR { get; set; }
            public decimal TRIMF { get; set; }
            public decimal CFCE { get; set; }
            public decimal IPRES_RG_Sal { get; set; }
            public decimal IPRES_RG_Emp { get; set; }
            public decimal IPRES_RC_Sal { get; set; }
            public decimal IPRES_RC_Emp { get; set; }
            public decimal CSS_AT { get; set; }
            public decimal CSS_AF { get; set; }
            public decimal Net { get; set; }
        }
    }
}
