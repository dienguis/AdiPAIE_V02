using DevExpress.Drawing;
using DevExpress.Utils;
using DevExpress.Xpo;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using System;
// pour ExportToPdf(...)

using System.Drawing;
using System.IO;
using System.Linq;

namespace AdiPAIE_V02.Module.Reports
{
    // Rapport minimal programmatique pour un Bulletin
    // DataSource attendu: IEnumerable<Bulletin> (ex: new[] { bulletin })
    public class BulletinReport : XtraReport
    {
        private TopMarginBand topMarginBand1;
        private DetailBand detailBand1;
        private BottomMarginBand bottomMarginBand1;

        public BulletinReport()
        {
            // Tailles de page par défaut (A4)
            this.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.A4;
            this.Margins = new System.Drawing.Printing.Margins(50, 50, 50, 50);

            // Bandes
            var reportHeader = new ReportHeaderBand { HeightF = 60 };
            var detail = new DetailBand { HeightF = 20 };
            var groupHeader = new GroupHeaderBand { HeightF = 26 };
            var groupFooter = new GroupFooterBand { HeightF = 26 };
            var pageFooter = new PageFooterBand { HeightF = 20 };

            this.Bands.AddRange(new Band[] { reportHeader, groupHeader, detail, groupFooter, pageFooter });

            // Titre
            var title = new XRLabel {
                Text = "Bulletin de paie",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                BoundsF = new RectangleF(0, 0, PageWidth - Margins.Left - Margins.Right, 30),
                TextAlignment = TextAlignment.MiddleCenter
            };
            reportHeader.Controls.Add(title);


            // Logo (gauche)
            var logo = new XRPictureBox {
                Sizing = ImageSizeMode.Squeeze,
                BoundsF = new RectangleF(0, 0, 140, 50)
            };
            reportHeader.Controls.Add(logo);

            // Nom société (depuis Company.RaisonSociale), aligné à gauche sous le logo
            var companyLbl = new XRLabel {
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BoundsF = new RectangleF(130, 0, PageWidth - Margins.Left - Margins.Right - 130, 22),
                TextAlignment = TextAlignment.MiddleLeft
            };
            reportHeader.Controls.Add(companyLbl);

            // Adresse + identifiants (NINEA / RC) sous la raison sociale
            var addressLbl = new XRLabel {
                Font = new Font("Segoe UI", 8, FontStyle.Regular),
                BoundsF = new RectangleF(130, 22, PageWidth - Margins.Left - Margins.Right - 130, 16),
                TextAlignment = TextAlignment.MiddleLeft
            };
            var idsLbl = new XRLabel {
                Font = new Font("Segoe UI", 8, FontStyle.Regular),
                BoundsF = new RectangleF(130, 38, PageWidth - Margins.Left - Margins.Right - 130, 16),
                TextAlignment = TextAlignment.MiddleLeft
            };
            reportHeader.Controls.Add(addressLbl);
            reportHeader.Controls.Add(idsLbl);
    


            // Signature (footer, gauche)
            var signPic = new XRPictureBox { Sizing = ImageSizeMode.Squeeze, BoundsF = new RectangleF(0, 0, 140, 50) };
            // Cachet (footer, droite)
            var stampPic = new XRPictureBox { Sizing = ImageSizeMode.Squeeze, BoundsF = new RectangleF(480, 0, 120, 40) };

            var signName = new XRLabel { BoundsF = new RectangleF(0, 44, 300, 18), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            var signTitle = new XRLabel { BoundsF = new RectangleF(0, 62, 300, 16), Font = new Font("Segoe UI", 8, FontStyle.Regular) };

            groupFooter.Controls.Add(signPic);
            groupFooter.Controls.Add(stampPic);
            groupFooter.Controls.Add(signName);
            groupFooter.Controls.Add(signTitle);

            // Charger images/infos depuis ParametresPaie (singleton)
            try {
                using (var uow = new UnitOfWork())
                {
                    var prm = new XPQuery<AdiPAIE_V02.Module.BusinessObjects.ParametresPaie>(uow).FirstOrDefault();
                    var company = new XPQuery<AdiPAIE_V02.Module.BusinessObjects.Company>(uow).FirstOrDefault();
                    // Logo : priorité société, sinon paramètres
                    var logoSrc = ToImageSource(company?.LogoImage ?? prm?.LogoImage);
                    if (logoSrc != null) logo.ImageSource = logoSrc;

                    // Signature & cachet
                    var signSrc = ToImageSource(prm?.SignatureImage);
                    if (signSrc != null) signPic.ImageSource = signSrc;

                    var stampSrc = ToImageSource(prm?.CachetImage);
                    if (stampSrc != null) stampPic.ImageSource = stampSrc;

                    // Libellés
                    signName.Text = prm?.SignatoryName ?? string.Empty;
                    signTitle.Text = prm?.SignatoryTitle ?? string.Empty;

                    // Société (ajuste l'adresse : évite la double "Address")
                    companyLbl.Text = company?.RaisonSociale ?? string.Empty;
                    addressLbl.Text = company?.Address ?? company?.Address ?? string.Empty;
                    idsLbl.Text = $"NINEA: {company?.NINEA ?? ""}    RC: {company?.RC ?? ""}";
                }

            }
            catch { /* silencieux si indisponible */ }


            // Infos salarié + période
            var info = new XRLabel {
                ExpressionBindings = {
                    new ExpressionBinding("BeforePrint", "Text", "Concat('Salarié: ', [Salarie].[NomComplet], '  |  Période: ', PadLeft([Mois], 2, '0'), '/', [Annee])")
                },
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                BoundsF = new RectangleF(0, 32, PageWidth - Margins.Left - Margins.Right, 20)
            };
            reportHeader.Controls.Add(info);

            // Entêtes colonnes lignes
            var hdrTable = new XRTable { BoundsF = new RectangleF(0, 0, PageWidth - Margins.Left - Margins.Right, 26), BackColor = Color.Gainsboro };
            hdrTable.BeginInit();
            var hdrRow = new XRTableRow();
            hdrRow.Cells.AddRange(new[] {
                MakeHeaderCell("Code", 80),
                MakeHeaderCell("Libellé", 260),
                MakeHeaderCell("Base", 80, TextAlignment.MiddleRight),
                MakeHeaderCell("Taux", 80, TextAlignment.MiddleRight),
                MakeHeaderCell("Montant", 100, TextAlignment.MiddleRight)
            });
            hdrTable.Rows.Add(hdrRow);
            hdrTable.EndInit();
            groupHeader.Controls.Add(hdrTable);

            // Détail lignes
            var tbl = new XRTable { BoundsF = new RectangleF(0, 0, PageWidth - Margins.Left - Margins.Right, 20) };
            tbl.BeginInit();
            var row = new XRTableRow();
            row.Cells.AddRange(new[] {
                MakeDetailCell("[LigneCode]", 80, TextAlignment.MiddleLeft),
                MakeDetailCell("[Libelle]", 260, TextAlignment.MiddleLeft),
                MakeDetailCell("FormatString('{0:n2}', [BaseCalcul])", 80, TextAlignment.MiddleRight, true),
                MakeDetailCell("FormatString('{0:n2}', [Taux])", 80, TextAlignment.MiddleRight, true),
                MakeDetailCell("FormatString('{0:n0}', [Montant])", 100, TextAlignment.MiddleRight, true),
            });
            tbl.Rows.Add(row);
            tbl.EndInit();
            detail.Controls.Add(tbl);

            // Totaux (exemple: somme Montant)
            var totalLbl = new XRLabel {
                Text = "Total",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BoundsF = new RectangleF(0, 0, 520, 20),
                TextAlignment = TextAlignment.MiddleRight
            };
            var totalVal = new XRLabel {
                ExpressionBindings = { new ExpressionBinding("BeforePrint", "Text", "FormatString('{0:n0}', Sum([Montant]))") },
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BoundsF = new RectangleF(520, 0, 100, 20),
                TextAlignment = TextAlignment.MiddleRight
            };
            groupFooter.Controls.AddRange(new XRControl[] { totalLbl, totalVal });

            // Pagination
            var pageInfo = new XRPageInfo {
                PageInfo = PageInfo.NumberOfTotal,
                BoundsF = new RectangleF(0, 0, PageWidth - Margins.Left - Margins.Right, 20),
                TextAlignment = TextAlignment.MiddleRight
            };
            pageFooter.Controls.Add(pageInfo);

            // Source de données: On utilisera DetailReportBand pour Lignes si tu veux; ici on suppose que Bulletin expose une collection "Lignes"
            // Simplification: on lie le Detail à Lignes via une DetailReportBand
            var dr = new DetailReportBand();
            this.Bands.Add(dr);
            dr.DataMember = "Lignes";
            dr.Bands.Add(detail);
            dr.Bands.Add(groupFooter);
            this.DataMember = null; // racine = Bulletin lui-même
        }

        private XRTableCell MakeHeaderCell(string text, float width, TextAlignment align = TextAlignment.MiddleLeft) {
            return new XRTableCell {
                Text = text,
                WidthF = width,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlignment = align,
                Padding = new PaddingInfo(4, 4, 0, 0)
            };
        }
        private XRTableCell MakeDetailCell(string exprOrText, float width, TextAlignment align, bool isExpr=false) {
            var c = new XRTableCell {
                WidthF = width,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                TextAlignment = align,
                Padding = new PaddingInfo(4, 4, 0, 0)
            };
            if (isExpr || exprOrText.StartsWith("[")) {
                c.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", exprOrText));
            } else {
                c.Text = exprOrText;
            }
            return c;
        }
        // Helper: byte[] -> ImageSource (25.1)
        private static ImageSource ToImageSource(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;

            using (var ms = new MemoryStream(bytes))
            using (DXImage dx = DXImage.FromStream(ms))   // step 1
            {
                return new ImageSource(dx);               // step 2
            }
        }

        private void InitializeComponent()
        {
            this.topMarginBand1 = new DevExpress.XtraReports.UI.TopMarginBand();
            this.detailBand1 = new DevExpress.XtraReports.UI.DetailBand();
            this.bottomMarginBand1 = new DevExpress.XtraReports.UI.BottomMarginBand();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            // 
            // topMarginBand1
            // 
            this.topMarginBand1.Name = "topMarginBand1";
            // 
            // detailBand1
            // 
            this.detailBand1.Name = "detailBand1";
            // 
            // bottomMarginBand1
            // 
            this.bottomMarginBand1.Name = "bottomMarginBand1";
            // 
            // BulletinReport
            // 
            this.Bands.AddRange(new DevExpress.XtraReports.UI.Band[] {
            this.topMarginBand1,
            this.detailBand1,
            this.bottomMarginBand1});
            this.Version = "25.1";
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();

        }
    }
}
