// =============================================================================
//  FamilleAnnuaire.cs — V1.7 — Annuaire famille hiérarchique pour RH
//
//  Entité NON persistante affichée dans une ListView XAF groupée par matricule
//  du salarié. Une ligne par membre de la famille (salarié / conjoint / enfant).
//
//  Utilité :
//    - Vue d'ensemble RH : tous les salariés et leur famille en une seule liste
//    - Tri/filtrage/regroupement standard XAF
//    - Export Excel facile (DAF & RH peuvent télécharger l'annuaire complet)
//    - Calcul à la volée des charges fiscales (parts TRIMF)
//
//  Population :
//    - Service `FamilleAnnuaireService.LoaderAnnuaire(IObjectSpace)` qui
//      itère sur les Salariés actifs et matérialise N lignes virtuelles.
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using System;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.NonPersistent
{
    /// <summary>Type de membre dans l'annuaire famille.</summary>
    public enum TypeMembreFamille
    {
        [XafDisplayName("👤 Salarié")]
        Salarie = 0,

        [XafDisplayName("💍 Conjoint")]
        Conjoint = 1,

        [XafDisplayName("👶 Enfant")]
        Enfant = 2
    }

    [DomainComponent]
    [XafDisplayName("Annuaire famille")]
    [ImageName("BO_Salutation")]
    [DefaultProperty(nameof(NomComplet))]
    // V1.7 — Badges colorés par type de membre (palette unifiée projet)
    [Appearance("Famille_Type_Salarie",
        TargetItems = nameof(TypeMembre),
        Criteria = "TypeMembre = ##Enum#AdiPAIE_V02.Module.NonPersistent.TypeMembreFamille,Salarie#",
        BackColor = "LightSkyBlue", FontColor = "DarkBlue",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Famille_Type_Conjoint",
        TargetItems = nameof(TypeMembre),
        Criteria = "TypeMembre = ##Enum#AdiPAIE_V02.Module.NonPersistent.TypeMembreFamille,Conjoint#",
        BackColor = "Plum", FontColor = "Indigo",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Famille_Type_Enfant",
        TargetItems = nameof(TypeMembre),
        Criteria = "TypeMembre = ##Enum#AdiPAIE_V02.Module.NonPersistent.TypeMembreFamille,Enfant#",
        BackColor = "Moccasin", FontColor = "DarkOrange",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    // ACharge en vert si oui (alimente TRIMF)
    [Appearance("Famille_ACharge_Oui",
        TargetItems = nameof(ACharge),
        Criteria = "ACharge = True",
        BackColor = "PaleGreen", FontColor = "DarkGreen",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    public class FamilleAnnuaire : NonPersistentBaseObject
    {
        // ── Identification du groupe (Salarié) ─────────────────────────
        [XafDisplayName("Matricule")]
        [ToolTip("Matricule JDE du salarié titulaire (clé de regroupement).")]
        public string MatriculeSalarie { get; set; }

        [XafDisplayName("Salarié titulaire")]
        public string SalarieTitulaire { get; set; }

        // ── Membre (la ligne elle-même) ────────────────────────────────
        [XafDisplayName("Type")]
        public TypeMembreFamille TypeMembre { get; set; }

        [XafDisplayName("Nom complet")]
        public string NomComplet { get; set; }

        [XafDisplayName("Date naissance")]
        public DateTime? DateNaissance { get; set; }

        [XafDisplayName("Âge")]
        public int? Age
        {
            get
            {
                if (!DateNaissance.HasValue) return null;
                var today = DateTime.Today;
                var age = today.Year - DateNaissance.Value.Year;
                if (DateNaissance.Value.Date > today.AddYears(-age)) age--;
                return age < 0 ? 0 : age;
            }
        }

        [XafDisplayName("Sexe")]
        public string Sexe { get; set; }

        // ── Statut/Détail (variable selon type) ────────────────────────
        [XafDisplayName("Statut / Situation")]
        [ToolTip("Salarié: Actif/Inactif/Période d'essai • Conjoint: Marié/Divorcé/Veuf • Enfant: situation scolaire")]
        public string StatutDetail { get; set; }

        [XafDisplayName("À charge fiscale")]
        [ToolTip("Si oui, alimente le calcul des parts fiscales (TRIMF).")]
        public bool ACharge { get; set; }
    }
}
