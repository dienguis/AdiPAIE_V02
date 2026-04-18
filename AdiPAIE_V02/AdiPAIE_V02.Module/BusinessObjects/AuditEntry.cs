// AdiPAIE_V02.Module/BusinessObjects/AuditEntry.cs
// Journal d'audit — traçabilité de toutes les modifications métier
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using System;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    /// <summary>
    /// Entrée du journal d'audit.
    /// Enregistre chaque action significative effectuée dans l'application :
    /// création, modification, validation, clôture, suppression, etc.
    ///
    /// Lecture seule — les entrées ne sont jamais modifiées ni supprimées.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Journal d'audit")]
    [DefaultProperty(nameof(Resume))]
    [ImageName("BO_Audit")]
    [NavigationItem("Administration")]
    public class AuditEntry : BaseObject
    {
        public AuditEntry(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateAction = DateTime.Now;
        }

        // ── Date et utilisateur ──────────────────────────────────
        [XafDisplayName("Date")]
        [ModelDefault("AllowEdit", "False")]
        [Indexed]
        public DateTime DateAction
        {
            get => dateAction;
            set => SetPropertyValue(nameof(DateAction), ref dateAction, value);
        }
        DateTime dateAction;

        [Size(100)]
        [XafDisplayName("Utilisateur")]
        [ModelDefault("AllowEdit", "False")]
        [Indexed]
        public string Utilisateur
        {
            get => utilisateur;
            set => SetPropertyValue(nameof(Utilisateur), ref utilisateur, value);
        }
        string utilisateur;

        // ── Entité et action ─────────────────────────────────────
        [Size(100)]
        [XafDisplayName("Entité")]
        [ModelDefault("AllowEdit", "False")]
        [Indexed]
        public string NomEntite
        {
            get => nomEntite;
            set => SetPropertyValue(nameof(NomEntite), ref nomEntite, value);
        }
        string nomEntite;

        [Size(50)]
        [XafDisplayName("Action")]
        [ModelDefault("AllowEdit", "False")]
        public string Action
        {
            get => action;
            set => SetPropertyValue(nameof(Action), ref action, value);
        }
        string action;

        [Size(50)]
        [XafDisplayName("Identifiant objet")]
        [ModelDefault("AllowEdit", "False")]
        [Browsable(false)]
        public string ObjectId
        {
            get => objectId;
            set => SetPropertyValue(nameof(ObjectId), ref objectId, value);
        }
        string objectId;

        [Size(200)]
        [XafDisplayName("Libellé objet")]
        [ModelDefault("AllowEdit", "False")]
        public string ObjectLabel
        {
            get => objectLabel;
            set => SetPropertyValue(nameof(ObjectLabel), ref objectLabel, value);
        }
        string objectLabel;

        // ── Détails ──────────────────────────────────────────────
        [Size(SizeAttribute.Unlimited)]
        [XafDisplayName("Détails modifications")]
        [ModelDefault("AllowEdit", "False")]
        [VisibleInListView(false)]
        public string Details
        {
            get => details;
            set => SetPropertyValue(nameof(Details), ref details, value);
        }
        string details;

        [Size(500)]
        [XafDisplayName("Ancien statut")]
        [ModelDefault("AllowEdit", "False")]
        [VisibleInListView(false)]
        public string AncienStatut
        {
            get => ancienStatut;
            set => SetPropertyValue(nameof(AncienStatut), ref ancienStatut, value);
        }
        string ancienStatut;

        [Size(500)]
        [XafDisplayName("Nouveau statut")]
        [ModelDefault("AllowEdit", "False")]
        [VisibleInListView(false)]
        public string NouveauStatut
        {
            get => nouveauStatut;
            set => SetPropertyValue(nameof(NouveauStatut), ref nouveauStatut, value);
        }
        string nouveauStatut;

        [Size(50)]
        [XafDisplayName("Adresse IP")]
        [ModelDefault("AllowEdit", "False")]
        [VisibleInListView(false)]
        public string AdresseIP
        {
            get => adresseIP;
            set => SetPropertyValue(nameof(AdresseIP), ref adresseIP, value);
        }
        string adresseIP;

        // ── Propriété calculée ───────────────────────────────────
        [NonPersistent]
        public string Resume =>
            $"{DateAction:dd/MM/yyyy HH:mm} — {Utilisateur} — {Action} — {NomEntite} : {ObjectLabel}";

        public override string ToString() => Resume;

        // ── Méthode factory ──────────────────────────────────────
        /// <summary>
        /// Crée une entrée d'audit dans la session donnée.
        /// </summary>
        public static AuditEntry Enregistrer(
            Session session,
            string utilisateur,
            string nomEntite,
            string action,
            string objectId,
            string objectLabel,
            string details = null,
            string ancienStatut = null,
            string nouveauStatut = null,
            string adresseIP = null)
        {
            var entry = new AuditEntry(session)
            {
                Utilisateur = utilisateur ?? "(système)",
                NomEntite = nomEntite,
                Action = action,
                ObjectId = objectId,
                ObjectLabel = objectLabel ?? "",
                Details = details,
                AncienStatut = ancienStatut,
                NouveauStatut = nouveauStatut,
                AdresseIP = adresseIP,
            };
            return entry;
        }
    }
}
