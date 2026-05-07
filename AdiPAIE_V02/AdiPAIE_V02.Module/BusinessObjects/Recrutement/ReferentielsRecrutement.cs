// =============================================================================
//  ReferentielsRecrutement.cs — V1.4 (mai 2026)
//
//  4 entités référentielles courtes pour le Module Recrutement :
//    - MotifOuverturePoste  (Croissance, Remplacement, Création de fonction)
//    - SourceRecrutement    (LinkedIn, Recommandation, ANEM, Salons, etc.)
//    - MotifRefusCandidat   (côté entreprise : Manque expérience, etc.)
//    - MotifRefusOffre      (côté candidat : Rémunération insuf., etc.)
// =============================================================================

using System.ComponentModel;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace AdiPAIE_V02.Module.BusinessObjects.Recrutement
{
    /// <summary>
    /// Pourquoi un poste est ouvert au recrutement ?
    /// Ex : Croissance, Remplacement (départ d'un salarié), Création nouvelle fonction.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Motif d'ouverture poste")]
    [DefaultProperty(nameof(Libelle))]
    [ImageName("BO_Category")]
    [NavigationItem("GRH - Recrutement")]
    public class MotifOuverturePoste : BaseObject
    {
        public MotifOuverturePoste(Session s) : base(s) { Actif = true; }

        private string _code;
        [RuleRequiredField, RuleUniqueValue, Size(20)]
        [XafDisplayName("Code")]
        public string Code { get => _code; set => SetPropertyValue(nameof(Code), ref _code, value?.Trim()); }

        private string _libelle;
        [RuleRequiredField, Size(120)]
        [XafDisplayName("Libellé")]
        public string Libelle { get => _libelle; set => SetPropertyValue(nameof(Libelle), ref _libelle, value?.Trim()); }

        private bool _actif;
        [XafDisplayName("Actif")]
        public bool Actif { get => _actif; set => SetPropertyValue(nameof(Actif), ref _actif, value); }
    }

    /// <summary>
    /// Source par laquelle un candidat a postulé.
    /// Ex : LinkedIn, Recommandation, ANEM (Agence Nationale de l'Emploi), Salons emploi, Site corporate.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Source de recrutement")]
    [DefaultProperty(nameof(Libelle))]
    [ImageName("BO_Sale_Item")]
    [NavigationItem("GRH - Recrutement")]
    public class SourceRecrutement : BaseObject
    {
        public SourceRecrutement(Session s) : base(s) { Actif = true; }

        private string _code;
        [RuleRequiredField, RuleUniqueValue, Size(20)]
        [XafDisplayName("Code")]
        public string Code { get => _code; set => SetPropertyValue(nameof(Code), ref _code, value?.Trim()); }

        private string _libelle;
        [RuleRequiredField, Size(120)]
        [XafDisplayName("Libellé")]
        public string Libelle { get => _libelle; set => SetPropertyValue(nameof(Libelle), ref _libelle, value?.Trim()); }

        private decimal _coutMoyenParCandidat;
        [XafDisplayName("Coût moyen / candidat (FCFA)")]
        [ToolTip("Coût attribué à cette source par candidat sourcé (sert au calcul Coût Moyen Recrutement).")]
        public decimal CoutMoyenParCandidat
        {
            get => _coutMoyenParCandidat;
            set => SetPropertyValue(nameof(CoutMoyenParCandidat), ref _coutMoyenParCandidat, value);
        }

        private bool _actif;
        [XafDisplayName("Actif")]
        public bool Actif { get => _actif; set => SetPropertyValue(nameof(Actif), ref _actif, value); }
    }

    /// <summary>
    /// Motif de refus d'un candidat par l'entreprise.
    /// Ex : Manque d'expérience, Habite trop loin, Prétentions trop élevées, Entretien non concluant.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Motif refus candidat")]
    [DefaultProperty(nameof(Libelle))]
    [ImageName("Action_Close")]
    [NavigationItem("GRH - Recrutement")]
    public class MotifRefusCandidat : BaseObject
    {
        public MotifRefusCandidat(Session s) : base(s) { Actif = true; }

        private string _code;
        [RuleRequiredField, RuleUniqueValue, Size(20)]
        [XafDisplayName("Code")]
        public string Code { get => _code; set => SetPropertyValue(nameof(Code), ref _code, value?.Trim()); }

        private string _libelle;
        [RuleRequiredField, Size(150)]
        [XafDisplayName("Libellé")]
        public string Libelle { get => _libelle; set => SetPropertyValue(nameof(Libelle), ref _libelle, value?.Trim()); }

        private bool _actif;
        [XafDisplayName("Actif")]
        public bool Actif { get => _actif; set => SetPropertyValue(nameof(Actif), ref _actif, value); }
    }

    /// <summary>
    /// Motif de refus d'une offre d'emploi PAR LE CANDIDAT.
    /// Ex : Rémunération insuffisante, Absence d'opportunités d'évolution, Surqualifié, Préfère autre offre.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Motif refus offre")]
    [DefaultProperty(nameof(Libelle))]
    [ImageName("Action_Close")]
    [NavigationItem("GRH - Recrutement")]
    public class MotifRefusOffre : BaseObject
    {
        public MotifRefusOffre(Session s) : base(s) { Actif = true; }

        private string _code;
        [RuleRequiredField, RuleUniqueValue, Size(20)]
        [XafDisplayName("Code")]
        public string Code { get => _code; set => SetPropertyValue(nameof(Code), ref _code, value?.Trim()); }

        private string _libelle;
        [RuleRequiredField, Size(150)]
        [XafDisplayName("Libellé")]
        public string Libelle { get => _libelle; set => SetPropertyValue(nameof(Libelle), ref _libelle, value?.Trim()); }

        private bool _actif;
        [XafDisplayName("Actif")]
        public bool Actif { get => _actif; set => SetPropertyValue(nameof(Actif), ref _actif, value); }
    }
}
