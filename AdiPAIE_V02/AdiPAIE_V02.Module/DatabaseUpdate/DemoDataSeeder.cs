// =============================================================================
//  DemoDataSeeder.cs — V1.1 (mai 2026)
//
//  Seed COMPLET de données de démonstration pour tester les 6 dashboards
//  RH du module Tableaux de Bord. Tous les enregistrements créés ici ont
//  un Code/Matricule préfixé "DEMO_" → suppression triviale via le
//  Controller "Vider les données démo" (cf. DemoDataWipeController).
//
//  Contenu seedé (volumes pour test réaliste) :
//    - 6  Sites (3 stations + 1 siège + 2 dépôts)
//    - 19 Unités organisationnelles (12 BU + 4 départements + 3 segments)
//    - 1  Société d'intérim
//    - 8  Postes intérimaires
//    - 25 Intérimaires (avec dates de naissance variées)
//    - ~40 Contrats intérim répartis 2024-2026
//    - ~10 Mouvements entre sites
//
//  ⚠️ Idempotent : si DEMO_BANDIA existe déjà, on ne le recrée pas.
//      Pour repartir de zéro : utilisez le Controller "Vider données démo".
//
//  ⚠️ Garantie anti-suppression seeds réels : le wiper filtre uniquement
//      sur Code.StartsWith("DEMO_") — les rubriques de paie, paramètres,
//      catégories métier réels ne sont JAMAIS touchés.
// =============================================================================

