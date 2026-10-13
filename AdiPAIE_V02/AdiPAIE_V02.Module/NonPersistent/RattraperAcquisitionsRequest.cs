using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;

namespace AdiPAIE_V02.Module.NonPersistent
{
    /// <summary>
    /// V1.11 - DTO popup pour l'action "Rattraper acquisitions mensuelles".
    ///
    /// Cas d'usage : bascule vers SunuPaie en cours d'annee. Les salaries
    /// n'ont jamais recu les acquisitions mensuelles automatiques des mois
    /// precedant la bascule. Resultat : les soldes sont a 0 (ou negatifs si
    /// des conges ont ete pris et saisis manuellement).
    ///
    /// Workflow : RH ouvre la liste "Soldes de conges", clique "Rattraper
    /// acquisitions", renseigne la plage de mois a rattraper (ex. janvier a
    /// septembre 2026), confirme. L'action boucle sur chaque mois et appelle
    /// AcquerirTousSalaries() de maniere idempotente (les mois deja traites
    /// sont ignores).
    ///
    /// Idempotent : relancer l'action deux fois ne cree rien de nouveau.
    /// </summary>
    [DomainComponent]
    [XafDisplayName("Rattraper acquisitions mensuelles")]
    public class RattraperAcquisitionsRequest : NonPersistentBaseObject
    {
        [XafDisplayName("Mois de debut")]
        [ToolTip("Premier mois a rattraper (ex. 1 pour janvier).")]
        [RuleRange(DefaultContexts.Save, 1, 12,
            CustomMessageTemplate = "Le mois de debut doit etre entre 1 et 12.")]
        public int MoisDebut { get; set; } = 1;

        [XafDisplayName("Annee de debut")]
        [RuleRange(DefaultContexts.Save, 2020, 2100,
            CustomMessageTemplate = "Annee invalide.")]
        public int AnneeDebut { get; set; } = DateTime.Today.Year;

        [XafDisplayName("Mois de fin")]
        [ToolTip("Dernier mois a rattraper (ex. 9 pour septembre). " +
                 "Par defaut : mois precedent le mois en cours.")]
        [RuleRange(DefaultContexts.Save, 1, 12,
            CustomMessageTemplate = "Le mois de fin doit etre entre 1 et 12.")]
        public int MoisFin { get; set; }
            = DateTime.Today.Month > 1 ? DateTime.Today.Month - 1 : 12;

        [XafDisplayName("Annee de fin")]
        [RuleRange(DefaultContexts.Save, 2020, 2100,
            CustomMessageTemplate = "Annee invalide.")]
        public int AnneeFin { get; set; }
            = DateTime.Today.Month > 1 ? DateTime.Today.Year : DateTime.Today.Year - 1;

        [XafDisplayName("Commentaire (optionnel)")]
        [ToolTip("Trace libre, apparaitra dans la reference des mouvements crees.")]
        [Size(200)]
        public string Commentaire { get; set; } = "Rattrapage historique";
    }
}
