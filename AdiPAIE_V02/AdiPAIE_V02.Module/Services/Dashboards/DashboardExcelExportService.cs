// =============================================================================
//  DashboardExcelExportService.cs
//  Implémentation ClosedXML — export .xlsx pour les 6 tableaux de bord RH.
//
//  Convention :
//    - 1 worksheet par section logique (KPI, table principale, etc.)
//    - En-tête style navy ELTON, centré, gras blanc
//    - Format FCFA (séparateur d'espace) pour les montants
//    - Auto-fit colonnes en fin de génération
// =============================================================================

using System;
using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using AdiPAIE_V02.Module.Models.Dashboards;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <inheritdoc cref="IDashboardExcelExportService"/>
    public sealed class DashboardExcelExportService : IDashboardExcelExportService
    {
        private static readonly XLColor HeaderBg     = XLColor.FromHtml("#142E4D");  // Navy ELTON
        private static readonly XLColor TotalBg      = XLColor.FromHtml("#F18A1C");  // Orange ELTON
        private static readonly XLColor TotalFg      = XLColor.White;
        private static readonly CultureInfo Fr       = CultureInfo.GetCultureInfo("fr-FR");
        private const string FmtFcfa  = "# ##0";
        private const string FmtPct   = "0.0\"%\"";
        private const string FmtDate  = "dd/MM/yyyy";

        /// <summary>
        /// Format FCFA pour les conversions string ToString(format, culture)
        /// utilisé par les onglets Synthese (séparateur d'espace, 0 décimales).
        /// </summary>
        private static readonly NumberFormatInfo FcfaFmt = new()
        {
            NumberGroupSeparator   = " ",
            NumberDecimalSeparator = ",",
            NumberDecimalDigits    = 0
        };

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportEffectifDetaille(
            EffectifDetailleDto dto, EffectifDetailleFilterModel filter)
        {
            using var wb = new XLWorkbook();

            // ── Onglet 0 : SYNTHESE ──────────────────────────────────────
            BuildSyntheseGenerique(wb, "Tableau N°1 — Effectif détaillé",
                $"Effectif total : {dto.EffectifTotal:N0} au {dto.DateReference:dd/MM/yyyy}",
                ("Effectif total", dto.EffectifTotal.ToString("N0", Fr)),
                ("Date réf.", dto.DateReference.ToString("dd/MM/yyyy", Fr)),
                ("Âge moyen Global", $"{dto.Kpis.AgeMoyenGlobal:0} ans"),
                ("Anc. moyenne Global", $"{dto.Kpis.AncienneteMoyenneGlobal:0} ans"),
                ("Âge ♂ / ♀", $"{dto.Kpis.AgeMoyenHommes:0} / {dto.Kpis.AgeMoyenFemmes:0}"));

            // ── Feuille 1 : KPI ─────────────────────────────────────────
            var ws = wb.Worksheets.Add("KPI");
            WriteHeader(ws, "Indicateur", "Valeur");
            ws.Cell("A2").Value = "Effectif total";                ws.Cell("B2").Value = dto.EffectifTotal;
            ws.Cell("A3").Value = "Date de référence";             ws.Cell("B3").Value = dto.DateReference;       ws.Cell("B3").Style.NumberFormat.Format = FmtDate;
            ws.Cell("A4").Value = "Âge moyen Global";              ws.Cell("B4").Value = dto.Kpis.AgeMoyenGlobal;
            ws.Cell("A5").Value = "Âge moyen Hommes";              ws.Cell("B5").Value = dto.Kpis.AgeMoyenHommes;
            ws.Cell("A6").Value = "Âge moyen Femmes";              ws.Cell("B6").Value = dto.Kpis.AgeMoyenFemmes;
            ws.Cell("A7").Value = "Ancienneté moyenne Global";     ws.Cell("B7").Value = dto.Kpis.AncienneteMoyenneGlobal;
            ws.Cell("A8").Value = "Ancienneté moyenne Hommes";     ws.Cell("B8").Value = dto.Kpis.AncienneteMoyenneHommes;
            ws.Cell("A9").Value = "Ancienneté moyenne Femmes";     ws.Cell("B9").Value = dto.Kpis.AncienneteMoyenneFemmes;
            ws.Columns().AdjustToContents();

            // ── Feuille 2 : Bar stack F/H ───────────────────────────────
            ws = wb.Worksheets.Add("Repartition F-H");
            WriteHeader(ws, "Tranche d'âge", "Catégorie", "Hommes", "Femmes", "Total", "% Femmes");
            int row = 2;
            foreach (var b in dto.BarStackHommesFemmes)
            {
                ws.Cell(row, 1).Value = AgeBucketHelper.GetLibelle(b.Tranche);
                ws.Cell(row, 2).Value = b.Categorie;
                ws.Cell(row, 3).Value = b.Hommes;
                ws.Cell(row, 4).Value = b.Femmes;
                ws.Cell(row, 5).Value = b.Total;
                ws.Cell(row, 6).Value = b.PourcentageFemmes; ws.Cell(row, 6).Style.NumberFormat.Format = FmtPct;
                row++;
            }
            ws.Columns().AdjustToContents();

            // ── Feuille 3 : Évolution 3 ans ─────────────────────────────
            ws = wb.Worksheets.Add("Evolution");
            WriteHeader(ws, "Année", "Effectif", "Variation %");
            row = 2;
            foreach (var e in dto.EvolutionTroisAns)
            {
                ws.Cell(row, 1).Value = e.Annee;
                ws.Cell(row, 2).Value = e.Effectif;
                if (e.VariationPourcent.HasValue)
                {
                    ws.Cell(row, 3).Value = e.VariationPourcent.Value;
                    ws.Cell(row, 3).Style.NumberFormat.Format = FmtPct;
                }
                row++;
            }
            ws.Columns().AdjustToContents();

            var anneeLabel = (filter.Annees != null && filter.Annees.Count > 0)
                ? string.Join("-", filter.Annees)
                : DateTime.Today.Year.ToString();
            return Finalize(wb, $"Effectif_Detaille_{anneeLabel}.xlsx");
        }

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportAnalyseEffectif(
            AnalyseEffectifDto dto, AnalyseEffectifFilterModel filter)
        {
            using var wb = new XLWorkbook();

            BuildSyntheseGenerique(wb, "Tableau N°2 — Analyse de l'Effectif",
                $"{filter.Personnel} · Mode {filter.Mode} · {filter.Annee}",
                ("Effectif",        dto.Kpis.EffectifTotal.ToString("N0", Fr)),
                ("% Départs",       dto.Kpis.PourcentageDeparts.ToString("0.0", Fr) + " %"),
                ("% Rotation",      dto.Kpis.PourcentageRotation.ToString("0.0", Fr) + " %"),
                ("Anc. Moy.",       dto.Kpis.AncienneteMoyenne.ToString("0", Fr) + " ans"),
                ("% Femmes",        dto.Kpis.PourcentageFemmes.ToString("0.0", Fr) + " %"),
                ("% Hommes",        dto.Kpis.PourcentageHommes.ToString("0.0", Fr) + " %"),
                ("Âge Moyen",       dto.Kpis.AgeMoyen.ToString("0", Fr) + " ans"));

            var ws = wb.Worksheets.Add("KPI");
            WriteHeader(ws, "Indicateur", "Valeur");
            ws.Cell("A2").Value = "Effectif total";        ws.Cell("B2").Value = dto.Kpis.EffectifTotal;
            ws.Cell("A3").Value = "% Départs";             ws.Cell("B3").Value = dto.Kpis.PourcentageDeparts;   ws.Cell("B3").Style.NumberFormat.Format = FmtPct;
            ws.Cell("A4").Value = "% Rotation";            ws.Cell("B4").Value = dto.Kpis.PourcentageRotation;  ws.Cell("B4").Style.NumberFormat.Format = FmtPct;
            ws.Cell("A5").Value = "Ancienneté moyenne";    ws.Cell("B5").Value = dto.Kpis.AncienneteMoyenne;
            ws.Cell("A6").Value = "% Femmes";              ws.Cell("B6").Value = dto.Kpis.PourcentageFemmes;    ws.Cell("B6").Style.NumberFormat.Format = FmtPct;
            ws.Cell("A7").Value = "% Hommes";              ws.Cell("B7").Value = dto.Kpis.PourcentageHommes;    ws.Cell("B7").Style.NumberFormat.Format = FmtPct;
            ws.Cell("A8").Value = "Âge moyen";             ws.Cell("B8").Value = dto.Kpis.AgeMoyen;
            ws.Columns().AdjustToContents();

            WriteBarSheet(wb, "Tranche age",   dto.ParTrancheAge);
            WriteBarSheet(wb, "Anciennete",    dto.ParAnciennete);
            WriteBarSheet(wb, "Segment",       dto.ParSegment);
            WriteBarSheet(wb, "Categorie",     dto.ParCategorieTop5);
            WriteBarSheet(wb, "Type contrat",  dto.ParTypeContrat);

            ws = wb.Worksheets.Add("Evolution 8 ans");
            WriteHeader(ws, "Année", "Effectif", "Variation %");
            int row = 2;
            foreach (var e in dto.EvolutionHuitAns)
            {
                ws.Cell(row, 1).Value = e.Annee;
                ws.Cell(row, 2).Value = e.Effectif;
                if (e.VariationPourcent.HasValue)
                {
                    ws.Cell(row, 3).Value = e.VariationPourcent.Value;
                    ws.Cell(row, 3).Style.NumberFormat.Format = FmtPct;
                }
                row++;
            }
            ws.Columns().AdjustToContents();

            return Finalize(wb, $"Analyse_Effectif_{filter.Personnel}_{filter.Annee}.xlsx");
        }

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportMouvements(
            MouvementsDto dto, MouvementsFilterModel filter)
        {
            using var wb = new XLWorkbook();

            BuildSyntheseGenerique(wb, "Tableau N°3 — Mouvements (Arrivées / Départs)",
                $"{filter.Personnel} · {filter.Annee}",
                ("Arrivées",       dto.Kpis.NbArrivees.ToString("N0", Fr)),
                ("Départs",        dto.Kpis.NbDeparts.ToString("N0", Fr)),
                ("Solde net",      dto.Kpis.Solde.ToString("+0;-0;0", Fr)),
                ("% Arrivées",     dto.Kpis.TauxArrivees.ToString("0.0", Fr) + " %"),
                ("% Départs",      dto.Kpis.TauxDeparts.ToString("0.0", Fr) + " %"),
                ("Effectif Début", dto.Kpis.EffectifDebut.ToString("N0", Fr)),
                ("Effectif Fin",   dto.Kpis.EffectifFin.ToString("N0", Fr)));

            var ws = wb.Worksheets.Add("KPI");
            WriteHeader(ws, "Indicateur", "Valeur");
            ws.Cell("A2").Value = "Arrivées";         ws.Cell("B2").Value = dto.Kpis.NbArrivees;
            ws.Cell("A3").Value = "Départs";          ws.Cell("B3").Value = dto.Kpis.NbDeparts;
            ws.Cell("A4").Value = "Solde net";        ws.Cell("B4").Value = dto.Kpis.Solde;
            ws.Cell("A5").Value = "% Arrivées";       ws.Cell("B5").Value = dto.Kpis.TauxArrivees;   ws.Cell("B5").Style.NumberFormat.Format = FmtPct;
            ws.Cell("A6").Value = "% Départs";        ws.Cell("B6").Value = dto.Kpis.TauxDeparts;    ws.Cell("B6").Style.NumberFormat.Format = FmtPct;
            ws.Cell("A7").Value = "Effectif Début";   ws.Cell("B7").Value = dto.Kpis.EffectifDebut;
            ws.Cell("A8").Value = "Effectif Fin";     ws.Cell("B8").Value = dto.Kpis.EffectifFin;
            ws.Columns().AdjustToContents();

            WriteBarSheet(wb, "Arrivees mensuelles",  dto.ArriveesParMois);
            WriteBarSheet(wb, "Arrivees par site",    dto.ArriveesParSite);
            WriteBarSheet(wb, "Arrivees par cat",     dto.ArriveesParCategorie);
            WriteBarSheet(wb, "Departs mensuels",     dto.DepartsParMois);
            WriteBarSheet(wb, "Departs par motif",    dto.DepartsParMotif);
            WriteBarSheet(wb, "Departs par site",     dto.DepartsParSite);
            WriteBarSheet(wb, "Departs par cat",      dto.DepartsParCategorie);

            return Finalize(wb, $"Mouvements_{filter.Personnel}_{filter.Annee}.xlsx");
        }

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportRemuneration(
            RemunerationDto dto, RemunerationFilterModel filter)
        {
            using var wb = new XLWorkbook();

            // ── Onglet 1 : SYNTHESE (KPI + 2 tableaux clés Profession + Segment) ──
            //   Vue « tout en un » alignée sur la page web.
            BuildSyntheseRemuneration(wb, dto, filter);

            var ws = wb.Worksheets.Add("KPI");
            WriteHeader(ws, "Indicateur", "Valeur");
            ws.Cell("A2").Value = "Total";                  ws.Cell("B2").Value = dto.Kpis.Total;             ws.Cell("B2").Style.NumberFormat.Format = FmtFcfa;
            ws.Cell("A3").Value = "Salaire Min";            ws.Cell("B3").Value = dto.Kpis.SalaireMin;        ws.Cell("B3").Style.NumberFormat.Format = FmtFcfa;
            ws.Cell("A4").Value = "Salaire Max";            ws.Cell("B4").Value = dto.Kpis.SalaireMax;        ws.Cell("B4").Style.NumberFormat.Format = FmtFcfa;
            ws.Cell("A5").Value = "Salaire Moyen";          ws.Cell("B5").Value = dto.Kpis.SalaireMoyen;      ws.Cell("B5").Style.NumberFormat.Format = FmtFcfa;
            ws.Cell("A6").Value = "Coût Moy. Salarié";      ws.Cell("B6").Value = dto.Kpis.CoutMoyenSalarie;  ws.Cell("B6").Style.NumberFormat.Format = FmtFcfa;
            ws.Cell("A7").Value = "Nb salariés distincts";  ws.Cell("B7").Value = dto.Kpis.NbSalariesDistincts;
            ws.Cell("A8").Value = "Nb bulletins";           ws.Cell("B8").Value = dto.Kpis.NbBulletins;
            ws.Columns().AdjustToContents();

            WriteEgaliteSheet(wb, "Egalite par segment",   dto.ParSegment);
            WriteEgaliteSheet(wb, "Egalite par categorie", dto.ParCategorie);

            ws = wb.Worksheets.Add("Evolution mensuelle");
            WriteHeader(ws, "Mois", "Libelle", "Masse", "Nb bulletins");
            int row = 2;
            foreach (var m in dto.EvolutionMensuelle)
            {
                ws.Cell(row, 1).Value = m.Mois;
                ws.Cell(row, 2).Value = m.Libelle;
                ws.Cell(row, 3).Value = m.Masse;          ws.Cell(row, 3).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 4).Value = m.NbBulletins;
                row++;
            }
            ws.Columns().AdjustToContents();

            if (dto.ParFamilleRubrique.Count > 0)
            {
                ws = wb.Worksheets.Add("Decomposition rubriques");
                WriteHeader(ws, "Famille", "Total Salarial", "Total Employeur", "Total Combiné");
                row = 2;
                foreach (var d in dto.ParFamilleRubrique)
                {
                    ws.Cell(row, 1).Value = d.FamilleMacro;
                    ws.Cell(row, 2).Value = d.TotalSalarial;   ws.Cell(row, 2).Style.NumberFormat.Format = FmtFcfa;
                    ws.Cell(row, 3).Value = d.TotalEmployeur;  ws.Cell(row, 3).Style.NumberFormat.Format = FmtFcfa;
                    ws.Cell(row, 4).Value = d.TotalCombined;   ws.Cell(row, 4).Style.NumberFormat.Format = FmtFcfa;
                    row++;
                }
                ws.Columns().AdjustToContents();
            }

            return Finalize(wb, $"Remuneration_{filter.Personnel}_{filter.Mode}_{filter.Annee}.xlsx");
        }

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportSuiviAbsences(
            SuiviAbsencesDto dto, SuiviAbsencesFilterModel filter)
        {
            using var wb = new XLWorkbook();

            string anneesStr = filter.Annees != null && filter.Annees.Count > 0
                ? string.Join(",", filter.Annees) : DateTime.Today.Year.ToString();

            BuildSyntheseGenerique(wb, "Tableau N°5 — Suivi des Absences",
                $"INTERNE · {anneesStr}",
                ("Effectif moyen",   dto.Kpis.EffectifMoyen.ToString("N0", Fr)),
                ("Se sont absentés", dto.Kpis.NbSalariesAbsents.ToString("N0", Fr)),
                ("Total jours",      dto.Kpis.TotalJours.ToString("N1", Fr)),
                ("Absentéisme",      dto.Kpis.TauxAbsenteismePct.ToString("0.00", Fr) + " %"),
                ("JO Perdus",        dto.Kpis.JOPerdus.ToString("N1", Fr) + " j"),
                ("Resp Tmp Travail", dto.Kpis.RespTempsTravailPct.ToString("0.0", Fr) + " %"));

            var ws = wb.Worksheets.Add("KPI");
            WriteHeader(ws, "Indicateur", "Valeur");
            ws.Cell("A2").Value = "Effectif moyen";          ws.Cell("B2").Value = dto.Kpis.EffectifMoyen;
            ws.Cell("A3").Value = "Se sont absentés";        ws.Cell("B3").Value = dto.Kpis.NbSalariesAbsents;
            ws.Cell("A4").Value = "Total jours";             ws.Cell("B4").Value = dto.Kpis.TotalJours;
            ws.Cell("A5").Value = "% Absentéisme";           ws.Cell("B5").Value = dto.Kpis.TauxAbsenteismePct;       ws.Cell("B5").Style.NumberFormat.Format = FmtPct;
            ws.Cell("A6").Value = "JO Perdus";               ws.Cell("B6").Value = dto.Kpis.JOPerdus;
            ws.Cell("A7").Value = "Resp Tmp Travail";        ws.Cell("B7").Value = dto.Kpis.RespTempsTravailPct;      ws.Cell("B7").Style.NumberFormat.Format = FmtPct;
            ws.Cell("A8").Value = "Nb absences";             ws.Cell("B8").Value = dto.Kpis.NbAbsences;
            ws.Cell("A9").Value = "Durée moyenne";           ws.Cell("B9").Value = dto.Kpis.DureeMoyenneJours;
            ws.Cell("A10").Value = "Top absent";              ws.Cell("B10").Value = dto.Kpis.TopAbsent;
            ws.Cell("A11").Value = "Top absent jours";        ws.Cell("B11").Value = dto.Kpis.TopAbsentJours;
            ws.Cell("A12").Value = "Mois pic";                ws.Cell("B12").Value = dto.Kpis.MoisPicLibelle ?? "";
            ws.Columns().AdjustToContents();

            // Par Motif (10 lignes max)
            ws = wb.Worksheets.Add("Par motif");
            WriteHeader(ws, "Code", "Libellé", "Catégorie", "Nb jours", "Nb absences", "% Total");
            int row = 2;
            foreach (var m in dto.ParMotif)
            {
                ws.Cell(row, 1).Value = m.Code;
                ws.Cell(row, 2).Value = m.Libelle;
                ws.Cell(row, 3).Value = m.IsAbsenteisme ? "Absentéisme" : "Programmée";
                ws.Cell(row, 4).Value = m.NbJours;
                ws.Cell(row, 5).Value = m.NbAbsences;
                ws.Cell(row, 6).Value = m.PourcentageDuTotal / 100m; ws.Cell(row, 6).Style.NumberFormat.Format = FmtPct;
                row++;
            }
            ws.Columns().AdjustToContents();

            // Par Catégorie (DimRowDto)
            ws = wb.Worksheets.Add("Par categorie");
            WriteHeader(ws, "Catégorie", "Eff. actifs", "JO Perdus", "Absentéisme %");
            row = 2;
            foreach (var c in dto.ParCategorie)
            {
                ws.Cell(row, 1).Value = c.Libelle;
                ws.Cell(row, 2).Value = c.NbSalariesActifs;
                ws.Cell(row, 3).Value = c.JOPerdus;
                ws.Cell(row, 4).Value = c.TauxAbsenteismePct / 100m; ws.Cell(row, 4).Style.NumberFormat.Format = FmtPct;
                row++;
            }
            ws.Columns().AdjustToContents();

            // Par Département
            ws = wb.Worksheets.Add("Par departement");
            WriteHeader(ws, "Département", "Eff. actifs", "JO Perdus", "Absentéisme %");
            row = 2;
            foreach (var d in dto.ParDepartement)
            {
                ws.Cell(row, 1).Value = d.Libelle;
                ws.Cell(row, 2).Value = d.NbSalariesActifs;
                ws.Cell(row, 3).Value = d.JOPerdus;
                ws.Cell(row, 4).Value = d.TauxAbsenteismePct / 100m; ws.Cell(row, 4).Style.NumberFormat.Format = FmtPct;
                row++;
            }
            ws.Columns().AdjustToContents();

            // Par Ancienneté
            ws = wb.Worksheets.Add("Par anciennete");
            WriteHeader(ws, "Tranche", "Eff. actifs", "JO Perdus", "Absentéisme %");
            row = 2;
            foreach (var a in dto.ParAnciennete)
            {
                ws.Cell(row, 1).Value = a.Tranche;
                ws.Cell(row, 2).Value = a.NbSalariesActifs;
                ws.Cell(row, 3).Value = a.JOPerdus;
                ws.Cell(row, 4).Value = a.TauxAbsenteismePct / 100m; ws.Cell(row, 4).Style.NumberFormat.Format = FmtPct;
                row++;
            }
            ws.Columns().AdjustToContents();

            // Évolution mensuelle (2 séries)
            ws = wb.Worksheets.Add("Evolution mensuelle");
            WriteHeader(ws, "Mois", "Libellé", "J. Absentéisme", "J. Programmées", "Nb absences");
            row = 2;
            foreach (var m in dto.ParMois)
            {
                ws.Cell(row, 1).Value = m.Mois;
                ws.Cell(row, 2).Value = m.Libelle;
                ws.Cell(row, 3).Value = m.JoursAbsenteisme;
                ws.Cell(row, 4).Value = m.JoursProgrammees;
                ws.Cell(row, 5).Value = m.NbAbsences;
                row++;
            }
            ws.Columns().AdjustToContents();

            // Liste détaillée employés (avec décomposition motif)
            ws = wb.Worksheets.Add("Detail employes");
            WriteHeader(ws, "Matricule", "Salarié", "Site", "Département", "Catégorie", "Fonction", "Ancienneté",
                            "J. Abs", "J. Prog", "Total", "Resp Tmp",
                            "AUT", "NAUT", "EVENT", "MAL", "AT",
                            "CPAYE", "FORM", "MAT", "PAT");
            row = 2;
            foreach (var e in dto.Employes.Where(x => x.TotalJours > 0))
            {
                ws.Cell(row, 1).Value = e.Matricule;
                ws.Cell(row, 2).Value = e.NomComplet;
                ws.Cell(row, 3).Value = e.Site;
                ws.Cell(row, 4).Value = e.Departement;
                ws.Cell(row, 5).Value = e.Categorie;
                ws.Cell(row, 6).Value = e.Fonction;
                ws.Cell(row, 7).Value = e.AncienneteAnnees;
                ws.Cell(row, 8).Value = e.JoursAbsenteisme;
                ws.Cell(row, 9).Value = e.JoursProgrammees;
                ws.Cell(row, 10).Value = e.TotalJours;
                ws.Cell(row, 11).Value = e.RespTempsTravailPct / 100m; ws.Cell(row, 11).Style.NumberFormat.Format = FmtPct;
                ws.Cell(row, 12).Value = e.JoursAUT;
                ws.Cell(row, 13).Value = e.JoursNAUT;
                ws.Cell(row, 14).Value = e.JoursEVENT;
                ws.Cell(row, 15).Value = e.JoursMAL;
                ws.Cell(row, 16).Value = e.JoursAT;
                ws.Cell(row, 17).Value = e.JoursCPAYE;
                ws.Cell(row, 18).Value = e.JoursFORM;
                ws.Cell(row, 19).Value = e.JoursMAT;
                ws.Cell(row, 20).Value = e.JoursPAT;
                row++;
            }
            ws.Columns().AdjustToContents();

            return Finalize(wb, $"Suivi_Absences_{anneesStr.Replace(',', '_')}.xlsx");
        }

        // ─────────────────────────────────────────────────────────────────────
        public (byte[] Bytes, string FileName) ExportBilanSocial(
            BilanSocialDto dto, BilanSocialFilterModel filter)
        {
            using var wb = new XLWorkbook();

            BuildSyntheseGenerique(wb, "Tableau N°6 — Bilan Social Mensuel",
                $"INTERNE · Synthèse DTSS · {filter.Annee}",
                ("Effectif moyen",   dto.Kpis.EffectifMoyenAnnuel.ToString("N0", Fr)),
                ("Embauches",        dto.Kpis.TotalEmbauches.ToString("N0", Fr)),
                ("Départs",          dto.Kpis.TotalDeparts.ToString("N0", Fr)),
                ("Coût Employeur",   dto.Kpis.CoutEmployeurAnnuel.ToString("#,##0", FcfaFmt)),
                ("% Absentéisme",    dto.Kpis.TauxAbsenteisme.ToString("0.00", Fr) + " %"),
                ("Complétude data",  dto.Kpis.CompletudeData.ToString("0.0", Fr) + " %"));

            var ws = wb.Worksheets.Add("KPI annuels");
            WriteHeader(ws, "Indicateur", "Valeur");
            ws.Cell("A2").Value = "Effectif moyen";             ws.Cell("B2").Value = dto.Kpis.EffectifMoyenAnnuel;
            ws.Cell("A3").Value = "Effectif fin année";         ws.Cell("B3").Value = dto.Kpis.EffectifFinAnnee;
            ws.Cell("A4").Value = "Total embauches";            ws.Cell("B4").Value = dto.Kpis.TotalEmbauches;
            ws.Cell("A5").Value = "Total départs";              ws.Cell("B5").Value = dto.Kpis.TotalDeparts;
            ws.Cell("A6").Value = "Masse salariale annuelle";   ws.Cell("B6").Value = dto.Kpis.MasseSalarialeAnnuelle;     ws.Cell("B6").Style.NumberFormat.Format = FmtFcfa;
            ws.Cell("A7").Value = "Charges patronales";         ws.Cell("B7").Value = dto.Kpis.ChargesPatronalesAnnuelles; ws.Cell("B7").Style.NumberFormat.Format = FmtFcfa;
            ws.Cell("A8").Value = "Coût employeur";             ws.Cell("B8").Value = dto.Kpis.CoutEmployeurAnnuel;        ws.Cell("B8").Style.NumberFormat.Format = FmtFcfa;
            ws.Cell("A9").Value = "Total jours absence";        ws.Cell("B9").Value = dto.Kpis.TotalJoursAbsence;
            ws.Cell("A10").Value = "% Absentéisme";             ws.Cell("B10").Value = dto.Kpis.TauxAbsenteisme;           ws.Cell("B10").Style.NumberFormat.Format = FmtPct;
            ws.Cell("A11").Value = "% Complétude données";      ws.Cell("B11").Value = dto.Kpis.CompletudeData;            ws.Cell("B11").Style.NumberFormat.Format = FmtPct;
            ws.Columns().AdjustToContents();

            ws = wb.Worksheets.Add("Recap mensuel");
            WriteHeader(ws,
                "Mois", "Effectif fin", "Embauches", "Departs",
                "Masse Salariale", "Charges Patronales", "Cout Employeur",
                "Employes Absents", "Jours Absence");
            int row = 2;
            foreach (var m in dto.Mois)
            {
                ws.Cell(row, 1).Value = m.Libelle;
                ws.Cell(row, 2).Value = m.EffectifFinMois;
                ws.Cell(row, 3).Value = m.Embauches;
                ws.Cell(row, 4).Value = m.Departs;
                ws.Cell(row, 5).Value = m.MasseSalariale;       ws.Cell(row, 5).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 6).Value = m.ChargesPatronales;    ws.Cell(row, 6).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 7).Value = m.CoutEmployeur;        ws.Cell(row, 7).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 8).Value = m.EmployesAbsents;
                ws.Cell(row, 9).Value = m.JoursAbsence;
                if (m.EstTotal)
                {
                    var rng = ws.Range(row, 1, row, 9);
                    rng.Style.Fill.BackgroundColor = TotalBg;
                    rng.Style.Font.FontColor       = TotalFg;
                    rng.Style.Font.Bold            = true;
                }
                row++;
            }
            ws.Columns().AdjustToContents();

            // Ligne « dont Intérimaires »
            if (dto.DontInterimaires != null && dto.DontInterimaires.NbContrats > 0)
            {
                ws = wb.Worksheets.Add("Dont Interimaires");
                WriteHeader(ws,
                    "Indicateur", "Effectif moyen", "Nb contrats",
                    "Arrivées", "Départs", "Coût total");
                ws.Cell("A2").Value = $"Intérimaires {filter.Annee}";
                ws.Cell("B2").Value = dto.DontInterimaires.EffectifMoyen;
                ws.Cell("C2").Value = dto.DontInterimaires.NbContrats;
                ws.Cell("D2").Value = dto.DontInterimaires.ArriveesContrats;
                ws.Cell("E2").Value = dto.DontInterimaires.DepartsContrats;
                ws.Cell("F2").Value = dto.DontInterimaires.CoutTotal;
                ws.Cell("F2").Style.NumberFormat.Format = FmtFcfa;
                ws.Columns().AdjustToContents();
            }

            return Finalize(wb, $"Bilan_Social_{filter.Annee}.xlsx");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Helpers privés
        // ═════════════════════════════════════════════════════════════════════
        private static void WriteHeader(IXLWorksheet ws, params string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];
            var range = ws.Range(1, 1, 1, headers.Length);
            range.Style.Fill.BackgroundColor = HeaderBg;
            range.Style.Font.FontColor       = XLColor.White;
            range.Style.Font.Bold            = true;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }

        private static void WriteBarSheet(XLWorkbook wb, string sheetName,
            System.Collections.Generic.List<BarItemDto> items)
        {
            var ws = wb.Worksheets.Add(sheetName);
            WriteHeader(ws, "Libellé", "Valeur", "%");
            int row = 2;
            foreach (var b in items)
            {
                ws.Cell(row, 1).Value = b.Libelle;
                ws.Cell(row, 2).Value = b.Valeur;
                ws.Cell(row, 3).Value = b.Pourcentage; ws.Cell(row, 3).Style.NumberFormat.Format = FmtPct;
                row++;
            }
            // DataBar visuel sur la colonne Valeur (orange ELTON, comme la page web)
            if (items.Count > 0)
                ws.Range(2, 2, row - 1, 2).AddConditionalFormat()
                    .DataBar(XLColor.FromHtml("#F18A1C"))
                    .LowestValue().HighestValue();
            ws.Columns().AdjustToContents();
        }

        private static void WriteEgaliteSheet(XLWorkbook wb, string sheetName,
            System.Collections.Generic.List<EgaliteSalaireRowDto> rows)
        {
            var ws = wb.Worksheets.Add(sheetName);
            WriteHeader(ws, "Libellé", "Total", "Coût Moy.", "Min", "Max", "Moyenne", "Moy. Femme", "Moy. Homme", "Nb salariés", "% masse");
            int row = 2;
            foreach (var r in rows)
            {
                ws.Cell(row, 1).Value = r.Libelle;
                ws.Cell(row, 2).Value = r.Total;     ws.Cell(row, 2).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 3).Value = r.CoutMoyen; ws.Cell(row, 3).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 4).Value = r.Min;       ws.Cell(row, 4).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 5).Value = r.Max;       ws.Cell(row, 5).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 6).Value = r.Moyenne;   ws.Cell(row, 6).Style.NumberFormat.Format = FmtFcfa;
                if (r.MoyFemme.HasValue) { ws.Cell(row, 7).Value = r.MoyFemme.Value; ws.Cell(row, 7).Style.NumberFormat.Format = FmtFcfa; }
                if (r.MoyHomme.HasValue) { ws.Cell(row, 8).Value = r.MoyHomme.Value; ws.Cell(row, 8).Style.NumberFormat.Format = FmtFcfa; }
                ws.Cell(row, 9).Value = r.NbSalaries;
                ws.Cell(row, 10).Value = r.PourcentageMasse; ws.Cell(row, 10).Style.NumberFormat.Format = FmtPct;
                row++;
            }

            // ── DataBars visuels (effet barre orange dans la cellule, comme sur la page web) ──
            if (rows.Count > 0)
                ws.Range(2, 2, row - 1, 2).AddConditionalFormat()
                    .DataBar(XLColor.FromHtml("#F18A1C"))   // Orange ELTON
                    .LowestValue().HighestValue();

            ws.Columns().AdjustToContents();
        }

        /// <summary>
        /// Onglet « Synthese » générique : titre + grille de KPI sur une page
        /// imprimable. Utilisé par tous les tableaux SAUF Rémunération qui a
        /// son propre `BuildSyntheseRemuneration` plus riche (KPI + 2 tableaux Égalité).
        /// </summary>
        private static void BuildSyntheseGenerique(XLWorkbook wb, string titrePrincipal,
            string sousTitre, params (string Label, string Value)[] kpis)
        {
            var ws = wb.Worksheets.Add("Synthese");

            // Titre
            ws.Cell(1, 1).Value = titrePrincipal;
            int colSpan = Math.Max(kpis.Length, 4);
            ws.Range(1, 1, 1, colSpan).Merge();
            ws.Cell(1, 1).Style.Fill.BackgroundColor = HeaderBg;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(1).Height = 28;

            // Sous-titre
            ws.Cell(2, 1).Value = sousTitre;
            ws.Range(2, 1, 2, colSpan).Merge();
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#5C6679");
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // KPI sur 1 ligne (header + valeurs)
            int row = 4;
            for (int i = 0; i < kpis.Length; i++)
            {
                ws.Cell(row, i + 1).Value = kpis[i].Label;
                ws.Cell(row, i + 1).Style.Fill.BackgroundColor = HeaderBg;
                ws.Cell(row, i + 1).Style.Font.FontColor = XLColor.White;
                ws.Cell(row, i + 1).Style.Font.Bold = true;
                ws.Cell(row, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row + 1, i + 1).Value = kpis[i].Value;
                ws.Cell(row + 1, i + 1).Style.Font.Bold = true;
                ws.Cell(row + 1, i + 1).Style.Font.FontSize = 12;
                ws.Cell(row + 1, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row + 1, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Cell(row + 1, i + 1).Style.Border.OutsideBorderColor = XLColor.FromHtml("#E5E7EB");
                ws.Cell(row + 1, i + 1).Style.Border.LeftBorder = XLBorderStyleValues.Thick;
                ws.Cell(row + 1, i + 1).Style.Border.LeftBorderColor = XLColor.FromHtml("#F18A1C");
            }
            ws.Row(row + 1).Height = 22;

            // Note de bas de page
            ws.Cell(row + 4, 1).Value = "→ Pour les tableaux détaillés et bar charts : voir les onglets suivants ↓";
            ws.Cell(row + 4, 1).Style.Font.Italic = true;
            ws.Cell(row + 4, 1).Style.Font.FontColor = XLColor.FromHtml("#5C6679");

            ws.Columns().AdjustToContents();
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 1);
        }

        /// <summary>
        /// Onglet « Synthese » pour l'export Rémunération : KPI + 2 tableaux clés
        /// (par profession, par segment) sur une seule feuille imprimable.
        /// </summary>
        private static void BuildSyntheseRemuneration(XLWorkbook wb, RemunerationDto dto, RemunerationFilterModel filter)
        {
            var ws = wb.Worksheets.Add("Synthese");

            // Titre principal
            ws.Cell(1, 1).Value = $"Tableau N°4 — Rémunération · {filter.Personnel} · {filter.Mode} · {filter.Annee}";
            ws.Range(1, 1, 1, 10).Merge();
            ws.Cell(1, 1).Style.Fill.BackgroundColor = HeaderBg;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(1).Height = 28;

            // KPI sur 1 ligne (5 colonnes)
            int row = 3;
            ws.Cell(row, 1).Value = "Total"; ws.Cell(row, 2).Value = "Salaire Min"; ws.Cell(row, 3).Value = "Salaire Max";
            ws.Cell(row, 4).Value = "Salaire Moyen"; ws.Cell(row, 5).Value = "Coût Moy. Salarié";
            var kpiHeader = ws.Range(row, 1, row, 5);
            kpiHeader.Style.Fill.BackgroundColor = HeaderBg;
            kpiHeader.Style.Font.FontColor = XLColor.White;
            kpiHeader.Style.Font.Bold = true;
            kpiHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row++;
            ws.Cell(row, 1).Value = dto.Kpis.Total;             ws.Cell(row, 1).Style.NumberFormat.Format = FmtFcfa;
            ws.Cell(row, 2).Value = dto.Kpis.SalaireMin;        ws.Cell(row, 2).Style.NumberFormat.Format = FmtFcfa;
            ws.Cell(row, 3).Value = dto.Kpis.SalaireMax;        ws.Cell(row, 3).Style.NumberFormat.Format = FmtFcfa;
            ws.Cell(row, 4).Value = dto.Kpis.SalaireMoyen;      ws.Cell(row, 4).Style.NumberFormat.Format = FmtFcfa;
            ws.Cell(row, 5).Value = dto.Kpis.CoutMoyenSalarie;  ws.Cell(row, 5).Style.NumberFormat.Format = FmtFcfa;
            var kpiData = ws.Range(row, 1, row, 5);
            kpiData.Style.Font.Bold = true;
            kpiData.Style.Font.FontSize = 12;
            kpiData.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            kpiData.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Row(row).Height = 22;

            // ── Tableau 1 : Par catégorie professionnelle ────
            row += 3;
            ws.Cell(row, 1).Value = "Rémunération par catégorie professionnelle — Égalité des salaires";
            ws.Range(row, 1, row, 10).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#142E4D");
            ws.Cell(row, 1).Style.Font.FontSize = 11;
            ws.Cell(row, 1).Style.Border.LeftBorder = XLBorderStyleValues.Thick;
            ws.Cell(row, 1).Style.Border.LeftBorderColor = XLColor.FromHtml("#F18A1C");
            row++;
            row = AppendEgaliteRows(ws, row, dto.ParCategorie);

            // ── Tableau 2 : Par segment ───────────────────────
            row += 2;
            ws.Cell(row, 1).Value = "Rémunération par segment — Égalité des salaires";
            ws.Range(row, 1, row, 10).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#142E4D");
            ws.Cell(row, 1).Style.Font.FontSize = 11;
            ws.Cell(row, 1).Style.Border.LeftBorder = XLBorderStyleValues.Thick;
            ws.Cell(row, 1).Style.Border.LeftBorderColor = XLColor.FromHtml("#F18A1C");
            row++;
            row = AppendEgaliteRows(ws, row, dto.ParSegment);

            ws.Columns().AdjustToContents();
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);
        }

        /// <summary>Ajoute en-tête + lignes Égalité avec DataBars, retourne la ligne suivante.</summary>
        private static int AppendEgaliteRows(IXLWorksheet ws, int startRow,
            System.Collections.Generic.List<EgaliteSalaireRowDto> rows)
        {
            // En-têtes
            string[] headers = { "Libellé", "Total", "Coût Moy.", "Min", "Max", "Moyenne", "Moy. ♀", "Moy. ♂", "Nb sal.", "% masse" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(startRow, i + 1).Value = headers[i];
            var hRange = ws.Range(startRow, 1, startRow, headers.Length);
            hRange.Style.Fill.BackgroundColor = HeaderBg;
            hRange.Style.Font.FontColor = XLColor.White;
            hRange.Style.Font.Bold = true;
            hRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            int dataStart = startRow + 1;
            int row = dataStart;
            foreach (var r in rows)
            {
                ws.Cell(row, 1).Value = r.Libelle;
                ws.Cell(row, 2).Value = r.Total;     ws.Cell(row, 2).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 3).Value = r.CoutMoyen; ws.Cell(row, 3).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 4).Value = r.Min;       ws.Cell(row, 4).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 5).Value = r.Max;       ws.Cell(row, 5).Style.NumberFormat.Format = FmtFcfa;
                ws.Cell(row, 6).Value = r.Moyenne;   ws.Cell(row, 6).Style.NumberFormat.Format = FmtFcfa;
                if (r.MoyFemme.HasValue) { ws.Cell(row, 7).Value = r.MoyFemme.Value; ws.Cell(row, 7).Style.NumberFormat.Format = FmtFcfa; }
                if (r.MoyHomme.HasValue) { ws.Cell(row, 8).Value = r.MoyHomme.Value; ws.Cell(row, 8).Style.NumberFormat.Format = FmtFcfa; }
                ws.Cell(row, 9).Value = r.NbSalaries;
                ws.Cell(row, 10).Value = r.PourcentageMasse; ws.Cell(row, 10).Style.NumberFormat.Format = FmtPct;
                row++;
            }

            // DataBars sur la colonne Total (visuel orange)
            if (rows.Count > 0)
                ws.Range(dataStart, 2, row - 1, 2).AddConditionalFormat()
                    .DataBar(XLColor.FromHtml("#F18A1C"))
                    .LowestValue().HighestValue();

            return row;
        }

        private static (byte[] Bytes, string FileName) Finalize(XLWorkbook wb, string fileName)
        {
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return (ms.ToArray(), fileName);
        }
    }
}
