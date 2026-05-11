// =============================================================================
//  RapportGratification.cs — V1.7.2e
//
//  Vue de REPORTING a posteriori des gratifications versées.
//  Agrégat (Salarié × Année) avec montants cumulés et compteur.
//
//  Cf. clarification 5 RH ELTON (option B) : « Pas de provision en amont,
//  reporting a posteriori des gratifications versées sur l'année. »
//
//  Granularité : 1 ligne par (Salarié × Année) ayant au moins une gratification
//  intégrée ou payée.
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
    [XafDisplayName("Rapport Gratifications")]
    [ImageName("BO_Report")]
    [DefaultProperty(nameof(NomComplet))]
    // Surligner les montants élevés (> 1 M FCFA cumulé sur l'année)
    [Appearance("Rapp_Gratif_Eleve",
        TargetItems = nameof(MontantTotalAnnee),
        Criteria = "MontantTotalAnnee >= 1000000",
        BackColor = "PaleGreen", FontColor = "DarkGreen",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    // Salariés ayant reçu plusieurs gratifications
    [Appearance("Rapp_Gratif_Multiples",
        TargetItems = nameof(NombreGratifications),
        Criteria = "NombreGratifications >= 3",
        BackColor = "Moccasin", FontColor = "DarkOrange",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    public class RapportGratification : NonPersistentBaseObject
    {
        // ── Identification ──────────────────────────────────────────
        [XafDisplayName("Année")]
        [ModelDefault("DisplayFormat", "{0:####}")]
        public int Annee { get; set; }

        [XafDisplayName("Matricule")]
        public string Matricule { get; set; }

        [XafDisplayName("Nom complet")]
        public string NomComplet { get; set; }

        [XafDisplayName("Département")]
        public string Departement { get; set; }

        [XafDisplayName("Fonction")]
        public string Fonction { get; set; }

        // ── Indicateurs annuels ─────────────────────────────────────
        [XafDisplayName("Nb gratifications")]
        [ToolTip("Nombre de gratifications intégrées au bulletin (statut " +
                 "IntegreeBulletin ou Payee) sur l'année.")]
        public int NombreGratifications { get; set; }

        [XafDisplayName("Montant total année")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ToolTip("Cumul brut versé sur l'année (somme des Gratification.MontantCalcule).")]
        public decimal MontantTotalAnnee { get; set; }

        [XafDisplayName("Multiplicateur moyen")]
        [ModelDefault("DisplayFormat", "{0:N2}")]
        [ToolTip("Moyenne des multiplicateurs utilisés (hors forfaits).")]
        public decimal MultiplicateurMoyen { get; set; }

        [XafDisplayName("Dernier mois versement")]
        [ToolTip("Mois (1-12) de la dernière gratification intégrée.")]
        public int DernierMoisVersement { get; set; }

        [XafDisplayName("Total brut récurrent annuel")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ToolTip("Cumul brut récurrent du salarié sur l'année — base de " +
                 "comparaison pour évaluer le poids relatif des gratifications.")]
        public decimal BrutRecurrentAnnuel { get; set; }

        [XafDisplayName("% sur brut annuel")]
        [ModelDefault("DisplayFormat", "{0:N1} %")]
        [ToolTip("Poids des gratifications versées sur le cumul brut récurrent " +
                 "annuel. Indicateur de générosité relative.")]
        public decimal PourcentageSurBrutAnnuel { get; set; }
    }
}
