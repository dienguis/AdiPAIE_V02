using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DocumentFormat.OpenXml.Packaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Service de fusion du template Word d'attestation.
    ///
    /// Principe :
    ///   1. Charge le template .docx (stocké dans ParametresPaie.TemplateAttestation)
    ///   2. Remplace tous les marqueurs {{Champ}} par les vraies valeurs
    ///   3. Retourne le .docx fusionné en mémoire (byte[])
    ///
    /// Marqueurs supportés dans le template :
    ///   {{Civilite}}          → M. / Mme / Mlle
    ///   {{FullName}}          → Prénom NOM
    ///   {{Matricule}}         → numéro matricule
    ///   {{Birthday}}          → date de naissance (dd MMMM yyyy)
    ///   {{DateEmbauche}}      → date d'embauche (dd MMMM yyyy)
    ///   {{Fonction}}          → intitulé de la fonction
    ///   {{Echelon}}           → libellé de l'échelon / catégorie
    ///   {{NumeroRef}}         → référence générée automatiquement
    ///   {{DateDocument}}      → date du jour (dd MMMM yyyy)
    ///   {{VilleFait}}         → ville (défaut : Dakar)
    ///   {{RaisonSociale}}     → raison sociale de la société
    ///   {{AdresseSociete}}    → adresse complète
    ///   {{SignataireNom}}     → nom du signataire (ParametresPaie)
    ///   {{SignataireTitre}}   → titre du signataire (ParametresPaie)
    ///   {{NombreParts}}       → nombre de parts fiscales
    ///   {{SalaireBase}}       → salaire de base brut (formaté)
    ///
    /// Usage :
    ///   var docxBytes = AttestationTemplateService.Fusionner(demande, os);
    ///   // docxBytes contient le .docx prêt à télécharger ou convertir en PDF
    /// </summary>
    public static class AttestationTemplateService
    {
        // ── Point d'entrée principal ─────────────────────────────

        /// <summary>
        /// Fusionne le template avec les données de la demande.
        /// Retourne le .docx fusionné en tant que byte[].
        /// </summary>
        public static byte[] Fusionner(
            DemandeAttestation demande,
            DevExpress.ExpressApp.IObjectSpace os)
        {
            if (demande?.Salarie == null)
                throw new ArgumentNullException(nameof(demande));

            // ── Charge le template ─────────────────────────────────
            var templateBytes = ChargerTemplate(os);
            if (templateBytes == null || templateBytes.Length == 0)
                throw new InvalidOperationException(
                    "Template d'attestation introuvable. " +
                    "Veuillez l'uploader dans Paramètres de paie → Template attestation de travail.");

            // ── Construit le dictionnaire de remplacement ──────────
            var prm = ParametresPaie.TryGet(os);
            var company = new DevExpress.Xpo.XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                .FirstOrDefault();

            var marqueurs = BuildMarqueurs(demande, prm, company);

            // ── Fusionne ───────────────────────────────────────────
            return FusionnerDocx(templateBytes, marqueurs);
        }

        // ── Chargement du template ───────────────────────────────

        private static byte[] ChargerTemplate(DevExpress.ExpressApp.IObjectSpace os)
        {
            try
            {
                var prm = ParametresPaie.TryGet(os);
                if (prm?.TemplateAttestation?.Content != null &&
                    prm.TemplateAttestation.Content.Length > 0)
                    return prm.TemplateAttestation.Content;
            }
            catch { }
            return null;
        }

        // ── Construction des marqueurs ───────────────────────────

        private static Dictionary<string, string> BuildMarqueurs(
            DemandeAttestation demande,
            ParametresPaie prm,
            Company company)
        {
            var sal = demande.Salarie;
            var cultureFr = new CultureInfo("fr-FR");

            // Civilité
            var civilite = sal.Civilite switch
            {
                Civilite.Madame => "Mme",
                Civilite.Mademoiselle => "Mlle",
                _ => "M."
            };

            // Date de naissance (si disponible sur Person)
            string birthday = "—";
            try
            {
                var bd = (sal as DevExpress.Persistent.BaseImpl.Person)?.Birthday;
                if (bd.HasValue) birthday = bd.Value.ToString("dd MMMM yyyy", cultureFr);
            }
            catch { }

            // Fonction
            var fonctionIntitule = sal.Fonction?.Intitule ?? sal.Categories?.Intitule?? "—";

            // Échelon
            var echelon = sal.Echelon?.Code ?? sal.Categories?.Intitule ?? "—";

            // Référence auto : format RH/NNN/MM/AA
            var numRef = $"RH/{new Random().Next(1, 999):D3}/{DateTime.Today:MM/yy}";

            // Adresse société
            var adresse = string.Join(", ",
                new[] { company?.Address, company?.Ville, company?.Pays }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            // Crée le dictionnaire de base
            var marqueurs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["{{Civilite}}"] = civilite,
                ["{{FullName}}"] = sal.FullName ?? "—",
                ["{{Matricule}}"] = sal.Matricule ?? "—",
                //["{{Matricule}}"] = sal.Email ?? "—",
                ["{{Birthday}}"] = birthday,
                ["{{DateEmbauche}}"] = sal.DateEmbauche != default
                                            ? sal.DateEmbauche.ToString("dd MMMM yyyy", cultureFr)
                                            : "—",
                ["{{Fonction}}"] = fonctionIntitule,
                ["{{Echelon}}"] = echelon,
                ["{{NumeroRef}}"] = numRef,
                ["{{DateDocument}}"] = DateTime.Today.ToString("dd MMMM yyyy", cultureFr),
                ["{{VilleFait}}"] = "Dakar",
                ["{{RaisonSociale}}"] = company?.RaisonSociale ?? "—",
                ["{{AdresseSociete}}"] = adresse,
                ["{{SignataireNom}}"] = prm?.SignatoryName ?? prm?.SignatureName ?? "—",
                ["{{SignataireTitre}}"] = prm?.SignatoryTitle ?? prm?.SignatureTitle ?? "—",
                ["{{NombreParts}}"] = sal.NombrePartsFiscales.ToString("N1"),
                ["{{SalaireBase}}"] = sal.SalaireBase.ToString("N0") + " FCFA",
            };

            // ── Marqueurs spécifiques au congé ───────────────────────────
            if (demande.Nature == AttestationNature.Conge && demande.CongeSource != null)
            {
                var c = demande.CongeSource;
                marqueurs["{{DateDebutConge}}"] = c.DateDebut.ToString("dd MMMM yyyy", cultureFr);
                marqueurs["{{DateFinConge}}"] = c.DateFin.ToString("dd MMMM yyyy", cultureFr);
                marqueurs["{{NombreJours}}"] = c.DureeJours.ToString("N1") + " jour(s)";
                marqueurs["{{TypeConge}}"] = c.Type?.Libelle ?? "Congé payé";
                marqueurs["{{MotifConge}}"] = c.Motif ?? string.Empty;
            }
            else
            {
                // Valeurs vides pour éviter les marqueurs non remplacés dans le template
                marqueurs["{{DateDebutConge}}"] = "—";
                marqueurs["{{DateFinConge}}"] = "—";
                marqueurs["{{NombreJours}}"] = "—";
                marqueurs["{{TypeConge}}"] = "—";
                marqueurs["{{MotifConge}}"] = string.Empty;
            }

            return marqueurs;


            //return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            //{
            //    ["{{Civilite}}"] = civilite,
            //    ["{{FullName}}"] = sal.FullName ?? "—",
            //    ["{{Matricule}}"] = sal.Matricule ?? "—",
            //    ["{{Birthday}}"] = birthday,

            //    ["{{DateEmbauche}}"] = sal.DateEmbauche != default
            //     ? sal.DateEmbauche.ToString("dd MMMM yyyy", cultureFr)
            //                             : "—",
            //    ["{{Fonction}}"] = fonctionIntitule,
            //    ["{{Echelon}}"] = echelon,
            //    ["{{NumeroRef}}"] = numRef,
            //    ["{{DateDocument}}"] = DateTime.Today.ToString("dd MMMM yyyy", cultureFr),
            //    ["{{VilleFait}}"] = "Dakar",
            //    ["{{RaisonSociale}}"] = company?.RaisonSociale ?? "—",
            //    ["{{AdresseSociete}}"] = adresse,
            //    ["{{SignataireNom}}"] = prm?.SignatoryName ?? prm?.SignatureName ?? "—",
            //    ["{{SignataireTitre}}"] = prm?.SignatoryTitle ?? prm?.SignatureTitle ?? "—",
            //    ["{{NombreParts}}"] = sal.NombrePartsFiscales.ToString("N1"),
            //    ["{{SalaireBase}}"] = sal.SalaireBase.ToString("N0") + " FCFA",

            //};
        }

        // ── Fusion Open XML ──────────────────────────────────────

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

                // Lit le XML brut du document
                string xml;
                using (var reader = new StreamReader(mainPart.GetStream()))
                    xml = reader.ReadToEnd();

                // Étape 1 : nettoie les marqueurs fragmentés
                // Word peut écrire {{Ma<w:r/>tricule}} — on nettoie les balises
                // qui interrompent les marqueurs entre {{ et }}
                xml = NettoierMarqueursFragmentes(xml);

                // Étape 2 : remplace les marqueurs
                foreach (var kv in marqueurs)
                    xml = xml.Replace(kv.Key, EchapperXml(kv.Value));

                // Réécrit le XML dans le document
                using (var writer = new StreamWriter(
                    mainPart.GetStream(FileMode.Create)))
                    writer.Write(xml);

                doc.Save();
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Supprime les balises XML qui fragmentent les marqueurs {{...}}.
        /// Ex : {{Ma</w:t></w:r><w:r><w:t>tricule}} → {{Matricule}}
        /// </summary>
        private static string NettoierMarqueursFragmentes(string xml)
        {
            // Remplace tout ce qui se trouve entre {{ et }} en supprimant
            // les balises XML intermédiaires
            return System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"\{\{[^}]*\}\}",
                m =>
                {
                    // Supprime toutes les balises XML dans le marqueur
                    var propre = System.Text.RegularExpressions.Regex.Replace(
                        m.Value, @"<[^>]+>", "");
                    return propre;
                });
        }

        /// <summary>Échappe les caractères spéciaux XML dans les valeurs.</summary>
        private static string EchapperXml(string valeur)
        {
            if (string.IsNullOrEmpty(valeur)) return valeur;
            return valeur
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }


        /// <summary>
        /// Fusionne le template et convertit en PDF via LibreOffice headless.
        /// Retourne les bytes du PDF.
        /// </summary>
        public static byte[] FusionnerEtConvertirEnPdf(
            DemandeAttestation demande,
            DevExpress.ExpressApp.IObjectSpace os)
        {
            // 1. Génère le .docx fusionné
            var docxBytes = Fusionner(demande, os);

            // 2. Écrit le .docx dans un fichier temporaire
            var tempDir = Path.Combine(Path.GetTempPath(), "AdiPAIE_Attest");
            Directory.CreateDirectory(tempDir);

            var docxPath = Path.Combine(tempDir, $"attest_{Guid.NewGuid():N}.docx");
            File.WriteAllBytes(docxPath, docxBytes);

            try
            {
                // 3. Convertit en PDF via LibreOffice headless
                var pdfPath = ConvertirEnPdf(docxPath, tempDir);

                // 4. Lit le PDF
                return File.ReadAllBytes(pdfPath);
            }
            finally
            {
                // 5. Nettoyage des fichiers temporaires
                try { File.Delete(docxPath); } catch { }
                try
                {
                    var pdfTemp = Path.ChangeExtension(docxPath, ".pdf");
                    if (File.Exists(pdfTemp)) File.Delete(pdfTemp);
                }
                catch { }
            }
        }

        private static string ConvertirEnPdf(string docxPath, string outDir)
        {
            // Cherche LibreOffice dans les emplacements standard
            var soffice = TrouverSoffice();
            if (soffice == null)
                throw new InvalidOperationException(
                    "LibreOffice introuvable. Installez LibreOffice sur le serveur " +
                    "et assurez-vous que 'soffice' est dans le PATH.");

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = soffice,
                    Arguments = $"--headless --convert-to pdf --outdir \"{outDir}\" \"{docxPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            bool finished = process.WaitForExit(30_000); // timeout 30s

            if (!finished)
            {
                process.Kill();
                throw new TimeoutException("La conversion PDF a dépassé le délai de 30 secondes.");
            }

            var pdfPath = Path.ChangeExtension(docxPath, ".pdf");
            if (!File.Exists(pdfPath))
                throw new InvalidOperationException(
                    $"Échec de la conversion PDF. Stderr : {process.StandardError.ReadToEnd()}");

            return pdfPath;
        }

        private static string TrouverSoffice()
        {
            var cheminsWindows = new[]
            {
        @"C:\Program Files\LibreOffice\program\soffice.exe",
        @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
        // Versions récentes (24.x / 25.x)
        @"C:\Program Files\LibreOffice 24\program\soffice.exe",
        @"C:\Program Files\LibreOffice\program\soffice.exe",
    };
            foreach (var p in cheminsWindows)
                if (File.Exists(p)) return p;

            // Linux / Docker
            foreach (var p in new[]
                { "/usr/bin/soffice",
          "/usr/lib/libreoffice/program/soffice" })
                if (File.Exists(p)) return p;

            // Dernier recours : PATH système
            return "soffice";
        }

    }


    // ── Extension helper ────────────────────────────────────────
    internal static class OpenXmlExtensions
    {
        public static XDocument ToXDocument(
            this DocumentFormat.OpenXml.OpenXmlElement element)
        {
            // OuterXml retourne le XML sérialisé sans appel à Save()
            return XDocument.Parse(element.OuterXml);
        }
    }
}
