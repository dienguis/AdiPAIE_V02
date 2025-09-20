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
        public bool JeuDemo { get; set; } = false;
        public string CodeBaremeTRIMF { get; set; } = "TRIMF_2025";
    }

    public static class SeedService
    {
        public static void Run(IObjectSpace os, SeedOptions opt)
        {
            // Convention + catégories de base
            var conv = EnsureConvention(os, "CONV_PET", "CONVENTION PETROLE");
            var catAgent = EnsureCategorie(os, conv, "Non Cadre");
            var catCadre = EnsureCategorie(os, conv, "Cadre");

            if (opt.Echelons)
                EnsureEchelonsFromTable(os, catAgent, catCadre);

            // Groupes + Types
            BusinessObjects.GroupeImpressionRef gSalaireBrut = null, gCotSocial = null, gRetFiscal = null;
            if (opt.GroupesTypes)
            {
                gSalaireBrut = EnsureGroupe(os, "Salaire brut (1)");
                gCotSocial = EnsureGroupe(os, "Total Cotisations Sociales");
                gRetFiscal = EnsureGroupe(os, "Total Retenues Fiscales");

                var tBrute = EnsureTypeRef(os, "BRUTE", "Éléments bruts", gSalaireBrut, RubriqueTypeCalcul.Gain, Domain.DomainEnums.SensAssiette.Plus, bf: true, bs: true);
                var tIndImpos = EnsureTypeRef(os, "INDEM_IMPOSA", "Indemnités imposables", gSalaireBrut, RubriqueTypeCalcul.Gain, Domain.DomainEnums.SensAssiette.Plus, bf: true, bs: true);
                var tIndNonImp = EnsureTypeRef(os, "INDEM_NON_IMPOSA", "Indemnités non imposables", gSalaireBrut, RubriqueTypeCalcul.Gain, Domain.DomainEnums.SensAssiette.Plus, bf: false, bs: true);
                var tCotSoc = EnsureTypeRef(os, "COTSOC", "Cotisations sociales", gCotSocial, RubriqueTypeCalcul.Retenue, Domain.DomainEnums.SensAssiette.Moins, bf: false, bs: false);
                var tCotFis = EnsureTypeRef(os, "COTFISC", "Retenues fiscales", gRetFiscal, RubriqueTypeCalcul.Retenue, Domain.DomainEnums.SensAssiette.Moins, bf: false, bs: false);
               var tRetenue = EnsureTypeRef(os, "RETENUE", "Total Autres retenues(3)", gRetFiscal, RubriqueTypeCalcul.Retenue, SensAssiette.Moins, false, false);
                var tAvantageImp = EnsureTypeRef(os, "AV_NATURE_IMPOSABLE", "Avantage en Nature Imposable", gSalaireBrut, RubriqueTypeCalcul.Gain, SensAssiette.Plus, bf: true, bs: false);
                var tAvantageNonImp = EnsureTypeRef(os, "AV_NATURE_NON_IMPOSABLE", "Avantage en Nature Non Imposable", gSalaireBrut, RubriqueTypeCalcul.Gain, SensAssiette.Plus, bf: false, bs: false);


            }

            // Comptes
            PlanComptable c661100 = null, c663110 = null, c431300 = null, c431310 = null, c447100 = null, c447200 = null, c612450 = null, c612530 = null, c421100 = null;
            if (opt.Comptes)
            {
                c661100 = EnsureCompte(os, "661100", "Appointements & salaires");
                c663110 = EnsureCompte(os, "663110", "Indemnités de logement");
                c431300 = EnsureCompte(os, "431300", "IPRES - Régime Général (tiers)");
                c431310 = EnsureCompte(os, "431310", "IPRES - Régime Cadre (tiers)");
                c447100 = EnsureCompte(os, "447100", "IRPP");
                c447200 = EnsureCompte(os, "447200", "TRIMF");
                c612450 = EnsureCompte(os, "612450", "CSS - Accident de travail");
                c612530 = EnsureCompte(os, "612530", "CSS - Allocation familiale");
                c421100 = EnsureCompte(os, "421100", "Personnel - Rémunérations dues");
            }

            // Rubriques
            if (opt.Rubriques)
            {
                var tBrute = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(x => x.Code == "BRUTE");
                var tIndImpos = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(x => x.Code == "INDEM_IMPOSA");
                var tCotSoc = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(x => x.Code == "COTSOC");
                var tCotFis = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(x => x.Code == "COTFISC");

                var rSB = EnsureRubrique(os, "SB", "Salaire de base", tBrute, ordre: 10, canon: RubriqueCanonique.SalaireDeBase, debitDefaut: c661100, creditDefaut: c421100);
                var rLOGT = EnsureRubrique(os, "LOGT", "Indemnité de logement", tIndImpos, ordre: 40, canon: RubriqueCanonique.IndemniteLogement, debitDefaut: c663110, creditDefaut: c421100);
                var rANC = EnsureRubrique(os, "ANC", "Prime d'ancienneté", tBrute, ordre: 30, canon: RubriqueCanonique.PrimeAnciennete, debitDefaut: c661100, creditDefaut: c421100);

                var rIPRG = EnsureRubrique(os, "IPRES_RG", "RETENUE IPRES RG", tCotSoc, ordre: 200, canon: RubriqueCanonique.IPRES_RG, creditDefaut: c431300, taux1: 5.60m, taux2: 8.40m, plafond: 432000m);
                var rIPRC = EnsureRubrique(os, "IPRES_RC", "RETENUE IPRES RC", tCotSoc, ordre: 210, canon: RubriqueCanonique.IPRES_RC, creditDefaut: c431310, taux1: 2.40m, taux2: 3.60m, plafond: 1296000m);
                var rCSSAT = EnsureRubrique(os, "CSS_AT", "CSS - Assu. accident travail", tCotSoc, ordre: 220, canon: RubriqueCanonique.CSS_AccidentTravail, debitDefaut: c612450, taux2: 3.00m, plafond: 63000m);
                var rCSSAF = EnsureRubrique(os, "CSS_AF", "CSS - Allocation familiale", tCotSoc, ordre: 230, canon: RubriqueCanonique.CSS_AllocationFamiliale, debitDefaut: c612530, taux2: 7.00m, plafond: 63000m);

                var rTRIMF = EnsureRubrique(os, "TRIMF", "RETENUE TRIMF", tCotFis, ordre: 300, canon: RubriqueCanonique.TRIMF, creditDefaut: c447200);
                var rIR = EnsureRubrique(os, "IR", "RETENUE IMPÔTS", tCotFis, ordre: 310, canon: RubriqueCanonique.IRPP, creditDefaut: c447100);
            }

            if (opt.JeuDemo)
            {
                // Laisse vide (ou réintègre ton jeu démo ici si besoin)
            }

            os.CommitChanges();
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

        private static GroupeImpressionRef EnsureGroupe(IObjectSpace os, string libelle)
        {
            var g = os.GetObjectsQuery<GroupeImpressionRef>().FirstOrDefault(x => x.Libelle == libelle);
            if (g == null)
            {
                g = os.CreateObject<GroupeImpressionRef>();
                g.Libelle = libelle; g.Actif = true;
            }
            return g;
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
