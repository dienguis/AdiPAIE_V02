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
    /// Fusion du template Ordre de Mission (document d'autorisation avant départ).
    /// Pas de frais dynamiques — juste l'identité, le circuit, les dates et les signatures.
    ///
    /// Marqueurs :
    ///   {{NumeroOrdre}} {{RaisonSociale}} {{DateDocument}}
    ///   {{FullName}} {{Matricule}} {{Fonction}} {{Departement}}
    ///   {{Objet}} {{Circuit}}
    ///   {{DateDepart}} {{DateRetour}} {{NombreJours}}
    ///   {{Etape1Depart}} ... {{Etape8Transport}}
    ///   {{SignataireNom}} {{SignataireTitre}}
    ///   {{DateApprobation}} {{DateValidationDAF}}
    /// </summary>
    public static class OrdreMissionSimpleService
    {
        private static readonly CultureInfo Fr = new CultureInfo("fr-FR");

        public static byte[] Fusionner(
            DemandeDeplacement demande,
            DevExpress.ExpressApp.IObjectSpace os)
        {
            if (demande?.Salarie == null)
                throw new ArgumentNullException(nameof(demande));

            var templateBytes = ChargerTemplate(os);
            if (templateBytes == null || templateBytes.Length == 0)
                throw new InvalidOperationException(
                    "Template ordre de mission introuvable. "
                    + "Veuillez l'uploader dans Paramètres de paie → "
                    + "Template ordre de mission.");

            var prm = ParametresPaie.TryGet(os);
            var company = new DevExpress.Xpo.XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                .FirstOrDefault();

            var marqueurs = BuildMarqueurs(demande, prm, company);
            return FusionnerDocx(templateBytes, marqueurs);
        }

        private static byte[] ChargerTemplate(
            DevExpress.ExpressApp.IObjectSpace os)
        {
            try
            {
                var prm = ParametresPaie.TryGet(os);
                if (prm?.TemplateOrdreMission?.Content != null
                    && prm.TemplateOrdreMission.Content.Length > 0)
                    return prm.TemplateOrdreMission.Content;
            }
            catch { }
            return null;
        }

        private static Dictionary<string, string> BuildMarqueurs(
            DemandeDeplacement d, ParametresPaie prm, Company company)
        {
            var sal = d.Salarie;

            // Circuit texte résumé
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
                ["{{Circuit}}"] = circuitTexte,
                ["{{DateDepart}}"] = d.DateDepart.ToString("dd MMMM yyyy", Fr),
                ["{{DateRetour}}"] = d.DateRetour.ToString("dd MMMM yyyy", Fr),
                ["{{NombreJours}}"] = d.NombreJours.ToString(),
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

            return m;
        }

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

                xml = NettoierMarqueursFragmentes(xml);
                foreach (var kv in marqueurs)
                    xml = xml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));

                using (var writer = new StreamWriter(
                    mainPart.GetStream(FileMode.Create)))
                    writer.Write(xml);

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

        private static string NettoierMarqueursFragmentes(string xml)
        {
            // Étape 0 : fusionner les accolades séparées par des balises XML
            // Cas : {</w:t></w:r><w:r><w:t>{ → {{
            xml = Regex.Replace(xml, @"\{(<[^>]+>)+\{", "{{");
            // Cas : }</w:t></w:r><w:r><w:t>} → }}
            xml = Regex.Replace(xml, @"\}(<[^>]+>)+\}", "}}");
            // Étape 1 : supprimer les balises XML qui fragmentent l'intérieur
            // Ex : {{Nom</w:r><w:r><w:t>breJours}} → {{NombreJours}}
            return Regex.Replace(xml, @"\{\{[^}]*\}\}",
                m => Regex.Replace(m.Value, @"<[^>]+>", ""));
        }

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
