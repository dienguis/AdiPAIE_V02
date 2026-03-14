using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_FileAttachment")]
    public class DossierDocument : BaseObject
    {
        public DossierDocument(Session session) : base(session) { }

        DossierSalarie dossier;
        [Association("DossierSalarie-Documents")]
        [RuleRequiredField]
        public DossierSalarie Dossier
        {
            get => dossier;
            set => SetPropertyValue(nameof(Dossier), ref dossier, value);
        }

        DossierCategorieDocument categorie;
        public DossierCategorieDocument Categorie
        {
            get => categorie;
            set => SetPropertyValue(nameof(Categorie), ref categorie, value);
        }

        DocumentTypeRef documentType;
        [DataSourceCriteria("Actif = true")]
        public DocumentTypeRef DocumentType
        {
            get => documentType;
            set => SetPropertyValue(nameof(DocumentType), ref documentType, value);
        }

        string titre;
        [Size(150)]
        [RuleRequiredField]
        public string Titre
        {
            get => titre;
            set => SetPropertyValue(nameof(Titre), ref titre, value);
        }

        string description;
        [Size(SizeAttribute.Unlimited)]
        public string Description
        {
            get => description;
            set => SetPropertyValue(nameof(Description), ref description, value);
        }

        DateTime dateDocument;
        public DateTime DateDocument
        {
            get => dateDocument;
            set => SetPropertyValue(nameof(DateDocument), ref dateDocument, value);
        }

        DateTime? dateExpiration;
        public DateTime? DateExpiration
        {
            get => dateExpiration;
            set => SetPropertyValue(nameof(DateExpiration), ref dateExpiration, value);
        }

        bool confidentiel;
        public bool Confidentiel
        {
            get => confidentiel;
            set => SetPropertyValue(nameof(Confidentiel), ref confidentiel, value);
        }

        [Association("DossierDocument-PiecesJointes"), Aggregated]
        public XPCollection<DossierPieceJointe> PiecesJointes
    => GetCollection<DossierPieceJointe>(nameof(PiecesJointes));

        [NonPersistent]
        public string DisplayName => $"{Categorie} - {Titre}";

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateDocument = DateTime.Today;
            Categorie = DossierCategorieDocument.Administratif;
        }
    }
}