// =============================================================================
//  AnalyseEffectifFilterModel.cs
//  Tableau N°2 (Analyse de l'Effectif) — filtres saisis par l'utilisateur.
//
//  Périmètre : INTERNE (Salarie) ou EXTERNE (Interimaire) — toggle.
//  Mode      : Global (à dateRef) ou Moyen ((début + fin) / 2 sur l'année).
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>Mode de calcul de l'effectif (toggle haut du tableau).</summary>
    public enum EffectifMode
    {
        Global = 0,  // Effectif à la date de référence (instantané).
        Moyen  = 1   // Effectif moyen annuel = (début + fin) / 2.
    }

    /// <summary>Filtres pour le Tableau N°2 « Analyse de l'Effectif ».</summary>
    public sealed class AnalyseEffectifFilterModel
    {
        /// <summary>Type de personnel observé (Interne / Externe / Global).</summary>
        public PersonnelType Personnel { get; set; } = PersonnelType.Interne;

        /// <summary>Mode de calcul de l'effectif (Global / Moyen).</summary>
        public EffectifMode Mode { get; set; } = EffectifMode.Global;

        /// <summary>Année sélectionnée (UNE seule pour ce tableau, contrairement au N°1).</summary>
        public int Annee { get; set; } = DateTime.Today.Year;

        public Guid? SiteOid { get; set; }            // Site (INTERNE) ou StationService.Oid (EXTERNE)
        public Sexe? Genre { get; set; }
        public string? Segment { get; set; }          // Département pour INTERNE, BU pour EXTERNE
        public Guid? CategorieOid { get; set; }       // Categories pour INTERNE
        public AncienneteBucket? Anciennete { get; set; }

        // ── Filtres spécifiques EXTERNE (intérimaires) ──
        public Guid? SocieteInterimOid { get; set; }
        public Guid? PosteInterimaireOid { get; set; }

        /// <summary>
        /// Calcule la date de référence — toujours 31/12 de l'année sélectionnée,
        /// ou aujourd'hui si l'année est l'année courante.
        /// </summary>
        public DateTime ResolveDateReference()
        {
            if (Annee == DateTime.Today.Year)
                return DateTime.Today;
            return new DateTime(Annee, 12, 31);
        }

        /// <summary>Construit une clé de cache stable.</summary>
        public string ToCacheKey() =>
            $"analyse_effectif|p={Personnel}|m={Mode}|a={Annee}" +
            $"|site={SiteOid}|genre={Genre}|seg={Segment}" +
            $"|cat={CategorieOid}|anc={Anciennete}" +
            $"|soc={SocieteInterimOid}|poste={PosteInterimaireOid}";
    }
}
