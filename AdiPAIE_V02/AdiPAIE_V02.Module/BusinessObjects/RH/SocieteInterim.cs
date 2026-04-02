using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    [DefaultClassOptions]
    [XafDisplayName("Societe d'interim")]
    [DefaultProperty(nameof(RaisonSociale))]
   // [NavigationItem("GRH - Interimaires")]
    public class SocieteInterim : BaseObject
    {
        public SocieteInterim(Session session) : base(session) { }

        [RuleRequiredField]
        [Size(200)]
        [XafDisplayName("Raison sociale")]
        public string RaisonSociale { get => rs; set => SetPropertyValue(nameof(RaisonSociale), ref rs, value?.Trim()); }
        string rs;

        [Size(100)]
        [XafDisplayName("Contact")]
        public string Contact { get => c; set => SetPropertyValue(nameof(Contact), ref c, value?.Trim()); }
        string c;

        [Size(100)]
        [XafDisplayName("Email")]
        public string Email { get => e; set => SetPropertyValue(nameof(Email), ref e, value?.Trim()); }
        string e;

        [Size(20)]
        [XafDisplayName("Telephone")]
        public string Telephone { get => t; set => SetPropertyValue(nameof(Telephone), ref t, value?.Trim()); }
        string t;

        [XafDisplayName("Active")]
        public bool Actif { get => a; set => SetPropertyValue(nameof(Actif), ref a, value); }
        bool a = true;

        [Association("SocieteInterim-Interimaires")]
        public XPCollection<Interimaire> Interimaires => GetCollection<Interimaire>(nameof(Interimaires));

        public override string ToString() => RaisonSociale;
    }

    [DefaultClassOptions]
    [XafDisplayName("Alerte interimaire")]
    [DefaultProperty(nameof(Message))]
   // [NavigationItem("GRH - Interimaires")]
    public class AlerteInterimaire : BaseObject
    {
        public AlerteInterimaire(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); DateAlerte = DateTime.Now; Traitee = false; }

        [Association("Interimaire-Alertes")]
        public Interimaire Interimaire { get => inter; set => SetPropertyValue(nameof(Interimaire), ref inter, value); }
        Interimaire inter;

        public AlerteInterimaireType TypeAlerte { get => typ; set => SetPropertyValue(nameof(TypeAlerte), ref typ, value); }
        AlerteInterimaireType typ;

        public AlerteInterimaireNiveau Niveau { get => niv; set => SetPropertyValue(nameof(Niveau), ref niv, value); }
        AlerteInterimaireNiveau niv;

        [Size(1000)]
        [RuleRequiredField]
        public string Message { get => msg; set => SetPropertyValue(nameof(Message), ref msg, value?.Trim()); }
        string msg;

        [DevExpress.ExpressApp.Model.ModelDefault("AllowEdit", "False")]
        public DateTime DateAlerte { get => dat; set => SetPropertyValue(nameof(DateAlerte), ref dat, value); }
        DateTime dat;

        public bool Traitee { get => tr; set => SetPropertyValue(nameof(Traitee), ref tr, value); }
        bool tr;

        [Size(50)]
        public string TraiteParNom { get => tpn; set => SetPropertyValue(nameof(TraiteParNom), ref tpn, value); }
        string tpn;

        public StationService Station { get => sta; set => SetPropertyValue(nameof(Station), ref sta, value); }
        StationService sta;

        public override string ToString() => Message;
    }
}
