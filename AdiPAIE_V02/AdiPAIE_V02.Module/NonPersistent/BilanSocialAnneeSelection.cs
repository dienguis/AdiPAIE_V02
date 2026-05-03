// ============================================================
//  BilanSocialAnneeSelection.cs
//  Première étape du wizard Bilan Social annuel.
//  L'utilisateur choisit l'année avant que les données soient
//  chargées et figées dans le formulaire principal.
// ============================================================
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using System;

namespace AdiPAIE_V02.Module.NonPersistent
{
    [DomainComponent]
    [XafDisplayName("Bilan Social — Choix de l'année")]
    public class BilanSocialAnneeSelection : NonPersistentBaseObject
    {
        [XafDisplayName("Année du bilan")]
        [ToolTip("Année pour laquelle générer le Bilan Social. " +
                 "Par défaut l'année précédente (l'année courante n'étant pas " +
                 "encore close).")]
        [RuleRange("BilanSocial_Annee_Plage",
                   DefaultContexts.Save,
                   2000, 2100,
                   CustomMessageTemplate =
                       "L'année doit être comprise entre 2000 et 2100.")]
        public int Annee { get; set; } = DateTime.Today.Year - 1;
    }
}
