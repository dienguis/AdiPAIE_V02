using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Data;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using System;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    /// <summary>
    /// Objet non-persistant — Tableau de Bord Intérimaires.
    /// Design inspiré du dashboard de référence avec KPIs, graphiques et distributions.
    /// </summary>
    [DomainComponent]
    [DefaultClassOptions]
    [NavigationItem("Tableaux de Bord")]
    [XafDisplayName("Intérimaires")]
    [ImageName("BO_Person")]
    public class TableauBordInterimaire : IXafEntityObject, IObjectSpaceLink
    {
        private IObjectSpace _objectSpace;

        // ══════════════ KPIs principaux ══════════════

        [XafDisplayName("Effectif Total")]
        [ModelDefault("AllowEdit", "False")]
        public int EffectifTotal { get; set; }

        [XafDisplayName("Départs")]
        [ModelDefault("AllowEdit", "False")]
        public int NbDeparts { get; set; }

        [XafDisplayName("% Départs")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}%")]
        public double PctDeparts { get; set; }

        [XafDisplayName("Taux Rotation")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}%")]
        public double TauxRotation { get; set; }

        [XafDisplayName("Ancienneté Moyenne (ans)")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N1}")]
        public double AncienneteMoyenne { get; set; }

        [XafDisplayName("% Femmes")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}%")]
        public double PctFemmes { get; set; }

        [XafDisplayName("% Hommes")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}%")]
        public double PctHommes { get; set; }

        [XafDisplayName("Hommes")]
        [ModelDefault("AllowEdit", "False")]
        public int NbHommes { get; set; }

        [XafDisplayName("Femmes")]
        [ModelDefault("AllowEdit", "False")]
        public int NbFemmes { get; set; }

        [XafDisplayName("Âge Moyen")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0} ans")]
        public double AgeMoyen { get; set; }

        // ══════════════ Distributions ══════════════

        [XafDisplayName("Par Tranche d'Âge")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionItem> ParTrancheAge { get; set; } = new();

        [XafDisplayName("Par Ancienneté")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionItem> ParAnciennete { get; set; } = new();

        [XafDisplayName("Par Station / Segment")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionItem> ParStation { get; set; } = new();

        [XafDisplayName("Par Fonction (Top)")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionItem> ParFonction { get; set; } = new();

        [XafDisplayName("Par Type Contrat")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionItem> ParTypeContrat { get; set; } = new();

        [XafDisplayName("Par Société Intérim")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionItem> ParSociete { get; set; } = new();

        // ══════════════ Évolution annuelle ══════════════

        [XafDisplayName("Évolution Annuelle")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<EvolutionItem> EvolutionAnnuelle { get; set; } = new();

        // ══════════════ IXafEntityObject ══════════════
        public void OnCreated() { }
        public void OnSaving() { }
        public void OnLoaded() { }

        IObjectSpace IObjectSpaceLink.ObjectSpace
        {
            get => _objectSpace;
            set => _objectSpace = value;
        }
    }
}
