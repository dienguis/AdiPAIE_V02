using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Blazor.Server.Services
{
    /// <summary>
    /// Service d'arrière-plan : rappels évaluation à froid des formations.
    ///
    /// Logique :\
    ///   - Tourne toutes les heures, se déclenche une fois par jour à 8 h.
    ///   - Repère les SuiviFormation dont DateEvaluationFroid est null (non évaluée).
    ///   - Envoi J+30  : DateFormation entre (aujourd'hui − 60j) et (aujourd'hui − 30j).
    ///   - Envoi J+90  : DateFormation entre (aujourd'hui − 120j) et (aujourd'hui − 90j).
    ///   - Envoie un email de rappel au N+1 du salarié (ou aux RH si pas de N+1).
    ///   - Envoie en parallèle une synthèse globale aux emails RH configurés.
    ///
    /// Enregistrement dans Startup.cs :
    ///   services.AddHostedService&lt;EvaluationFroidRappelService&gt;();
    /// </summary>
    public class EvaluationFroidRappelService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<EvaluationFroidRappelService> _log;
        private DateTime _dernierEnvoi = DateTime.MinValue;

        public EvaluationFroidRappelService(
            IServiceProvider services,
            ILogger<EvaluationFroidRappelService> log)
        {
            _services = services;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("EvaluationFroidRappelService démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await TryRunIfDue(); }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Erreur EvaluationFroidRappelService.");
                }
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        private async Task TryRunIfDue()
        {
            var now = DateTime.Now;
            // Déclenchement à 8h une fois par jour
            if (now.Hour != 8 || _dernierEnvoi.Date == now.Date) return;
            _dernierEnvoi = now;

            _log.LogInformation("Contrôle évaluations à froid ({0:HH:mm}).", now);
            await Task.Run(TraiterRappels);
        }

        // ─────────────────────────────────────────────────────────────────
        private void TraiterRappels()
        {
            using var scope = _services.CreateScope();
            var ospFactory = scope.ServiceProvider
                .GetRequiredService<IObjectSpaceFactory>();
            using var os = ospFactory.CreateObjectSpace(typeof(SuiviFormation));

            var prm = ParametresPaie.TryGet(os);
            if (prm == null || !prm.EmailActif) return;

            var rhEmails = ParseEmails(prm.EmailsRHAlertes);
            var aujourd = DateTime.Today;

            // ── Fenêtres temporelles ──────────────────────────────────
            // J+30 : formation terminée il y a 30 à 60 jours
            var j30Min = aujourd.AddDays(-60);
            var j30Max = aujourd.AddDays(-30);
            // J+90 : formation terminée il y a 90 à 120 jours
            var j90Min = aujourd.AddDays(-120);
            var j90Max = aujourd.AddDays(-90);

            var tousLesNonEvalues = os.GetObjectsQuery<SuiviFormation>()
                .Where(sf => sf.DateEvaluationFroid == null)
                .ToList();

            var dansJ30 = tousLesNonEvalues
                .Where(sf => sf.DateFormation.Date >= j30Min
                          && sf.DateFormation.Date <= j30Max)
                .ToList();

            var dansJ90 = tousLesNonEvalues
                .Where(sf => sf.DateFormation.Date >= j90Min
                          && sf.DateFormation.Date <= j90Max)
                .ToList();

            if (!dansJ30.Any() && !dansJ90.Any())
            {
                _log.LogDebug("Aucune évaluation à froid à rappeler.");
                return;
            }

            _log.LogInformation(
                "Rappels évaluation à froid : {0} à J+30, {1} à J+90.",
                dansJ30.Count, dansJ90.Count);

            try
            {
                var sender = prm.CreateEmailSender();

                // ── Email individuel au N+1 ───────────────────────────
                EnvoyerRappelsIndividuels(sender, dansJ30, "J+30", aujourd);
                EnvoyerRappelsIndividuels(sender, dansJ90, "J+90", aujourd);

                // ── Email synthèse aux RH ────────────────────────────
                if (rhEmails.Any())
                {
                    var body = BuildEmailSyntheseRH(dansJ30, dansJ90, aujourd);
                    var sujet = $"[AdiPAIE] Rappel — Évaluations formation à froid en attente ({aujourd:dd/MM/yyyy})";
                    foreach (var dest in rhEmails)
                        sender.Send(dest, sujet, body);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Échec envoi emails rappel évaluation à froid.");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Envoie un email au N+1 (ou RH si absent) pour chaque SuiviFormation
        private static void EnvoyerRappelsIndividuels(
            IEmailSender sender,
            List<SuiviFormation> suivis,
            string echeance,
            DateTime aujourd)
        {
            // Grouper par N+1 pour envoyer un seul email par manager
            var parManager = suivis
                .GroupBy(sf => sf.Salarie?.Manager)
                .ToList();

            foreach (var grp in parManager)
            {
                var manager = grp.Key;
                var email = manager?.Email?.Trim();
                if (string.IsNullOrWhiteSpace(email)) continue;

                var nomManager = manager?.FullName ?? "Responsable";
                var lignes = grp.Select(sf => (
                    Salarie: sf.Salarie?.FullName ?? "—",
                    Formation: sf.IntituleFormation ?? "—",
                    Date: sf.DateFormation.ToString("dd/MM/yyyy"),
                    Domaine: sf.Domaine ?? "—"
                )).ToList();

                var body = BuildEmailManagerHtml(nomManager, lignes, echeance, aujourd);
                var sujet = $"[AdiPAIE] Rappel évaluation formation ({echeance}) — {lignes.Count} collaborateur(s)";

                try { sender.Send(email, sujet, body); }
                catch { /* log individuel silencieux */ }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        private static string BuildEmailManagerHtml(
            string nomManager,
            List<(string Salarie, string Formation, string Date, string Domaine)> lignes,
            string echeance,
            DateTime aujourd)
        {
            var lignesHtml = new StringBuilder();
            bool alt = false;
            foreach (var l in lignes)
            {
                var bg = alt ? "#EBF3FB" : "#FFFFFF";
                lignesHtml.Append(
                    $"<tr style='background:{bg};'>" +
                    $"<td style='padding:7px 12px;border-bottom:1px solid #dde;'>{l.Salarie}</td>" +
                    $"<td style='padding:7px 12px;border-bottom:1px solid #dde;'>{l.Formation}</td>" +
                    $"<td style='padding:7px 12px;border-bottom:1px solid #dde;'>{l.Domaine}</td>" +
                    $"<td style='padding:7px 12px;border-bottom:1px solid #dde;'>{l.Date}</td>" +
                    "</tr>");
                alt = !alt;
            }

            return $@"<!DOCTYPE html><html>
<body style='font-family:Segoe UI,Arial;font-size:14px;color:#333;margin:0;padding:24px;'>
<h2 style='color:#1F4E79;margin-bottom:4px;'>Rappel — Évaluation à froid ({echeance})</h2>
<p style='color:#555;margin-top:4px;'>Bonjour <strong>{nomManager}</strong>,</p>
<p>Les collaborateurs ci-dessous ont suivi une formation il y a environ {echeance.Replace("+", " ")}
et n'ont pas encore fait l'objet d'une évaluation à froid dans AdiPAIE.</p>
<p>Merci d'ouvrir chaque <strong>Suivi formation</strong> concerné et de renseigner
la section <em>Évaluation à froid</em> (acquis, impact poste, note globale…).</p>
<table style='border-collapse:collapse;width:100%;max-width:700px;margin:16px 0;'>
<thead>
  <tr style='background:#1F4E79;color:#fff;'>
    <th style='padding:8px 12px;text-align:left;'>Collaborateur</th>
    <th style='padding:8px 12px;text-align:left;'>Formation</th>
    <th style='padding:8px 12px;text-align:left;'>Domaine</th>
    <th style='padding:8px 12px;text-align:left;'>Date formation</th>
  </tr>
</thead>
<tbody>{lignesHtml}</tbody>
</table>
<hr style='border:none;border-top:1px solid #BDD7EE;margin:16px 0;'/>
<p style='color:#888;font-size:12px;'>Message automatique AdiPAIE — {aujourd:dd/MM/yyyy}. Ne pas répondre.</p>
</body></html>";
        }

        // ─────────────────────────────────────────────────────────────────
        private static string BuildEmailSyntheseRH(
            List<SuiviFormation> j30,
            List<SuiviFormation> j90,
            DateTime aujourd)
        {
            static string Bloc(string titre, string couleur,
                List<SuiviFormation> liste)
            {
                if (!liste.Any()) return "";
                var sb = new StringBuilder();
                sb.Append(
                    $"<h3 style='color:{couleur};'>{titre} ({liste.Count})</h3>" +
                    "<table style='border-collapse:collapse;width:100%;max-width:700px;margin-bottom:20px;'>" +
                    $"<thead><tr style='background:{couleur};color:#fff;'>" +
                    "<th style='padding:8px 12px;text-align:left;'>Salarié</th>" +
                    "<th style='padding:8px 12px;text-align:left;'>N+1</th>" +
                    "<th style='padding:8px 12px;text-align:left;'>Formation</th>" +
                    "<th style='padding:8px 12px;text-align:left;'>Date</th>" +
                    "</tr></thead><tbody>");
                bool alt = false;
                foreach (var sf in liste)
                {
                    var bg = alt ? "#F8F8F8" : "#FFFFFF";
                    sb.Append(
                        $"<tr style='background:{bg};'>" +
                        $"<td style='padding:7px 12px;border-bottom:1px solid #dde;'>{sf.Salarie?.FullName ?? "—"}</td>" +
                        $"<td style='padding:7px 12px;border-bottom:1px solid #dde;'>{sf.Salarie?.Manager?.FullName ?? "—"}</td>" +
                        $"<td style='padding:7px 12px;border-bottom:1px solid #dde;'>{sf.IntituleFormation ?? "—"}</td>" +
                        $"<td style='padding:7px 12px;border-bottom:1px solid #dde;'>{sf.DateFormation:dd/MM/yyyy}</td>" +
                        "</tr>");
                    alt = !alt;
                }
                sb.Append("</tbody></table>");
                return sb.ToString();
            }

            var body = new StringBuilder();
            body.Append(
                "<!DOCTYPE html><html><body style='font-family:Segoe UI,Arial;font-size:14px;color:#333;padding:24px;'>" +
                $"<h2 style='color:#1F4E79;'>Synthèse — Évaluations à froid en attente ({aujourd:dd/MM/yyyy})</h2>" +
                Bloc("Évaluations J+30 dues", "#1F4E79", j30) +
                Bloc("Évaluations J+90 dues", "#7D4E00", j90) +
                "<p style='color:#888;font-size:12px;'>Message automatique AdiPAIE.</p>" +
                "</body></html>");

            return body.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        private static List<string> ParseEmails(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            return raw.Split(new[] { ';', ',', ' ' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => e.Contains('@'))
                .ToList();
        }
    }
}
