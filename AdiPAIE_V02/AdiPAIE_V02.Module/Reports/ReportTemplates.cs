using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;

using DevExpress.Persistent.Base.ReportsV2;

using DevExpress.XtraReports.UI;

namespace AdiPAIE_V02.Module.Reports
{

    public static class ReportTemplates
    {
        // Alias pour compatibilité éventuelle avec du code existant
        public static XtraReport CreateBulletinA4() => CreateBulletinA4_Simple();

        public static XtraReport CreateBulletinA4_Simple()
        {
            var rpt = new XtraReport
            {
                PaperKind = DXPaperKind.A4,
                Margins = new DXMargins(20, 20, 20, 20)
            };

            // ==== DataSource maître : Bulletin ====
            var dsBulletin = new CollectionDataSource
            {
                Name = "dsBulletin",
                ObjectTypeName = typeof(Bulletin).FullName
            };
            rpt.ComponentStorage.Add(dsBulletin);
            rpt.DataSource = dsBulletin;   // Header lit dans Bulletin

            // ==== DataSource ParametresPaie (singleton) pour logo + signature ====
            var dsParam = new CollectionDataSource
            {
                Name = "dsParametresPaie",
                ObjectTypeName = typeof(ParametresPaie).FullName,
                TopReturnedRecords = 1
            };
            rpt.ComponentStorage.Add(dsParam);

            // ==== Bands ====
            var top = new TopMarginBand();
            var bottom = new BottomMarginBand();
            var reportHeader = new ReportHeaderBand { HeightF = 160 };
            var detail = new DetailBand { HeightF = 0 };

            var dr = new DetailReportBand { DataSource = dsBulletin, DataMember = "Lignes" };
            var drHeader = new GroupHeaderBand { HeightF = 26 };
            var drDetail = new DetailBand { HeightF = 22 };
            var drFooter = new GroupFooterBand { HeightF = 120 };

            rpt.Bands.AddRange(new Band[] { top, reportHeader, detail, dr, bottom });
            dr.Bands.AddRange(new Band[] { drHeader, drDetail, drFooter });

            // ---------- Header : Logo (ParametresPaie) + Titres ----------
            var picLogo = new XRPictureBox
            {
                BoundsF = new System.Drawing.RectangleF(0, 0, 180, 58),
                Sizing = DevExpress.XtraPrinting.ImageSizeMode.Squeeze,
                Borders = DevExpress.XtraPrinting.BorderSide.None
            };
            // Bind au dsParam (pas de .DataSource sur le contrôle, on passe par DataBindings)
            picLogo.DataBindings.Add("ImageSource", dsParam, "LogoImage");
            reportHeader.Controls.Add(picLogo);

            reportHeader.Controls.Add(new XRLabel
            {
                Text = "Bulletin de paie – A4",
                Font = new DXFont("Segoe UI", 14, DXFontStyle.Bold),
                BoundsF = new System.Drawing.RectangleF(190, 0, rpt.PageWidth - rpt.Margins.Left - rpt.Margins.Right - 190, 26)
            });

            var lblPeriode = new XRLabel
            {
                Font = new DXFont("Segoe UI", 9),
                BoundsF = new System.Drawing.RectangleF(190, 28, 300, 18)
            };
            lblPeriode.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
                "'Période : ' + GetMonthName(Month([Periode])) + ' ' + GetYear([Periode])"));
            reportHeader.Controls.Add(lblPeriode);

