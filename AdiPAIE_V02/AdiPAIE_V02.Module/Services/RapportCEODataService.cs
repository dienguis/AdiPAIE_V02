// AdiPAIE_V02.Module/Services/RapportCEODataService.cs
// Agrège les données XPO en un RapportCEOData (primitifs purs).
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using System;
using System.Collections.Generic;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class RapportCEODataService
    {
        /// <summary>
        /// Point d'entrée unique : construit le DTO complet pour l'année/mois donnés.
        /// </summary>
        public static RapportCEOData Collecter(IObjectSpace os, int annee, int mois)
        {
            var data = new RapportCEOData { Annee = annee, Mois = mois };

            // ── Entreprise ────────────────────────────────────────
            var company = os.GetObjectsQuery<Company>().FirstOrDefault();
            if (company != null)
            {
                data.EntrepriseNom = company.RaisonSociale ?? "";
                data.EntrepriseNINEA = company.NINEA ?? "";
                data.LogoImage = company.LogoImage;
            }

            // ── Seuils depuis ParametresPaie ──────────────────────
            var param = os.GetObjectsQuery<ParametresPaie>().FirstOrDefault();
            if (param != null)
            {
                data.SeuilTurnover = (double)(param.SeuilTurnoverPct ?? 15m);
                data.SeuilAbsenteisme = (double)(param.SeuilAbsenteismePct ?? 5m);
                data.SeuilMasseSalariale = param.SeuilMasseSalariale ?? 0m;
            }

            // ── Effectifs ─────────────────────────────────────────
            var tousLesActifs = os.GetObjectsQuery<Salarie>()
                .Where(s => s.IsActif)
                .ToList();

            var tousLesSalaries = os.GetObjectsQuery<Salarie>().ToList();

            PopulateEffectif(data, tousLesActifs);
            PopulateRepartitions(data, tousLesActifs);

            // ── Masse salariale (bulletins du mois) ───────────────
            PopulateMasseSalariale(data, os, annee, mois, tousLesActifs.Count);

            // ── Turnover ──────────────────────────────────────────
            PopulateTurnover(data, os, tousLesSalaries, annee, mois);

            // ── Congés & absences ─────────────────────────────────
            PopulateConges(data, os, annee, mois, tousLesActifs.Count);

            // ── Contrats ──────────────────────────────────────────
            PopulateContrats(data, os);

            // ── Évolution 12 mois glissants ───────────────────────
            PopulateEvolution(data, os, annee, mois);

            // ── Top salaires par département ──────────────────────
            PopulateTopSalaires(data, tousLesActifs);

            // ── Alertes automatiques ──────────────────────────────
            GenererAlertes(data);

            return data;
        }

        // ══════════════════════════════════════════════════════════
        //  BLOCS D'AGRÉGATION
        // ══════════════════════════════════════════════════════════

        private static void PopulateEffectif(RapportCEOData d, List<Salarie> actifs)
        {
            d.EffectifActif = actifs.Count;
            d.NbHommes = actifs.Count(s => s.Sexe == Sexe.Masculin);
            d.NbFemmes = actifs.Count(s => s.Sexe == Sexe.Feminin);
            d.EffectifTotal = d.NbHommes + d.NbFemmes;
            d.PctHommes = d.EffectifTotal > 0 ? Math.Round(100.0 * d.NbHommes / d.EffectifTotal, 1) : 0;
            d.PctFemmes = d.EffectifTotal > 0 ? Math.Round(100.0 * d.NbFemmes / d.EffectifTotal, 1) : 0;

            var today = DateTime.Today;
            var ages = actifs
                .Where(s => s.Birthday != default && s.Birthday > DateTime.MinValue)
                .Select(s => (today - s.Birthday).TotalDays / 365.25)
                .ToList();
            d.AgeMoyen = ages.Count > 0 ? Math.Round(ages.Average(), 1) : 0;
            d.AncienneteMoyenne = actifs.Count > 0
                ? Math.Round(actifs.Average(s => (double)s.Anciennete), 1) : 0;
        }

        private static void PopulateRepartitions(RapportCEOData d, List<Salarie> actifs)
        {
            d.ParDepartement = BuildRepart(actifs,
                s => s.Departement?.Nom ?? "(non affecté)",
                s => s.SalaireBase);

            d.ParTypeContrat = BuildRepartContrat(actifs);

            d.ParCategorie = BuildRepart(actifs,
                s => s.Categories?.Intitule ?? "(non affectée)",
                s => s.SalaireBase);

            d.ParTrancheAge = BuildRepartAge(actifs);
            d.ParTrancheAnciennete = BuildRepartAnciennete(actifs);
        }

        private static void PopulateMasseSalariale(
            RapportCEOData d, IObjectSpace os, int annee, int mois, int effectifActif)
        {
            var bulletins = os.GetObjectsQuery<Bulletin>()
                .Where(b => b.Annee == annee && b.Mois == mois
                    && b.Statut != BulletinStatut.Brouillon)
                .ToList();

            if (bulletins.Count > 0)
            {
                d.MasseSalarialeBrute = bulletins.Sum(b => b.TotalGains);
                d.MasseSalarialeNette = bulletins.Sum(b => b.NetAPayer);
                d.TotalCotisationsSalariales = bulletins.Sum(b => b.TotalCotisationsSociales);
                d.TotalRetenusFiscales = bulletins.Sum(b => b.TotalRetenuesFiscales);

                // Cotisations patronales = somme des MontantEmployeur sur les lignes
                d.TotalCotisationsPatronales = bulletins
                    .SelectMany(b => b.Lignes)
                    .Where(l => l.MontantEmployeur > 0)
                    .Sum(l => l.MontantEmployeur);

                d.CoutTotalEmployeur = d.MasseSalarialeBrute + d.TotalCotisationsPatronales;

                var bruts = bulletins.Select(b => b.TotalGains).OrderBy(x => x).ToList();
                d.SalaireMoyenBrut = Math.Round(bruts.Average(), 0);
                d.SalaireMoyenNet = Math.Round(bulletins.Average(b => b.NetAPayer), 0);
                d.SalaireMedianBrut = bruts.Count > 0
                    ? bruts[bruts.Count / 2] : 0;
            }

            // Variation M-1
            int moisPrec = mois == 1 ? 12 : mois - 1;
            int anneePrec = mois == 1 ? annee - 1 : annee;
            var bulletinsPrec = os.GetObjectsQuery<Bulletin>()
                .Where(b => b.Annee == anneePrec && b.Mois == moisPrec
                    && b.Statut != BulletinStatut.Brouillon)
                .ToList();

            d.MasseSalarialeBruteMoisPrecedent = bulletinsPrec.Count > 0
                ? bulletinsPrec.Sum(b => b.TotalGains) : 0;

            d.VariationMasseSalarialePct = d.MasseSalarialeBruteMoisPrecedent > 0
                ? Math.Round((double)((d.MasseSalarialeBrute - d.MasseSalarialeBruteMoisPrecedent)
                    / d.MasseSalarialeBruteMoisPrecedent * 100), 1)
                : 0;
        }

        private static void PopulateTurnover(
            RapportCEOData d, IObjectSpace os, List<Salarie> tous, int annee, int mois)
        {
            var debutMois = new DateTime(annee, mois, 1);
            var finMois = debutMois.AddMonths(1).AddDays(-1);

            // Entrées du mois = DateEmbauche dans le mois
            var entrees = tous.Where(s =>
                s.DateEmbauche >= debutMois && s.DateEmbauche <= finMois).ToList();
            d.Entrees = entrees.Count;

            // Répartition par type contrat
            d.EntreesCDI = entrees.Count(s =>
                s.Contrats != null &&
                s.Contrats.Cast<ContratSalarie>().Any(c =>
                    c.TypeContrat == TypeContrat.CDI && c.Statut == ContratSalarieStatut.Actif));
            d.EntreesCDD = entrees.Count(s =>
                s.Contrats != null &&
                s.Contrats.Cast<ContratSalarie>().Any(c =>
                    c.TypeContrat == TypeContrat.CDD && c.Statut == ContratSalarieStatut.Actif));
            d.EntreesStage = d.Entrees - d.EntreesCDI - d.EntreesCDD;

            // Sorties du mois = DateSortie dans le mois
            var sorties = tous.Where(s =>
                s.DateSortie >= debutMois && s.DateSortie <= finMois).ToList();
            d.Sorties = sorties.Count;

            // Taux turnover mensuel = (Entrées + Sorties) / (2 × Effectif moyen)
            var effectifMoyen = d.EffectifActif > 0 ? d.EffectifActif : 1;
            d.TauxTurnover = Math.Round(
                100.0 * (d.Entrees + d.Sorties) / (2.0 * effectifMoyen), 1);

            // Taux turnover annuel (cumul depuis janvier)
            var debutAnnee = new DateTime(annee, 1, 1);
            var entreesAnnee = tous.Count(s =>
                s.DateEmbauche >= debutAnnee && s.DateEmbauche <= finMois);
            var sortiesAnnee = tous.Count(s =>
                s.DateSortie >= debutAnnee && s.DateSortie <= finMois);
            d.TauxTurnoverAnnuel = Math.Round(
                100.0 * (entreesAnnee + sortiesAnnee) / (2.0 * effectifMoyen), 1);

            // Motifs de départ
            d.RepartitionMotifDepart = sorties
                .Where(s => s.MotifDepart != null)
                .GroupBy(s => s.MotifDepart.ToString())
                .Select(g => new MotifDepartItem
                {
                    Motif = FormatMotif(g.Key),
                    Nombre = g.Count(),
                    Pourcentage = sorties.Count > 0
                        ? Math.Round(100.0 * g.Count() / sorties.Count, 1) : 0
                })
                .OrderByDescending(x => x.Nombre)
                .ToList();
        }

        private static void PopulateConges(
            RapportCEOData d, IObjectSpace os, int annee, int mois, int effectifActif)
        {
            var debutMois = new DateTime(annee, mois, 1);
            var finMois = debutMois.AddMonths(1).AddDays(-1);

            var conges = os.GetObjectsQuery<CongeDemande>().ToList();

            // En cours = chevauchement avec le mois
            d.CongesEnCours = conges.Count(c =>
                c.Statut == CongeStatut.Accordee
                && c.DateDebut <= finMois
                && c.DateFin >= debutMois);

            // Accordés ce mois (date traitement dans le mois)
            d.CongesAccordesMois = conges.Count(c =>
                c.Statut == CongeStatut.Accordee
                && c.DateTraitementRH >= debutMois
                && c.DateTraitementRH <= finMois);

            // Refusés ce mois
            d.CongesRefusesMois = conges.Count(c =>
                c.Statut == CongeStatut.Refusee
                && c.DateTraitementRH >= debutMois
                && c.DateTraitementRH <= finMois);

            // En attente (tous statuts d'attente)
            d.CongesEnAttente = conges.Count(c =>
                c.Statut == CongeStatut.EnAttenteN1
                || c.Statut == CongeStatut.EnAttenteN2
                || c.Statut == CongeStatut.Soumise);

            // Jours d'absence du mois (congés accordés chevauchant le mois)
            var congesAccordes = conges.Where(c =>
                c.Statut == CongeStatut.Accordee
                && c.DateDebut <= finMois
                && c.DateFin >= debutMois).ToList();

            d.JoursAbsenceTotalMois = (int)congesAccordes
                .Sum(c => c.DureeJours);

            // Taux absentéisme = Jours absence / (Effectif × Jours ouvrés du mois)
            int joursOuvresMois = 22; // approximation standard
            d.TauxAbsenteisme = effectifActif > 0
                ? Math.Round(100.0 * d.JoursAbsenceTotalMois / (effectifActif * joursOuvresMois), 1)
                : 0;

            // Répartition par type de congé
            var totalCongesMois = congesAccordes.Count > 0 ? congesAccordes.Count : 1;
            d.RepartitionCongesParType = congesAccordes
                .GroupBy(c => c.Type?.Libelle ?? "(inconnu)")
                .Select(g => new CongeParTypeItem
                {
                    TypeConge = g.Key,
                    NbDemandes = g.Count(),
                    JoursTotaux = g.Sum(c => c.DureeJours),
                    Pourcentage = Math.Round(100.0 * g.Count() / totalCongesMois, 1)
                })
                .OrderByDescending(x => x.NbDemandes)
                .ToList();
        }

        private static void PopulateContrats(RapportCEOData d, IObjectSpace os)
        {
            var contratsActifs = os.GetObjectsQuery<ContratSalarie>()
                .Where(c => c.Statut == ContratSalarieStatut.Actif)
                .ToList();

            d.NbCDI = contratsActifs.Count(c => c.TypeContrat == TypeContrat.CDI);
            d.NbCDD = contratsActifs.Count(c => c.TypeContrat == TypeContrat.CDD);
            d.NbStage = contratsActifs.Count(c => c.TypeContrat == TypeContrat.Stage);

            var today = DateTime.Today;
            d.CDDExpirantSous30Jours = contratsActifs.Count(c =>
                c.TypeContrat != TypeContrat.CDI
                && c.DateFin.HasValue
                && c.DateFin.Value >= today
                && c.DateFin.Value <= today.AddDays(30));

            d.CDDExpirantSous60Jours = contratsActifs.Count(c =>
                c.TypeContrat != TypeContrat.CDI
                && c.DateFin.HasValue
                && c.DateFin.Value >= today
                && c.DateFin.Value <= today.AddDays(60));
        }

        // Date minimale SQL Server (évite SqlDateTime overflow avec DateTime.MinValue)
        private static readonly DateTime SqlMinDate = new DateTime(1753, 1, 2);

        private static void PopulateEvolution(
            RapportCEOData d, IObjectSpace os, int annee, int mois)
        {
            var evEffectif = new List<EvolutionMensuelleItem>();
            var evMasse = new List<EvolutionMensuelleItem>();

            // 12 mois glissants
            for (int i = 11; i >= 0; i--)
            {
                var dt = new DateTime(annee, mois, 1).AddMonths(-i);
                int a = dt.Year, m = dt.Month;
                var finM = new DateTime(a, m, DateTime.DaysInMonth(a, m));

                // Effectif à fin de mois :
                // Actif OU (parti après finM) — on exclut les DateSortie < SqlMinDate (= jamais parti)
                int eff = os.GetObjectsQuery<Salarie>()
                    .Count(s => s.DateEmbauche <= finM
                        && (s.IsActif || s.DateSortie < SqlMinDate || s.DateSortie > finM));

                // Entrées/sorties du mois (DateSortie > SqlMinDate filtre les 0001-01-01)
                var debutM = new DateTime(a, m, 1);
                int ent = os.GetObjectsQuery<Salarie>()
                    .Count(s => s.DateEmbauche >= debutM && s.DateEmbauche <= finM);
                int sor = os.GetObjectsQuery<Salarie>()
                    .Count(s => s.DateSortie > SqlMinDate && s.DateSortie >= debutM && s.DateSortie <= finM);

                // Masse salariale du mois
                decimal masse = os.GetObjectsQuery<Bulletin>()
                    .Where(b => b.Annee == a && b.Mois == m
                        && b.Statut != BulletinStatut.Brouillon)
                    .Sum(b => (decimal?)b.TotalGains) ?? 0;

                evEffectif.Add(new EvolutionMensuelleItem
                {
                    Annee = a, Mois = m,
                    Effectif = eff, Entrees = ent, Sorties = sor,
                    MasseSalarialeBrute = masse
                });

                evMasse.Add(new EvolutionMensuelleItem
                {
                    Annee = a, Mois = m,
                    Effectif = eff,
                    MasseSalarialeBrute = masse
                });
            }

            d.EvolutionEffectif = evEffectif;
            d.EvolutionMasseSalariale = evMasse;
        }

        private static void PopulateTopSalaires(RapportCEOData d, List<Salarie> actifs)
        {
            d.TopSalaires = actifs
                .GroupBy(s => s.Departement?.Nom ?? "(non affecté)")
                .Select(g => new TopSalaireItem
                {
                    Departement = g.Key,
                    SalaireMoyenBrut = Math.Round(g.Average(s => s.SalaireBase), 0),
                    Effectif = g.Count()
                })
                .OrderByDescending(x => x.SalaireMoyenBrut)
                .Take(10)
                .ToList();
        }

        // ══════════════════════════════════════════════════════════
        //  ALERTES
        // ══════════════════════════════════════════════════════════

        private static void GenererAlertes(RapportCEOData d)
        {
            if (d.TauxTurnover > d.SeuilTurnover)
                d.Alertes.Add(new AlerteItem
                {
                    Niveau = "Danger",
                    Titre = "Turnover élevé",
                    Description = $"Le taux de turnover mensuel ({d.TauxTurnover:F1}%) "
                        + $"dépasse le seuil de {d.SeuilTurnover:F0}%."
                });

            if (d.TauxAbsenteisme > d.SeuilAbsenteisme)
                d.Alertes.Add(new AlerteItem
                {
                    Niveau = "Warning",
                    Titre = "Absentéisme élevé",
                    Description = $"Le taux d'absentéisme ({d.TauxAbsenteisme:F1}%) "
                        + $"dépasse le seuil de {d.SeuilAbsenteisme:F0}%."
                });

            if (d.SeuilMasseSalariale > 0 && d.MasseSalarialeBrute > d.SeuilMasseSalariale)
                d.Alertes.Add(new AlerteItem
                {
                    Niveau = "Warning",
                    Titre = "Masse salariale au-delà du budget",
                    Description = $"La masse salariale brute ({d.MasseSalarialeBrute:N0} FCFA) "
                        + $"dépasse le seuil budgétaire de {d.SeuilMasseSalariale:N0} FCFA."
                });

            if (d.CDDExpirantSous30Jours > 0)
                d.Alertes.Add(new AlerteItem
                {
                    Niveau = "Warning",
                    Titre = "CDD/Stages expirant bientôt",
                    Description = $"{d.CDDExpirantSous30Jours} contrat(s) expire(nt) dans les 30 prochains jours."
                });

            if (d.VariationMasseSalarialePct > 10)
                d.Alertes.Add(new AlerteItem
                {
                    Niveau = "Info",
                    Titre = "Variation masse salariale significative",
                    Description = $"La masse salariale a augmenté de {d.VariationMasseSalarialePct:F1}% par rapport au mois précédent."
                });

            if (d.CongesEnAttente > 5)
                d.Alertes.Add(new AlerteItem
                {
                    Niveau = "Info",
                    Titre = "Demandes de congé en attente",
                    Description = $"{d.CongesEnAttente} demande(s) de congé en attente de validation."
                });
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS DE RÉPARTITION
        // ══════════════════════════════════════════════════════════

        private static List<RepartitionItem> BuildRepart(
            List<Salarie> actifs, Func<Salarie, string> keySelector, Func<Salarie, decimal> masseSelector)
        {
            var total = actifs.Count;
            return actifs
                .GroupBy(keySelector)
                .Select(g => new RepartitionItem
                {
                    Libelle = g.Key,
                    Effectif = g.Count(),
                    Pourcentage = total > 0 ? Math.Round(100.0 * g.Count() / total, 1) : 0,
                    MasseSalariale = g.Sum(masseSelector)
                })
                .OrderByDescending(x => x.Effectif)
                .ToList();
        }

        private static List<RepartitionItem> BuildRepartContrat(List<Salarie> actifs)
        {
            // Basé sur le contrat actif le plus récent
            var items = new Dictionary<string, (int count, decimal masse)>
            {
                ["CDI"] = (0, 0), ["CDD"] = (0, 0), ["Stage"] = (0, 0), ["Sans contrat"] = (0, 0)
            };

            foreach (var s in actifs)
            {
                var contratActif = s.Contrats?
                    .Cast<ContratSalarie>()
                    .Where(c => c.Statut == ContratSalarieStatut.Actif)
                    .OrderByDescending(c => c.DateDebut)
                    .FirstOrDefault();

                string key = contratActif?.TypeContrat switch
                {
                    TypeContrat.CDI => "CDI",
                    TypeContrat.CDD => "CDD",
                    TypeContrat.Stage => "Stage",
                    _ => "Sans contrat"
                };

                var (count, masse) = items[key];
                items[key] = (count + 1, masse + s.SalaireBase);
            }

            var total = actifs.Count;
            return items
                .Where(kvp => kvp.Value.count > 0)
                .Select(kvp => new RepartitionItem
                {
                    Libelle = kvp.Key,
                    Effectif = kvp.Value.count,
                    Pourcentage = total > 0 ? Math.Round(100.0 * kvp.Value.count / total, 1) : 0,
                    MasseSalariale = kvp.Value.masse
                })
                .OrderByDescending(x => x.Effectif)
                .ToList();
        }

        private static List<RepartitionItem> BuildRepartAge(List<Salarie> actifs)
        {
            var today = DateTime.Today;
            var tranches = new[] { "< 25 ans", "25-34 ans", "35-44 ans", "45-54 ans", "55+ ans" };
            var counts = new int[5];
            var masses = new decimal[5];

            foreach (var s in actifs)
            {
                int age = s.Birthday != default && s.Birthday > DateTime.MinValue
                    ? (int)((today - s.Birthday).TotalDays / 365.25)
                    : 30; // défaut

                int idx = age < 25 ? 0 : age < 35 ? 1 : age < 45 ? 2 : age < 55 ? 3 : 4;
                counts[idx]++;
                masses[idx] += s.SalaireBase;
            }

            var total = actifs.Count;
            return Enumerable.Range(0, 5)
                .Where(i => counts[i] > 0)
                .Select(i => new RepartitionItem
                {
                    Libelle = tranches[i],
                    Effectif = counts[i],
                    Pourcentage = total > 0 ? Math.Round(100.0 * counts[i] / total, 1) : 0,
                    MasseSalariale = masses[i]
                })
                .ToList();
        }

        private static List<RepartitionItem> BuildRepartAnciennete(List<Salarie> actifs)
        {
            var tranches = new[] { "< 1 an", "1-2 ans", "3-5 ans", "6-10 ans", "11-20 ans", "20+ ans" };
            var counts = new int[6];
            var masses = new decimal[6];

            foreach (var s in actifs)
            {
                int anc = s.Anciennete;
                int idx = anc < 1 ? 0 : anc <= 2 ? 1 : anc <= 5 ? 2 : anc <= 10 ? 3 : anc <= 20 ? 4 : 5;
                counts[idx]++;
                masses[idx] += s.SalaireBase;
            }

            var total = actifs.Count;
            return Enumerable.Range(0, 6)
                .Where(i => counts[i] > 0)
                .Select(i => new RepartitionItem
                {
                    Libelle = tranches[i],
                    Effectif = counts[i],
                    Pourcentage = total > 0 ? Math.Round(100.0 * counts[i] / total, 1) : 0,
                    MasseSalariale = masses[i]
                })
                .ToList();
        }

        private static string FormatMotif(string raw)
        {
            return raw switch
            {
                "Demission" => "Démission",
                "Licenciement" => "Licenciement",
                "Retraite" => "Retraite",
                "FinCDD" => "Fin de CDD",
                "RuptureConventionnelle" => "Rupture conventionnelle",
                "Deces" => "Décès",
                "Autre" => "Autre",
                _ => raw
            };
        }
    }
}
