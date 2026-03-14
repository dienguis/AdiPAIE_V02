using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Folder")]

    public class DossierSalarie : BaseObject
    {
        public DossierSalarie(Session session) : base(session) { }

        Salarie salarie;
        [Association("Salarie-DossiersRH")]
        [RuleRequiredField]
           [RuleUniqueValue("DossierSalarie_OnePerEmployee", DefaultContexts.Save,
        CustomMessageTemplate = "Un dossier salarié existe déjà pour ce salarié.")]

        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }

        string reference;
        [Size(50)]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value);
        }

        DateTime dateOuverture;
        public DateTime DateOuverture
        {
            get => dateOuverture;
            set => SetPropertyValue(nameof(DateOuverture), ref dateOuverture, value);
        }

        string observations;
        [Size(SizeAttribute.Unlimited)]
        public string Observations
        {
            get => observations;
            set => SetPropertyValue(nameof(Observations), ref observations, value);
        }

        [Association("DossierSalarie-Documents"), Aggregated]
        public XPCollection<DossierDocument> Documents
            => GetCollection<DossierDocument>(nameof(Documents));

        [NonPersistent]
        public string DisplayName => $"Dossier RH - {Salarie?.FullName}";

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateOuverture = DateTime.Today;
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            if (string.IsNullOrWhiteSpace(Reference) && Salarie != null)
                Reference = $"DS-{Salarie.Matricule}";
        }
    }
}