            var lblEmisLe = new XRLabel
            {
                Font = new DXFont("Segoe UI", 9),
                BoundsF = new System.Drawing.RectangleF(500, 28, 300, 18),
                TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight
            };
            lblEmisLe.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
                "'Émis le ' + FormatString('{0:dd/MM/yyyy}', [DateEmission])"));
            reportHeader.Controls.Add(lblEmisLe);

            // Panneaux infos (contexte Bulletin)
            var panelSal = new XRPanel
            {
                BoundsF = new System.Drawing.RectangleF(0, 70, (rpt.PageWidth - rpt.Margins.Left - rpt.Margins.Right) / 2f - 6, 78),
                Borders = DevExpress.XtraPrinting.BorderSide.All,
                BorderColor = System.Drawing.Color.Gainsboro
            };
            var panelPaie = new XRPanel
            {
                BoundsF = new System.Drawing.RectangleF(panelSal.WidthF + 12, 70, panelSal.WidthF, 78),
                Borders = DevExpress.XtraPrinting.BorderSide.All,
                BorderColor = System.Drawing.Color.Gainsboro
            };
            reportHeader.Controls.AddRange(new XRControl[] { panelSal, panelPaie });

            AddKV(panelSal, 8, 8, "Salarié", "[Salarie.FullName]");
            AddKV(panelSal, 8, 26, "Matricule", "[Salarie.Matricule]");
            AddKV(panelSal, 8, 44, "Embauche", "FormatString('{0:dd/MM/yyyy}', [Salarie.DateEmbauche])");

            AddKV(panelPaie, 8, 8, "Base", "[BasePaie]");
            AddKV(panelPaie, 8, 26, "Jours/Mois", "[JoursParMois]");
            AddKV(panelPaie, 8, 44, "Heures/Semaine", "[HeuresParSemaine]");
            AddKV(panelPaie, 8, 62, "Devise", "[Devise]");

            // ---------- Tableau (header) ----------
            float tableW = panelPaie.WidthF + panelSal.WidthF + 12;
            var tblHeader = new XRTable { BoundsF = new System.Drawing.RectangleF(0, 0, tableW, 26) };
            var trh = new XRTableRow();
            tblHeader.Rows.Add(trh);
            trh.Cells.Add(MakeTh("Rubrique", 0.40f, DevExpress.XtraPrinting.TextAlignment.MiddleLeft));
            trh.Cells.Add(MakeTh("Base", 0.18f));
            trh.Cells.Add(MakeTh("Taux", 0.12f));
            trh.Cells.Add(MakeTh("Gain", 0.15f));
            trh.Cells.Add(MakeTh("Retenue", 0.15f));
            drHeader.Controls.Add(tblHeader);

            // ---------- Tableau (detail) ----------
            var tblDetail = new XRTable { BoundsF = new System.Drawing.RectangleF(0, 0, tableW, 22), OddStyleName = "OddRow" };
            var tr = new XRTableRow();
            tblDetail.Rows.Add(tr);

            // Contexte = BulletinLigne (via DataMember="Lignes")
            tr.Cells.Add(MakeTd("[Rubrique.Libelle]", 0.40f, DevExpress.XtraPrinting.TextAlignment.MiddleLeft));
            tr.Cells.Add(MakeTd("Iif([Base]==0, '', FormatString('{0:n2}', [Base]))", 0.18f));
            tr.Cells.Add(MakeTd("Iif(IsNull([Taux]) or [Taux]==0, '', FormatString('{0:n2} %', [Taux]*100))", 0.12f));
            tr.Cells.Add(MakeTd("Iif([Rubrique.TypeCalcul] == 0, FormatString('{0:n2}', [Montant]), '')", 0.15f));
            tr.Cells.Add(MakeTd("Iif([Rubrique.TypeCalcul] == 1, FormatString('{0:n2}', [Montant]), '')", 0.15f));
            drDetail.Controls.Add(tblDetail);

            // Styles
            rpt.StyleSheet.Add(new XRControlStyle { Name = "OddRow", BackColor = System.Drawing.Color.FromArgb(250, 250, 252) });

            // ---------- Totaux + Signature (footer du DetailReport) ----------
            var tblTotals = new XRTable { BoundsF = new System.Drawing.RectangleF(0, 0, tableW, 80) };
            tblTotals.Rows.AddRange(new[] {
        new XRTableRow {
            Cells = {
                new XRTableCell { Text="Total gains", Font = new DXFont("Segoe UI", 9),
                    TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft,
                    Padding = new DevExpress.XtraPrinting.PaddingInfo(6,6,4,4), Borders = DevExpress.XtraPrinting.BorderSide.Top },
                new XRTableCell(), new XRTableCell(),
                new XRTableCell {
                    ExpressionBindings = { new ExpressionBinding("BeforePrint","Text","FormatString('{0:n2}', sumSum(Iif([Rubrique.TypeCalcul]==0, [Montant], 0)))") },
                    TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight },
                new XRTableCell()
            }
        },
        new XRTableRow {
            Cells = {
                new XRTableCell { Text="Total retenues", Font = new DXFont("Segoe UI", 9),
                    TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft,
                    Padding = new DevExpress.XtraPrinting.PaddingInfo(6,6,4,4), Borders = DevExpress.XtraPrinting.BorderSide.Top },
                new XRTableCell(), new XRTableCell(),
                new XRTableCell(),
                new XRTableCell {
                    ExpressionBindings = { new ExpressionBinding("BeforePrint","Text","FormatString('{0:n2}', sumSum(Iif([Rubrique.TypeCalcul]==1, [Montant], 0)))") },
                    TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight }
            }
        },
        new XRTableRow {
            Cells = {
                new XRTableCell { Text="Net à payer", Font = new DXFont("Segoe UI", 10, DXFontStyle.Bold),
                    TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft,
                    Padding = new DevExpress.XtraPrinting.PaddingInfo(6,6,4,4), Borders = DevExpress.XtraPrinting.BorderSide.Top },
                new XRTableCell(), new XRTableCell(),
                new XRTableCell { Font = new DXFont("Segoe UI", 10, DXFontStyle.Bold),
                    ExpressionBindings = { new ExpressionBinding("BeforePrint","Text","FormatString('{0:n2}', sumSum(Iif([Rubrique.TypeCalcul]==0, [Montant], 0)) - sumSum(Iif([Rubrique.TypeCalcul]==1, [Montant], 0)))") },
                    TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight },
                new XRTableCell()
            }
        }
    });
            drFooter.Controls.Add(tblTotals);

            // --- Bloc Signature (images + nom/titre) depuis ParametresPaie ---
            var signPanel = new XRPanel
            {
                BoundsF = new System.Drawing.RectangleF(0, 84, tableW, 100),
                Borders = DevExpress.XtraPrinting.BorderSide.None
            };

            var lblSign = new XRLabel
            {
                Text = "Signature & cachet",
                Font = new DXFont("Segoe UI", 9),
                BoundsF = new System.Drawing.RectangleF(0, 0, 220, 18)
            };
            signPanel.Controls.Add(lblSign);

            var picSign = new XRPictureBox
            {
                BoundsF = new System.Drawing.RectangleF(tableW - 240, 0, 220, 80),
                Sizing = DevExpress.XtraPrinting.ImageSizeMode.Squeeze,
                Borders = DevExpress.XtraPrinting.BorderSide.None
            };
            picSign.DataBindings.Add("ImageSource", dsParam, "SignatureImage");
            signPanel.Controls.Add(picSign);

            var lblName = new XRLabel
            {
                BoundsF = new System.Drawing.RectangleF(tableW - 240, 82, 220, 16),
                Font = new DXFont("Segoe UI", 9, DXFontStyle.Bold),
                TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight
            };
            lblName.DataBindings.Add("Text", dsParam, "SignatoryName");
            signPanel.Controls.Add(lblName);

            var lblTitle = new XRLabel
            {
                BoundsF = new System.Drawing.RectangleF(tableW - 240, 100, 220, 16),
                Font = new DXFont("Segoe UI", 8),
                ForeColor = System.Drawing.Color.DimGray,
                TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight
            };
            lblTitle.DataBindings.Add("Text", dsParam, "SignatoryTitle");
            signPanel.Controls.Add(lblTitle);

            drFooter.Controls.Add(signPanel);

            return rpt;
        }




        // ================= Helpers (DX 25.1) =================
        private static void AddKV(XRPanel parent, float x, float y, string k, string vExpression)
        {
            var left = new XRLabel
            {
                Text = k,
                BoundsF = new System.Drawing.RectangleF(x, y, 120, 16),
                Font = new DXFont("Segoe UI", 8),
                ForeColor = System.Drawing.Color.DimGray
            };
            var right = new XRLabel
            {
                BoundsF = new System.Drawing.RectangleF(x + 122, y, 220, 16),
                Font = new DXFont("Segoe UI", 9, DXFontStyle.Bold)
            };
            right.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", vExpression));
            parent.Controls.AddRange(new XRControl[] { left, right });
        }

        private static XRTableCell MakeTh(string text, float w, DevExpress.XtraPrinting.TextAlignment align = DevExpress.XtraPrinting.TextAlignment.MiddleRight)
        {
            return new XRTableCell
            {
                Text = text,
                WidthF = w * 700, // largeur relative total ~700
                BackColor = System.Drawing.Color.FromArgb(249, 250, 251),
                Font = new DXFont("Segoe UI", 9, DXFontStyle.Bold),
                Padding = new DevExpress.XtraPrinting.PaddingInfo(6, 6, 4, 4),
                TextAlignment = align,
                Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom
            };
        }

        private static XRTableCell MakeTd(string expression, float w, DevExpress.XtraPrinting.TextAlignment align = DevExpress.XtraPrinting.TextAlignment.MiddleRight)
        {
            var c = new XRTableCell
            {
                WidthF = w * 700,
                Font = new DXFont("Segoe UI", 9),
                Padding = new DevExpress.XtraPrinting.PaddingInfo(6, 6, 2, 2),
                TextAlignment = align
            };
            c.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expression));
            return c;
        }

        private static XRTableRow MakeTotalRow(string label, string sumExpression)
        {
            var r = new XRTableRow();
            var left = new XRTableCell
            {
                Text = label,
                Font = new DXFont("Segoe UI", 9),
                TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft,
                Padding = new DevExpress.XtraPrinting.PaddingInfo(6, 6, 4, 4),
                Borders = DevExpress.XtraPrinting.BorderSide.Top
            };
            var empty1 = new XRTableCell(); // Base
            var empty2 = new XRTableCell(); // Taux
            var gain = new XRTableCell { TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight };
            gain.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"FormatString('{{0:n2}}', {sumExpression})"));
            var retenue = new XRTableCell();
            // 👉 Si colonne Part employeur active, ajoute une cell ici :
            // var patro  = new XRTableCell();
            // r.Cells.AddRange(new[] { left, empty1, empty2, gain, retenue, patro });
            r.Cells.AddRange(new[] { left, empty1, empty2, gain, retenue });
            return r;
        }

        private static XRTableRow MakeTotalRowBold(string label, string expression)
        {
            var r = MakeTotalRow(label, expression);
            foreach (XRTableCell c in r.Cells)
                c.Font = new DXFont("Segoe UI", 10, DXFontStyle.Bold);
            return r;
        }
    }


}
