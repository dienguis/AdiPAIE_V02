// AdiPAIE_V02.Module/Services/RapportCEOExcelGenerator.cs
// Génération du rapport CEO au format Excel multi-onglets avec graphiques.
using ClosedXML.Excel;
using System;
using System.IO;
using System.Linq;

namespace AdiPAIE_V02.Module.Services
{
    public static class RapportCEOExcelGenerator
    {
        public static byte[] Generer(RapportCEOData d)
        {
            using var wb = new XLWorkbook();

            BuildSynthese(wb, d);
            BuildEffectifs(wb, d);
            BuildMasseSalariale(wb, d);
            BuildConges(wb, d);
            BuildTurnover(wb, d);
            BuildAlertes(wb, d);

            // Injection de graphiques OpenXML
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var bytes = ms.ToArray();

            bytes = InjecterGraphiques(bytes, d);
            return bytes;
        }

        // ══════════════════════════════════════════════════════════
        //  ONGLET 1 : SYNTHÈSE
        // ══════════════════════════════════════════════════════════

        private static void BuildSynthese(XLWorkbook wb, RapportCEOData d)
        {
            var ws = wb.Worksheets.Add("Synthèse");
            ws.Style.Font.FontName = "Arial";
            ws.Style.Font.FontSize = 10;

            // Titre
            ws.Cell("A1").Value = $"RAPPORT EXÉCUTIF CEO — {d.EntrepriseNom}";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;
            ws.Range("A1:F1").Merge().Style.Font.FontColor = XLColor.FromHtml("#0d1b4a");

            ws.Cell("A2").Value = $"Période : {NomMois(d.Mois)} {d.Annee}";
            ws.Cell("A2").Style.Font.FontSize = 11;
            ws.Cell("A2").Style.Font.Italic = true;

            // KPIs en tableau
            int row = 4;
            var header = XLColor.FromHtml("#0d1b4a");
            ws.Cell(row, 1).Value = "Indicateur";
            ws.Cell(row, 2).Value = "Valeur";
            ws.Cell(row, 3).Value = "Détail";
            StyleHeader(ws.Range(row, 1, row, 3), header);

            row++;
            AddKpiRow(ws, ref row, "Effectif actif", d.EffectifActif, $"{d.NbHommes} H / {d.NbFemmes} F");
            AddKpiRow(ws, ref row, "Âge moyen", d.AgeMoyen, "ans");
            AddKpiRow(ws, ref row, "Ancienneté moyenne", d.AncienneteMoyenne, "ans");
            AddKpiRow(ws, ref row, "Masse salariale brute", d.MasseSalarialeBrute, $"Var. M-1 : {d.VariationMasseSalarialePct:+0.0;-0.0;0}%");
            AddKpiRow(ws, ref row, "Coût total employeur", d.CoutTotalEmployeur, "Brut + patronales");
            AddKpiRow(ws, ref row, "Salaire moyen brut", d.SalaireMoyenBrut, $"Médiane : {d.SalaireMedianBrut:N0}");
            AddKpiRow(ws, ref row, "Salaire moyen net", d.SalaireMoyenNet, "");
            AddKpiRow(ws, ref row, "Turnover mensuel", d.TauxTurnover, $"{d.Entrees} entrées / {d.Sorties} sorties");
            AddKpiRow(ws, ref row, "Turnover annuel", d.TauxTurnoverAnnuel, $"Cumul {d.Annee}");
            AddKpiRow(ws, ref row, "Taux absentéisme", d.TauxAbsenteisme, $"{d.JoursAbsenceTotalMois} jours");
            AddKpiRow(ws, ref row, "CDI", d.NbCDI, "");
            AddKpiRow(ws, ref row, "CDD", d.NbCDD, d.CDDExpirantSous30Jours > 0 ? $"{d.CDDExpirantSous30Jours} expirent sous 30j" : "");
            AddKpiRow(ws, ref row, "Stages", d.NbStage, "");
            AddKpiRow(ws, ref row, "Congés en attente", d.CongesEnAttente, "");

            ws.Columns().AdjustToContents();
            ws.Column(2).Width = 20;
        }

