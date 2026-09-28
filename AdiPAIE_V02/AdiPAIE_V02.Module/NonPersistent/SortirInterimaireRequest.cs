using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.NonPersistent
{
    /// <summary>
    /// V1.10 - DTO popup pour l'action "Sortir intérimaire".
    ///
    /// Renseigné par le RH : Date de sortie + Motif + Observations.
    /// L'action déclenche en 1 clic :
    ///   1. Interimaire.Statut = Inactif (ou Blackliste si motif Blackliste)
    ///   2. Interimaire.DateSortieAgence + MotifSortie = valeurs saisies
    ///   3. Tous les ContratInterim EnCours -> Statut=Termine + DateFinReelle
    ///   4. MouvementInterimaire créé avec TypeMouvement approprié
    /// </summary>
    [DomainComponent]
    [XafDisplayName("Sortir intérimaire")]
    public class SortirInterimaireRequest : NonPersistentBaseObject
    {
        [XafDisplayName("Intérimaire")]
        [ModelDefault("AllowEdit", "False")]
        public string InterimaireDisplayName { get; set; }

        [XafDisplayName("Contrats en cours à terminer")]
        [ModelDefault("AllowEdit", "False")]
        public int NbContratsEnCours { get; set; }

        [XafDisplayName("Date de sortie")]
        [RuleRequiredField(DefaultContexts.Save, CustomMessageTemplate = "La date de sortie est obligatoire.")]
        public DateTime DateSortie { get; set; } = DateTime.Today;

        [XafDisplayName("Motif de sortie")]
        [RuleRequiredField(DefaultContexts.Save, CustomMessageTemplate = "Le motif de sortie est obligatoire.")]
        public MotifSortieInterim? Motif { get; set; }

        [XafDisplayName("Observations (optionnel)")]
        [Size(500)]
        public string Observations { get; set; }
    }
}
