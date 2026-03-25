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

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Fusion du template "État de frais de mission" avec les données de la demande.
    ///
    /// Marqueurs identification :
    ///   {{NumeroOrdre}} {{RaisonSociale}} {{FullName}} {{Matricule}}
    ///   {{Fonction}} {{Departement}}
    ///
    /// Marqueurs itinéraire (1-8) :
    ///   {{Etape1Depart}} {{Etape1Arrivee}} {{Etape1DateDepart}}
    ///   {{Etape1DateArrivee}} {{Etape1DateRetour}}
    ///
    /// Marqueurs frais (fidèles au modèle réel) :
    ///   {{HebergOuiNon}} {{HebergNombreNuitees}} {{HebergMontant}}
    ///   {{RepasOuiNon}} {{RepasNombreRepas}} {{RepasMontant}}
    ///   {{TransportOuiNon}} {{TransportMontant}}
    ///   {{PeageOuiNon}} {{PeageMontant}}
    ///   {{AutresOuiNon}} {{AutresMontant}}
    ///   {{TotalFrais}}
    ///   {{CarburantOuiNon}} {{CarburantNombreLitres}}
    ///   {{Observations}}
    ///
    /// Marqueurs signatures :
    ///   {{DirecteurNom}} {{DateValidationN1}}
    ///   {{SignataireNom}} {{DateApprobation}}
    ///   {{DateValidationDAF}}
    /// </summary>
    public static class OrdreMissionTemplateService
    {
        // Codes catégories attendus dans le référentiel
        private const string CODE_HEBERGEMENT = "HEBERGEMENT";
        private const string CODE_REPAS = "REPAS";
        private const string CODE_TRANSPORT = "TRANSPORT";
        private const string CODE_PEAGE = "PEAGE";
        private const string CODE_AUTRES = "AUTRES";
        private const string CODE_CARBURANT = "CARBURANT";

        public static byte[] Fusionner(
            DemandeDeplacement demande,
            DevExpress.ExpressApp.IObjectSpace os)
        {
            if (demande?.Salarie == null)
                throw new ArgumentNullException(nameof(demande));

            var templateBytes = ChargerTemplate(os);
            if (templateBytes == null || templateBytes.Length == 0)
                throw new InvalidOperationException(
                    "Template état de frais introuvable. "
                    + "Veuillez uploader 'Template état de frais de mission (.docx)' dans Paramètres de paie → "
                    + "Template ordre de mission.");

            var prm = ParametresPaie.TryGet(os);
            var company = new DevExpress.Xpo.XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                .FirstOrDefault();

            var marqueurs = BuildMarqueurs(demande, prm, company);
            var lignesFrais = demande.Frais.OrderBy(f => f.Categorie?.OrdreAffichage ?? 0).ToList();
            return FusionnerDocx(templateBytes, marqueurs, lignesFrais);
        }

        private static byte[] ChargerTemplate(DevExpress.ExpressApp.IObjectSpace os)
        {
            try
            {
                var prm = ParametresPaie.TryGet(os);
                // TemplateEtatFrais — champ dédié à l'état de frais
                if (prm?.TemplateEtatFrais?.Content != null
                    && prm.TemplateEtatFrais.Content.Length > 0)
                    return prm.TemplateEtatFrais.Content;
            }
            catch { }
            return null;
        }

        private static Dictionary<string, string> BuildMarqueurs(
            DemandeDeplacement d, ParametresPaie prm, Company company)
        {
            var sal = d.Salarie;
            var cultureFr = new CultureInfo("fr-FR");

            var marqueurs = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                // ── Identification ────────────────────────────
                ["{{NumeroOrdre}}"] = d.NumeroOrdre ?? "—",
                ["{{RaisonSociale}}"] = company?.RaisonSociale ?? "—",
                ["{{DateDocument}}"] = DateTime.Today.ToString("dd/MM/yyyy", cultureFr),
                ["{{FullName}}"] = sal.FullName ?? "—",
                ["{{Matricule}}"] = sal.Matricule ?? "—",
                ["{{Fonction}}"] = sal.Fonction?.Intitule ?? "—",
                ["{{Departement}}"] = sal.Departement?.Nom ?? "—",

                // ── Mission ───────────────────────────────────
                ["{{Objet}}"] = d.Objet ?? "—",
                ["{{DateDepart}}"] = d.DateDepart.ToString("dd MMMM yyyy", cultureFr),
                ["{{DateRetour}}"] = d.DateRetour.ToString("dd MMMM yyyy", cultureFr),
                ["{{NombreJours}}"] = d.NombreJours.ToString(),
                ["{{Circuit}}"] = string.Join(" → ",
                    d.Circuit.OrderBy(c => c.Ordre)
                             .Select(c => c.VilleDepart)
                             .Concat(new[] {
                                 d.Circuit.OrderBy(c => c.Ordre)
                                          .LastOrDefault()?.VilleArrivee ?? ""
                             })
                             .Where(v => !string.IsNullOrWhiteSpace(v))),

                // ── Signatures ────────────────────────────────
                ["{{DirecteurNom}}"] = d.ValideurN1?.FullName ?? "—",
                ["{{DateValidationN1}}"] = d.DateValidationN1.HasValue
                    ? d.DateValidationN1.Value.ToString("dd/MM/yyyy") : "—",
                ["{{SignataireNom}}"] = prm?.SignatoryName ?? prm?.SignatureName ?? "—",
                ["{{DateApprobation}}"] = d.DateApprobationRH.HasValue
                    ? d.DateApprobationRH.Value.ToString("dd/MM/yyyy") : "—",
                ["{{DateValidationDAF}}"] = d.DateValidationDAF.HasValue
                    ? d.DateValidationDAF.Value.ToString("dd/MM/yyyy") : "—",

                // ── Total ─────────────────────────────────────
                ["{{TotalFrais}}"] = d.TotalFrais.ToString("N0", cultureFr),

                // ── Observations ──────────────────────────────
                ["{{Observations}}"] = d.MotifDeplacement ?? "",
            };

            // ── Étapes du circuit (1-8) ───────────────────────
            var etapes = d.Circuit.OrderBy(c => c.Ordre).ToList();
            for (int i = 1; i <= 8; i++)
            {
                var c = etapes.ElementAtOrDefault(i - 1);
                marqueurs[$"{{{{Etape{i}Depart}}}}"] = c?.VilleDepart ?? "";
                marqueurs[$"{{{{Etape{i}Arrivee}}}}"] = c?.VilleArrivee ?? "";
                marqueurs[$"{{{{Etape{i}DateDepart}}}}"] = i == 1 && d.DateDepart != default
                    ? d.DateDepart.ToString("dd/MM/yyyy") : "";
                marqueurs[$"{{{{Etape{i}DateArrivee}}}}"] = "";
                marqueurs[$"{{{{Etape{i}DateRetour}}}}"] = i == etapes.Count && d.DateRetour != default
                    ? d.DateRetour.ToString("dd/MM/yyyy") : "";
                marqueurs[$"{{{{Etape{i}Transport}}}}"] = c?.MoyenTransport ?? "";
            }

            // ── Frais par catégorie ───────────────────────────
            LigneFraisMission GetFrais(string code) =>
                d.Frais.FirstOrDefault(f =>
                    string.Equals(f.Categorie?.Code, code,
                        StringComparison.OrdinalIgnoreCase));

            string OuiNon(LigneFraisMission f) =>
                f != null && f.Montant > 0 ? "OUI" : "NON";

            string Montant(LigneFraisMission f) =>
                f != null && f.Montant > 0
                    ? f.Montant.ToString("N0", cultureFr)
                    : "";

            var hebergement = GetFrais(CODE_HEBERGEMENT);
            var repas = GetFrais(CODE_REPAS);
            var transport = GetFrais(CODE_TRANSPORT);
            var peage = GetFrais(CODE_PEAGE);
            var autres = GetFrais(CODE_AUTRES);
            var carburant = GetFrais(CODE_CARBURANT);

            marqueurs["{{HebergOuiNon}}"] = OuiNon(hebergement);
            marqueurs["{{HebergNombreNuitees}}"] = hebergement != null
                ? ((int)hebergement.Quantite).ToString() : "";
            marqueurs["{{HebergMontant}}"] = Montant(hebergement);

            marqueurs["{{RepasOuiNon}}"] = OuiNon(repas);
            marqueurs["{{RepasNombreRepas}}"] = repas != null
                ? ((int)repas.Quantite).ToString() : "";
            marqueurs["{{RepasMontant}}"] = Montant(repas);

            marqueurs["{{TransportOuiNon}}"] = OuiNon(transport);
            marqueurs["{{TransportMontant}}"] = Montant(transport);

            marqueurs["{{PeageOuiNon}}"] = OuiNon(peage);
            marqueurs["{{PeageMontant}}"] = Montant(peage);

            marqueurs["{{AutresOuiNon}}"] = OuiNon(autres);
            marqueurs["{{AutresMontant}}"] = Montant(autres);

            marqueurs["{{CarburantOuiNon}}"] = carburant != null
                && carburant.Quantite > 0 ? "OUI" : "NON";
            marqueurs["{{CarburantNombreLitres}}"] = carburant != null
                && carburant.Quantite > 0
                    ? carburant.Quantite.ToString("N1", cultureFr) + " L"
                    : "";

            return marqueurs;
        }

        private static byte[] FusionnerDocx(
            byte[] templateBytes,
            Dictionary<string, string> marqueurs,
            System.Collections.Generic.List<LigneFraisMission> lignesFrais)
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

                // ── Répétition FRAIS_ROW ──────────────────────────────
                // Trouve la ligne template contenant {{#FRAIS_ROW}},
                // la duplique pour chaque LigneFraisMission, puis la supprime.
                xml = TraiterFraisRow(xml, lignesFrais);

                // ── Marqueurs simples ─────────────────────────────────
                foreach (var kv in marqueurs)
                    xml = xml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));

                using (var writer = new StreamWriter(
                    mainPart.GetStream(FileMode.Create)))
                    writer.Write(xml);

                foreach (var hdr in mainPart.HeaderParts)
                {
                    string hxml;
                    using (var r = new StreamReader(hdr.GetStream())) hxml = r.ReadToEnd();
                    hxml = NettoierMarqueursFragmentes(hxml);
                    hxml = TraiterFraisRow(hxml, lignesFrais);
                    foreach (var kv in marqueurs)
                        hxml = hxml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));
                    using (var w = new StreamWriter(hdr.GetStream(FileMode.Create)))
                        w.Write(hxml);
                }
                doc.Save();
            }
            return ms.ToArray();
        }

        /// <summary>
        /// Gère la répétition de lignes de tableau marquées {{#FRAIS_ROW}}...{{/FRAIS_ROW}}.
        /// Trouve la ligne &lt;w:tr&gt; template, la clone pour chaque frais, la supprime.
        /// </summary>
        private static string TraiterFraisRow(
            string xml,
            System.Collections.Generic.List<LigneFraisMission> lignes)
        {
            const string OPEN = "{{#FRAIS_ROW}}";
            const string CLOSE = "{{/FRAIS_ROW}}";

            if (!xml.Contains(OPEN)) return xml;

            // Trouver <w:tr ...>...(contient OPEN)...</w:tr>
            var rowMatch = Regex.Match(xml,
                @"<w:tr[ >](?:(?!</w:tr>)[\s\S])*?" +
                Regex.Escape(OPEN) +
                @"(?:(?!</w:tr>)[\s\S])*?</w:tr>",
                RegexOptions.Singleline);

            if (!rowMatch.Success) return xml;

            var rowTemplate = rowMatch.Value;

            // Construire les lignes clonées
            var sb = new System.Text.StringBuilder();
            var cultureFr = new System.Globalization.CultureInfo("fr-FR");

            foreach (var f in lignes)
            {
                var modeLibelle = f.ModeCalcul switch
                {
                    AdiPAIE_V02.Module.Domain.DomainEnums.FraisCalculMode.TauxJournalier => "Journalier",
                    AdiPAIE_V02.Module.Domain.DomainEnums.FraisCalculMode.Kilometrique => "Km",
                    AdiPAIE_V02.Module.Domain.DomainEnums.FraisCalculMode.Forfait => "Forfait",
                    _ => ""
                };

                var row = rowTemplate
                    .Replace(OPEN, "")
                    .Replace(CLOSE, "")
                    .Replace("{{FraisLibelle}}", EchapperXml(f.Categorie?.Libelle ?? ""))
                    .Replace("{{FraisMode}}", EchapperXml(modeLibelle))
                    .Replace("{{FraisQuantite}}", f.Quantite > 0
                        ? f.Quantite.ToString("N1", cultureFr) : "")
                    .Replace("{{FraisTaux}}", f.TauxUnitaire > 0
                        ? f.TauxUnitaire.ToString("N0", cultureFr) : "")
                    .Replace("{{FraisMontant}}", f.Montant > 0
                        ? f.Montant.ToString("N0", cultureFr) : "")
                    .Replace("{{FraisObservation}}", EchapperXml(f.Observation ?? ""));

                sb.Append(row);
            }

            // Si aucune ligne, laisser une ligne vide propre
            if (lignes.Count == 0)
            {
                sb.Append(rowTemplate
                    .Replace(OPEN, "").Replace(CLOSE, "")
                    .Replace("{{FraisLibelle}}", "")
                    .Replace("{{FraisMode}}", "")
                    .Replace("{{FraisQuantite}}", "")
                    .Replace("{{FraisTaux}}", "")
                    .Replace("{{FraisMontant}}", "")
                    .Replace("{{FraisObservation}}", ""));
            }

            return xml.Replace(rowMatch.Value, sb.ToString());
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
            return v.Replace("&", "&amp;").Replace("<", "&lt;")
                    .Replace(">", "&gt;").Replace("\"", "&quot;")
                    .Replace("\r\n", "&#xD;&#xA;").Replace("\n", "&#xA;");
        }
    }
}
