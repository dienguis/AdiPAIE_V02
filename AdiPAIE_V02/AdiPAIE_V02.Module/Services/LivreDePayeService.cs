// AdiPAIE_V02.Module/Services/LivreDePayeService.cs
// Génère le Livre de Paie (journal de paie) mensuel en Excel
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
    /// Génère le Livre de Paie mensuel — document obligatoire (Code du Travail sénégalais, art. L.120).
    ///
    /// Colonnes :
    ///   Matricule | Nom | Département | Catégorie | Jours |
    ///   Salaire Base | Indemnité Logement | Sursalaire | Prime Transport | Avantage Véhicule |
    ///   Brut Fiscal | Brut Social |
    ///   IPRES RG | IPRES RC | TRIMF | IR |
    ///   Total Cotisations | Total Retenues Fiscales | Net à Payer
    ///
    /// Ligne de totaux en bas + résumé entreprise.
    /// </summary>
    public static class LivreDePayeService
    {
        private static readonly XLColor BG_HEADER = XLColor.FromHtml("#0F6E56");
        private static readonly XLColor BG_GROUP = XLColor.FromHtml("#E8F5E9");
        private static readonly XLColor BG_TOTAL = XLColor.FromHtml("#FFF2CC");
        private static readonly XLColor BG_ALT = XLColor.FromHtml("#F7F7F7");
        private static readonly XLColor FG_WHITE = XLColor.White;

        public static byte[] Generer(IObjectSpace os, int annee, int mois)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (mois < 1 || mois > 12)
                throw new UserFriendlyException($"Mois invalide : {mois}.");

            // Société
            var company = os.GetObjectsQuery<Company>().FirstOrDefault();
            var raisonSociale = company?.RaisonSociale ?? "";
            var ninea = company?.NINEA ?? "";

            // Bulletins validés du mois
            var statuts = new[]
            {
                BulletinStatut.Valide, BulletinStatut.Exporte,
                BulletinStatut.Envoye, BulletinStatut.Cloture,
                BulletinStatut.Comptabilise
            };

            var bulletins = os.GetObjectsQuery<Bulletin>()
                .Where(b => b.Annee == annee && b.Mois == mois && statuts.Contains(b.Statut))
                .ToList();

            if (!bulletins.Any())
                throw new UserFriendlyException(
                    $"Aucun bulletin validé trouvé pour {mois:D2}/{annee}.");

            // Construire les lignes du livre
            var lignes = bulletins
                .Where(b => b.Salarie != null)
                .Select(b =>
                {
                    var sal = b.Salarie;
                    return new LignePaye
                    {
                        Matricule = sal.Matricule ?? "",
                        NomPrenom = sal.FullName?.ToUpper() ?? "",
                        Departement = sal.Departement?.Nom ?? "",
                        Categorie = sal.Categories?.Intitule ?? "",
                        Jours = b.JoursTravailles,
                        SalaireBase = sal.SalaireBase,
                        IndemniteLogement = sal.IndemniteLogement,
                        Sursalaire = sal.Sursalaire,
                        PrimeTransport = sal.PrimeTransport,
                        AvantageVehicule = sal.AvantageVehicule,
                        BrutFiscal = b.BrutFiscal,
                        BrutSocial = b.BrutSocial,
                        IPRES_RG = b.IPRES_RG_Mois,
                        IPRES_RC = b.IPRES_RC_Mois,
                        TRIMF = b.TRIMF_Mois,
                        IR = b.IR_Mois,
                        TotalCotisations = b.TotalCotisationsSociales,
                        TotalRetenues = b.TotalRetenuesFiscales,
                        NetAPayer = b.NetAPayer,
                    };
                })
                .OrderBy(l => l.NomPrenom)
                .ToList();

            // ── Générer Excel ────────────────────────────────────────────
            using var wb = new XLWorkbook();
            var nomMois = NomMoisFr(mois);
            var ws = wb.Worksheets.Add($"Livre de Paie {nomMois} {annee}");

            // ── En-tête entreprise ───────────────────────────────────────
            int totalCols = 19;
            ws.Cell(1, 1).Value = $"LIVRE DE PAIE — {nomMois.ToUpper()} {annee}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.FontColor = BG_HEADER;
            ws.Range(1, 1, 1, totalCols).Merge();

            ws.Cell(2, 1).Value = $"Société : {raisonSociale}";
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 7).Value = $"NINEA : {ninea}";
            ws.Cell(2, 7).Style.Font.Bold = true;
            ws.Cell(3, 1).Value = $"Période : {nomMois} {annee}";
            ws.Cell(3, 1).Style.Font.Italic = true;
            ws.Cell(3, 7).Value = $"Effectif : {lignes.Count} salarié(s)";
            ws.Cell(3, 7).Style.Font.Italic = true;

            // ── Groupes (row 5) ─────────────────────────────────────────
            // Identification (cols 1-5)
            ws.Cell(5, 1).Value = "Identification";
            ws.Range(5, 1, 5, 5).Merge();
            StyleGroupe(ws.Range(5, 1, 5, 5));

            // Rémunération (cols 6-10)
            ws.Cell(5, 6).Value = "Rémunération";
            ws.Range(5, 6, 5, 10).Merge();
            StyleGroupe(ws.Range(5, 6, 5, 10));

            // Bruts (cols 11-12)
            ws.Cell(5, 11).Value = "Bruts";
            ws.Range(5, 11, 5, 12).Merge();
            StyleGroupe(ws.Range(5, 11, 5, 12));

            // Cotisations & Retenues (cols 13-18)
            ws.Cell(5, 13).Value = "Cotisations & Retenues";
            ws.Range(5, 13, 5, 18).Merge();
            StyleGroupe(ws.Range(5, 13, 5, 18));

            // Net (col 19)
            ws.Cell(5, 19).Value = "Net";
            StyleGroupe(ws.Range(5, 19, 5, 19));

            // ── En-têtes colonnes (row 6) ────────────────────────────────
            var headers = new[]
            {
                "Matricule", "Nom & Prénom", "Département", "Catégorie", "Jours",
                "Sal. Base", "Indem. Logt", "Sursalaire", "Prime Transp.", "Av. Véhicule",
                "Brut Fiscal", "Brut Social",
                "IPRES RG", "IPRES RC", "TRIMF", "IR",
                "Tot. Cotis.", "Tot. Retenues",
                "Net à Payer"
            };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(6, c + 1);
                cell.Value = headers[c];
                cell.Style.Fill.BackgroundColor = BG_HEADER;
                cell.Style.Font.FontColor = FG_WHITE;
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 9;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // ── Données ─────────────────────────────────────────────────
            int r = 7;
            foreach (var l in lignes)
            {
                ws.Cell(r, 1).Value = l.Matricule;
                ws.Cell(r, 2).Value = l.NomPrenom;
                ws.Cell(r, 3).Value = l.Departement;
                ws.Cell(r, 4).Value = l.Categorie;
                ws.Cell(r, 5).Value = l.Jours;
                ws.Cell(r, 6).Value = (double)l.SalaireBase;
                ws.Cell(r, 7).Value = (double)l.IndemniteLogement;
                ws.Cell(r, 8).Value = (double)l.Sursalaire;
                ws.Cell(r, 9).Value = (double)l.PrimeTransport;
                ws.Cell(r, 10).Value = (double)l.AvantageVehicule;
                ws.Cell(r, 11).Value = (double)l.BrutFiscal;
                ws.Cell(r, 12).Value = (double)l.BrutSocial;
                ws.Cell(r, 13).Value = (double)l.IPRES_RG;
                ws.Cell(r, 14).Value = (double)l.IPRES_RC;
                ws.Cell(r, 15).Value = (double)l.TRIMF;
                ws.Cell(r, 16).Value = (double)l.IR;
                ws.Cell(r, 17).Value = (double)l.TotalCotisations;
                ws.Cell(r, 18).Value = (double)l.TotalRetenues;
                ws.Cell(r, 19).Value = (double)l.NetAPayer;

                // Format numérique pour les montants
                for (int c = 5; c <= 19; c++)
                    ws.Cell(r, c).Style.NumberFormat.Format = "#,##0";

                // Style lignes
                for (int c = 1; c <= 19; c++)
                {
                    ws.Cell(r, c).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    ws.Cell(r, c).Style.Border.BottomBorderColor = XLColor.FromHtml("#CCCCCC");
                    ws.Cell(r, c).Style.Font.FontSize = 9;
                }
                if (r % 2 == 0)
                    ws.Row(r).Style.Fill.BackgroundColor = BG_ALT;

                r++;
            }

            // ── Ligne de totaux ──────────────────────────────────────────
            ws.Cell(r, 1).Value = "TOTAUX";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 2).Value = $"{lignes.Count} salarié(s)";
            ws.Cell(r, 2).Style.Font.Bold = true;

            var totaux = new (int col, decimal val)[]
            {
                (5,  lignes.Sum(l => l.Jours)),
                (6,  lignes.Sum(l => l.SalaireBase)),
                (7,  lignes.Sum(l => l.IndemniteLogement)),
                (8,  lignes.Sum(l => l.Sursalaire)),
                (9,  lignes.Sum(l => l.PrimeTransport)),
                (10, lignes.Sum(l => l.AvantageVehicule)),
                (11, lignes.Sum(l => l.BrutFiscal)),
                (12, lignes.Sum(l => l.BrutSocial)),
                (13, lignes.Sum(l => l.IPRES_RG)),
                (14, lignes.Sum(l => l.IPRES_RC)),
                (15, lignes.Sum(l => l.TRIMF)),
                (16, lignes.Sum(l => l.IR)),
                (17, lignes.Sum(l => l.TotalCotisations)),
                (18, lignes.Sum(l => l.TotalRetenues)),
                (19, lignes.Sum(l => l.NetAPayer)),
            };
            foreach (var (col, val) in totaux)
            {
                ws.Cell(r, col).Value = (double)val;
                ws.Cell(r, col).Style.Font.Bold = true;
                ws.Cell(r, col).Style.NumberFormat.Format = "#,##0";
            }
            for (int c = 1; c <= 19; c++)
            {
                ws.Cell(r, c).Style.Fill.BackgroundColor = BG_TOTAL;
                ws.Cell(r, c).Style.Font.Bold = true;
                ws.Cell(r, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // ── Résumé sous les totaux ───────────────────────────────────
            r += 2;
            ws.Cell(r, 1).Value = "Récapitulatif";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Font.FontSize = 11;
            ws.Cell(r, 1).Style.Font.FontColor = BG_HEADER;

            r++;
            var masseSalariale = lignes.Sum(l => l.BrutFiscal);
            var totalNet = lignes.Sum(l => l.NetAPayer);
            var totalCotis = lignes.Sum(l => l.TotalCotisations);
            var totalImpots = lignes.Sum(l => l.TotalRetenues);

            ws.Cell(r, 1).Value = "Masse salariale brute :";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 3).Value = (double)masseSalariale;
            ws.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 3).Style.Font.Bold = true;
            r++;

            ws.Cell(r, 1).Value = "Total cotisations sociales :";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 3).Value = (double)totalCotis;
            ws.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            r++;

            ws.Cell(r, 1).Value = "Total retenues fiscales :";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 3).Value = (double)totalImpots;
            ws.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            r++;

            ws.Cell(r, 1).Value = "Total net à payer :";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 3).Value = (double)totalNet;
            ws.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 3).Style.Font.Bold = true;
            ws.Cell(r, 3).Style.Font.FontColor = BG_HEADER;

            // ── Mise en page ─────────────────────────────────────────────
            ws.Column(1).Width = 11;   // Matricule
            ws.Column(2).Width = 26;   // Nom
            ws.Column(3).Width = 16;   // Département
            ws.Column(4).Width = 12;   // Catégorie
            ws.Column(5).Width = 7;    // Jours
            for (int c = 6; c <= 19; c++) ws.Column(c).Width = 13;
            ws.Column(19).Width = 14;  // Net à Payer un peu plus large

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A3Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(5, 6);
            ws.SheetView.FreezeRows(6);
            ws.SheetView.FreezeColumns(2);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static void StyleGroupe(IXLRange range)
        {
            range.Style.Fill.BackgroundColor = BG_GROUP;
            range.Style.Font.Bold = true;
            range.Style.Font.FontSize = 10;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        private static string NomMoisFr(int mois) => mois switch
        {
            1 => "Janvier", 2 => "Février", 3 => "Mars",
            4 => "Avril", 5 => "Mai", 6 => "Juin",
            7 => "Juillet", 8 => "Août", 9 => "Septembre",
            10 => "Octobre", 11 => "Novembre", 12 => "Décembre",
            _ => mois.ToString()
        };

        private class LignePaye
        {
            public string Matricule { get; set; }
            public string NomPrenom { get; set; }
            public string Departement { get; set; }
            public string Categorie { get; set; }
            public int Jours { get; set; }
            public decimal SalaireBase { get; set; }
            public decimal IndemniteLogement { get; set; }
            public decimal Sursalaire { get; set; }
            public decimal PrimeTransport { get; set; }
            public decimal AvantageVehicule { get; set; }
            public decimal BrutFiscal { get; set; }
            public decimal BrutSocial { get; set; }
            public decimal IPRES_RG { get; set; }
            public decimal IPRES_RC { get; set; }
            public decimal TRIMF { get; set; }
            public decimal IR { get; set; }
            public decimal TotalCotisations { get; set; }
            public decimal TotalRetenues { get; set; }
            public decimal NetAPayer { get; set; }
        }
    }
}
