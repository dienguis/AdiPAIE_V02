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
                    + "Veuillez l'uploader dans Paramètres de paie → "
                    + "Template ordre de mission.");

            var prm = ParametresPaie.TryGet(os);
            var company = new DevExpress.Xpo.XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                .FirstOrDefault();

            var marqueurs = BuildMarqueurs(demande, prm, company);
            return FusionnerDocx(templateBytes, marqueurs);
        }

        private static byte[] ChargerTemplate(DevExpress.ExpressApp.IObjectSpace os)
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
            byte[] templateBytes, Dictionary<string, string> marqueurs)
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
                    using (var r = new StreamReader(hdr.GetStream())) hxml = r.ReadToEnd();
                    hxml = NettoierMarqueursFragmentes(hxml);
                    foreach (var kv in marqueurs)
                        hxml = hxml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));
                    using (var w = new StreamWriter(hdr.GetStream(FileMode.Create)))
                        w.Write(hxml);
                }
                doc.Save();
            }
            return ms.ToArray();
        }

        private static string NettoierMarqueursFragmentes(string xml)
            => Regex.Replace(xml, @"\{\{[^}]*\}\}",
                m => Regex.Replace(m.Value, @"<[^>]+>", ""));

        private static string EchapperXml(string v)
        {
            if (string.IsNullOrEmpty(v)) return v;
            return v.Replace("&", "&amp;").Replace("<", "&lt;")
                    .Replace(">", "&gt;").Replace("\"", "&quot;")
                    .Replace("\r\n", "&#xD;&#xA;").Replace("\n", "&#xA;");
        }
    }
}
