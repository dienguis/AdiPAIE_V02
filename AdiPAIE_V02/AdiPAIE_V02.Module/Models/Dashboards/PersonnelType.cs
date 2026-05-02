// =============================================================================
//  PersonnelType.cs
//  Module « Tableaux de Bord RH » — distinction du périmètre du personnel.
//
//  - Interne : salariés de l'entreprise (CDI / CDD / Stage),
//              entité métier `Salarie`.
//  - Externe : prestataires / intérimaires,
//              entité métier `Interimaire` (+ `ContratInterim` pour le coût).
//  - Global  : union Interne ∪ Externe (utilisé par le Bilan Social, par ex.).
//
//  Rappel mission : chaque tableau précise à quel(s) périmètre(s) il s'applique.
// =============================================================================

namespace AdiPAIE_V02.Module.Models.Dashboards
{
    /// <summary>
    /// Périmètre du personnel ciblé par un tableau de bord RH.
    /// </summary>
    public enum PersonnelType
    {
        /// <summary>Salariés internes (CDI/CDD/Stage) — entité <c>Salarie</c>.</summary>
        Interne = 0,

        /// <summary>Prestataires / intérimaires — entité <c>Interimaire</c>.</summary>
        Externe = 1,

        /// <summary>Union Interne ∪ Externe.</summary>
        Global = 2
    }
}
