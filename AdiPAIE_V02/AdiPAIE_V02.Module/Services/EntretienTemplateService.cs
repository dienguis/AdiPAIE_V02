using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DocumentFormat.OpenXml.Packaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Service de fusion du template Word de la fiche d'entretien annuel.
    ///
    /// Marqueurs supportés :
    ///   Identification    : {{FullName}}, {{Fonction}}, {{Departement}}, {{Anciennete}},
    ///                       {{AnciennetePoste}}, {{Evaluateur}}, {{DateRealisation}},
    ///                       {{NiveauInstruction}}, {{RaisonSociale}}, {{DateDocument}},
    ///                       {{SignataireNom}}
    ///   Notes globales    : {{NoteGlobaleManager}}, {{NoteGlobaleService}}
    ///   Commentaires      : {{CommentairesHierarchie}}, {{CommentairesCollaborateur}},
    ///                       {{EvolutionSouhaitee}}, {{ConclusionGenerale}}
    ///   Score             : {{ScoreGlobal}}
    ///   Missions (1-5)    : {{Mission1Intitule}}, {{Mission1CommentaireManager}},
    ///                       {{Mission1CommentaireSalarie}}
    ///   Objectifs (1-5)   : {{Obj1Libelle}}, {{Obj1Commentaire}}
    ///   Formations (1-3)  : {{Formation1Intitule}}, {{Formation1PointsAmelioration}},
    ///                       {{Formation1Mesures}}
    /// </summary>
    public static class EntretienTemplateService
    {
        // ── Point d'entrée ────────────────────────────────────

        public static byte[] Fusionner(
            EntretienAnnuel entretien,
            DevExpress.ExpressApp.IObjectSpace os)
        {
            if (entretien?.Salarie == null)
                throw new ArgumentNullException(nameof(entretien));

            var templateBytes = ChargerTemplate(os);
            if (templateBytes == null || templateBytes.Length == 0)
                throw new InvalidOperationException(
                    "Template entretien introuvable. "
                    + "Veuillez l'uploader dans Paramètres de paie → Template entretien annuel.");

            var prm = ParametresPaie.TryGet(os);
            var company = new DevExpress.Xpo.XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                .FirstOrDefault();

            var marqueurs = BuildMarqueurs(entretien, prm, company);
            return FusionnerDocx(templateBytes, marqueurs);
        }

        // ── Chargement template ───────────────────────────────

        private static byte[] ChargerTemplate(DevExpress.ExpressApp.IObjectSpace os)
        {
            try
            {
                var prm = ParametresPaie.TryGet(os);
                if (prm?.TemplateEntretienAnnuel?.Content != null
                    && prm.TemplateEntretienAnnuel.Content.Length > 0)
                    return prm.TemplateEntretienAnnuel.Content;
            }
            catch { }
            return null;
        }

        // ── Construction des marqueurs ────────────────────────

        private static Dictionary<string, string> BuildMarqueurs(
            EntretienAnnuel entretien,
            ParametresPaie prm,
            Company company)
        {
            var sal = entretien.Salarie;
            var cultureFr = new CultureInfo("fr-FR");

            // ── Identification ────────────────────────────────
            var marqueurs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["{{FullName}}"] = sal.FullName ?? "—",
                ["{{Fonction}}"] = sal.Fonction?.Intitule ?? "—",
                ["{{Departement}}"] = sal.Departement?.Nom ?? "—",
                ["{{Anciennete}}"] = sal.Anciennete > 0
                                            ? $"{sal.Anciennete} an(s)" : "—",
                ["{{AnciennetePoste}}"] = "—",  // à calculer si tu as DateChangementPoste
                ["{{Evaluateur}}"] = entretien.Evaluateur?.FullName ?? "—",
                ["{{DateRealisation}}"] = entretien.DateRealisation.HasValue
                                            ? entretien.DateRealisation.Value.ToString("dd MMMM yyyy", cultureFr)
                                            : "—",
                ["{{NiveauInstruction}}"] = entretien.NiveauInstruction ?? "—",
                ["{{RaisonSociale}}"] = company?.RaisonSociale ?? "—",
                ["{{DateDocument}}"] = DateTime.Today.ToString("dd MMMM yyyy", cultureFr),
                ["{{SignataireNom}}"] = prm?.SignatoryName ?? prm?.SignatureName ?? "—",
                ["{{NoteGlobaleManager}}"] = entretien.NoteGlobaleManager.HasValue
                                            ? entretien.NoteGlobaleManager.Value.ToString()
                                                .Replace("APlus", "A+").Replace("_", "")
                                            : "—",
                ["{{NoteGlobaleService}}"] = entretien.NoteGlobaleService.HasValue
                                            ? entretien.NoteGlobaleService.Value.ToString()
                                                .Replace("APlus", "A+").Replace("_", "")
                                            : "—",
                ["{{CommentairesHierarchie}}"] = entretien.CommentairesHierarchie ?? "—",
                ["{{CommentairesCollaborateur}}"] = entretien.CommentairesCollaborateur ?? "—",
                ["{{EvolutionSouhaitee}}"] = entretien.EvolutionSouhaitee ?? "—",
                ["{{ConclusionGenerale}}"] = entretien.ConclusionGenerale ?? "—",
                ["{{ScoreGlobal}}"] = entretien.ScoreGlobal.ToString("N2", cultureFr),
                ["{{Annee}}"] = entretien.Campagne?.Annee.ToString() ?? "—",
            };

            // ── Missions 1 à 5 (Partie I-A) ───────────────────
            var missions = entretien.Missions
                .OrderBy(m => m.Numero)
                .ToList();
            for (int i = 1; i <= 5; i++)
            {
                var m = missions.ElementAtOrDefault(i - 1);
                marqueurs[$"{{{{Mission{i}Intitule}}}}"] = m?.IntituleMission ?? "—";
                marqueurs[$"{{{{Mission{i}NoteManager}}}}"] = m?.NoteManager?.ToString() ?? "—";
                marqueurs[$"{{{{Mission{i}NoteAutoEval}}}}"] = m?.NoteAutoEval?.ToString() ?? "—";
                marqueurs[$"{{{{Mission{i}CommentaireManager}}}}"] = m?.CommentaireManager ?? "";
                marqueurs[$"{{{{Mission{i}CommentaireSalarie}}}}"] = m?.CommentaireSalarie ?? "";
            }

            // ── Objectifs 1 à 5 (Partie I-B) ──────────────────
            var objectifs = entretien.Objectifs
                .OrderBy(o => o.EstObjectifNplus1)
                .ToList();
            var objectifsN = objectifs.Where(o => !o.EstObjectifNplus1).ToList();
            var objectifsNp1 = objectifs.Where(o => o.EstObjectifNplus1).ToList();

            for (int i = 1; i <= 5; i++)
            {
                var o = objectifsN.ElementAtOrDefault(i - 1);
                marqueurs[$"{{{{Obj{i}Libelle}}}}"] = o?.Libelle ?? "—";
                marqueurs[$"{{{{Obj{i}Commentaire}}}}"] = o?.CommentaireBilan ?? "";
                marqueurs[$"{{{{Obj{i}Atteinte}}}}"] = o?.StatutAtteinte.ToString() ?? "—";
            }

            for (int i = 1; i <= 3; i++)
            {
                var o = objectifsNp1.ElementAtOrDefault(i - 1);
                marqueurs[$"{{{{ObjNp1_{i}Libelle}}}}"] = o?.Libelle ?? "—";
                marqueurs[$"{{{{ObjNp1_{i}Commentaire}}}}"] = o?.CommentaireObjectif ?? "";
            }

            // ── Formations 1 à 3 (Partie II) ──────────────────
            var formations = entretien.BesoinsFormation
                .OrderBy(f => f.Numero)
                .ToList();
            for (int i = 1; i <= 3; i++)
            {
                var f = formations.ElementAtOrDefault(i - 1);
                marqueurs[$"{{{{Formation{i}Intitule}}}}"] = f?.IntituleFormation ?? "—";
                marqueurs[$"{{{{Formation{i}PointsAmelioration}}}}"] = f?.PointsAmelioration ?? "";
                marqueurs[$"{{{{Formation{i}Mesures}}}}"] = f?.MesuresMoyens ?? "";
            }

            // ── Aptitudes management (Partie III) ─────────────
            var aptitudes = entretien.Aptitudes.OrderBy(a => a.Ordre).ToList();
            for (int i = 1; i <= 7; i++)
            {
                var a = aptitudes.ElementAtOrDefault(i - 1);
                marqueurs[$"{{{{Apt{i}Niveau}}}}"] = a?.Niveau?.ToString() ?? "—";
                marqueurs[$"{{{{Apt{i}Appreciation}}}}"] = a?.Appreciation ?? "";
            }

            return marqueurs;
        }

        // ── Fusion Open XML ───────────────────────────────────

        private static byte[] FusionnerDocx(
            byte[] templateBytes,
            Dictionary<string, string> marqueurs)
        {
            var ms = new MemoryStream();
            ms.Write(templateBytes, 0, templateBytes.Length);
            ms.Position = 0;

            using (var doc = WordprocessingDocument.Open(ms, isEditable: true))
            {
                var mainPart = doc.MainDocumentPart;

                string xml;
                using (var reader = new StreamReader(mainPart.GetStream()))
                    xml = reader.ReadToEnd();

                // Nettoie les marqueurs fragmentés par Word
                xml = NettoierMarqueursFragmentes(xml);

                // Remplace les marqueurs
                foreach (var kv in marqueurs)
                    xml = xml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));

                using (var writer = new StreamWriter(mainPart.GetStream(FileMode.Create)))
                    writer.Write(xml);

                // Headers et footers
                foreach (var hdr in mainPart.HeaderParts)
                {
                    string hxml;
                    using (var r = new StreamReader(hdr.GetStream()))
                        hxml = r.ReadToEnd();
                    hxml = NettoierMarqueursFragmentes(hxml);
                    foreach (var kv in marqueurs)
                        hxml = hxml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));
                    using (var w = new StreamWriter(hdr.GetStream(FileMode.Create)))
                        w.Write(hxml);
                }

                foreach (var ftr in mainPart.FooterParts)
                {
                    string fxml;
                    using (var r = new StreamReader(ftr.GetStream()))
                        fxml = r.ReadToEnd();
                    fxml = NettoierMarqueursFragmentes(fxml);
                    foreach (var kv in marqueurs)
                        fxml = fxml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));
                    using (var w = new StreamWriter(ftr.GetStream(FileMode.Create)))
                        w.Write(fxml);
                }

                doc.Save();
            }

            return ms.ToArray();
        }

        private static string NettoierMarqueursFragmentes(string xml)
        {
            return Regex.Replace(xml, @"\{\{[^}]*\}\}",
                m => Regex.Replace(m.Value, @"<[^>]+>", ""));
        }

        private static string EchapperXml(string valeur)
        {
            if (string.IsNullOrEmpty(valeur)) return valeur;
            return valeur
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;")
                .Replace("\r\n", "&#xD;&#xA;")
                .Replace("\n", "&#xA;");
        }
    }
}
