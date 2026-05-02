using AdiPAIE_V02.Module.BusinessObjects;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Génère un fichier Excel professionnel du Tableau de Bord Effectifs RH
    /// avec KPIs, tableaux de distribution, tableau croisé et tableau d'évolution.
    /// Design aux couleurs Elton Oil (bleu / rouge / blanc).
    /// </summary>
    public static class DashboardExportExcelService
    {
        // ── Charte graphique Elton Oil ──
        private static readonly XLColor EltonBlue = XLColor.FromHtml("#003DA5");
        private static readonly XLColor EltonRed = XLColor.FromHtml("#E31E24");
        private static readonly XLColor EltonDark = XLColor.FromHtml("#2D2D2D");
        private static readonly XLColor LightGray = XLColor.FromHtml("#F2F2F2");
        private static readonly XLColor MedGray = XLColor.FromHtml("#666666");
        private static readonly XLColor LightBlue = XLColor.FromHtml("#E8F0FE");
        private static readonly XLColor Green = XLColor.FromHtml("#28A745");
        private static readonly XLColor Red = XLColor.FromHtml("#DC3545");
        private static readonly XLColor SubGray = XLColor.FromHtml("#E8E8E8");

        public static byte[] Generate(TableauBordEffectif tb)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Tableau de Bord RH");
            ws.ShowGridLines = false;

            // Largeurs : col A=séparateur, B=libellé(large), C=effectif, D=%,  E=sep, F-H idem, I=sep
            int[] colWidths = { 2, 24, 10, 8, 2, 24, 10, 8, 2, 24, 10, 8, 2, 24, 10, 8 };
            for (int i = 0; i < colWidths.Length; i++)
                ws.Column(i + 1).Width = colWidths[i];

            BuildBanner(ws);
            BuildKPIs(ws, tb);
            int leftEnd = BuildTables(ws, tb);
            int crEnd = BuildCroisee(ws, tb, leftEnd);
            BuildEvolution(ws, tb, crEnd);
            BuildDataSheet(wb, tb);

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var rawBytes = ms.ToArray();

            // ── Injection des graphiques natifs via OpenXML SDK ──
            int deptCount = tb.ParDepartement.Count;
            int evoCount = tb.EvolutionAnnuelle.Count;
            int catCount = tb.ParCategorieSexe.Count;
            int foncCount = tb.ParFonction.Count;

            // Préparer les données pour les caches des graphiques
            var evoLabels = tb.EvolutionAnnuelle.Select(e => e.Annee.ToString()).ToArray();
            var evoValues = tb.EvolutionAnnuelle.Select(e => (double)e.Effectif).ToArray();
            var deptLabels = tb.ParDepartement.Select(d => d.Libelle).ToArray();
            var deptValues = tb.ParDepartement.Select(d => (double)d.Effectif).ToArray();
            var catLabels = tb.ParCategorieSexe.Select(c => c.Categorie).ToArray();
            var catValH = tb.ParCategorieSexe.Select(c => (double)c.PctHommes).ToArray();
            var catValF = tb.ParCategorieSexe.Select(c => (double)c.PctFemmes).ToArray();

            var charts = new List<ChartDef>
            {
                // Pie H/F
                new ChartDef
                {
                    Title = "Répartition Homme / Femme",
                    Type = ChartType.Pie,
                    CategoryRange = "A2:A3",
                    ValueRanges = new[] { "B2:B3" },
                    SeriesNames = new[] { "Effectif" },
                    Colors = new[] { "003DA5", "E31E24" },
                    AnchorRow = 3, AnchorCol = 9, Width = 7, Height = 14,
                    CategoryLabels = new[] { "Hommes", "Femmes" },
                    SeriesValues = new[] { new double[] { tb.NbHommes, tb.NbFemmes } }
                },
                // Bar Évolution
                new ChartDef
                {
                    Title = "Évolution des Effectifs",
                    Type = ChartType.ColumnBar,
                    CategoryRange = $"D2:D{1 + evoCount}",
                    ValueRanges = new[] { $"E2:E{1 + evoCount}" },
                    SeriesNames = new[] { "Effectif" },
                    Colors = new[] { "003DA5" },
                    AnchorRow = 17, AnchorCol = 9, Width = 7, Height = 14,
                    CategoryLabels = evoLabels,
                    SeriesValues = new[] { evoValues }
                },
                // Bar Département (horizontal)
                new ChartDef
                {
                    Title = "Effectif par Département",
                    Type = ChartType.HorizontalBar,
                    CategoryRange = $"H2:H{1 + deptCount}",
                    ValueRanges = new[] { $"I2:I{1 + deptCount}" },
                    SeriesNames = new[] { "Effectif" },
                    Colors = new[] { "E31E24" },
                    AnchorRow = 31, AnchorCol = 9, Width = 7, Height = 14,
                    CategoryLabels = deptLabels,
                    SeriesValues = new[] { deptValues }
                },
                // Stacked H/F par Catégorie
                new ChartDef
                {
                    Title = "Répartition H/F par Catégorie",
                    Type = ChartType.StackedBar,
                    CategoryRange = $"K2:K{1 + catCount}",
                    ValueRanges = new[] { $"L2:L{1 + catCount}", $"M2:M{1 + catCount}" },
                    SeriesNames = new[] { "% Hommes", "% Femmes" },
                    Colors = new[] { "003DA5", "E31E24" },
                    AnchorRow = 45, AnchorCol = 9, Width = 7, Height = 14,
                    CategoryLabels = catLabels,
                    SeriesValues = new[] { catValH, catValF }
                },
            };

            // Ajouter un bar Fonction si données disponibles
            if (foncCount > 0)
            {
                var foncLabels = tb.ParFonction.Select(f => f.Libelle).ToArray();
                var foncValues = tb.ParFonction.Select(f => (double)f.Effectif).ToArray();
                charts.Add(new ChartDef
                {
                    Title = "Effectif par Fonction",
                    Type = ChartType.HorizontalBar,
                    CategoryRange = $"O2:O{1 + foncCount}",
                    ValueRanges = new[] { $"P2:P{1 + foncCount}" },
                    SeriesNames = new[] { "Effectif" },
                    Colors = new[] { "003DA5" },
                    AnchorRow = 59, AnchorCol = 9, Width = 7, Height = 14,
                    CategoryLabels = foncLabels,
                    SeriesValues = new[] { foncValues }
                });
            }

            return ExcelChartInjector.InjectCharts(rawBytes, "Tableau de Bord RH", "Données", charts);
        }

        // ── Bandeau titre Elton Oil ──
        private static void BuildBanner(IXLWorksheet ws)
        {
            var r = ws.Range(1, 1, 1, 16);
            r.Merge();
            r.Style.Fill.BackgroundColor = EltonBlue;
            var c = ws.Cell(1, 1);
            c.Value = "ELTON OIL — TABLEAU DE BORD EFFECTIFS RH";
            c.Style.Font.FontName = "Arial";
            c.Style.Font.Bold = true;
            c.Style.Font.FontSize = 16;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(1).Height = 45;

            // Ligne rouge accent
            var rAccent = ws.Range(2, 1, 2, 16);
            rAccent.Merge();
            rAccent.Style.Fill.BackgroundColor = EltonRed;
            ws.Row(2).Height = 4;

            var r3 = ws.Range(3, 1, 3, 16);
            r3.Merge();
            var c3 = ws.Cell(3, 1);
            c3.Value = $"Généré le {DateTime.Now:dd/MM/yyyy à HH:mm}  —  Exercice {DateTime.Now.Year}";
            c3.Style.Font.FontName = "Arial";
            c3.Style.Font.FontSize = 9;
            c3.Style.Font.FontColor = MedGray;
            c3.Style.Font.Italic = true;
            c3.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // ── KPI Cards avec badges H/F (Elton Oil) ──
        private static void BuildKPIs(IXLWorksheet ws, TableauBordEffectif tb)
        {
            double taux = tb.EffectifTotal > 0 ? Math.Round(100.0 * tb.NbFemmes / tb.EffectifTotal) : 0;

            var kpis = new (int col, string label, string value, string tagH, string tagF)[]
            {
                (2, "Effectif Total", tb.EffectifTotal.ToString(), $"\u2642 {tb.NbHommes}", $"\u2640 {tb.NbFemmes}"),
                (6, "Ancienneté Moyenne", $"{tb.AncienneteMoyenne:N1} ans",
                    $"\u2642 {tb.AncienneteMoyHommes:N1} ans", $"\u2640 {tb.AncienneteMoyFemmes:N1} ans"),
                (10, "Salaire Moyen", $"{tb.SalaireMoyen:N0} F",
                    $"\u2642 {tb.SalaireMoyHommes:N0}", $"\u2640 {tb.SalaireMoyFemmes:N0}"),
                (14, "Taux Féminisation", $"{taux:N0}%",
                    $"\u2642 {100 - taux:N0}%", $"\u2640 {taux:N0}%"),
            };

            foreach (var (col, label, value, tagH, tagF) in kpis)
            {
                int c2 = col + 1, c3 = col + 2;

                // Fond clair bleuté
                for (int rr = 5; rr <= 8; rr++)
                    for (int cc = col; cc <= c3; cc++)
                        ws.Cell(rr, cc).Style.Fill.BackgroundColor = LightBlue;

                // Label
                ws.Range(5, col, 5, c3).Merge();
                SetCell(ws.Cell(5, col), label, "Arial", 11, EltonBlue, bold: true, hAlign: XLAlignmentHorizontalValues.Center);

                // Valeur
                ws.Range(6, col, 6, c3).Merge();
                SetCell(ws.Cell(6, col), value, "Arial", 28, EltonDark, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                ws.Row(6).Height = 38;

                // Badge Homme (fond bleu Elton)
                SetCell(ws.Cell(7, col), tagH, "Arial", 10, XLColor.White, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                ws.Cell(7, col).Style.Fill.BackgroundColor = EltonBlue;

                // Badge Femme (fond rouge Elton)
                SetCell(ws.Cell(7, c2), tagF, "Arial", 10, XLColor.White, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                ws.Cell(7, c2).Style.Fill.BackgroundColor = EltonRed;
            }
        }

        // ── Tableaux de distribution ──
        private static int BuildTables(IXLWorksheet ws, TableauBordEffectif tb)
        {
            int row = 10;
            row = WriteDistTable(ws, "Répartition par Sexe", tb.ParSexe.ToList(), row, 2);
            row = WriteDistTable(ws, "Par Département", tb.ParDepartement.ToList(), row, 2);
            row = WriteDistTable(ws, "Par Ancienneté", tb.ParAnciennete.ToList(), row, 2);

            int row2 = 10;
            row2 = WriteDistTable(ws, "Par Catégorie", tb.ParCategorie.ToList(), row2, 6);
            row2 = WriteDistTable(ws, "Par Fonction", tb.ParFonction.ToList(), row2, 6);

            return Math.Max(row, row2);
        }

        private static int WriteDistTable(IXLWorksheet ws, string title,
            System.Collections.Generic.List<DistributionItem> data, int startRow, int startCol)
        {
            int c1 = startCol, c2 = startCol + 1, c3 = startCol + 2;

            // Titre section
            ws.Range(startRow, c1, startRow, c3).Merge();
            SetCell(ws.Cell(startRow, c1), title, "Arial", 11, EltonBlue, bold: true);
            for (int c = c1; c <= c3; c++)
                ws.Cell(startRow, c).Style.Fill.BackgroundColor = LightGray;

            // Badge année
            int r = startRow + 1;
            ws.Range(r, c2, r, c3).Merge();
            SetCell(ws.Cell(r, c2), DateTime.Now.Year.ToString(), "Arial", 10, EltonDark, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Cell(r, c2).Style.Fill.BackgroundColor = SubGray;

            // Données
            foreach (var item in data)
            {
                r++;
                // Libellé avec indicateur ▸
                SetCell(ws.Cell(r, c1), $"\u25B8 {item.Libelle}", "Arial", 10, EltonDark);
                ws.Cell(r, c1).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Cell(r, c1).Style.Border.BottomBorderColor = XLColor.FromHtml("#DDDDDD");

                // Effectif
                SetCell(ws.Cell(r, c2), item.Effectif.ToString(), "Arial", 10, EltonDark, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                ws.Cell(r, c2).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Cell(r, c2).Style.Border.BottomBorderColor = XLColor.FromHtml("#DDDDDD");

                // %
                var pctCell = ws.Cell(r, c3);
                SetCell(pctCell, $"{item.Pourcentage:N0}%", "Arial", 10, EltonBlue, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                pctCell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                pctCell.Style.Border.BottomBorderColor = XLColor.FromHtml("#DDDDDD");
                if (item.Pourcentage >= 50)
                    pctCell.Style.Fill.BackgroundColor = LightBlue;
            }

            return r + 2;
        }

        // ── Tableau croisé Catégorie × Sexe ──
        private static int BuildCroisee(IXLWorksheet ws, TableauBordEffectif tb, int startRow)
        {
            int cr = startRow + 1;
            ws.Range(cr, 2, cr, 8).Merge();
            SetCell(ws.Cell(cr, 2), "Répartition H/F par Catégorie", "Arial", 11, EltonBlue, bold: true);
            for (int c = 2; c <= 8; c++)
                ws.Cell(cr, c).Style.Fill.BackgroundColor = LightGray;

            cr++;
            string[] hdrs = { "Catégorie", "\u2642 Hommes", "\u2640 Femmes", "% H", "% F" };
            for (int i = 0; i < hdrs.Length; i++)
            {
                SetCell(ws.Cell(cr, 2 + i), hdrs[i], "Arial", 10, XLColor.White, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                ws.Cell(cr, 2 + i).Style.Fill.BackgroundColor = EltonBlue;
            }

            foreach (var item in tb.ParCategorieSexe)
            {
                cr++;
                SetCell(ws.Cell(cr, 2), item.Categorie, "Arial", 10, EltonDark);
                SetCell(ws.Cell(cr, 3), item.Hommes.ToString(), "Arial", 10, EltonBlue, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                SetCell(ws.Cell(cr, 4), item.Femmes.ToString(), "Arial", 10, EltonRed, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                SetCell(ws.Cell(cr, 5), $"{item.PctHommes:N0}%", "Arial", 10, EltonBlue, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                SetCell(ws.Cell(cr, 6), $"{item.PctFemmes:N0}%", "Arial", 10, EltonRed, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                for (int c = 2; c <= 6; c++)
                {
                    ws.Cell(cr, c).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    ws.Cell(cr, c).Style.Border.BottomBorderColor = XLColor.FromHtml("#DDDDDD");
                }
            }
            return cr;
        }

        // ── Tableau Évolution ──
        private static void BuildEvolution(IXLWorksheet ws, TableauBordEffectif tb, int startRow)
        {
            int r = startRow + 2;
            ws.Range(r, 2, r, 8).Merge();
            SetCell(ws.Cell(r, 2), "Évolution des Effectifs", "Arial", 11, EltonBlue, bold: true);
            for (int c = 2; c <= 8; c++)
                ws.Cell(r, c).Style.Fill.BackgroundColor = LightGray;

            r++;
            string[] hdrs2 = { "Année", "Effectif", "Variation", "Tendance" };
            for (int i = 0; i < hdrs2.Length; i++)
            {
                SetCell(ws.Cell(r, 2 + i), hdrs2[i], "Arial", 10, XLColor.White, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                ws.Cell(r, 2 + i).Style.Fill.BackgroundColor = EltonBlue;
            }

            foreach (var item in tb.EvolutionAnnuelle)
            {
                r++;
                SetCell(ws.Cell(r, 2), item.Annee.ToString(), "Arial", 10, EltonDark, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                SetCell(ws.Cell(r, 3), item.Effectif.ToString(), "Arial", 12, EltonDark, bold: true, hAlign: XLAlignmentHorizontalValues.Center);

                var varCell = ws.Cell(r, 4);
                if (item.Variation > 0)
                    SetCell(varCell, $"\u25B2{item.Variation:N0}%", "Arial", 10, Green, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                else if (item.Variation < 0)
                    SetCell(varCell, $"\u25BC{Math.Abs(item.Variation):N0}%", "Arial", 10, Red, bold: true, hAlign: XLAlignmentHorizontalValues.Center);
                else
                    SetCell(varCell, "—", "Arial", 10, MedGray, hAlign: XLAlignmentHorizontalValues.Center);

                // Barre visuelle
                int barLen = Math.Min(item.Effectif / 3, 20);
                SetCell(ws.Cell(r, 5), new string('\u2588', barLen), "Arial", 8, EltonRed);

                for (int c = 2; c <= 5; c++)
                {
                    ws.Cell(r, c).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    ws.Cell(r, c).Style.Border.BottomBorderColor = XLColor.FromHtml("#DDDDDD");
                }
            }
        }

        // ── Feuille Données (pour graphiques manuels) ──
        private static void BuildDataSheet(IXLWorkbook wb, TableauBordEffectif tb)
        {
            var ws = wb.Worksheets.Add("Données");

            ws.Cell("A1").Value = "Sexe"; ws.Cell("B1").Value = "Effectif";
            ws.Cell("A1").Style.Font.Bold = true; ws.Cell("B1").Style.Font.Bold = true;
            ws.Cell("A2").Value = "Hommes"; ws.Cell("B2").Value = tb.NbHommes;
            ws.Cell("A3").Value = "Femmes"; ws.Cell("B3").Value = tb.NbFemmes;

            ws.Cell("D1").Value = "Année"; ws.Cell("E1").Value = "Effectif"; ws.Cell("F1").Value = "Variation %";
            ws.Cell("D1").Style.Font.Bold = true; ws.Cell("E1").Style.Font.Bold = true; ws.Cell("F1").Style.Font.Bold = true;
            int r = 2;
            foreach (var item in tb.EvolutionAnnuelle)
            {
                ws.Cell(r, 4).Value = item.Annee; ws.Cell(r, 5).Value = item.Effectif;
                ws.Cell(r, 6).Value = item.Variation / 100; ws.Cell(r, 6).Style.NumberFormat.Format = "0.0%";
                r++;
            }

            ws.Cell("H1").Value = "Département"; ws.Cell("I1").Value = "Effectif";
            ws.Cell("H1").Style.Font.Bold = true; ws.Cell("I1").Style.Font.Bold = true;
            r = 2;
            foreach (var item in tb.ParDepartement)
            { ws.Cell(r, 8).Value = item.Libelle; ws.Cell(r, 9).Value = item.Effectif; r++; }

            ws.Cell("K1").Value = "Catégorie"; ws.Cell("L1").Value = "% Hommes"; ws.Cell("M1").Value = "% Femmes";
            ws.Cell("K1").Style.Font.Bold = true; ws.Cell("L1").Style.Font.Bold = true; ws.Cell("M1").Style.Font.Bold = true;
            r = 2;
            foreach (var item in tb.ParCategorieSexe)
            { ws.Cell(r, 11).Value = item.Categorie; ws.Cell(r, 12).Value = item.PctHommes; ws.Cell(r, 13).Value = item.PctFemmes; r++; }

            ws.Cell("O1").Value = "Fonction"; ws.Cell("P1").Value = "Effectif";
            ws.Cell("O1").Style.Font.Bold = true; ws.Cell("P1").Style.Font.Bold = true;
            r = 2;
            foreach (var item in tb.ParFonction)
            { ws.Cell(r, 15).Value = item.Libelle; ws.Cell(r, 16).Value = item.Effectif; r++; }
        }

        // ── Helper ──
        private static void SetCell(IXLCell cell, string value, string font, int size, XLColor color,
            bool bold = false, bool italic = false, XLAlignmentHorizontalValues hAlign = XLAlignmentHorizontalValues.Left)
        {
            cell.Value = value;
            cell.Style.Font.FontName = font;
            cell.Style.Font.FontSize = size;
            cell.Style.Font.FontColor = color;
            cell.Style.Font.Bold = bold;
            cell.Style.Font.Italic = italic;
            cell.Style.Alignment.Horizontal = hAlign;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
    }
}
