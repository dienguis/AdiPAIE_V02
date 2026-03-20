using AdiPAIE_V02.Module.Domain;
using DevExpress.Drawing;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
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

    // ── Alertes expiration ────────────────────────────────────
    [Appearance("Doc_Expire",
        TargetItems = "*",
        Criteria = "DateExpiration != null AND DateExpiration < LocalDateTimeToday()",
        FontColor = "Red",
        FontStyle = DXFontStyle.Bold)]

    [Appearance("Doc_ExpireBientot",
        TargetItems = "*",
        Criteria = "DateExpiration != null "
                 + "AND DateExpiration >= LocalDateTimeToday() "
                 + "AND DateExpiration <= AddDays(LocalDateTimeToday(), 30)",
        BackColor = "LightSalmon")]
    public class DossierDocument : BaseObject
    {
        public DossierDocument(Session session) : base(session) { }

        // ── Dossier parent ────────────────────────────────────
        DossierSalarie dossier;
        [Association("DossierSalarie-Documents")]
        [RuleRequiredField]
        public DossierSalarie Dossier
        {
            get => dossier;
            set => SetPropertyValue(nameof(Dossier), ref dossier, value);
        }

        // ── Classification ────────────────────────────────────
        DossierCategorieDocument? categorie;
        [XafDisplayName("Catégorie")]
        public DossierCategorieDocument? Categorie
        {
            get => categorie;
            set => SetPropertyValue(nameof(Categorie), ref categorie, value);
        }

        DocumentTypeRef documentType;
        [DataSourceCriteria("Actif = true")]
        [XafDisplayName("Type de document")]
        public DocumentTypeRef DocumentType
        {
            get => documentType;
            set => SetPropertyValue(nameof(DocumentType), ref documentType, value);
        }

        // ── Contenu ───────────────────────────────────────────
        string titre;
        [Size(150)]
        [RuleRequiredField]
        [XafDisplayName("Titre")]
        public string Titre
        {
            get => titre;
            set => SetPropertyValue(nameof(Titre), ref titre, value?.Trim());
        }

        string description;
        [Size(SizeAttribute.Unlimited)]
        [XafDisplayName("Description")]
        public string Description
        {
            get => description;
            set => SetPropertyValue(nameof(Description), ref description, value);
        }

        // ── Dates ─────────────────────────────────────────────
        DateTime dateDocument;
        [XafDisplayName("Date du document")]
        public DateTime DateDocument
        {
            get => dateDocument;
            set => SetPropertyValue(nameof(DateDocument), ref dateDocument, value);
        }

        DateTime? dateExpiration;
        [XafDisplayName("Date d'expiration")]
        public DateTime? DateExpiration
        {
            get => dateExpiration;
            set => SetPropertyValue(nameof(DateExpiration), ref dateExpiration, value);
        }

        // ── Options ───────────────────────────────────────────
        bool confidentiel;
        [XafDisplayName("Confidentiel")]
        public bool Confidentiel
        {
            get => confidentiel;
            set => SetPropertyValue(nameof(Confidentiel), ref confidentiel, value);
        }

        // ── Source automatique (pour traçabilité archivage) ───
        string sourceAuto;
        [Size(100)]
        [XafDisplayName("Source")]
        [ModelDefault("AllowEdit", "False")]
        [System.ComponentModel.ReadOnly(true)]
        /// <summary>
        /// Indique l'origine du document si ajouté automatiquement.
        /// Ex : "Attestation générée automatiquement", "Bulletin de paie"
        /// </summary>
        public string SourceAuto
        {
            get => sourceAuto;
            set => SetPropertyValue(nameof(SourceAuto), ref sourceAuto, value?.Trim());
        }

        // ── Pièces jointes ────────────────────────────────────
        [Association("DossierDocument-PiecesJointes"), Aggregated]
        [XafDisplayName("Pièces jointes")]
        public XPCollection<DossierPieceJointe> PiecesJointes
            => GetCollection<DossierPieceJointe>(nameof(PiecesJointes));

        // ── Propriétés calculées ──────────────────────────────
        [NonPersistent]
        [XafDisplayName("Expiré")]
        public bool EstExpire =>
            DateExpiration.HasValue && DateExpiration.Value.Date < DateTime.Today;

        [NonPersistent]
        [XafDisplayName("Jours avant expiration")]
        public int JoursAvantExpiration =>
            DateExpiration.HasValue
                ? (int)(DateExpiration.Value.Date - DateTime.Today).TotalDays
                : int.MaxValue;

        [NonPersistent]
        public string DisplayName =>
            $"{Categorie} — {Titre}";

        // ── Init ──────────────────────────────────────────────
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateDocument = DateTime.Today;
            Categorie = DossierCategorieDocument.Administratif;
        }
    }
}
