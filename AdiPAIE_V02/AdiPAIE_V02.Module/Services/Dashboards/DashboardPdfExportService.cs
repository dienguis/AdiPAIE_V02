// =============================================================================
//  DashboardPdfExportService.cs
//  Implémentation QuestPDF — export .pdf pour les 6 tableaux de bord RH.
//
//  Convention :
//    - Page A4 paysage avec en-tête navy ELTON (logo + titre + sous-titre)
//    - Section KPI en grille de cartes
//    - Sections data : tableau avec en-têtes navy + lignes alternées
//    - Pied de page : numéro de page + horodatage + mention "ELTON Oil"
//
//  Note licence QuestPDF :
//    Settings.License = LicenseType.Community  → gratuit dev / CA < 1 M$
//    Pour ELTON en prod, basculer sur LicenseType.Professional après achat.
// =============================================================================

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using AdiPAIE_V02.Module.Models.Dashboards;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <inheritdoc cref="IDashboardPdfExportService"/>
    public sealed class DashboardPdfExportService : IDashboardPdfExportService
    {
        private static readonly string ColorNavy   = "#142E4D";
        private static readonly string ColorOrange = "#F18A1C";
        private static readonly string ColorGray   = "#5C6679";
        private static readonly string ColorBorder = "#E5E7EB";

        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
        private static readonly NumberFormatInfo FcfaFmt = new()
        {
            NumberGroupSeparator   = " ",
            NumberDecimalSeparator = ",",
            NumberDecimalDigits    = 0
        };

        static DashboardPdfExportService()
        {
            // Licence Community (dev/petit CA). Voir commentaire haut de fichier.
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportEffectifDetaille(
            EffectifDetailleDto dto, EffectifDetailleFilterModel filter)
        {
            var anneeLabel = (filter.Annees != null && filter.Annees.Count > 0)
                ? string.Join("-", filter.Annees) : DateTime.Today.Year.ToString();

            var pdf = BuildDocument("Tableau N°1 — Effectif détaillé", $"Année {anneeLabel}",
                page =>
                {
                    page.Item().Element(c => Kpi(c, "Effectif total", dto.EffectifTotal.ToString("N0", Fr)));
                    page.Item().Element(c => SectionTitle(c, "Indicateurs démographiques"));
                    page.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                        Th(t, "Indicateur", "Global", "Hommes", "Femmes");
                        Td(t, "Âge moyen",       Ans(dto.Kpis.AgeMoyenGlobal), Ans(dto.Kpis.AgeMoyenHommes), Ans(dto.Kpis.AgeMoyenFemmes));
                        Td(t, "Ancienneté moyenne", Ans(dto.Kpis.AncienneteMoyenneGlobal), Ans(dto.Kpis.AncienneteMoyenneHommes), Ans(dto.Kpis.AncienneteMoyenneFemmes));
                    });

                    if (dto.BarStackHommesFemmes.Count > 0)
                    {
                        page.Item().Element(c => SectionTitle(c, "Répartition Hommes / Femmes"));
                        page.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                            Th(t, "Tranche d'âge", "Catégorie", "Hommes", "Femmes", "Total", "% Femmes");
                            foreach (var b in dto.BarStackHommesFemmes)
                                Td(t, AgeBucketHelper.GetLibelle(b.Tranche), b.Categorie, b.Hommes.ToString("N0", Fr), b.Femmes.ToString("N0", Fr), b.Total.ToString("N0", Fr), Pct(b.PourcentageFemmes));
                        });
                    }
                });
            return (pdf, $"Effectif_Detaille_{anneeLabel}.pdf");
        }

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportAnalyseEffectif(
            AnalyseEffectifDto dto, AnalyseEffectifFilterModel filter)
        {
            var pdf = BuildDocument(
                "Tableau N°2 — Analyse de l'Effectif",
                $"{filter.Personnel} · Mode {filter.Mode} · Année {filter.Annee}",
                page =>
                {
                    page.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => Kpi(c, "Effectif", dto.Kpis.EffectifTotal.ToString("N0", Fr)));
                        r.RelativeItem().Element(c => Kpi(c, "% Départs", Pct(dto.Kpis.PourcentageDeparts)));
                        r.RelativeItem().Element(c => Kpi(c, "% Rotation", Pct(dto.Kpis.PourcentageRotation)));
                        r.RelativeItem().Element(c => Kpi(c, "Anc. Moy.", Ans(dto.Kpis.AncienneteMoyenne)));
                    });
                    page.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => Kpi(c, "% Femmes", Pct(dto.Kpis.PourcentageFemmes)));
                        r.RelativeItem().Element(c => Kpi(c, "% Hommes", Pct(dto.Kpis.PourcentageHommes)));
                        r.RelativeItem().Element(c => Kpi(c, "Âge Moyen", Ans(dto.Kpis.AgeMoyen)));
                        r.RelativeItem();
                    });

                    BarTable(page, "Tranche d'âge",  dto.ParTrancheAge);
                    BarTable(page, "Ancienneté",     dto.ParAnciennete);
                    BarTable(page, "Segment",        dto.ParSegment);
                    BarTable(page, "Catégorie Top 5", dto.ParCategorieTop5);
                    BarTable(page, "Type contrat",   dto.ParTypeContrat);
                });
            return (pdf, $"Analyse_Effectif_{filter.Personnel}_{filter.Annee}.pdf");
        }

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportMouvements(
            MouvementsDto dto, MouvementsFilterModel filter)
        {
            var pdf = BuildDocument(
                "Tableau N°3 — Mouvements (Arrivées / Départs)",
                $"{filter.Personnel} · Année {filter.Annee}",
                page =>
                {
                    page.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => Kpi(c, "Arrivées",     dto.Kpis.NbArrivees.ToString("N0", Fr)));
                        r.RelativeItem().Element(c => Kpi(c, "Départs",      dto.Kpis.NbDeparts.ToString("N0", Fr)));
                        r.RelativeItem().Element(c => Kpi(c, "Solde net",    dto.Kpis.Solde.ToString("+0;-0;0", Fr)));
                        r.RelativeItem().Element(c => Kpi(c, "% Arrivées",   Pct(dto.Kpis.TauxArrivees)));
                    });
                    page.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => Kpi(c, "% Départs",      Pct(dto.Kpis.TauxDeparts)));
                        r.RelativeItem().Element(c => Kpi(c, "Effectif Début", dto.Kpis.EffectifDebut.ToString("N0", Fr)));
                        r.RelativeItem().Element(c => Kpi(c, "Effectif Fin",   dto.Kpis.EffectifFin.ToString("N0", Fr)));
                        r.RelativeItem();
                    });

                    page.Item().Element(c => SectionTitle(c, "Arrivées par mois"));
                    BarTableInline(page, dto.ArriveesParMois);
                    BarTable(page, "Arrivées par site",      dto.ArriveesParSite);
                    BarTable(page, "Arrivées par catégorie", dto.ArriveesParCategorie);

                    page.Item().Element(c => SectionTitle(c, "Départs par mois"));
                    BarTableInline(page, dto.DepartsParMois);
                    BarTable(page, "Départs par motif",     dto.DepartsParMotif);
                    BarTable(page, "Départs par site",      dto.DepartsParSite);
                    BarTable(page, "Départs par catégorie", dto.DepartsParCategorie);
                });
            return (pdf, $"Mouvements_{filter.Personnel}_{filter.Annee}.pdf");
        }

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportRemuneration(
            RemunerationDto dto, RemunerationFilterModel filter)
        {
            var pdf = BuildDocument(
                "Tableau N°4 — Rémunération (Égalité des salaires)",
                $"{filter.Personnel} · {filter.Mode} · Année {filter.Annee}",
                page =>
                {
                    page.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => Kpi(c, "Total",            Fcfa(dto.Kpis.Total)));
                        r.RelativeItem().Element(c => Kpi(c, "Salaire Min",      Fcfa(dto.Kpis.SalaireMin)));
                        r.RelativeItem().Element(c => Kpi(c, "Salaire Max",      Fcfa(dto.Kpis.SalaireMax)));
                    });
                    page.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => Kpi(c, "Salaire Moyen",    Fcfa(dto.Kpis.SalaireMoyen)));
                        r.RelativeItem().Element(c => Kpi(c, "Coût Moy. Salarié", Fcfa(dto.Kpis.CoutMoyenSalarie)));
                        r.RelativeItem();
                    });

                    EgaliteTable(page, "Rémunération par segment",   dto.ParSegment);
                    EgaliteTable(page, "Rémunération par catégorie", dto.ParCategorie);

                    if (dto.EvolutionMensuelle.Any(m => m.Masse > 0))
                    {
                        page.Item().Element(c => SectionTitle(c, "Évolution mensuelle"));
                        page.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(2); cd.RelativeColumn(); });
                            Th(t, "Mois", "Masse", "Nb bulletins");
                            foreach (var m in dto.EvolutionMensuelle)
                                Td(t, m.Libelle, Fcfa(m.Masse), m.NbBulletins.ToString("N0", Fr));
                        });
                    }

                    if (dto.ParFamilleRubrique.Count > 0)
                    {
                        page.Item().Element(c => SectionTitle(c, "Décomposition par famille de rubrique"));
                        page.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                            Th(t, "Famille", "Total Salarial", "Total Employeur", "Total Combiné");
                            foreach (var d in dto.ParFamilleRubrique)
                                Td(t, d.FamilleMacro, Fcfa(d.TotalSalarial), Fcfa(d.TotalEmployeur), Fcfa(d.TotalCombined));
                        });
                    }
                });
            return (pdf, $"Remuneration_{filter.Personnel}_{filter.Mode}_{filter.Annee}.pdf");
        }

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportSuiviAbsences(
            SuiviAbsencesDto dto, SuiviAbsencesFilterModel filter)
        {
            string anneesStr = filter.Annees != null && filter.Annees.Count > 0
                ? string.Join(",", filter.Annees) : DateTime.Today.Year.ToString();

            var pdf = BuildDocument(
                "Tableau N°5 — Suivi des Absences",
                $"INTERNE · {anneesStr} · {(filter.InclureEnAttente ? "Avec En attente" : "Accordées seules")}",
                page =>
                {
                    // 6 KPIs Excel-style en 2 rangées de 3
                    page.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => Kpi(c, "Effectif Moyen",   dto.Kpis.EffectifMoyen.ToString("N0", Fr)));
                        r.RelativeItem().Element(c => Kpi(c, "Se sont Absentés", dto.Kpis.NbSalariesAbsents.ToString("N0", Fr)));
                        r.RelativeItem().Element(c => Kpi(c, "Total jours",      dto.Kpis.TotalJours.ToString("N1", Fr)));
                    });
                    page.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => Kpi(c, "Absentéisme",      Pct(dto.Kpis.TauxAbsenteismePct)));
                        r.RelativeItem().Element(c => Kpi(c, "JO Perdus",        dto.Kpis.JOPerdus.ToString("N1", Fr) + " j"));
                        r.RelativeItem().Element(c => Kpi(c, "Resp Tmp Travail", Pct(dto.Kpis.RespTempsTravailPct)));
                    });

                    // Tableau Par Motif (10 motifs)
                    if (dto.ParMotif.Count > 0)
                    {
                        page.Item().Element(c => SectionTitle(c, "Décomposition par motif"));
                        page.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.ConstantColumn(60); cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                            Th(t, "Code", "Libellé", "Catégorie", "Jours", "% Total");
                            foreach (var m in dto.ParMotif)
                                Td(t, m.Code, m.Libelle, m.IsAbsenteisme ? "Absentéisme" : "Programmée",
                                    m.NbJours.ToString("N1", Fr), Pct(m.PourcentageDuTotal));
                        });
                    }

                    // Top 10 absents (par TotalJours décroissant, depuis Employes)
                    var top10 = dto.Employes.Where(e => e.TotalJours > 0).Take(10).ToList();
                    if (top10.Count > 0)
                    {
                        page.Item().Element(c => SectionTitle(c, "Top 10 salariés les plus absents"));
                        page.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.ConstantColumn(20); cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                            Th(t, "#", "Salarié", "Site", "Département", "Catégorie", "J. Abs", "J. Prog");
                            int rank = 1;
                            foreach (var r in top10)
                                Td(t, (rank++).ToString(), r.NomComplet, r.Site, r.Departement, r.Categorie,
                                    r.JoursAbsenteisme.ToString("N1", Fr), r.JoursProgrammees.ToString("N1", Fr));
                        });
                    }
                });
            return (pdf, $"Suivi_Absences_{anneesStr.Replace(',', '_')}.pdf");
        }

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportBilanSocial(
            BilanSocialDto dto, BilanSocialFilterModel filter)
        {
            var pdf = BuildDocument(
                "Tableau N°6 — Bilan Social Mensuel",
                $"INTERNE · Synthèse DTSS · Année {filter.Annee}",
                page =>
                {
                    page.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => Kpi(c, "Effectif moyen",    dto.Kpis.EffectifMoyenAnnuel.ToString("N0", Fr)));
                        r.RelativeItem().Element(c => Kpi(c, "Embauches",         dto.Kpis.TotalEmbauches.ToString("N0", Fr)));
                        r.RelativeItem().Element(c => Kpi(c, "Départs",           dto.Kpis.TotalDeparts.ToString("N0", Fr)));
                    });
                    page.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => Kpi(c, "Coût Employeur",    Fcfa(dto.Kpis.CoutEmployeurAnnuel)));
                        r.RelativeItem().Element(c => Kpi(c, "% Absentéisme",     Pct(dto.Kpis.TauxAbsenteisme)));
                        r.RelativeItem().Element(c => Kpi(c, "Complétude data",   Pct(dto.Kpis.CompletudeData)));
                    });

                    page.Item().Element(c => SectionTitle(c, "Récapitulatif mensuel — 12 mois"));
                    page.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                        });
                        Th(t, "Mois", "Effectif", "Embauches", "Départs", "Masse Sal.", "Charges Pat.", "Coût Empl.", "Emp. Abs.", "J. Abs.");
                        foreach (var m in dto.Mois)
                        {
                            string ToStr(int i) => i > 0 ? i.ToString("N0", Fr) : "—";
                            string ToStrM(decimal d) => d > 0 ? Fcfa(d) : "—";

                            if (m.EstTotal)
                                TdTotal(t, m.Libelle, m.EffectifFinMois.ToString("N0", Fr),
                                        m.Embauches.ToString("N0", Fr), m.Departs.ToString("N0", Fr),
                                        Fcfa(m.MasseSalariale), Fcfa(m.ChargesPatronales), Fcfa(m.CoutEmployeur),
                                        m.EmployesAbsents.ToString("N0", Fr), m.JoursAbsence.ToString("N0", Fr));
                            else
                                Td(t, m.Libelle, m.EffectifFinMois.ToString("N0", Fr),
                                   ToStr(m.Embauches), ToStr(m.Departs),
                                   ToStrM(m.MasseSalariale), ToStrM(m.ChargesPatronales), ToStrM(m.CoutEmployeur),
                                   ToStr(m.EmployesAbsents), ToStr(m.JoursAbsence));
                        }
                    });

                    if (dto.DontInterimaires != null && dto.DontInterimaires.NbContrats > 0)
                    {
                        page.Item().Element(c => SectionTitle(c, "Complément — dont Intérimaires (hors DTSS officiel)"));
                        page.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(2); });
                            Th(t, "Indicateur", "Eff. moyen", "Nb contrats", "Arrivées", "Départs", "Coût total");
                            Td(t, $"Intérimaires {filter.Annee}",
                                dto.DontInterimaires.EffectifMoyen.ToString("N0", Fr),
                                dto.DontInterimaires.NbContrats.ToString("N0", Fr),
                                dto.DontInterimaires.ArriveesContrats.ToString("N0", Fr),
                                dto.DontInterimaires.DepartsContrats.ToString("N0", Fr),
                                Fcfa(dto.DontInterimaires.CoutTotal));
                        });
                    }
                });
            return (pdf, $"Bilan_Social_{filter.Annee}.pdf");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Helpers QuestPDF (factorisation du layout)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Squelette commun : page A4 paysage, en-tête navy, pied de page.</summary>
        private static byte[] BuildDocument(string title, string subtitle, Action<ColumnDescriptor> body)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(t => t.FontFamily("Segoe UI").FontSize(9).FontColor(ColorNavy));

                    // ── HEADER ─────────────────────────────────────────
                    page.Header().Background(ColorNavy).Padding(12).Row(r =>
                    {
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().Text("ELTON Oil Company").FontSize(9).FontColor(ColorOrange).Bold();
                            col.Item().Text(title).FontSize(15).FontColor(Colors.White).Bold();
                            col.Item().Text(subtitle).FontSize(9).FontColor("#A8B5C9");
                        });
                        r.ConstantItem(150).AlignRight().Column(col =>
                        {
                            col.Item().AlignRight().Text("Tableaux de Bord RH").FontSize(8).FontColor(ColorOrange);
                            col.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm", Fr)).FontSize(8).FontColor("#A8B5C9");
                        });
                    });

                    // ── CONTENT ────────────────────────────────────────
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(10);
                        body(col);
                    });

                    // ── FOOTER ─────────────────────────────────────────
                    page.Footer().BorderTop(1).BorderColor(ColorBorder).PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Text(t =>
                        {
                            t.DefaultTextStyle(s => s.FontSize(8).FontColor(ColorGray));
                            t.Span("ELTON Oil · Tableaux de Bord RH · Généré le ");
                            t.Span(DateTime.Now.ToString("dd/MM/yyyy à HH:mm", Fr));
                        });
                        r.ConstantItem(80).AlignRight().Text(t =>
                        {
                            t.DefaultTextStyle(s => s.FontSize(8).FontColor(ColorGray));
                            t.Span("Page ");
                            t.CurrentPageNumber();
                            t.Span(" / ");
                            t.TotalPages();
                        });
                    });
                });
            });

            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);
            return ms.ToArray();
        }

        private static void Kpi(IContainer c, string label, string value)
        {
            c.Padding(4).Border(1).BorderColor(ColorBorder).Background(Colors.White).Padding(8).Column(col =>
            {
                col.Item().Text(label.ToUpper()).FontSize(7).FontColor(ColorGray).LetterSpacing(0.5f);
                col.Item().PaddingTop(2).Text(value).FontSize(13).FontColor(ColorNavy).Bold();
            });
        }

        private static void SectionTitle(IContainer c, string title)
        {
            c.PaddingTop(6).BorderLeft(3).BorderColor(ColorOrange).PaddingLeft(8)
                .Text(title).FontSize(11).FontColor(ColorNavy).Bold();
        }

        private static void Th(TableDescriptor t, params string[] cols)
        {
            t.Header(h =>
            {
                foreach (var col in cols)
                    h.Cell().Background(ColorNavy).Padding(5).Text(col).FontColor(Colors.White).Bold().FontSize(8);
            });
        }

        private static void Td(TableDescriptor t, params string[] cells)
        {
            foreach (var v in cells)
                t.Cell().BorderBottom(0.5f).BorderColor(ColorBorder).Padding(4).Text(v).FontSize(8);
        }

        private static void TdTotal(TableDescriptor t, params string[] cells)
        {
            foreach (var v in cells)
                t.Cell().Background(ColorOrange).Padding(5).Text(v).FontColor(Colors.White).Bold().FontSize(9);
        }

        private static void BarTable(ColumnDescriptor page, string title, System.Collections.Generic.List<BarItemDto> items)
        {
            if (items == null || items.Count == 0) return;
            page.Item().Element(c => SectionTitle(c, title));
            page.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(); cd.RelativeColumn(); });
                Th(t, "Libellé", "Valeur", "%");
                foreach (var b in items)
                    Td(t, b.Libelle, b.Valeur.ToString("N0", Fr), Pct(b.Pourcentage));
            });
        }

        private static void BarTableInline(ColumnDescriptor page, System.Collections.Generic.List<BarItemDto> items)
        {
            // Affichage compact 12 mois sur une ligne (pour mensuel)
            if (items == null || items.Count == 0) return;
            page.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    for (int i = 0; i < items.Count; i++) cd.RelativeColumn();
                });
                t.Header(h =>
                {
                    foreach (var b in items)
                        h.Cell().Background(ColorNavy).Padding(3).AlignCenter().Text(b.Libelle).FontColor(Colors.White).FontSize(7);
                });
                foreach (var b in items)
                    t.Cell().BorderBottom(0.5f).BorderColor(ColorBorder).Padding(3).AlignCenter().Text(b.Valeur.ToString("N0", Fr)).FontSize(8);
            });
        }

        private static void EgaliteTable(ColumnDescriptor page, string title, System.Collections.Generic.List<EgaliteSalaireRowDto> rows)
        {
            if (rows == null || rows.Count == 0) return;
            page.Item().Element(c => SectionTitle(c, title));
            page.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2); cd.RelativeColumn(2);
                    cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2);
                    cd.RelativeColumn(2); cd.RelativeColumn(2);
                });
                Th(t, "Libellé", "Total", "Coût Moy.", "Min", "Max", "Moyenne", "Moy. ♀", "Moy. ♂");
                foreach (var r in rows)
                    Td(t, r.Libelle, Fcfa(r.Total), Fcfa(r.CoutMoyen),
                        Fcfa(r.Min), Fcfa(r.Max), Fcfa(r.Moyenne),
                        r.MoyFemme.HasValue ? Fcfa(r.MoyFemme.Value) : "—",
                        r.MoyHomme.HasValue ? Fcfa(r.MoyHomme.Value) : "—");
            });
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Format helpers
        // ═════════════════════════════════════════════════════════════════════
        private static string Fcfa(decimal v) => Math.Round(v, 0).ToString("#,##0", FcfaFmt);
        private static string Pct(decimal v)  => v.ToString("0.0", Fr) + " %";
        private static string Ans(decimal v)  => Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("0", Fr) + " ans";
    }
}