using System;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.Budget;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.DatabaseUpdate
{
    /// <summary>
    /// Crée un jeu de données démo COMPLET pour tester les dashboards RH.
    /// Tous les Code et Matricule sont préfixés "DEMO_" pour permettre une
    /// suppression chirurgicale via <see cref="DemoDataWipe.WipeAll"/>.
    /// </summary>
    public static class DemoDataSeeder
    {
        public const string DemoPrefix = "DEMO_";

        // ─────────────────────────────────────────────────────────────────────
        public static void EnsureAll(IObjectSpace os)
        {
            if (os == null) return;
            var sites      = EnsureSites(os);
            var unites     = EnsureUnites(os, sites);
            var societe    = EnsureSocieteInterim(os);
            var postes     = EnsurePostesInterimaire(os);
            var interims   = EnsureInterimaires(os, societe);
            EnsureContrats(os, interims, sites, unites, postes);
            EnsureMouvements(os, interims, sites, unites);
            EnsureBudgetMasseSalariale(os, sites);   // V1.2 — démo Budget vs Réalisé
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SITES (3 stations + 1 siège + 2 dépôts)
        // ═════════════════════════════════════════════════════════════════════
        private static (Site bandia, Site cdb, Site mermoz, Site siege, Site depotDakar, Site depotThies)
            EnsureSites(IObjectSpace os)
        {
            var bandia      = EnsureSite(os, "DEMO_BANDIA",       "BANDIA",       TypeSite.StationService, "Route Bandia",     "Bandia");
            var cdb         = EnsureSite(os, "DEMO_CDB",          "CAP DES BICHES", TypeSite.StationService, "Cap des Biches", "Rufisque");
            var mermoz      = EnsureSite(os, "DEMO_MERMOZ",       "MERMOZ",       TypeSite.StationService, "Av. Cheikh Anta Diop", "Dakar");
            var siege       = EnsureSite(os, "DEMO_SIEGE",        "Siège ELTON",  TypeSite.Siege,          "Plateau",         "Dakar");
            var depotDakar  = EnsureSite(os, "DEMO_DEPOT_DAKAR",  "Dépôt Dakar",  TypeSite.Depot,          "Zone Industrielle", "Dakar");
            var depotThies  = EnsureSite(os, "DEMO_DEPOT_THIES",  "Dépôt Thiès",  TypeSite.Depot,          "Zone Indus. Thiès", "Thiès");
            return (bandia, cdb, mermoz, siege, depotDakar, depotThies);
        }

        private static Site EnsureSite(IObjectSpace os, string code, string nom, TypeSite type, string adresse, string ville)
        {
            var existing = os.GetObjectsQuery<Site>().Where(s => s.Code == code).FirstOrDefault();
            if (existing != null) return existing;

            var s = os.CreateObject<Site>();
            s.Code = code; s.Nom = nom; s.Type = type;
            s.Adresse = adresse; s.Ville = ville; s.Actif = true;
            return s;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  UNITÉS ORGANISATIONNELLES (12 BU stations + 4 dépts siège + 3 segments)
        // ═════════════════════════════════════════════════════════════════════
        private record UniteSeeds(
            UniteOrganisationnelle[] BUsBandia,
            UniteOrganisationnelle[] BUsCdb,
            UniteOrganisationnelle[] BUsMermoz,
            UniteOrganisationnelle dsi,
            UniteOrganisationnelle commerciale,
            UniteOrganisationnelle adminFin,
            UniteOrganisationnelle rh,
            UniteOrganisationnelle segConsommateurs,
            UniteOrganisationnelle segBtp,
            UniteOrganisationnelle segMines);

        private static UniteSeeds EnsureUnites(IObjectSpace os,
            (Site bandia, Site cdb, Site mermoz, Site siege, Site depotDakar, Site depotThies) sites)
        {
            // BU pour chaque station (4 chacune × 3 stations = 12)
            var busBandia = EnsureBUsForStation(os, sites.bandia);
            var busCdb    = EnsureBUsForStation(os, sites.cdb);
            var busMermoz = EnsureBUsForStation(os, sites.mermoz);

            // Départements du Siège (4)
            var dsi      = EnsureDepartement(os, sites.siege, "DSI",          "DSI",                    CouleurPalette.BleuClair, 0);
            var commer   = EnsureDepartement(os, sites.siege, "DIR_COMM",     "Direction Commerciale",  CouleurPalette.OrangeElton, 1);
            var adminFin = EnsureDepartement(os, sites.siege, "DIR_AF",       "Direction Admin & Fin.", CouleurPalette.NavyElton, 2);
            var rh       = EnsureDepartement(os, sites.siege, "DIR_RH",       "Direction RH",           CouleurPalette.Vert,        3);

            // Segments sous Direction Commerciale (3)
            var segCons = EnsureSegment(os, sites.siege, commer, "SEG_CONSO", "Segment Consommateurs", CouleurPalette.Jaune,  0);
            var segBtp  = EnsureSegment(os, sites.siege, commer, "SEG_BTP",   "Segment BTP",           CouleurPalette.Marron, 1);
            var segMin  = EnsureSegment(os, sites.siege, commer, "SEG_MINES", "Segment Mines",         CouleurPalette.Gris,   2);

            return new UniteSeeds(busBandia, busCdb, busMermoz, dsi, commer, adminFin, rh, segCons, segBtp, segMin);
        }

        private static UniteOrganisationnelle[] EnsureBUsForStation(IObjectSpace os, Site station)
        {
            var bu1 = EnsureBU(os, station, "BU_BOUTIQUE",    "Boutique",    CouleurPalette.OrangeElton, 0);
            var bu2 = EnsureBU(os, station, "BU_PISTE",       "Piste",       CouleurPalette.NavyElton,   1);
            var bu3 = EnsureBU(os, station, "BU_ESERVICE",    "E-Service",   CouleurPalette.BleuClair,   2);
            var bu4 = EnsureBU(os, station, "BU_ESPACE_AUTO", "Espace Auto", CouleurPalette.Vert,        3);
            return new[] { bu1, bu2, bu3, bu4 };
        }

        private static UniteOrganisationnelle EnsureBU(IObjectSpace os, Site site, string codeSuffix, string nom, CouleurPalette palette, int ordre)
        {
            var fullCode = $"{DemoPrefix}{codeSuffix}_{site.Code.Replace(DemoPrefix, "")}";
            return EnsureUnite(os, fullCode, nom, site, null, TypeUnite.BU, palette, ordre);
        }

        private static UniteOrganisationnelle EnsureDepartement(IObjectSpace os, Site site, string codeSuffix, string nom, CouleurPalette palette, int ordre)
        {
            var fullCode = $"{DemoPrefix}{codeSuffix}";
            return EnsureUnite(os, fullCode, nom, site, null, TypeUnite.Departement, palette, ordre);
        }

        private static UniteOrganisationnelle EnsureSegment(IObjectSpace os, Site site, UniteOrganisationnelle parent, string codeSuffix, string nom, CouleurPalette palette, int ordre)
        {
            var fullCode = $"{DemoPrefix}{codeSuffix}";
            return EnsureUnite(os, fullCode, nom, site, parent, TypeUnite.Segment, palette, ordre);
        }

        private static UniteOrganisationnelle EnsureUnite(IObjectSpace os, string code, string nom,
            Site site, UniteOrganisationnelle parent, TypeUnite type, CouleurPalette palette, int ordre)
        {
            var existing = os.GetObjectsQuery<UniteOrganisationnelle>().Where(u => u.Code == code).FirstOrDefault();
            if (existing != null) return existing;

            var u = os.CreateObject<UniteOrganisationnelle>();
            u.Code = code; u.Nom = nom; u.Site = site; u.Parent = parent;
            u.TypeUnite = type; u.Palette = palette; u.Ordre = ordre; u.Actif = true;
            return u;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SOCIÉTÉ D'INTÉRIM
        // ═════════════════════════════════════════════════════════════════════
        private static SocieteInterim EnsureSocieteInterim(IObjectSpace os)
        {
            const string code = "DEMO_SEN_INTERIM";
            var existing = os.GetObjectsQuery<SocieteInterim>()
                .ToList()
                .FirstOrDefault(s => (s.RaisonSociale ?? "").StartsWith(code));
            if (existing != null) return existing;

            var s = os.CreateObject<SocieteInterim>();
            // RaisonSociale est le seul champ commun — on préfixe pour identification
            s.RaisonSociale = $"{code} — SEN INTERIM SARL";
            s.Telephone     = "+221 33 123 45 67";
            return s;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  POSTES INTÉRIMAIRES (8 postes pour couvrir les cas)
        // ═════════════════════════════════════════════════════════════════════
        private static PosteInterimaire[] EnsurePostesInterimaire(IObjectSpace os)
        {
            var libelles = new[]
            {
                ("DEMO Caissier",        "Caissier station service"),
                ("DEMO Pompiste",        "Pompiste piste"),
                ("DEMO Laveur",          "Laveur Espace Auto"),
                ("DEMO Technicien",      "Technicien E-Service"),
                ("DEMO Chef Boutique",   "Chef de boutique"),
                ("DEMO Magasinier",      "Magasinier dépôt"),
                ("DEMO Comptable Aux.",  "Comptable auxiliaire (Direction)"),
                ("DEMO Assistant RH",    "Assistant RH (Direction)")
            };
            var result = new PosteInterimaire[libelles.Length];
            for (int i = 0; i < libelles.Length; i++)
            {
                var lib = libelles[i].Item1;
                var existing = os.GetObjectsQuery<PosteInterimaire>()
                    .ToList()
                    .FirstOrDefault(p => p.Libelle == lib);
                if (existing != null) { result[i] = existing; continue; }
                var p = os.CreateObject<PosteInterimaire>();
                p.Libelle = lib; p.Description = libelles[i].Item2; p.Actif = true;
                result[i] = p;
            }
            return result;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  INTÉRIMAIRES (25 personnes avec données réalistes Sénégal)
        // ═════════════════════════════════════════════════════════════════════
        private static readonly (string Nom, string Prenom, int Age)[] PersonnesDemo =
        {
            ("DIOP",     "Mamadou",  28),  ("SARR",    "Aminata",   34),
            ("FALL",     "Ibrahima", 41),  ("NDIAYE",  "Fatou",     26),
            ("BA",       "Abdoulaye",52),  ("SOW",     "Awa",       29),
            ("CISSE",    "Cheikh",   37),  ("SECK",    "Mariama",   45),
            ("KANE",     "Moussa",   23),  ("WADE",    "Khady",     31),
            ("SY",       "Ousmane",  48),  ("THIAM",   "Aïssatou",  39),
            ("MBAYE",    "Aliou",    27),  ("DIAGNE",  "Bineta",    33),
            ("GUEYE",    "Modou",    44),  ("SAMBE",   "Rokhaya",   30),
            ("DIENG",    "Babacar",  56),  ("FAYE",    "Adja",      35),
            ("CAMARA",   "Souleymane",24), ("SENE",    "Diodio",    42),
            ("SOUMARE",  "Lamine",   38),  ("BADJI",   "Yacine",    47),
            ("MENDY",    "Pierre",   29),  ("NIANG",   "Sokhna",    32),
            ("DIATTA",   "Joseph",   51)
        };

        private static Interimaire[] EnsureInterimaires(IObjectSpace os, SocieteInterim societe)
        {
            var result = new Interimaire[PersonnesDemo.Length];
            for (int i = 0; i < PersonnesDemo.Length; i++)
            {
                var p = PersonnesDemo[i];
                var matricule = $"{DemoPrefix}INT-{(i + 1):D4}";
                var existing = os.GetObjectsQuery<Interimaire>()
                    .Where(x => x.Matricule == matricule).FirstOrDefault();
                if (existing != null) { result[i] = existing; continue; }

                var inter = os.CreateObject<Interimaire>();
                inter.Matricule    = matricule;   // override l'auto-génération
                inter.Nom          = p.Nom;
                inter.Prenom       = p.Prenom;
                inter.NumeroCNI    = $"DEMO-CNI-{(i + 1):D4}";
                inter.DateNaissance = DateTime.Today.AddYears(-p.Age).AddDays(-i * 7);
                inter.Nationalite  = "Sénégalaise";
                inter.Statut       = InterimaireStatut.Disponible;
                inter.DateCreation = DateTime.Today.AddDays(-i * 30);
                result[i] = inter;
            }
            return result;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  CONTRATS INTÉRIM (~40 contrats répartis 2024-2026, avec multi-affect)
        // ═════════════════════════════════════════════════════════════════════
        private static void EnsureContrats(IObjectSpace os,
            Interimaire[] interims,
            (Site bandia, Site cdb, Site mermoz, Site siege, Site depotDakar, Site depotThies) sites,
            UniteSeeds unites,
            PosteInterimaire[] postes)
        {
            // Si on a déjà au moins 1 contrat démo, on suppose tout seedé
            var anyExisting = os.GetObjectsQuery<ContratInterim>()
                .ToList()
                .Any(c => c.Interimaire != null
                       && (c.Interimaire.Matricule ?? "").StartsWith(DemoPrefix));
            if (anyExisting) return;

            // Mapping interim → (site, unité, poste, taux, dateDebut, dureeMois, statut)
            // Variées pour tester tous les KPI dashboards
            var rnd = new Random(42); // seed fixe pour reproductibilité
            var sitesArray   = new[] { sites.bandia, sites.cdb, sites.mermoz, sites.siege, sites.depotDakar, sites.depotThies };
            var unitesArrays = new System.Collections.Generic.Dictionary<Site, UniteOrganisationnelle[]>
            {
                [sites.bandia]     = unites.BUsBandia,
                [sites.cdb]        = unites.BUsCdb,
                [sites.mermoz]     = unites.BUsMermoz,
                [sites.siege]      = new[] { unites.dsi, unites.commerciale, unites.adminFin, unites.rh },
                [sites.depotDakar] = Array.Empty<UniteOrganisationnelle>(),
                [sites.depotThies] = Array.Empty<UniteOrganisationnelle>()
            };

            int contratIdx = 0;
            foreach (var inter in interims)
            {
                int nbContrats = rnd.Next(1, 3); // 1 ou 2 contrats par intérim
                for (int c = 0; c < nbContrats; c++)
                {
                    contratIdx++;
                    var contrat = os.CreateObject<ContratInterim>();
                    var site    = sitesArray[rnd.Next(sitesArray.Length)];
                    contrat.Interimaire    = inter;
                    contrat.Site           = site;
                    contrat.PosteOccupe    = postes[rnd.Next(postes.Length)];
                    contrat.TauxJournalier = 15000m + rnd.Next(0, 14) * 5000m; // 15k à 80k FCFA
                    contrat.MotifRecours   = $"DEMO contrat {contratIdx} — surcroît d'activité";
                    contrat.TypeContrat    = ContratInterimType.PremiereMission;

                    // Date début variée 2024-2026
                    int year   = 2024 + rnd.Next(0, 3);
                    int month  = rnd.Next(1, 13);
                    int day    = rnd.Next(1, 28);
                    contrat.DateDebut = new DateTime(year, month, day);
                    int dureeMois = rnd.Next(1, 13); // 1 à 12 mois
                    contrat.DateFin = contrat.DateDebut.AddMonths(dureeMois);

                    // Statut : 30% terminé, 60% en cours, 10% résilié
                    var roll = rnd.Next(0, 100);
                    if (contrat.DateFin < DateTime.Today)
                    {
                        contrat.Statut = ContratInterimStatut.Termine;
                        contrat.DateFinReelle = contrat.DateFin;
                    }
                    else if (roll < 10)
                    {
                        contrat.Statut = ContratInterimStatut.Resilie;
                        contrat.DateFinReelle = contrat.DateDebut.AddMonths(rnd.Next(1, dureeMois));
                    }
                    else
                    {
                        contrat.Statut = ContratInterimStatut.EnCours;
                    }

                    // Ajout d'unité(s) — 1 obligatoire si le site en a, multi pour 20% des cas
                    if (unitesArrays.TryGetValue(site, out var unitesSite) && unitesSite.Length > 0)
                    {
                        var u1 = unitesSite[rnd.Next(unitesSite.Length)];
                        contrat.Unites.Add(u1);
                        // Multi-affectation : 20% des contrats commerciaux ont 2-3 segments
                        if (site == sites.siege && unitesArrays[sites.siege].Contains(unites.commerciale)
                            && rnd.Next(0, 100) < 30)
                        {
                            contrat.Unites.Add(unites.segConsommateurs);
                            if (rnd.Next(0, 100) < 50) contrat.Unites.Add(unites.segBtp);
                            if (rnd.Next(0, 100) < 30) contrat.Unites.Add(unites.segMines);
                        }
                    }
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MOUVEMENTS INTÉRIMAIRES (~10 mouvements pour Tab 3)
        // ═════════════════════════════════════════════════════════════════════
        private static void EnsureMouvements(IObjectSpace os,
            Interimaire[] interims,
            (Site bandia, Site cdb, Site mermoz, Site siege, Site depotDakar, Site depotThies) sites,
            UniteSeeds unites)
        {
            // Si déjà des mouvements démo, skip
            var anyExisting = os.GetObjectsQuery<MouvementInterimaire>()
                .ToList()
                .Any(m => m.Interimaire != null
                       && (m.Interimaire.Matricule ?? "").StartsWith(DemoPrefix));
            if (anyExisting) return;

            var rnd = new Random(123);
            var sitesArray = new[] { sites.bandia, sites.cdb, sites.mermoz, sites.siege, sites.depotDakar, sites.depotThies };

            // 10 mouvements aléatoires sur les 12 derniers mois
            for (int i = 0; i < 10; i++)
            {
                var inter   = interims[rnd.Next(interims.Length)];
                var origine = sitesArray[rnd.Next(sitesArray.Length)];
                Site dest;
                do { dest = sitesArray[rnd.Next(sitesArray.Length)]; }
                while (dest == origine);

                var mvt = os.CreateObject<MouvementInterimaire>();
                mvt.Interimaire        = inter;
                mvt.DateMouvement      = DateTime.Today.AddDays(-rnd.Next(1, 365));
                mvt.SiteOrigineV1      = origine;
                mvt.SiteDestinationV1  = dest;
                mvt.TypeMouvement      = MouvementInterimaireType.Affectation;
                mvt.Motif              = $"DEMO mouvement {i + 1} — réaffectation opérationnelle";
                mvt.ValideRH           = rnd.Next(0, 100) < 80; // 80% validés
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  V1.2 — BUDGET MASSE SALARIALE (DÉMO 2025 + 2026)
        //
        //  Génère 2 années de budgets pour permettre le comparatif N-1 :
        //    - 2025 (année N-1)  : budget de base
        //    - 2026 (année N)    : budget +5 % (inflation)
        //
        //  Pour chaque année : 12 mois × 9 rubriques :
        //    - 1 ligne globale par mois × rubrique (Site=NULL) → 108 lignes/an
        //    - + ventilation par site sur 3 stations principales pour
        //      Salaires de base + Charges patronales              →  72 lignes/an
        //
        //  Volumes : ~360 lignes (180 par année).
        //  Tous les Commentaire commencent par "DEMO_BUDGET_" pour le wipe.
        //
        //  Hypothèses budget annuel ELTON 2025 (env. 250 salariés, 480k brut moyen) :
        //    - Salaires base       : 1 050 000 000 FCFA   (52 %)
        //    - Charges patronales  :   380 000 000 FCFA   (19 %)
        //    - Primes              :   190 000 000 FCFA
        //    - 13e mois            :    95 000 000 FCFA   (100 % décembre)
        //    - Indemnités          :    75 000 000 FCFA
        //    - Avantages nature    :    95 000 000 FCFA
        //    - Gratifications      :    47 000 000 FCFA   (50 % juin + 50 % décembre)
        //    - Formation           :    28 000 000 FCFA
        //    - Recrutement         :    19 000 000 FCFA
        //    Total 2025 ≈ 1 979 000 000 FCFA
        //  Hypothèse 2026 : tous les montants × 1,05 (inflation 5 %)
        //    Total 2026 ≈ 2 078 000 000 FCFA
        // ═════════════════════════════════════════════════════════════════════
        private static void EnsureBudgetMasseSalariale(
            IObjectSpace os,
            (Site bandia, Site cdb, Site mermoz, Site siege, Site depotDakar, Site depotThies) sites)
        {
            int anneeCourante = DateTime.Today.Year;
            int anneePrecedente = anneeCourante - 1;

            // ── Budget annuel de référence (base = année N-1) ─────────────
            var totauxBaseAnneeNm1 = new (BudgetRubrique R, decimal Annuel)[]
            {
                (BudgetRubrique.SalairesBase,        1_050_000_000m),
                (BudgetRubrique.ChargesPatronales,     380_000_000m),
                (BudgetRubrique.Primes,                190_000_000m),
                (BudgetRubrique.TreiziemeMois,          95_000_000m),
                (BudgetRubrique.Indemnites,             75_000_000m),
                (BudgetRubrique.AvantagesNature,        95_000_000m),
                (BudgetRubrique.Gratifications,         47_000_000m),
                (BudgetRubrique.Formation,              28_000_000m),
                (BudgetRubrique.Recrutement,            19_000_000m),
            };

            // Coefficient d'inflation appliqué à chaque année par rapport à N-1
            const decimal CoefInflationParAn = 1.05m;

            // Seeder pour 2 années consécutives
            SeederBudgetAnnee(os, sites, anneePrecedente, totauxBaseAnneeNm1, multiplicateur: 1.00m);
            SeederBudgetAnnee(os, sites, anneeCourante,   totauxBaseAnneeNm1, multiplicateur: CoefInflationParAn);
        }

        /// <summary>
        /// Génère les ~180 lignes de budget pour une année donnée :
        ///   - 108 lignes globales (Site=NULL) × 9 rubriques × 12 mois
        ///   -  72 lignes ventilées par site (3 sites × 2 rubriques × 12 mois)
        /// Idempotent par année grâce au check sur Annee + préfixe Commentaire.
        /// </summary>
        private static void SeederBudgetAnnee(
            IObjectSpace os,
            (Site bandia, Site cdb, Site mermoz, Site siege, Site depotDakar, Site depotThies) sites,
            int annee,
            (BudgetRubrique R, decimal Annuel)[] totauxBase,
            decimal multiplicateur)
        {
            // Idempotence par année — si on trouve déjà un budget DEMO sur cette année, on skip
            bool dejaExistant = os.GetObjectsQuery<BudgetMasseSalariale>()
                .ToList()
                .Any(b => b.Annee == annee
                       && b.Commentaire != null
                       && b.Commentaire.StartsWith("DEMO_BUDGET_"));
            if (dejaExistant) return;

            // Application du multiplicateur d'inflation
            var totauxAnnee = totauxBase
                .Select(t => (t.R, Annuel: Math.Round(t.Annuel * multiplicateur, 0)))
                .ToArray();

            // ── 1. Lignes globales (Site=NULL) — mensualisation auto ──────
            foreach (var (rubrique, annuel) in totauxAnnee)
            {
                for (int mois = 1; mois <= 12; mois++)
                {
                    decimal montantMois = MensualiserBudget(rubrique, annuel, mois);
                    if (montantMois <= 0) continue;

                    var b = os.CreateObject<BudgetMasseSalariale>();
                    b.Annee = annee;
                    b.Mois = mois;
                    b.Site = null;          // Budget global non ventilé
                    b.Rubrique = rubrique;
                    b.Montant = montantMois;
                    b.Source = BudgetSource.AutoMensualise;
                    b.Commentaire = $"DEMO_BUDGET_GLOBAL_{annee}";
                }
            }

            // ── 2. Ventilation par site — Salaires + Charges seulement ────
            //   On répartit le budget annuel sur 3 sites principaux à 33 % chacun
            //   (Bandia + CDB + Mermoz). Le siège et les dépôts seront couverts
            //   en V1.2.1 quand le DAF affinera la ventilation.
            var sitesVentiles = new[] { sites.bandia, sites.cdb, sites.mermoz };
            var rubriquesVentilees = new[]
            {
                BudgetRubrique.SalairesBase,
                BudgetRubrique.ChargesPatronales
            };

            foreach (var rub in rubriquesVentilees)
            {
                decimal annuel = totauxAnnee.First(t => t.R == rub).Annuel;
                decimal partSiteAnnuel = Math.Round(annuel / sitesVentiles.Length, 0);

                foreach (var site in sitesVentiles)
                {
                    for (int mois = 1; mois <= 12; mois++)
                    {
                        decimal montantMoisSite = MensualiserBudget(rub, partSiteAnnuel, mois);
                        if (montantMoisSite <= 0) continue;

                        var b = os.CreateObject<BudgetMasseSalariale>();
                        b.Annee = annee;
                        b.Mois = mois;
                        b.Site = site;
                        b.Rubrique = rub;
                        b.Montant = montantMoisSite;
                        b.Source = BudgetSource.AutoMensualise;
                        b.Commentaire = $"DEMO_BUDGET_SITE_{site.Code}_{annee}";
                    }
                }
            }
        }

        /// <summary>
        /// Applique la règle de mensualisation propre à chaque rubrique :
        ///   - 13e mois        → 100 % en décembre
        ///   - Gratifications  → 50 % en juin + 50 % en décembre
        ///   - Recrutement     → concentré 1er semestre (60 % S1)
        ///   - Autres          → mensualisation linéaire /12
        /// </summary>
        private static decimal MensualiserBudget(BudgetRubrique rubrique, decimal annuel, int mois)
        {
            switch (rubrique)
            {
                case BudgetRubrique.TreiziemeMois:
                    return mois == 12 ? annuel : 0m;

                case BudgetRubrique.Gratifications:
                    if (mois == 6) return Math.Round(annuel / 2m, 0);
                    if (mois == 12) return Math.Round(annuel / 2m, 0);
                    return 0m;

                case BudgetRubrique.Recrutement:
                    // 60 % en S1, 40 % en S2
                    decimal s1 = Math.Round(annuel * 0.60m / 6m, 0);
                    decimal s2 = Math.Round(annuel * 0.40m / 6m, 0);
                    return mois <= 6 ? s1 : s2;

                default:
                    return Math.Round(annuel / 12m, 0);
            }
        }
    }

    /// <summary>
    /// Suppression chirurgicale des données démo (préfixe "DEMO_").
    /// Aucun risque pour les seeds réels (rubriques de paie, paramètres,
    /// catégories métier) car ils n'ont pas ce préfixe.
    /// </summary>
    public static class DemoDataWipe
    {
        public static (int sites, int unites, int contrats, int mouvements, int interims, int postes, int societes)
            WipeAll(IObjectSpace os)
        {
            int s = 0, u = 0, c = 0, m = 0, i = 0, p = 0, sc = 0;

            // V1.2 — Suppression des budgets démo (Commentaire commence par "DEMO_BUDGET_")
            //   Avant les sites, sinon FK orpheline si Site supprimé.
            var budgets = os.GetObjectsQuery<BudgetMasseSalariale>().ToList()
                .Where(x => (x.Commentaire ?? "").StartsWith("DEMO_BUDGET_"))
                .ToList();
            foreach (var x in budgets) { os.Delete(x); }

            // Ordre de suppression : enfants d'abord pour éviter les violations FK
            var mouvements = os.GetObjectsQuery<MouvementInterimaire>().ToList()
                .Where(x => x.Interimaire != null
                         && (x.Interimaire.Matricule ?? "").StartsWith(DemoDataSeeder.DemoPrefix))
                .ToList();
            foreach (var x in mouvements) { os.Delete(x); m++; }

            var contrats = os.GetObjectsQuery<ContratInterim>().ToList()
                .Where(x => x.Interimaire != null
                         && (x.Interimaire.Matricule ?? "").StartsWith(DemoDataSeeder.DemoPrefix))
                .ToList();
            foreach (var x in contrats) { os.Delete(x); c++; }

            var interims = os.GetObjectsQuery<Interimaire>().ToList()
                .Where(x => (x.Matricule ?? "").StartsWith(DemoDataSeeder.DemoPrefix))
                .ToList();
            foreach (var x in interims) { os.Delete(x); i++; }

            var postes = os.GetObjectsQuery<PosteInterimaire>().ToList()
                .Where(x => (x.Libelle ?? "").StartsWith("DEMO "))
                .ToList();
            foreach (var x in postes) { os.Delete(x); p++; }

            var societes = os.GetObjectsQuery<SocieteInterim>().ToList()
                .Where(x => (x.RaisonSociale ?? "").StartsWith(DemoDataSeeder.DemoPrefix))
                .ToList();
            foreach (var x in societes) { os.Delete(x); sc++; }

            var unites = os.GetObjectsQuery<UniteOrganisationnelle>().ToList()
                .Where(x => (x.Code ?? "").StartsWith(DemoDataSeeder.DemoPrefix))
                .ToList();
            foreach (var x in unites) { os.Delete(x); u++; }

            var sites = os.GetObjectsQuery<Site>().ToList()
                .Where(x => (x.Code ?? "").StartsWith(DemoDataSeeder.DemoPrefix))
                .ToList();
            foreach (var x in sites) { os.Delete(x); s++; }

            os.CommitChanges();
            return (s, u, c, m, i, p, sc);
        }
    }
}