        // ══════════════════════════════════════════════════════════
        //  ONGLET 2 : EFFECTIFS
        // ══════════════════════════════════════════════════════════

        private static void BuildEffectifs(XLWorkbook wb, RapportCEOData d)
        {
            var ws = wb.Worksheets.Add("Effectifs");
            ws.Style.Font.FontName = "Arial";
            var header = XLColor.FromHtml("#0d1b4a");
            int row = 1;

            // Par département
            ws.Cell(row, 1).Value = "Répartition par département";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;

            ws.Cell(row, 1).Value = "Département";
            ws.Cell(row, 2).Value = "Effectif";
            ws.Cell(row, 3).Value = "%";
            ws.Cell(row, 4).Value = "Masse salariale";
            StyleHeader(ws.Range(row, 1, row, 4), header);
            row++;
            int dataStartDept = row;

            foreach (var dep in d.ParDepartement)
            {
                ws.Cell(row, 1).Value = dep.Libelle;
                ws.Cell(row, 2).Value = dep.Effectif;
                ws.Cell(row, 3).Value = dep.Pourcentage / 100.0;
                ws.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
                ws.Cell(row, 4).Value = dep.MasseSalariale;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0";
                row++;
            }
            int dataEndDept = row - 1;

            row += 2;

            // Par type contrat
            ws.Cell(row, 1).Value = "Répartition par type de contrat";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;
            ws.Cell(row, 1).Value = "Type";
            ws.Cell(row, 2).Value = "Effectif";
            ws.Cell(row, 3).Value = "%";
            StyleHeader(ws.Range(row, 1, row, 3), header);
            row++;

            foreach (var c in d.ParTypeContrat)
            {
                ws.Cell(row, 1).Value = c.Libelle;
                ws.Cell(row, 2).Value = c.Effectif;
                ws.Cell(row, 3).Value = c.Pourcentage / 100.0;
                ws.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
                row++;
            }

            row += 2;

            // Par tranche d'âge
            ws.Cell(row, 1).Value = "Répartition par tranche d'âge";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;
            ws.Cell(row, 1).Value = "Tranche";
            ws.Cell(row, 2).Value = "Effectif";
            ws.Cell(row, 3).Value = "%";
            StyleHeader(ws.Range(row, 1, row, 3), header);
            row++;

            foreach (var a in d.ParTrancheAge)
            {
                ws.Cell(row, 1).Value = a.Libelle;
                ws.Cell(row, 2).Value = a.Effectif;
                ws.Cell(row, 3).Value = a.Pourcentage / 100.0;
                ws.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
                row++;
            }

            // Par ancienneté
            row += 2;
            ws.Cell(row, 1).Value = "Répartition par tranche d'ancienneté";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;
            ws.Cell(row, 1).Value = "Tranche";
            ws.Cell(row, 2).Value = "Effectif";
            ws.Cell(row, 3).Value = "%";
            StyleHeader(ws.Range(row, 1, row, 3), header);
            row++;

            foreach (var a in d.ParTrancheAnciennete)
            {
                ws.Cell(row, 1).Value = a.Libelle;
                ws.Cell(row, 2).Value = a.Effectif;
                ws.Cell(row, 3).Value = a.Pourcentage / 100.0;
                ws.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        // ══════════════════════════════════════════════════════════
        //  ONGLET 3 : MASSE SALARIALE
        // ══════════════════════════════════════════════════════════

        private static void BuildMasseSalariale(XLWorkbook wb, RapportCEOData d)
        {
            var ws = wb.Worksheets.Add("Masse Salariale");
            ws.Style.Font.FontName = "Arial";
            var header = XLColor.FromHtml("#0d1b4a");
            int row = 1;

            ws.Cell(row, 1).Value = "Décomposition masse salariale";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;

            ws.Cell(row, 1).Value = "Composante";
            ws.Cell(row, 2).Value = "Montant (FCFA)";
            StyleHeader(ws.Range(row, 1, row, 2), header);
            row++;

            ws.Cell(row, 1).Value = "Total Gains (brut)";
            ws.Cell(row, 2).Value = d.MasseSalarialeBrute;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row++;
            ws.Cell(row, 1).Value = "Cotisations salariales";
            ws.Cell(row, 2).Value = d.TotalCotisationsSalariales;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row++;
            ws.Cell(row, 1).Value = "Retenues fiscales";
            ws.Cell(row, 2).Value = d.TotalRetenusFiscales;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row++;
            ws.Cell(row, 1).Value = "Net à payer";
            ws.Cell(row, 2).Value = d.MasseSalarialeNette;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 2).Style.Font.Bold = true;
            row++;
            ws.Cell(row, 1).Value = "Cotisations patronales";
            ws.Cell(row, 2).Value = d.TotalCotisationsPatronales;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row++;
            ws.Cell(row, 1).Value = "Coût total employeur";
            ws.Cell(row, 2).Value = d.CoutTotalEmployeur;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 2).Style.Font.Bold = true;

            // Évolution 12 mois
            row += 3;
            ws.Cell(row, 1).Value = "Évolution mensuelle (12 mois)";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;

            ws.Cell(row, 1).Value = "Période";
            ws.Cell(row, 2).Value = "Masse salariale brute";
            ws.Cell(row, 3).Value = "Effectif";
            StyleHeader(ws.Range(row, 1, row, 3), header);
            row++;

            foreach (var e in d.EvolutionMasseSalariale)
            {
                ws.Cell(row, 1).Value = e.Periode;
                ws.Cell(row, 2).Value = e.MasseSalarialeBrute;
                ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 3).Value = e.Effectif;
                row++;
            }

