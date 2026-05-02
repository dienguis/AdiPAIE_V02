// =============================================================================
//  IEffectifDetailleDashboardService.cs
//  Tableau N°1 (Effectif détaillé) — interface du service métier.
//
//  Le service est agnostique de l'environnement XAF Blazor : il prend un
//  IObjectSpace en paramètre, fourni par la page Razor (qui sait comment
//  l'obtenir via INonSecuredObjectSpaceFactory).
// =============================================================================

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Models.Dashboards;
using DevExpress.ExpressApp;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services.Dashboards
{
    /// <summary>
    /// Service du Tableau N°1 « Effectif détaillé ».
    /// Périmètre : Personnel INTERNE (entité <see cref="Salarie"/>).
    /// </summary>
    public interface IEffectifDetailleDashboardService
    {
        /// <summary>
        /// Calcule l'ensemble des données du tableau pour les filtres donnés.
        /// Utilise un cache mémoire (TTL 5 min) sur la clé issue du filtre.
        /// </summary>
        /// <param name="filter">Filtres saisis par l'utilisateur.</param>
        /// <param name="objectSpace">IObjectSpace ouvert par l'appelant (using).</param>
        EffectifDetailleDto GetData(
            EffectifDetailleFilterModel filter,
            IObjectSpace objectSpace);

        /// <summary>
        /// Liste des années disponibles pour le filtre Année (multi).
        /// </summary>
        List<int> GetAnneesDisponibles(IObjectSpace objectSpace);

        /// <summary>Liste des sites actifs pour le filtre Site.</summary>
        List<Site> GetSitesActifs(IObjectSpace objectSpace);

        /// <summary>Liste des genres possibles (Masculin / Feminin).</summary>
        IReadOnlyList<Sexe> GetGenresDisponibles();

        /// <summary>Liste des types de contrat possibles (CDI / CDD / Stage).</summary>
        IReadOnlyList<TypeContrat> GetTypesContratDisponibles();

        /// <summary>Force l'invalidation du cache pour ces filtres.</summary>
        void InvalidateCache(EffectifDetailleFilterModel filter);
    }
}
