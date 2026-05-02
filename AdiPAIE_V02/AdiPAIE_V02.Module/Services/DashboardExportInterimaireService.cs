using AdiPAIE_V02.Module.BusinessObjects;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Génère un fichier Excel professionnel du Tableau de Bord Intérimaires.
    /// Design aux couleurs Elton Oil (bleu / rouge / blanc).
    /// </summary>
    public static class DashboardExportInterimaireService
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

        public static byte[] Generate(TableauBordInterimaire tb)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Tableau de Bord Intérimaires");
            ws.ShowGridLines = false;

            int[] colWidths = { 2, 24, 10, 8, 2, 24, 10, 8, 2, 24, 10, 8, 2, 24, 10, 8 };
            for (int i = 0; i < colWidths.Length; i++)
                ws.Column(i + 1).Width = colWidths[i];

            // ── Bandeau Elton Oil ──
            var r1 = ws.Range(1, 1, 1, 16);
            r1.Merge();
            r1.Style.Fill.BackgroundColor = EltonBlue;
            Set(ws.Cell(1, 1), "ELTON OIL — TABLEAU DE BORD INTÉRIMAIRES", "Arial", 16, XLColor.White, true, XLAlignmentHorizontalValues.Center);
            ws.Row(1).Height = 45;

            // Ligne rouge accent
            var rAccent = ws.Range(2, 1, 2, 16);
            rAccent.Merge();
            rAccent.Style.Fill.BackgroundColor = EltonRed;
            ws.Row(2).Height = 4;

            var r2 = ws.Range(3, 1, 3, 16);
            r2.Merge();
            Set(ws.Cell(3, 1), $"Généré le {DateTime.Now:dd/MM/yyyy à HH:mm}  —  Exercice {DateTime.Now.Year}",
                "Arial", 9, MedGray, false, XLAlignmentHorizontalValues.Center, true);

            // ── KPIs ──
            var kpis = new (int col, string label, string value, string sub)[]
            {
                (2, "Effectif", tb.EffectifTotal.ToString(), "intérimaires actifs"),
                (4, "Départs", $"{tb.PctDeparts:N0}%", $"{tb.NbDeparts} sur 12 mois"),
                (6, "Rotation", $"{tb.TauxRotation:N0}%", "taux annuel"),
                (8, "Ancienneté Moy.", $"{tb.AncienneteMoyenne:N1} ans", "depuis 1er contrat"),
                (10, "Femmes", $"{tb.PctFemmes:N0}%", $"{tb.NbFemmes} sur {tb.EffectifTotal}"),
                (12, "Hommes", $"{tb.PctHommes:N0}%", $"{tb.NbHommes} sur {tb.EffectifTotal}"),
                (14, "Âge Moyen", $"{tb.AgeMoyen:N0} ans", ""),
            };

            foreach (var (col, label, value, sub) in kpis)
            {
                int c2 = col + 1;
                for (int rr = 5; rr <= 8; rr++)
                    for (int cc = col; cc <= c2; cc++)
                        ws.Cell(rr, cc).Style.Fill.BackgroundColor = LightBlue;

                ws.Range(5, col, 5, c2).Merge();
                Set(ws.Cell(5, col), label, "Arial", 10, EltonBlue, true, XLAlignmentHorizontalValues.Center);

                ws.Range(6, col, 6, c2).Merge();
                Set(ws.Cell(6, col), value, "Arial", 22, EltonDark, true, XLAlignmentHorizontalValues.Center);
                ws.Row(6).Height = 35;

                ws.Range(7, col, 7, c2).Merge();
                Set(ws.Cell(7, col), sub, "Arial", 8, EltonRed, true, XLAlignmentHorizontalValues.Center);
            }

            // ── Tableaux ──
            int row = 10;
            row = WriteDist(ws, "Par Tranche d'Âge", tb.ParTrancheAge.ToList(), row, 2);
            row = WriteDist(ws, "Par Ancienneté", tb.ParAnciennete.ToList(), row, 2);
            row = WriteDist(ws, "Par Type Contrat", tb.ParTypeContrat.ToList(), row, 2);

            int row2 = 10;
            row2 = WriteDist(ws, "Par Station / Segment", tb.ParStation.ToList(), row2, 6);
            row2 = WriteDist(ws, "Par Fonction (Top 5)", tb.ParFonction.ToList(), row2, 6);
            row2 = WriteDist(ws, "Par Société Intérim", tb.ParSociete.ToList(), row2, 6);

            // ── Évolution ──
            int er = Math.Max(row, row2) + 1;
            ws.Range(er, 2, er, 8).Merge();
            Set(ws.Cell(er, 2), "Évolution des Effectifs Intérimaires", "Arial", 11, EltonBlue, true);
            for (int c = 2; c <= 8; c++)
                ws.Cell(er, c).Style.Fill.BackgroundColor = LightGray;

            er++;
            foreach (var h in new[] { "Année", "Effectif", "Variation", "Tendance" })
            {
                int ci = 2 + System.Array.IndexOf(new[] { "Année", "Effectif", "Variation", "Tendance" }, h);
                Set(ws.Cell(er, ci), h, "Arial", 10, XLColor.White, true, XLAlignmentHorizontalValues.Center);
                ws.Cell(er, ci).Style.Fill.BackgroundColor = EltonBlue;
            }

            foreach (var item in tb.EvolutionAnnuelle)
            {
                er++;
                Set(ws.Cell(er, 2), item.Annee.ToString(), "Arial", 10, EltonDark, true, XLAlignmentHorizontalValues.Center);
                Set(ws.Cell(er, 3), item.Effectif.ToString(), "Arial", 12, EltonDark, true, XLAlignmentHorizontalValues.Center);

                if (item.Variation > 0)
                    Set(ws.Cell(er, 4), $"\u25B2{item.Variation:N0}%", "Arial", 10, Green, true, XLAlignmentHorizontalValues.Center);
                else if (item.Variation < 0)
                    Set(ws.Cell(er, 4), $"\u25BC{Math.Abs(item.Variation):N0}%", "Arial", 10, Red, true, XLAlignmentHorizontalValues.Center);
                else
                    Set(ws.Cell(er, 4), "—", "Arial", 10, MedGray, false, XLAlignmentHorizontalValues.Center);

                int barLen = Math.Min(item.Effectif / 3, 20);
                Set(ws.Cell(er, 5), new string('\u2588', Math.Max(barLen, 1)), "Arial", 8, EltonRed);

                for (int c = 2; c <= 5; c++)
                {
                    ws.Cell(er, c).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    ws.Cell(er, c).Style.Border.BottomBorderColor = XLColor.FromHtml("#DDDDDD");
                }
            }

            // ── Feuille Données (pour graphiques) ──
            BuildDataSheet(wb, tb);

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var rawBytes = ms.ToArray();

            // ── Injection des graphiques natifs via OpenXML SDK ──
            int stationCount = tb.ParStation.Count;
            int evoCount = tb.EvolutionAnnuelle.Count;
            int typeCount = tb.ParTypeContrat.Count;
            int societeCount = tb.ParSociete.Count;

            // Préparer les données pour les caches des graphiques
            var typeLabels = tb.ParTypeContrat.Select(t => t.Libelle).ToArray();
            var typeValues = tb.ParTypeContrat.Select(t => (double)t.Effectif).ToArray();
            var evoLabels = tb.EvolutionAnnuelle.Select(e => e.Annee.ToString()).ToArray();
            var evoValues = tb.EvolutionAnnuelle.Select(e => (double)e.Effectif).ToArray();
            var stationLabels = tb.ParStation.Select(s => s.Libelle).ToArray();
            var stationValues = tb.ParStation.Select(s => (double)s.Effectif).ToArray();

            var charts = new List<ChartDef>
            {
                // Pie Type Contrat
                new ChartDef
                {
                    Title = "Par Type de Contrat",
                    Type = ChartType.Pie,
                    CategoryRange = $"A2:A{1 + typeCount}",
                    ValueRanges = new[] { $"B2:B{1 + typeCount}" },
                    SeriesNames = new[] { "Effectif" },
                    Colors = new[] { "003DA5", "E31E24", "2D2D2D", "28A745" },
                    AnchorRow = 3, AnchorCol = 9, Width = 7, Height = 14,
                    CategoryLabels = typeLabels,
                    SeriesValues = new[] { typeValues }
                },
                // Bar Évolution
                new ChartDef
                {
                    Title = "Évolution des Effectifs Intérimaires",
                    Type = ChartType.ColumnBar,
                    CategoryRange = $"D2:D{1 + evoCount}",
                    ValueRanges = new[] { $"E2:E{1 + evoCount}" },
                    SeriesNames = new[] { "Effectif" },
                    Colors = new[] { "003DA5" },
                    AnchorRow = 17, AnchorCol = 9, Width = 7, Height = 14,
                    CategoryLabels = evoLabels,
                    SeriesValues = new[] { evoValues }
                },
                // Bar Station (horizontal)
                new ChartDef
                {
                    Title = "Effectif par Station / Segment",
                    Type = ChartType.HorizontalBar,
                    CategoryRange = $"G2:G{1 + stationCount}",
                    ValueRanges = new[] { $"H2:H{1 + stationCount}" },
                    SeriesNames = new[] { "Effectif" },
                    Colors = new[] { "E31E24" },
                    AnchorRow = 31, AnchorCol = 9, Width = 7, Height = 14,
                    CategoryLabels = stationLabels,
                    SeriesValues = new[] { stationValues }
                },
            };

            // Pie Société Intérim si données
            if (societeCount > 0)
            {
                var socLabels = tb.ParSociete.Select(s => s.Libelle).ToArray();
                var socValues = tb.ParSociete.Select(s => (double)s.Effectif).ToArray();
                charts.Add(new ChartDef
                {
                    Title = "Par Société Intérim",
                    Type = ChartType.Pie,
                    CategoryRange = $"J2:J{1 + societeCount}",
                    ValueRanges = new[] { $"K2:K{1 + societeCount}" },
                    SeriesNames = new[] { "Effectif" },
                    Colors = new[] { "003DA5", "E31E24", "2D2D2D", "28A745", "666666" },
                    AnchorRow = 45, AnchorCol = 9, Width = 7, Height = 14,
                    CategoryLabels = socLabels,
                    SeriesValues = new[] { socValues }
                });
            }

            return ExcelChartInjector.InjectCharts(rawBytes, "Tableau de Bord Intérimaires", "Données", charts);
        }

        private static int WriteDist(IXLWorksheet ws, string title,
            System.Collections.Generic.List<DistributionItem> data, int startRow, int startCol)
        {
            int c1 = startCol, c2 = startCol + 1, c3 = startCol + 2;

            ws.Range(startRow, c1, startRow, c3).Merge();
            Set(ws.Cell(startRow, c1), title, "Arial", 11, EltonBlue, true);
            for (int c = c1; c <= c3; c++)
                ws.Cell(startRow, c).Style.Fill.BackgroundColor = LightGray;

            int r = startRow + 1;
            ws.Range(r, c2, r, c3).Merge();
            Set(ws.Cell(r, c2), DateTime.Now.Year.ToString(), "Arial", 10, EltonDark, true, XLAlignmentHorizontalValues.Center);
            ws.Cell(r, c2).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8E8E8");

            foreach (var item in data)
            {
                r++;
                // Libellé avec indicateur ▸
                Set(ws.Cell(r, c1), $"\u25B8 {item.Libelle}", "Arial", 10, EltonDark);
                ws.Cell(r, c1).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Cell(r, c1).Style.Border.BottomBorderColor = XLColor.FromHtml("#DDDDDD");

                Set(ws.Cell(r, c2), item.Effectif.ToString(), "Arial", 10, EltonDark, true, XLAlignmentHorizontalValues.Center);
                ws.Cell(r, c2).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Cell(r, c2).Style.Border.BottomBorderColor = XLColor.FromHtml("#DDDDDD");

                Set(ws.Cell(r, c3), $"{item.Pourcentage:N0}%", "Arial", 10, EltonBlue, true, XLAlignmentHorizontalValues.Center);
                ws.Cell(r, c3).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Cell(r, c3).Style.Border.BottomBorderColor = XLColor.FromHtml("#DDDDDD");
                if (item.Pourcentage >= 50)
                    ws.Cell(r, c3).Style.Fill.BackgroundColor = LightBlue;
            }
            return r + 2;
        }

        // ── Feuille Données (pour graphiques) ──
        private static void BuildDataSheet(IXLWorkbook wb, TableauBordInterimaire tb)
        {
            var ws = wb.Worksheets.Add("Données");

            // Type Contrat
            ws.Cell("A1").Value = "Type Contrat"; ws.Cell("B1").Value = "Effectif";
            ws.Cell("A1").Style.Font.Bold = true; ws.Cell("B1").Style.Font.Bold = true;
            int r = 2;
            foreach (var item in tb.ParTypeContrat)
            { ws.Cell(r, 1).Value = item.Libelle; ws.Cell(r, 2).Value = item.Effectif; r++; }

            // Évolution
            ws.Cell("D1").Value = "Année"; ws.Cell("E1").Value = "Effectif";
            ws.Cell("D1").Style.Font.Bold = true; ws.Cell("E1").Style.Font.Bold = true;
            r = 2;
            foreach (var item in tb.EvolutionAnnuelle)
            { ws.Cell(r, 4).Value = item.Annee; ws.Cell(r, 5).Value = item.Effectif; r++; }

            // Station
            ws.Cell("G1").Value = "Station"; ws.Cell("H1").Value = "Effectif";
            ws.Cell("G1").Style.Font.Bold = true; ws.Cell("H1").Style.Font.Bold = true;
            r = 2;
            foreach (var item in tb.ParStation)
            { ws.Cell(r, 7).Value = item.Libelle; ws.Cell(r, 8).Value = item.Effectif; r++; }

            // Société Intérim
            ws.Cell("J1").Value = "Société"; ws.Cell("K1").Value = "Effectif";
            ws.Cell("J1").Style.Font.Bold = true; ws.Cell("K1").Style.Font.Bold = true;
            r = 2;
            foreach (var item in tb.ParSociete)
            { ws.Cell(r, 10).Value = item.Libelle; ws.Cell(r, 11).Value = item.Effectif; r++; }
        }

        private static void Set(IXLCell cell, string value, string font, int size, XLColor color,
            bool bold = false, XLAlignmentHorizontalValues hAlign = XLAlignmentHorizontalValues.Left,
            bool italic = false)
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
