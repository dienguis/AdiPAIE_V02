using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions, XafDisplayName("Échéance de prêt")]
    [DefaultProperty(nameof(DisplayName))]
    public class PretEcheance : BaseObject
    {
        public PretEcheance(Session s) : base(s) { }

        [Association("Pret-Echeances"), RuleRequiredField]
        public Pret Pret
        {
            get => pret;
            set => SetPropertyValue(nameof(Pret), ref pret, value);
        }
        private Pret pret;

        public DateTime DateEcheance
        {
            get => dEch;
            set => SetPropertyValue(nameof(DateEcheance), ref dEch, value);
        }
        private DateTime dEch = DateTime.Today;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal MontantCapital
        {
            get => cap;
            set => SetPropertyValue(nameof(MontantCapital), ref cap, value);
        }
        private decimal cap;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal MontantInteret
        {
            get => interet;
            set => SetPropertyValue(nameof(MontantInteret), ref interet, value);
        }
        private decimal interet;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        public decimal? MontantFrais
        {
            get => frais;
            set => SetPropertyValue(nameof(MontantFrais), ref frais, value);
        }
        private decimal? frais;

        //[PersistentAlias("Nvl(MontantCapital,0) + Nvl(MontantInteret,0) + Nvl(MontantFrais,0)")]
        //[ModelDefault("DisplayFormat", "N0")]
        //public decimal MontantTotal => (decimal)EvaluateAlias(nameof(MontantTotal));
        [PersistentAlias("IsNull(MontantCapital, 0) + IsNull(MontantInteret, 0) + IsNull(MontantFrais, 0)")]
        [ModelDefault("DisplayFormat", "N0")]
        public decimal MontantTotal => (decimal)EvaluateAlias(nameof(MontantTotal));


        public PretEcheanceStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }
        private PretEcheanceStatut statut = PretEcheanceStatut.Prevue;

        // Lien (optionnel) vers le bulletin qui a prélevé l’échéance
        // -> Ajoute le XPCollection côté Bulletin (voir §4)
        [Association("Bulletin-PretsPreleves")]
        public Bulletin BulletinPreleveur
        {
            get => bulletin;
            set => SetPropertyValue(nameof(BulletinPreleveur), ref bulletin, value);
        }
        private Bulletin bulletin;

        [Size(120)]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value?.Trim());
        }
        private string reference;
        // PretEcheance.cs
        [ModelDefault("DisplayFormat", "g")]
        public DateTime? DatePrelevement
        {
            get => datePrev;
            set => SetPropertyValue(nameof(DatePrelevement), ref datePrev, value);
        }
        private DateTime? datePrev;



        //[PersistentAlias("Concat(FormatString('{0:yyyy-MM}', DateEcheance), ' - ', ToStr(MontantTotal))")]
        //public string DisplayName => (string)EvaluateAlias(nameof(DisplayName));

        [PersistentAlias(
  "Concat(" +
  "  ToStr(GetYear(DateEcheance)), '-', " +
  "  Iif(GetMonth(DateEcheance) < 10, Concat('0', ToStr(GetMonth(DateEcheance))), ToStr(GetMonth(DateEcheance))), " +
  "  ' - ', ToStr(MontantTotal)" +
  ")")]
        public string DisplayName => (string)EvaluateAlias(nameof(DisplayName));



        protected override void OnSaving()
        {
            base.OnSaving();
            Pret?.RecalculerEtat();
        }

        protected override void OnDeleted()
        {
            base.OnDeleted();
            Pret?.RecalculerEtat();
        }
    }
}
