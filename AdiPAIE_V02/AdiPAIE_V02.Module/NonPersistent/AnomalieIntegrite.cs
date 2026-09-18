using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using System;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.NonPersistent
{
    /// <summary>
    /// V1.8.7 - Anomalie d'intégrité détectée par IntegriteService.
    ///
    /// Exposée en tant que ListView non-persistante dans le menu
    /// "Contrôle d'intégrité". Chaque ligne est une anomalie qui peut
    /// avoir une action de correction associée.
    ///
    /// Hérite de NonPersistentBaseObject pour fournir la clé Oid requise
    /// par XAF Blazor (sinon les ListView/DetailView ne sont pas générées).
    /// </summary>
    [DomainComponent]
    [XafDisplayName("Anomalie d'intégrité")]
    [DefaultProperty(nameof(Description))]
    public class AnomalieIntegrite : NonPersistentBaseObject
    {
        [XafDisplayName("Catégorie")]
        public string Categorie { get; set; }

        [XafDisplayName("Sévérité")]
        public string Severite { get; set; }

        [XafDisplayName("Type d'anomalie")]
        [ModelDefault("ColumnWidth", "250")]
        public string TypeAnomalie { get; set; }

        [XafDisplayName("Description")]
        [ModelDefault("ColumnWidth", "400")]
        public string Description { get; set; }

        [XafDisplayName("Objet concerné")]
        [ModelDefault("ColumnWidth", "250")]
        public string ObjetConcerne { get; set; }

        [XafDisplayName("Action suggérée")]
        [ModelDefault("ColumnWidth", "300")]
        public string ActionSuggere { get; set; }

        // -- Références vers les objets métier pour permettre la correction --
        [Browsable(false)]
        public Guid? OidBulletin { get; set; }

        [Browsable(false)]
        public Guid? OidPret { get; set; }

        [Browsable(false)]
        public Guid? OidSalarie { get; set; }

        [Browsable(false)]
        public Guid? OidBulletinLigne { get; set; }

        [Browsable(false)]
        public Guid? OidPretEcheance { get; set; }

        [Browsable(false)]
        public string CodeAction { get; set; } // Ex: "ReouvrirRevalider", "SupprimerLigneDoublon", ...

        [Browsable(false)]
        public decimal? MontantConcerne { get; set; }

        // Pour tri stable et clic
        [Browsable(false)]
        public string Cle { get; set; }
    }
}
