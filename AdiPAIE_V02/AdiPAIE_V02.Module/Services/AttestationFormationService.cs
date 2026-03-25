using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.Persistent.Base;
using DocumentFormat.OpenXml.Packaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Service de génération des attestations de formation.
    ///
    /// Principe identique à AttestationTemplateService :
    ///   1. Charge le template .docx depuis ParametresPaie.TemplateAttestationFormation
    ///   2. Remplace les marqueurs {{XX}} par les données de l'inscription
    ///   3. Convertit en PDF via LibreOffice headless
    ///   4. Archive dans le DossierSalarie
    ///
    /// Marqueurs disponibles dans le template Word :
    ///
    ///   Salarié :
    ///     {{Civilite}}         → M. / Mme / Mlle
    ///     {{FullName}}         → Prénom NOM
    ///     {{Matricule}}        → numéro matricule
    ///     {{Fonction}}         → intitulé de la fonction
    ///     {{Departement}}      → nom du département
    ///
    ///   Formation :
    ///     {{Intitule}}         → intitulé de la session
    ///     {{Domaine}}          → libellé du domaine
    ///     {{Modalite}}         → Présentiel / Distanciel…
    ///     {{DateDebut}}        → date de début (dd MMMM yyyy)
    ///     {{DateFin}}          → date de fin (dd MMMM yyyy)
    ///     {{DureeJours}}       → durée en jours
    ///     {{DureeHeures}}      → durée en heures
    ///     {{Lieu}}             → lieu de la formation
    ///     {{FormateurNom}}     → nom du formateur ou organisme
    ///     {{Objectifs}}        → objectifs pédagogiques
    ///     {{NumeroAttestation}} → référence unique générée
    ///
    ///   Société & signataire :
    ///     {{RaisonSociale}}    → raison sociale
    ///     {{AdresseSociete}}   → adresse de la société
    ///     {{VilleSociete}}     → ville (défaut : Dakar)
    ///     {{SignataireNom}}    → nom du signataire
    ///     {{SignataireTitre}}  → titre du signataire
    ///     {{DateDocument}}     → date du jour (dd MMMM yyyy)
    ///     {{VilleDocument}}    → ville d'émission (défaut : Dakar)
    /// </summary>
    public static class AttestationFormationService
    {
        private static readonly CultureInfo Fr = new CultureInfo("fr-FR");

        // ════════════════════════════════════════════════════════
        // POINT D'ENTRÉE PRINCIPAL
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Génère l'attestation PDF pour une inscription et l'archive
        /// dans le dossier salarié.
        /// </summary>
        public static GenerationResult GenererEtArchiver(
            DevExpress.ExpressApp.IObjectSpace os,
            InscriptionFormation inscription)
        {
            if (inscription?.Salarie == null)
                return GenerationResult.Echec("Inscription ou salarié manquant.");

            if (!inscription.Presence)
                return GenerationResult.Echec(
                    $"{inscription.Salarie.FullName} n'est pas marqué présent. "
                    + "Cochez la présence avant de générer l'attestation.");

            try
            {
                // 1. Charger le template
                var templateBytes = ChargerTemplate(os);
                if (templateBytes == null || templateBytes.Length == 0)
                    return GenerationResult.Echec(
                        "Template d'attestation de formation introuvable. "
                        + "Uploadez-le dans Paramètres de paie → Template attestation formation.");

                // 2. Construire les marqueurs
                var prm = ParametresPaie.TryGet(os);
                var company = new DevExpress.Xpo.XPQuery<Company>(
                    ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                    .FirstOrDefault();

                var numero = GenererNumero(os, inscription);
                var marqueurs = BuildMarqueurs(inscription, prm, company, numero);

                // 3. Fusionner
                var docxBytes = FusionnerDocx(templateBytes, marqueurs);

                // 4. Convertir en PDF
                var pdfBytes = ConvertirEnPdf(docxBytes);

                // 5. Sauvegarder sur l'inscription
                var nomFichier = $"Attestation_{inscription.SessionFormation?.Intitule?.Replace(" ", "_")}"
                               + $"_{inscription.Salarie.LastName}_{DateTime.Today:yyyyMMdd}.pdf";

                if (inscription.DocumentAttestation == null)
                    inscription.DocumentAttestation =
                        os.CreateObject<DevExpress.Persistent.BaseImpl.FileData>();

                using var ms = new MemoryStream(pdfBytes);
                inscription.DocumentAttestation.LoadFromStream(nomFichier, ms);
                inscription.AttestationGeneree = true;
                inscription.DateAttestation = DateTime.Now;

                // 6. Archiver dans le dossier salarié
                ArchiverDansDossier(os, inscription, pdfBytes, nomFichier, numero);

                return GenerationResult.Ok(nomFichier, numero);
            }
            catch (Exception ex)
            {
                return GenerationResult.Echec($"Erreur génération : {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════
        // CHARGEMENT DU TEMPLATE
        // ════════════════════════════════════════════════════════

        private static byte[] ChargerTemplate(DevExpress.ExpressApp.IObjectSpace os)
        {
            try
            {
                var prm = ParametresPaie.TryGet(os);
                var tpl = prm?.TemplateAttestationFormation;
                if (tpl?.Content != null && tpl.Content.Length > 0)
                    return tpl.Content;
                return null;
            }
            catch { return null; }
        }

        // ════════════════════════════════════════════════════════
        // CONSTRUCTION DES MARQUEURS
        // ════════════════════════════════════════════════════════

        private static Dictionary<string, string> BuildMarqueurs(
            InscriptionFormation insc,
            ParametresPaie prm,
            Company company,
            string numero)
        {
            var sal = insc.Salarie;
            var session = insc.SessionFormation;

            var civilite = sal.Civilite switch
            {
                Civilite.Monsieur => "M.",
                Civilite.Madame => "Mme",
                Civilite.Mademoiselle=> "Mlle",
                _ => ""
            };

            var modalite = session?.Modalite switch
            {
                FormationModalite.Presentiel => "Présentiel",
                FormationModalite.Distanciel => "Distanciel",
                FormationModalite.Mixte => "Mixte",
                FormationModalite.ELearning => "E-learning",
                _ => "—"
            };

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // ── Salarié ───────────────────────────────────
                ["{{Civilite}}"] = civilite,
                ["{{FullName}}"] = sal.FullName ?? "—",
                ["{{Prenom}}"] = sal.FirstName ?? "—",
                ["{{Nom}}"] = sal.LastName ?? "—",
                ["{{Matricule}}"] = sal.Matricule ?? "—",
                ["{{Fonction}}"] = sal.Fonction?.Intitule ?? "—",
                ["{{Departement}}"] = sal.Departement?.Nom ?? "—",

                // ── Formation ─────────────────────────────────
                ["{{Intitule}}"] = session?.Intitule ?? "—",
                ["{{Domaine}}"] = session?.Domaine?.Libelle ?? "—",
                ["{{Modalite}}"] = modalite,
                ["{{DateDebut}}"] = session?.DateDebut.ToString("dd MMMM yyyy", Fr) ?? "—",
                ["{{DateFin}}"] = session?.DateFin.ToString("dd MMMM yyyy", Fr) ?? "—",
                ["{{DureeJours}}"] = session?.DureeJours.ToString("N0", Fr) ?? "—",
                ["{{DureeHeures}}"] = session?.DureeHeures > 0
                                            ? session.DureeHeures.ToString("N0", Fr)
                                            : "—",
                ["{{Lieu}}"] = session?.Lieu ?? "—",
                ["{{FormateurNom}}"] = session?.FormateurNom ?? "—",
                ["{{Objectifs}}"] = session?.Objectifs ?? "—",
                ["{{NumeroAttestation}}"] = numero,

                // ── Société ───────────────────────────────────
                ["{{RaisonSociale}}"] = company?.RaisonSociale ?? "—",
                ["{{AdresseSociete}}"] = company?.Address ?? "—",
                ["{{VilleSociete}}"] = company?.Ville ?? "Dakar",

                // ── Signataire ────────────────────────────────
                ["{{SignataireNom}}"] = prm?.SignatoryName ?? "—",
                ["{{SignataireTitre}}"] = prm?.SignatoryTitle ?? "—",

                // ── Date ──────────────────────────────────────
                ["{{DateDocument}}"] = DateTime.Today.ToString("dd MMMM yyyy", Fr),
                ["{{VilleDocument}}"] = company?.Ville ?? "Dakar",
            };
        }

        // ════════════════════════════════════════════════════════
        // FUSION OPEN XML
        // ════════════════════════════════════════════════════════

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

                // Nettoyer les marqueurs fragmentés par Word
                xml = Regex.Replace(xml, @"\{\{[^}]*\}\}",
                    m => Regex.Replace(m.Value, @"<[^>]+>", ""));

                foreach (var kv in marqueurs)
                    xml = xml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));

                using (var writer = new StreamWriter(mainPart.GetStream(FileMode.Create)))
                    writer.Write(xml);

                // En-têtes / pieds de page
                foreach (var hdr in mainPart.HeaderParts)
                {
                    string hxml;
                    using (var r = new StreamReader(hdr.GetStream())) hxml = r.ReadToEnd();
                    hxml = Regex.Replace(hxml, @"\{\{[^}]*\}\}", m => Regex.Replace(m.Value, @"<[^>]+>", ""));
                    foreach (var kv in marqueurs) hxml = hxml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));
                    using (var w = new StreamWriter(hdr.GetStream(FileMode.Create))) w.Write(hxml);
                }
                foreach (var ftr in mainPart.FooterParts)
                {
                    string fxml;
                    using (var r = new StreamReader(ftr.GetStream())) fxml = r.ReadToEnd();
                    fxml = Regex.Replace(fxml, @"\{\{[^}]*\}\}", m => Regex.Replace(m.Value, @"<[^>]+>", ""));
                    foreach (var kv in marqueurs) fxml = fxml.Replace(kv.Key, EchapperXml(kv.Value ?? ""));
                    using (var w = new StreamWriter(ftr.GetStream(FileMode.Create))) w.Write(fxml);
                }

                doc.Save();
            }
            return ms.ToArray();
        }

        private static string EchapperXml(string v)
        {
            if (string.IsNullOrEmpty(v)) return v;
            return v.Replace("&", "&amp;").Replace("<", "&lt;")
                    .Replace(">", "&gt;").Replace("\"", "&quot;")
                    .Replace("\r\n", "&#xD;&#xA;").Replace("\n", "&#xA;");
        }

        // ════════════════════════════════════════════════════════
        // CONVERSION PDF
        // ════════════════════════════════════════════════════════

        private static byte[] ConvertirEnPdf(byte[] docxBytes)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AdiPAIE_AttestFormation");
            Directory.CreateDirectory(tempDir);
            var docxPath = Path.Combine(tempDir, $"attest_form_{Guid.NewGuid():N}.docx");
            File.WriteAllBytes(docxPath, docxBytes);
            try
            {
                var chemins = new[]
                {
                    @"C:\Program Files\LibreOffice\program\soffice.exe",
                    @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
                    "/usr/bin/soffice", "soffice"
                };
                var soffice = chemins.FirstOrDefault(File.Exists) ?? "soffice";
                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = soffice,
                        Arguments = $"--headless --convert-to pdf "
                                  + $"--outdir \"{tempDir}\" \"{docxPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                if (!proc.WaitForExit(30_000))
                {
                    proc.Kill();
                    // Fallback : retourner le docx si timeout
                    return docxBytes;
                }
                var pdfPath = Path.ChangeExtension(docxPath, ".pdf");
                return File.Exists(pdfPath) ? File.ReadAllBytes(pdfPath) : docxBytes;
            }
            finally { try { File.Delete(docxPath); } catch { } }
        }

        // ════════════════════════════════════════════════════════
        // ARCHIVAGE DOSSIER SALARIÉ
        // ════════════════════════════════════════════════════════

        private static void ArchiverDansDossier(
            DevExpress.ExpressApp.IObjectSpace os,
            InscriptionFormation insc,
            byte[] pdfBytes,
            string nomFichier,
            string numero)
        {
            try
            {
                // Trouver ou créer le dossier du salarié
                var dossier = os.GetObjectsQuery<DossierSalarie>()
                    .FirstOrDefault(d => d.Salarie.Oid == insc.Salarie.Oid);
                if (dossier == null)
                {
                    dossier = os.CreateObject<DossierSalarie>();
                    dossier.Salarie = insc.Salarie;
                }

                // Créer le document dans le dossier
                var doc = os.CreateObject<DossierDocument>();
                doc.Dossier = dossier;
                doc.Categorie = DossierCategorieDocument.Autre;
                doc.Titre = $"Attestation formation — {insc.SessionFormation?.Intitule} ({numero})";
                doc.DateDocument = DateTime.Today;
                doc.SourceAuto = $"Généré automatiquement — Formation";

                // Pièce jointe
                var pj = os.CreateObject<DossierPieceJointe>();
                pj.DossierDocument = doc;
                pj.Titre = nomFichier;
                pj.Fichier = os.CreateObject<DevExpress.Persistent.BaseImpl.FileData>();
                using var ms = new MemoryStream(pdfBytes);
                pj.Fichier.LoadFromStream(nomFichier, ms);
            }
            catch (Exception ex)
            {
                Tracing.Tracer.LogError(ex);
                // Pas de throw — l'archivage est secondaire
            }
        }

        // ════════════════════════════════════════════════════════
        // NUMÉROTATION
        // ════════════════════════════════════════════════════════

        private static string GenererNumero(
            DevExpress.ExpressApp.IObjectSpace os,
            InscriptionFormation insc)
        {
            var annee = insc.SessionFormation?.DateDebut.Year ?? DateTime.Today.Year;
            var count = os.GetObjectsQuery<InscriptionFormation>()
                .Count(i => i.AttestationGeneree
                         && i.SessionFormation != null
                         && i.SessionFormation.DateDebut.Year == annee);
            return $"ATT-FORM-{annee}-{(count + 1):D4}";
        }
    }

    // ════════════════════════════════════════════════════════════
    // RÉSULTAT DE GÉNÉRATION
    // ════════════════════════════════════════════════════════════

    public class GenerationResult
    {
        public bool EstReussi { get; private set; }
        public string Message { get; private set; }
        public string NomFichier { get; private set; }
        public string Numero { get; private set; }

        public static GenerationResult Ok(string nom, string numero) =>
            new GenerationResult
            {
                EstReussi = true,
                NomFichier = nom,
                Numero = numero,
                Message = $"Attestation {numero} générée et archivée."
            };

        public static GenerationResult Echec(string raison) =>
            new GenerationResult { EstReussi = false, Message = raison };
    }
}
