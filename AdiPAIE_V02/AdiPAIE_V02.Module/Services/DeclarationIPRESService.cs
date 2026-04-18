// AdiPAIE_V02.Module/Services/DeclarationIPRESService.cs
// Génère le bordereau mensuel IPRES (Régime Général + Régime Cadre)
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
    /// Génère le bordereau mensuel IPRES.
    /// Colonnes : Matricule, Nom, Catégorie, Brut Social,
    ///            RG Salarié, RG Employeur, RC Salarié, RC Employeur, Total.
    /// </summary>
    public static class DeclarationIPRESService
    {
        private static readonly XLColor BG_HEADER = XLColor.FromHtml("#0F6E56");
        private static readonly XLColor BG_TOTAL = XLColor.FromHtml("#FFF2CC");
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

            // Charger les lignes IPRES
            var bulletinOids = bulletins.Select(b => b.Oid).ToList();
            var lignesIPRES = os.GetObjectsQuery<BulletinLigne>()
                .Where(l => bulletinOids.Contains(l.Bulletin.Oid))
                .ToList()
                .Where(l => l.Rubrique?.Canonique == RubriqueCanonique.IPRES_RG
                         || l.Rubrique?.Canonique == RubriqueCanonique.IPRES_RC)
                .Select(l => new
                {
                    SalarieOid = l.Bulletin?.Salarie?.Oid ?? Guid.Empty,
                    l.Montant,
                    l.MontantEmployeur,
                    Canonique = l.Rubrique?.Canonique
                })
                .ToList();

            var parSalarie = lignesIPRES.GroupBy(l => l.SalarieOid)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Construire les lignes du rapport
            var lignes = bulletins
                .Where(b => b.Salarie != null)
                .Select(b =>
                {
                    var sal = b.Salarie;
                    var ll = parSalarie.TryGetValue(sal.Oid, out var list) ? list : new();

                    var rgSal = ll.Where(l => l.Canonique == RubriqueCanonique.IPRES_RG).Sum(l => l.Montant);
                    var rgEmp = ll.Where(l => l.Canonique == RubriqueCanonique.IPRES_RG).Sum(l => l.MontantEmployeur);
                    var rcSal = ll.Where(l => l.Canonique == RubriqueCanonique.IPRES_RC).Sum(l => l.Montant);
                    var rcEmp = ll.Where(l => l.Canonique == RubriqueCanonique.IPRES_RC).Sum(l => l.MontantEmployeur);

                    return new LigneIPRES
                    {
                        Matricule = sal.Matricule ?? "",
                        NomPrenom = sal.FullName?.ToUpper() ?? "",
                        Categorie = sal.Categories?.Intitule ?? "",
                        BrutSocial = b.BrutSocial,
                        RG_Sal = rgSal,
                        RG_Emp = rgEmp,
                        RC_Sal = rcSal,
                        RC_Emp = rcEmp,
                        Total = rgSal + rgEmp + rcSal + rcEmp,
                    };
                })
                .OrderBy(l => l.NomPrenom)
                .ToList();

            // Générer Excel
            using var wb = new XLWorkbook();
            var nomMois = NomMoisFr(mois);
            var ws = wb.Worksheets.Add($"IPRES {nomMois} {annee}");

            // En-tête
            ws.Cell(1, 1).Value = $"BORDEREAU IPRES — {nomMois.ToUpper()} {annee}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.FontColor = BG_HEADER;
            ws.Range(1, 1, 1, 9).Merge();

            ws.Cell(2, 1).Value = $"Société : {raisonSociale}";
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 5).Value = $"NINEA : {ninea}";
            ws.Cell(2, 5).Style.Font.Bold = true;
            ws.Cell(3, 1).Value = $"Période : {nomMois} {annee}";
            ws.Cell(3, 1).Style.Font.Italic = true;

            // Ligne de groupes (row 5)
            ws.Cell(5, 5).Value = "Régime Général";
            ws.Range(5, 5, 5, 6).Merge();
            StyleGroupe(ws.Range(5, 5, 5, 6));

            ws.Cell(5, 7).Value = "Régime Cadre";
            ws.Range(5, 7, 5, 8).Merge();
            StyleGroupe(ws.Range(5, 7, 5, 8));

            foreach (var col in new[] { 1, 2, 3, 4, 9 })
            {
                ws.Cell(5, col).Style.Fill.BackgroundColor = BG_HEADER;
                ws.Cell(5, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // En-têtes colonnes (row 6)
            var headers = new[] { "Matricule", "Nom", "Catégorie", "Brut Social",
                                  "Salarié", "Employeur", "Salarié", "Employeur", "Total" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(6, c + 1);
                cell.Value = headers[c];
                cell.Style.Fill.BackgroundColor = BG_HEADER;
                cell.Style.Font.FontColor = FG_WHITE;
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 9;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Données
            int r = 7;
            foreach (var l in lignes)
            {
                ws.Cell(r, 1).Value = l.Matricule;
                ws.Cell(r, 2).Value = l.NomPrenom;
                ws.Cell(r, 3).Value = l.Categorie;
                ws.Cell(r, 4).Value = (double)l.BrutSocial;
                ws.Cell(r, 5).Value = (double)l.RG_Sal;
                ws.Cell(r, 6).Value = (double)l.RG_Emp;
                ws.Cell(r, 7).Value = (double)l.RC_Sal;
                ws.Cell(r, 8).Value = (double)l.RC_Emp;
                ws.Cell(r, 9).Value = (double)l.Total;

                for (int c = 4; c <= 9; c++)
                    ws.Cell(r, c).Style.NumberFormat.Format = "#,##0";
                for (int c = 1; c <= 9; c++)
                {
                    ws.Cell(r, c).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    ws.Cell(r, c).Style.Border.BottomBorderColor = XLColor.FromHtml("#CCCCCC");
                    ws.Cell(r, c).Style.Font.FontSize = 9;
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

            var cols = new (int c, decimal v)[]
            {
                (4, lignes.Sum(l => l.BrutSocial)),
                (5, lignes.Sum(l => l.RG_Sal)),
                (6, lignes.Sum(l => l.RG_Emp)),
                (7, lignes.Sum(l => l.RC_Sal)),
                (8, lignes.Sum(l => l.RC_Emp)),
                (9, lignes.Sum(l => l.Total)),
            };
            foreach (var (c, v) in cols)
            {
                ws.Cell(r, c).Value = (double)v;
                ws.Cell(r, c).Style.Font.Bold = true;
                ws.Cell(r, c).Style.NumberFormat.Format = "#,##0";
            }
            for (int c = 1; c <= 9; c++)
            {
                ws.Cell(r, c).Style.Fill.BackgroundColor = BG_TOTAL;
                ws.Cell(r, c).Style.Font.Bold = true;
                ws.Cell(r, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Sous-totaux
            r++;
            var totalRG = lignes.Sum(l => l.RG_Sal + l.RG_Emp);
            var totalRC = lignes.Sum(l => l.RC_Sal + l.RC_Emp);
            ws.Cell(r, 4).Value = "Sous-totaux :";
            ws.Cell(r, 4).Style.Font.Bold = true;
            ws.Cell(r, 4).Style.Font.Italic = true;

            ws.Cell(r, 5).Value = (double)totalRG;
            ws.Range(r, 5, r, 6).Merge();
            ws.Cell(r, 5).Style.Font.Bold = true;
            ws.Cell(r, 5).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(r, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F4C3");

            ws.Cell(r, 7).Value = (double)totalRC;
            ws.Range(r, 7, r, 8).Merge();
            ws.Cell(r, 7).Style.Font.Bold = true;
            ws.Cell(r, 7).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(r, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F4C3");

            // Mise en page
            ws.Column(1).Width = 12;
            ws.Column(2).Width = 26;
            ws.Column(3).Width = 16;
            for (int c = 4; c <= 9; c++) ws.Column(c).Width = 14;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(5, 6);
            ws.SheetView.FreezeRows(6);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static void StyleGroupe(IXLRange range)
        {
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F5E9");
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

        private class LigneIPRES
        {
            public string Matricule { get; set; }
            public string NomPrenom { get; set; }
            public string Categorie { get; set; }
            public decimal BrutSocial { get; set; }
            public decimal RG_Sal { get; set; }
            public decimal RG_Emp { get; set; }
            public decimal RC_Sal { get; set; }
            public decimal RC_Emp { get; set; }
            public decimal Total { get; set; }
        }
    }
}
