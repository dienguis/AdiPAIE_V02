using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    // ════════════════════════════════════════════════════════════════════
    // STATION DE SERVICE — référentiel ELTON
    // ════════════════════════════════════════════════════════════════════

    // ⚠️ V1.1 Sprint 1D — DEPRECATED. Remplacée par Site (Type=StationService).
    // Conservée pour compat des écrans hors dashboards (DemandeRecrutementInterim,
    // SocieteInterim, AlerteInterimaireService). Masquée du menu XAF — visible
    // uniquement via les FK existantes. À supprimer définitivement quand les
    // écrans métier auront migré vers Site V1.1.
    [XafDisplayName("[Deprecated] Station de service")]
    [DefaultProperty(nameof(Nom))]
    [ImageName("BO_Organization")]
    [VisibleInReports(false)]
    public class StationService : BaseObject
    {
        public StationService(Session session) : base(session) { }

        [RuleRequiredField]
        [Size(100)]
        [XafDisplayName("Nom de la station")]
        public string Nom
        {
            get => nom;
            set => SetPropertyValue(nameof(Nom), ref nom, value?.Trim());
        }
        string nom;

        [Size(20)]
        [RuleUniqueValue(DefaultContexts.Save,
            CustomMessageTemplate = "Ce code est déjà utilisé.")]
        [XafDisplayName("Code")]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code, value?.Trim().ToUpper());
        }
        string code;

        [Size(200)]
        [XafDisplayName("Adresse")]
        public string Adresse
        {
            get => adresse;
            set => SetPropertyValue(nameof(Adresse), ref adresse, value?.Trim());
        }
        string adresse;

        [Size(100)]
        [XafDisplayName("Ville")]
        public string Ville
        {
            get => ville;
            set => SetPropertyValue(nameof(Ville), ref ville, value?.Trim());
        }
        string ville;

        [XafDisplayName("Effectif max global (intérimaires)")]
        [ModelDefault("DisplayFormat", "N0")]
        public int EffectifMaxGlobal
        {
            get => effectifMaxGlobal;
            set => SetPropertyValue(nameof(EffectifMaxGlobal), ref effectifMaxGlobal, value);
        }
        int effectifMaxGlobal;

        [XafDisplayName("Active")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif = true;

        // ── BU propres à cette station ────────────────────────────────
        [Association("Station-BUs"), Aggregated]
        [XafDisplayName("Business Units")]
        public XPCollection<BusinessUnitStation> BUs
            => GetCollection<BusinessUnitStation>(nameof(BUs));

        // ── Contrats actifs ───────────────────────────────────────────
        [Association("Station-Contrats")]
        [XafDisplayName("Contrats en cours")]
        public XPCollection<ContratInterim> Contrats
            => GetCollection<ContratInterim>(nameof(Contrats));

        // ── V1.5 — Assistants Commerciaux responsables de la station ──
        [Association("AC-StationsGerees")]
        [XafDisplayName("Assistants Commerciaux")]
        public XPCollection<Salarie> AssistantsCommerciaux
            => GetCollection<Salarie>(nameof(AssistantsCommerciaux));

        // ── Propriété calculée ────────────────────────────────────────
        [NonPersistent]
        [XafDisplayName("Effectif actuel")]
        public int EffectifActuel =>
            Contrats.Count(c => c.Statut == ContratInterimStatut.EnCours);

        [NonPersistent]
        [XafDisplayName("Sureffectif ?")]
        public bool EnSureffectif =>
            EffectifMaxGlobal > 0 && EffectifActuel > EffectifMaxGlobal;

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Code) ? Nom : $"{Nom} ({Code})";
    }

    // ════════════════════════════════════════════════════════════════════
    // BUSINESS UNIT — propre à une station
    // Piste de Station A ≠ Piste de Station B
    // ════════════════════════════════════════════════════════════════════

    // ⚠️ V1.1 Sprint 1D — DEPRECATED. Remplacée par UniteOrganisationnelle
    // (Type=BU). Conservée pour compat ContratInterim.BU + MouvementInterimaire.
    // Masquée du menu XAF.
    [XafDisplayName("[Deprecated] Business Unit (Station)")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Department")]
    [VisibleInReports(false)]
    public class BusinessUnitStation : BaseObject
    {
        public BusinessUnitStation(Session session) : base(session) { }

        [Association("Station-BUs")]
        [RuleRequiredField]
        [XafDisplayName("Station")]
        public StationService Station
        {
            get => station;
            set => SetPropertyValue(nameof(Station), ref station, value);
        }
        StationService station;

        [RuleRequiredField]
        [Size(100)]
        [XafDisplayName("Libellé BU")]
        public string Libelle
        {
            get => libelle;
            set => SetPropertyValue(nameof(Libelle), ref libelle, value?.Trim());
        }
        string libelle;

        // ── Type partagé (référentiel transversal) ─────────────────────
        // Permet de regrouper toutes les "Boutique" de toutes les stations
        // sous un même Type pour les KPI dashboards. Migration auto au
        // démarrage via Updater.MigrateBUsToTypes (assigne le bon Type
        // selon le Libelle existant).
        // RuleRequiredField volontairement omis pour permettre la
        // migration douce — sera activé en V1.2 une fois tous les BU
        // historiques rattachés.
        [Association("BUType-BUs")]
        [XafDisplayName("Type")]
        [ImmediatePostData]
        public BusinessUnitType Type
        {
            get => type;
            set => SetPropertyValue(nameof(Type), ref type, value);
        }
        BusinessUnitType type;

        // ── Effectif max pour cette BU sur cette station ──────────────
        [XafDisplayName("Effectif max (intérimaires)")]
        [ModelDefault("DisplayFormat", "N0")]
        public int EffectifMax
        {
            get => effectifMax;
            set => SetPropertyValue(nameof(EffectifMax), ref effectifMax, value);
        }
        int effectifMax;

        [XafDisplayName("Active")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif = true;

        // ── Contrats actifs sur cette BU ──────────────────────────────
        [Association("BU-Contrats")]
        [XafDisplayName("Contrats en cours")]
        public XPCollection<ContratInterim> Contrats
            => GetCollection<ContratInterim>(nameof(Contrats));

        [NonPersistent]
        [XafDisplayName("Effectif actuel")]
        public int EffectifActuel =>
            Contrats.Count(c => c.Statut == ContratInterimStatut.EnCours);

        [NonPersistent]
        [XafDisplayName("Sureffectif ?")]
        public bool EnSureffectif =>
            EffectifMax > 0 && EffectifActuel > EffectifMax;

        [NonPersistent]
        [XafDisplayName("BU")]
        public string DisplayName =>
            Station != null ? $"{Libelle} — {Station.Nom}" : Libelle;

        public override string ToString() => DisplayName;
    }
}
