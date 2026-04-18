// ============================================================
//  BilanSocialService.cs
//  AdiPAIE V02 — Génération du Bilan Social annuel (DTSS Sénégal)
//  Conforme au formulaire réglementaire Décret 2009-4181/MFPTEOP/DTSS
// ============================================================
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class BilanSocialService
    {
        // ── Point d'entrée ────────────────────────────────────────────────
        public static byte[] Generer(int annee, IObjectSpace os)
        {
            var session = ((XPObjectSpace)os).Session;
            var data = CollecterDonnees(annee, session);
            return BuildDocx(data);
        }

        // ═════════════════════════════════════════════════════════════════
        //  COLLECTE DES DONNÉES
        // ═════════════════════════════════════════════════════════════════
        private static BilanData CollecterDonnees(int annee, Session session)
        {
            var d = new BilanData { Annee = annee };
            var anneeN = annee;
            var anneeP = annee - 1;

            // ── Entreprise ──────────────────────────────────────────────
            var company = new XPQuery<Company>(session).FirstOrDefault();
            d.RaisonSociale = company?.RaisonSociale ?? "";
            d.Adresse = company?.Address ?? "";
            d.Ville = company?.Ville ?? "Dakar";
            d.Telephone = company?.Telephone ?? "";
            d.Email = company?.Email ?? "";
            d.NINEA = company?.NINEA ?? "";

            // ── Tous les salariés ────────────────────────────────────────
            var tousSalaries = new XPQuery<Salarie>(session).ToList();

            // Actifs au 31/12/N
            var date31Dec = new DateTime(anneeN, 12, 31);
            var actifs = tousSalaries.Where(s =>
                s.DateEmbauche <= date31Dec &&
                (!s.IsActif == false || s.DateSortie == default || s.DateSortie >= date31Dec)
            ).ToList();

            // Actifs au 31/12/N-1
            var date31DecP = new DateTime(anneeP, 12, 31);
            var actifsP = tousSalaries.Where(s =>
                s.DateEmbauche <= date31DecP &&
                (s.DateSortie == default || s.DateSortie >= date31DecP)
            ).ToList();

            // ── 21. Effectif permanent ──────────────────────────────────
            d.EffPerm_CDI_N = ContratCount(actifs, session, anneeN, TypeContrat.CDI);
            d.EffPerm_CDD_N = ContratCount(actifs, session, anneeN, TypeContrat.CDD);
            d.EffPerm_CDI_P = ContratCount(actifsP, session, anneeP, TypeContrat.CDI);
            d.EffPerm_CDD_P = ContratCount(actifsP, session, anneeP, TypeContrat.CDD);

            // ── 31. Par filière ─────────────────────────────────────────
            // On regroupe par catégorie → Administratif / Technique (simplifié)
            d.Filieres_N = GroupParFiliere(actifs, session, anneeN);
            d.Filieres_P = GroupParFiliere(actifsP, session, anneeP);

            // ── 32. Par statut ──────────────────────────────────────────
            d.Statuts_N = GroupParStatut(actifs, session, anneeN);
            d.Statuts_P = GroupParStatut(actifsP, session, anneeP);

            // ── 33. Par tranche d'âge ────────────────────────────────────
            d.Ages_H_N = TranchesAge(actifs.Where(s => s.Sexe == Sexe.Masculin).ToList(), anneeN);
            d.Ages_F_N = TranchesAge(actifs.Where(s => s.Sexe == Sexe.Feminin).ToList(), anneeN);
            d.Ages_H_P = TranchesAge(actifsP.Where(s => s.Sexe == Sexe.Masculin).ToList(), anneeP);
            d.Ages_F_P = TranchesAge(actifsP.Where(s => s.Sexe == Sexe.Feminin).ToList(), anneeP);

            // ── 34. Par nationalité ──────────────────────────────────────
            d.Nat_Sen_H_N = actifs.Count(s => s.Sexe == Sexe.Masculin && EstSenegalais(s));
            d.Nat_Sen_F_N = actifs.Count(s => s.Sexe == Sexe.Feminin && EstSenegalais(s));
            d.Nat_Etr_H_N = actifs.Count(s => s.Sexe == Sexe.Masculin && !EstSenegalais(s));
            d.Nat_Etr_F_N = actifs.Count(s => s.Sexe == Sexe.Feminin && !EstSenegalais(s));
            d.Nat_Sen_H_P = actifsP.Count(s => s.Sexe == Sexe.Masculin && EstSenegalais(s));
            d.Nat_Sen_F_P = actifsP.Count(s => s.Sexe == Sexe.Feminin && EstSenegalais(s));

            // ── 35. Par ancienneté ────────────────────────────────────────
            d.Anc_H_N = TranchesAnc(actifs.Where(s => s.Sexe == Sexe.Masculin).ToList(), anneeN);
            d.Anc_F_N = TranchesAnc(actifs.Where(s => s.Sexe == Sexe.Feminin).ToList(), anneeN);

            // ── 37. Recrutements dans l'année ────────────────────────────
            var recrutes = tousSalaries.Where(s =>
                s.DateEmbauche.Year == anneeN).ToList();
            d.Recrutes = GroupStatutSexe(recrutes, session, anneeN);

            // ── 38. Départs dans l'année ─────────────────────────────────
            var partis = tousSalaries.Where(s =>
                s.DateSortie != default && s.DateSortie.Year == anneeN).ToList();
            d.Departs = GroupStatutSexe(partis, session, anneeN);

            // ── 41. Promotions ────────────────────────────────────────────
            // Non disponible sans log historique — laissé vide

            // ── 51/52/53. Masse salariale ────────────────────────────────
            var bulletinsN = new XPQuery<Bulletin>(session)
                .Where(b => b.Annee == anneeN && b.Statut != BulletinStatut.Brouillon)
                .ToList();
            var bulletinsP = new XPQuery<Bulletin>(session)
                .Where(b => b.Annee == anneeP && b.Statut != BulletinStatut.Brouillon)
                .ToList();

            d.MasseSal_Total_N = bulletinsN.Sum(b => b.BrutFiscal);
            d.MasseSal_Total_P = bulletinsP.Sum(b => b.BrutFiscal);
            d.Charges_IPRES_N = bulletinsN.Sum(b => b.IPRES_RG_Mois + b.IPRES_RC_Mois) * 12 / Math.Max(1, bulletinsN.Select(b => b.Mois).Distinct().Count());
            d.Charges_IPRES_N = bulletinsN.Sum(b => b.IPRES_RG_CumulAnnee + b.IPRES_RC_CumulAnnee) / Math.Max(1, actifs.Count);
            // Plus fiable : on prend la somme directe des lignes employeur
            d.Charges_IPRES_N = SumLignesEmployeur(bulletinsN, session, RubriqueCanonique.IPRES_RG, RubriqueCanonique.IPRES_RC);
            d.Charges_CSS_N = SumLignesEmployeur(bulletinsN, session, RubriqueCanonique.CSS_AccidentTravail, RubriqueCanonique.CSS_AllocationFamiliale);
            d.Charges_CFCE_N = SumLignesEmployeur(bulletinsN, session, RubriqueCanonique.CFCE);
            d.Impots_N = bulletinsN.Sum(b => b.IR_Mois + b.TRIMF_Mois);
            d.Charges_IPRES_P = SumLignesEmployeur(bulletinsP, session, RubriqueCanonique.IPRES_RG, RubriqueCanonique.IPRES_RC);
            d.Charges_CSS_P = SumLignesEmployeur(bulletinsP, session, RubriqueCanonique.CSS_AccidentTravail, RubriqueCanonique.CSS_AllocationFamiliale);
            d.Charges_CFCE_P = SumLignesEmployeur(bulletinsP, session, RubriqueCanonique.CFCE);
            d.Impots_P = bulletinsP.Sum(b => b.IR_Mois + b.TRIMF_Mois);

            // Masse par catégorie
            d.MasseSal_Statuts_N = MasseParStatut(bulletinsN, session, anneeN);
            d.MasseSal_Statuts_P = MasseParStatut(bulletinsP, session, anneeP);

            // ── IX. Formation ─────────────────────────────────────────────
            var sessions = new XPQuery<SessionFormation>(session)
                .Where(sf => sf.Plan != null && sf.Plan.Annee == anneeN &&
                             sf.Plan.Statut == PlanFormationStatut.Approuve)
                .ToList();
            d.Formations_N = sessions.Select(sf => new FormationLigne
            {
                Domaine = sf.Domaine?.Libelle ?? sf.Intitule,
                DureeHeures = sf.DureeHeures,
                NbParticipants = sf.NbInscrits,
                CoutTotal = sf.CoutReel > 0 ? sf.CoutReel : sf.CoutPrevisionnel
            }).ToList();

            // ── 112. État des congés (mois par mois) ─────────────────────
            // Calculé depuis les effectifs actifs par mois
            d.CongesParMois = new List<CongesMois>();
            for (int m = 1; m <= 12; m++)
            {
                var debutM = new DateTime(anneeN, m, 1);
                var finM = debutM.AddMonths(1).AddDays(-1);
                var effM = tousSalaries.Count(s =>
                    s.DateEmbauche <= finM &&
                    (s.DateSortie == default || s.DateSortie >= debutM));
                d.CongesParMois.Add(new CongesMois { Mois = m, Effectif = effM });
            }

            return d;
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS DONNÉES
        // ═════════════════════════════════════════════════════════════════
        private static int ContratCount(List<Salarie> salaries, Session session, int annee, TypeContrat type)
        {
            var oids = salaries.Select(s => s.Oid).ToList();
            if (!oids.Any()) return 0;
            return new XPQuery<ContratSalarie>(session)
                .Where(c => oids.Contains(c.Salarie.Oid) &&
                            c.TypeContrat == type &&
                            c.Statut == ContratSalarieStatut.Actif)
                .Select(c => c.Salarie.Oid).Distinct().Count();
        }

        private static bool EstSenegalais(Salarie s)
            => string.IsNullOrWhiteSpace(s.Nationalite) ||
               s.Nationalite.ToUpperInvariant().Contains("SÉNÉ") ||
               s.Nationalite.ToUpperInvariant().Contains("SENE") ||
               s.Nationalite.ToUpperInvariant().Contains("SN");

        private static string GetStatutLabel(Salarie s)
        {
            var cat = s.Categories?.Intitule?.ToUpperInvariant() ?? "";
            if (cat.Contains("CADRE")) return "Cadres";
            if (cat.Contains("MAITRISE") || cat.Contains("MAÎTRISE")) return "Agents de maîtrise";
            if (cat.Contains("EMPLOYE") || cat.Contains("EMPLOYÉ")) return "Employés";
            return "Ouvriers";
        }

        private static Dictionary<string, HF> GroupParStatut(
            List<Salarie> salaries, Session session, int annee)
        {
            var result = new Dictionary<string, HF>
            {
                ["Ouvriers"] = new HF(0, 0),
                ["Employés"] = new HF(0, 0),
                ["Agents de maîtrise"] = new HF(0, 0),
                ["Cadres"] = new HF(0, 0),
            };
            foreach (var s in salaries)
            {
                var key = GetStatutLabel(s);
                var cur = result[key];
                if (s.Sexe == Sexe.Masculin) result[key] = new HF(cur.H + 1, cur.F);
                else result[key] = new HF(cur.H, cur.F + 1);
            }
            return result;
        }

        private static Dictionary<string, (int H_CDI, int H_CDD, int F_CDI, int F_CDD)> GroupParFiliere(
            List<Salarie> salaries, Session session, int annee)
        {
            var result = new Dictionary<string, (int H_CDI, int H_CDD, int F_CDI, int F_CDD)>
            {
                ["Technique"] = (0, 0, 0, 0),
                ["Administrative"] = (0, 0, 0, 0),
            };
            var oids = salaries.Select(s => s.Oid).ToList();
            if (!oids.Any()) return result;

            var contrats = new XPQuery<ContratSalarie>(session)
                .Where(c => oids.Contains(c.Salarie.Oid) && c.Statut == ContratSalarieStatut.Actif)
                .ToList()
                .GroupBy(c => c.Salarie.Oid)
                .ToDictionary(g => g.Key, g => g.First().TypeContrat ?? TypeContrat.CDI);

            foreach (var s in salaries)
            {
                var cat = s.Categories?.Intitule?.ToUpperInvariant() ?? "";
                var key = cat.Contains("TECH") ? "Technique" : "Administrative";
                var type = contrats.TryGetValue(s.Oid, out var t) ? t : TypeContrat.CDI;
                var cur = result[key];
                if (s.Sexe == Sexe.Masculin)
                    result[key] = type == TypeContrat.CDI
                        ? (cur.H_CDI + 1, cur.H_CDD, cur.F_CDI, cur.F_CDD)
                        : (cur.H_CDI, cur.H_CDD + 1, cur.F_CDI, cur.F_CDD);
                else
                    result[key] = type == TypeContrat.CDI
                        ? (cur.H_CDI, cur.H_CDD, cur.F_CDI + 1, cur.F_CDD)
                        : (cur.H_CDI, cur.H_CDD, cur.F_CDI, cur.F_CDD + 1);
            }
            return result;
        }

        private static Dictionary<string, HF> GroupStatutSexe(
            List<Salarie> salaries, Session session, int annee)
        {
            var result = new Dictionary<string, HF>
            {
                ["Ouvriers"] = new HF(0, 0),
                ["Employés"] = new HF(0, 0),
                ["Agents de maîtrise"] = new HF(0, 0),
                ["Cadres"] = new HF(0, 0),
            };
            foreach (var s in salaries)
            {
                var key = GetStatutLabel(s);
                var cur = result[key];
                if (s.Sexe == Sexe.Masculin) result[key] = new HF(cur.H + 1, cur.F);
                else result[key] = new HF(cur.H, cur.F + 1);
            }
            return result;
        }

        private static readonly (string Label, int Min, int Max)[] _tranchesAge =
        {
            ("Moins de 20 ans", 0, 19),
            ("20 à 24 ans", 20, 24),
            ("25 à 29 ans", 25, 29),
            ("30 à 34 ans", 30, 34),
            ("35 à 39 ans", 35, 39),
            ("40 à 44 ans", 40, 44),
            ("45 à 49 ans", 45, 49),
            ("50 à 54 ans", 50, 54),
            ("55 à 59 ans", 55, 59),
            ("60 ans et plus", 60, 999),
        };

        private static Dictionary<string, int> TranchesAge(List<Salarie> salaries, int annee)
        {
            var result = _tranchesAge.ToDictionary(t => t.Label, _ => 0);
            foreach (var s in salaries)
            {
                var bd = (s as Person)?.Birthday;
                if (bd == null) continue;
                var age = annee - bd.Value.Year;
                var t = _tranchesAge.FirstOrDefault(x => age >= x.Min && age <= x.Max);
                if (t.Label != null) result[t.Label]++;
            }
            return result;
        }

        private static readonly (string Label, int Min, int Max)[] _tranchesAnc =
        {
            ("Moins d'un an", 0, 0),
            ("1 à 4 ans", 1, 4),
            ("5 à 9 ans", 5, 9),
            ("10 à 14 ans", 10, 14),
            ("15 à 19 ans", 15, 19),
            ("20 à 24 ans", 20, 24),
            ("25 à 29 ans", 25, 29),
            ("30 à 34 ans", 30, 34),
            ("35 à 40 ans", 35, 40),
            ("Plus de 40 ans", 41, 999),
        };

        private static Dictionary<string, int> TranchesAnc(List<Salarie> salaries, int annee)
        {
            var result = _tranchesAnc.ToDictionary(t => t.Label, _ => 0);
            var ref31Dec = new DateTime(annee, 12, 31);
            foreach (var s in salaries)
            {
                var anc = AncienneteHelper.NombreAnnee(s.DateEmbauche, ref31Dec);
                var t = _tranchesAnc.FirstOrDefault(x => anc >= x.Min && anc <= x.Max);
                if (t.Label != null) result[t.Label]++;
            }
            return result;
        }

        private static decimal SumLignesEmployeur(
            List<Bulletin> bulletins, Session session, params RubriqueCanonique[] roles)
        {
            if (!bulletins.Any()) return 0;
            var bids = bulletins.Select(b => b.Oid).ToList();
            var rolesSet = new HashSet<RubriqueCanonique>(roles);
            return new XPQuery<BulletinLigne>(session)
                .Where(l => bids.Contains(l.Bulletin.Oid) &&
                            l.Rubrique != null &&
                            l.Rubrique.Canonique.HasValue &&
                            rolesSet.Contains(l.Rubrique.Canonique.Value))
                .Sum(l => l.MontantEmployeur);
        }

        private static Dictionary<string, decimal> MasseParStatut(
            List<Bulletin> bulletins, Session session, int annee)
        {
            var result = new Dictionary<string, decimal>
            {
                ["Ouvriers"] = 0,
                ["Employés"] = 0,
                ["Agents de maîtrise"] = 0,
                ["Cadres"] = 0,
            };
            foreach (var b in bulletins)
            {
                var key = GetStatutLabel(b.Salarie);
                result[key] += b.BrutFiscal;
            }
            return result;
        }

        // ═════════════════════════════════════════════════════════════════
        //  GÉNÉRATION WORD (OpenXML)
        // ═════════════════════════════════════════════════════════════════
        private static byte[] BuildDocx(BilanData d)
        {
            using var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body(BuildContent(d)));
                AddStyles(mainPart);
            }
            return ms.ToArray();
        }

        private static IEnumerable<OpenXmlElement> BuildContent(BilanData d)
        {
            var elements = new List<OpenXmlElement>();

            // ── Page de titre ──────────────────────────────────────────
            elements.Add(CenteredPara("BILAN SOCIAL DES ENTREPRISES", bold: true, size: 32));
            elements.Add(CenteredPara("MINISTÈRE DE LA FONCTION PUBLIQUE, DU TRAVAIL", bold: true, size: 22));
            elements.Add(CenteredPara("DE L'EMPLOI ET DES ORGANISATIONS PROFESSIONNELLES", bold: true, size: 22));
            elements.Add(CenteredPara("Direction du Travail et de la Sécurité Sociale", size: 22));
            elements.Add(CenteredPara($"BILAN SOCIAL au 31 décembre {d.Annee}", bold: true, size: 26));
            elements.Add(CenteredPara($"Établissement : {d.RaisonSociale}", bold: true, size: 24));
            elements.Add(SpacerPara());

            // ── I. Renseignements généraux ─────────────────────────────
            elements.Add(TitrePara("I. RENSEIGNEMENTS GÉNÉRAUX SUR L'ENTREPRISE"));
            elements.Add(InfoLigne("Raison sociale", d.RaisonSociale));
            elements.Add(InfoLigne("Adresse", d.Adresse));
            elements.Add(InfoLigne("Ville", d.Ville));
            elements.Add(InfoLigne("Téléphone", d.Telephone));
            elements.Add(InfoLigne("E-mail", d.Email));
            elements.Add(InfoLigne("NINEA", d.NINEA));
            elements.Add(SpacerPara());

            // ── II. Effectif permanent ─────────────────────────────────
            elements.Add(TitrePara("II. EFFECTIF TOTAL DE L'ÉTABLISSEMENT"));
            elements.Add(Sous2("21. Effectif permanent"));
            var hdrsEff = new[] { "", "CDI", "CDD", "Total" };
            var rowsEff = new[]
            {
                new[] { $"Année {d.Annee}",
                    F(d.EffPerm_CDI_N), F(d.EffPerm_CDD_N),
                    F(d.EffPerm_CDI_N + d.EffPerm_CDD_N) },
                new[] { $"Année {d.Annee - 1}",
                    F(d.EffPerm_CDI_P), F(d.EffPerm_CDD_P),
                    F(d.EffPerm_CDI_P + d.EffPerm_CDD_P) },
            };
            elements.Add(Tableau(hdrsEff, rowsEff));
            elements.Add(SpacerPara());

            // ── III. Répartition ───────────────────────────────────────
            elements.Add(TitrePara("III. RÉPARTITION DES EFFECTIFS"));

            // 32. Par statut
            elements.Add(Sous2("32. Répartition par statut au 31 décembre"));
            var hdrsStatut = new[] { "Statut", "Sexe", $"CDI {d.Annee}", $"CDD {d.Annee}", $"CDI {d.Annee - 1}", $"CDD {d.Annee - 1}" };
            var rowsStatut = new List<string[]>();
            foreach (var kv in d.Statuts_N)
            {
                var pv = d.Statuts_P.TryGetValue(kv.Key, out var pVal) ? pVal : new HF(0, 0);
                rowsStatut.Add(new[] { kv.Key, "Hommes + Femmes",
                    F(kv.Value.H), F(kv.Value.F),
                    F(pv.H), F(pv.F) });
            }
            var totH_N = d.Statuts_N.Sum(x => x.Value.H);
            var totF_N = d.Statuts_N.Sum(x => x.Value.F);
            var totH_P = d.Statuts_P.Sum(x => x.Value.H);
            var totF_P = d.Statuts_P.Sum(x => x.Value.F);
            rowsStatut.Add(new[] { "TOTAL", $"H:{totH_N} F:{totF_N}", "", "", $"H:{totH_P} F:{totF_P}", "" });
            elements.Add(Tableau(hdrsStatut, rowsStatut.ToArray()));
            elements.Add(SpacerPara());

            // 33. Par tranche d'âge
            elements.Add(Sous2("33. Répartition par tranche d'âge au 31 décembre"));
            var hdrsAge = new[] { "Tranche", $"H {d.Annee}", $"F {d.Annee}", $"H {d.Annee - 1}", $"F {d.Annee - 1}" };
            var rowsAge = _tranchesAge.Select(t => new[]
            {
                t.Label,
                F(d.Ages_H_N.TryGetValue(t.Label, out var ahn) ? ahn : 0),
                F(d.Ages_F_N.TryGetValue(t.Label, out var afn) ? afn : 0),
                F(d.Ages_H_P.TryGetValue(t.Label, out var ahp) ? ahp : 0),
                F(d.Ages_F_P.TryGetValue(t.Label, out var afp) ? afp : 0),
            }).ToArray();
            elements.Add(Tableau(hdrsAge, rowsAge));
            elements.Add(SpacerPara());

            // 34. Par nationalité
            elements.Add(Sous2("34. Répartition par nationalité au 31 décembre"));
            var hdrsNat = new[] { "Nationalité", $"H {d.Annee}", $"F {d.Annee}", $"H {d.Annee - 1}", $"F {d.Annee - 1}" };
            var rowsNat = new[]
            {
                new[] { "Sénégalais", F(d.Nat_Sen_H_N), F(d.Nat_Sen_F_N), F(d.Nat_Sen_H_P), F(d.Nat_Sen_F_P) },
                new[] { "Étrangers",  F(d.Nat_Etr_H_N), F(d.Nat_Etr_F_N), "-", "-" },
                new[] { "TOTAL",      F(d.Nat_Sen_H_N+d.Nat_Etr_H_N), F(d.Nat_Sen_F_N+d.Nat_Etr_F_N), F(d.Nat_Sen_H_P), F(d.Nat_Sen_F_P) },
            };
            elements.Add(Tableau(hdrsNat, rowsNat));
            elements.Add(SpacerPara());

            // 35. Par ancienneté
            elements.Add(Sous2("35. Répartition par ancienneté au 31 décembre"));
            var hdrsAnc = new[] { "Tranche", $"H {d.Annee}", $"F {d.Annee}" };
            var rowsAnc = _tranchesAnc.Select(t => new[]
            {
                t.Label,
                F(d.Anc_H_N.TryGetValue(t.Label, out var ah) ? ah : 0),
                F(d.Anc_F_N.TryGetValue(t.Label, out var af) ? af : 0),
            }).ToArray();
            elements.Add(Tableau(hdrsAnc, rowsAnc));
            elements.Add(SpacerPara());

            // ── 37. Recrutements ────────────────────────────────────────
            elements.Add(TitrePara("37. Recrutements au cours de l'année"));
            var hdrsRec = new[] { "Statut", $"Hommes {d.Annee}", $"Femmes {d.Annee}", "Total" };
            var rowsRec = d.Recrutes.Select(kv => new[]
            {
                kv.Key, F(kv.Value.H), F(kv.Value.F), F(kv.Value.H + kv.Value.F)
            }).Append(new[]
            {
                "TOTAL",
                F(d.Recrutes.Sum(x => x.Value.H)),
                F(d.Recrutes.Sum(x => x.Value.F)),
                F(d.Recrutes.Sum(x => x.Value.H + x.Value.F))
            }).ToArray();
            elements.Add(Tableau(hdrsRec, rowsRec));
            elements.Add(SpacerPara());

            // ── 38. Départs ─────────────────────────────────────────────
            elements.Add(TitrePara("38. Départs au cours de l'année"));
            var hdrsD = new[] { "Statut", "Hommes", "Femmes", "Total" };
            var rowsD = d.Departs.Select(kv => new[]
            {
                kv.Key, F(kv.Value.H), F(kv.Value.F), F(kv.Value.H + kv.Value.F)
            }).Append(new[]
            {
                "TOTAL",
                F(d.Departs.Sum(x => x.Value.H)),
                F(d.Departs.Sum(x => x.Value.F)),
                F(d.Departs.Sum(x => x.Value.H + x.Value.F))
            }).ToArray();
            elements.Add(Tableau(hdrsD, rowsD));
            elements.Add(SpacerPara());

            // ── V. Masse salariale ─────────────────────────────────────
            elements.Add(PageBreak());
            elements.Add(TitrePara("V. RÉMUNÉRATIONS ET CHARGES ACCESSOIRES (en FCFA)"));

            elements.Add(Sous2("51. Masses salariales brutes par statut"));
            var hdrsMS = new[] { "Statut", $"Masse {d.Annee}", $"Masse {d.Annee - 1}" };
            var rowsMS = d.MasseSal_Statuts_N.Select(kv => new[]
            {
                kv.Key, FM(kv.Value),
                FM(d.MasseSal_Statuts_P.TryGetValue(kv.Key, out var mv) ? mv : 0)
            }).Append(new[] { "TOTAL", FM(d.MasseSal_Total_N), FM(d.MasseSal_Total_P) })
            .ToArray();
            elements.Add(Tableau(hdrsMS, rowsMS));
            elements.Add(SpacerPara());

            elements.Add(Sous2("52. Charges salariales (coûts employeur)"));
            var hdrsCS = new[] { "Nature", $"Année {d.Annee}", $"Année {d.Annee - 1}" };
            var rowsCS = new[]
            {
                new[] { "Cotisations IPRES",       FM(d.Charges_IPRES_N), FM(d.Charges_IPRES_P) },
                new[] { "Cotisations CSS",          FM(d.Charges_CSS_N),   FM(d.Charges_CSS_P) },
                new[] { "CFCE (charge employeur)",  FM(d.Charges_CFCE_N),  FM(d.Charges_CFCE_P) },
                new[] { "TOTAL",
                    FM(d.Charges_IPRES_N + d.Charges_CSS_N + d.Charges_CFCE_N),
                    FM(d.Charges_IPRES_P + d.Charges_CSS_P + d.Charges_CFCE_P) },
            };
            elements.Add(Tableau(hdrsCS, rowsCS));
            elements.Add(SpacerPara());

            elements.Add(Sous2("53. Détail des frais de personnel"));
            var hdrsDP = new[] { "Nature", $"Année {d.Annee}", $"Année {d.Annee - 1}" };
            var rowsDP = new[]
            {
                new[] { "Salaires (brut fiscal)",  FM(d.MasseSal_Total_N),  FM(d.MasseSal_Total_P) },
                new[] { "Charges sociales (IPRES + CSS)", FM(d.Charges_IPRES_N + d.Charges_CSS_N), FM(d.Charges_IPRES_P + d.Charges_CSS_P) },
                new[] { "Impôts (IRPP + TRIMF)",   FM(d.Impots_N), FM(d.Impots_P) },
                new[] { "CFCE",                    FM(d.Charges_CFCE_N), FM(d.Charges_CFCE_P) },
                new[] { "TOTAL",
                    FM(d.MasseSal_Total_N + d.Charges_IPRES_N + d.Charges_CSS_N + d.Charges_CFCE_N + d.Impots_N),
                    FM(d.MasseSal_Total_P + d.Charges_IPRES_P + d.Charges_CSS_P + d.Charges_CFCE_P + d.Impots_P) },
            };
            elements.Add(Tableau(hdrsDP, rowsDP));
            elements.Add(SpacerPara());

            // ── Sections vides (à compléter manuellement) ──────────────
            elements.Add(PageBreak());
            elements.Add(TitrePara("VI. HYGIÈNE, SÉCURITÉ ET SANTÉ"));
            elements.Add(NotePara("→ À compléter manuellement (accidents du travail, maladies professionnelles, dépenses de santé)"));
            elements.Add(SpacerPara());

            elements.Add(TitrePara("VII. RELATIONS PROFESSIONNELLES"));
            elements.Add(NotePara("→ À compléter manuellement (syndicats, délégués, grèves)"));
            elements.Add(SpacerPara());

            // ── IX. Formation ──────────────────────────────────────────
            elements.Add(TitrePara("IX. FORMATION"));
            elements.Add(Sous2("92. Formations par domaine — année " + d.Annee));
            if (d.Formations_N.Any())
            {
                var hdrsF = new[] { "Domaine / Intitulé", "Durée (h)", "Participants", "Coût FCFA" };
                var rowsF = d.Formations_N.Select(f => new[]
                {
                    f.Domaine, F(f.DureeHeures), F(f.NbParticipants), FM(f.CoutTotal)
                }).Append(new[]
                {
                    "TOTAL",
                    F(d.Formations_N.Sum(f => f.DureeHeures)),
                    F(d.Formations_N.Sum(f => f.NbParticipants)),
                    FM(d.Formations_N.Sum(f => f.CoutTotal))
                }).ToArray();
                elements.Add(Tableau(hdrsF, rowsF));
            }
            else
                elements.Add(NotePara("Aucune formation enregistrée pour cette année."));
            elements.Add(SpacerPara());

            // ── 112. État des congés ───────────────────────────────────
            elements.Add(TitrePara("XI. AUTRES DONNÉES — État des congés"));
            var hdrsC = new[] { "Mois", "Effectif" };
            var moisLabels = new[] { "Janvier", "Février", "Mars", "Avril", "Mai", "Juin", "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre" };
            var rowsC = d.CongesParMois.Select((c, i) => new[] { moisLabels[i], F(c.Effectif) }).ToArray();
            elements.Add(Tableau(hdrsC, rowsC));
            elements.Add(SpacerPara());

            // ── Signature ──────────────────────────────────────────────
            elements.Add(PageBreak());
            elements.Add(CenteredPara($"Fait à Dakar, le {DateTime.Today:dd/MM/yyyy}", size: 22));
            elements.Add(SpacerPara());
            elements.Add(CenteredPara("Signature et cachet de l'Employeur", bold: true));

            return elements;
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS WORD
        // ═════════════════════════════════════════════════════════════════
        private static Paragraph CenteredPara(string text, bool bold = false, int size = 24)
        {
            var run = new Run(new RunProperties(
                bold ? new Bold() : null!,
                new FontSize { Val = size.ToString() }
            ), new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                run);
        }

        private static Paragraph TitrePara(string text) =>
            new Paragraph(
                new ParagraphProperties(
                    new ParagraphBorders(new BottomBorder
                    {
                        Val = BorderValues.Single,
                        Size = 6,
                        Color = "1F5C99",
                        Space = 1
                    }),
                    new SpacingBetweenLines { Before = "240", After = "80" }
                ),
                new Run(
                    new RunProperties(new Bold(), new FontSize { Val = "26" },
                        new Color { Val = "1F5C99" }),
                    new Text(text)));

        private static Paragraph Sous2(string text) =>
            new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { Before = "160", After = "60" }),
                new Run(
                    new RunProperties(new Bold(), new FontSize { Val = "22" }),
                    new Text(text)));

        private static Paragraph InfoLigne(string label, string valeur) =>
            new Paragraph(new Run(
                new RunProperties(new Bold()),
                new Text($"{label} : ") { Space = SpaceProcessingModeValues.Preserve }),
                new Run(new Text(valeur)));

        private static Paragraph NotePara(string text) =>
            new Paragraph(
                new ParagraphProperties(new Indentation { Left = "720" }),
                new Run(
                    new RunProperties(new Color { Val = "888888" }, new Italic()),
                    new Text(text)));

        private static Paragraph SpacerPara() =>
            new Paragraph(new ParagraphProperties(
                new SpacingBetweenLines { After = "80" }));

        private static Paragraph PageBreak() =>
            new Paragraph(new Run(new Break { Type = BreakValues.Page }));

        private static Table Tableau(string[] headers, string[][] rows)
        {
            var colCount = headers.Length;
            var colW = 9026 / colCount; // A4 content width in DXA

            var tbl = new Table(new TableProperties(
                new TableWidth { Width = "9026", Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    MakeBorder<TopBorder>(), MakeBorder<BottomBorder>(),
                    MakeBorder<LeftBorder>(), MakeBorder<RightBorder>(),
                    MakeBorder<InsideHorizontalBorder>(), MakeBorder<InsideVerticalBorder>())));

            // Header row
            var hRow = new TableRow();
            foreach (var h in headers)
                hRow.Append(MakeCell(h, colW, headerBg: true));
            tbl.Append(hRow);

            // Data rows
            foreach (var row in rows)
            {
                var tRow = new TableRow();
                foreach (var cell in row)
                    tRow.Append(MakeCell(cell, colW));
                tbl.Append(tRow);
            }

            return tbl;
        }

        private static TableCell MakeCell(string text, int width, bool headerBg = false)
        {
            var rpr = new RunProperties(new FontSize { Val = "18" });
            if (headerBg) rpr.Append(new Bold(), new Color { Val = "FFFFFF" });

            var para = new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { After = "0" }),
                new Run(rpr, new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve }));

            var cellProps = new TableCellProperties(
                new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableCellMargin(
                    new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa }));

            if (headerBg)
                cellProps.Append(new Shading
                {
                    Val = ShadingPatternValues.Clear,
                    Fill = "1F5C99",
                    Color = "auto"
                });

            return new TableCell(cellProps, para);
        }

        private static T MakeBorder<T>() where T : BorderType, new()
        {
            var b = new T();
            b.Val = BorderValues.Single;
            b.Size = 4;
            b.Color = "CCCCCC";
            return b;
        }

        private static void AddStyles(MainDocumentPart mainPart)
        {
            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles(
                new DocDefaults(new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
                    new FontSize { Val = "24" }))));
        }

        // Formatters
        private static string F(int n) => n == 0 ? "0" : n.ToString("N0");
        private static string F(int? n) => F(n ?? 0);
        private static string FM(decimal m) => m == 0 ? "-" : m.ToString("N0");
    }

    // ═════════════════════════════════════════════════════════════════
    //  MODÈLE DE DONNÉES INTERMÉDIAIRE
    // ═════════════════════════════════════════════════════════════════
    internal class BilanData
    {
        public int Annee;
        // Entreprise
        public string RaisonSociale = "", Adresse = "", Ville = "", Telephone = "", Email = "", NINEA = "";
        // Effectif permanent
        public int EffPerm_CDI_N, EffPerm_CDD_N, EffPerm_CDI_P, EffPerm_CDD_P;
        // Répartitions
        public Dictionary<string, (int H_CDI, int H_CDD, int F_CDI, int F_CDD)> Filieres_N = new();
        public Dictionary<string, (int H_CDI, int H_CDD, int F_CDI, int F_CDD)> Filieres_P = new();
        public Dictionary<string, HF> Statuts_N = new(), Statuts_P = new();
        public Dictionary<string, int> Ages_H_N = new(), Ages_F_N = new(), Ages_H_P = new(), Ages_F_P = new();
        public int Nat_Sen_H_N, Nat_Sen_F_N, Nat_Etr_H_N, Nat_Etr_F_N, Nat_Sen_H_P, Nat_Sen_F_P;
        public Dictionary<string, int> Anc_H_N = new(), Anc_F_N = new();
        public Dictionary<string, HF> Recrutes = new(), Departs = new();
        // Masse salariale
        public decimal MasseSal_Total_N, MasseSal_Total_P;
        public decimal Charges_IPRES_N, Charges_IPRES_P;
        public decimal Charges_CSS_N, Charges_CSS_P;
        public decimal Charges_CFCE_N, Charges_CFCE_P;
        public decimal Impots_N, Impots_P;
        public Dictionary<string, decimal> MasseSal_Statuts_N = new(), MasseSal_Statuts_P = new();
        // Formation
        public List<FormationLigne> Formations_N = new();
        // Congés
        public List<CongesMois> CongesParMois = new();
    }

    // Petit struct pour éviter les pertes de noms de tuples après LINQ
    internal struct HF
    {
        public int H;
        public int F;
        public HF(int h, int f) { H = h; F = f; }
        public static HF operator +(HF a, HF b) => new HF(a.H + b.H, a.F + b.F);
    }

    internal class FormationLigne
    {
        public string Domaine = "";
        public int DureeHeures;
        public int NbParticipants;
        public decimal CoutTotal;
    }

    internal class CongesMois
    {
        public int Mois;
        public int Effectif;
    }
}