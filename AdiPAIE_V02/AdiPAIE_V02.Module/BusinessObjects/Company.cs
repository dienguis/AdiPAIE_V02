using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions, XafDisplayName("Entreprise")]
    [DefaultProperty(nameof(RaisonSociale ))]
    public class Company : BaseObject
    { // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
        // Use CodeRush to create XPO classes and properties with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/118557
        public Company(Session session)
            : base(session)
        {
        }
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
            DateCreation = DateTime.Today;
           }
        private string _Code;
        [Size(20)]
        [Indexed(Unique = true, Name = "IX_Societes_Code")]
        public string Code
        {
            get => _Code;
            set => SetPropertyValue(nameof(Code), ref _Code, value);
        }

        // RaisonSociale NVARCHAR(100) NOT NULL
        private string _RaisonSociale;
        [Size(100)]
        public string RaisonSociale
        {
            get => _RaisonSociale;
            set => SetPropertyValue(nameof(RaisonSociale), ref _RaisonSociale, value?.Trim());
        }

        // Adresse NVARCHAR(200) NULL
        private string _Adresse;
        [Size(200)]
        public string Address
        {
            get => _Adresse;
            set => SetPropertyValue(nameof(Address), ref _Adresse, value?.Trim()   );
        }

        // Ville NVARCHAR(50) NULL
        private string _Ville;
        [Size(50)]
        public string Ville
        {
            get => _Ville;
            set => SetPropertyValue(nameof(Ville), ref _Ville, value);
        }

        // Pays NVARCHAR(50) NULL
        private string _Pays;
        [Size(50)]
        public string Pays
        {
            get => _Pays;
            set => SetPropertyValue(nameof(Pays), ref _Pays, value);
        }

        // Telephone NVARCHAR(20) NULL
        private string _Telephone;
        [Size(20)]
        public string Telephone
        {
            get => _Telephone;
            set => SetPropertyValue(nameof(Telephone), ref _Telephone, value);
        }

        // Email NVARCHAR(100) NULL
        private string _Email;
        [Size(100)]
        public string Email
        {
            get => _Email;
            set => SetPropertyValue(nameof(Email), ref _Email, value);
        }

        // NINEA NVARCHAR(50) NULL
        private string _NINEA;
        [Size(50)]
        public string NINEA
        {
            get => _NINEA;
            set => SetPropertyValue(nameof(NINEA), ref _NINEA, value);
        }

        // RC NVARCHAR(50) NULL
        private string _RC;
        [Size(50)]
        public string RC
        {
            get => _RC;
            set => SetPropertyValue(nameof(RC), ref _RC, value);
        }

        // ── Champs Bilan Social / DTSS ──────────────────────────────────
        private string _Region;
        [Size(100)]
        [XafDisplayName("Région")]
        public string Region
        {
            get => _Region;
            set => SetPropertyValue(nameof(Region), ref _Region, value);
        }

        private string _Commune;
        [Size(100)]
        [XafDisplayName("Commune / Arrondissement")]
        public string Commune
        {
            get => _Commune;
            set => SetPropertyValue(nameof(Commune), ref _Commune, value);
        }

        private string _Telefax;
        [Size(20)]
        [XafDisplayName("Téléfax")]
        public string Telefax
        {
            get => _Telefax;
            set => SetPropertyValue(nameof(Telefax), ref _Telefax, value);
        }

        private string _BoitePostale;
        [Size(50)]
        [XafDisplayName("Boîte postale")]
        public string BoitePostale
        {
            get => _BoitePostale;
            set => SetPropertyValue(nameof(BoitePostale), ref _BoitePostale, value);
        }

        private string _SiteInternet;
        [Size(200)]
        [XafDisplayName("Site Internet")]
        public string SiteInternet
        {
            get => _SiteInternet;
            set => SetPropertyValue(nameof(SiteInternet), ref _SiteInternet, value);
        }

        private string _FormeJuridique;
        [Size(100)]
        [XafDisplayName("Forme juridique")]
        public string FormeJuridique
        {
            get => _FormeJuridique;
            set => SetPropertyValue(nameof(FormeJuridique), ref _FormeJuridique, value);
        }

        private string _ActivitePrincipale;
        [Size(200)]
        [XafDisplayName("Activité principale")]
        public string ActivitePrincipale
        {
            get => _ActivitePrincipale;
            set => SetPropertyValue(nameof(ActivitePrincipale), ref _ActivitePrincipale, value);
        }

        private string _AutresActivites;
        [Size(500)]
        [XafDisplayName("Autres activités")]
        public string AutresActivites
        {
            get => _AutresActivites;
            set => SetPropertyValue(nameof(AutresActivites), ref _AutresActivites, value);
        }

        private int _NombreEtablissements;
        [XafDisplayName("Nombre d'établissements")]
        public int NombreEtablissements
        {
            get => _NombreEtablissements;
            set => SetPropertyValue(nameof(NombreEtablissements), ref _NombreEtablissements, value);
        }

        private string _SiegeHorsSenegal;
        [Size(200)]
        [XafDisplayName("Siège hors Sénégal")]
        public string SiegeHorsSenegal
        {
            get => _SiegeHorsSenegal;
            set => SetPropertyValue(nameof(SiegeHorsSenegal), ref _SiegeHorsSenegal, value);
        }

        
// Logo de la société (image)
[ImageEditor(ListViewImageEditorCustomHeight = 40, DetailViewImageEditorFixedHeight = 120)]
public byte[] LogoImage { get; set; }



        // DateCreation DATETIME NOT NULL (DEFAULT GETDATE())
        private DateTime _DateCreation;

        [Browsable(false)]                 // cache dans toutes les vues (Detail/List/Lookup)
        public DateTime DateCreation
        {
            get => _DateCreation;
            set => SetPropertyValue(nameof(DateCreation), ref _DateCreation, value);
        }

        // xpcl   pour collection


        private Convention _Convention;
        // xpa pour Association
        [XafDisplayName("Convention")]
        [RuleRequiredField(DefaultContexts.Save)] // FK NOT NULL
        [Association("ConventionCompany")] // Same association name as in Customer
         public Convention Convention
        {
            // get; set;
            get => _Convention;
            set => SetPropertyValue(nameof(Convention), ref _Convention, value);

        }

        [Association("Company-Periodes")]
        public XPCollection<PeriodePaie> Periodes => GetCollection<PeriodePaie>(nameof(Periodes));


        protected override void OnSaving()
        {
            base.OnSaving();
            if (Session.IsNewObject(this))
            {
                // S'il existe déjà une Company, on refuse la création d'une seconde
                var nb = Convert.ToInt32(Session.Evaluate(typeof(Company), CriteriaOperator.Parse("Count()"), null));
                if (nb > 0)
                    throw new UserFriendlyException("Configuration mono-entreprise : une seule Société est autorisée.");
            }
        }


        [Association("Company-Campagnes"), Aggregated]
        [XafDisplayName("Campagnes d'évaluation")]
        public XPCollection<CampagneEvaluation> CampagnesEvaluation => GetCollection<CampagneEvaluation>(nameof(CampagnesEvaluation));
       
        
        [Association("Company-PlansFormation")]
        [XafDisplayName("Plans de formation")]
        [Browsable(false)]
        public XPCollection<PlanFormation> PlansFormation
         => GetCollection<PlanFormation>(nameof(PlansFormation));


    }
}