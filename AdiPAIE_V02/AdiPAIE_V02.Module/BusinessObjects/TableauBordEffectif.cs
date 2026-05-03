using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using DevExpress.ExpressApp.Data;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    /// <summary>
    /// Objet non-persistant — Tableau de Bord Effectifs RH.
    /// Affiché dans une DetailView avec KPIs et grilles de distribution.
    /// </summary>
    [DomainComponent]
    [DefaultClassOptions]
    [NavigationItem("Tableaux de Bord")]
    [XafDisplayName("Effectifs RH")]
    [ImageName("BO_Dashboard")]
    public class TableauBordEffectif : IXafEntityObject, IObjectSpaceLink
    {
        private IObjectSpace _objectSpace;

        // ══════════════ KPIs principaux ══════════════

        [XafDisplayName("Effectif Total")]
        [ModelDefault("AllowEdit", "False")]
        public int EffectifTotal { get; set; }

        [XafDisplayName("Hommes")]
        [ModelDefault("AllowEdit", "False")]
        public int NbHommes { get; set; }

        [XafDisplayName("Femmes")]
        [ModelDefault("AllowEdit", "False")]
        public int NbFemmes { get; set; }

        [XafDisplayName("% Hommes")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:P0}")]
        public double PctHommes => EffectifTotal > 0 ? (double)NbHommes / EffectifTotal : 0;

        [XafDisplayName("% Femmes")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:P0}")]
        public double PctFemmes => EffectifTotal > 0 ? (double)NbFemmes / EffectifTotal : 0;

        // ══════════════ Ancienneté ══════════════

        [XafDisplayName("Ancienneté Moyenne (ans)")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N1}")]
        public double AncienneteMoyenne { get; set; }

        [XafDisplayName("Ancienneté Moy. Hommes")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N1}")]
        public double AncienneteMoyHommes { get; set; }

        [XafDisplayName("Ancienneté Moy. Femmes")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N1}")]
        public double AncienneteMoyFemmes { get; set; }

        // ══════════════ Salaire ══════════════

        [XafDisplayName("Salaire Moyen")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        public decimal SalaireMoyen { get; set; }

        [XafDisplayName("Salaire Moy. Hommes")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        public decimal SalaireMoyHommes { get; set; }

        [XafDisplayName("Salaire Moy. Femmes")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0} FCFA")]
        public decimal SalaireMoyFemmes { get; set; }

        // ══════════════ Distributions (grilles intégrées) ══════════════

        [XafDisplayName("Par Département")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionItem> ParDepartement { get; set; } = new();

        [XafDisplayName("Par Fonction")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionItem> ParFonction { get; set; } = new();

        [XafDisplayName("Par Catégorie")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionItem> ParCategorie { get; set; } = new();

        [XafDisplayName("Par Sexe")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionItem> ParSexe { get; set; } = new();

        [XafDisplayName("Par Ancienneté")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionItem> ParAnciennete { get; set; } = new();

        // ══════════════ Évolution annuelle ══════════════

        [XafDisplayName("Évolution Annuelle")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<EvolutionItem> EvolutionAnnuelle { get; set; } = new();

        // ══════════════ Distribution croisée (Catégorie × Sexe) ══════════════

        [XafDisplayName("Répartition H/F par Catégorie")]
        [DevExpress.ExpressApp.DC.Aggregated]
        public BindingList<DistributionCroiseeItem> ParCategorieSexe { get; set; } = new();

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

    // ── DTOs pour les distributions ──

    [DomainComponent]
    [XafDisplayName("Distribution")]
    public class DistributionItem
    {
        [Key]
        [Browsable(false)]
        public Guid Oid { get; set; } = Guid.NewGuid();

        [XafDisplayName("Libellé")]
        public string Libelle { get; set; }

        [XafDisplayName("Effectif")]
        public int Effectif { get; set; }

        [XafDisplayName("%")]
        [ModelDefault("DisplayFormat", "{0:N0}%")]
        public double Pourcentage { get; set; }
    }

    [DomainComponent]
    [XafDisplayName("Évolution")]
    public class EvolutionItem
    {
        [Key]
        [Browsable(false)]
        public Guid Oid { get; set; } = Guid.NewGuid();

        [XafDisplayName("Année")]
        public int Annee { get; set; }

        [XafDisplayName("Effectif")]
        public int Effectif { get; set; }

        [XafDisplayName("Variation")]
        [ModelDefault("DisplayFormat", "{0:+0;-0;0}%")]
        public double Variation { get; set; }
    }

    [DomainComponent]
    [XafDisplayName("Répartition H/F")]
    public class DistributionCroiseeItem
    {
        [Key]
        [Browsable(false)]
        public Guid Oid { get; set; } = Guid.NewGuid();

        [XafDisplayName("Catégorie")]
        public string Categorie { get; set; }

        [XafDisplayName("Hommes")]
        public int Hommes { get; set; }

        [XafDisplayName("Femmes")]
        public int Femmes { get; set; }

        [XafDisplayName("% Hommes")]
        [ModelDefault("DisplayFormat", "{0:N0}%")]
        public double PctHommes { get; set; }

        [XafDisplayName("% Femmes")]
        [ModelDefault("DisplayFormat", "{0:N0}%")]
        public double PctFemmes { get; set; }
    }
}
