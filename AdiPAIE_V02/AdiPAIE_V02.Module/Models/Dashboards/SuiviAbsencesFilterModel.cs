// =============================================================================
//  SuiviAbsencesFilterModel.cs — V1.3.2 (mai 2026)
//
//  REFONTE pour Excel-style multi-sélection :
//    - Annees (multi)
//    - SiteOids (multi)
//    - Mois (multi 1..12)
//    - GenreSet (Masculin et/ou Feminin)
//    - DepartementsNoms (multi)
//    - CategorieOids (multi)
//    - MotifsActifs (multi MotifAbsence)
//
//  Conserve InclureEnAttente pour ne pas casser la logique existante
//  (par défaut = false = uniquement absences Accordee).
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>Filtres multi-sélection pour le Tableau N°5 « Suivi des Absences ».</summary>
    public sealed class SuiviAbsencesFilterModel
    {
        /// <summary>Périmètre : Interne (CongeDemande) ou Externe (BulletinInterim 30ème).</summary>
        public PersonnelType Personnel { get; set; } = PersonnelType.Interne;

        /// <summary>Années sélectionnées (ex {2025, 2026}). Vide = année courante uniquement.</summary>
        public List<int> Annees { get; set; } = new() { DateTime.Today.Year };

        /// <summary>Sites sélectionnés. Vide = tous sites.</summary>
        public List<Guid> SiteOids { get; set; } = new();

        /// <summary>Mois sélectionnés (1..12). Vide = tous mois.</summary>
        public List<int> Mois { get; set; } = new();

        /// <summary>Genres sélectionnés. Vide = tous.</summary>
        public List<Sexe> GenreSet { get; set; } = new();

        /// <summary>Noms de département (Salarie.Departement.Nom). Vide = tous.</summary>
        public List<string> DepartementsNoms { get; set; } = new();

        /// <summary>Catégories sélectionnées. Vide = toutes.</summary>
        public List<Guid> CategorieOids { get; set; } = new();

        /// <summary>Motifs sélectionnés. Vide = tous (= comportement par défaut).</summary>
        public List<MotifAbsence> MotifsActifs { get; set; } = new();

        /// <summary>
        /// Inclure les statuts EnAttente + Soumise + Accordée ?
        /// Par défaut false = uniquement CongeStatut.Accordee.
        /// </summary>
        public bool InclureEnAttente { get; set; } = false;

        public string ToCacheKey()
        {
            string j(IEnumerable<object> e) => string.Join(",", e ?? Enumerable.Empty<object>());
            // ⚠️ Personnel DOIT être dans la clé : sans ça, le cache renvoie
            //    les données du périmètre précédent au switch Interne⇄Externe.
            return $"absences|p={Personnel}|a={j(Annees.Cast<object>())}|s={j(SiteOids.Cast<object>())}|m={j(Mois.Cast<object>())}" +
                   $"|g={j(GenreSet.Cast<object>())}|d={j(DepartementsNoms.Cast<object>())}|c={j(CategorieOids.Cast<object>())}" +
                   $"|mt={j(MotifsActifs.Cast<object>())}|att={InclureEnAttente}";
        }
    }
}
