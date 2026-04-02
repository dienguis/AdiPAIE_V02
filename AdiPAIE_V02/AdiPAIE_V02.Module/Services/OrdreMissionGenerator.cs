using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DocumentFormat.OpenXml.Packaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Génère l'état de frais de mission par manipulation XML directe.
    /// Même approche que AttestationTemplateService — pas de conflit
    /// avec les types DevExpress.XtraRichEdit.
    ///
    /// Le document est construit depuis le template uploadé :
    /// 1. Remplace les marqueurs simples {{...}}
    /// 2. Cherche le bloc {{#FRAIS_ROW}} ... {{/FRAIS_ROW}} dans le XML
    /// 3. Génère autant de lignes XML que de catégories de frais
    /// 4. Injecte le bloc généré et supprime le bloc modèle
    /// </summary>
    public static class OrdreMissionGenerator
    {
        private static readonly CultureInfo Fr = new CultureInfo("fr-FR");

        // ── Point d'entrée ────────────────────────────────────

        public static byte[] Generer(
            DemandeDeplacement demande,
            DevExpress.ExpressApp.IObjectSpace os)
        {
            if (demande?.Salarie == null)
                throw new ArgumentNullException(nameof(demande));

            var templateBytes = ChargerTemplate(os);
            if (templateBytes == null || templateBytes.Length == 0)
                throw new InvalidOperationException(
                    "Template état de frais introuvable. "
                    + "Veuillez l'uploader dans Paramètres de paie → "
                    + "Template état de frais de mission.");

            var prm = ParametresPaie.TryGet(os);
            var company = new DevExpress.Xpo.XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                .FirstOrDefault();

            var marqueurs = BuildMarqueurs(demande, prm, company);
            return FusionnerDocx(templateBytes, marqueurs, demande);
        }

        // ── Chargement template ───────────────────────────────

        private static byte[] ChargerTemplate(
            DevExpress.ExpressApp.IObjectSpace os)
        {
            try
            {
                var prm = ParametresPaie.TryGet(os);
                // Essaie d'abord le champ TemplateEtatFrais
                if (prm?.TemplateEtatFrais?.Content != null
                    && prm.TemplateEtatFrais.Content.Length > 0)
                    return prm.TemplateEtatFrais.Content;
                // Fallback sur TemplateOrdreMission
                if (prm?.TemplateOrdreMission?.Content != null
                    && prm.TemplateOrdreMission.Content.Length > 0)
                    return prm.TemplateOrdreMission.Content;
            }
            catch { }
            return null;
        }

        // ── Marqueurs simples ─────────────────────────────────

        private static Dictionary<string, string> BuildMarqueurs(
            DemandeDeplacement d, ParametresPaie prm, Company company)
        {
            var sal = d.Salarie;

            var circuitTexte = string.Join(" → ",
                d.Circuit.OrderBy(c => c.Ordre)
                    .Select(c => c.VilleArrivee));
            if (string.IsNullOrWhiteSpace(circuitTexte))
                circuitTexte = d.Destination ?? "—";

            var m = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["{{NumeroOrdre}}"] = d.NumeroOrdre ?? "—",
                ["{{RaisonSociale}}"] = company?.RaisonSociale ?? "—",
                ["{{DateDocument}}"] = DateTime.Today
                    .ToString("dd MMMM yyyy", Fr),
                ["{{FullName}}"] = sal.FullName ?? "—",
                ["{{Matricule}}"] = sal.Matricule ?? "—",
                ["{{Fonction}}"] = sal.Fonction?.Intitule ?? "—",
                ["{{Departement}}"] = sal.Departement?.Nom ?? "—",
                ["{{Objet}}"] = d.Objet ?? "—",
                ["{{Destination}}"] = d.Destination ?? "—",
                ["{{Circuit}}"] = circuitTexte,
                ["{{DateDepart}}"] = d.DateDepart
                    .ToString("dd MMMM yyyy", Fr),
                ["{{DateRetour}}"] = d.DateRetour
                    .ToString("dd MMMM yyyy", Fr),
                ["{{NombreJours}}"] = d.NombreJours.ToString(),
                ["{{TotalFrais}}"] = d.TotalFrais
                    .ToString("N0", Fr) + " FCFA",
                ["{{Observations}}"] = d.MotifDeplacement ?? "",
                ["{{DirecteurNom}}"] = d.ValideurN1?.FullName ?? "—",
                ["{{DateValidationN1}}"] = d.DateValidationN1.HasValue
                    ? d.DateValidationN1.Value.ToString("dd/MM/yyyy") : "—",
                ["{{SignataireNom}}"] = prm?.SignatoryName
                    ?? prm?.SignatureName ?? "—",
                ["{{SignataireTitre}}"] = prm?.SignatoryTitle
                    ?? prm?.SignatureTitle ?? "—",
                ["{{DateApprobation}}"] = d.DateApprobationRH.HasValue
                    ? d.DateApprobationRH.Value.ToString("dd/MM/yyyy") : "—",
                ["{{DateValidationDAF}}"] = d.DateValidationDAF.HasValue
                    ? d.DateValidationDAF.Value.ToString("dd/MM/yyyy") : "—",
            };

            // Étapes circuit (1-8)
            var etapes = d.Circuit.OrderBy(c => c.Ordre).ToList();
            for (int i = 1; i <= 8; i++)
            {
                var e = etapes.ElementAtOrDefault(i - 1);
                m[$"{{{{Etape{i}Depart}}}}"] = e?.VilleDepart?.Nom ?? "";
                m[$"{{{{Etape{i}Arrivee}}}}"] = e?.VilleArrivee?.Nom ?? "";
                m[$"{{{{Etape{i}DateDepart}}}}"] = i == 1
                    ? d.DateDepart.ToString("dd/MM/yyyy") : "";
                m[$"{{{{Etape{i}DateRetour}}}}"] = i == etapes.Count
                    ? d.DateRetour.ToString("dd/MM/yyyy") : "";
                m[$"{{{{Etape{i}Transport}}}}"] = e?.MoyenTransport ?? "";
            }

            // Carburant séparé
            var carburant = d.Frais.FirstOrDefault(f =>
                string.Equals(f.Categorie?.Code, "CARBURANT",
                    StringComparison.OrdinalIgnoreCase));
            m["{{CarburantOuiNon}}"] = carburant?.Quantite > 0
                ? "OUI" : "NON";
            m["{{CarburantNombreLitres}}"] = carburant?.Quantite > 0
                ? carburant.Quantite.ToString("N1", Fr) + " L" : "—";

            return m;
        }

        // ── Fusion hybride XML ────────────────────────────────

        private static byte[] FusionnerDocx(
            byte[] templateBytes,
            Dictionary<string, string> marqueurs,
            DemandeDeplacement d)
        {
            var ms = new MemoryStream();
            ms.Write(templateBytes, 0, templateBytes.Length);
            ms.Position = 0;

            using (var doc = WordprocessingDocument.Open(ms, isEditable: true))
            {
                var mainPart = doc.MainDocumentPart;

                // Étape 1 : lire le XML du body
                string xml;
                using (var reader = new StreamReader(mainPart.GetStream()))
                    xml = reader.ReadToEnd();

                // Étape 2 : injecter les lignes de frais dynamiques
                xml = InjecterFraisDynamiques(xml, d);

                // Étape 3 : nettoyer les marqueurs fragmentés par Word
                xml = NettoierMarqueursFragmentes(xml);

                // Étape 4 : remplacer les marqueurs simples
                foreach (var kv in marqueurs)
                    xml = xml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));

                // Étape 5 : écrire le XML modifié
                using (var writer = new StreamWriter(
                    mainPart.GetStream(FileMode.Create)))
                    writer.Write(xml);

                // Étape 6 : même traitement pour les headers
                foreach (var hdr in mainPart.HeaderParts)
                {
                    string hxml;
                    using (var r = new StreamReader(hdr.GetStream()))
                        hxml = r.ReadToEnd();
                    hxml = NettoierMarqueursFragmentes(hxml);
                    foreach (var kv in marqueurs)
                        hxml = hxml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));
                    using (var w = new StreamWriter(
                        hdr.GetStream(FileMode.Create)))
                        w.Write(hxml);
                }

                doc.Save();
            }

            return ms.ToArray();
        }

        // ── Injection frais dynamiques par XML ────────────────

        /// <summary>
        /// Cherche le bloc {{#FRAIS_ROW}} ... {{/FRAIS_ROW}} dans le XML.
        /// Le bloc correspond à une ligne de tableau Word (w:tr).
        /// Pour chaque ligne de frais, génère un clone avec les données réelles.
        /// Remplace le bloc modèle par toutes les lignes générées.
        /// </summary>
        private static string InjecterFraisDynamiques(
            string xml, DemandeDeplacement d)
        {
            // Cherche la ligne tableau qui contient {{#FRAIS_ROW}}
            // Une ligne Word = <w:tr ...> ... </w:tr>
            var rowPattern = new Regex(
                @"<w:tr[ >].*?</w:tr>",
                RegexOptions.Singleline);

            var match = rowPattern.Matches(xml)
                .Cast<Match>()
                .FirstOrDefault(m => m.Value.Contains("{{#FRAIS_ROW}}"));

            if (match == null)
                return xml; // Pas de ligne modèle trouvée — retourne tel quel

            var ligneModele = match.Value;

            // Frais à injecter (sauf carburant qui est séparé)
            var frais = d.Frais
                .OrderBy(f => f.Categorie?.OrdreAffichage ?? 99)
                .Where(f => !string.Equals(
                    f.Categorie?.Code, "CARBURANT",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Génère les lignes XML
            var sb = new StringBuilder();
            foreach (var f in frais)
            {
                var modeLabel = f.ModeCalcul switch
                {
                    FraisCalculMode.TauxJournalier => "Par jour",
                    FraisCalculMode.Forfait => "Forfait",
                    FraisCalculMode.Kilometrique => "Par km/litre",
                    _ => "—"
                };

                var ligne = ligneModele
                    .Replace("{{#FRAIS_ROW}}", "")
                    .Replace("{{/FRAIS_ROW}}", "")
                    .Replace("{{FraisLibelle}}",
                        EchapperXml(f.Categorie?.Libelle ?? "—"))
                    .Replace("{{FraisMode}}",
                        EchapperXml(modeLabel))
                    .Replace("{{FraisQuantite}}",
                        f.Quantite.ToString("N1", Fr))
                    .Replace("{{FraisTaux}}",
                        f.TauxUnitaire.ToString("N0", Fr)
                        + (f.EstModifie ? " *" : ""))
                    .Replace("{{FraisMontant}}",
                        f.Montant.ToString("N0", Fr))
                    .Replace("{{FraisObservation}}",
                        EchapperXml(f.Observation ?? ""));

                sb.Append(ligne);
            }

            // Si aucun frais — affiche une ligne vide informative
            if (!frais.Any())
            {
                var ligneVide = ligneModele
                    .Replace("{{#FRAIS_ROW}}", "")
                    .Replace("{{/FRAIS_ROW}}", "")
                    .Replace("{{FraisLibelle}}", "Aucun frais saisi")
                    .Replace("{{FraisMode}}", "")
                    .Replace("{{FraisQuantite}}", "")
                    .Replace("{{FraisTaux}}", "")
                    .Replace("{{FraisMontant}}", "")
                    .Replace("{{FraisObservation}}", "");
                sb.Append(ligneVide);
            }

            // Remplace la ligne modèle par les lignes générées
            return xml.Replace(ligneModele, sb.ToString());
        }

        // ── Helpers XML texte ─────────────────────────────────

        private static string NettoierMarqueursFragmentes(string xml)
            => Regex.Replace(xml, @"\{\{[^}]*\}\}",
                m => Regex.Replace(m.Value, @"<[^>]+>", ""));

        private static string EchapperXml(string v)
        {
            if (string.IsNullOrEmpty(v)) return v;
            return v.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("\r\n", "&#xA;")
                    .Replace("\n", "&#xA;");
        }
    }
}
