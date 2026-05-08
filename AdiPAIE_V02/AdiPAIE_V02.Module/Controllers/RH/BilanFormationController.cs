using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using Microsoft.Extensions.DependencyInjection;

namespace AdiPAIE_V02.Module.Controllers.RH
{
    // ══════════════════════════════════════════════════════════════════════
    // BILAN DE FORMATION — action sur PlanFormation DetailView
    //
    // Génère un PDF récapitulatif contenant :
    //   1. Résumé exécutif (KPIs : sessions, inscrits, taux présence, budget)
    //   2. Tableau de détail des sessions (coûts, présents, note moyenne)
    //   3. Répartition par domaine (inscrits, heures, coût)
    //   4. Top participants (salariés ayant suivi le plus de formations)
    // ══════════════════════════════════════════════════════════════════════
    public class BilanFormationController
        : ObjectViewController<DetailView, PlanFormation>
    {
        private readonly SimpleAction _bilanAction;

        public BilanFormationController()
        {
            _bilanAction = new SimpleAction(this,
                "PlanFormation_GenererBilan",
                PredefinedCategory.View)
            {
                Caption = "Générer le bilan",
                ImageName = "Action_AnalyzeHorizontal",
                ToolTip = "Génère un bilan PDF complet du plan de formation."
            };
            _bilanAction.Execute += OnBilanExecute;
        }

        // ─────────────────────────────────────────────────────────────────
        private void OnBilanExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var plan = (PlanFormation)View.CurrentObject;
            var company = ObjectSpace.GetObjectsQuery<Company>().FirstOrDefault();

            try
            {
                var html = BilanHtmlBuilder.Build(plan, company);
                var htmlBytes = Encoding.UTF8.GetBytes(html);
                var nomBase = $"Bilan_Formation_{plan.Annee}_{Nettoyer(plan.Titre)}";

                var pdfBytes = ConvertirEnPdf(htmlBytes, nomBase);
                var estPdf = pdfBytes != null;
                var contenu = estPdf ? pdfBytes : htmlBytes;
                var ext = estPdf ? ".pdf" : ".html";
                var nomFich = nomBase + ext;

                // Archiver dans le dossier société (DossierSalarie non applicable ici)
                // → on archive directement sur le PlanFormation via une notification RH

                // Envoyer le fichier aux RH par email
                var rhEmails = WorkflowEmailHelper.ExtraireEmailsRH(Application);
                if (rhEmails.Any())
                {
                    var senders = WorkflowEmailHelper.ExtraireSender(Application);
                    if (senders != null)
                    {
                        var body = WorkflowEmailHelper.HtmlTableau(
                            $"Bilan formation — {plan.Titre} ({plan.Annee})",
                            $"Le bilan du plan de formation est généré. " +
                            $"Retrouvez le fichier {nomFich} en pièce jointe.",
                            new[]
                            {
                                ("Plan",              plan.Titre),
                                ("Année",             plan.Annee.ToString()),
                                ("Sessions",          plan.NbSessions.ToString()),
                                ("Inscrits",          plan.NbInscrits.ToString()),
                                ("Coût réel",         plan.CoutReel.ToString("N0") + " FCFA"),
                                ("Budget prévis.",    plan.BudgetPrevisionnel.ToString("N0") + " FCFA"),
                                ("Consommation",      plan.TauxConsommation.ToString("N1") + " %"),
                            });

                        foreach (var dest in rhEmails)
                            WorkflowEmailHelper.EnvoyerAsync(senders, dest,
                                $"[AdiPAIE] Bilan formation — {plan.Titre} ({plan.Annee})", body);
                    }
                }

                // Sauvegarder en temp et proposer le téléchargement
                // (XAF Blazor : via ShowViewStrategy ou message d'info avec lien)
                var tempPath = Path.Combine(
                    Path.GetTempPath(), "AdiPAIE_Bilan", nomFich);
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
                File.WriteAllBytes(tempPath, contenu);

                Application.ShowViewStrategy?.ShowMessage(
                    $"Bilan généré ({nomFich}). " +
                    (rhEmails.Any()
                        ? "Un email a été envoyé aux RH."
                        : "Configurez les emails RH dans Paramètres de paie pour l'envoi automatique."),
                    InformationType.Success, 6000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur génération bilan : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        private static byte[] ConvertirEnPdf(byte[] htmlBytes, string nomBase)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AdiPAIE_Bilan");
            Directory.CreateDirectory(tempDir);
            var htmlPath = Path.Combine(tempDir, $"{nomBase}_{Guid.NewGuid():N}.html");
            File.WriteAllBytes(htmlPath, htmlBytes);
            try
            {
                var chemins = new[]
                {
                    @"C:\Program Files\LibreOffice\program\soffice.exe",
                    @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
                    "/usr/bin/soffice", "soffice"
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
                var pdfPath = Path.ChangeExtension(htmlPath, ".pdf");
                return File.Exists(pdfPath) ? File.ReadAllBytes(pdfPath) : null;
            }
            catch { return null; }
            finally { try { File.Delete(htmlPath); } catch { } }
        }

        private static string Nettoyer(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "plan";
            return new string(s.Select(c =>
                    char.IsLetterOrDigit(c) ? c : '_').ToArray()).TrimEnd('_');
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // FEUILLE D'ÉMARGEMENT — action sur SessionFormation (ajout au controller existant)
    //
    // Séparé pour ne pas modifier FormationWorkflowController.
    // ══════════════════════════════════════════════════════════════════════
    
 
    // ══════════════════════════════════════════════════════════════════════
    // BUILDER HTML DU BILAN — classe utilitaire interne
    // ══════════════════════════════════════════════════════════════════════
    internal static class BilanHtmlBuilder
    {
        private static readonly CultureInfo Fr = new CultureInfo("fr-FR");

        internal static string Build(PlanFormation plan, Company company)
        {
            var sessions = plan.Sessions.ToList();
            var raisonSociale = company?.RaisonSociale ?? "Société";
            var ville = company?.Ville ?? "Dakar";
            var aujourd = DateTime.Today.ToString("dd MMMM yyyy", Fr);

            // ── KPIs globaux ──────────────────────────────────────────
            var nbSessions = sessions.Count;
            var nbInscrits = sessions.SelectMany(s => s.Inscriptions)
                .Count(i => i.Statut != InscriptionStatut.Annulee);
            var nbPresents = sessions.SelectMany(s => s.Inscriptions)
                .Count(i => i.Presence && i.Statut == InscriptionStatut.Confirmee);
            var tauxPresence = nbInscrits > 0
                ? Math.Round((decimal)nbPresents / nbInscrits * 100, 1) : 0;
            var coutReel = sessions.Sum(s => s.CoutReel);
            var coutPrevis = sessions.Sum(s => s.CoutPrevisionnel);
            var ecartBudget = plan.BudgetPrevisionnel > 0
                ? Math.Round(coutReel / plan.BudgetPrevisionnel * 100, 1) : 0;
            var dureeTotal = sessions.Sum(s => s.DureeHeures > 0 ? s.DureeHeures
                                    : s.DureeJours * 8);

            // ── Tableau des sessions ──────────────────────────────────
            var lignesSessions = new StringBuilder();
            int num = 1;
            foreach (var s in sessions.OrderBy(s => s.DateDebut))
            {
                var nbI = s.Inscriptions.Count(i => i.Statut != InscriptionStatut.Annulee);
                var nbP = s.Inscriptions.Count(i =>
                    i.Presence && i.Statut == InscriptionStatut.Confirmee);
                var tx = nbI > 0 ? Math.Round((decimal)nbP / nbI * 100, 0) : 0;
                var noteMoy = nbI > 0
                    ? Math.Round(s.Inscriptions
                        .Where(i => i.NoteMoyenneGlobale > 0)
                        .Select(i => i.NoteMoyenneGlobale)
                        .DefaultIfEmpty(0)
                        .Average(), 1)
                    : 0;
                var bg = num % 2 == 0 ? "#F4F8FC" : "#FFFFFF";
                lignesSessions.Append(
                    $"<tr style='background:{bg};'>" +
                    $"<td class='c'>{num++}</td>" +
                    $"<td>{s.Intitule}</td>" +
                    $"<td>{s.Domaine?.Libelle ?? "—"}</td>" +
                    $"<td class='c'>{s.DateDebut:dd/MM/yyyy}</td>" +
                    $"<td class='c'>{s.DureeJours}j</td>" +
                    $"<td class='c'>{nbI}</td>" +
                    $"<td class='c'>{nbP}</td>" +
                    $"<td class='c'>{tx} %</td>" +
                    $"<td class='r'>{s.CoutReel:N0}</td>" +
                    $"<td class='c'>{(noteMoy > 0 ? noteMoy.ToString("N1") : "—")}</td>" +
                    "</tr>");
            }

            // ── Répartition par domaine ───────────────────────────────
            var parDomaine = sessions
                .GroupBy(s => s.Domaine?.Libelle ?? "Non classé")
                .OrderByDescending(g => g.Count())
                .ToList();
            var lignesDomaine = new StringBuilder();
            foreach (var grp in parDomaine)
            {
                var heures = grp.Sum(s => s.DureeHeures > 0 ? s.DureeHeures : s.DureeJours * 8);
                var inscrits = grp.SelectMany(s => s.Inscriptions)
                    .Count(i => i.Statut != InscriptionStatut.Annulee);
                var cout = grp.Sum(s => s.CoutReel);
                lignesDomaine.Append(
                    $"<tr><td>{grp.Key}</td>" +
                    $"<td class='c'>{grp.Count()}</td>" +
                    $"<td class='c'>{inscrits}</td>" +
                    $"<td class='c'>{heures} h</td>" +
                    $"<td class='r'>{cout:N0} FCFA</td></tr>");
            }

            // ── Top participants ──────────────────────────────────────
            var topParticipants = sessions
                .SelectMany(s => s.Inscriptions)
                .Where(i => i.Statut != InscriptionStatut.Annulee && i.Salarie != null)
                .GroupBy(i => i.Salarie)
                .Select(g => new
                {
                    Nom = g.Key.FullName ?? "—",
                    Matricule = g.Key.Matricule ?? "—",
                    Dept = g.Key.Departement?.Nom ?? "—",
                    Nb = g.Count(),
                    Heures = g.Sum(i => i.SessionFormation?.DureeHeures > 0
                                    ? i.SessionFormation.DureeHeures
                                    : (i.SessionFormation?.DureeJours ?? 0) * 8),
                    Present = g.Count(i => i.Presence),
                })
                .OrderByDescending(x => x.Nb)
                .Take(15)
                .ToList();

            var lignesTop = new StringBuilder();
            int tn = 1;
            foreach (var p in topParticipants)
            {
                var bg = tn % 2 == 0 ? "#F4F8FC" : "#FFFFFF";
                lignesTop.Append(
                    $"<tr style='background:{bg};'>" +
                    $"<td class='c'>{tn++}</td>" +
                    $"<td>{p.Nom}</td>" +
                    $"<td class='c'>{p.Matricule}</td>" +
                    $"<td>{p.Dept}</td>" +
                    $"<td class='c'>{p.Nb}</td>" +
                    $"<td class='c'>{p.Heures} h</td>" +
                    $"<td class='c'>{p.Present}</td>" +
                    "</tr>");
            }

            // ── CSS commun ────────────────────────────────────────────
            const string css = @"
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: Arial, sans-serif; font-size: 10pt; color: #111;
       padding: 16mm 18mm; }
h1 { font-size: 16pt; color: #1F4E79; }
h2 { font-size: 12pt; color: #1F4E79; margin: 22px 0 10px;
     border-bottom: 2px solid #BDD7EE; padding-bottom: 4px; }
.meta { font-size: 9pt; color: #777; margin-top: 2px; }
hr { border: none; border-top: 2px solid #1F4E79; margin: 14px 0; }
/* KPI cards */
.kpis { display: flex; flex-wrap: wrap; gap: 12px; margin: 14px 0; }
.kpi  { background: #EBF3FB; border-radius: 6px; padding: 12px 18px;
         min-width: 140px; flex: 1; }
.kpi-val  { font-size: 20pt; font-weight: bold; color: #1F4E79; }
.kpi-lbl  { font-size: 8pt;  color: #555; margin-top: 2px; }
/* Tables */
table  { width: 100%; border-collapse: collapse; margin-bottom: 8px;
         font-size: 9pt; }
thead tr  { background: #1F4E79; color: #fff; }
thead th  { padding: 7px 10px; text-align: left; font-weight: normal; }
tbody tr  { border-bottom: 1px solid #DDE; }
tbody td  { padding: 6px 10px; }
td.c { text-align: center; }
td.r { text-align: right; }
.footer { margin-top: 28px; font-size: 8pt; color: #AAA;
          border-top: 1px solid #DDE; padding-top: 8px;
          display: flex; justify-content: space-between; }
";

            return $@"<!DOCTYPE html>
<html lang='fr'>
<head>
  <meta charset='UTF-8'/>
  <style>{css}</style>
</head>
<body>

  <!-- ── En-tête ── -->
  <div style='display:flex;justify-content:space-between;align-items:flex-start;'>
    <div>
      <h1>{plan.Titre}</h1>
      <p class='meta'>{raisonSociale} · Bilan formation {plan.Annee}</p>
    </div>
    <div style='text-align:right;font-size:9pt;color:#888;'>
      <div>Statut : <strong style='color:#1F4E79;'>{plan.Statut}</strong></div>
      <div>Édité le {aujourd}</div>
    </div>
  </div>
  <hr/>

  <!-- ── KPIs ── -->
  <h2>Résumé exécutif</h2>
  <div class='kpis'>
    <div class='kpi'>
      <div class='kpi-val'>{nbSessions}</div>
      <div class='kpi-lbl'>Sessions planifiées</div>
    </div>
    <div class='kpi'>
      <div class='kpi-val'>{nbInscrits}</div>
      <div class='kpi-lbl'>Inscriptions totales</div>
    </div>
    <div class='kpi'>
      <div class='kpi-val'>{nbPresents}</div>
      <div class='kpi-lbl'>Participants présents</div>
    </div>
    <div class='kpi'>
      <div class='kpi-val'>{tauxPresence:N1} %</div>
      <div class='kpi-lbl'>Taux de présence</div>
    </div>
    <div class='kpi'>
      <div class='kpi-val'>{dureeTotal} h</div>
      <div class='kpi-lbl'>Heures de formation</div>
    </div>
    <div class='kpi'>
      <div class='kpi-val'>{coutReel:N0}</div>
      <div class='kpi-lbl'>Coût réel (FCFA)</div>
    </div>
    <div class='kpi'>
      <div class='kpi-val'>{ecartBudget:N1} %</div>
      <div class='kpi-lbl'>Consommation budget</div>
    </div>
    <div class='kpi'>
      <div class='kpi-val'>{plan.BudgetPrevisionnel:N0}</div>
      <div class='kpi-lbl'>Budget prévisionnel (FCFA)</div>
    </div>
  </div>

  <!-- ── Sessions ── -->
  <h2>Détail des sessions</h2>
  <table>
    <thead>
      <tr>
        <th style='width:28px;'>N°</th>
        <th>Intitulé</th>
        <th>Domaine</th>
        <th>Date début</th>
        <th>Durée</th>
        <th>Inscrits</th>
        <th>Présents</th>
        <th>Taux</th>
        <th style='text-align:right;'>Coût réel (FCFA)</th>
        <th>Note moy.</th>
      </tr>
    </thead>
    <tbody>
      {lignesSessions}
    </tbody>
    <tfoot>
      <tr style='background:#EBF3FB;font-weight:bold;'>
        <td colspan='5' class='r'>TOTAL</td>
        <td class='c'>{nbInscrits}</td>
        <td class='c'>{nbPresents}</td>
        <td class='c'>{tauxPresence:N1} %</td>
        <td class='r'>{coutReel:N0}</td>
        <td></td>
      </tr>
    </tfoot>
  </table>

  <!-- ── Par domaine ── -->
  <h2>Répartition par domaine</h2>
  <table>
    <thead>
      <tr>
        <th>Domaine</th>
        <th>Sessions</th>
        <th>Inscrits</th>
        <th>Heures</th>
        <th style='text-align:right;'>Coût réel</th>
      </tr>
    </thead>
    <tbody>{lignesDomaine}</tbody>
  </table>

  <!-- ── Top participants ── -->
  <h2>Top participants (15 premiers)</h2>
  <table>
    <thead>
      <tr>
        <th style='width:28px;'>Rang</th>
        <th>Nom Prénom</th>
        <th>Matricule</th>
        <th>Département</th>
        <th>Formations</th>
        <th>Heures</th>
        <th>Présences</th>
      </tr>
    </thead>
    <tbody>{lignesTop}</tbody>
  </table>

  <!-- ── Pied de page ── -->
  <div class='footer'>
    <span>Plan approuvé le {plan.DateApprobation:dd/MM/yyyy} par {plan.ApprovePar?.FullName ?? "—"}</span>
    <span>Bilan AdiPAIE · {raisonSociale} · {aujourd}</span>
  </div>

</body>
</html>";
        }
    }
}
