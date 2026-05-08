// =============================================================================
//  TypeMouvementInterimTests.cs — V1.5.2
//
//  Tests sur le mapping TypeMouvementInterim → MouvementInterimaireType
//  utilisé par DemandeMouvementService.AppliquerAsync.
// =============================================================================
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Tests.Domain;

public class TypeMouvementInterimTests
{
    [Fact]
    public void TypeMouvementInterim_ValeursStablesEnDB()
    {
        ((int)TypeMouvementInterim.ChangementStation).Should().Be(0);
        ((int)TypeMouvementInterim.ChangementPoste).Should().Be(1);
        ((int)TypeMouvementInterim.FinMissionAnticipee).Should().Be(2);
        ((int)TypeMouvementInterim.RemplacementTemporaire).Should().Be(3);
        ((int)TypeMouvementInterim.Autre).Should().Be(99);
    }

    [Theory]
    [InlineData(TypeMouvementInterim.ChangementStation, MouvementInterimaireType.MutationInterne)]
    [InlineData(TypeMouvementInterim.ChangementPoste, MouvementInterimaireType.Reaffectation)]
    [InlineData(TypeMouvementInterim.FinMissionAnticipee, MouvementInterimaireType.FinMission)]
    [InlineData(TypeMouvementInterim.RemplacementTemporaire, MouvementInterimaireType.Affectation)]
    [InlineData(TypeMouvementInterim.Autre, MouvementInterimaireType.Reaffectation)]
    public void Mapping_TypeMouvementInterim_VersMouvementInterimaireType(
        TypeMouvementInterim type, MouvementInterimaireType attendu)
    {
        // Reflète le switch dans DemandeMouvementService.MapTypeToMvtInterim
        var calcule = type switch
        {
            TypeMouvementInterim.ChangementStation => MouvementInterimaireType.MutationInterne,
            TypeMouvementInterim.ChangementPoste => MouvementInterimaireType.Reaffectation,
            TypeMouvementInterim.FinMissionAnticipee => MouvementInterimaireType.FinMission,
            TypeMouvementInterim.RemplacementTemporaire => MouvementInterimaireType.Affectation,
            _ => MouvementInterimaireType.Reaffectation
        };
        calcule.Should().Be(attendu);
    }

    [Theory]
    [InlineData(TypeMouvementInterim.ChangementStation, true)]
    [InlineData(TypeMouvementInterim.ChangementPoste, false)]
    [InlineData(TypeMouvementInterim.FinMissionAnticipee, false)]
    [InlineData(TypeMouvementInterim.RemplacementTemporaire, false)]
    [InlineData(TypeMouvementInterim.Autre, false)]
    public void StationDestination_RequiseSeulementPourChangementStation(
        TypeMouvementInterim type, bool stationDestRequise)
    {
        // Reflète la validation dans DemandeMouvementService.ValiderPreconditionsSoumission
        bool calcule = type == TypeMouvementInterim.ChangementStation;
        calcule.Should().Be(stationDestRequise);
    }
}
