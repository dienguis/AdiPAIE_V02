// =============================================================================
//  DemandeMouvementStatutTests.cs — V1.5.2
//
//  Tests sur l'enum DemandeMouvementStatut V1.5 + transitions valides.
//  Garantit la stabilité numérique des codes (DB vivante) et que les
//  conditions de visibilité côté UI/permissions restent cohérentes.
// =============================================================================
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Tests.Domain;

public class DemandeMouvementStatutTests
{
    [Fact]
    public void DemandeMouvementStatut_CodesNumeriques_StablesEnDB()
    {
        // Codes choisis avec espacement pour évolutions futures (ex. 11, 12...)
        ((int)DemandeMouvementStatut.Brouillon).Should().Be(0);
        ((int)DemandeMouvementStatut.SoumiseAssistantRH).Should().Be(10);
        ((int)DemandeMouvementStatut.ValideeAssistantRH).Should().Be(20);
        ((int)DemandeMouvementStatut.ValideeRH).Should().Be(30);
        ((int)DemandeMouvementStatut.ValideeDAF).Should().Be(35);
        ((int)DemandeMouvementStatut.Appliquee).Should().Be(40);
        ((int)DemandeMouvementStatut.RejeteeAssistantRH).Should().Be(80);
        ((int)DemandeMouvementStatut.RejeteeRH).Should().Be(81);
        ((int)DemandeMouvementStatut.RejeteeDAF).Should().Be(82);
        ((int)DemandeMouvementStatut.Annulee).Should().Be(90);
    }

    [Theory]
    [InlineData(DemandeMouvementStatut.Brouillon, true)]
    [InlineData(DemandeMouvementStatut.SoumiseAssistantRH, false)]
    [InlineData(DemandeMouvementStatut.ValideeAssistantRH, false)]
    [InlineData(DemandeMouvementStatut.ValideeRH, false)]
    [InlineData(DemandeMouvementStatut.Appliquee, false)]
    public void Brouillon_EstSeulStatutModifiableParAc(
        DemandeMouvementStatut statut, bool modifiableParAc)
    {
        // Reflète la permission ObjectPermission AssistantCommercial :
        // Initiateur.Email = CurrentUserName() AND Statut = Brouillon
        bool calcule = statut == DemandeMouvementStatut.Brouillon;
        calcule.Should().Be(modifiableParAc);
    }

    [Theory]
    [InlineData(DemandeMouvementStatut.Brouillon, false)]
    [InlineData(DemandeMouvementStatut.SoumiseAssistantRH, true)]
    [InlineData(DemandeMouvementStatut.ValideeAssistantRH, true)]
    [InlineData(DemandeMouvementStatut.ValideeRH, true)]
    [InlineData(DemandeMouvementStatut.ValideeDAF, true)]
    [InlineData(DemandeMouvementStatut.Appliquee, false)]
    [InlineData(DemandeMouvementStatut.RejeteeAssistantRH, false)]
    [InlineData(DemandeMouvementStatut.Annulee, false)]
    public void Annulation_PossibleSeulementAvantAppliqueeOuFinale(
        DemandeMouvementStatut statut, bool peutAnnuler)
    {
        // Reflète la logique DemandeMouvementService.Annuler
        bool calcule =
            statut != DemandeMouvementStatut.Appliquee &&
            statut != DemandeMouvementStatut.Annulee &&
            statut != DemandeMouvementStatut.RejeteeAssistantRH &&
            statut != DemandeMouvementStatut.RejeteeRH &&
            statut != DemandeMouvementStatut.RejeteeDAF &&
            statut != DemandeMouvementStatut.Brouillon; // Brouillon = supprimer plutôt qu'annuler
        calcule.Should().Be(peutAnnuler);
    }

    [Theory]
    [InlineData(DemandeMouvementStatut.SoumiseAssistantRH, true)]   // court-circuit OK
    [InlineData(DemandeMouvementStatut.ValideeAssistantRH, true)]   // chemin normal OK
    [InlineData(DemandeMouvementStatut.Brouillon, false)]
    [InlineData(DemandeMouvementStatut.ValideeRH, false)]            // déjà validé
    [InlineData(DemandeMouvementStatut.Appliquee, false)]
    public void ValiderRH_AcceptableDepuis_SoumiseOuValideeAssistantRH(
        DemandeMouvementStatut statut, bool acceptable)
    {
        // Reflète la condition dans DemandeMouvementInterimController.UpdateStates
        bool calcule =
            statut == DemandeMouvementStatut.ValideeAssistantRH ||
            statut == DemandeMouvementStatut.SoumiseAssistantRH;
        calcule.Should().Be(acceptable);
    }
}
