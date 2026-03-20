using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.XtraPrinting.Preview;
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
    /// Service d'arrière-plan qui vérifie quotidiennement les documents
    /// du dossier salarié qui arrivent à expiration.
    ///
    /// Envoie un email récapitulatif aux RH avec :
    ///   - Documents déjà expirés
    ///   - Documents expirant dans les 30 prochains jours
    ///
    /// Enregistrement dans Startup.cs :
    ///   services.AddHostedService<DossierExpirationRappelService>();
    /// </summary>
    public class DossierExpirationRappelService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<DossierExpirationRappelService> _log;
        private DateTime _dernierEnvoi = DateTime.MinValue;

        public DossierExpirationRappelService(
            IServiceProvider services,
            ILogger<DossierExpirationRappelService> log)
        {
            _services = services;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("DossierExpirationRappelService démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await TryRunIfDue(); }
                catch (Exception ex) { _log.LogError(ex, "Erreur DossierExpirationRappelService."); }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private async Task TryRunIfDue()
        {
            var now = DateTime.Now;
            // Déclenche à 9h chaque jour (1h après les rappels attestations)
            if (now.Hour != 9 || _dernierEnvoi.Date == now.Date) return;
            _dernierEnvoi = now;

            _log.LogInformation("Contrôle des documents expirants ({0:HH:mm}).", now);
            await Task.Run(TraiterDocumentsExpirants);
        }

        private void TraiterDocumentsExpirants()
        {
            using var scope = _services.CreateScope();
            var ospFactory = scope.ServiceProvider.GetRequiredService<IObjectSpaceFactory>();
            using var os = ospFactory.CreateObjectSpace(typeof(DossierDocument));

            var prm = ParametresPaie.TryGet(os);
            if (prm == null || !prm.EmailActif) return;

            var destinataires = ParseEmails(prm.EmailsRHAlertes);
            if (!destinataires.Any()) return;

            var aujourd = DateTime.Today;
            var dans30j = aujourd.AddDays(30);

            // Documents expirés
            var expires = os.GetObjectsQuery<DossierDocument>()
                .Where(d => d.DateExpiration.HasValue
                         && d.DateExpiration.Value.Date < aujourd)
                .OrderBy(d => d.DateExpiration)
                .ToList();

            // Documents expirant dans 30 jours
            var bientot = os.GetObjectsQuery<DossierDocument>()
                .Where(d => d.DateExpiration.HasValue
                         && d.DateExpiration.Value.Date >= aujourd
                         && d.DateExpiration.Value.Date <= dans30j)
                .OrderBy(d => d.DateExpiration)
                .ToList();

            if (!expires.Any() && !bientot.Any())
            {
                _log.LogDebug("Aucun document expirant à signaler.");
                return;
            }

            _log.LogInformation("{0} expiré(s), {1} expirant bientôt.", expires.Count, bientot.Count);

            try
            {
                var sender = prm.CreateEmailSender();
                var sujet = $"[AdiPAIE] Alerte documents dossiers salariés — {aujourd:dd/MM/yyyy}";
                var body = BuildEmailHtml(expires, bientot, aujourd);

                foreach (var dest in destinataires)
                    sender.Send(dest, sujet, body);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Échec envoi email expiration documents.");
            }
        }

        private static string BuildEmailHtml(
            List<DossierDocument> expires,
            List<DossierDocument> bientot,
            DateTime aujourd)
        {
            var sb = new StringBuilder();
            sb.Append(@"<!DOCTYPE html><html><body style='font-family:Segoe UI,Arial;font-size:14px;color:#333;'>");
            sb.Append("<h2 style='color:#1F4E79;'>Alerte — Documents dossiers salariés</h2>");

            if (expires.Any())
            {
                sb.Append($"<h3 style='color:#C00000;'>Documents expirés ({expires.Count})</h3>");
                sb.Append(BuildTable(expires, "#FCE4D6", "#C00000"));
            }

            if (bientot.Any())
            {
                sb.Append($"<h3 style='color:#7D4E00;'>Expirant dans 30 jours ({bientot.Count})</h3>");
                sb.Append(BuildTable(bientot, "#FFF2CC", "#7D4E00"));
            }

            sb.Append("<p style='color:#666;font-size:12px;margin-top:20px;'>Message automatique AdiPAIE.</p>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static string BuildTable(List<DossierDocument> docs, string bgHeader, string colorHeader)
        {
            var sb = new StringBuilder();
            sb.Append($@"<table style='border-collapse:collapse;width:100%;max-width:700px;margin-bottom:20px;'>
<thead><tr style='background:{bgHeader};'>
  <th style='padding:8px 12px;text-align:left;color:{colorHeader};'>Salarié</th>
  <th style='padding:8px 12px;text-align:left;color:{colorHeader};'>Document</th>
  <th style='padding:8px 12px;text-align:left;color:{colorHeader};'>Catégorie</th>
  <th style='padding:8px 12px;text-align:left;color:{colorHeader};'>Date expiration</th>
</tr></thead><tbody>");

            bool alt = false;
            foreach (var d in docs)
            {
                var bg = alt ? "#F8F8F8" : "#FFFFFF";
                sb.Append($@"<tr style='background:{bg};'>
  <td style='padding:7px 12px;border-bottom:1px solid #dde;'>{d.Dossier?.Salarie?.FullName ?? "—"}</td>
  <td style='padding:7px 12px;border-bottom:1px solid #dde;'>{d.Titre}</td>
  <td style='padding:7px 12px;border-bottom:1px solid #dde;'>{d.Categorie}</td>
  <td style='padding:7px 12px;border-bottom:1px solid #dde;'>{d.DateExpiration:dd/MM/yyyy}</td>
</tr>");
                alt = !alt;
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private static List<string> ParseEmails(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            return raw.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(e => e.Trim())
                      .Where(e => e.Contains('@'))
                      .ToList();
        }
    }
}
