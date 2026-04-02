using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Génère les déclarations IPRES et CSS à partir des bulletins d'une période.
    ///
    /// Retourne les bytes (PDF ou XLSX) prêts à télécharger via JSInterop.
    ///
    /// Sources de données (BulletinLigne.Montant / MontantEmployeur) :
    ///   IPRES_RG  → Taux1=5.60% sal / Taux2=8.40% pat / Plafond 432 000
    ///   IPRES_RC  → Taux1=2.40% sal / Taux2=3.60% pat / Plafond 1 296 000 (cadres)
    ///   CSS_AT    → Taux2=3.00% pat / Plafond 63 000
    ///   CSS_AF    → Taux2=7.00% pat / Plafond 63 000
    /// </summary>
    public static class DeclarationService
    {
        private static readonly CultureInfo Fr = new("fr-FR");

        // ═══════════════════════════════════════════════════════════════
        // MODÈLE DE DONNÉES
        // ═══════════════════════════════════════════════════════════════

        public record LigneDeclaration(
            string Matricule,
            string NumeroIPRES,
            string NomPrenom,
            decimal BrutSocial,
            // IPRES RG
            decimal BaseRG,
            decimal RG_Sal,
            decimal RG_Pat,
            // IPRES RC
            decimal BaseRC,
            decimal RC_Sal,
            decimal RC_Pat,
            // CSS
            decimal BaseCSS,
            decimal CSS_AT,
            decimal CSS_AF
        )
        {
            public decimal TotalIPRES_Sal => RG_Sal + RC_Sal;
            public decimal TotalIPRES_Pat => RG_Pat + RC_Pat;
            public decimal TotalIPRES => TotalIPRES_Sal + TotalIPRES_Pat;
            public decimal TotalCSS => CSS_AT + CSS_AF;
        }

        // ═══════════════════════════════════════════════════════════════
        // EXTRACTION DES DONNÉES
        // ═══════════════════════════════════════════════════════════════

        public static List<LigneDeclaration> ExtraireLignes(
            IObjectSpace os, int annee, int mois)
        {
            var bulletins = new XPQuery<Bulletin>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                .Where(b => b.Annee == annee && b.Mois == mois
                         && b.Statut != BulletinStatut.Brouillon)
                .OrderBy(b => b.Salarie.LastName)
                .ToList();

            var lignes = new List<LigneDeclaration>();

            foreach (var b in bulletins)
            {
                decimal Get(RubriqueCanonique rc, bool patron = false)
                {
                    var l = b.Lignes.FirstOrDefault(x => x.Rubrique?.Canonique == rc);
                    return l == null ? 0m : (patron ? l.MontantEmployeur : l.Montant);
                }
                decimal GetBase(RubriqueCanonique rc)
                {
                    var l = b.Lignes.FirstOrDefault(x => x.Rubrique?.Canonique == rc);
                    return l?.Base ?? 0m;
                }

                lignes.Add(new LigneDeclaration(
                    Matricule: b.Salarie?.Matricule ?? "—",
                    NumeroIPRES: b.Salarie?.NumeroIPRESS ?? "—",
                    NomPrenom: b.Salarie?.FullName ?? "—",
                    BrutSocial: b.BrutSocial,
                    BaseRG: GetBase(RubriqueCanonique.IPRES_RG),
                    RG_Sal: Get(RubriqueCanonique.IPRES_RG),
                    RG_Pat: Get(RubriqueCanonique.IPRES_RG, true),
                    BaseRC: GetBase(RubriqueCanonique.IPRES_RC),
                    RC_Sal: Get(RubriqueCanonique.IPRES_RC),
                    RC_Pat: Get(RubriqueCanonique.IPRES_RC, true),
                    BaseCSS: GetBase(RubriqueCanonique.CSS_AccidentTravail),
                    CSS_AT: Get(RubriqueCanonique.CSS_AccidentTravail, true),
                    CSS_AF: Get(RubriqueCanonique.CSS_AllocationFamiliale, true)
                ));
            }

            return lignes;
        }

        // ═══════════════════════════════════════════════════════════════
        // HTML COMMUN
        // ═══════════════════════════════════════════════════════════════

        private static string BuildHtmlIPRES(
            List<LigneDeclaration> lignes,
            Company company,
            int annee, int mois)
        {
            var rs = company?.RaisonSociale ?? "Société";
            var periode = new DateTime(annee, mois, 1).ToString("MMMM yyyy", Fr);
            var sb = new StringBuilder();

            sb.Append($@"<!DOCTYPE html>
<html lang='fr'><head><meta charset='UTF-8'/>
<style>
* {{ box-sizing:border-box; margin:0; padding:0; }}
body {{ font-family:Arial,sans-serif; font-size:9pt; color:#111; padding:15mm 12mm; }}
.header {{ display:flex; justify-content:space-between; margin-bottom:12px; }}
.societe {{ font-size:12pt; font-weight:bold; color:#1F4E79; }}
.doc-title {{ text-align:right; }}
.doc-title h1 {{ font-size:14pt; color:#1F4E79; text-transform:uppercase; }}
.doc-title .sub {{ font-size:9pt; color:#555; }}
hr {{ border:none; border-top:2px solid #1F4E79; margin:10px 0; }}
table {{ width:100%; border-collapse:collapse; font-size:8pt; margin-top:10px; }}
thead tr {{ background:#1F4E79; color:#fff; }}
thead th {{ padding:5px 6px; text-align:center; font-weight:normal; border:1px solid #1565a0; }}
tbody tr {{ border-bottom:1px solid #ddd; }}
tbody tr:nth-child(even) {{ background:#F0F4F8; }}
tbody td {{ padding:4px 6px; border:1px solid #ddd; }}
td.r {{ text-align:right; }}
td.c {{ text-align:center; }}
tfoot tr {{ background:#D6E4F0; font-weight:bold; }}
tfoot td {{ padding:5px 6px; border:1px solid #1565a0; }}
.footer {{ margin-top:20px; font-size:8pt; color:#555; }}
.section-header {{ background:#BDD7EE; padding:4px 8px; font-weight:bold; font-size:9pt; 
                   margin:8px 0 0; color:#1F4E79; }}
</style></head><body>

<div class='header'>
  <div>
    <div class='societe'>{rs}</div>
    <div style='font-size:8pt;color:#555;'>{company?.Address ?? ""}</div>
  </div>
  <div class='doc-title'>
    <h1>Déclaration IPRES</h1>
    <div class='sub'>Période : {periode}</div>
    <div class='sub'>Édité le {DateTime.Today:dd/MM/yyyy}</div>
  </div>
</div>
<hr/>

<div class='section-header'>Régime Général (RG) — Taux salarié 5,60% · Patronal 8,40% · Plafond 432 000 FCFA</div>
<table>
<thead><tr>
  <th style='width:30px'>N°</th>
  <th>Matricule</th>
  <th>N° IPRES</th>
  <th>Nom et Prénom</th>
  <th>Brut social</th>
  <th>Base RG</th>
  <th>Cotis. Salarié</th>
  <th>Cotis. Employeur</th>
  <th>Total RG</th>
</tr></thead>
<tbody>");

            int n = 1;
            foreach (var l in lignes)
            {
                sb.Append($@"<tr>
  <td class='c'>{n++}</td>
  <td class='c'>{l.Matricule}</td>
  <td class='c'>{l.NumeroIPRES}</td>
  <td>{l.NomPrenom}</td>
  <td class='r'>{l.BrutSocial:N0}</td>
  <td class='r'>{l.BaseRG:N0}</td>
  <td class='r'>{l.RG_Sal:N0}</td>
  <td class='r'>{l.RG_Pat:N0}</td>
  <td class='r'>{(l.RG_Sal + l.RG_Pat):N0}</td>
</tr>");
            }

            var totRGSal = lignes.Sum(x => x.RG_Sal);
            var totRGPat = lignes.Sum(x => x.RG_Pat);
            sb.Append($@"</tbody>
<tfoot><tr>
  <td colspan='6' class='r'>TOTAL</td>
  <td class='r'>{totRGSal:N0}</td>
  <td class='r'>{totRGPat:N0}</td>
  <td class='r'>{(totRGSal + totRGPat):N0}</td>
</tr></tfoot>
</table>");

            // Section RC (cadres uniquement)
            var cadres = lignes.Where(x => x.BaseRC > 0).ToList();
            if (cadres.Any())
            {
                sb.Append($@"
<div class='section-header'>Régime Cadre (RC) — Taux salarié 2,40% · Patronal 3,60% · Plafond 1 296 000 FCFA</div>
<table>
<thead><tr>
  <th style='width:30px'>N°</th>
  <th>Matricule</th>
  <th>N° IPRES</th>
  <th>Nom et Prénom</th>
  <th>Base RC</th>
  <th>Cotis. Salarié</th>
  <th>Cotis. Employeur</th>
  <th>Total RC</th>
</tr></thead>
<tbody>");
                int nc = 1;
                foreach (var l in cadres)
                {
                    sb.Append($@"<tr>
  <td class='c'>{nc++}</td>
  <td class='c'>{l.Matricule}</td>
  <td class='c'>{l.NumeroIPRES}</td>
  <td>{l.NomPrenom}</td>
  <td class='r'>{l.BaseRC:N0}</td>
  <td class='r'>{l.RC_Sal:N0}</td>
  <td class='r'>{l.RC_Pat:N0}</td>
  <td class='r'>{(l.RC_Sal + l.RC_Pat):N0}</td>
</tr>");
                }
                var totRCSal = cadres.Sum(x => x.RC_Sal);
                var totRCPat = cadres.Sum(x => x.RC_Pat);
                sb.Append($@"</tbody>
<tfoot><tr>
  <td colspan='5' class='r'>TOTAL</td>
  <td class='r'>{totRCSal:N0}</td>
  <td class='r'>{totRCPat:N0}</td>
  <td class='r'>{(totRCSal + totRCPat):N0}</td>
</tr></tfoot>
</table>");
            }

            // Récapitulatif
            var totalSal = lignes.Sum(x => x.TotalIPRES_Sal);
            var totalPat = lignes.Sum(x => x.TotalIPRES_Pat);
            sb.Append($@"
<div class='section-header'>Récapitulatif IPRES</div>
<table style='width:50%;margin-top:6px;'>
<thead><tr><th>Rubrique</th><th>Part salarié</th><th>Part employeur</th><th>Total</th></tr></thead>
<tbody>
  <tr><td>IPRES Régime Général</td>
      <td class='r'>{totRGSal:N0}</td><td class='r'>{totRGPat:N0}</td>
      <td class='r'>{(totRGSal + totRGPat):N0}</td></tr>
  {(cadres.Any() ? $@"<tr><td>IPRES Régime Cadre</td>
      <td class='r'>{cadres.Sum(x => x.RC_Sal):N0}</td>
      <td class='r'>{cadres.Sum(x => x.RC_Pat):N0}</td>
      <td class='r'>{cadres.Sum(x => x.TotalIPRES_Pat + x.TotalIPRES_Sal - x.RG_Sal - x.RG_Pat):N0}</td></tr>" : "")}
</tbody>
<tfoot><tr>
  <td>TOTAL IPRES</td>
  <td class='r'>{totalSal:N0}</td>
  <td class='r'>{totalPat:N0}</td>
  <td class='r'>{(totalSal + totalPat):N0}</td>
</tr></tfoot>
</table>

<div class='footer'>Document généré par AdiPAIE V02 — {DateTime.Now:dd/MM/yyyy HH:mm}</div>
</body></html>");

            return sb.ToString();
        }

        private static string BuildHtmlCSS(
            List<LigneDeclaration> lignes,
            Company company,
            int annee, int mois)
        {
            var rs = company?.RaisonSociale ?? "Société";
            var periode = new DateTime(annee, mois, 1).ToString("MMMM yyyy", Fr);
            var sb = new StringBuilder();

            sb.Append($@"<!DOCTYPE html>
<html lang='fr'><head><meta charset='UTF-8'/>
<style>
* {{ box-sizing:border-box; margin:0; padding:0; }}
body {{ font-family:Arial,sans-serif; font-size:9pt; color:#111; padding:15mm 12mm; }}
.header {{ display:flex; justify-content:space-between; margin-bottom:12px; }}
.societe {{ font-size:12pt; font-weight:bold; color:#1F4E79; }}
.doc-title {{ text-align:right; }}
.doc-title h1 {{ font-size:14pt; color:#1F4E79; text-transform:uppercase; }}
.doc-title .sub {{ font-size:9pt; color:#555; }}
hr {{ border:none; border-top:2px solid #1F4E79; margin:10px 0; }}
table {{ width:100%; border-collapse:collapse; font-size:8.5pt; margin-top:10px; }}
thead tr {{ background:#1F4E79; color:#fff; }}
thead th {{ padding:5px 6px; text-align:center; font-weight:normal; border:1px solid #1565a0; }}
tbody tr {{ border-bottom:1px solid #ddd; }}
tbody tr:nth-child(even) {{ background:#F0F4F8; }}
tbody td {{ padding:4px 6px; border:1px solid #ddd; }}
td.r {{ text-align:right; }}
td.c {{ text-align:center; }}
tfoot tr {{ background:#D6E4F0; font-weight:bold; }}
tfoot td {{ padding:5px 6px; border:1px solid #1565a0; }}
.footer {{ margin-top:20px; font-size:8pt; color:#555; }}
</style></head><body>

<div class='header'>
  <div>
    <div class='societe'>{rs}</div>
    <div style='font-size:8pt;color:#555;'>{company?.Address ?? ""}</div>
  </div>
  <div class='doc-title'>
    <h1>Déclaration CSS</h1>
    <div class='sub'>Période : {periode}</div>
    <div class='sub'>Édité le {DateTime.Today:dd/MM/yyyy}</div>
  </div>
</div>
<hr/>
<p style='font-size:8pt;color:#555;margin-bottom:8px;'>
  Accident du travail : 3,00% employeur · Allocation familiale : 7,00% employeur · Plafond : 63 000 FCFA
</p>

<table>
<thead><tr>
  <th style='width:30px'>N°</th>
  <th>Matricule</th>
  <th>Nom et Prénom</th>
  <th>Brut social</th>
  <th>Base CSS</th>
  <th>AT (3%)</th>
  <th>AF (7%)</th>
  <th>Total CSS</th>
</tr></thead>
<tbody>");

            int n = 1;
            foreach (var l in lignes)
            {
                sb.Append($@"<tr>
  <td class='c'>{n++}</td>
  <td class='c'>{l.Matricule}</td>
  <td>{l.NomPrenom}</td>
  <td class='r'>{l.BrutSocial:N0}</td>
  <td class='r'>{l.BaseCSS:N0}</td>
  <td class='r'>{l.CSS_AT:N0}</td>
  <td class='r'>{l.CSS_AF:N0}</td>
  <td class='r'>{l.TotalCSS:N0}</td>
</tr>");
            }

            var totAT = lignes.Sum(x => x.CSS_AT);
            var totAF = lignes.Sum(x => x.CSS_AF);
            sb.Append($@"</tbody>
<tfoot><tr>
  <td colspan='5' class='r'>TOTAL</td>
  <td class='r'>{totAT:N0}</td>
  <td class='r'>{totAF:N0}</td>
  <td class='r'>{(totAT + totAF):N0}</td>
</tr></tfoot>
</table>

<div class='footer'>Document généré par AdiPAIE V02 — {DateTime.Now:dd/MM/yyyy HH:mm}</div>
</body></html>");

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // GÉNÉRATION PDF
        // ═══════════════════════════════════════════════════════════════

        public static byte[] GenererIPRES_PDF(IObjectSpace os, int annee, int mois)
        {
            var company = new XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session).FirstOrDefault();
            var lignes = ExtraireLignes(os, annee, mois);
            var html = BuildHtmlIPRES(lignes, company, annee, mois);
            return ConvertirEnPdf(html, $"IPRES_{annee}_{mois:D2}");
        }

        public static byte[] GenererCSS_PDF(IObjectSpace os, int annee, int mois)
        {
            var company = new XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session).FirstOrDefault();
            var lignes = ExtraireLignes(os, annee, mois);
            var html = BuildHtmlCSS(lignes, company, annee, mois);
            return ConvertirEnPdf(html, $"CSS_{annee}_{mois:D2}");
        }

        // ═══════════════════════════════════════════════════════════════
        // GÉNÉRATION XLSX (LibreOffice HTML → xlsx)
        // ═══════════════════════════════════════════════════════════════

        public static byte[] GenererIPRES_XLSX(IObjectSpace os, int annee, int mois)
        {
            var company = new XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session).FirstOrDefault();
            var lignes = ExtraireLignes(os, annee, mois);
            var html = BuildHtmlIPRES(lignes, company, annee, mois);
            return ConvertirEnXlsx(html, $"IPRES_{annee}_{mois:D2}");
        }

        public static byte[] GenererCSS_XLSX(IObjectSpace os, int annee, int mois)
        {
            var company = new XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session).FirstOrDefault();
            var lignes = ExtraireLignes(os, annee, mois);
            var html = BuildHtmlCSS(lignes, company, annee, mois);
            return ConvertirEnXlsx(html, $"CSS_{annee}_{mois:D2}");
        }

        // ═══════════════════════════════════════════════════════════════
        // CONVERSION LibreOffice — COMMUN PDF + XLSX
        // ═══════════════════════════════════════════════════════════════

        private static byte[] ConvertirEnPdf(string html, string nomBase)
            => ConvertirViaLibreOffice(html, nomBase, "pdf", ".pdf");

        private static byte[] ConvertirEnXlsx(string html, string nomBase)
            => ConvertirViaLibreOffice(html, nomBase, "xlsx", ".xlsx");

        private static byte[] ConvertirViaLibreOffice(
            string html, string nomBase, string format, string ext)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AdiPAIE_Decl");
            Directory.CreateDirectory(tempDir);

            var htmlPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.html");
            var outputPath = Path.ChangeExtension(htmlPath, ext);

            File.WriteAllBytes(htmlPath, Encoding.UTF8.GetBytes(html));
            try
            {
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
                        Arguments = $"--headless --convert-to {format} " +
                                          $"--outdir \"{tempDir}\" \"{htmlPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                if (!proc.WaitForExit(30_000)) { proc.Kill(); return null; }

                return File.Exists(outputPath) ? File.ReadAllBytes(outputPath) : null;
            }
            catch { return null; }
            finally
            {
                try { File.Delete(htmlPath); } catch { }
                try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            }
        }
    }
}
