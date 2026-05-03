// =============================================================================
//  SuiviAbsencesFilterModel.cs
//  Tableau N°5 (Suivi des Absences) — INTERNE uniquement.
//
//  Source de données validée :
//    - Entité `CongeDemande` (Salarie, Type → CongeType.Famille,
//      DateDebut, DateFin, DureeJours, Statut)
//    - Filtre par défaut : Statut = `Accordee` (20)  → absences réellement prises
//    - Familles (CongeType.Famille) : Annuel / Maladie / Maternité /
//      EvenementFamilial / SansSolde / Récupération / autres
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>Filtres pour le Tableau N°5 « Suivi des Absences ».</summary>
    public sealed class SuiviAbsencesFilterModel
    {
        public int    Annee        { get; set; } = DateTime.Today.Year;
        public Guid?  SiteOid      { get; set; }
        public Sexe?  Genre        { get; set; }
        public string? Segment     { get; set; }   // Departement.Nom
        public Guid?  CategorieOid { get; set; }

        /// <summary>
        /// Filtre par famille de congé (Annuel / Maladie / etc.).
        /// Null = toutes familles.
        /// </summary>
        public FamilleConge? Famille { get; set; }

        /// <summary>
        /// Inclure les statuts EnAttente + Soumise + Accordée (vue large) ?
        /// Par défaut <c>false</c> = uniquement <c>CongeStatut.Accordee</c> (20)
        /// (vue « absences réellement prises » — recommandée pour KPI métier).
        /// </summary>
        public bool InclureEnAttente { get; set; } = false;

        public string ToCacheKey() =>
            $"absences|a={Annee}|site={SiteOid}|g={Genre}|seg={Segment}|cat={CategorieOid}|fam={Famille}|att={InclureEnAttente}";
    }
}
