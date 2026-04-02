// AdiPAIE_V02.Module/Services/Declaration1024Service.cs
// Génère l'état 1024 (Déclaration annuelle des salaires - DGID Sénégal)
// Dépendance NuGet : ClosedXML (Install-Package ClosedXML)
using AdiPAIE_V02.Module.BusinessObjects;
using ClosedXML.Excel;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Génère l'état 1024 (Déclaration annuelle des sommes versées aux salariés)
    /// conforme au formulaire DGID Sénégal.
    /// </summary>
    public static class Declaration1024Service
    {
        // ── Couleurs ──────────────────────────────────────────────────────────
        private static readonly XLColor BG_HEADER = XLColor.FromHtml("#1F4E79");
        private static readonly XLColor BG_SUBHEAD = XLColor.FromHtml("#D6E4F0");
        private static readonly XLColor BG_TOTAL = XLColor.FromHtml("#FFF2CC");
        private static readonly XLColor FG_WHITE = XLColor.White;
        private static readonly XLColor FG_DARK = XLColor.FromHtml("#1F1F1F");

        /// <summary>
        /// Génère l'état 1024 pour une année donnée.
        /// Retourne les bytes du fichier XLSX.
        /// </summary>
        public static byte[] Generer(IObjectSpace os, int annee)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));

            // ── 1. Données société ────────────────────────────────────────────
            var company = os.GetObjectsQuery<Company>().FirstOrDefault();
            var raisonSociale = company?.RaisonSociale ?? "";
            var ninea = company?.NINEA ?? "";
            // Company.Address est une string (défini dans Company.cs)
            var adresseSociete = string.Join(" — ", new[]
            {
                company?.Address,
                company?.Ville
            }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var telephone = company?.Telephone ?? "";

            // ── 2. Charger toutes les lignes de bulletins en une seule requête ──
            //    Requête plate sur BulletinLigne → évite le N+1
            //    XPO charge Rubrique.TypeRef en lazy mais depuis le même contexte
            var statuts = new[]
            {
                BulletinStatut.Valide, BulletinStatut.Exporte,
                BulletinStatut.Envoye, BulletinStatut.Cloture
            };

            var bulletinList = os.GetObjectsQuery<Bulletin>()
                .Where(b => b.Annee == annee && statuts.Contains(b.Statut))
                .ToList();

            if (!bulletinList.Any())
                throw new UserFriendlyException(
                    $"Aucun bulletin Validé / Exporté trouvé pour l'année {annee}.");

            // Forcer le chargement des lignes + rubriques + typeref en une seule requête
            var bulletinOids = bulletinList.Select(b => b.Oid).ToList();
            var toutesLesLignes = os.GetObjectsQuery<BulletinLigne>()
                .Where(l => bulletinOids.Contains(l.Bulletin.Oid))
                .Select(l => new
                {
                    BulletinOid = l.Bulletin.Oid,
                    SalarieOid = l.Bulletin.Salarie.Oid,
                    Montant = l.Montant,
                    MontantEmployeur = l.MontantEmployeur,
                    Col13 = l.Rubrique.TypeRef.EntreColonne13_1024,
                    Col14 = l.Rubrique.TypeRef.EntreColonne14_1024,
                    EstCFCE = l.Rubrique.Canonique == RubriqueCanonique.CFCE
                                     || l.Rubrique.Code == "CFCE",
                    EstTransport = l.Rubrique.Canonique == RubriqueCanonique.PrimeTransport,
                })
                .ToList();

            // Index rapide par salarié
            var lignesParSalarie = toutesLesLignes
                .GroupBy(l => l.SalarieOid)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ── 3. Agréger par salarié ────────────────────────────────────────
            var lignes = BuildLignes(bulletinList, annee);

            // ── 4. Construire le fichier Excel ────────────────────────────────
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Déclaration");

            EcrireEnTete(ws, raisonSociale, ninea, adresseSociete, telephone, annee);
            EcrireColonnes(ws);
            int ligneData = EcrireDonnees(ws, lignes, annee);
            EcrireTotaux(ws, ligneData, lignes);
            EcrireRecommandations(ws, ligneData + 2);
            AppliquerMiseEnPage(ws);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  CONSTRUCTION DES LIGNES
        // ═════════════════════════════════════════════════════════════════════

        private static List<Ligne1024> BuildLignes(IList<Bulletin> bulletins, int annee)
        {
            // Requête plate : charge toutes les lignes en mémoire une seule fois
            // Évite le N+1 sur Lignes → Rubrique → TypeRef
            var lignesPlan = bulletins
                .SelectMany(b => b.Lignes, (b, l) => new
                {
                    SalarieOid = b.Salarie?.Oid ?? Guid.Empty,
                    Bulletin = b,
                    Montant = l.Montant,
                    MontantEmployeur = l.MontantEmployeur,
                    Col13 = l.Rubrique?.TypeRef?.EntreColonne13_1024 == true,
                    Col14 = l.Rubrique?.TypeRef?.EntreColonne14_1024 == true,
                    EstCFCE = l.Rubrique?.Canonique == RubriqueCanonique.CFCE
                                    || (l.Rubrique?.Code ?? "").ToUpper() == "CFCE",
                    EstTransport = l.Rubrique?.Canonique == RubriqueCanonique.PrimeTransport,
                })
                .GroupBy(x => x.SalarieOid)
                .ToDictionary(g => g.Key, g => g.ToList());

            return bulletins
                .GroupBy(b => b.Salarie?.Oid)
                .Where(g => g.Key.HasValue && g.First().Salarie != null)
                .Select((g, idx) =>
                {
                    var sal = g.First().Salarie;
                    var last = g.OrderByDescending(b => b.Mois).First();

                    var lignes = lignesPlan.TryGetValue(sal.Oid, out var ll)
                                 ? ll : new List<object>().Select(x => new
                                 {
                                     SalarieOid = Guid.Empty,
                                     Bulletin = (Bulletin)null,
                                     Montant = 0m,
                                     MontantEmployeur = 0m,
                                     Col13 = false,
                                     Col14 = false,
                                     EstCFCE = false,
                                     EstTransport = false
                                 }).ToList();

                    // ── Col 13 : montant annuel brut imposable ─────────────
                    decimal brutFiscalAnnuel = lignes
                        .Where(l => l.Col13)
                        .Sum(l => l.Montant);

                    // ── Col 14 : avantages en nature ───────────────────────
                    decimal avantageAnnuel = lignes
                        .Where(l => l.Col14)
                        .Sum(l => l.Montant);

                    // Retenues fiscales (cumuls du dernier bulletin)
                    decimal irAnnuel = last.IR_CumulAnnee;
                    decimal trimfAnnuel = last.TRIMF_CumulAnnee;

                    // CFCE — part patronale
                    decimal cfceAnnuel = lignes
                        .Where(l => l.EstCFCE)
                        .Sum(l => l.MontantEmployeur);

                    // Indemnités frais d'emploi — transport
                    decimal transportAnnuel = lignes
                        .Where(l => l.EstTransport)
                        .Sum(l => l.Montant);

                    // Période
                    int moisMin = g.Min(b => b.Mois);
                    int moisMax = g.Max(b => b.Mois);
                    string periode = (moisMin == 1 && moisMax == 12)
                        ? annee.ToString()
                        : $"Du {moisMin:D2}/{annee} au {moisMax:D2}/{annee}";

                    return new Ligne1024
                    {
                        Ordre = idx + 1,
                        Matricule = sal.Matricule,
                        NomPrenom = FormatNomPrenom(sal),
                        Emploi = sal.Fonction?.Intitule ?? "",
                        Adresse = FormatAdresse(sal),
                        Sexe = FormatSexe(sal),
                        Nationalite = FormatNationalite(sal),
                        SituationFamille = FormatSituationFamille(sal),
                        NbEnfants = sal.NombreEnfant,
                        NbEpouses = sal.NbConjointsActuels,
                        NbParts = (double)(sal.NombrePartsFiscales > 0
                                               ? sal.NombrePartsFiscales
                                               : last.NombrePartsFiscales),
                        Periode = periode,
                        MontantAnnuel = brutFiscalAnnuel,
                        EvalAvantages = avantageAnnuel,
                        TotalBrut = brutFiscalAnnuel + avantageAnnuel,
                        IR = irAnnuel,
                        TRIMF = trimfAnnuel,
                        CFCE = cfceAnnuel,
                        IndemnitesFrais = transportAnnuel,
                    };
                })
                .OrderBy(l => l.NomPrenom)
                .ToList();
        }

        private static void EcrireEnTete(IXLWorksheet ws, string raisonSociale,
            string ninea, string adresse, string tel, int annee)
        {
            // Ligne 1 : République
            ws.Cell(1, 1).Value = "REPUBLIQUE DU SENEGAL";
            StyleEntete1(ws.Cell(1, 1));
            ws.Cell(1, 10).Value = $"ETAT DES SOMMES VERSÉES EN {annee}";
            StyleEntete1(ws.Cell(1, 10));

            ws.Cell(2, 1).Value = "MINISTERE DE L'ECONOMIE ET DES FINANCES";
            ws.Cell(2, 10).Value = "SOUS LA FORME DE :";

            ws.Cell(3, 1).Value = "DIRECTION GENERALE DES IMPÔTS ET DES DOMAINES";
            ws.Cell(3, 10).Value = "Traitements, salaires, rétributions, accessoires, pensions, et rentes viagères.";

            ws.Cell(4, 1).Value = "DIRECTION DES IMPOTS";
            ws.Cell(5, 1).Value = "CENTRE DES GRANDES ENTREPRISES";

            // Bloc société
            ws.Cell(7, 1).Value = "Prénoms, Nom ou Raison Sociale :";
            ws.Cell(7, 4).Value = raisonSociale;
            ws.Cell(7, 10).Value = "NINEA :";
            ws.Cell(7, 12).Value = ninea;

            ws.Cell(8, 1).Value = "Adresse :";
            ws.Cell(8, 4).Value = adresse;
            ws.Cell(8, 10).Value = "Tél :";
            ws.Cell(8, 12).Value = tel;

            // Styles
            foreach (var r in new[] { 7, 8 })
            {
                ws.Cell(r, 1).Style.Font.Bold = true;
                ws.Cell(r, 10).Style.Font.Bold = true;
            }

            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 10).Style.Font.Bold = true;
        }

        private static void EcrireColonnes(IXLWorksheet ws)
        {
            int r = 11;

            // Ligne titre colonnes
            var headers = new[]
            {
                (1,  "N°"),
                (2,  "N° Matricule"),
                (3,  "PRENOMS ET NOM\n(M./Mme/Mlle)"),
                (4,  "EMPLOI"),
                (5,  "ADRESSE DOMICILE"),
                (6,  "SEXE"),
                (7,  "NAT."),
                (8,  "CMDV"),
                (9,  "Nb\nEnfants"),
                (10, "Nb\nEpouses"),
                (11, "Nb\nParts"),
                (12, "PERIODE"),
                (13, "MONTANT\nANNUEL BRUT\nFISCAL"),
                (14, "EVAL.\nAVANTAGES"),
                (15, "TOTAL\nBRUT\n(13+14)"),
                (16, "IR\nANNUEL"),
                (17, "PREL.\nEXCEP."),
                (18, "TRIMF\nANNUEL"),
                (19, "CFCE"),
                (20, "INDEM.\nFRAIS\nEMPLOI"),
            };

            foreach (var (col, label) in headers)
            {
                var cell = ws.Cell(r, col);
                cell.Value = label;
                cell.Style.Fill.BackgroundColor = BG_HEADER;
                cell.Style.Font.FontColor = FG_WHITE;
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 8;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            ws.Row(r).Height = 45;

            // Numéros de colonnes (ligne 12)
            for (int i = 1; i <= 20; i++)
            {
                var cell = ws.Cell(r + 1, i);
                cell.Value = i;
                cell.Style.Fill.BackgroundColor = BG_SUBHEAD;
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 8;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
        }

        private static int EcrireDonnees(IXLWorksheet ws, List<Ligne1024> lignes, int annee)
        {
            int r = 13; // Première ligne de données

            foreach (var l in lignes)
            {
                ws.Cell(r, 1).Value = l.Ordre;
                ws.Cell(r, 2).Value = l.Matricule;
                ws.Cell(r, 3).Value = l.NomPrenom;
                ws.Cell(r, 4).Value = l.Emploi;
                ws.Cell(r, 5).Value = l.Adresse;
                ws.Cell(r, 6).Value = l.Sexe;
                ws.Cell(r, 7).Value = l.Nationalite;
                ws.Cell(r, 8).Value = l.SituationFamille;
                ws.Cell(r, 9).Value = l.NbEnfants;
                ws.Cell(r, 10).Value = l.NbEpouses;
                ws.Cell(r, 11).Value = l.NbParts;
                ws.Cell(r, 12).Value = l.Periode;
                ws.Cell(r, 13).Value = (double)l.MontantAnnuel;
                ws.Cell(r, 14).Value = (double)l.EvalAvantages;
                ws.Cell(r, 15).Value = (double)l.TotalBrut;
                ws.Cell(r, 16).Value = (double)l.IR;
                ws.Cell(r, 17).Value = 0;
                ws.Cell(r, 18).Value = (double)l.TRIMF;
                ws.Cell(r, 19).Value = (double)l.CFCE;
                ws.Cell(r, 20).Value = (double)l.IndemnitesFrais;

                // Format nombres
                FormatMontantRange(ws, r, 13, 20);
                FormatLigneData(ws, r);

                // Alternance fond
                if (r % 2 == 0)
                    ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F7F7F7");

                r++;
            }

            return r; // Retourne la prochaine ligne disponible
        }

        private static void EcrireTotaux(IXLWorksheet ws, int r, List<Ligne1024> lignes)
        {
            ws.Cell(r, 1).Value = "TOTAL";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Fill.BackgroundColor = BG_TOTAL;

            var totaux = new[]
            {
                (13, (double)lignes.Sum(l => l.MontantAnnuel)),
                (14, (double)lignes.Sum(l => l.EvalAvantages)),
                (15, (double)lignes.Sum(l => l.TotalBrut)),
                (16, (double)lignes.Sum(l => l.IR)),
                (17, 0d),
                (18, (double)lignes.Sum(l => l.TRIMF)),
                (19, (double)lignes.Sum(l => l.CFCE)),
                (20, (double)lignes.Sum(l => l.IndemnitesFrais)),
            };

            foreach (var (col, val) in totaux)
            {
                var cell = ws.Cell(r, col);
                cell.Value = val;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = BG_TOTAL;
                cell.Style.NumberFormat.Format = "#,##0";
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Nb salariés
            ws.Cell(r, 2).Value = $"{lignes.Count} salarié(s)";
            ws.Cell(r, 2).Style.Font.Bold = true;
            ws.Cell(r, 2).Style.Fill.BackgroundColor = BG_TOTAL;
        }

        private static void EcrireRecommandations(IXLWorksheet ws, int r)
        {
            r += 2;
            var recs = new[]
            {
                "RECOMMANDATIONS CONCERNANT LA RÉDACTION DU TABLEAU CI-DESSUS",
                "Colonne 2 - Inscrire le numéro de matricule IPRES ou interne du salarié.",
                "Colonne 6 - Sexe : «F» Femme - «H» Homme.",
                "Colonne 7 - Nationalité : «S» Sénégalais - «A» Africains - «F» Français - «L» Libano-Syriens - «E» Étranger.",
                "Colonne 8 - Situation de famille : «C» Célibataire - «M» Marié - «D» Divorcé - «V» Veuf.",
                "Colonne 12 - Mentionner «année» ou à défaut «du … au …».",
            };

            foreach (var rec in recs)
            {
                ws.Cell(r, 1).Value = rec;
                if (r == recs[0].Length + r - recs.Length)
                    ws.Cell(r, 1).Style.Font.Bold = true;
                ws.Cell(r, 1).Style.Font.FontSize = 8;
                ws.Cell(r, 1).Style.Font.Italic = true;
                r++;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MISE EN PAGE
        // ═════════════════════════════════════════════════════════════════════

        private static void AppliquerMiseEnPage(IXLWorksheet ws)
        {
            // Largeurs colonnes
            ws.Column(1).Width = 5;   // N°
            ws.Column(2).Width = 14;  // Matricule
            ws.Column(3).Width = 28;  // NomPrenom
            ws.Column(4).Width = 16;  // Emploi
            ws.Column(5).Width = 22;  // Adresse
            ws.Column(6).Width = 5;   // Sexe
            ws.Column(7).Width = 5;   // Nat
            ws.Column(8).Width = 6;   // CMDV
            ws.Column(9).Width = 6;   // Nb enfants
            ws.Column(10).Width = 6;   // Nb épouses
            ws.Column(11).Width = 6;   // Nb parts
            ws.Column(12).Width = 16;  // Période
            ws.Column(13).Width = 16;  // Montant annuel
            ws.Column(14).Width = 12;  // Eval avantages
            ws.Column(15).Width = 12;  // Total brut
            ws.Column(16).Width = 12;  // IR
            ws.Column(17).Width = 8;   // Prél. excep.
            ws.Column(18).Width = 12;  // TRIMF
            ws.Column(19).Width = 12;  // CFCE
            ws.Column(20).Width = 12;  // Indem. frais

            // Orientation paysage
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A3Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(11, 12);

            // Figer les 12 premières lignes
            ws.SheetView.FreezeRows(12);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  HELPERS FORMATAGE
        // ═════════════════════════════════════════════════════════════════════

        private static void FormatMontantRange(IXLWorksheet ws, int row, int colStart, int colEnd)
        {
            for (int c = colStart; c <= colEnd; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0";
        }

        private static void FormatLigneData(IXLWorksheet ws, int row)
        {
            for (int c = 1; c <= 20; c++)
            {
                var cell = ws.Cell(row, c);
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#CCCCCC");
                cell.Style.Font.FontSize = 9;
                if (c == 3 || c == 4 || c == 5)
                    cell.Style.Alignment.WrapText = true;
            }
        }

        private static void StyleEntete1(IXLCell cell)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 10;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  FORMATAGE DES VALEURS MÉTIER
        // ═════════════════════════════════════════════════════════════════════

        private static string FormatNomPrenom(Salarie s)
        {
            var civ = s.Civilite switch
            {
                Civilite.Monsieur => "M.",
                Civilite.Madame => "Mme",
                Civilite.Mademoiselle => "Mlle",
                _ => ""
            };
            return $"{civ} {s.FullName?.ToUpper()}".Trim();
        }

        private static string FormatAdresse(Salarie s)
        {
            // Address1 et Address2 sont des objets DevExpress.Persistent.BaseImpl.Address
            // On utilise ToString() pour obtenir la représentation textuelle
            var a1 = s.Address1?.ToString();
            var a2 = s.Address2?.ToString();
            var parts = new[] { a1, a2 }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(", ", parts);
        }

        private static string FormatSexe(Salarie s) => s.Sexe switch
        {
            Sexe.Masculin => "H",
            Sexe.Feminin => "F",
            _ => ""
        };

        private static string FormatNationalite(Salarie s)
        {
            var nat = s.Nationalite?.ToLower() ?? "";
            if (nat.Contains("sénégal") || nat.Contains("senegal")) return "S";
            if (nat.Contains("français") || nat.Contains("france")) return "F";
            if (nat.Contains("liban") || nat.Contains("syrie")) return "L";
            // Pays africains autres
            var paysAfrique = new[] { "mali", "guinée", "mauritanie", "côte d'ivoire",
                                       "burkina", "niger", "togo", "bénin", "cameroun", "congo" };
            if (paysAfrique.Any(p => nat.Contains(p))) return "A";
            return string.IsNullOrWhiteSpace(nat) ? "S" : "E";
        }

        private static string FormatSituationFamille(Salarie s) => s.SatutMarital switch
        {
            SituationMaritale.Celibataire => "C",
            SituationMaritale.Marie => "M",
            SituationMaritale.Divorce => "D",
            SituationMaritale.Veuf => "V",
            _ => "C"
        };

        // ═════════════════════════════════════════════════════════════════════
        //  DTO INTERNE
        // ═════════════════════════════════════════════════════════════════════

        private class Ligne1024
        {
            public int Ordre { get; set; }
            public string Matricule { get; set; }
            public string NomPrenom { get; set; }
            public string Emploi { get; set; }
            public string Adresse { get; set; }
            public string Sexe { get; set; }
            public string Nationalite { get; set; }
            public string SituationFamille { get; set; }
            public int NbEnfants { get; set; }
            public int NbEpouses { get; set; }
            public double NbParts { get; set; }
            public string Periode { get; set; }
            public decimal MontantAnnuel { get; set; }
            public decimal EvalAvantages { get; set; }
            public decimal TotalBrut { get; set; }
            public decimal IR { get; set; }
            public decimal TRIMF { get; set; }
            public decimal CFCE { get; set; }
            public decimal IndemnitesFrais { get; set; }
        }
    }
}
