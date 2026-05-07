// =============================================================================
//  PilotageRecrutementFilterModel.cs — V1.4 (mai 2026)
//
//  Filtres du Dashboard N°12 Pilotage Recrutement.
//   - Personnel (Interne / Externe)
//   - Année (par défaut : année en cours)
//   - Site(s) — multi-sélection (Externe uniquement)
//   - Département(s) — multi-sélection (Interne uniquement)
//   - Catégorie(s) professionnelle(s) — multi-sélection (Interne uniquement)
//   - TypeContrat (CDI / CDD / Stage) — Interne uniquement
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    public sealed class PilotageRecrutementFilterModel
    {
        /// <summary>Périmètre : Interne (salariés) ou Externe (intérim).</summary>
        public PersonnelType Personnel { get; set; } = PersonnelType.Interne;

        public int Annee { get; set; } = DateTime.Today.Year;

        public List<Guid> SiteOids { get; set; } = new();
        public List<Guid> DepartementOids { get; set; } = new();
        public List<Guid> CategorieOids { get; set; } = new();

        /// <summary>Filtre optionnel par TypeContrat (vide = tous).</summary>
        public string TypeContrat { get; set; } = "";

        public string ToCacheKey()
        {
            string Join(IEnumerable<Guid> ids) =>
                ids == null ? "" : string.Join(",", ids.OrderBy(g => g));

            return $"pilotage-recrutement|p={Personnel}|y={Annee}"
                 + $"|s={Join(SiteOids)}"
                 + $"|d={Join(DepartementOids)}"
                 + $"|c={Join(CategorieOids)}"
                 + $"|tc={TypeContrat}";
        }
    }
}
