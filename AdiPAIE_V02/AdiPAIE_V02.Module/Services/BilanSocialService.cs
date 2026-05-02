// ============================================================
//  BilanSocialService.cs
//  AdiPAIE V02 — Génération du Bilan Social annuel (DTSS Sénégal)
//  Conforme au formulaire réglementaire Décret 2009-4181/MFPTEOP/DTSS
//
//  Deux points d'entrée :
//   1. PreRemplir(formulaire, os)  → pré-calcule les données auto
//   2. Generer(formulaire)         → produit le .docx final
// ============================================================
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.NonPersistent;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class BilanSocialService
    {
        // ═════════════════════════════════════════════════════════════════
        //  1. PRÉ-REMPLISSAGE AUTOMATIQUE
        // ═════════════════════════════════════════════════════════════════
        public static void PreRemplir(BilanSocialFormulaire f, IObjectSpace os)
        {
            var session = ((XPObjectSpace)os).Session;
            var annee = f.Annee;
            var anneeP = annee - 1;

            // ── Entreprise ──────────────────────────────────────────────
            var company = new XPQuery<Company>(session).FirstOrDefault();
            if (company != null)
            {
                f.RaisonSociale = company.RaisonSociale ?? "";
                f.Telephone = company.Telephone ?? "";
                f.EmailEntreprise = company.Email ?? "";
                f.NINEA = company.NINEA ?? "";
                f.VilleLocalite = company.Ville ?? "Dakar";
                // Nouveaux champs auto-remplis depuis Company
                if (!string.IsNullOrWhiteSpace(company.Region))
                    f.Region = company.Region;
                if (!string.IsNullOrWhiteSpace(company.Commune))
                    f.Commune = company.Commune;
                if (!string.IsNullOrWhiteSpace(company.Telefax))
                    f.Telefax = company.Telefax;
                if (!string.IsNullOrWhiteSpace(company.BoitePostale))
                    f.BoitePostale = company.BoitePostale;
                if (!string.IsNullOrWhiteSpace(company.SiteInternet))
                    f.SiteInternet = company.SiteInternet;
                if (!string.IsNullOrWhiteSpace(company.FormeJuridique))
                    f.FormeJuridique = company.FormeJuridique;
                if (!string.IsNullOrWhiteSpace(company.ActivitePrincipale))
                    f.ActivitePrincipale = company.ActivitePrincipale;
                if (!string.IsNullOrWhiteSpace(company.AutresActivites))
                    f.AutresActivites = company.AutresActivites;
                if (company.NombreEtablissements > 0)
                    f.NombreEtablissements = company.NombreEtablissements;
                if (!string.IsNullOrWhiteSpace(company.SiegeHorsSenegal))
                    f.SiegeHorsSenegal = company.SiegeHorsSenegal;
                // Département géographique = Ville par défaut
                if (string.IsNullOrWhiteSpace(f.Departement))
                    f.Departement = company.Ville ?? "Dakar";
            }

            // ── Tous les salariés ────────────────────────────────────────
            var tousSalaries = new XPQuery<Salarie>(session).ToList();

            // Actifs au 31/12/N
            var date31Dec = new DateTime(annee, 12, 31);
            var actifs = tousSalaries.Where(s =>
                s.DateEmbauche <= date31Dec &&
                (s.DateSortie == default || s.DateSortie >= date31Dec)
            ).ToList();

            // Actifs au 31/12/N-1
            var date31DecP = new DateTime(anneeP, 12, 31);
            var actifsP = tousSalaries.Where(s =>
                s.DateEmbauche <= date31DecP &&
                (s.DateSortie == default || s.DateSortie >= date31DecP)
            ).ToList();

            // ── 21. Effectif permanent ──────────────────────────────────
            f.EffPerm_CDI_N = ContratCount(actifs, session, TypeContrat.CDI);
            f.EffPerm_CDD_N = ContratCount(actifs, session, TypeContrat.CDD);
            f.EffPerm_CDI_P = ContratCount(actifsP, session, TypeContrat.CDI);
            f.EffPerm_CDD_P = ContratCount(actifsP, session, TypeContrat.CDD);

            // ── 32. Par statut ──────────────────────────────────────────
            FillStatut(f, actifs, actifsP);

            // ── 33. Par tranche d'âge ────────────────────────────────────
            FillAge(f, actifs, annee);

            // ── 34. Par nationalité ──────────────────────────────────────
            f.Nat_Sen_H_N = actifs.Count(s => s.Sexe == Sexe.Masculin && EstSenegalais(s));
            f.Nat_Sen_F_N = actifs.Count(s => s.Sexe == Sexe.Feminin && EstSenegalais(s));
            f.Nat_Etr_H_N = actifs.Count(s => s.Sexe == Sexe.Masculin && !EstSenegalais(s));
            f.Nat_Etr_F_N = actifs.Count(s => s.Sexe == Sexe.Feminin && !EstSenegalais(s));

            // ── 35. Par ancienneté ────────────────────────────────────────
            FillAnciennete(f, actifs, annee);

            // ── 37. Recrutements ─────────────────────────────────────────
            var recrutes = tousSalaries.Where(s => s.DateEmbauche.Year == annee).ToList();
            FillRecrutDepart(f, recrutes, isRecrut: true);

            // ── 38. Départs ──────────────────────────────────────────────
            var partis = tousSalaries.Where(s =>
                s.DateSortie != default && s.DateSortie.Year == annee).ToList();
            FillRecrutDepart(f, partis, isRecrut: false);

            // ── V. Masse salariale ───────────────────────────────────────
            var bulletinsN = new XPQuery<Bulletin>(session)
                .Where(b => b.Annee == annee && b.Statut != BulletinStatut.Brouillon)
                .ToList();
            var bulletinsP = new XPQuery<Bulletin>(session)
                .Where(b => b.Annee == anneeP && b.Statut != BulletinStatut.Brouillon)
                .ToList();

            f.MasseSal_Total_N = bulletinsN.Sum(b => b.BrutFiscal);
            f.MasseSal_Total_P = bulletinsP.Sum(b => b.BrutFiscal);
            f.Charges_IPRES_N = SumLignesEmployeur(bulletinsN, session,
                RubriqueCanonique.IPRES_RG, RubriqueCanonique.IPRES_RC);
            f.Charges_CSS_N = SumLignesEmployeur(bulletinsN, session,
                RubriqueCanonique.CSS_AccidentTravail, RubriqueCanonique.CSS_AllocationFamiliale);
            f.Charges_CFCE_N = SumLignesEmployeur(bulletinsN, session,
                RubriqueCanonique.CFCE);
            f.Impots_N = bulletinsN.Sum(b => b.IR_Mois + b.TRIMF_Mois);

            f.Charges_IPRES_P = SumLignesEmployeur(bulletinsP, session,
                RubriqueCanonique.IPRES_RG, RubriqueCanonique.IPRES_RC);
            f.Charges_CSS_P = SumLignesEmployeur(bulletinsP, session,
                RubriqueCanonique.CSS_AccidentTravail, RubriqueCanonique.CSS_AllocationFamiliale);
            f.Charges_CFCE_P = SumLignesEmployeur(bulletinsP, session,
                RubriqueCanonique.CFCE);
            f.Impots_P = bulletinsP.Sum(b => b.IR_Mois + b.TRIMF_Mois);

            // ── XI. Congés ───────────────────────────────────────────────
            var conges = new XPQuery<CongeDemande>(session)
                .Where(c => c.DateDebut.Year == annee &&
                            c.Statut == CongeStatut.Accordee)
                .ToList();
            f.Conges_Payes_N = (int)conges
                .Where(c => c.Type?.Code == "CP" || c.Type?.Libelle?.Contains("payé") == true)
                .Sum(c => c.DureeJours);
            f.Conges_Maternite_N = (int)conges
                .Where(c => c.Type?.Code == "MAT" || c.Type?.Libelle?.Contains("maternité") == true)
                .Sum(c => c.DureeJours);
            f.Conges_Maladie_N = (int)conges
                .Where(c => c.Type?.Code == "MAL" || c.Type?.Libelle?.Contains("maladie") == true)
                .Sum(c => c.DureeJours);
            f.Conges_Permission_N = (int)conges
                .Where(c => c.Type?.Code == "PERM" || c.Type?.Libelle?.Contains("permission") == true)
                .Sum(c => c.DureeJours);
        }

        // ═════════════════════════════════════════════════════════════════
        //  2. GÉNÉRATION DU DOCUMENT WORD
        //     Si un template est fourni → remplacement des {{placeholders}}
        //     Sinon → génération depuis le code
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Génère le bilan social à partir du template Word stocké dans ParametresPaie.
        /// Si aucun template n'est configuré, génère le document depuis le code.
        /// </summary>
        public static byte[] Generer(BilanSocialFormulaire f, IObjectSpace os = null)
        {
            // Essayer de charger le template depuis ParametresPaie
            byte[] templateBytes = null;
            if (os != null)
            {
                var param = ParametresPaie.TryGet(os);
                if (param?.TemplateBilanSocial != null && param.TemplateBilanSocial.Size > 0)
                {
                    using var msT = new MemoryStream();
                    param.TemplateBilanSocial.SaveToStream(msT);
                    templateBytes = msT.ToArray();
                }
            }

            return templateBytes != null
                ? GenererDepuisTemplate(f, templateBytes)
                : GenererDepuisCode(f);
        }

        /// <summary>
        /// Ouvre le template .docx et remplace tous les {{PLACEHOLDER}} par les valeurs.
        /// Le RH peut modifier le template Word librement, tant qu'il conserve les placeholders.
        /// </summary>
        private static byte[] GenererDepuisTemplate(BilanSocialFormulaire f, byte[] templateBytes)
        {
            // Construire le dictionnaire de remplacement
            var replacements = BuildPlaceholders(f);

            using var ms = new MemoryStream();
            ms.Write(templateBytes, 0, templateBytes.Length);
            ms.Position = 0;

            using (var doc = WordprocessingDocument.Open(ms, true))
            {
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) throw new Exception("Template invalide : pas de contenu.");

                // Remplacer dans tout le document (paragraphes, tableaux, headers, footers)
                ReplaceInElement(body, replacements);

                // Headers & footers
                foreach (var hp in doc.MainDocumentPart.HeaderParts)
                    if (hp.Header != null)
                        ReplaceInElement(hp.Header, replacements);
                foreach (var fp in doc.MainDocumentPart.FooterParts)
                    if (fp.Footer != null)
                        ReplaceInElement(fp.Footer, replacements);

                doc.MainDocumentPart.Document.Save();
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Parcourt récursivement l'élément XML et remplace les {{PLACEHOLDER}}
        /// dans les nœuds texte. Gère le cas où Word fragmente le texte
        /// en plusieurs <w:r> dans un même paragraphe.
        /// </summary>
        private static void ReplaceInElement(OpenXmlElement element,
            Dictionary<string, string> replacements)
        {
            // Traiter chaque paragraphe : concaténer les runs, chercher les placeholders
            foreach (var para in element.Descendants<Paragraph>())
            {
                var runs = para.Elements<Run>().ToList();
                if (runs.Count == 0) continue;

                // Concaténer le texte de tous les runs
                var fullText = string.Concat(runs.SelectMany(r =>
                    r.Elements<Text>().Select(t => t.Text)));

                // Vérifier s'il y a des placeholders
                if (!fullText.Contains("{{")) continue;

                // Appliquer les remplacements
                var newText = fullText;
                foreach (var kv in replacements)
                    newText = newText.Replace(kv.Key, kv.Value);

                if (newText == fullText) continue;

                // Remplacer : vider tous les runs sauf le premier, mettre le texte complet dedans
                for (int i = 1; i < runs.Count; i++)
                    runs[i].Remove();

                var firstRun = runs[0];
                foreach (var t in firstRun.Elements<Text>().ToList())
                    t.Remove();
                firstRun.Append(new Text(newText)
                    { Space = SpaceProcessingModeValues.Preserve });
            }
        }

        /// <summary>
        /// Construit le dictionnaire {{PLACEHOLDER}} → valeur pour le formulaire.
        /// Le RH place ces codes dans son template Word, et le service les remplace.
        /// </summary>
        private static Dictionary<string, string> BuildPlaceholders(BilanSocialFormulaire f)
        {
            int an = f.Annee;
            int ap = an - 1;

            return new Dictionary<string, string>
            {
                // ── Général ──────────────────────────────────────────────
                ["{{ANNEE}}"] = an.ToString(),
                ["{{ANNEE_PRECEDENTE}}"] = ap.ToString(),
                ["{{RAISON_SOCIALE}}"] = f.RaisonSociale ?? "",
                ["{{REGION}}"] = f.Region ?? "",
                ["{{DEPARTEMENT}}"] = f.Departement ?? "",
                ["{{COMMUNE}}"] = f.Commune ?? "",
                ["{{VILLE}}"] = f.VilleLocalite ?? "",
                ["{{TELEPHONE}}"] = f.Telephone ?? "",
                ["{{TELEFAX}}"] = f.Telefax ?? "",
                ["{{EMAIL}}"] = f.EmailEntreprise ?? "",
                ["{{BOITE_POSTALE}}"] = f.BoitePostale ?? "",
                ["{{SITE_INTERNET}}"] = f.SiteInternet ?? "",
                ["{{SIEGE_HORS_SN}}"] = f.SiegeHorsSenegal ?? "",
                ["{{NB_ETABLISSEMENTS}}"] = f.NombreEtablissements.ToString(),
                ["{{NINEA}}"] = f.NINEA ?? "",
                ["{{ACTIVITE_PRINCIPALE}}"] = f.ActivitePrincipale ?? "",
                ["{{AUTRES_ACTIVITES}}"] = f.AutresActivites ?? "",
                ["{{FORME_JURIDIQUE}}"] = f.FormeJuridique ?? "",
                ["{{HORAIRE_TYPE}}"] = f.HoraireType ?? "",

                // ── II. Effectif permanent ──────────────────────────────
                ["{{EFF_CDI_N}}"] = N(f.EffPerm_CDI_N),
                ["{{EFF_CDD_N}}"] = N(f.EffPerm_CDD_N),
                ["{{EFF_APPRENTIS_N}}"] = N(f.EffPerm_Apprentis_N),
                ["{{EFF_CDI_P}}"] = N(f.EffPerm_CDI_P),
                ["{{EFF_CDD_P}}"] = N(f.EffPerm_CDD_P),
                ["{{EFF_APPRENTIS_P}}"] = N(f.EffPerm_Apprentis_P),
                ["{{EFF_SAISONNIER_N}}"] = N(f.EffSaisonnier_N),
                ["{{EFF_SAISONNIER_P}}"] = N(f.EffSaisonnier_P),
                ["{{EFF_JOURNALIER_N}}"] = N(f.EffJournalier_N),
                ["{{EFF_JOURNALIER_P}}"] = N(f.EffJournalier_P),

                // ── III. Répartition par statut ─────────────────────────
                ["{{STAT_OUV_H_N}}"] = N(f.Stat_Ouvriers_H_N),
                ["{{STAT_OUV_F_N}}"] = N(f.Stat_Ouvriers_F_N),
                ["{{STAT_EMP_H_N}}"] = N(f.Stat_Employes_H_N),
                ["{{STAT_EMP_F_N}}"] = N(f.Stat_Employes_F_N),
                ["{{STAT_MAI_H_N}}"] = N(f.Stat_Maitrise_H_N),
                ["{{STAT_MAI_F_N}}"] = N(f.Stat_Maitrise_F_N),
                ["{{STAT_CAD_H_N}}"] = N(f.Stat_Cadres_H_N),
                ["{{STAT_CAD_F_N}}"] = N(f.Stat_Cadres_F_N),
                ["{{STAT_OUV_H_P}}"] = N(f.Stat_Ouvriers_H_P),
                ["{{STAT_OUV_F_P}}"] = N(f.Stat_Ouvriers_F_P),
                ["{{STAT_EMP_H_P}}"] = N(f.Stat_Employes_H_P),
                ["{{STAT_EMP_F_P}}"] = N(f.Stat_Employes_F_P),
                ["{{STAT_MAI_H_P}}"] = N(f.Stat_Maitrise_H_P),
                ["{{STAT_MAI_F_P}}"] = N(f.Stat_Maitrise_F_P),
                ["{{STAT_CAD_H_P}}"] = N(f.Stat_Cadres_H_P),
                ["{{STAT_CAD_F_P}}"] = N(f.Stat_Cadres_F_P),
                ["{{STAT_TOT_H_N}}"] = N(f.Stat_Ouvriers_H_N + f.Stat_Employes_H_N + f.Stat_Maitrise_H_N + f.Stat_Cadres_H_N),
                ["{{STAT_TOT_F_N}}"] = N(f.Stat_Ouvriers_F_N + f.Stat_Employes_F_N + f.Stat_Maitrise_F_N + f.Stat_Cadres_F_N),
                ["{{STAT_TOT_H_P}}"] = N(f.Stat_Ouvriers_H_P + f.Stat_Employes_H_P + f.Stat_Maitrise_H_P + f.Stat_Cadres_H_P),
                ["{{STAT_TOT_F_P}}"] = N(f.Stat_Ouvriers_F_P + f.Stat_Employes_F_P + f.Stat_Maitrise_F_P + f.Stat_Cadres_F_P),

                // ── III. Par tranche d'âge ───────────────────────────────
                ["{{AGE_INF20_H}}"] = N(f.Age_Inf20_H_N), ["{{AGE_INF20_F}}"] = N(f.Age_Inf20_F_N),
                ["{{AGE_20_24_H}}"] = N(f.Age_20_24_H_N), ["{{AGE_20_24_F}}"] = N(f.Age_20_24_F_N),
                ["{{AGE_25_29_H}}"] = N(f.Age_25_29_H_N), ["{{AGE_25_29_F}}"] = N(f.Age_25_29_F_N),
                ["{{AGE_30_34_H}}"] = N(f.Age_30_34_H_N), ["{{AGE_30_34_F}}"] = N(f.Age_30_34_F_N),
                ["{{AGE_35_39_H}}"] = N(f.Age_35_39_H_N), ["{{AGE_35_39_F}}"] = N(f.Age_35_39_F_N),
                ["{{AGE_40_44_H}}"] = N(f.Age_40_44_H_N), ["{{AGE_40_44_F}}"] = N(f.Age_40_44_F_N),
                ["{{AGE_45_49_H}}"] = N(f.Age_45_49_H_N), ["{{AGE_45_49_F}}"] = N(f.Age_45_49_F_N),
                ["{{AGE_50_54_H}}"] = N(f.Age_50_54_H_N), ["{{AGE_50_54_F}}"] = N(f.Age_50_54_F_N),
                ["{{AGE_55_59_H}}"] = N(f.Age_55_59_H_N), ["{{AGE_55_59_F}}"] = N(f.Age_55_59_F_N),
                ["{{AGE_60P_H}}"] = N(f.Age_60Plus_H_N), ["{{AGE_60P_F}}"] = N(f.Age_60Plus_F_N),

                // ── III. Par nationalité ─────────────────────────────────
                ["{{NAT_SEN_H}}"] = N(f.Nat_Sen_H_N), ["{{NAT_SEN_F}}"] = N(f.Nat_Sen_F_N),
                ["{{NAT_ETR_H}}"] = N(f.Nat_Etr_H_N), ["{{NAT_ETR_F}}"] = N(f.Nat_Etr_F_N),

                // ── III. Par ancienneté ──────────────────────────────────
                ["{{ANC_INF1_H}}"] = N(f.Anc_Inf1_H_N), ["{{ANC_INF1_F}}"] = N(f.Anc_Inf1_F_N),
                ["{{ANC_1_4_H}}"] = N(f.Anc_1_4_H_N), ["{{ANC_1_4_F}}"] = N(f.Anc_1_4_F_N),
                ["{{ANC_5_9_H}}"] = N(f.Anc_5_9_H_N), ["{{ANC_5_9_F}}"] = N(f.Anc_5_9_F_N),
                ["{{ANC_10_14_H}}"] = N(f.Anc_10_14_H_N), ["{{ANC_10_14_F}}"] = N(f.Anc_10_14_F_N),
                ["{{ANC_15_19_H}}"] = N(f.Anc_15_19_H_N), ["{{ANC_15_19_F}}"] = N(f.Anc_15_19_F_N),
                ["{{ANC_20_24_H}}"] = N(f.Anc_20_24_H_N), ["{{ANC_20_24_F}}"] = N(f.Anc_20_24_F_N),
                ["{{ANC_25P_H}}"] = N(f.Anc_25Plus_H_N), ["{{ANC_25P_F}}"] = N(f.Anc_25Plus_F_N),

                // ── Recrutements ─────────────────────────────────────────
                ["{{REC_OUV_H}}"] = N(f.Rec_Ouvriers_H), ["{{REC_OUV_F}}"] = N(f.Rec_Ouvriers_F),
                ["{{REC_EMP_H}}"] = N(f.Rec_Employes_H), ["{{REC_EMP_F}}"] = N(f.Rec_Employes_F),
                ["{{REC_MAI_H}}"] = N(f.Rec_Maitrise_H), ["{{REC_MAI_F}}"] = N(f.Rec_Maitrise_F),
                ["{{REC_CAD_H}}"] = N(f.Rec_Cadres_H), ["{{REC_CAD_F}}"] = N(f.Rec_Cadres_F),

                // ── Départs ──────────────────────────────────────────────
                ["{{DEP_OUV_H}}"] = N(f.Dep_Ouvriers_H), ["{{DEP_OUV_F}}"] = N(f.Dep_Ouvriers_F),
                ["{{DEP_EMP_H}}"] = N(f.Dep_Employes_H), ["{{DEP_EMP_F}}"] = N(f.Dep_Employes_F),
                ["{{DEP_MAI_H}}"] = N(f.Dep_Maitrise_H), ["{{DEP_MAI_F}}"] = N(f.Dep_Maitrise_F),
                ["{{DEP_CAD_H}}"] = N(f.Dep_Cadres_H), ["{{DEP_CAD_F}}"] = N(f.Dep_Cadres_F),

                // ── V. Masse salariale ───────────────────────────────────
                ["{{MASSE_SAL_N}}"] = FM(f.MasseSal_Total_N),
                ["{{MASSE_SAL_P}}"] = FM(f.MasseSal_Total_P),
                ["{{CHARGES_CSS_N}}"] = FM(f.Charges_CSS_N),
                ["{{CHARGES_CSS_P}}"] = FM(f.Charges_CSS_P),
                ["{{CHARGES_IPRES_N}}"] = FM(f.Charges_IPRES_N),
                ["{{CHARGES_IPRES_P}}"] = FM(f.Charges_IPRES_P),
                ["{{CHARGES_CFCE_N}}"] = FM(f.Charges_CFCE_N),
                ["{{CHARGES_CFCE_P}}"] = FM(f.Charges_CFCE_P),
                ["{{CHARGES_IPM_N}}"] = FM(f.Charges_IPM_N),
                ["{{CHARGES_IPM_P}}"] = FM(f.Charges_IPM_P),
                ["{{CHARGES_ASSUR_DECES_N}}"] = FM(f.Charges_AssurDeces_N),
                ["{{CHARGES_ASSUR_DECES_P}}"] = FM(f.Charges_AssurDeces_P),
                ["{{CHARGES_ASSUR_RETRAITE_N}}"] = FM(f.Charges_AssurRetraite_N),
                ["{{CHARGES_ASSUR_RETRAITE_P}}"] = FM(f.Charges_AssurRetraite_P),
                ["{{IMPOTS_N}}"] = FM(f.Impots_N),
                ["{{IMPOTS_P}}"] = FM(f.Impots_P),
                ["{{FRAIS_EAU_N}}"] = FM(f.Frais_Eau_N),
                ["{{FRAIS_EAU_P}}"] = FM(f.Frais_Eau_P),
                ["{{FRAIS_ELEC_N}}"] = FM(f.Frais_Electricite_N),
                ["{{FRAIS_ELEC_P}}"] = FM(f.Frais_Electricite_P),
                ["{{FRAIS_HABIL_N}}"] = FM(f.Frais_Habillement_N),
                ["{{FRAIS_HABIL_P}}"] = FM(f.Frais_Habillement_P),
                ["{{FRAIS_MEDIC_N}}"] = FM(f.Frais_Medicaments_N),
                ["{{FRAIS_MEDIC_P}}"] = FM(f.Frais_Medicaments_P),
                ["{{FRAIS_FORM_N}}"] = FM(f.Frais_Formation_N),
                ["{{FRAIS_FORM_P}}"] = FM(f.Frais_Formation_P),

                // ── VI. Hygiène / sécurité ───────────────────────────────
                ["{{AT_AVEC_ARRET_N}}"] = N(f.Accidents_AvecArret_N),
                ["{{AT_SANS_ARRET_N}}"] = N(f.Accidents_SansArret_N),
                ["{{AT_DECES_N}}"] = N(f.Accidents_Deces_N),
                ["{{AT_JOURS_PERDUS_N}}"] = N(f.Accidents_JourneesPerdues_N),
                ["{{AT_AVEC_ARRET_P}}"] = N(f.Accidents_AvecArret_P),
                ["{{AT_SANS_ARRET_P}}"] = N(f.Accidents_SansArret_P),
                ["{{AT_DECES_P}}"] = N(f.Accidents_Deces_P),
                ["{{AT_JOURS_PERDUS_P}}"] = N(f.Accidents_JourneesPerdues_P),
                ["{{SANTE_MEDIC_N}}"] = FM(f.Sante_Medicaments_N),
                ["{{SANTE_SAL_MEDICAL_N}}"] = FM(f.Sante_SalaireMedical_N),
                ["{{SANTE_TOTAL_N}}"] = FM(f.Sante_Total_N),

                // ── VII. Relations professionnelles ──────────────────────
                ["{{SYNDICATS}}"] = f.Syndicats ?? "",
                ["{{NB_DELEGUES}}"] = N(f.NombreDelegues),
                ["{{DATE_ELECTIONS}}"] = f.DateDernieresElections?.ToString("dd/MM/yyyy") ?? "",
                ["{{ORG_PATRONALE}}"] = f.OrganisationPatronale ?? "",

                // ── VIII. Fonctionnement des organes ─────────────────────
                ["{{IPM_NOM}}"] = f.IPM_Nom ?? "",
                ["{{COMITE_HYGIENE}}"] = f.ComiteHygiene ? "Oui" : "Non",
                ["{{SERVICE_MEDECINE}}"] = f.ServiceMedecine ? "Oui" : "Non",
                ["{{DATE_CREATION_MEDECINE}}"] = f.DateCreationMedecine ?? "",

                // ── X. Évolution ─────────────────────────────────────────
                ["{{PREVISION_EMPLOI}}"] = f.PrevisionEmploi ?? "",

                // ── XI. Autres données ───────────────────────────────────
                ["{{HORAIRE_DEBUT}}"] = f.Horaire_Debut ?? "",
                ["{{HORAIRE_FIN}}"] = f.Horaire_Fin ?? "",
                ["{{HORAIRE_PAUSE}}"] = f.Horaire_Pause ?? "",
                ["{{CONGES_PAYES}}"] = N(f.Conges_Payes_N),
                ["{{CONGES_MATERNITE}}"] = N(f.Conges_Maternite_N),
                ["{{CONGES_MALADIE}}"] = N(f.Conges_Maladie_N),
                ["{{CONGES_PERMISSION}}"] = N(f.Conges_Permission_N),
                ["{{CONGES_MISE_A_PIED}}"] = N(f.Conges_MiseAPied_N),
                ["{{CONGES_ABS_NON_AUTO}}"] = N(f.Conges_AbsNonAuto_N),

                // ── Signature ────────────────────────────────────────────
                ["{{FAIT_A}}"] = f.FaitA ?? "Dakar",
                ["{{DATE_SIGNATURE}}"] = f.DateSignature.ToString("dd/MM/yyyy"),
            };
        }

        /// <summary>
        /// Génération complète depuis le code (fallback si pas de template).
        /// </summary>
        private static byte[] GenererDepuisCode(BilanSocialFormulaire f)
        {
            using var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                var body = new Body(BuildContent(f));

                // Page A4, marges 2cm
                body.Append(new SectionProperties(
                    new PageSize { Width = 11906, Height = 16838 },
                    new PageMargin
                    {
                        Top = 1134, Bottom = 1134,
                        Left = 1134, Right = 1134,
                        Header = 720, Footer = 720
                    }));

                mainPart.Document = new Document(body);
                AddStyles(mainPart);
            }
            return ms.ToArray();
        }

        // Ancien point d'entrée pour compatibilité
        public static byte[] Generer(int annee, IObjectSpace os)
        {
            var f = new BilanSocialFormulaire { Annee = annee };
            PreRemplir(f, os);
            return Generer(f);
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS DONNÉES
        // ═════════════════════════════════════════════════════════════════
        private static int ContratCount(List<Salarie> salaries, Session session, TypeContrat type)
        {
            var oids = salaries.Select(s => s.Oid).ToList();
            if (!oids.Any()) return 0;
            return new XPQuery<ContratSalarie>(session)
                .Where(c => oids.Contains(c.Salarie.Oid) &&
                            c.TypeContrat == type &&
                            c.Statut == ContratSalarieStatut.Actif)
                .Select(c => c.Salarie.Oid).Distinct().Count();
        }

        private static bool EstSenegalais(Salarie s)
            => string.IsNullOrWhiteSpace(s.Nationalite) ||
               s.Nationalite.ToUpperInvariant().Contains("SÉNÉ") ||
               s.Nationalite.ToUpperInvariant().Contains("SENE") ||
               s.Nationalite.ToUpperInvariant().Contains("SN");

        private static string GetStatutLabel(Salarie s)
        {
            var cat = s.Categories?.Intitule?.ToUpperInvariant() ?? "";
            if (cat.Contains("CADRE")) return "Cadres";
            if (cat.Contains("MAITRISE") || cat.Contains("MAÎTRISE")) return "Maitrise";
            if (cat.Contains("EMPLOYE") || cat.Contains("EMPLOYÉ")) return "Employes";
            return "Ouvriers";
        }

        private static void FillStatut(BilanSocialFormulaire f,
            List<Salarie> actifs, List<Salarie> actifsP)
        {
            foreach (var s in actifs)
            {
                var key = GetStatutLabel(s);
                bool h = s.Sexe == Sexe.Masculin;
                switch (key)
                {
                    case "Ouvriers": if (h) f.Stat_Ouvriers_H_N++; else f.Stat_Ouvriers_F_N++; break;
                    case "Employes": if (h) f.Stat_Employes_H_N++; else f.Stat_Employes_F_N++; break;
                    case "Maitrise": if (h) f.Stat_Maitrise_H_N++; else f.Stat_Maitrise_F_N++; break;
                    case "Cadres": if (h) f.Stat_Cadres_H_N++; else f.Stat_Cadres_F_N++; break;
                }
            }
            foreach (var s in actifsP)
            {
                var key = GetStatutLabel(s);
                bool h = s.Sexe == Sexe.Masculin;
                switch (key)
                {
                    case "Ouvriers": if (h) f.Stat_Ouvriers_H_P++; else f.Stat_Ouvriers_F_P++; break;
                    case "Employes": if (h) f.Stat_Employes_H_P++; else f.Stat_Employes_F_P++; break;
                    case "Maitrise": if (h) f.Stat_Maitrise_H_P++; else f.Stat_Maitrise_F_P++; break;
                    case "Cadres": if (h) f.Stat_Cadres_H_P++; else f.Stat_Cadres_F_P++; break;
                }
            }
        }

        private static void FillAge(BilanSocialFormulaire f, List<Salarie> actifs, int annee)
        {
            foreach (var s in actifs)
            {
                var bd = (s as DevExpress.Persistent.BaseImpl.Person)?.Birthday;
                if (bd == null) continue;
                int age = annee - bd.Value.Year;
                bool h = s.Sexe == Sexe.Masculin;

                if (age < 20) { if (h) f.Age_Inf20_H_N++; else f.Age_Inf20_F_N++; }
                else if (age <= 24) { if (h) f.Age_20_24_H_N++; else f.Age_20_24_F_N++; }
                else if (age <= 29) { if (h) f.Age_25_29_H_N++; else f.Age_25_29_F_N++; }
                else if (age <= 34) { if (h) f.Age_30_34_H_N++; else f.Age_30_34_F_N++; }
                else if (age <= 39) { if (h) f.Age_35_39_H_N++; else f.Age_35_39_F_N++; }
                else if (age <= 44) { if (h) f.Age_40_44_H_N++; else f.Age_40_44_F_N++; }
                else if (age <= 49) { if (h) f.Age_45_49_H_N++; else f.Age_45_49_F_N++; }
                else if (age <= 54) { if (h) f.Age_50_54_H_N++; else f.Age_50_54_F_N++; }
                else if (age <= 59) { if (h) f.Age_55_59_H_N++; else f.Age_55_59_F_N++; }
                else { if (h) f.Age_60Plus_H_N++; else f.Age_60Plus_F_N++; }
            }
        }

        private static void FillAnciennete(BilanSocialFormulaire f, List<Salarie> actifs, int annee)
        {
            var ref31Dec = new DateTime(annee, 12, 31);
            foreach (var s in actifs)
            {
                int anc = AncienneteHelper.NombreAnnee(s.DateEmbauche, ref31Dec);
                bool h = s.Sexe == Sexe.Masculin;

                if (anc < 1) { if (h) f.Anc_Inf1_H_N++; else f.Anc_Inf1_F_N++; }
                else if (anc <= 4) { if (h) f.Anc_1_4_H_N++; else f.Anc_1_4_F_N++; }
                else if (anc <= 9) { if (h) f.Anc_5_9_H_N++; else f.Anc_5_9_F_N++; }
                else if (anc <= 14) { if (h) f.Anc_10_14_H_N++; else f.Anc_10_14_F_N++; }
                else if (anc <= 19) { if (h) f.Anc_15_19_H_N++; else f.Anc_15_19_F_N++; }
                else if (anc <= 24) { if (h) f.Anc_20_24_H_N++; else f.Anc_20_24_F_N++; }
                else { if (h) f.Anc_25Plus_H_N++; else f.Anc_25Plus_F_N++; }
            }
        }

        private static void FillRecrutDepart(BilanSocialFormulaire f,
            List<Salarie> salaries, bool isRecrut)
        {
            foreach (var s in salaries)
            {
                var key = GetStatutLabel(s);
                bool h = s.Sexe == Sexe.Masculin;

                if (isRecrut)
                {
                    switch (key)
                    {
                        case "Ouvriers": if (h) f.Rec_Ouvriers_H++; else f.Rec_Ouvriers_F++; break;
                        case "Employes": if (h) f.Rec_Employes_H++; else f.Rec_Employes_F++; break;
                        case "Maitrise": if (h) f.Rec_Maitrise_H++; else f.Rec_Maitrise_F++; break;
                        case "Cadres": if (h) f.Rec_Cadres_H++; else f.Rec_Cadres_F++; break;
                    }
                }
                else
                {
                    switch (key)
                    {
                        case "Ouvriers": if (h) f.Dep_Ouvriers_H++; else f.Dep_Ouvriers_F++; break;
                        case "Employes": if (h) f.Dep_Employes_H++; else f.Dep_Employes_F++; break;
                        case "Maitrise": if (h) f.Dep_Maitrise_H++; else f.Dep_Maitrise_F++; break;
                        case "Cadres": if (h) f.Dep_Cadres_H++; else f.Dep_Cadres_F++; break;
                    }
                }
            }
        }

        private static decimal SumLignesEmployeur(
            List<Bulletin> bulletins, Session session, params RubriqueCanonique[] roles)
        {
            if (!bulletins.Any()) return 0;
            var bids = bulletins.Select(b => b.Oid).ToList();
            var rolesSet = new HashSet<RubriqueCanonique>(roles);
            return new XPQuery<BulletinLigne>(session)
                .Where(l => bids.Contains(l.Bulletin.Oid) &&
                            l.Rubrique != null &&
                            l.Rubrique.Canonique.HasValue &&
                            rolesSet.Contains(l.Rubrique.Canonique.Value))
                .Sum(l => l.MontantEmployeur);
        }

        // ═════════════════════════════════════════════════════════════════
        //  GÉNÉRATION DU DOCUMENT WORD — CONTENU
        // ═════════════════════════════════════════════════════════════════
        private static IEnumerable<OpenXmlElement> BuildContent(BilanSocialFormulaire f)
        {
            var el = new List<OpenXmlElement>();
            int an = f.Annee;
            int ap = an - 1;

            // ════════════════════════════════════════════════════════════
            //  PAGE DE TITRE (conforme DTSS)
            // ════════════════════════════════════════════════════════════
            el.Add(CenteredPara("ANNEXE 1 :", bold: true, size: 20, underline: true));
            el.Add(CenteredPara("BILAN SOCIAL DES ENTREPRISES", bold: true, size: 28, underline: true));
            el.Add(SpacerPara());
            el.Add(CenteredPara("MINISTÈRE DE LA FONCTION PUBLIQUE, DU TRAVAIL", bold: true, size: 20));
            el.Add(CenteredPara("DE L'EMPLOI ET DES ORGANISATIONS PROFESSIONNELLES", bold: true, size: 20));
            el.Add(CenteredPara("Direction du Travail et de la Sécurité Sociale", bold: true, size: 20));
            el.Add(CenteredPara("SERVICE DES STATISTIQUES DU TRAVAIL", italic: true, size: 18));
            el.Add(SpacerPara());
            el.Add(CenteredPara($"BILAN SOCIAL au 31 décembre {an}", bold: true, size: 24));
            el.Add(CenteredPara($"de l'établissement : {f.RaisonSociale}", bold: true, size: 22));
            el.Add(SpacerPara());

            // ════════════════════════════════════════════════════════════
            //  SOMMAIRE
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("SOMMAIRE DU BILAN"));
            var sommaire = new[]
            {
                "I. RENSEIGNEMENTS GÉNÉRAUX SUR L'ENTREPRISE",
                "II. EFFECTIF TOTAL DE L'ÉTABLISSEMENT",
                "III. RÉPARTITION DES EFFECTIFS",
                "IV. PROMOTIONS EFFECTUÉES",
                "V. RÉMUNÉRATIONS ET CHARGES ACCESSOIRES",
                "VI. HYGIÈNE, SÉCURITÉ ET SANTÉ",
                "VII. RELATIONS PROFESSIONNELLES",
                "VIII. FONCTIONNEMENT DES ORGANES",
                "IX. FORMATION",
                "X. ÉVOLUTION DE L'EMPLOI",
                "XI. AUTRES DONNÉES"
            };
            foreach (var s in sommaire)
                el.Add(TextPara(s, indent: 360));
            el.Add(PageBreak());

            // ════════════════════════════════════════════════════════════
            //  I. RENSEIGNEMENTS GÉNÉRAUX
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("I. RENSEIGNEMENTS GÉNÉRAUX SUR L'ENTREPRISE"));
            el.Add(InfoLigne("11. Raison sociale", f.RaisonSociale));
            el.Add(InfoLigne("12. Région", f.Region));
            el.Add(InfoLigne("    Département", f.Departement));
            el.Add(InfoLigne("    Commune", f.Commune));
            el.Add(InfoLigne("    Ville", f.VilleLocalite));
            el.Add(InfoLigne("    Téléphone", f.Telephone));
            el.Add(InfoLigne("    Téléfax", f.Telefax));
            el.Add(InfoLigne("    E-mail", f.EmailEntreprise));
            el.Add(InfoLigne("    Boîte postale", f.BoitePostale));
            el.Add(InfoLigne("    Site Internet", f.SiteInternet));
            el.Add(InfoLigne("13. Siège hors Sénégal", f.SiegeHorsSenegal));
            el.Add(InfoLigne("14. Nombre d'établissements", f.NombreEtablissements.ToString()));
            el.Add(InfoLigne("16. NINEA", f.NINEA));
            el.Add(InfoLigne("17. Activité principale", f.ActivitePrincipale));
            el.Add(InfoLigne("18. Autres activités", f.AutresActivites));
            el.Add(InfoLigne("19. Forme juridique", f.FormeJuridique));
            el.Add(InfoLigne("20. Horaire de travail", f.HoraireType));
            el.Add(PageBreak());

            // ════════════════════════════════════════════════════════════
            //  II. EFFECTIF TOTAL
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("II. EFFECTIF TOTAL DE L'ÉTABLISSEMENT"));
            el.Add(Sous2("21. Effectif permanent"));
            el.Add(Tableau(
                new[] { "", "CDD", "CDI", "Apprentis/Stagiaires" },
                new[]
                {
                    new[] { $"Année {an}", N(f.EffPerm_CDD_N), N(f.EffPerm_CDI_N), N(f.EffPerm_Apprentis_N) },
                    new[] { $"Année {ap}", N(f.EffPerm_CDD_P), N(f.EffPerm_CDI_P), N(f.EffPerm_Apprentis_P) },
                }));
            el.Add(SpacerPara());

            el.Add(Sous2("22. Effectif saisonnier"));
            el.Add(Tableau(
                new[] { "", $"Année {an}", $"Année {ap}" },
                new[] { new[] { "Total annuel", N(f.EffSaisonnier_N), N(f.EffSaisonnier_P) } }));
            el.Add(SpacerPara());

            el.Add(Sous2("23. Effectif journalier"));
            el.Add(Tableau(
                new[] { "", $"Année {an}", $"Année {ap}" },
                new[] { new[] { "Total annuel", N(f.EffJournalier_N), N(f.EffJournalier_P) } }));
            el.Add(PageBreak());

            // ════════════════════════════════════════════════════════════
            //  III. RÉPARTITION DES EFFECTIFS
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("III. RÉPARTITION DES EFFECTIFS"));

            // 32. Par statut
            el.Add(Sous2("32. Répartition par statut au 31 décembre"));
            el.Add(Tableau(
                new[] { "Statut", "Sexe", $"Année {an}", $"Année {ap}" },
                new[]
                {
                    new[] { "Ouvriers", "Hommes", N(f.Stat_Ouvriers_H_N), N(f.Stat_Ouvriers_H_P) },
                    new[] { "", "Femmes", N(f.Stat_Ouvriers_F_N), N(f.Stat_Ouvriers_F_P) },
                    new[] { "Employés", "Hommes", N(f.Stat_Employes_H_N), N(f.Stat_Employes_H_P) },
                    new[] { "", "Femmes", N(f.Stat_Employes_F_N), N(f.Stat_Employes_F_P) },
                    new[] { "Agents de maîtrise", "Hommes", N(f.Stat_Maitrise_H_N), N(f.Stat_Maitrise_H_P) },
                    new[] { "", "Femmes", N(f.Stat_Maitrise_F_N), N(f.Stat_Maitrise_F_P) },
                    new[] { "Cadres", "Hommes", N(f.Stat_Cadres_H_N), N(f.Stat_Cadres_H_P) },
                    new[] { "", "Femmes", N(f.Stat_Cadres_F_N), N(f.Stat_Cadres_F_P) },
                    new[] { "TOTAL", "Hommes",
                        N(f.Stat_Ouvriers_H_N + f.Stat_Employes_H_N + f.Stat_Maitrise_H_N + f.Stat_Cadres_H_N),
                        N(f.Stat_Ouvriers_H_P + f.Stat_Employes_H_P + f.Stat_Maitrise_H_P + f.Stat_Cadres_H_P) },
                    new[] { "", "Femmes",
                        N(f.Stat_Ouvriers_F_N + f.Stat_Employes_F_N + f.Stat_Maitrise_F_N + f.Stat_Cadres_F_N),
                        N(f.Stat_Ouvriers_F_P + f.Stat_Employes_F_P + f.Stat_Maitrise_F_P + f.Stat_Cadres_F_P) },
                }));
            el.Add(SpacerPara());

            // 33. Par âge
            el.Add(Sous2("33. Répartition par tranche d'âge au 31 décembre"));
            el.Add(Tableau(
                new[] { "Tranche", $"Hommes {an}", $"Femmes {an}" },
                new[]
                {
                    new[] { "Moins de 20 ans", N(f.Age_Inf20_H_N), N(f.Age_Inf20_F_N) },
                    new[] { "20 à 24 ans", N(f.Age_20_24_H_N), N(f.Age_20_24_F_N) },
                    new[] { "25 à 29 ans", N(f.Age_25_29_H_N), N(f.Age_25_29_F_N) },
                    new[] { "30 à 34 ans", N(f.Age_30_34_H_N), N(f.Age_30_34_F_N) },
                    new[] { "35 à 39 ans", N(f.Age_35_39_H_N), N(f.Age_35_39_F_N) },
                    new[] { "40 à 44 ans", N(f.Age_40_44_H_N), N(f.Age_40_44_F_N) },
                    new[] { "45 à 49 ans", N(f.Age_45_49_H_N), N(f.Age_45_49_F_N) },
                    new[] { "50 à 54 ans", N(f.Age_50_54_H_N), N(f.Age_50_54_F_N) },
                    new[] { "55 à 59 ans", N(f.Age_55_59_H_N), N(f.Age_55_59_F_N) },
                    new[] { "60 ans et plus", N(f.Age_60Plus_H_N), N(f.Age_60Plus_F_N) },
                    new[] { "TOTAL",
                        N(f.Age_Inf20_H_N + f.Age_20_24_H_N + f.Age_25_29_H_N + f.Age_30_34_H_N +
                          f.Age_35_39_H_N + f.Age_40_44_H_N + f.Age_45_49_H_N + f.Age_50_54_H_N +
                          f.Age_55_59_H_N + f.Age_60Plus_H_N),
                        N(f.Age_Inf20_F_N + f.Age_20_24_F_N + f.Age_25_29_F_N + f.Age_30_34_F_N +
                          f.Age_35_39_F_N + f.Age_40_44_F_N + f.Age_45_49_F_N + f.Age_50_54_F_N +
                          f.Age_55_59_F_N + f.Age_60Plus_F_N) },
                }));
            el.Add(SpacerPara());

            // 34. Par nationalité
            el.Add(Sous2("34. Répartition par nationalité au 31 décembre"));
            el.Add(Tableau(
                new[] { "Nationalité", $"Hommes {an}", $"Femmes {an}" },
                new[]
                {
                    new[] { "Sénégalais", N(f.Nat_Sen_H_N), N(f.Nat_Sen_F_N) },
                    new[] { "Étrangers", N(f.Nat_Etr_H_N), N(f.Nat_Etr_F_N) },
                    new[] { "TOTAL", N(f.Nat_Sen_H_N + f.Nat_Etr_H_N), N(f.Nat_Sen_F_N + f.Nat_Etr_F_N) },
                }));
            el.Add(SpacerPara());

            // 35. Par ancienneté
            el.Add(Sous2("35. Répartition par ancienneté au 31 décembre"));
            el.Add(Tableau(
                new[] { "Tranche", $"Hommes {an}", $"Femmes {an}" },
                new[]
                {
                    new[] { "Moins d'un an", N(f.Anc_Inf1_H_N), N(f.Anc_Inf1_F_N) },
                    new[] { "1 à 4 ans", N(f.Anc_1_4_H_N), N(f.Anc_1_4_F_N) },
                    new[] { "5 à 9 ans", N(f.Anc_5_9_H_N), N(f.Anc_5_9_F_N) },
                    new[] { "10 à 14 ans", N(f.Anc_10_14_H_N), N(f.Anc_10_14_F_N) },
                    new[] { "15 à 19 ans", N(f.Anc_15_19_H_N), N(f.Anc_15_19_F_N) },
                    new[] { "20 à 24 ans", N(f.Anc_20_24_H_N), N(f.Anc_20_24_F_N) },
                    new[] { "25 ans et plus", N(f.Anc_25Plus_H_N), N(f.Anc_25Plus_F_N) },
                }));
            el.Add(PageBreak());

            // ════════════════════════════════════════════════════════════
            //  37. Recrutements
            // ════════════════════════════════════════════════════════════
            el.Add(Sous2($"37. Recrutements au cours de l'année {an}"));
            el.Add(Tableau(
                new[] { "Statut", "Hommes", "Femmes", "Total" },
                new[]
                {
                    new[] { "Ouvriers", N(f.Rec_Ouvriers_H), N(f.Rec_Ouvriers_F), N(f.Rec_Ouvriers_H + f.Rec_Ouvriers_F) },
                    new[] { "Employés", N(f.Rec_Employes_H), N(f.Rec_Employes_F), N(f.Rec_Employes_H + f.Rec_Employes_F) },
                    new[] { "Agents de maîtrise", N(f.Rec_Maitrise_H), N(f.Rec_Maitrise_F), N(f.Rec_Maitrise_H + f.Rec_Maitrise_F) },
                    new[] { "Cadres", N(f.Rec_Cadres_H), N(f.Rec_Cadres_F), N(f.Rec_Cadres_H + f.Rec_Cadres_F) },
                    new[] { "TOTAL",
                        N(f.Rec_Ouvriers_H + f.Rec_Employes_H + f.Rec_Maitrise_H + f.Rec_Cadres_H),
                        N(f.Rec_Ouvriers_F + f.Rec_Employes_F + f.Rec_Maitrise_F + f.Rec_Cadres_F),
                        N(f.Rec_Ouvriers_H + f.Rec_Ouvriers_F + f.Rec_Employes_H + f.Rec_Employes_F +
                          f.Rec_Maitrise_H + f.Rec_Maitrise_F + f.Rec_Cadres_H + f.Rec_Cadres_F) },
                }));
            el.Add(SpacerPara());

            // 38. Départs
            el.Add(Sous2($"38. Départs au cours de l'année {an}"));
            el.Add(Tableau(
                new[] { "Statut", "Hommes", "Femmes", "Total" },
                new[]
                {
                    new[] { "Ouvriers", N(f.Dep_Ouvriers_H), N(f.Dep_Ouvriers_F), N(f.Dep_Ouvriers_H + f.Dep_Ouvriers_F) },
                    new[] { "Employés", N(f.Dep_Employes_H), N(f.Dep_Employes_F), N(f.Dep_Employes_H + f.Dep_Employes_F) },
                    new[] { "Agents de maîtrise", N(f.Dep_Maitrise_H), N(f.Dep_Maitrise_F), N(f.Dep_Maitrise_H + f.Dep_Maitrise_F) },
                    new[] { "Cadres", N(f.Dep_Cadres_H), N(f.Dep_Cadres_F), N(f.Dep_Cadres_H + f.Dep_Cadres_F) },
                    new[] { "TOTAL",
                        N(f.Dep_Ouvriers_H + f.Dep_Employes_H + f.Dep_Maitrise_H + f.Dep_Cadres_H),
                        N(f.Dep_Ouvriers_F + f.Dep_Employes_F + f.Dep_Maitrise_F + f.Dep_Cadres_F),
                        N(f.Dep_Ouvriers_H + f.Dep_Ouvriers_F + f.Dep_Employes_H + f.Dep_Employes_F +
                          f.Dep_Maitrise_H + f.Dep_Maitrise_F + f.Dep_Cadres_H + f.Dep_Cadres_F) },
                }));
            el.Add(SpacerPara());

            // ════════════════════════════════════════════════════════════
            //  IV. PROMOTIONS
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("IV. PROMOTIONS EFFECTUÉES"));
            el.Add(NotePara("→ À compléter manuellement (changements de catégorie, de statut)"));
            el.Add(PageBreak());

            // ════════════════════════════════════════════════════════════
            //  V. RÉMUNÉRATIONS ET CHARGES
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("V. RÉMUNÉRATIONS ET CHARGES ACCESSOIRES (en FCFA)"));

            el.Add(Sous2("51. Masses salariales brutes"));
            el.Add(Tableau(
                new[] { "", $"Année {an}", $"Année {ap}" },
                new[]
                {
                    new[] { "Montant total masses salariales brutes", FM(f.MasseSal_Total_N), FM(f.MasseSal_Total_P) },
                }));
            el.Add(SpacerPara());

            el.Add(Sous2("52. Charges salariales (coûts employeur)"));
            var totChargesN = f.Charges_CSS_N + f.Charges_IPRES_N + f.Charges_CFCE_N +
                              f.Charges_IPM_N + f.Charges_AssurDeces_N + f.Charges_AssurRetraite_N;
            var totChargesP = f.Charges_CSS_P + f.Charges_IPRES_P + f.Charges_CFCE_P +
                              f.Charges_IPM_P + f.Charges_AssurDeces_P + f.Charges_AssurRetraite_P;
            el.Add(Tableau(
                new[] { "Nature", $"Année {an}", $"Année {ap}" },
                new[]
                {
                    new[] { "Cotisations CSS", FM(f.Charges_CSS_N), FM(f.Charges_CSS_P) },
                    new[] { "Cotisations IPRES", FM(f.Charges_IPRES_N), FM(f.Charges_IPRES_P) },
                    new[] { "Cotisations IPM / Mutuelle", FM(f.Charges_IPM_N), FM(f.Charges_IPM_P) },
                    new[] { "CFCE", FM(f.Charges_CFCE_N), FM(f.Charges_CFCE_P) },
                    new[] { "Assurance décès", FM(f.Charges_AssurDeces_N), FM(f.Charges_AssurDeces_P) },
                    new[] { "Assurance retraite complémentaire", FM(f.Charges_AssurRetraite_N), FM(f.Charges_AssurRetraite_P) },
                    new[] { "TOTAL", FM(totChargesN), FM(totChargesP) },
                }));
            el.Add(SpacerPara());

            el.Add(Sous2("53. Détail des frais de personnel"));
            var totFraisN = f.MasseSal_Total_N + f.Charges_IPRES_N + f.Charges_CSS_N + f.Impots_N +
                            f.Frais_Eau_N + f.Frais_Electricite_N + f.Frais_Habillement_N +
                            f.Frais_Medicaments_N + f.Frais_Formation_N;
            var totFraisP = f.MasseSal_Total_P + f.Charges_IPRES_P + f.Charges_CSS_P + f.Impots_P +
                            f.Frais_Eau_P + f.Frais_Electricite_P + f.Frais_Habillement_P +
                            f.Frais_Medicaments_P + f.Frais_Formation_P;
            el.Add(Tableau(
                new[] { "Nature", $"Année {an}", $"Année {ap}" },
                new[]
                {
                    new[] { "Salaires (brut)", FM(f.MasseSal_Total_N), FM(f.MasseSal_Total_P) },
                    new[] { "Charges sociales (IPRES + CSS)", FM(f.Charges_IPRES_N + f.Charges_CSS_N), FM(f.Charges_IPRES_P + f.Charges_CSS_P) },
                    new[] { "Impôts (IR + TRIMF)", FM(f.Impots_N), FM(f.Impots_P) },
                    new[] { "Eau", FM(f.Frais_Eau_N), FM(f.Frais_Eau_P) },
                    new[] { "Électricité", FM(f.Frais_Electricite_N), FM(f.Frais_Electricite_P) },
                    new[] { "Habillement", FM(f.Frais_Habillement_N), FM(f.Frais_Habillement_P) },
                    new[] { "Médicaments", FM(f.Frais_Medicaments_N), FM(f.Frais_Medicaments_P) },
                    new[] { "Formation", FM(f.Frais_Formation_N), FM(f.Frais_Formation_P) },
                    new[] { "TOTAL", FM(totFraisN), FM(totFraisP) },
                }));
            el.Add(PageBreak());

            // ════════════════════════════════════════════════════════════
            //  VI. HYGIÈNE, SÉCURITÉ ET SANTÉ
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("VI. HYGIÈNE, SÉCURITÉ ET SANTÉ"));

            el.Add(Sous2("61. Accidents de travail"));
            el.Add(Tableau(
                new[] { "", $"Année {an}", $"Année {ap}" },
                new[]
                {
                    new[] { "Avec arrêt", N(f.Accidents_AvecArret_N), N(f.Accidents_AvecArret_P) },
                    new[] { "Sans arrêt", N(f.Accidents_SansArret_N), N(f.Accidents_SansArret_P) },
                    new[] { "Décès", N(f.Accidents_Deces_N), N(f.Accidents_Deces_P) },
                    new[] { "Total victimes",
                        N(f.Accidents_AvecArret_N + f.Accidents_SansArret_N + f.Accidents_Deces_N),
                        N(f.Accidents_AvecArret_P + f.Accidents_SansArret_P + f.Accidents_Deces_P) },
                    new[] { "Journées perdues", N(f.Accidents_JourneesPerdues_N), N(f.Accidents_JourneesPerdues_P) },
                }));
            el.Add(SpacerPara());

            el.Add(Sous2("62-63. Maladies professionnelles et pathologies"));
            el.Add(NotePara("→ À compléter manuellement"));
            el.Add(SpacerPara());

            el.Add(Sous2("65. Dépenses de santé"));
            el.Add(Tableau(
                new[] { "Nature", $"Coût {an} (FCFA)" },
                new[]
                {
                    new[] { "Médicaments", FM(f.Sante_Medicaments_N) },
                    new[] { "Salaire personnel médical", FM(f.Sante_SalaireMedical_N) },
                    new[] { "Total", FM(f.Sante_Total_N) },
                }));
            el.Add(PageBreak());

            // ════════════════════════════════════════════════════════════
            //  VII. RELATIONS PROFESSIONNELLES
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("VII. RELATIONS PROFESSIONNELLES"));
            el.Add(InfoLigne("Syndicats", f.Syndicats ?? "—"));
            el.Add(InfoLigne("Nombre de délégués", f.NombreDelegues.ToString()));
            el.Add(InfoLigne("Date dernières élections",
                f.DateDernieresElections?.ToString("dd/MM/yyyy") ?? "—"));
            el.Add(InfoLigne("Organisation patronale", f.OrganisationPatronale ?? "—"));
            el.Add(SpacerPara());

            // ════════════════════════════════════════════════════════════
            //  VIII. FONCTIONNEMENT DES ORGANES
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("VIII. FONCTIONNEMENT DES ORGANES"));
            el.Add(InfoLigne("IPM d'affiliation", f.IPM_Nom ?? "—"));
            el.Add(InfoLigne("Comité hygiène et sécurité", f.ComiteHygiene ? "Oui" : "Non"));
            el.Add(InfoLigne("Service médecine du travail", f.ServiceMedecine ? "Oui" : "Non"));
            if (f.ServiceMedecine)
                el.Add(InfoLigne("    Date création", f.DateCreationMedecine ?? "—"));
            el.Add(SpacerPara());

            // ════════════════════════════════════════════════════════════
            //  IX. FORMATION
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("IX. FORMATION"));
            el.Add(NotePara("→ Détail des formations : voir module SunuPaie > Plans de formation"));
            el.Add(SpacerPara());

            // ════════════════════════════════════════════════════════════
            //  X. ÉVOLUTION DE L'EMPLOI
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("X. ÉVOLUTION DE L'EMPLOI"));
            el.Add(InfoLigne("Prévision année suivante", f.PrevisionEmploi));
            el.Add(SpacerPara());

            // ════════════════════════════════════════════════════════════
            //  XI. AUTRES DONNÉES
            // ════════════════════════════════════════════════════════════
            el.Add(TitrePara("XI. AUTRES DONNÉES"));

            el.Add(Sous2("111. Horaire de travail"));
            el.Add(InfoLigne("Journée continue", $"de {f.Horaire_Debut} à {f.Horaire_Fin}"));
            el.Add(InfoLigne("Durée de la pause", f.Horaire_Pause));
            el.Add(SpacerPara());

            el.Add(Sous2("113. État des absences (nombre de jours)"));
            el.Add(Tableau(
                new[] { "Raison", $"Année {an}" },
                new[]
                {
                    new[] { "Congés payés", N(f.Conges_Payes_N) },
                    new[] { "Maternité", N(f.Conges_Maternite_N) },
                    new[] { "Maladie", N(f.Conges_Maladie_N) },
                    new[] { "Permission", N(f.Conges_Permission_N) },
                    new[] { "Mise à pied", N(f.Conges_MiseAPied_N) },
                    new[] { "Absences non autorisées", N(f.Conges_AbsNonAuto_N) },
                    new[] { "TOTAL", N(f.Conges_Payes_N + f.Conges_Maternite_N + f.Conges_Maladie_N +
                                       f.Conges_Permission_N + f.Conges_MiseAPied_N + f.Conges_AbsNonAuto_N) },
                }));
            el.Add(SpacerPara());

            // ════════════════════════════════════════════════════════════
            //  SIGNATURE
            // ════════════════════════════════════════════════════════════
            el.Add(PageBreak());
            el.Add(SpacerPara());
            el.Add(TextPara($"Fait à {f.FaitA}, le {f.DateSignature:dd/MM/yyyy}"));
            el.Add(SpacerPara());
            el.Add(CenteredPara("Signature et cachet de l'Employeur", bold: true, underline: true));

            return el;
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS WORD
        // ═════════════════════════════════════════════════════════════════
        private static string N(int n) => n.ToString("N0");
        private static string FM(decimal m) => m == 0 ? "—" : m.ToString("N0");

        private static Paragraph CenteredPara(string text, bool bold = false,
            int size = 24, bool underline = false, bool italic = false)
        {
            var rp = new RunProperties();
            if (bold) rp.Append(new Bold());
            if (italic) rp.Append(new Italic());
            if (underline) rp.Append(new Underline { Val = UnderlineValues.Single });
            rp.Append(new FontSize { Val = size.ToString() });

            return new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        }

        private static Paragraph TitrePara(string text) =>
            new Paragraph(
                new ParagraphProperties(
                    new ParagraphBorders(new BottomBorder
                    {
                        Val = BorderValues.Single, Size = 6,
                        Color = "0F6E56", Space = 1
                    }),
                    new SpacingBetweenLines { Before = "360", After = "120" }),
                new Run(
                    new RunProperties(new Bold(), new FontSize { Val = "26" },
                        new Color { Val = "0F6E56" }),
                    new Text(text)));

        private static Paragraph Sous2(string text) =>
            new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { Before = "200", After = "80" }),
                new Run(
                    new RunProperties(new Bold(), new FontSize { Val = "22" }),
                    new Text(text)));

        private static Paragraph InfoLigne(string label, string valeur) =>
            new Paragraph(
                new Run(new RunProperties(new Bold()),
                    new Text($"{label} : ") { Space = SpaceProcessingModeValues.Preserve }),
                new Run(new Text(valeur ?? "") { Space = SpaceProcessingModeValues.Preserve }));

        private static Paragraph TextPara(string text, int indent = 0)
        {
            var pp = new ParagraphProperties();
            if (indent > 0) pp.Append(new Indentation { Left = indent.ToString() });
            return new Paragraph(pp, new Run(new Text(text)));
        }

        private static Paragraph NotePara(string text) =>
            new Paragraph(
                new ParagraphProperties(new Indentation { Left = "720" }),
                new Run(
                    new RunProperties(new Color { Val = "888888" }, new Italic()),
                    new Text(text)));

        private static Paragraph SpacerPara() =>
            new Paragraph(new ParagraphProperties(
                new SpacingBetweenLines { After = "80" }));

        private static Paragraph PageBreak() =>
            new Paragraph(new Run(new Break { Type = BreakValues.Page }));

        private static Table Tableau(string[] headers, string[][] rows)
        {
            int colCount = headers.Length;
            int contentWidth = 9638; // A4 - 2cm margins en DXA
            int colW = contentWidth / colCount;

            var tbl = new Table(new TableProperties(
                new TableWidth { Width = contentWidth.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    MakeBorder<TopBorder>(), MakeBorder<BottomBorder>(),
                    MakeBorder<LeftBorder>(), MakeBorder<RightBorder>(),
                    MakeBorder<InsideHorizontalBorder>(), MakeBorder<InsideVerticalBorder>())));

            // Header row
            var hRow = new TableRow();
            foreach (var h in headers)
                hRow.Append(MakeCell(h, colW, headerBg: true));
            tbl.Append(hRow);

            // Data rows
            foreach (var row in rows)
            {
                var tRow = new TableRow();
                for (int i = 0; i < colCount; i++)
                    tRow.Append(MakeCell(i < row.Length ? row[i] : "", colW,
                        boldText: i < row.Length && row[i] == "TOTAL"));
                tbl.Append(tRow);
            }

            return tbl;
        }

        private static TableCell MakeCell(string text, int width,
            bool headerBg = false, bool boldText = false)
        {
            var rpr = new RunProperties(new FontSize { Val = "18" });
            if (headerBg || boldText) rpr.Append(new Bold());
            if (headerBg) rpr.Append(new Color { Val = "FFFFFF" });

            var para = new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { After = "0" }),
                new Run(rpr, new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve }));

            var cellProps = new TableCellProperties(
                new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableCellMargin(
                    new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }));

            if (headerBg)
                cellProps.Append(new Shading
                {
                    Val = ShadingPatternValues.Clear,
                    Fill = "0F6E56",
                    Color = "auto"
                });

            return new TableCell(cellProps, para);
        }

        private static T MakeBorder<T>() where T : BorderType, new()
        {
            var b = new T();
            b.Val = BorderValues.Single;
            b.Size = 4;
            b.Color = "CCCCCC";
            return b;
        }

        private static void AddStyles(MainDocumentPart mainPart)
        {
            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles(
                new DocDefaults(new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
                    new FontSize { Val = "20" }))));
        }
    }
}
