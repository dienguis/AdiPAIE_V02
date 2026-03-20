using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions, XafDisplayName("Type de congé")]
    [DefaultProperty(nameof(Libelle))]
    [NavigationItem("Paramétrage")]
    public class CongeType : BaseObject
    {
        public CongeType(Session s) : base(s) { }

        [RuleRequiredField, Size(20), Indexed(Unique = true)]
        public string Code { get => code; set => SetPropertyValue(nameof(Code), ref code, value?.Trim()); }
        string code;

        [RuleRequiredField, Size(120)]
        public string Libelle { get => lib; set => SetPropertyValue(nameof(Libelle), ref lib, value?.Trim()); }
        string lib;

        public CongeImpactSalaire ImpactSalaire { get => impact; set => SetPropertyValue(nameof(ImpactSalaire), ref impact, value); }
        CongeImpactSalaire impact = CongeImpactSalaire.Paye;

        // Pour ImpactSalaire = Partiel (maintien de salaire, ex: 50%)
        [ModelDefault("DisplayFormat", "P0"), ModelDefault("EditMask", "P0")]
        public decimal? TauxMaintienSalaire { get => maintien; set => SetPropertyValue(nameof(TauxMaintienSalaire), ref maintien, value); }
        decimal? maintien;

        public bool CompteEnJoursOuvrables { get => ouvrables; set => SetPropertyValue(nameof(CompteEnJoursOuvrables), ref ouvrables, value); }
        bool ouvrables = true;
    }

    //-----------------Jours ferie-----------------------


    [DefaultClassOptions, XafDisplayName("Jour férié")]
    [NavigationItem("Paramétrage")]
    public class JourFerie : BaseObject
    {
        public JourFerie(Session s) : base(s) { }

        public DateTime Date { get => d; set => SetPropertyValue(nameof(Date), ref d, value.Date); }
        DateTime d;

        [Size(120)]
        public string Libelle { get => lib; set => SetPropertyValue(nameof(Libelle), ref lib, value?.Trim()); }
        string lib;
    }

}
