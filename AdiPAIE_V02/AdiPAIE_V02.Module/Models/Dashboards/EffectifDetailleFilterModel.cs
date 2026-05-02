// =============================================================================
//  EffectifDetailleFilterModel.cs
//  Tableau N°1 (Effectif détaillé) — filtres saisis par l'utilisateur.
//
//  Périmètre : Personnel INTERNE uniquement (Salarie).
//  Filtres mission : Année (multi), Site, Genre, Type de contrat.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using AdiPAIE_V02.Module.BusinessObjects;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>
    /// Filtres pour le Tableau N°1 « Effectif détaillé ».
    /// </summary>
    public sealed class EffectifDetailleFilterModel
    {
        /// <summary>
        /// Années sélectionnées (multi). Vide ⇒ année courante par défaut.
        /// La date de référence pour les KPI est :
        ///   - Aujourd'hui si l'année courante est sélectionnée (ou liste vide).
        ///   - 31/12 de la dernière année passée sélectionnée sinon.
        /// L'évolution affiche les 3 dernières années à partir de cette date.
        /// </summary>
        public List<int> Annees { get; set; } = new();

        /// <summary>
        /// OID du Site sélectionné, ou null pour « tous les sites ».
        /// </summary>
        public Guid? SiteOid { get; set; }

        /// <summary>
        /// Genre filtré (Masculin / Feminin), ou null pour les deux.
        /// </summary>
        public Sexe? Genre { get; set; }

        /// <summary>
        /// Type de contrat filtré (CDI / CDD / Stage), ou null pour tous.
        /// </summary>
        public TypeContrat? TypeContrat { get; set; }

        /// <summary>
        /// Calcule la date de référence à partir de la sélection d'années.
        /// </summary>
        public DateTime ResolveDateReference()
        {
            var anneeCourante = DateTime.Today.Year;
            if (Annees == null || Annees.Count == 0)
                return DateTime.Today;

            // Si l'année courante est dans la sélection, on prend aujourd'hui.
            if (Annees.Contains(anneeCourante))
                return DateTime.Today;

            // Sinon le 31/12 de la dernière année passée sélectionnée.
            var derniere = Annees.Where(a => a < anneeCourante).DefaultIfEmpty(anneeCourante - 1).Max();
            return new DateTime(derniere, 12, 31);
        }

        /// <summary>
        /// Construit une clé de cache stable pour ces filtres (TTL 5 min côté service).
        /// </summary>
        public string ToCacheKey() =>
            $"effectif_detaille|{string.Join('-', (Annees ?? new()).OrderBy(a => a))}" +
            $"|site={SiteOid}|genre={Genre}|contrat={TypeContrat}";
    }
}
