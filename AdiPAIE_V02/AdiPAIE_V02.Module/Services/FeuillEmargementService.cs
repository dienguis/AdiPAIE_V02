using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Génère la feuille d'émargement (liste de présence) d'une SessionFormation.
    ///
    /// Produit un fichier PDF prêt à imprimer contenant :
    ///   - En-tête société
    ///   - Informations de la session (intitulé, dates, lieu, formateur)
    ///   - Tableau des participants : N°, Nom Prénom, Matricule, Département, Signature
    ///   - Pied de page avec espace de signature formateur / RH
    ///
    /// Conversion HTML → PDF via LibreOffice headless.

    public static class FeuillEmargementService
    {
        private static readonly CultureInfo Fr = new CultureInfo("fr-FR");

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Génère la feuille d'émargement et la retourne en bytes (PDF ou HTML fallback).
     
        /// </summary>
        public static (byte[] Contenu, string NomFichier, bool EstPdf) Generer(
            DevExpress.ExpressApp.IObjectSpace os,
            SessionFormation session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            var company = new DevExpress.Xpo.XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                .FirstOrDefault();

            var html = BuildHtml(session, company);
            var htmlBytes = Encoding.UTF8.GetBytes(html);
            var nomBase = $"Emargement_{Nettoyer(session.Intitule)}_{session.DateDebut:yyyyMMdd}";

            
            var pdfBytes = ConvertirEnPdf(htmlBytes, nomBase);
            if (pdfBytes != null)
                return (pdfBytes, nomBase + ".pdf", true);

            // Fallback : HTML brut (LibreOffice absent ou échec)
            return (htmlBytes, nomBase + ".html", false);
        }

        // ─────────────────────────────────────────────────────────────────
        //  BUILD HTML
        // ─────────────────────────────────────────────────────────────────
        private static string BuildHtml(SessionFormation session, Company company)
        {
            var raisonSociale = company?.RaisonSociale ?? "Société";
            var adresse = company?.Address ?? "";
            var ville = company?.Ville ?? "Dakar";

            // ── Participants (hors annulés, triés par nom) ────────────
            var participants = session.Inscriptions
                .Where(i => i.Statut != InscriptionStatut.Annulee)
                .OrderBy(i => i.Salarie?.LastName)
                .ThenBy(i => i.Salarie?.FirstName)
                .ToList();

            // ── Modalité ──────────────────────────────────────────────
            var modalite = session.Modalite switch
            {
                FormationModalite.Presentiel => "Présentiel",
                FormationModalite.Distanciel => "Distanciel",
                FormationModalite.Mixte => "Mixte",
                FormationModalite.ELearning => "E-learning",
                _ => "—"
            };

            // ── Lignes tableau participants ───────────────────────────
            var lignesHtml = new StringBuilder();
            int num = 1;
            foreach (var insc in participants)
            {
                var sal = insc.Salarie;
                lignesHtml.Append(
                    $"<tr>" +
                    $"<td class='c'>{num++}</td>" +
                    $"<td>{sal?.FullName ?? "—"}</td>" +
                    $"<td class='c'>{sal?.Matricule ?? "—"}</td>" +
                    $"<td>{sal?.Departement?.Nom ?? "—"}</td>" +
                    "<td class='sig'></td>" +
                    "</tr>");
            }

            // Lignes vides pour minimum visuel (≥ 5 lignes)
            for (int i = participants.Count; i < 5; i++)
                lignesHtml.Append(
                    "<tr><td class='c'>&nbsp;</td><td>&nbsp;</td>" +
                    "<td class='c'>&nbsp;</td><td>&nbsp;</td>" +
                    "<td class='sig'>&nbsp;</td></tr>");

            // ── Infos période / durée ─────────────────────────────────
            var datesPeriode = session.DateDebut.Date == session.DateFin.Date
                ? session.DateDebut.ToString("dd MMMM yyyy", Fr)
                : $"du {session.DateDebut.ToString("dd MMMM yyyy", Fr)}" +
                  $" au {session.DateFin.ToString("dd MMMM yyyy", Fr)}";

            var duree = session.DureeHeures > 0
                ? $"{session.DureeJours} jour(s) / {session.DureeHeures} heure(s)"
                : $"{session.DureeJours} jour(s)";

            // ── HTML ──────────────────────────────────────────────────
            return $@"<!DOCTYPE html>
<html lang='fr'>
<head>
  <meta charset='UTF-8'/>
  <style>
    * {{ box-sizing: border-box; margin: 0; padding: 0; }}
    body {{ font-family: Arial, sans-serif; font-size: 11pt; color: #111;
           padding: 20mm 18mm; }}

    /* En-tête */
    .header {{ display: flex; justify-content: space-between;
               align-items: flex-start; margin-bottom: 16px; }}
    .societe     {{ font-size: 13pt; font-weight: bold; color: #1F4E79; }}
    .societe-sub {{ font-size: 9pt;  color: #555; margin-top: 2px; }}
    .doc-title   {{ text-align: right; }}
    .doc-title h1 {{ font-size: 15pt; color: #1F4E79; text-transform: uppercase;
                     letter-spacing: 1px; }}
    .doc-title .ref {{ font-size: 9pt; color: #888; margin-top: 4px; }}

    hr {{ border: none; border-top: 2px solid #1F4E79; margin: 12px 0; }}

    /* Infos session */
    .info-grid {{ display: grid; grid-template-columns: 1fr 1fr;
                  gap: 6px 20px; margin: 12px 0 18px; font-size: 10pt; }}
    .info-item  {{ display: flex; gap: 6px; }}
    .info-label {{ color: #555; white-space: nowrap; min-width: 90px;
                   font-weight: bold; }}

    /* Tableau */
    table {{ width: 100%; border-collapse: collapse; margin: 12px 0;
             font-size: 10pt; }}
    thead tr {{ background: #1F4E79; color: #fff; }}
    thead th {{ padding: 8px 10px; text-align: left; font-weight: normal; }}
    tbody tr {{ border-bottom: 1px solid #CCC; }}
    tbody tr:nth-child(even) {{ background: #F4F8FC; }}
    tbody td {{ padding: 8px 10px; }}
    td.c   {{ text-align: center; }}
    td.sig {{ min-width: 120px; border-left: 1px dashed #999;
              border-right: 1px dashed #999; }}

    /* Pied de page */
    .footer {{ margin-top: 30px; display: flex;
               justify-content: space-between; gap: 40px; font-size: 10pt; }}
    .sign-box {{ border: 1px solid #999; border-radius: 4px;
                 padding: 10px 16px; min-height: 80px; min-width: 200px; }}
    .sign-label {{ font-size: 9pt; color: #666; margin-bottom: 4px; }}
    .sign-name  {{ font-weight: bold; color: #1F4E79; margin-top: 6px;
                   font-size: 10pt; }}
    .date-edit  {{ color: #888; font-size: 9pt; margin-top: 14px; }}
  </style>
</head>
<body>

  <!-- En-tête -->
  <div class='header'>
    <div>
      <div class='societe'>{raisonSociale}</div>
      <div class='societe-sub'>{adresse} — {ville}</div>
    </div>
    <div class='doc-title'>
      <h1>Feuille d'émargement</h1>
      <div class='ref'>Formation · {session.DateDebut:yyyy}</div>
    </div>
  </div>
  <hr/>

  <!-- Informations session -->
  <div class='info-grid'>
    <div class='info-item'>
      <span class='info-label'>Formation :</span>
      <span><strong>{session.Intitule}</strong></span>
    </div>
    <div class='info-item'>
      <span class='info-label'>Domaine :</span>
      <span>{session.Domaine?.Libelle ?? "—"}</span>
    </div>
    <div class='info-item'>
      <span class='info-label'>Dates :</span>
      <span>{datesPeriode}</span>
    </div>
    <div class='info-item'>
      <span class='info-label'>Durée :</span>
      <span>{duree}</span>
    </div>
    <div class='info-item'>
      <span class='info-label'>Modalité :</span>
      <span>{modalite}</span>
    </div>
    <div class='info-item'>
      <span class='info-label'>Lieu :</span>
      <span>{session.Lieu ?? "À définir"}</span>
    </div>
    <div class='info-item'>
      <span class='info-label'>Formateur :</span>
      <span>{session.FormateurNom ?? "—"}</span>
    </div>
    <div class='info-item'>
      <span class='info-label'>Inscrits :</span>
      <span>{participants.Count} participant(s)</span>
    </div>
  </div>

  <!-- Tableau des participants -->
  <table>
    <thead>
      <tr>
        <th style='width:30px;'>N°</th>
        <th>Nom et Prénom</th>
        <th style='width:90px;'>Matricule</th>
        <th>Département</th>
        <th style='width:130px; text-align:center;'>Signature</th>
      </tr>
    </thead>
    <tbody>
      {lignesHtml}
    </tbody>
  </table>

  <!-- Pied de page -->
  <div class='footer'>
    <div class='sign-box'>
      <div class='sign-label'>Signature du formateur / organisme</div>
      <div style='height:40px;'></div>
      <div class='sign-name'>{session.FormateurNom ?? "— Formateur —"}</div>
    </div>
    <div class='sign-box'>
      <div class='sign-label'>Visa RH / Responsable formation</div>
      <div style='height:40px;'></div>
    </div>
    <div style='font-size:9pt; color:#888; align-self:flex-end;'>
      <div>Édité le {DateTime.Today.ToString("dd MMMM yyyy", Fr)}</div>
      <div style='margin-top:4px;'>Document confidentiel AdiPAIE</div>
    </div>
  </div>

</body>
</html>";
        }

        // ─────────────────────────────────────────────────────────────────
        //  CONVERSION PDF

        private static byte[] ConvertirEnPdf(byte[] htmlBytes, string nomBase)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AdiPAIE_Emargement");
            Directory.CreateDirectory(tempDir);

       
            var htmlPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.html");
            var pdfPath = Path.ChangeExtension(htmlPath, ".pdf");

            File.WriteAllBytes(htmlPath, htmlBytes);
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
                        Arguments = $"--headless --convert-to pdf " +
                                          $"--outdir \"{tempDir}\" \"{htmlPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                if (!proc.WaitForExit(30_000)) { proc.Kill(); return null; }

              
                return File.Exists(pdfPath) ? File.ReadAllBytes(pdfPath) : null;
            }
            catch { return null; }
            finally
            {
                // Nettoyage systématique — aucun fichier conservé
                try { File.Delete(htmlPath); } catch { /* silencieux */ }
                try { if (File.Exists(pdfPath)) File.Delete(pdfPath); } catch { /* silencieux */ }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        private static string Nettoyer(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "session";
            return new string(s.Select(c =>
                char.IsLetterOrDigit(c) ? c : '_').ToArray()).TrimEnd('_');
        }
    }
}
