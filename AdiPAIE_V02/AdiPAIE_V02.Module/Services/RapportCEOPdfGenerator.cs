// AdiPAIE_V02.Module/Services/RapportCEOPdfGenerator.cs
// Génération d'un rapport PDF exécutif (4-6 pages, style McKinsey)
// via HTML/CSS → LibreOffice headless → PDF.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AdiPAIE_V02.Module.Services
{
    public static class RapportCEOPdfGenerator
    {
        /// <summary>
        /// Génère le rapport PDF et retourne les octets du fichier.
        /// </summary>
        public static byte[] Generer(RapportCEOData d)
        {
            string html = BuildHtml(d);
            return ConvertirEnPdf(html, $"Rapport_CEO_{d.Annee}_{d.Mois:D2}");
        }

        // ══════════════════════════════════════════════════════════
        //  CONVERSION HTML → PDF (LibreOffice headless)
        // ══════════════════════════════════════════════════════════

        private static byte[] ConvertirEnPdf(string html, string baseName)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "adip_ceo_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);

            var htmlPath = Path.Combine(tempDir, baseName + ".html");
            File.WriteAllText(htmlPath, html, Encoding.UTF8);

            try
            {
                // Résolution du chemin LibreOffice (Windows + Linux)
                var chemins = new[]
                {
                    @"C:\Program Files\LibreOffice\program\soffice.exe",
                    @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
                    "/usr/bin/soffice",
                    "soffice"
                };
                var soffice = chemins.FirstOrDefault(File.Exists) ?? "soffice";

                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = soffice,
                        Arguments = $"--headless --convert-to pdf --outdir \"{tempDir}\" \"{htmlPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                if (!proc.WaitForExit(30_000)) { proc.Kill(); throw new Exception("Timeout LibreOffice (30s)."); }

                var pdfPath = Path.Combine(tempDir, baseName + ".pdf");
                if (!File.Exists(pdfPath))
                    throw new Exception("Le fichier PDF n'a pas été généré par LibreOffice. "
                        + "Vérifiez que LibreOffice est installé.");

                return File.ReadAllBytes(pdfPath);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { /* nettoyage best-effort */ }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CONSTRUCTION HTML
        // ══════════════════════════════════════════════════════════

        private static string BuildHtml(RapportCEOData d)
        {
            var sb = new StringBuilder(16_000);
            sb.Append(HtmlHead(d));
            sb.Append(PageCouverture(d));
            sb.Append(PageKPIs(d));
            sb.Append(PageMasseSalariale(d));
            sb.Append(PageEffectifRepartitions(d));
            sb.Append(PageCongesEtTurnover(d));
            sb.Append(PageAlertes(d));
            sb.Append("</body></html>");
            return sb.ToString();
        }

        // ── HEAD + CSS ───────────────────────────────────────────

        private static string HtmlHead(RapportCEOData d)
        {
            return @"<!DOCTYPE html>
<html lang=""fr"">
<head>
<meta charset=""UTF-8"">
<style>
@page { size: A4; margin: 15mm 18mm 20mm 18mm; }
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: 'Segoe UI', Arial, Helvetica, sans-serif; font-size: 10pt; color: #1a1a2e; line-height: 1.45; }
.page { page-break-after: always; min-height: 247mm; position: relative; padding: 0; }
.page:last-child { page-break-after: avoid; }

/* ── Couleurs ── */
:root {
    --primary: #0d1b4a;
    --accent: #1e6091;
    --accent2: #168aad;
    --success: #2d6a4f;
    --warning: #e76f51;
    --danger: #d62828;
    --light-bg: #f8f9fa;
    --border: #dee2e6;
}

/* ── Page de couverture ── */
.cover { display: flex; flex-direction: column; justify-content: center; align-items: center; text-align: center; min-height: 247mm; background: linear-gradient(160deg, #0d1b4a 0%, #1e6091 60%, #168aad 100%); color: white; }
.cover-logo { max-height: 80px; margin-bottom: 30px; }
.cover h1 { font-size: 28pt; font-weight: 700; letter-spacing: 1px; margin-bottom: 10px; }
.cover h2 { font-size: 16pt; font-weight: 300; opacity: 0.9; margin-bottom: 5px; }
.cover .period { font-size: 20pt; font-weight: 600; margin-top: 25px; padding: 10px 40px; border: 2px solid rgba(255,255,255,0.5); border-radius: 6px; }
.cover .date-gen { margin-top: 40px; font-size: 9pt; opacity: 0.7; }

/* ── Section titles ── */
.section-title { font-size: 14pt; font-weight: 700; color: var(--primary); border-bottom: 3px solid var(--accent); padding-bottom: 6px; margin: 18px 0 14px 0; }
.section-subtitle { font-size: 11pt; font-weight: 600; color: var(--accent); margin: 12px 0 8px 0; }

/* ── KPI cards ── */
.kpi-grid { display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 14px; }
.kpi-card { flex: 1 1 22%; min-width: 120px; background: var(--light-bg); border-radius: 6px; padding: 12px 14px; border-left: 4px solid var(--accent); }
.kpi-card.success { border-left-color: var(--success); }
.kpi-card.warning { border-left-color: var(--warning); }
.kpi-card.danger { border-left-color: var(--danger); }
.kpi-label { font-size: 8pt; color: #666; text-transform: uppercase; letter-spacing: 0.5px; }
.kpi-value { font-size: 18pt; font-weight: 700; color: var(--primary); margin-top: 2px; }
.kpi-sub { font-size: 8pt; color: #888; margin-top: 2px; }

/* ── Tables ── */
table { width: 100%; border-collapse: collapse; font-size: 9pt; margin-bottom: 12px; }
th { background: var(--primary); color: white; padding: 7px 10px; text-align: left; font-weight: 600; font-size: 8pt; text-transform: uppercase; letter-spacing: 0.3px; }
td { padding: 6px 10px; border-bottom: 1px solid var(--border); }
tr:nth-child(even) td { background: #f8f9fb; }
.text-right { text-align: right; }
.text-center { text-align: center; }
.num { font-variant-numeric: tabular-nums; }

/* ── Progress bars ── */
.bar-container { background: #e9ecef; border-radius: 3px; height: 14px; position: relative; }
.bar-fill { height: 100%; border-radius: 3px; background: var(--accent); min-width: 2px; }
.bar-label { position: absolute; right: 4px; top: 0; line-height: 14px; font-size: 7pt; color: #333; }

/* ── Alertes ── */
.alerte { padding: 10px 14px; border-radius: 5px; margin-bottom: 8px; border-left: 4px solid; }
.alerte.info { background: #e8f4fd; border-left-color: var(--accent2); }
.alerte.warning { background: #fff3e0; border-left-color: var(--warning); }
.alerte.danger { background: #fde8e8; border-left-color: var(--danger); }
.alerte-titre { font-weight: 700; font-size: 10pt; margin-bottom: 3px; }
.alerte-desc { font-size: 9pt; color: #444; }

/* ── Footer ── */
.page-footer { position: absolute; bottom: 0; left: 0; right: 0; text-align: center; font-size: 7pt; color: #aaa; padding: 5px 0; border-top: 1px solid #eee; }

/* ── Variation indicators ── */
.var-up { color: var(--danger); }
.var-down { color: var(--success); }
.var-neutral { color: #888; }
</style>
</head>
<body>";
        }

        // ── PAGE 1 : COUVERTURE ──────────────────────────────────

        private static string PageCouverture(RapportCEOData d)
        {
            var logoTag = "";
            if (d.LogoImage != null && d.LogoImage.Length > 0)
            {
                var b64 = Convert.ToBase64String(d.LogoImage);
                logoTag = $"<img src=\"data:image/png;base64,{b64}\" class=\"cover-logo\" alt=\"Logo\">";
            }

            return $@"
<div class=""page cover"">
    {logoTag}
    <h1>RAPPORT EXÉCUTIF</h1>
    <h2>{Esc(d.EntrepriseNom)}</h2>
    <div class=""period"">{NomMois(d.Mois)} {d.Annee}</div>
    <div class=""date-gen"">Généré le {d.DateGeneration:dd/MM/yyyy à HH:mm} — Document confidentiel</div>
</div>";
        }

        // ── PAGE 2 : KPIs SYNTHÈSE ──────────────────────────────

        private static string PageKPIs(RapportCEOData d)
        {
            var varIcon = d.VariationMasseSalarialePct > 0 ? "▲" : d.VariationMasseSalarialePct < 0 ? "▼" : "●";
            var varClass = d.VariationMasseSalarialePct > 5 ? "var-up" : d.VariationMasseSalarialePct < -5 ? "var-down" : "var-neutral";

            return $@"
<div class=""page"">
    <div class=""section-title"">1. Synthèse des indicateurs clés</div>

    <div class=""kpi-grid"">
        {KpiCard("Effectif total", d.EffectifActif.ToString("N0"), $"{d.NbHommes} H / {d.NbFemmes} F")}
        {KpiCard("Âge moyen", $"{d.AgeMoyen:F1} ans", $"Ancienneté moy. {d.AncienneteMoyenne:F1} ans")}
        {KpiCard("Masse salariale brute", Fmt(d.MasseSalarialeBrute), $"<span class=\"{varClass}\">{varIcon} {d.VariationMasseSalarialePct:+0.0;-0.0;0}% vs M-1</span>")}
        {KpiCard("Coût total employeur", Fmt(d.CoutTotalEmployeur), "Brut + cotisations patronales")}
    </div>

    <div class=""kpi-grid"">
        {KpiCard("Salaire moyen brut", Fmt(d.SalaireMoyenBrut), $"Médiane : {Fmt(d.SalaireMedianBrut)}")}
        {KpiCard("Salaire moyen net", Fmt(d.SalaireMoyenNet), "")}
        {KpiCard("Turnover mensuel", $"{d.TauxTurnover:F1}%", $"{d.Entrees} entrée(s) / {d.Sorties} sortie(s)", d.TauxTurnover > d.SeuilTurnover ? "danger" : "")}
        {KpiCard("Absentéisme", $"{d.TauxAbsenteisme:F1}%", $"{d.JoursAbsenceTotalMois} jour(s) d'absence", d.TauxAbsenteisme > d.SeuilAbsenteisme ? "warning" : "")}
    </div>

    <div class=""kpi-grid"">
        {KpiCard("Contrats CDI", d.NbCDI.ToString(), "")}
        {KpiCard("Contrats CDD", d.NbCDD.ToString(), d.CDDExpirantSous30Jours > 0 ? $"⚠ {d.CDDExpirantSous30Jours} expire(nt) sous 30j" : "")}
        {KpiCard("Stages", d.NbStage.ToString(), "")}
        {KpiCard("Congés en attente", d.CongesEnAttente.ToString(), $"{d.CongesAccordesMois} accordés ce mois")}
    </div>

    <div class=""section-subtitle"">Évolution de l'effectif (12 derniers mois)</div>
    {TableEvolution(d)}

    <div class=""page-footer"">Rapport Exécutif CEO — {Esc(d.EntrepriseNom)} — {d.PeriodeLibelle} — Page 2</div>
</div>";
        }

        // ── PAGE 3 : MASSE SALARIALE ─────────────────────────────

        private static string PageMasseSalariale(RapportCEOData d)
        {
            var sb = new StringBuilder();
            sb.Append($@"
<div class=""page"">
    <div class=""section-title"">2. Masse salariale</div>

    <div class=""kpi-grid"">
        {KpiCard("Total Gains (brut)", Fmt(d.MasseSalarialeBrute), "")}
        {KpiCard("Cotisations salariales", Fmt(d.TotalCotisationsSalariales), "")}
        {KpiCard("Retenues fiscales", Fmt(d.TotalRetenusFiscales), "TRIMF + IR")}
        {KpiCard("Net à payer global", Fmt(d.MasseSalarialeNette), "", "success")}
    </div>

    <div class=""kpi-grid"">
        {KpiCard("Cotisations patronales", Fmt(d.TotalCotisationsPatronales), "IPRES + CSS")}
        {KpiCard("Coût total employeur", Fmt(d.CoutTotalEmployeur), "Brut + patronales")}
    </div>

    <div class=""section-subtitle"">Masse salariale par département</div>
    <table>
        <tr><th>Département</th><th class=""text-center"">Effectif</th><th class=""text-right"">Masse salariale</th><th class=""text-right"">%</th><th style=""width:120px"">Répartition</th></tr>");

            foreach (var dep in d.ParDepartement.Take(12))
            {
                sb.Append($@"
        <tr>
            <td>{Esc(dep.Libelle)}</td>
            <td class=""text-center num"">{dep.Effectif}</td>
            <td class=""text-right num"">{Fmt(dep.MasseSalariale)}</td>
            <td class=""text-right num"">{dep.Pourcentage:F1}%</td>
            <td><div class=""bar-container""><div class=""bar-fill"" style=""width:{dep.Pourcentage:F0}%""></div><div class=""bar-label"">{dep.Pourcentage:F0}%</div></div></td>
        </tr>");
            }

            sb.Append(@"
    </table>

    <div class=""section-subtitle"">Évolution de la masse salariale (12 derniers mois)</div>");
            sb.Append(TableEvolutionMasse(d));

            sb.Append($@"
    <div class=""page-footer"">Rapport Exécutif CEO — {Esc(d.EntrepriseNom)} — {d.PeriodeLibelle} — Page 3</div>
</div>");
            return sb.ToString();
        }

        // ── PAGE 4 : RÉPARTITIONS EFFECTIF ───────────────────────

        private static string PageEffectifRepartitions(RapportCEOData d)
        {
            var sb = new StringBuilder();
            sb.Append($@"
<div class=""page"">
    <div class=""section-title"">3. Répartitions de l'effectif</div>

    <div class=""section-subtitle"">Par type de contrat</div>
    {BuildRepartTable(d.ParTypeContrat)}

    <div class=""section-subtitle"">Par catégorie professionnelle</div>
    {BuildRepartTable(d.ParCategorie)}

    <div class=""section-subtitle"">Par tranche d'âge</div>
    {BuildRepartTable(d.ParTrancheAge)}

    <div class=""section-subtitle"">Par tranche d'ancienneté</div>
    {BuildRepartTable(d.ParTrancheAnciennete)}

    <div class=""page-footer"">Rapport Exécutif CEO — {Esc(d.EntrepriseNom)} — {d.PeriodeLibelle} — Page 4</div>
</div>");
            return sb.ToString();
        }

        // ── PAGE 5 : CONGÉS & TURNOVER ───────────────────────────

        private static string PageCongesEtTurnover(RapportCEOData d)
        {
            var sb = new StringBuilder();
            sb.Append($@"
<div class=""page"">
    <div class=""section-title"">4. Congés, absences & turnover</div>

    <div class=""kpi-grid"">
        {KpiCard("Congés en cours", d.CongesEnCours.ToString(), "")}
        {KpiCard("Accordés (mois)", d.CongesAccordesMois.ToString(), "")}
        {KpiCard("Refusés (mois)", d.CongesRefusesMois.ToString(), "")}
        {KpiCard("Taux absentéisme", $"{d.TauxAbsenteisme:F1}%", $"{d.JoursAbsenceTotalMois} jour(s)")}
    </div>

    <div class=""section-subtitle"">Répartition des congés par type</div>
    <table>
        <tr><th>Type de congé</th><th class=""text-center"">Demandes</th><th class=""text-right"">Jours</th><th class=""text-right"">%</th></tr>");

            foreach (var c in d.RepartitionCongesParType.Take(8))
            {
                sb.Append($@"
        <tr>
            <td>{Esc(c.TypeConge)}</td>
            <td class=""text-center num"">{c.NbDemandes}</td>
            <td class=""text-right num"">{c.JoursTotaux:F1}</td>
            <td class=""text-right num"">{c.Pourcentage:F1}%</td>
        </tr>");
            }

            sb.Append(@"</table>");

            // Turnover
            sb.Append($@"
    <div class=""section-subtitle"">Turnover</div>
    <div class=""kpi-grid"">
        {KpiCard("Taux mensuel", $"{d.TauxTurnover:F1}%", $"Seuil : {d.SeuilTurnover:F0}%", d.TauxTurnover > d.SeuilTurnover ? "danger" : "")}
        {KpiCard("Taux annuel cumulé", $"{d.TauxTurnoverAnnuel:F1}%", $"Depuis janvier {d.Annee}")}
        {KpiCard("Entrées du mois", d.Entrees.ToString(), $"CDI:{d.EntreesCDI} CDD:{d.EntreesCDD} Stage:{d.EntreesStage}")}
        {KpiCard("Sorties du mois", d.Sorties.ToString(), "")}
    </div>");

            if (d.RepartitionMotifDepart.Count > 0)
            {
                sb.Append(@"
    <div class=""section-subtitle"">Motifs de départ</div>
    <table>
        <tr><th>Motif</th><th class=""text-center"">Nombre</th><th class=""text-right"">%</th></tr>");
                foreach (var m in d.RepartitionMotifDepart)
                {
                    sb.Append($@"
        <tr><td>{Esc(m.Motif)}</td><td class=""text-center num"">{m.Nombre}</td><td class=""text-right num"">{m.Pourcentage:F1}%</td></tr>");
                }
                sb.Append(@"</table>");
            }

            sb.Append($@"
    <div class=""page-footer"">Rapport Exécutif CEO — {Esc(d.EntrepriseNom)} — {d.PeriodeLibelle} — Page 5</div>
</div>");
            return sb.ToString();
        }

        // ── PAGE 6 : ALERTES ─────────────────────────────────────

        private static string PageAlertes(RapportCEOData d)
        {
            var sb = new StringBuilder();
            sb.Append($@"
<div class=""page"">
    <div class=""section-title"">5. Alertes & points d'attention</div>");

            if (d.Alertes.Count == 0)
            {
                sb.Append(@"
    <div class=""alerte info"">
        <div class=""alerte-titre"">Aucune alerte</div>
        <div class=""alerte-desc"">Tous les indicateurs sont dans les seuils normaux.</div>
    </div>");
            }
            else
            {
                foreach (var a in d.Alertes)
                {
                    var cls = a.Niveau == "Danger" ? "danger" : a.Niveau == "Warning" ? "warning" : "info";
                    sb.Append($@"
    <div class=""alerte {cls}"">
        <div class=""alerte-titre"">{Esc(a.Titre)}</div>
        <div class=""alerte-desc"">{Esc(a.Description)}</div>
    </div>");
                }
            }

            // Top salaires par département
            sb.Append(@"
    <div class=""section-subtitle"">Salaire moyen brut par département (Top 10)</div>
    <table>
        <tr><th>Département</th><th class=""text-center"">Effectif</th><th class=""text-right"">Salaire moyen brut</th></tr>");

            foreach (var t in d.TopSalaires.Take(10))
            {
                sb.Append($@"
        <tr>
            <td>{Esc(t.Departement)}</td>
            <td class=""text-center num"">{t.Effectif}</td>
            <td class=""text-right num"">{Fmt(t.SalaireMoyenBrut)}</td>
        </tr>");
            }

            sb.Append($@"
    </table>

    <div style=""margin-top:30px;padding:14px;background:#f8f9fa;border-radius:6px;text-align:center;font-size:9pt;color:#666"">
        Ce rapport est généré automatiquement par SunuPaie / AdiPAIE.<br>
        Les données reflètent la situation au {d.DateGeneration:dd/MM/yyyy}. Document strictement confidentiel.
    </div>

    <div class=""page-footer"">Rapport Exécutif CEO — {Esc(d.EntrepriseNom)} — {d.PeriodeLibelle} — Page 6</div>
</div>");
            return sb.ToString();
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS HTML
        // ══════════════════════════════════════════════════════════

        private static string KpiCard(string label, string value, string sub, string cssClass = "")
        {
            var cls = string.IsNullOrEmpty(cssClass) ? "" : $" {cssClass}";
            return $@"<div class=""kpi-card{cls}""><div class=""kpi-label"">{Esc(label)}</div><div class=""kpi-value"">{value}</div><div class=""kpi-sub"">{sub}</div></div>";
        }

        private static string BuildRepartTable(System.Collections.Generic.List<RepartitionItem> items)
        {
            if (items == null || items.Count == 0)
                return "<p style=\"color:#888;font-size:9pt\">Aucune donnée.</p>";

            var sb = new StringBuilder();
            sb.Append(@"<table><tr><th>Libellé</th><th class=""text-center"">Effectif</th><th class=""text-right"">%</th><th style=""width:140px"">Répartition</th></tr>");

            foreach (var item in items)
            {
                sb.Append($@"<tr><td>{Esc(item.Libelle)}</td><td class=""text-center num"">{item.Effectif}</td><td class=""text-right num"">{item.Pourcentage:F1}%</td><td><div class=""bar-container""><div class=""bar-fill"" style=""width:{item.Pourcentage:F0}%""></div><div class=""bar-label"">{item.Pourcentage:F0}%</div></div></td></tr>");
            }

            sb.Append("</table>");
            return sb.ToString();
        }

        private static string TableEvolution(RapportCEOData d)
        {
            if (d.EvolutionEffectif.Count == 0) return "";
            var sb = new StringBuilder();
            sb.Append(@"<table><tr><th>Période</th><th class=""text-center"">Effectif</th><th class=""text-center"">Entrées</th><th class=""text-center"">Sorties</th></tr>");

            foreach (var e in d.EvolutionEffectif)
            {
                sb.Append($@"<tr><td>{e.Periode}</td><td class=""text-center num"">{e.Effectif}</td><td class=""text-center num"">{e.Entrees}</td><td class=""text-center num"">{e.Sorties}</td></tr>");
            }
            sb.Append("</table>");
            return sb.ToString();
        }

        private static string TableEvolutionMasse(RapportCEOData d)
        {
            if (d.EvolutionMasseSalariale.Count == 0) return "";
            var sb = new StringBuilder();
            sb.Append(@"<table><tr><th>Période</th><th class=""text-right"">Masse salariale brute</th></tr>");

            foreach (var e in d.EvolutionMasseSalariale)
            {
                sb.Append($@"<tr><td>{e.Periode}</td><td class=""text-right num"">{Fmt(e.MasseSalarialeBrute)}</td></tr>");
            }
            sb.Append("</table>");
            return sb.ToString();
        }

        private static string Fmt(decimal val) => $"{val:N0} FCFA";
        private static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");

        private static string NomMois(int mois) => mois switch
        {
            1 => "Janvier", 2 => "Février", 3 => "Mars", 4 => "Avril",
            5 => "Mai", 6 => "Juin", 7 => "Juillet", 8 => "Août",
            9 => "Septembre", 10 => "Octobre", 11 => "Novembre", 12 => "Décembre",
            _ => mois.ToString()
        };
    }
}
