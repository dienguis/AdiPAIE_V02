// =============================================================================
//  ProvisionConges.cs — V1.7 — Provision congés payés annuelle par salarié
//
//  Reproduit la règle métier ELTON (requête SQL ancienne paie) :
//
//    Base CCT Sénégal : 2 jours ouvrables / mois travaillé
//    Bonus ancienneté :
//      ≤ 10 ans : 0 jour
//      11-15 ans : +1 jour
//      16-20 ans : +2 jours
//      21-25 ans : +3 jours
//      > 25 ans  : +6 jours
//    Bonus mère (CCT) : 1 jour / enfant à charge < 14 ans (Sexe = Féminin)
//      → À VALIDER AVEC RH (formule exacte ELTON, plafond éventuel)
//
//    NbreJour total = (2 × NbreMois) + bonus ancienneté + bonus mère
//
//    Provision FCFA = NbreJour × (PovMensu / 22 jours ouvrés)
//      où PovMensu = Σ Gain (rubriques BrutFiscal=true) / 12
//
//  Granularité : 1 ligne par Salarié × Année.
// =============================================================================

using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.NonPersistent
{
    [DomainComponent]
    [XafDisplayName("Provision congés")]
    [ImageName("BO_Calculator")]
    [DefaultProperty(nameof(NomComplet))]
    // Couleurs : surligner les provisions élevées (>= 30 jours = ancienneté + mère)
    [Appearance("ProvCong_Eleve",
        TargetItems = nameof(NbreJourTotal),
        Criteria = "NbreJourTotal >= 30",
        BackColor = "PaleGreen", FontColor = "DarkGreen",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("ProvCong_Femme",
        TargetItems = nameof(BonusEnfants),
        Criteria = "BonusEnfants > 0",
        BackColor = "Pink", FontColor = "DeepPink",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    // Écart théorique vs réel — alerte si divergence > 1 jour
    [Appearance("ProvCong_Ecart_Anomalie",
        TargetItems = nameof(Ecart),
        Criteria = "Ecart > 1 OR Ecart < -1",
        BackColor = "LightCoral", FontColor = "DarkRed",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("ProvCong_Ecart_OK",
        TargetItems = nameof(Ecart),
        Criteria = "Ecart >= -1 AND Ecart <= 1",
        BackColor = "PaleGreen", FontColor = "DarkGreen")]
    public class ProvisionConges : NonPersistentBaseObject
    {
        // ── Identification ─────────────────────────────────────────────
        [XafDisplayName("Année")]
        public int Annee { get; set; }

        [XafDisplayName("Matricule")]
        public string Matricule { get; set; }

        [XafDisplayName("Nom complet")]
        public string NomComplet { get; set; }

        [XafDisplayName("Sexe")]
        public string Sexe { get; set; }

        [XafDisplayName("Ancienneté (ans)")]
        public int AncienneteAns { get; set; }

        [XafDisplayName("Nombre enfants")]
        public int NombreEnfants { get; set; }

        // ── Calcul de jours ────────────────────────────────────────────
        [XafDisplayName("Mois travaillés")]
        [ToolTip("Nombre de bulletins dans l'année.")]
        public int NbreMois { get; set; }

        [XafDisplayName("Base (2 j × mois)")]
        [ToolTip("Provision de base CCT Sénégal : 2 jours ouvrables par mois travaillé.")]
        public int BaseProvision { get; set; }

        [XafDisplayName("Bonus ancienneté")]
        [ToolTip("0/+1/+2/+3/+6 jours selon l'ancienneté (palier ELTON).")]
        public int BonusAnciennete { get; set; }

        [XafDisplayName("Bonus enfants (mères)")]
        [ToolTip("1 jour par enfant à charge < 14 ans (femmes uniquement). À valider avec RH.")]
        public int BonusEnfants { get; set; }

        [XafDisplayName("Total jours acquis")]
        [ModelDefault("DisplayFormat", "N0")]
        public int NbreJourTotal { get; set; }

        // ── Valorisation FCFA ──────────────────────────────────────────
        [XafDisplayName("Brut imposable cumul")]
        [ModelDefault("DisplayFormat", "N0")]
        [ToolTip("Σ des gains BrutFiscal=true sur l'année.")]
        public decimal CumulBrut { get; set; }

        [XafDisplayName("Brut moyen mensuel")]
        [ModelDefault("DisplayFormat", "N0")]
        [ToolTip("CumulBrut / 12. Sert de base de valorisation.")]
        public decimal BrutMensuelMoyen { get; set; }

        [XafDisplayName("Provision FCFA")]
        [ModelDefault("DisplayFormat", "N0")]
        [ToolTip("NbreJourTotal × (BrutMensuelMoyen / 22 jours ouvrés).")]
        public decimal ProvisionFCFA { get; set; }

        // ── Réconciliation théorique vs réel (V1.7 — issue redondance) ─
        [XafDisplayName("Solde réel acquis")]
        [ModelDefault("DisplayFormat", "N1")]
        [ToolTip("Cumul SoldeConge.JoursAcquis du salarié pour l'année " +
                 "(alimenté chaque mois par le cron SoldeCongeCalculService).")]
        public decimal SoldeReelAcquis { get; set; }

        [XafDisplayName("Écart (théorique − réel)")]
        [ModelDefault("DisplayFormat", "N1")]
        [ToolTip("Différence entre la provision théorique CCT et le solde réel acquis. " +
                 ">0 = cron en retard ou bonus ancienneté/enfants pas appliqué côté Solde. " +
                 "<0 = ajustement manuel positif sur SoldeConge.")]
        public decimal Ecart { get; set; }
    }
}
