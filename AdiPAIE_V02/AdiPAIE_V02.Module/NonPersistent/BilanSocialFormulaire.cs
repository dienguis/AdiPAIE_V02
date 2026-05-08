// ============================================================
//  BilanSocialFormulaire.cs
//  Formulaire non-persistant pour le Bilan Social annuel (DTSS Sénégal)
//  Conforme au Décret 2009-4181/MFPTEOP/DTSS du 18/12/2009
//
//  Les champs marqués [ReadOnly] sont pré-remplis automatiquement
//  depuis les données SunuPaie. Les autres sont saisis par le RH.
// ============================================================
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using System;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.NonPersistent
{
    [DomainComponent]
    [XafDisplayName("Bilan Social — Formulaire DTSS")]
    public class BilanSocialFormulaire : NonPersistentBaseObject
    {
        // ══════════════════════════════════════════════════════════════
        //  I. RENSEIGNEMENTS GÉNÉRAUX
        // ══════════════════════════════════════════════════════════════
        // Année figée : choisie au préalable dans BilanSocialAnneeSelection.
        // Cf. BilanSocialController (wizard 2 étapes) — empêche le bug
        // "je change l'année mais les valeurs ne se rechargent pas".
        [XafDisplayName("Année du bilan")]
        [ModelDefault("AllowEdit", "False")]
        public int Annee { get; set; } = DateTime.Today.Year - 1;

        [XafDisplayName("Raison sociale")]
        [ModelDefault("AllowEdit", "False")]
        public string RaisonSociale { get; set; }

        [XafDisplayName("Région")]
        [ModelDefault("AllowEdit", "False")]
        public string Region { get; set; } = "Dakar";

        [XafDisplayName("Département")]
        [ModelDefault("AllowEdit", "False")]
        public string Departement { get; set; } = "Dakar";

        [XafDisplayName("Commune / Arrondissement")]
        [ModelDefault("AllowEdit", "False")]
        public string Commune { get; set; }

        [XafDisplayName("Ville")]
        [ModelDefault("AllowEdit", "False")]
        public string VilleLocalite { get; set; } = "Dakar";

        [XafDisplayName("Téléphone")]
        [ModelDefault("AllowEdit", "False")]
        public string Telephone { get; set; }

        [XafDisplayName("Téléfax")]
        [ModelDefault("AllowEdit", "False")]
        public string Telefax { get; set; }

        [XafDisplayName("E-mail")]
        [ModelDefault("AllowEdit", "False")]
        public string EmailEntreprise { get; set; }

        [XafDisplayName("Boîte postale")]
        [ModelDefault("AllowEdit", "False")]
        public string BoitePostale { get; set; }

        [XafDisplayName("Site Internet")]
        [ModelDefault("AllowEdit", "False")]
        public string SiteInternet { get; set; }

        [XafDisplayName("Siège hors Sénégal")]
        [ModelDefault("AllowEdit", "False")]
        public string SiegeHorsSenegal { get; set; }

        [XafDisplayName("Nombre d'établissements")]
        [ModelDefault("AllowEdit", "False")]
        public int NombreEtablissements { get; set; }

        [XafDisplayName("NINEA")]
        [ModelDefault("AllowEdit", "False")]
        public string NINEA { get; set; }

        [XafDisplayName("Activité principale")]
        [ModelDefault("AllowEdit", "False")]
        public string ActivitePrincipale { get; set; }

        [XafDisplayName("Autres activités")]
        [ModelDefault("AllowEdit", "False")]
        public string AutresActivites { get; set; }

        [XafDisplayName("Forme juridique")]
        [ModelDefault("AllowEdit", "False")]
        public string FormeJuridique { get; set; }

        [XafDisplayName("Horaire de travail")]
        public string HoraireType { get; set; } = "Journée continue";

        // ══════════════════════════════════════════════════════════════
        //  II. EFFECTIF TOTAL — 21. Permanent (auto-calculé)
        // ══════════════════════════════════════════════════════════════
        [XafDisplayName("CDI année N")]
        [ModelDefault("AllowEdit", "False")]
        public int EffPerm_CDI_N { get; set; }

        [XafDisplayName("CDD année N")]
        [ModelDefault("AllowEdit", "False")]
        public int EffPerm_CDD_N { get; set; }

        [XafDisplayName("Apprentis/stagiaires année N")]
        public int EffPerm_Apprentis_N { get; set; }

        [XafDisplayName("CDI année N-1")]
        [ModelDefault("AllowEdit", "False")]
        public int EffPerm_CDI_P { get; set; }

        [XafDisplayName("CDD année N-1")]
        [ModelDefault("AllowEdit", "False")]
        public int EffPerm_CDD_P { get; set; }

        [XafDisplayName("Apprentis/stagiaires année N-1")]
        public int EffPerm_Apprentis_P { get; set; }

        // ── II.22-23 Effectif saisonnier / journalier (saisie manuelle) ──
        [XafDisplayName("Total saisonniers année N")]
        public int EffSaisonnier_N { get; set; }

        [XafDisplayName("Total saisonniers année N-1")]
        public int EffSaisonnier_P { get; set; }

        [XafDisplayName("Total journaliers année N")]
        public int EffJournalier_N { get; set; }

        [XafDisplayName("Total journaliers année N-1")]
        public int EffJournalier_P { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  III. RÉPARTITION DES EFFECTIFS (auto-calculé)
        // ══════════════════════════════════════════════════════════════
        // 32. Par statut — Année N
        [XafDisplayName("Ouvriers H (N)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Ouvriers_H_N { get; set; }
        [XafDisplayName("Ouvriers F (N)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Ouvriers_F_N { get; set; }
        [XafDisplayName("Employés H (N)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Employes_H_N { get; set; }
        [XafDisplayName("Employés F (N)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Employes_F_N { get; set; }
        [XafDisplayName("Agents maîtrise H (N)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Maitrise_H_N { get; set; }
        [XafDisplayName("Agents maîtrise F (N)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Maitrise_F_N { get; set; }
        [XafDisplayName("Cadres H (N)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Cadres_H_N { get; set; }
        [XafDisplayName("Cadres F (N)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Cadres_F_N { get; set; }

        // Par statut — Année N-1
        [XafDisplayName("Ouvriers H (N-1)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Ouvriers_H_P { get; set; }
        [XafDisplayName("Ouvriers F (N-1)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Ouvriers_F_P { get; set; }
        [XafDisplayName("Employés H (N-1)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Employes_H_P { get; set; }
        [XafDisplayName("Employés F (N-1)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Employes_F_P { get; set; }
        [XafDisplayName("Agents maîtrise H (N-1)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Maitrise_H_P { get; set; }
        [XafDisplayName("Agents maîtrise F (N-1)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Maitrise_F_P { get; set; }
        [XafDisplayName("Cadres H (N-1)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Cadres_H_P { get; set; }
        [XafDisplayName("Cadres F (N-1)")]
        [ModelDefault("AllowEdit", "False")]
        public int Stat_Cadres_F_P { get; set; }

        // 33. Par tranche d'âge — Année N (auto)
        [XafDisplayName("< 20 ans H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_Inf20_H_N { get; set; }
        [XafDisplayName("< 20 ans F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_Inf20_F_N { get; set; }
        [XafDisplayName("20-24 H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_20_24_H_N { get; set; }
        [XafDisplayName("20-24 F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_20_24_F_N { get; set; }
        [XafDisplayName("25-29 H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_25_29_H_N { get; set; }
        [XafDisplayName("25-29 F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_25_29_F_N { get; set; }
        [XafDisplayName("30-34 H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_30_34_H_N { get; set; }
        [XafDisplayName("30-34 F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_30_34_F_N { get; set; }
        [XafDisplayName("35-39 H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_35_39_H_N { get; set; }
        [XafDisplayName("35-39 F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_35_39_F_N { get; set; }
        [XafDisplayName("40-44 H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_40_44_H_N { get; set; }
        [XafDisplayName("40-44 F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_40_44_F_N { get; set; }
        [XafDisplayName("45-49 H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_45_49_H_N { get; set; }
        [XafDisplayName("45-49 F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_45_49_F_N { get; set; }
        [XafDisplayName("50-54 H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_50_54_H_N { get; set; }
        [XafDisplayName("50-54 F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_50_54_F_N { get; set; }
        [XafDisplayName("55-59 H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_55_59_H_N { get; set; }
        [XafDisplayName("55-59 F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_55_59_F_N { get; set; }
        [XafDisplayName("60+ H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_60Plus_H_N { get; set; }
        [XafDisplayName("60+ F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Age_60Plus_F_N { get; set; }

        // 34. Par nationalité (auto)
        [XafDisplayName("Sénégalais H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Nat_Sen_H_N { get; set; }
        [XafDisplayName("Sénégalais F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Nat_Sen_F_N { get; set; }
        [XafDisplayName("Étrangers H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Nat_Etr_H_N { get; set; }
        [XafDisplayName("Étrangers F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Nat_Etr_F_N { get; set; }

        // 35. Par ancienneté (auto)
        [XafDisplayName("< 1 an H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_Inf1_H_N { get; set; }
        [XafDisplayName("< 1 an F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_Inf1_F_N { get; set; }
        [XafDisplayName("1-4 ans H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_1_4_H_N { get; set; }
        [XafDisplayName("1-4 ans F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_1_4_F_N { get; set; }
        [XafDisplayName("5-9 ans H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_5_9_H_N { get; set; }
        [XafDisplayName("5-9 ans F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_5_9_F_N { get; set; }
        [XafDisplayName("10-14 ans H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_10_14_H_N { get; set; }
        [XafDisplayName("10-14 ans F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_10_14_F_N { get; set; }
        [XafDisplayName("15-19 ans H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_15_19_H_N { get; set; }
        [XafDisplayName("15-19 ans F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_15_19_F_N { get; set; }
        [XafDisplayName("20-24 ans H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_20_24_H_N { get; set; }
        [XafDisplayName("20-24 ans F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_20_24_F_N { get; set; }
        [XafDisplayName("25+ ans H (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_25Plus_H_N { get; set; }
        [XafDisplayName("25+ ans F (N)")] [ModelDefault("AllowEdit", "False")]
        public int Anc_25Plus_F_N { get; set; }

        // 37. Recrutements (auto)
        [XafDisplayName("Recrut. Ouvriers H")] [ModelDefault("AllowEdit", "False")]
        public int Rec_Ouvriers_H { get; set; }
        [XafDisplayName("Recrut. Ouvriers F")] [ModelDefault("AllowEdit", "False")]
        public int Rec_Ouvriers_F { get; set; }
        [XafDisplayName("Recrut. Employés H")] [ModelDefault("AllowEdit", "False")]
        public int Rec_Employes_H { get; set; }
        [XafDisplayName("Recrut. Employés F")] [ModelDefault("AllowEdit", "False")]
        public int Rec_Employes_F { get; set; }
        [XafDisplayName("Recrut. Maîtrise H")] [ModelDefault("AllowEdit", "False")]
        public int Rec_Maitrise_H { get; set; }
        [XafDisplayName("Recrut. Maîtrise F")] [ModelDefault("AllowEdit", "False")]
        public int Rec_Maitrise_F { get; set; }
        [XafDisplayName("Recrut. Cadres H")] [ModelDefault("AllowEdit", "False")]
        public int Rec_Cadres_H { get; set; }
        [XafDisplayName("Recrut. Cadres F")] [ModelDefault("AllowEdit", "False")]
        public int Rec_Cadres_F { get; set; }

        // 38. Départs (auto)
        [XafDisplayName("Départs Ouvriers H")] [ModelDefault("AllowEdit", "False")]
        public int Dep_Ouvriers_H { get; set; }
        [XafDisplayName("Départs Ouvriers F")] [ModelDefault("AllowEdit", "False")]
        public int Dep_Ouvriers_F { get; set; }
        [XafDisplayName("Départs Employés H")] [ModelDefault("AllowEdit", "False")]
        public int Dep_Employes_H { get; set; }
        [XafDisplayName("Départs Employés F")] [ModelDefault("AllowEdit", "False")]
        public int Dep_Employes_F { get; set; }
        [XafDisplayName("Départs Maîtrise H")] [ModelDefault("AllowEdit", "False")]
        public int Dep_Maitrise_H { get; set; }
        [XafDisplayName("Départs Maîtrise F")] [ModelDefault("AllowEdit", "False")]
        public int Dep_Maitrise_F { get; set; }
        [XafDisplayName("Départs Cadres H")] [ModelDefault("AllowEdit", "False")]
        public int Dep_Cadres_H { get; set; }
        [XafDisplayName("Départs Cadres F")] [ModelDefault("AllowEdit", "False")]
        public int Dep_Cadres_F { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  V. MASSE SALARIALE (auto-calculé)
        // ══════════════════════════════════════════════════════════════
        [XafDisplayName("Masse salariale brute année N (FCFA)")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal MasseSal_Total_N { get; set; }

        [XafDisplayName("Masse salariale brute année N-1 (FCFA)")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal MasseSal_Total_P { get; set; }

        // Charges salariales (auto)
        [XafDisplayName("Cotisations CSS (N)")] [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_CSS_N { get; set; }

        [XafDisplayName("Cotisations IPRES (N)")] [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_IPRES_N { get; set; }

        [XafDisplayName("CFCE (N)")] [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_CFCE_N { get; set; }

        [XafDisplayName("IR + TRIMF (N)")] [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Impots_N { get; set; }

        [XafDisplayName("Cotisations CSS (N-1)")] [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_CSS_P { get; set; }

        [XafDisplayName("Cotisations IPRES (N-1)")] [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_IPRES_P { get; set; }

        [XafDisplayName("CFCE (N-1)")] [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_CFCE_P { get; set; }

        [XafDisplayName("IR + TRIMF (N-1)")] [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Impots_P { get; set; }

        // Charges manuelles (IPM, assurances, etc.)
        [XafDisplayName("Cotisations IPM / Mutuelle (N)")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_IPM_N { get; set; }

        [XafDisplayName("Assurance décès (N)")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_AssurDeces_N { get; set; }

        [XafDisplayName("Assurance retraite complémentaire (N)")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_AssurRetraite_N { get; set; }

        [XafDisplayName("Cotisations IPM / Mutuelle (N-1)")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_IPM_P { get; set; }

        [XafDisplayName("Assurance décès (N-1)")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_AssurDeces_P { get; set; }

        [XafDisplayName("Assurance retraite complémentaire (N-1)")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Charges_AssurRetraite_P { get; set; }

        // Détail frais de personnel (53) — manuels
        [XafDisplayName("Eau (N)")] [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Frais_Eau_N { get; set; }
        [XafDisplayName("Électricité (N)")] [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Frais_Electricite_N { get; set; }
        [XafDisplayName("Habillement (N)")] [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Frais_Habillement_N { get; set; }
        [XafDisplayName("Médicaments (N)")] [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Frais_Medicaments_N { get; set; }
        [XafDisplayName("Formation (N)")] [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Frais_Formation_N { get; set; }

        [XafDisplayName("Eau (N-1)")] [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Frais_Eau_P { get; set; }
        [XafDisplayName("Électricité (N-1)")] [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Frais_Electricite_P { get; set; }
        [XafDisplayName("Habillement (N-1)")] [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Frais_Habillement_P { get; set; }
        [XafDisplayName("Médicaments (N-1)")] [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Frais_Medicaments_P { get; set; }
        [XafDisplayName("Formation (N-1)")] [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Frais_Formation_P { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  VI. HYGIÈNE, SÉCURITÉ ET SANTÉ (saisie manuelle)
        // ══════════════════════════════════════════════════════════════
        [XafDisplayName("Accidents avec arrêt (N)")]
        public int Accidents_AvecArret_N { get; set; }
        [XafDisplayName("Accidents sans arrêt (N)")]
        public int Accidents_SansArret_N { get; set; }
        [XafDisplayName("Décès AT (N)")]
        public int Accidents_Deces_N { get; set; }
        [XafDisplayName("Journées perdues AT (N)")]
        public int Accidents_JourneesPerdues_N { get; set; }

        [XafDisplayName("Accidents avec arrêt (N-1)")]
        public int Accidents_AvecArret_P { get; set; }
        [XafDisplayName("Accidents sans arrêt (N-1)")]
        public int Accidents_SansArret_P { get; set; }
        [XafDisplayName("Décès AT (N-1)")]
        public int Accidents_Deces_P { get; set; }
        [XafDisplayName("Journées perdues AT (N-1)")]
        public int Accidents_JourneesPerdues_P { get; set; }

        [XafDisplayName("Salaire personnel médical (N)")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Sante_SalaireMedical_N { get; set; }
        [XafDisplayName("Coût médicaments (N)")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Sante_Medicaments_N { get; set; }
        [XafDisplayName("Total dépenses santé (N)")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Sante_Total_N { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  VII. RELATIONS PROFESSIONNELLES (saisie manuelle)
        // ══════════════════════════════════════════════════════════════
        [XafDisplayName("Syndicats (noms, séparés par ;)")]
        public string Syndicats { get; set; }

        [XafDisplayName("Nombre de délégués du personnel")]
        public int NombreDelegues { get; set; }

        [XafDisplayName("Date dernières élections")]
        public DateTime? DateDernieresElections { get; set; }

        [XafDisplayName("Organisation patronale")]
        public string OrganisationPatronale { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  VIII. FONCTIONNEMENT DES ORGANES (saisie manuelle)
        // ══════════════════════════════════════════════════════════════
        [XafDisplayName("IPM / Mutuelle d'affiliation")]
        public string IPM_Nom { get; set; }

        [XafDisplayName("Comité hygiène et sécurité ?")]
        public bool ComiteHygiene { get; set; }

        [XafDisplayName("Service médecine du travail ?")]
        public bool ServiceMedecine { get; set; }

        [XafDisplayName("Date création service médecine")]
        public string DateCreationMedecine { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  X. ÉVOLUTION DE L'EMPLOI (saisie manuelle)
        // ══════════════════════════════════════════════════════════════
        [XafDisplayName("Prévision emploi année suivante")]
        public string PrevisionEmploi { get; set; } = "Augmentera";

        // ══════════════════════════════════════════════════════════════
        //  XI. AUTRES DONNÉES — Congés (auto-calculé)
        // ══════════════════════════════════════════════════════════════
        [XafDisplayName("Congés payés (jours, N)")] [ModelDefault("AllowEdit", "False")]
        public int Conges_Payes_N { get; set; }

        [XafDisplayName("Congés maternité (jours, N)")] [ModelDefault("AllowEdit", "False")]
        public int Conges_Maternite_N { get; set; }

        [XafDisplayName("Congés maladie (jours, N)")] [ModelDefault("AllowEdit", "False")]
        public int Conges_Maladie_N { get; set; }

        [XafDisplayName("Permissions (jours, N)")] [ModelDefault("AllowEdit", "False")]
        public int Conges_Permission_N { get; set; }

        [XafDisplayName("Absences non autorisées (jours, N)")]
        public int Conges_AbsNonAuto_N { get; set; }

        [XafDisplayName("Mises à pied (jours, N)")]
        public int Conges_MiseAPied_N { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  XI. Horaire de travail (saisie manuelle)
        // ══════════════════════════════════════════════════════════════
        [XafDisplayName("Heure début")]
        public string Horaire_Debut { get; set; } = "08:00";

        [XafDisplayName("Heure fin")]
        public string Horaire_Fin { get; set; } = "17:00";

        [XafDisplayName("Durée pause")]
        public string Horaire_Pause { get; set; } = "01:00";

        // ══════════════════════════════════════════════════════════════
        //  Lieu et date
        // ══════════════════════════════════════════════════════════════
        [XafDisplayName("Fait à")]
        public string FaitA { get; set; } = "Dakar";

        [XafDisplayName("Le")]
        public DateTime DateSignature { get; set; } = DateTime.Today;
    }
}
