// AdiPAIE_V02.Module/BusinessObjects/RapportPowerBI.cs
// Liste des rapports Power BI configurés.
// Chaque rapport a un nom, une URL et un ordre d'affichage.
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [DefaultProperty(nameof(Nom))]
    [NavigationItem("Tableaux de bord")]
    [XafDisplayName("Rapports Power BI")]
    [ImageName("BO_Chart")]
    public class RapportPowerBI : BaseObject
    {
        public RapportPowerBI(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            IsActif = true;
            Ordre = 0;
        }

        string nom;
        [XafDisplayName("Nom du rapport")]
        [RuleRequiredField("RapportPowerBI_Nom", DefaultContexts.Save)]
        public string Nom
        {
            get => nom;
            set => SetPropertyValue(nameof(Nom), ref nom, value);
        }

        string description;
        [Size(500)]
        [XafDisplayName("Description")]
        public string Description
        {
            get => description;
            set => SetPropertyValue(nameof(Description), ref description, value);
        }

        string url;
        [Size(2000)]
        [XafDisplayName("URL Power BI")]
        [ToolTip("URL du rapport depuis Power BI Service (app.powerbi.com). Copiez l'URL depuis la barre d'adresse du navigateur.")]
        [RuleRequiredField("RapportPowerBI_Url", DefaultContexts.Save)]
        public string Url
        {
            get => url;
            set => SetPropertyValue(nameof(Url), ref url, value?.Trim());
        }

        bool isActif;
        [XafDisplayName("Actif")]
        public bool IsActif
        {
            get => isActif;
            set => SetPropertyValue(nameof(IsActif), ref isActif, value);
        }

        int ordre;
        [XafDisplayName("Ordre")]
        [ToolTip("Ordre d'affichage dans le menu (0 = premier)")]
        public int Ordre
        {
            get => ordre;
            set => SetPropertyValue(nameof(Ordre), ref ordre, value);
        }

        string categorie;
        [XafDisplayName("Catégorie")]
        [ToolTip("Ex: RH, Finance, Direction — pour regrouper les rapports")]
        public string Categorie
        {
            get => categorie;
            set => SetPropertyValue(nameof(Categorie), ref categorie, value);
        }
    }
}
