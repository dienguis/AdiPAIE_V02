// Module/Services/SeedService.cs
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public class SeedOptions
    {
        public bool Echelons { get; set; } = true;
        public bool GroupesTypes { get; set; } = true;
        public bool Comptes { get; set; } = true;
        public bool Rubriques { get; set; } = true;
        // V1.7.2 — Active la création des barèmes TRIMF (Mensuel+Annuel) et IR DPP
        public bool Baremes { get; set; } = true;
        public bool JeuDemo { get; set; } = false;
        public string CodeBaremeTRIMF { get; set; } = "TRIMF_2026";
        public int AnneeBaremes { get; set; } = 0; // 0 = année courante
    }

    public static class SeedService
    {
        public static void Run(IObjectSpace os, SeedOptions opt)
        {
            // ─────────────────────────────────────────────────────────
            // V1.7.2 — Seed COMPLET du référentiel paie ELTON, aligné
            // 1:1 avec le bloc équivalent de l'Updater. Permet de relancer
            // le seed à la demande (cf. RechargerReferentielController)
            // sans dépendre du DatabaseVersionMismatch.
            //
            // Liste des rubriques créées (26) :
            //   Bruts        : SB, 13EME, GRATIF, SURSAL, CONGE_PAYE, ANC,
            //                  LOGT, PRIME_GEN, INDEM_GEN_NON_IMP, INDEM_GEN_IMP,
            //                  TRANS, HS25, HS50, HS100, AV_NAT_VEH, AV_TEL
            //   Cotisations  : IPRES_RG, IPRES_RC, CSS_AT, CSS_AF
            //   Fiscales     : TRIMF, IR, CFCE
            //   Autres ret.  : PRET, AVANCE_SAL
            // ─────────────────────────────────────────────────────────

            // Convention + catégories de base
            var conv = EnsureConvention(os, "CONV_PET", "CONVENTION PETROLE");
            var catAgent = EnsureCategorie(os, conv, "Non Cadre");
            var catCadre = EnsureCategorie(os, conv, "Cadre");

            if (opt.Echelons)
                EnsureEchelonsFromTable(os, catAgent, catCadre);

            // ── Groupes + Types (déclarés au niveau méthode) ──────────
            BusinessObjects.GroupeImpressionRef gSalaireBrut = null, gCotSocial = null,
                                                gRetFiscal = null, gAutre = null;
            RubriqueTypeRef tBrute = null, tIndImpos = null, tIndNonImp = null;
            RubriqueTypeRef tCotSoc = null, tCotFis = null, tRetenue = null;
            RubriqueTypeRef tAvNatureImpos = null;

            if (opt.GroupesTypes)
            {
                gSalaireBrut = EnsureGroupe(os, "Salaire brut (1)");
                gCotSocial = EnsureGroupe(os, "Total Cotisations Sociales");
                gRetFiscal = EnsureGroupe(os, "Total Retenues Fiscales");
                gAutre = EnsureGroupe(os, "Total Autres Retenues");

                tBrute = EnsureTypeRef(os, "BRUTE", "Éléments bruts", gSalaireBrut,
                    RubriqueTypeCalcul.Gain, SensAssiette.Plus, bf: true, bs: true);
                tIndImpos = EnsureTypeRef(os, "INDEM_IMPOSA", "Indemnités imposables", gSalaireBrut,
                    RubriqueTypeCalcul.Gain, SensAssiette.Plus, bf: true, bs: true);
                tIndNonImp = EnsureTypeRef(os, "INDEM_NON_IMPOSA", "Indemnités non imposables", gSalaireBrut,
                    RubriqueTypeCalcul.Gain, SensAssiette.Plus, bf: false, bs: true);
                // V1.7.2 — Code en MAJUSCULES (regex [A-Z0-9_]{2,20}).
                // Ancien code "AvNatImpos" rejeté par la validation.
                tAvNatureImpos = EnsureTypeRef(os, "AV_NAT_IMPOS", "Av Nature Impos", gSalaireBrut,
                    RubriqueTypeCalcul.Gain, SensAssiette.Plus, bf: true, bs: false);

                // V1.8.1 — Av Nature Non Imposable (cas rare : cadeaux d'entreprise
                // sous seuil exonéré, paniers repas dans certaines limites CGI).
                // bf: false, bs: false → n'entre ni dans le brut fiscal ni social.
                // Le code TypeRef "AV_NAT_NON_IMP" permet la détection automatique
                // par notre helper EstAvantageEnNature (prefix "AV_NAT").
                EnsureTypeRef(os, "AV_NAT_NON_IMP", "Av Nature Non Impos", gSalaireBrut,
                    RubriqueTypeCalcul.Gain, SensAssiette.Plus, bf: false, bs: false);
                tCotSoc = EnsureTypeRef(os, "COTSOC", "Cotisations sociales", gCotSocial,
                    RubriqueTypeCalcul.Retenue, SensAssiette.Moins, bf: false, bs: false);
                tCotFis = EnsureTypeRef(os, "COTFISC", "Cotisation fiscales", gRetFiscal,
                    RubriqueTypeCalcul.Retenue, SensAssiette.Moins, bf: false, bs: false);
                tRetenue = EnsureTypeRef(os, "RETENUE", "Retenues", gAutre,
                    RubriqueTypeCalcul.Retenue, SensAssiette.Moins, bf: false, bs: false);
            }

            // ── Comptes (déclarés au niveau méthode) ──────────────────
            PlanComptable c661100 = null, c663110 = null, c431300 = null, c431310 = null,
                          c447100 = null, c447200 = null, c612450 = null, c612530 = null,
                          c421100 = null, c664100 = null, c272800 = null, c421000 = null;
            if (opt.Comptes)
            {
                c661100 = EnsureCompte(os, "661100", "Appointements & salaires");
                c663110 = EnsureCompte(os, "663110", "Indemnités de logement");
                c431300 = EnsureCompte(os, "432100", "IPRES - Régime Général (tiers)");
                c431310 = EnsureCompte(os, "432200", "IPRES - Régime Cadre (tiers)");
                c447100 = EnsureCompte(os, "447100", "IRPP");
                c447200 = EnsureCompte(os, "447200", "TRIMF");
                c612450 = EnsureCompte(os, "631100", "CSS - Accident de travail");
                c612530 = EnsureCompte(os, "631200", "CSS - Allocation familiale");
                c421100 = EnsureCompte(os, "422000", "Personnel - Rémunérations dues");
                c664100 = EnsureCompte(os, "664100", "CFCE");
                c272800 = EnsureCompte(os, "272800", "Prêts");
                c421000 = EnsureCompte(os, "421000", "Avance sur Salaire");
            }

            // ─────────────────────────────────────────────────────────
            // COMMIT intermédiaire — garantit que les TypeRef et Comptes
            // sont en DB AVANT d'être référencés par les Rubriques.
            // ─────────────────────────────────────────────────────────
            if (opt.GroupesTypes || opt.Comptes)
                os.CommitChanges();

            // ── Rubriques (26 — liste alignée 1:1 avec l'Updater) ────
            if (opt.Rubriques)
            {
                // Fallback : si opt.GroupesTypes était false, requêter depuis la DB
                if (tBrute == null)
                    tBrute = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(x => x.Code == "BRUTE");
                if (tIndImpos == null)
                    tIndImpos = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(x => x.Code == "INDEM_IMPOSA");
                if (tIndNonImp == null)
                    tIndNonImp = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(x => x.Code == "INDEM_NON_IMPOSA");
                if (tAvNatureImpos == null)
                {
                    // V1.7.2 — Nouveau code MAJ ; fallback sur ancien code mixte
                    tAvNatureImpos = os.GetObjectsQuery<RubriqueTypeRef>()
                        .FirstOrDefault(x => x.Code == "AV_NAT_IMPOS" || x.Code == "AvNatImpos");
                }
                if (tCotSoc == null)
                    tCotSoc = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(x => x.Code == "COTSOC");
                if (tCotFis == null)
                    tCotFis = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(x => x.Code == "COTFISC");
                if (tRetenue == null)
                    tRetenue = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(x => x.Code == "RETENUE");

                // Garde : TypeRef essentiels obligatoires
                if (tBrute == null || tIndImpos == null || tCotSoc == null || tCotFis == null)
                {
                    throw new InvalidOperationException(
                        "Impossible de créer les rubriques : RubriqueTypeRef requis " +
                        "introuvables (BRUTE, INDEM_IMPOSA, COTSOC, COTFISC). " +
                        "Activer opt.GroupesTypes dans le seed.");
                }

                // ── BLOC BRUT (ordre 1-70) ────────────────────────────
                EnsureRubrique(os, "SB", "Salaire de base", tBrute,
                    ordre: 1, canon: RubriqueCanonique.SalaireDeBase,
                    debitDefaut: c661100, creditDefaut: c421100);

                EnsureRubrique(os, "SURSAL", "Sursalaire", tBrute,
                    ordre: 20, canon: RubriqueCanonique.Sursalaire,
                    debitDefaut: c661100, creditDefaut: c421100);

                EnsureRubrique(os, "CONGE_PAYE", "Indemnité congés payés", tIndImpos,
                    ordre: 23, debitDefaut: c661100, creditDefaut: c421100);

                // V1.8 — Rubrique distincte demandée par RH (juin 2026) pour
                // le RACHAT de congés non pris en cours de carrière (≠ congé
                // pris physiquement). Permet de distinguer dans le reporting :
                //   - CONGE_PAYE = allocation versée quand le salarié part en congé
                //   - ICCP       = compensation monétaire des jours non pris
                //                  (rachat ponctuel OU ICCP de fin de contrat)
                // Mêmes cotisations sociales/fiscales que CONGE_PAYE (tIndImpos).
                EnsureRubrique(os, "ICCP", "Indemnité de congés compensatrice", tIndImpos,
                    ordre: 24, debitDefaut: c661100, creditDefaut: c421100);

                EnsureRubrique(os, "ANC", "Prime d'ancienneté", tBrute,
                    ordre: 30, canon: RubriqueCanonique.PrimeAnciennete,
                    debitDefaut: c661100, creditDefaut: c421100);

                EnsureRubrique(os, "LOGT", "Indemnité de logement", tIndImpos,
                    ordre: 60, canon: RubriqueCanonique.IndemniteLogement,
                    debitDefaut: c663110, creditDefaut: c421100);

                // V1.7.2 — 13ième mois (canon 700)
                EnsureRubrique(os, "13EME", "13e mois", tBrute,
                    ordre: 70, canon: RubriqueCanonique.TreiziemeMois,
                    debitDefaut: c661100, creditDefaut: c421100);

                // V1.7.2 — Gratification (canon 710)
                EnsureRubrique(os, "GRATIF", "Gratification", tBrute,
                    ordre: 75, canon: RubriqueCanonique.Gratification,
                    debitDefaut: c661100, creditDefaut: c421100);

                // ── BLOC INDEMNITÉS génériques (ordre 80-83) ─────────
                EnsureRubrique(os, "PRIME_GEN", "Prime générique", tIndImpos,
                    ordre: 80, debitDefaut: c661100, creditDefaut: c421100);

                if (tIndNonImp != null)
                {
                    EnsureRubrique(os, "INDEM_GEN_NON_IMP", "Indemnité générique Non Imposable", tIndNonImp,
                        ordre: 81, debitDefaut: c661100, creditDefaut: c421100);
                    EnsureRubrique(os, "TRANS", "Prime de transport", tIndNonImp,
                        ordre: 83, canon: RubriqueCanonique.PrimeTransport,
                        debitDefaut: c661100, creditDefaut: c421100);
                }

                EnsureRubrique(os, "INDEM_GEN_IMP", "Indemnité générique Imposable", tIndImpos,
                    ordre: 82, debitDefaut: c661100, creditDefaut: c421100);

                // ── HEURES SUPPLÉMENTAIRES (ordre 120-122) ───────────
                EnsureRubrique(os, "HS25", "Heures sup 25%", tBrute,
                    ordre: 120, taux1: 25m, debitDefaut: c661100, creditDefaut: c421100);
                EnsureRubrique(os, "HS50", "Heures sup 50%", tBrute,
                    ordre: 121, taux1: 50m, debitDefaut: c661100, creditDefaut: c421100);
                EnsureRubrique(os, "HS100", "Heures sup 100%", tBrute,
                    ordre: 122, taux1: 100m, debitDefaut: c661100, creditDefaut: c421100);

                // ── AVANTAGES EN NATURE (ordre 180-185) ──────────────
                // V1.8.1 — Tous les avantages en nature doivent utiliser le
                // TypeRef AV_NAT_IMPOS pour que :
                //   - ils entrent dans le brut fiscal (IR/TRIMF imposable)
                //   - ils n'entrent PAS dans le brut social (pas IPRES/CSS)
                //   - ils n'entrent PAS dans le Net à payer (pas de cash)
                if (tAvNatureImpos != null)
                {
                    EnsureRubrique(os, "AV_NAT_VEH", "Avantage en nature - véhicule", tAvNatureImpos,
                        ordre: 180, canon: RubriqueCanonique.AvantageNatureVehicule,
                        debitDefaut: c661100, creditDefaut: c421100);

                    // V1.8.1 — AV_TEL passe de tBrute → tAvNatureImpos
                    // Avant : téléphone était dans le Net à payer à tort.
                    EnsureRubrique(os, "AV_TEL", "Avantage en nature - téléphone", tAvNatureImpos,
                        ordre: 182, debitDefaut: c661100, creditDefaut: c421100);
                }

                // ── COTISATIONS SOCIALES (ordre 200-230) ─────────────
                EnsureRubrique(os, "IPRES_RG", "RETENUE IPRES RG", tCotSoc,
                    ordre: 200, canon: RubriqueCanonique.IPRES_RG,
                    creditDefaut: c431300, taux1: 5.60m, taux2: 8.40m, plafond: 432000m);

                EnsureRubrique(os, "IPRES_RC", "RETENUE IPRES RC", tCotSoc,
                    ordre: 210, canon: RubriqueCanonique.IPRES_RC,
                    creditDefaut: c431310, taux1: 2.40m, taux2: 3.60m, plafond: 1296000m);

                EnsureRubrique(os, "CSS_AT", "CSS - Assu. accident travail", tCotSoc,
                    ordre: 220, canon: RubriqueCanonique.CSS_AccidentTravail,
                    debitDefaut: c612450, taux2: 3.00m, plafond: 63000m);

                EnsureRubrique(os, "CSS_AF", "CSS - Allocation familiale", tCotSoc,
                    ordre: 230, canon: RubriqueCanonique.CSS_AllocationFamiliale,
                    debitDefaut: c612530, taux2: 7.00m, plafond: 63000m);

                // ── COTISATIONS FISCALES (ordre 300-311) ─────────────
                EnsureRubrique(os, "TRIMF", "RETENUE TRIMF", tCotFis,
                    ordre: 300, canon: RubriqueCanonique.TRIMF,
                    creditDefaut: c447200);

                EnsureRubrique(os, "IR", "RETENUE IMPÔTS", tCotFis,
                    ordre: 310, canon: RubriqueCanonique.IRPP,
                    creditDefaut: c447100);

                EnsureRubrique(os, "CFCE", "RETENUE CFCE", tCotFis,
                    ordre: 311, canon: RubriqueCanonique.CFCE,
                    creditDefaut: c664100, taux2: 3.00m);

                // ── AUTRES RETENUES (ordre 500-501) ──────────────────
                if (tRetenue != null)
                {
                    EnsureRubrique(os, "PRET", "RETENUE Prêt", tRetenue,
                        ordre: 500, canon: RubriqueCanonique.RemboursementPret,
                        creditDefaut: c272800);
                    EnsureRubrique(os, "AVANCE_SAL", "RETENUE Avance sur Salaire", tRetenue,
                        ordre: 501, canon: RubriqueCanonique.RemboursementAvance,
                        creditDefaut: c421000);
                }
            }

            // ─────────────────────────────────────────────────────────
            // V1.7.2 — BARÈMES TRIMF (Mensuel + Annuel) et IR DPP
            // Valeurs officielles Sénégal depuis Domain/PaieConsts.cs.
            // Le DBA peut ajuster les valeurs en base après si DGID publie
            // une nouvelle table (peu fréquent).
            // ─────────────────────────────────────────────────────────
            if (opt.Baremes)
            {
                int annee = opt.AnneeBaremes > 0
                    ? opt.AnneeBaremes
                    : DateTime.Today.Year;

                // TRIMF Mensuel
                EnsureBaremeTRIMF(os,
                    code: $"TRIMF_{annee}",
                    nature: Domain.DomainEnums.TrimfNature.Mensuel,
                    tranches: Domain.PaieConsts.Baremes.TRIMF_MENSUEL_DEFAULT,
                    annee);

                // TRIMF Annuel
                EnsureBaremeTRIMF(os,
                    code: $"TRIMF_AN_{annee}",
                    nature: Domain.DomainEnums.TrimfNature.Annuel,
                    tranches: Domain.PaieConsts.Baremes.TRIMF_ANNUEL_DEFAULT,
                    annee);

                // Barème IR DPP Annuel (Sénégal)
                var dppTranches = new (decimal Min, decimal Max, decimal Taux)[]
                {
                    (         0m,       630_000m,  0m),
                    (   630_001m,     1_500_000m, 20m),
                    ( 1_500_001m,     4_000_000m, 30m),
                    ( 4_000_001m,     8_000_000m, 35m),
                    ( 8_000_001m,    13_500_000m, 37m),
                    (13_500_001m,    50_000_000m, 40m),
                    (50_000_001m, 10_000_000_000m, 43m)
                };
                EnsureBaremeIR(os,
                    code: $"IR_DPP_{annee}",
                    tranches: dppTranches,
                    annee);

                // V1.7.2 — Table de réduction familiale IR (par nombre de parts)
                EnsureIRReductionsFamille(os);

                os.CommitChanges();
            }

            if (opt.JeuDemo)
            {
                // Réservé pour création de salariés démo (laissé désactivé).
            }

            os.CommitChanges();
        }

        // ─────────────────────────────────────────────────────────────
        // V1.7.2 — Helpers de création des barèmes (idempotents)
        // ─────────────────────────────────────────────────────────────
        private static void EnsureBaremeTRIMF(
            IObjectSpace os, string code,
            Domain.DomainEnums.TrimfNature nature,
            (decimal Min, decimal Max, decimal Montant)[] tranches,
            int annee)
        {
            // 1. Récupère le barème existant ou crée
            var bareme = os.GetObjectsQuery<BaremeTRIMF>()
                .FirstOrDefault(b => b.Code == code && b.Nature == nature);

            if (bareme == null)
            {
                bareme = os.CreateObject<BaremeTRIMF>();
                bareme.Code = code;
                bareme.Nature = nature;
                bareme.Actif = true;
                bareme.DateDebut = new DateTime(annee, 1, 1);
                bareme.DateFin = new DateTime(annee, 12, 31);
                os.CommitChanges();  // commit pour pouvoir lier les tranches
            }

            // 2. Ajoute les tranches manquantes (idempotent)
            int ordre = 1;
            foreach (var (min, max, montant) in tranches)
            {
                bool existe = os.GetObjectsQuery<BaremeTRIMFTranche>()
                    .Any(t => t.Bareme.Oid == bareme.Oid
                              && t.MontantMin == min
                              && t.MontantMax == max);
                if (!existe)
                {
                    var t = os.CreateObject<BaremeTRIMFTranche>();
                    t.Bareme = bareme;
                    t.Ordre = ordre;
                    t.MontantMin = min;
                    t.MontantMax = max;
                    t.Montant = montant;
                }
                ordre++;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // V1.7.2 — Table de réduction familiale IR (Sénégal)
        // 9 lignes : NbrePart 1.0 → 5.0 avec taux %, min annuel, max annuel.
        // Référence : Code Général des Impôts Sénégal Art. 174.
        // ─────────────────────────────────────────────────────────────
        private static void EnsureIRReductionsFamille(IObjectSpace os)
        {
            var rows = new (decimal Parts, decimal TauxPct, decimal MinA, decimal MaxA)[]
            {
                (1.0m,  0m,      0m,        0m),
                (1.5m, 10m, 100000m,  300000m),
                (2.0m, 15m, 200000m,  650000m),
                (2.5m, 20m, 300000m, 1100000m),
                (3.0m, 25m, 400000m, 1650000m),
                (3.5m, 30m, 500000m, 2030000m),
                (4.0m, 35m, 600000m, 2490000m),
                (4.5m, 40m, 700000m, 2755000m),
                (5.0m, 45m, 800000m, 3180000m)
            };

            foreach (var (parts, tauxPct, minA, maxA) in rows)
            {
                var row = os.GetObjectsQuery<IRReductionFamille>()
                            .FirstOrDefault(r => r.NbrePart == parts);
                if (row == null)
                {
                    row = os.CreateObject<IRReductionFamille>();
                    row.NbrePart = parts;
                }
                row.Taux = tauxPct;
                row.MinAnnuel = minA;
                row.MaxAnnuel = maxA;
                row.Actif = true;
            }
        }

        private static void EnsureBaremeIR(
            IObjectSpace os, string code,
            (decimal Min, decimal Max, decimal Taux)[] tranches,
            int annee)
        {
            var bareme = os.GetObjectsQuery<BaremeIR>()
                .FirstOrDefault(b => b.Code == code);

            if (bareme == null)
            {
                bareme = os.CreateObject<BaremeIR>();
                bareme.Code = code;
                bareme.Actif = true;
                bareme.DateDebut = new DateTime(annee, 1, 1);
                bareme.DateFin = new DateTime(annee, 12, 31);
                os.CommitChanges();
            }

            foreach (var (min, max, taux) in tranches)
            {
                bool existe = os.GetObjectsQuery<BaremeIRTranche>()
                    .Any(t => t.Bareme.Oid == bareme.Oid
                              && t.MontantMin == min
                              && t.MontantMax == max);
                if (!existe)
                {
                    var t = os.CreateObject<BaremeIRTranche>();
                    t.Bareme = bareme;
                    t.MontantMin = min;
                    t.MontantMax = max;
                    t.Taux = taux;
                }
            }
        }

        // ===========================
        //  ENSURE*  (tes helpers)
        // ===========================
        private static Convention EnsureConvention(IObjectSpace os, string code, string libelle)
        {
            var obj = os.GetObjectsQuery<Convention>().FirstOrDefault(c => c.CodeConvention == code);
            if (obj == null)
            {
                obj = os.CreateObject<Convention>();
                obj.CodeConvention = code; obj.NomConvention = libelle;
            }
            else if (string.IsNullOrWhiteSpace(obj.NomConvention))
                obj.NomConvention = libelle;
            return obj;
        }

        private static Categories EnsureCategorie(IObjectSpace os, Convention conv, string libelle)
        {
            var obj = os.GetObjectsQuery<Categories>().FirstOrDefault(c => c.Convention == conv && c.Intitule == libelle);
            if (obj == null)
            {
                obj = os.CreateObject<Categories>();
                obj.Convention = conv; obj.Intitule = libelle;
            }
            return obj;
        }

        private static void EnsureEchelonsFromTable(IObjectSpace os, Categories catAgent, Categories catCadre)
        {
            var rows = new[] {
                new { Code="1A",    Descriptif="Employés", Montant=133638m, Statut="NC", IdemLogement=105000m },
                new { Code="1ère",  Descriptif="Employés", Montant=136113m, Statut="NC", IdemLogement=120000m },
                new { Code="2",     Descriptif="Employés", Montant=142897m, Statut="NC", IdemLogement=105000m },
                new { Code="2émeA", Descriptif="Employés", Montant=157186m, Statut="NC", IdemLogement=120000m },
                new { Code="2émeB", Descriptif="Employés", Montant=159261m, Statut="NC", IdemLogement=120000m },
                new { Code="3B",    Descriptif="Employés", Montant=148533m, Statut="NC", IdemLogement=105000m },
                new { Code="3èA",   Descriptif="Employés", Montant=144784m, Statut="NC", IdemLogement=105000m },
                new { Code="3ème",  Descriptif="Employés", Montant=163389m, Statut="NC", IdemLogement=120000m },
                new { Code="4ème",  Descriptif="Employés", Montant=150607m, Statut="NC", IdemLogement=105000m },
                new { Code="4émeA", Descriptif="Employés", Montant=162993m, Statut="NC", IdemLogement=120000m },
                new { Code="4émeB", Descriptif="Employés", Montant=164329m, Statut="NC", IdemLogement=120000m },
                new { Code="4émeC", Descriptif="Employés", Montant=165668m, Statut="NC", IdemLogement=120000m },
                new { Code="5ème",  Descriptif="Employés", Montant=167002m, Statut="NC", IdemLogement=120000m },
                new { Code="6ème",  Descriptif="Employés", Montant=189698m, Statut="NC", IdemLogement=120000m },
                new { Code="7ème",  Descriptif="Employés", Montant=180451m, Statut="NC", IdemLogement=105000m },
                new { Code="7èmeA", Descriptif="Employés", Montant=198496m, Statut="NC", IdemLogement=120000m },
                new { Code="7èmeB", Descriptif="EMPLOYE",  Montant=239832m, Statut="NC", IdemLogement=120000m },
                new { Code="7èmeC", Descriptif="EMPLOYE",  Montant=255041m, Statut="NC", IdemLogement=120000m },
                new { Code="8ème",  Descriptif="EMPLOYE",  Montant=232135m, Statut="NC", IdemLogement=105000m },
                new { Code="AM1",   Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES", Montant=260662m, Statut="NC", IdemLogement=120000m },
                new { Code="AM1A",  Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES", Montant=219413m, Statut="NC", IdemLogement=105000m },
                new { Code="AM1B",  Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES", Montant=239506m, Statut="NC", IdemLogement=105000m },
                new { Code="AM2",   Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES", Montant=284534m, Statut="NC", IdemLogement=120000m },
                new { Code="AM3",   Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES", Montant=291835m, Statut="NC", IdemLogement=120000m },
                new { Code="AM4",   Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES", Montant=309761m, Statut="NC", IdemLogement=120000m },
                new { Code="AM5",   Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES", Montant=314599m, Statut="NC", IdemLogement=120000m },
                new { Code="C1",    Descriptif="INGENIEURS ET CADRES", Montant=268743m, Statut="C", IdemLogement=140000m },
                new { Code="C2",    Descriptif="INGENIEURS ET CADRES", Montant=289902m, Statut="C", IdemLogement=140000m },
                new { Code="C3",    Descriptif="INGENIEURS ET CADRES", Montant=310429m, Statut="C", IdemLogement=140000m },
                new { Code="D1",    Descriptif="INGENIEURS ET CADRES", Montant=335377m, Statut="C", IdemLogement=280000m },
                new { Code="D2",    Descriptif="INGENIEURS ET CADRES", Montant=361904m, Statut="C", IdemLogement=280000m },
                new { Code="D3",    Descriptif="DIRECTEURS",           Montant=390958m, Statut="C", IdemLogement=280000m },
                new { Code="E",     Descriptif="DIRECTEUR GENERAL",    Montant=422380m, Statut="C", IdemLogement=1330000m },
                new { Code="P1A",   Descriptif="INGENIEURS ET CADRES C1", Montant=316424m, Statut="C", IdemLogement=155000m },
                new { Code="P1B",   Descriptif="INGENIEURS ET CADRES",    Montant=322618m, Statut="C", IdemLogement=155000m },
                new { Code="P2A",   Descriptif="INGENIEURS ET CADRES C2", Montant=334837m, Statut="C", IdemLogement=155000m },
                new { Code="P2B",   Descriptif="INGENIEURS ET CADRES",    Montant=346691m, Statut="C", IdemLogement=155000m },
                new { Code="P3A",   Descriptif="INGENIEURS ET CADRES C3", Montant=358546m, Statut="C", IdemLogement=155000m },
                new { Code="P3B",   Descriptif="DIRECTEURS D1",          Montant=387360m, Statut="C", IdemLogement=295000m },
                new { Code="P4A",   Descriptif="DIRECTEURS D2",          Montant=417999m, Statut="C", IdemLogement=295000m },
                new { Code="P4B",   Descriptif="DIRECTEURS D3",          Montant=451556m, Statut="C", IdemLogement=295000m },
                new { Code="P5",    Descriptif="DIRECTEUR GENERAL",      Montant=487849m, Statut="C", IdemLogement=1345000m },
            };

            foreach (var r in rows)
            {
                var cat = (r.Statut ?? "").Trim().Equals("C", StringComparison.OrdinalIgnoreCase) ? catCadre : catAgent;
                var e = os.GetObjectsQuery<Echelons>().FirstOrDefault(x => x.Categories == cat && x.Code == r.Code);
                if (e == null)
                {
                    e = os.CreateObject<Echelons>();
                    e.Categories = cat;
                    e.Code = r.Code;
                }
                e.Libelle = r.Descriptif?.Trim();
                e.SalaireBase = r.Montant;
                e.IdemniteLogement = r.IdemLogement;
            }
        }

        // V1.7.2 — Surcharge backward-compatible : code auto-déduit du libellé.
        // GroupeImpressionRef.Code est obligatoire (validation XPO).
        // Format imposé : MAJUSCULES, 2-20 caractères, A-Z, 0-9, underscore.
        private static GroupeImpressionRef EnsureGroupe(IObjectSpace os, string libelle)
        {
            // Mapping connu des libellés ELTON → codes
            string code = libelle switch
            {
                "Salaire brut (1)" => "BRUT",
                "Total Cotisations Sociales" => "COT_SOC",
                "Total Retenues Fiscales" => "RET_FIS",
                "Total Autres Retenues" => "RET_AUTRE",
                _ => DeriveCodeFromLibelle(libelle)
            };
            return EnsureGroupe(os, code, libelle);
        }

        // V1.7.2 — Surcharge avec code explicite (préférable).
        private static GroupeImpressionRef EnsureGroupe(IObjectSpace os, string code, string libelle)
        {
            // 1. Priorité au Code (unique)
            var g = os.GetObjectsQuery<GroupeImpressionRef>().FirstOrDefault(x => x.Code == code);
            // 2. Fallback : par libellé
            if (g == null)
                g = os.GetObjectsQuery<GroupeImpressionRef>().FirstOrDefault(x => x.Libelle == libelle);

            if (g == null)
            {
                g = os.CreateObject<GroupeImpressionRef>();
                g.Code = code;
                g.Libelle = libelle;
                g.Actif = true;
            }
            else
            {
                // Si le code est manquant en base (ancien import), le compléter
                if (string.IsNullOrWhiteSpace(g.Code))
                    g.Code = code;
                if (string.IsNullOrWhiteSpace(g.Libelle))
                    g.Libelle = libelle;
                if (!g.Actif)
                    g.Actif = true;
            }
            return g;
        }

        // Helper : transforme un libellé en code MAJUSCULE 2-20 caractères
        // (A-Z, 0-9, underscore) — pour les groupes non listés dans le mapping.
        private static string DeriveCodeFromLibelle(string libelle)
        {
            if (string.IsNullOrWhiteSpace(libelle)) return "GRP_UNKNOWN";
            // Garde uniquement A-Z, 0-9 et remplace tout le reste par '_'
            var sb = new System.Text.StringBuilder();
            foreach (var c in libelle.ToUpperInvariant())
            {
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            // Compresser les '_' consécutifs et trim
            var code = System.Text.RegularExpressions.Regex
                .Replace(sb.ToString(), "_+", "_")
                .Trim('_');
            if (code.Length < 2) code = "GRP_" + code;
            if (code.Length > 20) code = code.Substring(0, 20);
            return code;
        }

        private static RubriqueTypeRef EnsureTypeRef(IObjectSpace os, string code, string libelle, GroupeImpressionRef grp,
            RubriqueTypeCalcul calc, Domain.DomainEnums.SensAssiette sens, bool bf, bool bs)
        {
            var t = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(x => x.Code == code);
            if (t == null)
            {
                t = os.CreateObject<RubriqueTypeRef>();
                t.Code = code; t.Libelle = libelle; t.Groupe = grp;
                t.DefaultTypeCalcul = calc; t.DefaultSens = sens;
                t.BruteFiscal = bf; t.BruteSocial = bs; t.Actif = true;
            }
            else
            {
                if (t.Groupe == null) t.Groupe = grp;
                if (!t.Actif) t.Actif = true;
            }
            return t;
        }

        private static PlanComptable EnsureCompte(IObjectSpace os, string numero, string libelle)
        {
            var c = os.GetObjectsQuery<PlanComptable>().FirstOrDefault(x => x .Code == numero);
            if (c == null)
            {
                c = os.CreateObject<PlanComptable>();
                c.Code = numero; c.Intitule = libelle;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(c.Intitule)) c.Intitule = libelle;
                //if (!c.Actif) c.Actif = true;
            }
            return c;
        }

        private static Rubrique EnsureRubrique(IObjectSpace os, string code, string libelle, RubriqueTypeRef typeRef,
            int ordre, RubriqueCanonique? canon = null,
            PlanComptable debitDefaut = null, PlanComptable creditDefaut = null,
            decimal? taux1 = null, decimal? taux2 = null, decimal? plafond = null)
        {
            var r = os.GetObjectsQuery<Rubrique>().FirstOrDefault(x => x.Code == code);
            if (r == null)
            {
                r = os.CreateObject<Rubrique>();
                r.Code = code; r.Libelle = libelle; r.TypeRef = typeRef;
                r.OrdreAffichage = ordre; r.Canonique = canon;
                r.Taux1 = taux1; r.Taux2 = taux2; r.Plafond = plafond; r.Actif = true;
                if (debitDefaut != null) r.CompteDebitDefaut = debitDefaut;
                if (creditDefaut != null) r.CompteCreditDefaut = creditDefaut;
            }
            else
            {
                if (r.TypeRef == null) r.TypeRef = typeRef;
                if (!r.OrdreAffichage.HasValue) r.OrdreAffichage = ordre;
                if (!r.Canonique.HasValue && canon.HasValue) r.Canonique = canon;
                if (r.Taux1 == null && taux1.HasValue) r.Taux1 = taux1;
                if (r.Taux2 == null && taux2.HasValue) r.Taux2 = taux2;
                if (r.Plafond == null && plafond.HasValue) r.Plafond = plafond;
                if (r.CompteDebitDefaut == null && debitDefaut != null) r.CompteDebitDefaut = debitDefaut;
                if (r.CompteCreditDefaut == null && creditDefaut != null) r.CompteCreditDefaut = creditDefaut;
                if (!r.Actif) r.Actif = true;
            }
            return r;
        }
    }
}
