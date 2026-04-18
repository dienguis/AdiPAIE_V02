using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
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
    /// Service de fusion des templates de contrat de travail.
    ///
    /// Un template par type — stockés dans ParametresPaie :
    ///   TemplateContratCDI   → CDI
    ///   TemplateContratCDD   → CDD
    ///   TemplateContratStage → Stage
    ///
    /// Marqueurs communs :
    ///   {{Reference}} {{TypeContrat}} {{DateDocument}} {{VilleFait}}
    ///   {{Civilite}} {{FullName}} {{Matricule}} {{DateNaissance}}
    ///   {{Nationalite}} {{Adresse}} {{NumeroCNI}}
    ///   {{Fonction}} {{Departement}} {{Categorie}} {{Echelon}}
    ///   {{DateDebut}} {{PeriodeEssai}} {{DureePeriodeEssai}}
    ///   {{SalaireBase}} {{IndemniteLogement}} {{PrimeTransport}} {{TotalBrut}}
    ///   {{RaisonSociale}} {{AdresseSociete}} {{SignataireNom}} {{SignataireTitre}}
    ///
    /// Marqueurs CDD/Stage uniquement :
    ///   {{DateFin}} {{DureeMois}} {{MotifCDD}} {{MotifCDDDetail}}
    /// </summary>
    public static class ContratTemplateService
    {
        private static readonly CultureInfo Fr = new("fr-FR");

        // ── Point d'entrée ────────────────────────────────────────────
        public static byte[] Fusionner(
            ContratSalarie contrat,
            DevExpress.ExpressApp.IObjectSpace os)
        {
            if (contrat?.Salarie == null)
                throw new ArgumentNullException(nameof(contrat));

            var templateBytes = ChargerTemplate(os, contrat.TypeContrat ?? TypeContrat.CDI);
            if (templateBytes == null || templateBytes.Length == 0)
                throw new InvalidOperationException(
                    $"Template {contrat.TypeContrat} introuvable. "
                    + "Veuillez l'uploader dans Paramètres de paie → Templates contrats.");

            var prm = ParametresPaie.TryGet(os);
            var company = new DevExpress.Xpo.XPQuery<Company>(
                ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session)
                .FirstOrDefault();

            var marqueurs = BuildMarqueurs(contrat, prm, company);
            return FusionnerDocx(templateBytes, marqueurs);
        }

        // ── Chargement du bon template ────────────────────────────────
        private static byte[] ChargerTemplate(
            DevExpress.ExpressApp.IObjectSpace os,
            TypeContrat? type)
        {
            try
            {
                var prm = ParametresPaie.TryGet(os);
                var fd = type switch
                {
                    TypeContrat.CDI => prm?.TemplateContratCDI,
                    TypeContrat.CDD => prm?.TemplateContratCDD,
                    TypeContrat.Stage => prm?.TemplateContratStage,
                    null => null,
                    _ => null
                };
                if (fd?.Content != null && fd.Content.Length > 0)
                    return fd.Content;
            }
            catch { }
            return null;
        }

        // ── Construction des marqueurs ────────────────────────────────
        private static Dictionary<string, string> BuildMarqueurs(
            ContratSalarie c, ParametresPaie prm, Company company)
        {
            var s = c.Salarie;
            var civilite = s?.Civilite.ToString() ?? "";

            return new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                // ── Document ──────────────────────────────────────────
                ["{{Reference}}"] = c.Reference ?? "—",
                ["{{TypeContrat}}"] = (c.TypeContrat ?? TypeContrat.CDI) switch
                {
                    TypeContrat.CDI => "Contrat à Durée Indéterminée (CDI)",
                    TypeContrat.CDD => "Contrat à Durée Déterminée (CDD)",
                    TypeContrat.Stage => "Contrat de Stage",
                    _ => c.TypeContrat?.ToString() ?? "—"
                },
                ["{{DateDocument}}"] = c.DateDocument.ToString("dd MMMM yyyy", Fr),
                ["{{VilleFait}}"] = c.VilleFait ?? "Dakar",

                // ── Identité salarié ──────────────────────────────────
                ["{{Civilite}}"] = civilite,
                ["{{FullName}}"] = s?.FullName ?? "—",
                ["{{Matricule}}"] = s?.Matricule ?? "—",
                ["{{DateNaissance}}"] = GetBirthday(s, Fr),
                ["{{FilsDe}}"] = s?.FilsDe ?? "—",
                ["{{Sexe}}"] = s?.Sexe switch
                {
                    Sexe.Masculin => "Masculin",
                    Sexe.Feminin  => "Féminin",
                    _ => "—"
                },
                ["{{Nationalite}}"] = s?.Nationalite ?? "Sénégalaise",
                ["{{SituationFamille}}"] = s?.SatutMarital switch
                {
                    SituationMaritale.Celibataire => "Célibataire",
                    SituationMaritale.Marie       => "Marié(e)",
                    SituationMaritale.Divorce     => "Divorcé(e)",
                    SituationMaritale.Veuf        => "Veuf/Veuve",
                    _ => "—"
                },
                ["{{Adresse}}"] = GetAdresse(s),
                ["{{NumeroCNI}}"] = s?.NumeroCNI ?? "—",
                ["{{PersonneUrgence}}"] = s?.ContactUrgenceNom ?? "—",
                ["{{TelUrgence}}"] = s?.ContactUrgenceTel ?? "—",

                // ── Poste ─────────────────────────────────────────────
                ["{{Fonction}}"] = s?.Fonction?.Intitule ?? "—",
                ["{{Departement}}"] = s?.Departement?.Nom ?? "—",
                ["{{Categorie}}"] = s?.Categories?.Intitule ?? "—",
                ["{{Echelon}}"] = s?.Echelon?.Libelle ?? s?.Echelon?.Code ?? "—",

                // ── Dates ─────────────────────────────────────────────
                ["{{DateDebut}}"] = c.DateDebut.ToString("dd MMMM yyyy", Fr),
                ["{{DateFin}}"] = c.DateFin?.ToString("dd MMMM yyyy", Fr) ?? "—",
                ["{{DureeMois}}"] = c.DureeMois?.ToString() ?? "—",

                // ── Période d'essai ───────────────────────────────────
                ["{{PeriodeEssai}}"] = c.PeriodeEssai ? "OUI" : "NON",
                ["{{DureePeriodeEssai}}"] = c.PeriodeEssai
                    ? (c.DureePeriodeEssai ?? "à préciser")
                    : "Sans période d'essai",

                // ── Motif CDD ─────────────────────────────────────────
                ["{{MotifCDD}}"] = c.MotifCDD?.ToString() ?? "—",
                ["{{MotifCDDDetail}}"] = c.MotifCDDDetail ?? "—",

                // ── Rémunération ──────────────────────────────────────
                ["{{SalaireBase}}"] = c.SalaireBase.ToString("N0", Fr) + " FCFA",
                ["{{Sursalaire}}"] = (s?.Sursalaire ?? 0m) > 0
                    ? (s!.Sursalaire.ToString("N0", Fr) + " FCFA")
                    : "—",
                ["{{IndemniteLogement}}"] = c.IndemniteLogement.ToString("N0", Fr) + " FCFA",
                ["{{PrimeTransport}}"] = c.PrimeTransport.ToString("N0", Fr) + " FCFA",
                ["{{TotalBrut}}"] = c.TotalBrut.ToString("N0", Fr) + " FCFA",

                // ── Durée contrat (phrase complète) ───────────────────
                ["{{DureeContrat}}"] = (c.TypeContrat ?? TypeContrat.CDI) switch
                {
                    TypeContrat.CDI   => "indéterminée",
                    TypeContrat.CDD   => $"déterminée de {c.DureeMois?.ToString() ?? "?"} mois",
                    TypeContrat.Stage => $"stage de {c.DureeMois?.ToString() ?? "?"} mois",
                    _ => "indéterminée"
                },

                // ── Société ───────────────────────────────────────────
                ["{{RaisonSociale}}"] = company?.RaisonSociale ?? prm?.SignatoryName ?? "—",
                ["{{AdresseSociete}}"] = company?.Address ?? "—",
                ["{{SignataireNom}}"] = prm?.SignatoryName ?? prm?.SignatureName ?? "—",
                ["{{SignataireTitre}}"] = prm?.SignatoryTitle ?? "Directeur des Ressources Humaines",
            };
        }

        private static string GetBirthday(Salarie s, System.Globalization.CultureInfo fr)
        {
            try
            {
                var p = s as DevExpress.Persistent.BaseImpl.Person;
                if (p == null) return "—";
                var bd = p.Birthday;
                return bd == default ? "—" : bd.ToString("dd MMMM yyyy", fr);
            }
            catch { return "—"; }
        }

        private static string GetAdresse(Salarie s)
        {
            try
            {
                var p = s as DevExpress.Persistent.BaseImpl.Person;
                if (p == null) return "—";
                // Adresse via NumeroCNI champ Salarie directement
                // Person.Address1 est un objet complexe — on utilise ToString()
                var adr = p.Address1?.ToString();
                return string.IsNullOrWhiteSpace(adr) ? "—" : adr;
            }
            catch { return "—"; }
        }

        // ── Fusion XML (même pattern que AttestationTemplateService) ──
        private static byte[] FusionnerDocx(
            byte[] templateBytes,
            Dictionary<string, string> marqueurs)
        {
            // IMPORTANT : ToArray() doit être appelé DANS le using
            // WordprocessingDocument ferme le MemoryStream à sa disposition
            var ms = new MemoryStream();
            ms.Write(templateBytes, 0, templateBytes.Length);
            ms.Position = 0;

            byte[] result;

            using (var doc = WordprocessingDocument.Open(ms, isEditable: true))
            {
                var mainPart = doc.MainDocumentPart;
                if (mainPart == null) return templateBytes;

                // Lire le XML brut
                string xml;
                using (var reader = new StreamReader(mainPart.GetStream()))
                    xml = reader.ReadToEnd();

                // Nettoyer les marqueurs fragmentés par Word
                xml = NettoierMarqueursFragmentes(xml);

                // Remplacer les marqueurs
                foreach (var kvp in marqueurs)
                    xml = xml.Replace(kvp.Key, EchapperXml(kvp.Value ?? ""));

                // Réécrire le XML dans le document
                using (var writer = new StreamWriter(mainPart.GetStream(FileMode.Create)))
                    writer.Write(xml);

                doc.Save();

                // Capturer les bytes AVANT la fermeture du document
                result = ms.ToArray();
            }

            return result;
        }

        private static string NettoierMarqueursFragmentes(string xml)
            => Regex.Replace(xml, @"\{\{[^}]*\}\}", m =>
                Regex.Replace(m.Value, @"<[^>]+>", ""));

        private static string EchapperXml(string v)
        {
            if (string.IsNullOrEmpty(v)) return v;
            return v.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;");
        }
    }
}
