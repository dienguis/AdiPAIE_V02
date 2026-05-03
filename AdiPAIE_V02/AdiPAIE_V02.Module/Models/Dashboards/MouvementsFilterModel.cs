// =============================================================================
//  MouvementsFilterModel.cs
//  Tableau N°3 (Mouvements — Arrivées / Départs).
//
//  Sources de données validées :
//    INTERNE :
//      - Arrivées = `Salarie.DateEmbauche` dans l'année
//      - Départs  = `Salarie.DateSortie` dans l'année + enum `MotifDepart`
//    EXTERNE :
//      - Mouvements = entité `MouvementInterimaire`
//        (TypeMouvement = Affectation / Départ / Changement station…)
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>Filtres pour le Tableau N°3 « Mouvements ».</summary>
    public sealed class MouvementsFilterModel
    {
        public PersonnelType Personnel { get; set; } = PersonnelType.Interne;
        public int Annee { get; set; } = DateTime.Today.Year;
        public Guid? SiteOid { get; set; }            // Site (INTERNE) / StationService (EXTERNE)
        public Sexe? Genre { get; set; }
        public string? Segment { get; set; }
        public Guid? CategorieOid { get; set; }

        public string ToCacheKey() =>
            $"mouvements|p={Personnel}|a={Annee}|site={SiteOid}|g={Genre}|seg={Segment}|cat={CategorieOid}";
    }
}
