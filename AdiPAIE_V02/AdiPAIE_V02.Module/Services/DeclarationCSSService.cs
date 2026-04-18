// AdiPAIE_V02.Module/Services/DeclarationCSSService.cs
// Génère le bordereau mensuel CSS (Caisse de Sécurité Sociale)
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
    /// Génère le bordereau mensuel CSS.
    /// Colonnes : Matricule, Nom, Brut Social,
    ///            Accident Travail (Employeur), Allocation Familiale (Employeur), Total CSS.
    /// </summary>
    public static class DeclarationCSSService
    {
        private static readonly XLColor BG_HEADER = XLColor.FromHtml("#0F6E56");
        private static readonly XLColor BG_TOTAL = XLColor.FromHtml("#FFF2CC");
        private static readonly XLColor FG_WHITE = XLColor.White;

        public static byte[] Generer(IObjectSpace os, int annee, int mois)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (mois < 1 || mois > 12)
                throw new UserFriendlyException($"Mois invalide : {mois}.");

            var company = os.GetObjectsQuery<Company>().FirstOrDefault();
            var raisonSociale = company?.RaisonSociale ?? "";
            var ninea = company?.NINEA ?? "";

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

            var bulletinOids = bulletins.Select(b => b.Oid).ToList();
            var lignesCSS = os.GetObjectsQuery<BulletinLigne>()
                .Where(l => bulletinOids.Contains(l.Bulletin.Oid))
                .ToList()
                .Where(l => l.Rubrique?.Canonique == RubriqueCanonique.CSS_AccidentTravail
                         || l.Rubrique?.Canonique == RubriqueCanonique.CSS_AllocationFamiliale)
                .Select(l => new
                {
                    SalarieOid = l.Bulletin?.Salarie?.Oid ?? Guid.Empty,
                    l.MontantEmployeur,
                    Canonique = l.Rubrique?.Canonique
                })
                .ToList();

            var parSalarie = lignesCSS.GroupBy(l => l.SalarieOid)
                .ToDictionary(g => g.Key, g => g.ToList());

            var lignes = bulletins
                .Where(b => b.Salarie != null)
                .Select(b =>
                {
                    var sal = b.Salarie;
                    var ll = parSalarie.TryGetValue(sal.Oid, out var list) ? list : new();

                    var at = ll.Where(l => l.Canonique == RubriqueCanonique.CSS_AccidentTravail)
                              .Sum(l => l.MontantEmployeur);
                    var af = ll.Where(l => l.Canonique == RubriqueCanonique.CSS_AllocationFamiliale)
                              .Sum(l => l.MontantEmployeur);

                    return new LigneCSS
                    {
                        Matricule = sal.Matricule ?? "",
                        NomPrenom = sal.FullName?.ToUpper() ?? "",
                        BrutSocial = b.BrutSocial,
                        AccidentTravail = at,
                        AllocationFamiliale = af,
                        Total = at + af,
                    };
                })
                .OrderBy(l => l.NomPrenom)
                .ToList();

            // Générer Excel
            using var wb = new XLWorkbook();
            var nomMois = NomMoisFr(mois);
            var ws = wb.Worksheets.Add($"CSS {nomMois} {annee}");

            // En-tête
            ws.Cell(1, 1).Value = $"BORDEREAU CSS — {nomMois.ToUpper()} {annee}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.FontColor = BG_HEADER;
            ws.Range(1, 1, 1, 6).Merge();

            ws.Cell(2, 1).Value = $"Société : {raisonSociale}";
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 4).Value = $"NINEA : {ninea}";
            ws.Cell(2, 4).Style.Font.Bold = true;
            ws.Cell(3, 1).Value = $"Période : {nomMois} {annee}";
            ws.Cell(3, 1).Style.Font.Italic = true;

            // En-têtes colonnes (row 5)
            var headers = new[] { "Matricule", "Nom", "Brut Social",
                                  "Accident Travail", "Allocation Familiale", "Total CSS" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(5, c + 1);
                cell.Value = headers[c];
                cell.Style.Fill.BackgroundColor = BG_HEADER;
                cell.Style.Font.FontColor = FG_WHITE;
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 10;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Données
            int r = 6;
            foreach (var l in lignes)
            {
                ws.Cell(r, 1).Value = l.Matricule;
                ws.Cell(r, 2).Value = l.NomPrenom;
                ws.Cell(r, 3).Value = (double)l.BrutSocial;
                ws.Cell(r, 4).Value = (double)l.AccidentTravail;
                ws.Cell(r, 5).Value = (double)l.AllocationFamiliale;
                ws.Cell(r, 6).Value = (double)l.Total;

                for (int c = 3; c <= 6; c++)
                    ws.Cell(r, c).Style.NumberFormat.Format = "#,##0";
                for (int c = 1; c <= 6; c++)
                {
                    ws.Cell(r, c).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    ws.Cell(r, c).Style.Border.BottomBorderColor = XLColor.FromHtml("#CCCCCC");
                    ws.Cell(r, c).Style.Font.FontSize = 10;
                }
                if (r % 2 == 0)
                    ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F7F7F7");
                r++;
            }

            // Totaux
            ws.Cell(r, 1).Value = "TOTAL";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 2).Value = $"{lignes.Count} salarié(s)";
            ws.Cell(r, 2).Style.Font.Bold = true;

            var totaux = new (int c, decimal v)[]
            {
                (3, lignes.Sum(l => l.BrutSocial)),
                (4, lignes.Sum(l => l.AccidentTravail)),
                (5, lignes.Sum(l => l.AllocationFamiliale)),
                (6, lignes.Sum(l => l.Total)),
            };
            foreach (var (c, v) in totaux)
            {
                ws.Cell(r, c).Value = (double)v;
                ws.Cell(r, c).Style.Font.Bold = true;
                ws.Cell(r, c).Style.NumberFormat.Format = "#,##0";
            }
            for (int c = 1; c <= 6; c++)
            {
                ws.Cell(r, c).Style.Fill.BackgroundColor = BG_TOTAL;
                ws.Cell(r, c).Style.Font.Bold = true;
                ws.Cell(r, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Mise en page
            ws.Column(1).Width = 12;
            ws.Column(2).Width = 28;
            ws.Column(3).Width = 16;
            ws.Column(4).Width = 16;
            ws.Column(5).Width = 18;
            ws.Column(6).Width = 14;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(5, 5);
            ws.SheetView.FreezeRows(5);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static string NomMoisFr(int mois) => mois switch
        {
            1 => "Janvier", 2 => "Février", 3 => "Mars",
            4 => "Avril", 5 => "Mai", 6 => "Juin",
            7 => "Juillet", 8 => "Août", 9 => "Septembre",
            10 => "Octobre", 11 => "Novembre", 12 => "Décembre",
            _ => mois.ToString()
        };

        private class LigneCSS
        {
            public string Matricule { get; set; }
            public string NomPrenom { get; set; }
            public decimal BrutSocial { get; set; }
            public decimal AccidentTravail { get; set; }
            public decimal AllocationFamiliale { get; set; }
            public decimal Total { get; set; }
        }
    }
}
