// =============================================================================
//  RecrutementDemoSeeder.cs — V1.4 (mai 2026)
//
//  Seed démo COMPLET pour le Module Recrutement (Dashboard N°12).
//  Crée :
//    - 4 référentiels (3 Motifs ouverture + 4 Sources + 4 Motifs refus cand.
//                       + 3 Motifs refus offre)
//    - 5 PosteVacant   (2 Pourvus, 2 EnRecrutement, 1 Brouillon)
//    - 30 Candidat     (avec sources variées)
//    - 50 Candidature  (workflow réaliste pipeline 7 étapes)
//    - 20 Entretien    (sur les candidatures qui ont passé l'étape entretien)
//    - 8  OffreEmploi  (issues variées : Acceptée, RefuseeCandidat, Expirée…)
//    - 2  PeriodeEssai (en cours, issues des 2 embauches)
//
//  Idempotence : code DEMO_RECRUT_* / matricule DEMO_CAND_* pour le wiper.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.Recrutement;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.DatabaseUpdate
{
    /// <summary>
    /// Seed démo du Module Recrutement V1.4. Idempotent — les enregistrements
    /// portent un préfixe "DEMO_RECRUT_" / "DEMO_CAND_" pour le wiper.
    /// </summary>
    public static class RecrutementDemoSeeder
    {
        public const string PrefixRef     = "DEMO_RECRUT_";
        public const string PrefixCand    = "DEMO_CAND_";
        public const string PrefixPoste   = "DEMO_POSTE_";

        // ─────────────────────────────────────────────────────────────────────
        public static void EnsureAll(IObjectSpace os)
        {
            if (os == null) return;

            // Si on a déjà des candidatures démo, on saute (idempotent)
            var dejaPresent = os.GetObjectsQuery<Candidature>().ToList()
                .Any(c => c.Candidat != null
                       && (c.Candidat.Matricule ?? "").StartsWith(PrefixCand));
            if (dejaPresent) return;

            var refs = EnsureReferentiels(os);
            var postes = EnsurePostes(os, refs);
            var candidats = EnsureCandidats(os, refs);
            var (candidatures, embauches) = EnsureCandidatures(os, candidats, postes, refs);
            EnsureEntretiens(os, candidatures);
            EnsureOffres(os, candidatures, refs);
            EnsurePeriodesEssai(os, embauches);

            os.CommitChanges();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  RÉFÉRENTIELS
        // ═════════════════════════════════════════════════════════════════════
        private sealed class RefBundle
        {
            public MotifOuverturePoste Croissance, Remplacement, Creation;
            public SourceRecrutement LinkedIn, Recommandation, Anem, Salon;
            public MotifRefusCandidat ManqueExp, TropLoin, Pretentions, NonConcluant;
            public MotifRefusOffre RemunerationInsuf, PasEvolution, AutreOffre;
        }

        private static RefBundle EnsureReferentiels(IObjectSpace os)
        {
            var b = new RefBundle();

            b.Croissance     = EnsureMotifOuv(os, "DEMO_RECRUT_CROISS",  "Croissance d'activité");
            b.Remplacement   = EnsureMotifOuv(os, "DEMO_RECRUT_REMPL",   "Remplacement de salarié");
            b.Creation       = EnsureMotifOuv(os, "DEMO_RECRUT_CREAT",   "Création de fonction");

            b.LinkedIn       = EnsureSource(os, "DEMO_RECRUT_LINKED",   "LinkedIn",          25_000m);
            b.Recommandation = EnsureSource(os, "DEMO_RECRUT_REC",      "Recommandation",     5_000m);
            b.Anem           = EnsureSource(os, "DEMO_RECRUT_ANEM",     "ANEM (Agence Nat. Emploi)", 0m);
            b.Salon          = EnsureSource(os, "DEMO_RECRUT_SALON",    "Salon emploi",      40_000m);

            b.ManqueExp      = EnsureMotifRefusCand(os, "DEMO_RECRUT_REF_EXP",  "Manque d'expérience");
            b.TropLoin       = EnsureMotifRefusCand(os, "DEMO_RECRUT_REF_LOIN", "Habite trop loin du site");
            b.Pretentions    = EnsureMotifRefusCand(os, "DEMO_RECRUT_REF_PRET", "Prétentions trop élevées");
            b.NonConcluant   = EnsureMotifRefusCand(os, "DEMO_RECRUT_REF_ENT",  "Entretien non concluant");

            b.RemunerationInsuf = EnsureMotifRefusOff(os, "DEMO_RECRUT_OFF_REM", "Rémunération insuffisante");
            b.PasEvolution      = EnsureMotifRefusOff(os, "DEMO_RECRUT_OFF_EVO", "Absence d'opportunités d'évolution");
            b.AutreOffre        = EnsureMotifRefusOff(os, "DEMO_RECRUT_OFF_AUT", "A préféré une autre offre");

            return b;
        }

        private static MotifOuverturePoste EnsureMotifOuv(IObjectSpace os, string code, string libelle)
        {
            var x = os.FindObject<MotifOuverturePoste>(CriteriaOperator.Parse("Code = ?", code))
                    ?? os.CreateObject<MotifOuverturePoste>();
            x.Code = code; x.Libelle = libelle; x.Actif = true;
            return x;
        }

        private static SourceRecrutement EnsureSource(IObjectSpace os, string code, string libelle, decimal cout)
        {
            var x = os.FindObject<SourceRecrutement>(CriteriaOperator.Parse("Code = ?", code))
                    ?? os.CreateObject<SourceRecrutement>();
            x.Code = code; x.Libelle = libelle; x.Actif = true; x.CoutMoyenParCandidat = cout;
            return x;
        }

        private static MotifRefusCandidat EnsureMotifRefusCand(IObjectSpace os, string code, string libelle)
        {
            var x = os.FindObject<MotifRefusCandidat>(CriteriaOperator.Parse("Code = ?", code))
                    ?? os.CreateObject<MotifRefusCandidat>();
            x.Code = code; x.Libelle = libelle; x.Actif = true;
            return x;
        }

        private static MotifRefusOffre EnsureMotifRefusOff(IObjectSpace os, string code, string libelle)
        {
            var x = os.FindObject<MotifRefusOffre>(CriteriaOperator.Parse("Code = ?", code))
                    ?? os.CreateObject<MotifRefusOffre>();
            x.Code = code; x.Libelle = libelle; x.Actif = true;
            return x;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  POSTES VACANTS
        // ═════════════════════════════════════════════════════════════════════
        private static List<PosteVacant> EnsurePostes(IObjectSpace os, RefBundle refs)
        {
            var depts = os.GetObjectsQuery<Departement>().ToList();
            var sites = os.GetObjectsQuery<Site>().Where(s => s.Actif).ToList();
            var cats = os.GetObjectsQuery<Categories>().ToList();

            var siegeOuFirst = sites.FirstOrDefault(s => (s.Code ?? "").Contains("SIEGE")) ?? sites.FirstOrDefault();
            var stationOuFirst = sites.FirstOrDefault(s => (s.Code ?? "").Contains("BANDIA")) ?? sites.FirstOrDefault();
            var depotOuFirst = sites.FirstOrDefault(s => (s.Code ?? "").Contains("DEPOT")) ?? sites.FirstOrDefault();

            var dept1 = depts.FirstOrDefault();
            var dept2 = depts.Count > 1 ? depts[1] : dept1;
            var cat1 = cats.FirstOrDefault();
            var cat2 = cats.Count > 1 ? cats[1] : cat1;
            var cat3 = cats.Count > 2 ? cats[2] : cat1;

            var anneeCour = DateTime.Today.Year;

            var liste = new List<PosteVacant>
            {
                // 1) Pourvu en mars (cycle ~50 jours)
                CreerPoste(os, "DEMO_POSTE_001", "Comptable Senior", dept1, cat2, siegeOuFirst, TypeContrat.CDI,
                    refs.Remplacement, 3, 350_000m, 500_000m,
                    new DateTime(anneeCour, 1, 15), new DateTime(anneeCour, 3, 5),
                    PosteVacantStatut.Pourvu),

                // 2) Pourvu en avril (cycle ~75 jours, signe long)
                CreerPoste(os, "DEMO_POSTE_002", "Chef de station", dept2, cat3, stationOuFirst, TypeContrat.CDI,
                    refs.Croissance, 5, 450_000m, 650_000m,
                    new DateTime(anneeCour, 1, 28), new DateTime(anneeCour, 4, 12),
                    PosteVacantStatut.Pourvu),

                // 3) En recrutement (ouvert il y a ~45 jours)
                CreerPoste(os, "DEMO_POSTE_003", "Pompiste polyvalent", dept2, cat1, stationOuFirst, TypeContrat.CDD,
                    refs.Croissance, 0, 130_000m, 175_000m,
                    DateTime.Today.AddDays(-45), default,
                    PosteVacantStatut.EnRecrutement),

                // 4) En recrutement (ouvert il y a ~70 jours = alerte rouge)
                CreerPoste(os, "DEMO_POSTE_004", "Responsable HSE", dept1, cat3, depotOuFirst, TypeContrat.CDI,
                    refs.Creation, 7, 600_000m, 850_000m,
                    DateTime.Today.AddDays(-70), default,
                    PosteVacantStatut.EnRecrutement),

                // 5) Brouillon (en cours de définition)
                CreerPoste(os, "DEMO_POSTE_005", "Stagiaire Marketing", dept1, cat1, siegeOuFirst, TypeContrat.Stage,
                    refs.Creation, 0, 75_000m, 100_000m,
                    DateTime.Today.AddDays(-7), default,
                    PosteVacantStatut.Brouillon),
            };

            return liste;
        }

        private static PosteVacant CreerPoste(
            IObjectSpace os, string code, string libelle,
            Departement dept, Categories cat, Site site, TypeContrat typeContrat,
            MotifOuverturePoste motif, int expReq,
            decimal salMin, decimal salMax,
            DateTime dateOuv, DateTime dateClot,
            PosteVacantStatut statut)
        {
            var p = os.FindObject<PosteVacant>(CriteriaOperator.Parse("Code = ?", code))
                  ?? os.CreateObject<PosteVacant>();
            p.Code = code; p.Libelle = libelle;
            p.Departement = dept; p.Categorie = cat; p.Site = site;
            p.TypeContrat = typeContrat; p.Motif = motif;
            p.ExperienceRequiseAnnees = expReq;
            p.FourchetteSalaireMin = salMin; p.FourchetteSalaireMax = salMax;
            p.DateOuverture = dateOuv;
            if (dateClot != default) p.DateCloture = dateClot;
            p.Statut = statut;
            return p;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  CANDIDATS
        // ═════════════════════════════════════════════════════════════════════
        private static readonly (string Nom, string Prenom, Sexe Sexe)[] CandidatsData =
        {
            ("DIOP",      "Awa",      Sexe.Feminin),
            ("FALL",      "Mamadou",  Sexe.Masculin),
            ("NDIAYE",    "Aïssatou", Sexe.Feminin),
            ("SARR",      "Cheikh",   Sexe.Masculin),
            ("BA",        "Fatou",    Sexe.Feminin),
            ("SOW",       "Ibrahima", Sexe.Masculin),
            ("FAYE",      "Khady",    Sexe.Feminin),
            ("DIENG",     "Modou",    Sexe.Masculin),
            ("KANE",      "Mariama",  Sexe.Feminin),
            ("THIAM",     "Ousmane",  Sexe.Masculin),
            ("CISSE",     "Bineta",   Sexe.Feminin),
            ("GUEYE",     "Pape",     Sexe.Masculin),
            ("SY",        "Ndèye",    Sexe.Feminin),
            ("SECK",      "Abdoulaye",Sexe.Masculin),
            ("CAMARA",    "Diary",    Sexe.Feminin),
            ("MBAYE",     "Lamine",   Sexe.Masculin),
            ("LO",        "Astou",    Sexe.Feminin),
            ("SAMB",      "Demba",    Sexe.Masculin),
            ("DIA",       "Coumba",   Sexe.Feminin),
            ("BARRY",     "Tijaan",   Sexe.Masculin),
            ("BADJI",     "Aminata",  Sexe.Feminin),
            ("DIATTA",    "Boubacar", Sexe.Masculin),
            ("DIALLO",    "Hawa",     Sexe.Feminin),
            ("WADE",      "Saliou",   Sexe.Masculin),
            ("DIAGNE",    "Sokhna",   Sexe.Feminin),
            ("SENE",      "Alioune",  Sexe.Masculin),
            ("TOURE",     "Mame",     Sexe.Feminin),
            ("DRAME",     "Abdou",    Sexe.Masculin),
            ("KEITA",     "Adja",     Sexe.Feminin),
            ("DABO",      "Ismaïla",  Sexe.Masculin),
        };

        private static List<Candidat> EnsureCandidats(IObjectSpace os, RefBundle refs)
        {
            var sources = new[] { refs.LinkedIn, refs.Recommandation, refs.Anem, refs.Salon };
            var rnd = new Random(42);
            var anneeCour = DateTime.Today.Year;
            var liste = new List<Candidat>();

            for (int i = 0; i < CandidatsData.Length; i++)
            {
                var (nom, prenom, sexe) = CandidatsData[i];
                var matricule = $"DEMO_CAND_{(i + 1):D3}";

                var c = os.FindObject<Candidat>(CriteriaOperator.Parse("Matricule = ?", matricule))
                        ?? os.CreateObject<Candidat>();
                c.Matricule = matricule;
                c.Nom = nom; c.Prenom = prenom; c.Sexe = sexe;
                c.DateNaissance = new DateTime(1980 + rnd.Next(20), 1 + rnd.Next(12), 1 + rnd.Next(28));
                c.Email = $"{prenom.ToLower()}.{nom.ToLower()}@example.sn";
                c.Telephone = $"77{rnd.Next(1000000, 9999999)}";
                c.Adresse = "Dakar";
                c.ExperienceAnnees = rnd.Next(0, 12);
                c.Source = sources[rnd.Next(sources.Length)];
                c.DateInscription = new DateTime(anneeCour, 1, 1).AddDays(rnd.Next(0, 100));
                c.CompetencesCles = "Comptabilité, Excel, SAP";
                liste.Add(c);
            }
            return liste;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  CANDIDATURES
        // ═════════════════════════════════════════════════════════════════════
        /// <summary>Crée 50 candidatures réparties sur les 5 postes avec workflow réaliste.</summary>
        private static (List<Candidature> all, List<Candidature> embauches)
            EnsureCandidatures(IObjectSpace os, List<Candidat> candidats, List<PosteVacant> postes, RefBundle refs)
        {
            var rnd = new Random(123);
            var all = new List<Candidature>();
            var embauches = new List<Candidature>();
            var anneeCour = DateTime.Today.Year;

            // Distribution : 12 cand pour P1 (Pourvu), 14 pour P2 (Pourvu),
            // 13 pour P3 (EnRecrutement), 10 pour P4 (EnRecrutement), 1 pour P5 (Brouillon)
            var distribution = new[] { 12, 14, 13, 10, 1 };
            int idxCand = 0;

            for (int p = 0; p < postes.Count; p++)
            {
                var poste = postes[p];
                int nbCand = distribution[p];

                for (int i = 0; i < nbCand && idxCand < candidats.Count * 2; i++, idxCand++)
                {
                    var candidat = candidats[idxCand % candidats.Count];
                    var matriculeCand = $"DEMO_CAND_{(idxCand + 1):D3}";
                    var c = os.CreateObject<Candidature>();
                    c.Candidat = candidat;
                    c.Poste = poste;
                    c.SourcePourCePoste = candidat.Source;
                    c.DateSoumission = poste.DateOuverture.AddDays(rnd.Next(0, 30));

                    // Statut selon le rang dans la liste pour le poste
                    var statut = DeterminerStatut(p, i, nbCand, rnd);
                    c.Statut = statut;

                    if ((int)statut >= (int)CandidatureStatut.OffreAcceptee)
                    {
                        c.DateDecision = c.DateSoumission.AddDays(rnd.Next(20, 60));
                        c.NoteFinale = 4m + (decimal)(rnd.NextDouble() * 1.0); // 4-5
                    }
                    else if ((int)statut >= (int)CandidatureStatut.EntretienFait)
                    {
                        c.NoteFinale = 3m + (decimal)(rnd.NextDouble() * 2.0); // 3-5
                    }

                    if (statut == CandidatureStatut.Refuse)
                    {
                        c.MotifRefusCandidat = ChoisirMotifRefusCand(rnd, refs);
                        c.DateDecision = c.DateSoumission.AddDays(rnd.Next(5, 20));
                    }

                    if (statut == CandidatureStatut.Embauche)
                    {
                        embauches.Add(c);
                    }

                    all.Add(c);
                }
            }

            return (all, embauches);
        }

        private static CandidatureStatut DeterminerStatut(int posteIdx, int candIdx, int total, Random rnd)
        {
            // Postes 0-1 (Pourvus) : 1 Embauche + workflow varié
            if (posteIdx <= 1)
            {
                if (candIdx == 0) return CandidatureStatut.Embauche;
                if (candIdx == 1) return CandidatureStatut.OffreAcceptee;
                if (candIdx <= 3) return CandidatureStatut.OffreProposee;
                if (candIdx <= 6) return CandidatureStatut.EntretienFait;
                if (candIdx <= 9) return CandidatureStatut.Refuse;
                return CandidatureStatut.PreSelection;
            }

            // Postes 2-3 (EnRecrutement) : pas d'embauche
            if (posteIdx <= 3)
            {
                if (candIdx == 0) return CandidatureStatut.OffreProposee;
                if (candIdx <= 2) return CandidatureStatut.EntretienFait;
                if (candIdx <= 4) return CandidatureStatut.EntretienPlanifie;
                if (candIdx <= 7) return CandidatureStatut.PreSelection;
                if (candIdx <= 9) return CandidatureStatut.Recue;
                return CandidatureStatut.Refuse;
            }

            // Poste 4 (Brouillon) : 1 candidature initiale
            return CandidatureStatut.Recue;
        }

        private static MotifRefusCandidat ChoisirMotifRefusCand(Random rnd, RefBundle refs)
        {
            var motifs = new[] { refs.ManqueExp, refs.TropLoin, refs.Pretentions, refs.NonConcluant };
            return motifs[rnd.Next(motifs.Length)];
        }

        // ═════════════════════════════════════════════════════════════════════
        //  ENTRETIENS
        // ═════════════════════════════════════════════════════════════════════
        private static void EnsureEntretiens(IObjectSpace os, List<Candidature> candidatures)
        {
            var rnd = new Random(456);
            var sals = os.GetObjectsQuery<Salarie>().ToList()
                .Where(s => (s.Matricule ?? "").Length > 0)
                .Take(5)
                .ToList();

            // On crée 1-2 entretiens pour chaque candidature qui a passé EntretienPlanifie
            var elig = candidatures
                .Where(c => (int)c.Statut >= (int)CandidatureStatut.EntretienPlanifie
                         && (int)c.Statut < 90)
                .Take(15)
                .ToList();

            int total = 0;
            foreach (var c in elig)
            {
                int nbEntr = (int)c.Statut >= (int)CandidatureStatut.EntretienFait ? 2 : 1;
                for (int i = 0; i < nbEntr && total < 20; i++, total++)
                {
                    var e = os.CreateObject<Entretien>();
                    e.Candidature = c;
                    e.DateEntretien = c.DateSoumission.AddDays(7 + i * 7 + rnd.Next(0, 5));
                    e.Type = i == 0 ? TypeEntretien.RH : TypeEntretien.Manager;
                    e.Intervieweur = sals.Count > 0 ? sals[rnd.Next(sals.Count)] : null;
                    e.Lieu = i == 0 ? "Visio Teams" : "Salle Bandia, Siège";
                    e.Note = 3m + (decimal)(rnd.NextDouble() * 2.0);
                    e.Avis = e.Note >= 4m ? AvisEntretien.Favorable
                           : e.Note >= 3m ? AvisEntretien.Mitige
                           : AvisEntretien.Defavorable;
                    e.PointsForts = "Profil intéressant, motivation visible.";
                    e.PointsFaibles = "Manque d'expérience sur SAP.";
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  OFFRES D'EMPLOI
        // ═════════════════════════════════════════════════════════════════════
        private static void EnsureOffres(IObjectSpace os, List<Candidature> candidatures, RefBundle refs)
        {
            var rnd = new Random(789);

            // Toutes les candidatures qui ont au moins atteint OffreProposee reçoivent une OffreEmploi
            var elig = candidatures
                .Where(c => (int)c.Statut >= (int)CandidatureStatut.OffreProposee
                         && (int)c.Statut < 90)
                .Take(8)
                .ToList();

            foreach (var c in elig)
            {
                var o = os.CreateObject<OffreEmploi>();
                o.Candidature = c;
                o.DateProposition = c.DateSoumission.AddDays(20 + rnd.Next(10));
                o.DateValiditeFin = o.DateProposition.AddDays(15);

                // Salaire proposé : milieu de la fourchette du poste
                if (c.Poste != null && c.Poste.FourchetteSalaireMax > 0)
                {
                    var milieu = (c.Poste.FourchetteSalaireMin + c.Poste.FourchetteSalaireMax) / 2m;
                    o.SalaireProposeMensuel = Math.Round(milieu / 1000m) * 1000m;
                }
                else o.SalaireProposeMensuel = 350_000m;
                o.AvantagesMensuels = 50_000m;
                o.DatePriseDePoste = o.DateProposition.AddDays(30);

                if (c.Statut == CandidatureStatut.Embauche || c.Statut == CandidatureStatut.OffreAcceptee)
                {
                    o.Statut = OffreEmploiStatut.Acceptee;
                    o.DateReponse = o.DateProposition.AddDays(rnd.Next(2, 10));
                }
                else if (c.Statut == CandidatureStatut.OffreProposee)
                {
                    // Mix : 1 envoyée + 1 refusée + 1 préparée
                    int dice = rnd.Next(3);
                    if (dice == 0)
                    {
                        o.Statut = OffreEmploiStatut.Envoyee;
                    }
                    else if (dice == 1)
                    {
                        o.Statut = OffreEmploiStatut.RefuseeCandidat;
                        o.MotifRefus = new[] { refs.RemunerationInsuf, refs.PasEvolution, refs.AutreOffre }[rnd.Next(3)];
                        o.DateReponse = o.DateProposition.AddDays(rnd.Next(3, 12));
                    }
                    else
                    {
                        o.Statut = OffreEmploiStatut.Preparee;
                    }
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  PÉRIODES D'ESSAI
        // ═════════════════════════════════════════════════════════════════════
        private static void EnsurePeriodesEssai(IObjectSpace os, List<Candidature> embauches)
        {
            var sals = os.GetObjectsQuery<Salarie>().ToList()
                .Where(s => s.DateEmbauche != default)
                .OrderBy(s => s.Matricule)
                .Take(2)
                .ToList();
            if (sals.Count == 0) return;

            for (int i = 0; i < embauches.Count && i < sals.Count; i++)
            {
                var c = embauches[i];
                var s = sals[i];
                var pe = os.CreateObject<PeriodeEssai>();
                pe.Salarie = s;
                pe.CandidatureOrigine = c;
                pe.DateDebut = c.DateDecision != default ? c.DateDecision : DateTime.Today.AddDays(-30);
                pe.DureeMois = 3;
                pe.DateFinPrevue = pe.DateDebut.AddMonths(3);
                pe.Statut = PeriodeEssaiStatut.EnCours;
                pe.AppreciationManager = "Intégration en cours, retours positifs.";
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  WIPE
        // ═════════════════════════════════════════════════════════════════════
        public static (int postes, int candidats, int candidatures, int entretiens, int offres, int periodes, int refs)
            WipeAll(IObjectSpace os)
        {
            int p = 0, c = 0, ca = 0, e = 0, o = 0, pe = 0, r = 0;

            // Ordre : enfants d'abord
            var pesDemo = os.GetObjectsQuery<PeriodeEssai>().ToList()
                .Where(x => x.CandidatureOrigine != null
                         && x.CandidatureOrigine.Candidat != null
                         && (x.CandidatureOrigine.Candidat.Matricule ?? "").StartsWith(PrefixCand))
                .ToList();
            foreach (var x in pesDemo) { os.Delete(x); pe++; }

            var offresDemo = os.GetObjectsQuery<OffreEmploi>().ToList()
                .Where(x => x.Candidature != null
                         && x.Candidature.Candidat != null
                         && (x.Candidature.Candidat.Matricule ?? "").StartsWith(PrefixCand))
                .ToList();
            foreach (var x in offresDemo) { os.Delete(x); o++; }

            var entrDemo = os.GetObjectsQuery<Entretien>().ToList()
                .Where(x => x.Candidature != null
                         && x.Candidature.Candidat != null
                         && (x.Candidature.Candidat.Matricule ?? "").StartsWith(PrefixCand))
                .ToList();
            foreach (var x in entrDemo) { os.Delete(x); e++; }

            var candDemo = os.GetObjectsQuery<Candidature>().ToList()
                .Where(x => x.Candidat != null
                         && (x.Candidat.Matricule ?? "").StartsWith(PrefixCand))
                .ToList();
            foreach (var x in candDemo) { os.Delete(x); ca++; }

            var candidatsDemo = os.GetObjectsQuery<Candidat>().ToList()
                .Where(x => (x.Matricule ?? "").StartsWith(PrefixCand))
                .ToList();
            foreach (var x in candidatsDemo) { os.Delete(x); c++; }

            var postesDemo = os.GetObjectsQuery<PosteVacant>().ToList()
                .Where(x => (x.Code ?? "").StartsWith(PrefixPoste))
                .ToList();
            foreach (var x in postesDemo) { os.Delete(x); p++; }

            // Référentiels
            var motOuv = os.GetObjectsQuery<MotifOuverturePoste>().ToList()
                .Where(x => (x.Code ?? "").StartsWith(PrefixRef)).ToList();
            foreach (var x in motOuv) { os.Delete(x); r++; }

            var srcs = os.GetObjectsQuery<SourceRecrutement>().ToList()
                .Where(x => (x.Code ?? "").StartsWith(PrefixRef)).ToList();
            foreach (var x in srcs) { os.Delete(x); r++; }

            var motRef = os.GetObjectsQuery<MotifRefusCandidat>().ToList()
                .Where(x => (x.Code ?? "").StartsWith(PrefixRef)).ToList();
            foreach (var x in motRef) { os.Delete(x); r++; }

            var motOff = os.GetObjectsQuery<MotifRefusOffre>().ToList()
                .Where(x => (x.Code ?? "").StartsWith(PrefixRef)).ToList();
            foreach (var x in motOff) { os.Delete(x); r++; }

            os.CommitChanges();
            return (p, c, ca, e, o, pe, r);
        }
    }
}