            // Top salaires par département
            row += 2;
            ws.Cell(row, 1).Value = "Salaire moyen par département";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;

            ws.Cell(row, 1).Value = "Département";
            ws.Cell(row, 2).Value = "Effectif";
            ws.Cell(row, 3).Value = "Salaire moyen brut";
            StyleHeader(ws.Range(row, 1, row, 3), header);
            row++;

            foreach (var t in d.TopSalaires)
            {
                ws.Cell(row, 1).Value = t.Departement;
                ws.Cell(row, 2).Value = t.Effectif;
                ws.Cell(row, 3).Value = t.SalaireMoyenBrut;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        // ══════════════════════════════════════════════════════════
        //  ONGLET 4 : CONGÉS
        // ══════════════════════════════════════════════════════════

        private static void BuildConges(XLWorkbook wb, RapportCEOData d)
        {
            var ws = wb.Worksheets.Add("Congés");
            ws.Style.Font.FontName = "Arial";
            var header = XLColor.FromHtml("#0d1b4a");
            int row = 1;

            ws.Cell(row, 1).Value = $"Congés & absences — {NomMois(d.Mois)} {d.Annee}";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;

            // KPIs
            ws.Cell(row, 1).Value = "Indicateur";
            ws.Cell(row, 2).Value = "Valeur";
            StyleHeader(ws.Range(row, 1, row, 2), header);
            row++;
            ws.Cell(row, 1).Value = "Congés en cours"; ws.Cell(row, 2).Value = d.CongesEnCours; row++;
            ws.Cell(row, 1).Value = "Accordés ce mois"; ws.Cell(row, 2).Value = d.CongesAccordesMois; row++;
            ws.Cell(row, 1).Value = "Refusés ce mois"; ws.Cell(row, 2).Value = d.CongesRefusesMois; row++;
            ws.Cell(row, 1).Value = "En attente"; ws.Cell(row, 2).Value = d.CongesEnAttente; row++;
            ws.Cell(row, 1).Value = "Jours d'absence (mois)"; ws.Cell(row, 2).Value = d.JoursAbsenceTotalMois; row++;
            ws.Cell(row, 1).Value = "Taux absentéisme"; ws.Cell(row, 2).Value = d.TauxAbsenteisme / 100.0;
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.0%"; row++;

            // Par type
            row += 2;
            ws.Cell(row, 1).Value = "Répartition par type de congé";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;

            ws.Cell(row, 1).Value = "Type";
            ws.Cell(row, 2).Value = "Demandes";
            ws.Cell(row, 3).Value = "Jours";
            ws.Cell(row, 4).Value = "%";
            StyleHeader(ws.Range(row, 1, row, 4), header);
            row++;

            foreach (var c in d.RepartitionCongesParType)
            {
                ws.Cell(row, 1).Value = c.TypeConge;
                ws.Cell(row, 2).Value = c.NbDemandes;
                ws.Cell(row, 3).Value = c.JoursTotaux;
                ws.Cell(row, 3).Style.NumberFormat.Format = "0.0";
                ws.Cell(row, 4).Value = c.Pourcentage / 100.0;
                ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        // ══════════════════════════════════════════════════════════
        //  ONGLET 5 : TURNOVER
        // ══════════════════════════════════════════════════════════

        private static void BuildTurnover(XLWorkbook wb, RapportCEOData d)
        {
            var ws = wb.Worksheets.Add("Turnover");
            ws.Style.Font.FontName = "Arial";
            var header = XLColor.FromHtml("#0d1b4a");
            int row = 1;

            ws.Cell(row, 1).Value = $"Turnover — {NomMois(d.Mois)} {d.Annee}";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;

            ws.Cell(row, 1).Value = "Indicateur";
            ws.Cell(row, 2).Value = "Valeur";
            StyleHeader(ws.Range(row, 1, row, 2), header);
            row++;
            ws.Cell(row, 1).Value = "Entrées du mois"; ws.Cell(row, 2).Value = d.Entrees; row++;
            ws.Cell(row, 1).Value = "  dont CDI"; ws.Cell(row, 2).Value = d.EntreesCDI; row++;
            ws.Cell(row, 1).Value = "  dont CDD"; ws.Cell(row, 2).Value = d.EntreesCDD; row++;
            ws.Cell(row, 1).Value = "  dont Stage"; ws.Cell(row, 2).Value = d.EntreesStage; row++;
            ws.Cell(row, 1).Value = "Sorties du mois"; ws.Cell(row, 2).Value = d.Sorties; row++;
            ws.Cell(row, 1).Value = "Taux turnover mensuel"; ws.Cell(row, 2).Value = d.TauxTurnover / 100.0;
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.0%"; row++;
            ws.Cell(row, 1).Value = "Taux turnover annuel"; ws.Cell(row, 2).Value = d.TauxTurnoverAnnuel / 100.0;
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.0%"; row++;

            // Évolution 12 mois
            row += 2;
            ws.Cell(row, 1).Value = "Évolution des mouvements (12 mois)";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;

            ws.Cell(row, 1).Value = "Période";
            ws.Cell(row, 2).Value = "Effectif";
            ws.Cell(row, 3).Value = "Entrées";
            ws.Cell(row, 4).Value = "Sorties";
            StyleHeader(ws.Range(row, 1, row, 4), header);
            row++;

            foreach (var e in d.EvolutionEffectif)
            {
                ws.Cell(row, 1).Value = e.Periode;
                ws.Cell(row, 2).Value = e.Effectif;
                ws.Cell(row, 3).Value = e.Entrees;
                ws.Cell(row, 4).Value = e.Sorties;
                row++;
            }

            // Motifs de départ
            if (d.RepartitionMotifDepart.Count > 0)
            {
                row += 2;
                ws.Cell(row, 1).Value = "Motifs de départ";
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 12;
                row += 2;

                ws.Cell(row, 1).Value = "Motif";
                ws.Cell(row, 2).Value = "Nombre";
                ws.Cell(row, 3).Value = "%";
                StyleHeader(ws.Range(row, 1, row, 3), header);
                row++;

                foreach (var m in d.RepartitionMotifDepart)
                {
                    ws.Cell(row, 1).Value = m.Motif;
                    ws.Cell(row, 2).Value = m.Nombre;
                    ws.Cell(row, 3).Value = m.Pourcentage / 100.0;
                    ws.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
                    row++;
                }
            }

            ws.Columns().AdjustToContents();
        }

        // ══════════════════════════════════════════════════════════
        //  ONGLET 6 : ALERTES
        // ══════════════════════════════════════════════════════════

        private static void BuildAlertes(XLWorkbook wb, RapportCEOData d)
        {
            var ws = wb.Worksheets.Add("Alertes");
            ws.Style.Font.FontName = "Arial";
            var header = XLColor.FromHtml("#0d1b4a");
            int row = 1;

            ws.Cell(row, 1).Value = "Alertes & points d'attention";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row += 2;

            ws.Cell(row, 1).Value = "Niveau";
            ws.Cell(row, 2).Value = "Titre";
            ws.Cell(row, 3).Value = "Description";
            StyleHeader(ws.Range(row, 1, row, 3), header);
            row++;

            if (d.Alertes.Count == 0)
            {
                ws.Cell(row, 1).Value = "Info";
                ws.Cell(row, 2).Value = "Aucune alerte";
                ws.Cell(row, 3).Value = "Tous les indicateurs sont dans les seuils normaux.";
                row++;
            }
            else
            {
                foreach (var a in d.Alertes)
                {
                    ws.Cell(row, 1).Value = a.Niveau;
                    if (a.Niveau == "Danger")
                    {
                        ws.Cell(row, 1).Style.Font.FontColor = XLColor.Red;
                        ws.Cell(row, 1).Style.Font.Bold = true;
                    }
                    else if (a.Niveau == "Warning")
                    {
                        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#e76f51");
                        ws.Cell(row, 1).Style.Font.Bold = true;
                    }
                    ws.Cell(row, 2).Value = a.Titre;
                    ws.Cell(row, 3).Value = a.Description;
                    row++;
                }
            }

            ws.Column(3).Width = 60;
            ws.Columns(1, 2).AdjustToContents();
        }

        // ══════════════════════════════════════════════════════════
        //  INJECTION GRAPHIQUES OPENXML
        // ══════════════════════════════════════════════════════════

        private static byte[] InjecterGraphiques(byte[] xlsxBytes, RapportCEOData d)
        {
            // ── Graphiques onglet Effectifs ───────────────────────
            var chartsEffectifs = new System.Collections.Generic.List<ChartDef>();

            if (d.ParDepartement.Count > 0)
            {
                chartsEffectifs.Add(new ChartDef
                {
                    Type = ChartType.Pie,
                    Title = "Répartition par département",
                    CategoryRange = $"A4:A{3 + d.ParDepartement.Count}",
                    ValueRanges = new[] { $"B4:B{3 + d.ParDepartement.Count}" },
                    SeriesNames = new[] { "Effectif" },
                    AnchorRow = 2, AnchorCol = 5, Width = 10, Height = 14,
                    CategoryLabels = d.ParDepartement.Select(x => x.Libelle).ToArray(),
                    SeriesValues = new[] { d.ParDepartement.Select(x => (double)x.Effectif).ToArray() }
                });
            }

            if (d.ParTypeContrat.Count > 0)
            {
                int offsetContrat = 4 + d.ParDepartement.Count + 4;
                chartsEffectifs.Add(new ChartDef
                {
                    Type = ChartType.Pie,
                    Title = "Répartition par type de contrat",
                    CategoryRange = $"A{offsetContrat}:A{offsetContrat + d.ParTypeContrat.Count - 1}",
                    ValueRanges = new[] { $"B{offsetContrat}:B{offsetContrat + d.ParTypeContrat.Count - 1}" },
                    SeriesNames = new[] { "Effectif" },
                    AnchorRow = 17, AnchorCol = 5, Width = 10, Height = 12,
                    CategoryLabels = d.ParTypeContrat.Select(x => x.Libelle).ToArray(),
                    SeriesValues = new[] { d.ParTypeContrat.Select(x => (double)x.Effectif).ToArray() }
                });
            }

            if (chartsEffectifs.Count > 0)
                xlsxBytes = ExcelChartInjector.InjectCharts(xlsxBytes, "Effectifs", "Effectifs", chartsEffectifs);

            // ── Graphique onglet Masse Salariale ──────────────────
            if (d.EvolutionMasseSalariale.Count > 0)
            {
                int evoStart = 13;
                var chartsMasse = new System.Collections.Generic.List<ChartDef>
                {
                    new ChartDef
                    {
                        Type = ChartType.ColumnBar,
                        Title = "Évolution masse salariale (12 mois)",
                        CategoryRange = $"A{evoStart}:A{evoStart + d.EvolutionMasseSalariale.Count - 1}",
                        ValueRanges = new[] { $"B{evoStart}:B{evoStart + d.EvolutionMasseSalariale.Count - 1}" },
                        SeriesNames = new[] { "Masse salariale brute" },
                        AnchorRow = 10, AnchorCol = 4, Width = 12, Height = 15,
                        CategoryLabels = d.EvolutionMasseSalariale.Select(x => x.Periode).ToArray(),
                        SeriesValues = new[] { d.EvolutionMasseSalariale.Select(x => (double)x.MasseSalarialeBrute).ToArray() }
                    }
                };
                xlsxBytes = ExcelChartInjector.InjectCharts(xlsxBytes, "Masse Salariale", "Masse Salariale", chartsMasse);
            }

            // ── Graphique onglet Turnover ─────────────────────────
            if (d.EvolutionEffectif.Count > 0)
            {
                int evoTurnStart = 15;
                var chartsTurnover = new System.Collections.Generic.List<ChartDef>
                {
                    new ChartDef
                    {
                        Type = ChartType.ColumnBar,
                        Title = "Évolution de l'effectif (12 mois)",
                        CategoryRange = $"A{evoTurnStart}:A{evoTurnStart + d.EvolutionEffectif.Count - 1}",
                        ValueRanges = new[] { $"B{evoTurnStart}:B{evoTurnStart + d.EvolutionEffectif.Count - 1}" },
                        SeriesNames = new[] { "Effectif" },
                        AnchorRow = 13, AnchorCol = 5, Width = 12, Height = 15,
                        CategoryLabels = d.EvolutionEffectif.Select(x => x.Periode).ToArray(),
                        SeriesValues = new[] { d.EvolutionEffectif.Select(x => (double)x.Effectif).ToArray() }
                    }
                };
                xlsxBytes = ExcelChartInjector.InjectCharts(xlsxBytes, "Turnover", "Turnover", chartsTurnover);
            }

            return xlsxBytes;
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════

        private static void StyleHeader(IXLRange range, XLColor bg)
        {
            range.Style.Fill.BackgroundColor = bg;
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Font.Bold = true;
            range.Style.Font.FontSize = 9;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private static void AddKpiRow(IXLWorksheet ws, ref int row, string label, object value, string detail)
        {
            ws.Cell(row, 1).Value = label;
            if (value is decimal dec)
            {
                ws.Cell(row, 2).Value = dec;
                ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            }
            else if (value is double dbl)
            {
                ws.Cell(row, 2).Value = dbl;
                ws.Cell(row, 2).Style.NumberFormat.Format = "0.0";
            }
            else if (value is int intVal)
            {
                ws.Cell(row, 2).Value = intVal;
            }
            else
            {
                ws.Cell(row, 2).Value = value?.ToString() ?? "";
            }
            ws.Cell(row, 3).Value = detail;
            ws.Cell(row, 3).Style.Font.FontColor = XLColor.Gray;
            row++;
        }

        private static string NomMois(int mois) => mois switch
        {
            1 => "Janvier", 2 => "Février", 3 => "Mars", 4 => "Avril",
            5 => "Mai", 6 => "Juin", 7 => "Juillet", 8 => "Août",
            9 => "Septembre", 10 => "Octobre", 11 => "Novembre", 12 => "Décembre",
            _ => mois.ToString()
        };
    }
}
