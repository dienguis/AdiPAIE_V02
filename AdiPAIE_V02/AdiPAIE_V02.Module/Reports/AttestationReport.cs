using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Domain;
using DevExpress.Drawing;
using DevExpress.Xpo;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Reports
{
    /// <summary>
    /// Rapport d'attestation généré programmatiquement depuis une DemandeAttestation.
    /// Supporte toutes les natures : Travail, Salaire, Congé, Emploi, Prise en charge.
    ///
    /// Usage :
    ///   var rpt = new AttestationReport(demande);
    ///   rpt.ExportToPdf(stream);
    /// </summary>
    public class AttestationReport : XtraReport
    {
        // ── Constantes de mise en page ──────────────────────────
        private const float PAGE_W = 793f;  // A4 largeur (points)
        private const float MARGIN_H = 50f;
        private const float CONTENT_W = PAGE_W - MARGIN_H * 2;

        // ── Police ──────────────────────────────────────────────
        private static Font F(float size, bool bold = false)
            => new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);

        public AttestationReport(DemandeAttestation demande)
        {
            if (demande == null) throw new ArgumentNullException(nameof(demande));

            PaperKind = DevExpress.Drawing.Printing.DXPaperKind.A4;
            Margins = new System.Drawing.Printing.Margins(50, 50, 80, 60);

            // ── Récupération des données ─────────────────────────
            ParametresPaie prm = null;
            Company company = null;

            try
            {
#pragma warning disable XAF0018 // UnitOfWork() implicite : utilise XpoDefault.DataLayer (acceptable pour rapport global, pas de tenant)
                using (var uow = new UnitOfWork())
                {
                    prm = new XPQuery<ParametresPaie>(uow).FirstOrDefault();
                    company = new XPQuery<Company>(uow).FirstOrDefault();
                }
#pragma warning restore XAF0018
            }
            catch { /* données non critiques */ }

            var sal = demande.Salarie;

            // ── Bandes ──────────────────────────────────────────
            var reportHeader = new ReportHeaderBand { HeightF = 160 };
            var detail = new DetailBand { HeightF = 480 };
            var pageFooter = new PageFooterBand { HeightF = 100 };

            Bands.AddRange(new Band[] { reportHeader, detail, pageFooter });

            // ════════════════════════════════════════════════════
            // HEADER : logo + société + titre du document
            // ════════════════════════════════════════════════════

            // Logo (haut gauche)
            var logo = new XRPictureBox
            {
                Sizing = ImageSizeMode.Squeeze,
                BoundsF = new RectangleF(0, 0, 120, 60)
            };
            var logoBytes = company?.LogoImage ?? prm?.LogoImage;
            if (logoBytes != null)
                logo.ImageSource = ToImageSource(logoBytes);
            reportHeader.Controls.Add(logo);

            // Raison sociale
            reportHeader.Controls.Add(new XRLabel
            {
                Text = company?.RaisonSociale ?? string.Empty,
                Font = F(13, true),
                BoundsF = new RectangleF(130, 4, CONTENT_W - 130, 22),
                TextAlignment = TextAlignment.MiddleLeft
            });

            // Adresse
            var adresse = $"{company?.Address ?? ""}{(string.IsNullOrWhiteSpace(company?.Ville) ? "" : " — " + company?.Ville)}";
            reportHeader.Controls.Add(new XRLabel
            {
                Text = adresse,
                Font = F(8),
                BoundsF = new RectangleF(130, 26, CONTENT_W - 130, 16),
                TextAlignment = TextAlignment.MiddleLeft,
                ForeColor = Color.Gray
            });

            // NINEA / RC
            reportHeader.Controls.Add(new XRLabel
            {
                Text = $"NINEA : {company?.NINEA ?? "—"}    RC : {company?.RC ?? "—"}",
                Font = F(8),
                BoundsF = new RectangleF(130, 42, CONTENT_W - 130, 16),
                TextAlignment = TextAlignment.MiddleLeft,
                ForeColor = Color.Gray
            });

            // Ligne séparatrice
            var sep1 = new XRLine
            {
                BoundsF = new RectangleF(0, 66, CONTENT_W, 2),
               ForeColor= Color.FromArgb(46, 117, 182),
                LineWidth = 2
            };
            reportHeader.Controls.Add(sep1);

            // Ville + date (haut droite)
            reportHeader.Controls.Add(new XRLabel
            {
                Text = $"Dakar, le {DateTime.Today:dd MMMM yyyy}",
                Font = F(9),
                BoundsF = new RectangleF(CONTENT_W - 220, 76, 220, 18),
                TextAlignment = TextAlignment.MiddleRight,
                ForeColor = Color.DimGray
            });

            // Titre du document (centré, en majuscules visuels)
            var titreDoc = GetTitreDocument(demande.Nature ?? AttestationNature.Travail);
            reportHeader.Controls.Add(new XRLabel
            {
                Text = titreDoc,
                Font = F(14, true),
                BoundsF = new RectangleF(0, 100, CONTENT_W, 28),
                TextAlignment = TextAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(31, 78, 121)
            });

            // Ligne décorative sous le titre
            reportHeader.Controls.Add(new XRLine
            {
                BoundsF = new RectangleF(CONTENT_W / 4, 132, CONTENT_W / 2, 1),
               ForeColor = Color.FromArgb(46, 117, 182),
                LineWidth = 1
            });

            // ════════════════════════════════════════════════════
            // DETAIL : corps de l'attestation
            // ════════════════════════════════════════════════════

            float y = 10;

            // Formule d'introduction
            var intro = BuildIntroText(prm, company);
            detail.Controls.Add(MakeLabel(intro, F(10), 0, y, CONTENT_W, 40, TextAlignment.TopLeft));
            y += 48;

            // Bloc salarié (encadré)
            detail.Controls.Add(MakePanel(0, y, CONTENT_W, 80));

            detail.Controls.Add(MakeLabel("Nom et prénom :", F(9, true), 10, y + 6, 160, 16));
            detail.Controls.Add(MakeLabel(sal?.FullName ?? "—", F(10), 175, y + 6, CONTENT_W - 185, 16));

            detail.Controls.Add(MakeLabel("Matricule :", F(9, true), 10, y + 26, 160, 16));
            detail.Controls.Add(MakeLabel(sal?.Matricule ?? "—", F(10), 175, y + 26, 200, 16));

            detail.Controls.Add(MakeLabel("Date d'embauche :", F(9, true), 10, y + 46, 160, 16));
            detail.Controls.Add(MakeLabel(
                sal?.DateEmbauche != default ? sal.DateEmbauche.ToString("dd/MM/yyyy") : "—",
                F(10), 175, y + 46, 200, 16));

            detail.Controls.Add(MakeLabel("Catégorie :", F(9, true), 350, y + 26, 120, 16));
            detail.Controls.Add(MakeLabel(sal?.Categories?.Intitule ?? "—", F(10), 475, y + 26, CONTENT_W - 485, 16));

            detail.Controls.Add(MakeLabel("Ancienneté :", F(9, true), 350, y + 46, 120, 16));
            var anciennete = sal != null
                ? $"{AncienneteHelper.NombreAnnee(sal.DateEmbauche, DateTime.Today)} an(s)"
                : "—";
            detail.Controls.Add(MakeLabel(anciennete, F(10), 475, y + 46, CONTENT_W - 485, 16));

            y += 96;

            // Corps du texte selon la nature
            var corps = BuildCorpsAttestation(demande, sal, company);
            detail.Controls.Add(MakeLabel(corps, F(10), 0, y, CONTENT_W, 200,
                TextAlignment.TopLeft, wordWrap: true));
            y += 210;

            // Mention salaire (uniquement pour Nature = Salaire)
            if (demande.Nature == AttestationNature.Conge && sal != null)
            {
                detail.Controls.Add(MakeLabel(
                    $"Salaire de base mensuel brut : {sal.SalaireBase:N0} FCFA",
                    F(10, true), 0, y, CONTENT_W, 20));
                y += 28;
            }

            // Objet de la demande (si motif renseigné)
            if (!string.IsNullOrWhiteSpace(demande.Motif))
            {
                detail.Controls.Add(MakeLabel(
                    $"Objet de la demande : {demande.Motif}",
                    F(9), 0, y, CONTENT_W, 20, TextAlignment.MiddleLeft, color: Color.DimGray));
                y += 26;
            }

            // Formule de clôture
            detail.Controls.Add(MakeLabel(
                "En foi de quoi, la présente attestation est délivrée à l'intéressé(e) pour servir et valoir ce que de droit.",
                F(10), 0, y, CONTENT_W, 36, TextAlignment.TopLeft, wordWrap: true));
            y += 50;

            // Zone signature (droite)
            var sigX = CONTENT_W - 220;

            detail.Controls.Add(MakeLabel("Le Directeur des Ressources Humaines",
                F(9, true), sigX, y, 220, 16, TextAlignment.MiddleCenter));
            y += 24;

            // Image signature
            var signBytes = prm?.SignatureImage;
            if (signBytes != null)
            {
                var sigPic = new XRPictureBox
                {
                    Sizing = ImageSizeMode.Squeeze,
                    BoundsF = new RectangleF(sigX + 40, y, 140, 50)
                };
                sigPic.ImageSource = ToImageSource(signBytes);
                detail.Controls.Add(sigPic);
            }
            y += 58;

            // Cachet
            var cachetBytes = prm?.CachetImage;
            if (cachetBytes != null)
            {
                var cachet = new XRPictureBox
                {
                    Sizing = ImageSizeMode.Squeeze,
                    BoundsF = new RectangleF(sigX + 60, y, 100, 40)
                };
                cachet.ImageSource = ToImageSource(cachetBytes);
                detail.Controls.Add(cachet);
            }
            y += 48;

            detail.Controls.Add(MakeLabel(
                prm?.SignatoryName ?? string.Empty,
                F(9, true), sigX, y, 220, 16, TextAlignment.MiddleCenter));
            y += 18;

            detail.Controls.Add(MakeLabel(
                prm?.SignatoryTitle ?? string.Empty,
                F(8), sigX, y, 220, 16, TextAlignment.MiddleCenter,
                color: Color.DimGray));

            // ════════════════════════════════════════════════════
            // FOOTER : numéro de page + référence
            // ════════════════════════════════════════════════════

            pageFooter.Controls.Add(new XRLine
            {
                BoundsF = new RectangleF(0, 0, CONTENT_W, 1),
                ForeColor= Color.LightGray,
                LineWidth = 1
            });

            pageFooter.Controls.Add(new XRLabel
            {
                Text = $"Réf. demande : {demande.Oid}  —  Générée le {DateTime.Now:dd/MM/yyyy HH:mm}",
                Font = F(7),
                BoundsF = new RectangleF(0, 6, CONTENT_W - 80, 16),
                TextAlignment = TextAlignment.MiddleLeft,
                ForeColor = Color.Gray
            });

            var pageInfo = new XRPageInfo
            {
                PageInfo = PageInfo.NumberOfTotal,
                Font = F(7),
                BoundsF = new RectangleF(CONTENT_W - 80, 6, 80, 16),
                TextAlignment = TextAlignment.MiddleRight,
                ForeColor = Color.Gray
            };
            pageFooter.Controls.Add(pageInfo);
        }

        // ── Textes métier ────────────────────────────────────────

        private static string GetTitreDocument(AttestationNature nature) => nature switch
        {
            AttestationNature.Travail => "ATTESTATION DE TRAVAIL",
            AttestationNature.CessationPaiement => "ATTESTATION DE CESSATION DE PAIEMENT",
            AttestationNature.Conge => "ATTESTATION DE CONGÉ PAYÉ",
            AttestationNature.Emploi => "CERTIFICAT D'EMPLOI",
         //   AttestationNature.Prise_En_Charge => "ATTESTATION DE PRISE EN CHARGE",
            _ => "ATTESTATION"
        };

        private static string BuildIntroText(ParametresPaie prm, Company company)
        {
            var signataire = prm?.SignatoryName ?? "Le soussigné";
            var titre = prm?.SignatoryTitle ?? "Directeur des Ressources Humaines";
            var soc = company?.RaisonSociale ?? "la société";
            return $"Je soussigné(e), {signataire}, {titre} de {soc}, certifie que :";
        }

        private static string BuildCorpsAttestation(
            DemandeAttestation demande, Salarie sal, Company company)
        {
            if (sal == null) return string.Empty;

            var nom = sal.FullName ?? "—";
            var matricule = sal.Matricule ?? "—";
            var categorie = sal.Categories?.Intitule ?? "—";
            var embauche = sal.DateEmbauche != default
                ? sal.DateEmbauche.ToString("dd MMMM yyyy")
                : "—";
            var soc = company?.RaisonSociale ?? "la société";

            return (demande.Nature ?? AttestationNature.Travail) switch
            {
                AttestationNature.Travail =>
                    $"Monsieur / Madame {nom}, matricule {matricule}, est bien employé(e) " +
                    $"au sein de {soc} en qualité de {categorie}, " +
                    $"depuis le {embauche}.\r\n\r\n" +
                    "Cette attestation est délivrée à la demande de l'intéressé(e) et pour servir et valoir ce que de droit.",

                AttestationNature.CessationPaiement =>
                    $"Monsieur / Madame {nom}, matricule {matricule}, est employé(e) " +
                    $"au sein de {soc} depuis le {embauche} " +
                    $"en qualité de {categorie}.\r\n\r\n" +
                    "Sa rémunération mensuelle est indiquée ci-dessous.",

                AttestationNature.Conge =>
                    $"Monsieur / Madame {nom}, matricule {matricule}, employé(e) " +
                    $"au sein de {soc} depuis le {embauche}, " +
                    $"bénéficie de congés payés conformément aux dispositions légales en vigueur " +
                    $"et à la convention collective applicable.",

                AttestationNature.Emploi =>
                    $"Monsieur / Madame {nom}, matricule {matricule}, a été employé(e) " +
                    $"au sein de {soc} du {embauche} " +
                    $"en qualité de {categorie}.\r\n\r\n" +
                    "Cette personne a quitté nos effectifs à la date indiquée ci-dessus " +
                    "et a satisfait à toutes ses obligations envers la société.",

                AttestationNature.DomiciliationSalaire =>
                    $"Monsieur / Madame {nom}, matricule {matricule}, employé(e) " +
                    $"au sein de {soc} depuis le {embauche}, " +
                    $"fait l'objet d'une prise en charge par la société " +
                    "dans le cadre de ses avantages professionnels.",

                _ =>
                    $"Monsieur / Madame {nom} est bien employé(e) au sein de {soc}."
            };
        }

        // ── Helpers visuels ──────────────────────────────────────

        private static XRLabel MakeLabel(
            string text, Font font,
            float x, float y, float w, float h,
            TextAlignment align = TextAlignment.MiddleLeft,
            bool wordWrap = false,
            Color? color = null)
        {
            var lbl = new XRLabel
            {
                Text = text,
                Font = font,
                BoundsF = new RectangleF(x, y, w, h),
                TextAlignment = align,
                WordWrap = wordWrap,
                ForeColor = color ?? Color.FromArgb(64, 64, 64)
            };
            return lbl;
        }

        /// <summary>Encadré gris clair pour le bloc salarié</summary>
        private static XRPanel MakePanel(float x, float y, float w, float h)
        {
            return new XRPanel
            {
                BoundsF = new RectangleF(x, y, w, h),
                BackColor = Color.FromArgb(235, 243, 251),
                BorderColor = Color.FromArgb(46, 117, 182),
                Borders = BorderSide.All,
                BorderWidth = 0.5f,
                BorderDashStyle = BorderDashStyle.Solid
            };
        }

        private static ImageSource ToImageSource(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                using (var ms = new MemoryStream(bytes))
                using (var dx = DXImage.FromStream(ms))
                    return new ImageSource(dx);
            }
            catch { return null; }
        }
    }
}
