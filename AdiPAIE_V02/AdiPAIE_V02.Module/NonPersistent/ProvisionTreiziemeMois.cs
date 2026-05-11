// =============================================================================
//  ProvisionTreiziemeMois.cs — V1.7.2c
//
//  Vue COMPTABLE DAF : provision mensuelle du 13ième mois.
//
//  Règle métier ELTON (cf. grille RH question 9) :
//    « Provision mensuelle (1/12) sur le brut récurrent du mois en cours »
//
//  Chaque ligne représente (Salarié × Mois × Année) :
//    - BrutRecurrent = somme des éléments récurrents du bulletin
//    - ProvisionDuMois = BrutRecurrent / 12
//    - CumulProvisionsAnnee = somme des provisions depuis janvier
//    - MontantFinal13ieme = montant intégré au bulletin de décembre (si dispo)
//    - Ecart = MontantFinal - CumulProvisions (si décembre intégré, sinon null)
//
//  Granularité : 1 ligne par Salarié × Mois.
//  L'utilisateur filtre dans la ListView XAF (par année, par salarié, etc.).
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
    [XafDisplayName("Provision 13ième mois")]
    [ImageName("BO_Calculator")]
    [DefaultProperty(nameof(NomComplet))]
    // Surligner le mois de décembre (intégration au bulletin)
    [Appearance("ProvM13_Decembre",
        TargetItems = nameof(Mois),
        Criteria = "Mois = 12",
        BackColor = "PaleGreen", FontColor = "DarkGreen",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    // Alerte sur écart théorique vs réel (si décembre intégré)
    [Appearance("ProvM13_Ecart_Anomalie",
        TargetItems = nameof(Ecart),
        Criteria = "Not IsNull(Ecart) AND (Ecart > 1000 OR Ecart < -1000)",
        BackColor = "LightCoral", FontColor = "DarkRed",
        FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("ProvM13_Ecart_OK",
        TargetItems = nameof(Ecart),
        Criteria = "Not IsNull(Ecart) AND Ecart >= -1000 AND Ecart <= 1000",
        BackColor = "PaleGreen", FontColor = "DarkGreen")]
    public class ProvisionTreiziemeMois : NonPersistentBaseObject
    {
        // ── Identification ─────────────────────────────────────────────
        [XafDisplayName("Année")]
        [ModelDefault("DisplayFormat", "{0:####}")]
        public int Annee { get; set; }

        [XafDisplayName("Mois")]
        [ToolTip("Mois civil 1-12.")]
        public int Mois { get; set; }

        [XafDisplayName("Matricule")]
        public string Matricule { get; set; }

        [XafDisplayName("Nom complet")]
        public string NomComplet { get; set; }

        [XafDisplayName("Département")]
        public string Departement { get; set; }

        // ── Calcul ─────────────────────────────────────────────────────
        [XafDisplayName("Brut récurrent du mois")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ToolTip("Somme des gains récurrents du bulletin du mois " +
                 "(SB + Sursalaire + Indemnités fixes + Ancienneté, " +
                 "hors congés et exceptionnels).")]
        public decimal BrutRecurrent { get; set; }

        [XafDisplayName("Provision du mois (1/12)")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ToolTip("= Brut récurrent du mois / 12. Charge mensuelle à provisionner.")]
        public decimal ProvisionDuMois { get; set; }

        [XafDisplayName("Cumul provisions année")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ToolTip("Somme des provisions de janvier jusqu'à ce mois inclus.")]
        public decimal CumulProvisionsAnnee { get; set; }

        // ── Réalisé (à partir de décembre) ─────────────────────────────
        [XafDisplayName("13ième effectif (déc.)")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ToolTip("Montant brut 13ième effectivement intégré au bulletin " +
                 "de décembre. Null tant que le 13ième n'a pas été " +
                 "calculé/intégré.")]
        public decimal? MontantFinal13ieme { get; set; }

        [XafDisplayName("Écart")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        [ToolTip("= 13ième effectif - Cumul provisions année. " +
                 "Si > 0 : la provision a sous-estimé (variations salaire). " +
                 "Si < 0 : la provision a surestimé. " +
                 "Idéalement proche de 0. Disponible uniquement en décembre.")]
        public decimal? Ecart { get; set; }
    }
}
