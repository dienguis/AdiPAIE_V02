using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using System.Linq;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Folder")]
    public class DossierSalarie : BaseObject
    {
        public DossierSalarie(Session session) : base(session) { }

        // ── Salarié ───────────────────────────────────────────
        Salarie salarie;
        [Association("Salarie-DossierSalarie")]
        [RuleRequiredField]
        [RuleUniqueValue("DossierSalarie_OnePerEmployee", DefaultContexts.Save,
            CustomMessageTemplate = "Un dossier salarié existe déjà pour ce salarié.")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }

        // ── Identification ────────────────────────────────────
        string reference;
        [Size(50)]
        [XafDisplayName("Référence")]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value?.Trim());
        }

        DateTime dateOuverture;
        [XafDisplayName("Date d'ouverture")]
        public DateTime DateOuverture
        {
            get => dateOuverture;
            set => SetPropertyValue(nameof(DateOuverture), ref dateOuverture, value);
        }

        string observations;
        [Size(SizeAttribute.Unlimited)]
        [XafDisplayName("Observations")]
        public string Observations
        {
            get => observations;
            set => SetPropertyValue(nameof(Observations), ref observations, value);
        }

        // ── Documents ─────────────────────────────────────────
        [Association("DossierSalarie-Documents"), Aggregated]
        [XafDisplayName("Documents")]
        public XPCollection<DossierDocument> Documents
            => GetCollection<DossierDocument>(nameof(Documents));

        // ── Compteurs (non persistants) ───────────────────────
        [NonPersistent, XafDisplayName("Total documents")]
        public int NbDocuments => Documents.Count;

        [NonPersistent, XafDisplayName("Documents expirés")]
        public int NbDocumentsExpires =>
            Documents.Count(d => d.DateExpiration.HasValue
                              && d.DateExpiration.Value.Date < DateTime.Today);

        [NonPersistent, XafDisplayName("Documents expirant dans 30 j")]
        public int NbDocumentsExpirantBientot =>
            Documents.Count(d => d.DateExpiration.HasValue
                              && d.DateExpiration.Value.Date >= DateTime.Today
                              && d.DateExpiration.Value.Date <= DateTime.Today.AddDays(30));

        [NonPersistent, XafDisplayName("Documents confidentiels")]
        public int NbDocumentsConfidentiels =>
            Documents.Count(d => d.Confidentiel);

        // ── Affichage ─────────────────────────────────────────
        [NonPersistent]
        public string DisplayName => $"Dossier RH — {Salarie?.FullName}";

        // ── Init & hooks ──────────────────────────────────────
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

        // ── Méthode d'archivage automatique ───────────────────
        /// <summary>
        /// Ajoute un document avec sa pièce jointe dans le dossier.
        /// Utilisé par l'archivage automatique des attestations, bulletins, etc.
        /// </summary>
        public DossierDocument AjouterDocument(
            Session session,
            DomainEnums.DossierCategorieDocument categorie,
            string titre,
            string sourceAuto,
            byte[] contenuFichier,
            string nomFichier,
            DateTime? dateExpiration = null,
            bool confidentiel = false)
        {
            var doc = new DossierDocument(session)
            {
                Dossier = this,
                Categorie = categorie,
                Titre = titre,
                SourceAuto = sourceAuto,
                DateDocument = DateTime.Today,
                DateExpiration = dateExpiration,
                Confidentiel = confidentiel
            };

            if (contenuFichier != null && contenuFichier.Length > 0)
            {
                var pj = new DossierPieceJointe(session)
                {
                    DossierDocument = doc,
                    Titre = nomFichier
                };
                pj.Fichier = new DevExpress.Persistent.BaseImpl.FileData(session);
                using var ms = new System.IO.MemoryStream(contenuFichier);
                pj.Fichier.LoadFromStream(nomFichier, ms);
            }

            return doc;
        }
    }
}
