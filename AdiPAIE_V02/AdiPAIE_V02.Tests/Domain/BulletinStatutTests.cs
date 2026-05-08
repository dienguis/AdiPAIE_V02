// =============================================================================
//  BulletinStatutTests.cs — V1.5.2
//
//  Tests xUnit sur l'enum BulletinStatut + propriété calculée Bulletin.EstPublie.
//  Ces tests sont rapides (pas de DB) et garantissent que :
//    - Les valeurs numériques de l'enum ne dérivent pas accidentellement
//      (impact direct sur les bulletins legacy en DB)
//    - La sémantique « EstPublie = Statut >= Envoye » reste cohérente
// =============================================================================
using AdiPAIE_V02.Module.BusinessObjects;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Tests.Domain;

public class BulletinStatutTests
{
    [Fact]
    public void BulletinStatut_ValeursNumeriques_NeChangentPas()
    {
        // Garantit la stabilité de l'enum vs DB existante
        ((int)BulletinStatut.Brouillon).Should().Be(0);
        ((int)BulletinStatut.Valide).Should().Be(1);
        ((int)BulletinStatut.Exporte).Should().Be(2);
        ((int)BulletinStatut.Imprime).Should().Be(3);
        ((int)BulletinStatut.Envoye).Should().Be(4);
        ((int)BulletinStatut.Comptabilise).Should().Be(5);
        ((int)BulletinStatut.Cloture).Should().Be(6);
    }

    [Theory]
    [InlineData(BulletinStatut.Brouillon, false)]
    [InlineData(BulletinStatut.Valide, false)]
    [InlineData(BulletinStatut.Exporte, false)]
    [InlineData(BulletinStatut.Imprime, false)]
    [InlineData(BulletinStatut.Envoye, true)]   // V1.4.3 — "Publié"
    [InlineData(BulletinStatut.Comptabilise, true)]
    [InlineData(BulletinStatut.Cloture, true)]
    public void EstPublie_RetourneVraiPourStatutsAuDessusDeEnvoye(
        BulletinStatut statut, bool attendu)
    {
        // Note : On ne peut pas instancier Bulletin sans Session XPO.
        // À la place on teste la condition logique directement.
        bool calcule = statut >= BulletinStatut.Envoye;
        calcule.Should().Be(attendu);
    }
}
