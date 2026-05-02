// =============================================================================
//  DashboardsRHMenu.cs
//  Module « Tableaux de Bord RH » — entrée de navigation XAF.
//
//  Cette classe non persistante sert d'« ancre » pour faire apparaître une
//  entrée « Tableaux de Bord RH » dans la navigation XAF. Lorsqu'un utilisateur
//  ouvre cette entrée, le contrôleur DashboardsRHNavigationController affiche
//  un bouton "Ouvrir les Tableaux de Bord" qui redirige le navigateur vers la
//  page Razor `/dashboards/` (Pages/Dashboards/DashboardHome.razor) où vit la
//  vraie UI (DevExpress Blazor — DxGrid, DxChart, DxComboBox, ...).
//
//  Pourquoi ce détour ?
//  - L'application est XAF (Blazor Server) : le menu principal est piloté par
//    XAF via les Navigation Items et les Permissions XAF.
//  - Les tableaux de bord RH sont des pages Razor classiques (besoin d'un
//    contrôle fin sur le rendu, charts complexes, layout custom).
//  - Cette entrée fait le pont sans imposer un rewrite du shell XAF.
// =============================================================================

using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.NonPersistent
{
    /// <summary>
    /// Entrée fictive « Tableaux de Bord RH » dans la navigation XAF.
    /// </summary>
    [DomainComponent]
    [DefaultClassOptions]
    [NavigationItem("GRH - Tableaux de Bord")]
    [XafDisplayName("Tableaux de Bord RH")]
    [ImageName("BO_Report")]
    [DefaultProperty(nameof(Description))]
    public class DashboardsRHMenu : NonPersistentBaseObject
    {
        public DashboardsRHMenu()
        {
            Description =
                "Effectif détaillé, analyse de l'effectif, mouvements (arrivées/départs), " +
                "rémunération, suivi des absences et bilan social mensuel.";
        }

        // ── Description affichée dans le DetailView ────────────────────────────
        [XafDisplayName("Description")]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("RowCount", "3")]
        public string Description { get; set; }

        public override string ToString() => "Tableaux de Bord RH";
    }
}
