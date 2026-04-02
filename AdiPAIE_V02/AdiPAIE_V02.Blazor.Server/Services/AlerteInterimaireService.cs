using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Blazor.Server.Services
{
    public class AlerteInterimaireService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AlerteInterimaireService> _log;
        private static readonly TimeSpan Intervalle = TimeSpan.FromHours(1);

        public AlerteInterimaireService(IServiceProvider services,
            ILogger<AlerteInterimaireService> log)
        { _services = services; _log = log; }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _log.LogInformation("AlerteInterimaireService démarré.");
            await Task.Delay(TimeSpan.FromMinutes(2), ct);
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Run(VerifierAlertes, ct); }
                catch (Exception ex) { _log.LogError(ex, "Erreur AlerteInterimaireService."); }
                await Task.Delay(Intervalle, ct);
            }
        }

        private void VerifierAlertes()
        {
            using var scope = _services.CreateScope();
            var ospFactory = scope.ServiceProvider.GetRequiredService<INonSecuredObjectSpaceFactory>();
            using var os = ospFactory.CreateNonSecuredObjectSpace(typeof(Interimaire));

            var today = DateTime.Today;
            var now = DateTime.Now;

            var contrats = os.GetObjectsQuery<ContratInterim>()
                .Where(c => c.Statut == ContratInterimStatut.EnCours).ToList();
            var inters = os.GetObjectsQuery<Interimaire>()
                .Where(i => i.Statut != InterimaireStatut.Inactif
                         && i.Statut != InterimaireStatut.Blackliste).ToList();
            var stations = os.GetObjectsQuery<StationService>()
                .Where(s => s.Actif && s.EffectifMaxGlobal > 0).ToList();
            var bus = os.GetObjectsQuery<BusinessUnitStation>()
                .Where(b => b.Actif && b.EffectifMax > 0).ToList();
            var mvts = os.GetObjectsQuery<MouvementInterimaire>()
                .Where(m => !m.ValideRH).ToList();
            var existing = os.GetObjectsQuery<AlerteInterimaire>()
                .Where(a => !a.Traitee).ToList();

            int n = 0;

            // 1 — Missions expirées
            foreach (var c in contrats.Where(c => c.DateFin < today))
                if (!Existe(existing, AlerteInterimaireType.MissionExpiree, c.Interimaire?.Oid))
                {
                    Creer(os, AlerteInterimaireType.MissionExpiree,
                        AlerteInterimaireNiveau.Urgent, c.Interimaire, c.Station,
                        $"Mission expirée le {c.DateFin:dd/MM/yyyy} — {c.Interimaire?.FullName ?? "—"} sur "
                        + (c.EstDG ? "Direction Générale"
                           : $"{c.Station?.Nom ?? "—"}{(c.BU != null ? "/" + c.BU.Libelle : "")}"));
                    c.Statut = ContratInterimStatut.Termine;
                    if (c.Interimaire != null) c.Interimaire.Statut = InterimaireStatut.Disponible;
                    n++;
                }

            // 2 — Sans affectation
            foreach (var i in inters.Where(i =>
                i.Statut == InterimaireStatut.Actif || i.Statut == InterimaireStatut.Disponible))
                if (!contrats.Any(c => c.Interimaire?.Oid == i.Oid)
                 && !Existe(existing, AlerteInterimaireType.SansAffectation, i.Oid))
                {
                    Creer(os, AlerteInterimaireType.SansAffectation,
                        AlerteInterimaireNiveau.Alerte, i, null,
                        $"{i.Matricule} {i.FullName} sans affectation active "
                        + $"(société : {i.SocieteInterim?.RaisonSociale ?? "—"}).");
                    n++;
                }

            // 3 — Doublons BU/poste
            foreach (var grp in contrats
                .Where(c => c.BU != null && c.PosteOccupe != null)
                .GroupBy(c => new { BUOid = c.BU.Oid, PosteOid = c.PosteOccupe != null ? c.PosteOccupe.Oid : (System.Guid?)null })
                .Where(g => g.Count() > 1))
            {
                var prem = grp.First();
                if (!Existe(existing, AlerteInterimaireType.DoublonStation, null, prem.Station?.Oid))
                {
                    var noms = string.Join(", ", grp.Select(c => c.Interimaire?.FullName ?? "—"));
                    Creer(os, AlerteInterimaireType.DoublonStation,
                        AlerteInterimaireNiveau.Alerte, null, prem.Station,
                        $"Doublon poste '{grp.First().PosteOccupe?.Libelle ?? "—"}'' sur "
                        + $"{prem.Station?.Nom ?? "—"}/{prem.BU?.Libelle ?? "—"} : {noms}.");
                    n++;
                }
            }

            // 4 — Sureffectif station
            foreach (var sta in stations)
            {
                var eff = contrats.Count(c => c.Station?.Oid == sta.Oid);
                if (eff > sta.EffectifMaxGlobal
                 && !Existe(existing, AlerteInterimaireType.Sureffectif, null, sta.Oid))
                {
                    Creer(os, AlerteInterimaireType.Sureffectif,
                        AlerteInterimaireNiveau.Urgent, null, sta,
                        $"Sureffectif {sta.Nom} : {eff} intérimaires (max {sta.EffectifMaxGlobal}).");
                    n++;
                }
            }

            // 4b — Sureffectif BU
            foreach (var bu in bus)
            {
                var effBU = contrats.Count(c => c.BU?.Oid == bu.Oid);
                if (effBU > bu.EffectifMax
                 && !Existe(existing, AlerteInterimaireType.Sureffectif, null, bu.Station?.Oid))
                {
                    Creer(os, AlerteInterimaireType.Sureffectif,
                        AlerteInterimaireNiveau.Urgent, null, bu.Station,
                        $"Sureffectif BU {bu.Station?.Nom ?? "—"}/{bu.Libelle} : "
                        + $"{effBU} (max {bu.EffectifMax}).");
                    n++;
                }
            }

            // 5 — Mouvements non validés > 48h
            foreach (var m in mvts.Where(m => m.DateMouvement < now.AddHours(-48)))
                if (!Existe(existing, AlerteInterimaireType.MouvementNonValide, m.Interimaire?.Oid))
                {
                    Creer(os, AlerteInterimaireType.MouvementNonValide,
                        AlerteInterimaireNiveau.Alerte, m.Interimaire, m.StationDestination,
                        $"Mouvement {m.TypeMouvement} de {m.Interimaire?.FullName ?? "—"} "
                        + $"du {m.DateMouvement:dd/MM/yyyy} non validé par RH.");
                    n++;
                }

            if (n > 0)
            {
                os.CommitChanges();
                _log.LogInformation("{0} alerte(s) intérimaire créée(s).", n);
                EnvoyerRecapEmail(ospFactory, n);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────
        private static bool Existe(List<AlerteInterimaire> lst,
            AlerteInterimaireType type, Guid? intOid, Guid? staOid = null)
            => lst.Any(a => a.TypeAlerte == type && !a.Traitee
                && (intOid == null || a.Interimaire?.Oid == intOid)
                && (staOid == null || a.Station?.Oid == staOid));

        private static void Creer(IObjectSpace os,
            AlerteInterimaireType type, AlerteInterimaireNiveau niveau,
            Interimaire interimaire, StationService station, string message)
        {
            var a = os.CreateObject<AlerteInterimaire>();
            a.TypeAlerte = type; a.Niveau = niveau;
            a.Interimaire = interimaire; a.Station = station;
            a.Message = message; a.DateAlerte = DateTime.Now; a.Traitee = false;
        }

        // ── Email via IEmailSender (sans WorkflowEmailHelper) ─────────
        private void EnvoyerRecapEmail(INonSecuredObjectSpaceFactory ospFactory, int nb)
        {
            try
            {
                using var os = ospFactory.CreateNonSecuredObjectSpace(typeof(ParametresPaie));
                var prm = ParametresPaie.TryGet(os);
                if (prm == null || !prm.EmailActif) return;

                var emails = (prm.EmailsRHAlertes ?? "")
                    .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()).Where(x => x.Contains('@')).ToList();
                if (!emails.Any()) return;

                var sender = prm.CreateEmailSender();
                var sujet = $"[AdiPAIE] {nb} alerte(s) intérimaire(s)";
                var body = $"<p><b>{nb}</b> alerte(s) détectée(s) le "
                           + $"{DateTime.Now:dd/MM/yyyy HH:mm}.<br/>"
                           + "Connectez-vous → GRH - Intérimaires → Alertes.</p>";

                foreach (var dest in emails)
                {
                    var d = dest;
                    Task.Run(() => sender.Send(d, sujet, body))
                        .ContinueWith(t =>
                            _log.LogWarning("Email alerte non envoyé à {0}.", d),
                            TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch (Exception ex) { _log.LogWarning(ex, "Récap email non envoyé."); }
        }
    }
}
