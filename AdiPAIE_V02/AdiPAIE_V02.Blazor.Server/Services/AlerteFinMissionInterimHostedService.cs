// =============================================================================
//  AlerteFinMissionInterimHostedService.cs — V1.5.2 (QW5)
//
//  BackgroundService .NET qui s'exécute en boucle sur la durée de vie du
//  process et déclenche un scan quotidien (par défaut 06:00) pour identifier
//  les intérimaires dont le contrat actif arrive à échéance dans les 30
//  prochains jours.
//
//  Pour chaque cas :
//    - Vérifie qu'aucune AlerteInterimaire FinMissionProche non traitée
//      n'existe déjà (idempotent)
//    - Crée une AlerteInterimaire (Niveau=Alerte si <30j, Urgent si <=7j)
//    - Logs structurés
//
//  Le service ne crash pas le process en cas d'erreur DB / SMTP — chaque
//  itération est wrappée try/catch.
// =============================================================================
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Blazor.Server.Services
{
    public sealed class AlerteFinMissionInterimHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AlerteFinMissionInterimHostedService> _logger;

        // Configuration : heure quotidienne du scan (06:00 par défaut, avant
        // l'arrivée de l'équipe). Modifiable plus tard via ParametresPaie.
        private static readonly TimeSpan ScanHeureQuotidienne = new(6, 0, 0);

        // Seuils de déclenchement (jours avant DateFin du ContratInterim)
        private const int SeuilAlerte = 30;  // Niveau "Alerte"
        private const int SeuilUrgent = 7;   // Niveau "Urgent"

        public AlerteFinMissionInterimHostedService(
            IServiceProvider serviceProvider,
            ILogger<AlerteFinMissionInterimHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "AlerteFinMissionInterim — service démarré. Scan quotidien à {Heure}.",
                ScanHeureQuotidienne);

            // V1.5.2 — Attente 30s avant 1er scan pour laisser XAF terminer
            // son bootstrap (sinon ArgumentException sur types non enregistrés
            // au moment du scan).
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (TaskCanceledException) { return; }

            if (stoppingToken.IsCancellationRequested) return;

            // Premier scan après le délai (utile en cas de restart serveur
            // après que l'heure quotidienne soit passée)
            await TryScanAsync(stoppingToken);

            // Boucle principale — réveil au prochain ScanHeureQuotidienne
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = ComputeDelayJusquProchainScan();
                _logger.LogDebug(
                    "AlerteFinMissionInterim — prochain scan dans {Delay}.",
                    delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException) { break; }

                if (stoppingToken.IsCancellationRequested) break;

                await TryScanAsync(stoppingToken);
            }

            _logger.LogInformation("AlerteFinMissionInterim — service arrêté.");
        }

        private TimeSpan ComputeDelayJusquProchainScan()
        {
            var now = DateTime.Now;
            var prochain = new DateTime(
                now.Year, now.Month, now.Day,
                ScanHeureQuotidienne.Hours,
                ScanHeureQuotidienne.Minutes,
                ScanHeureQuotidienne.Seconds);
            if (prochain <= now) prochain = prochain.AddDays(1);
            return prochain - now;
        }

        private async Task TryScanAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation(
                    "AlerteFinMissionInterim — scan démarré à {Heure}.", DateTime.Now);
                int crees = await ScanAndCreateAlertesAsync(stoppingToken);
                _logger.LogInformation(
                    "AlerteFinMissionInterim — scan terminé : {Nb} alerte(s) créée(s).",
                    crees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AlerteFinMissionInterim — échec du scan, on reessaie demain.");
            }
        }

        private Task<int> ScanAndCreateAlertesAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() =>
            {
                using var scope = _serviceProvider.CreateScope();
                var osFactory = scope.ServiceProvider
                    .GetRequiredService<INonSecuredObjectSpaceFactory>();

                using var os = osFactory.CreateNonSecuredObjectSpace(typeof(ContratInterim));
                int crees = 0;
                var aujourdhui = DateTime.Today;
                var seuilAlerte = aujourdhui.AddDays(SeuilAlerte);

                // Récupérer les contrats EnCours dont la fin est dans les 30 jours
                var contrats = os.GetObjectsQuery<ContratInterim>()
                    .Where(c => c.Statut == ContratInterimStatut.EnCours
                             && c.DateFin >= aujourdhui
                             && c.DateFin <= seuilAlerte)
                    .ToList();

                foreach (var contrat in contrats)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    if (contrat.Interimaire == null) continue;

                    // Idempotence : pas de doublon d'alerte non-traitée
                    var existante = os.GetObjectsQuery<AlerteInterimaire>()
                        .FirstOrDefault(a =>
                            a.Interimaire.Oid == contrat.Interimaire.Oid &&
                            a.TypeAlerte == AlerteInterimaireType.FinMissionProche &&
                            a.Traitee == false);
                    if (existante != null) continue;

                    var joursRestants = (contrat.DateFin - aujourdhui).Days;
                    var niveau = joursRestants <= SeuilUrgent
                        ? AlerteInterimaireNiveau.Urgent
                        : AlerteInterimaireNiveau.Alerte;

                    var alerte = os.CreateObject<AlerteInterimaire>();
                    alerte.Interimaire = contrat.Interimaire;
                    alerte.TypeAlerte = AlerteInterimaireType.FinMissionProche;
                    alerte.Niveau = niveau;
                    alerte.Message =
                        $"Fin de mission de {contrat.Interimaire.FullName} "
                        + $"(matricule {contrat.Interimaire.Matricule ?? "—"}) "
                        + $"prévue le {contrat.DateFin:dd/MM/yyyy} "
                        + $"({joursRestants} jour{(joursRestants > 1 ? "s" : "")} restant"
                        + $"{(joursRestants > 1 ? "s" : "")}). "
                        + "Anticiper renouvellement, prolongation, ou clôture.";
                    alerte.Station = contrat.Station;
                    alerte.DateAlerte = DateTime.Now;
                    alerte.Traitee = false;
                    alerte.TraiteParNom = "(auto-cron)";

                    crees++;
                }

                if (crees > 0) os.CommitChanges();
                return crees;
            }, stoppingToken);
        }
    }
}
