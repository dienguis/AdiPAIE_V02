# AdiPAIE_V02.Tests

Projet de tests unitaires xUnit pour SunuPaie / AdiPAIE V2.

## Stack

- **xUnit 2.9** — framework de test
- **FluentAssertions 6.12** — assertions lisibles (`.Should().Be(...)`)
- **Moq 4.20** — mocking d'`IObjectSpace`, `ILogger`, etc.
- **DevExpress.Xpo 25.1.9** — pour tests intégration avec `InMemoryDataStore` (à venir)

## Lancement

```bash
# Depuis la racine du repo
dotnet test AdiPAIE_V02/AdiPAIE_V02.Tests/AdiPAIE_V02.Tests.csproj

# Ou depuis Visual Studio
# Test Explorer → Run All
```

## Couverture actuelle (V1.5.2 — fondation)

| Catégorie | Fichier | Tests |
|-----------|---------|-------|
| Domain enums | `BulletinStatutTests.cs` | Stabilité codes numériques + sémantique EstPublie |
| Domain enums | `DemandeMouvementStatutTests.cs` | Stabilité codes + transitions valides AC/RH/Annul |
| Domain enums | `TypeMouvementInterimTests.cs` | Mapping vers MouvementInterimaireType + validations |

## À ajouter (sprints futurs)

- **Tests intégration XPO** : `InMemoryDataStoreProvider` + tests sur `BulletinPublicationService`,
  `DemandeMouvementService` avec une vraie session XPO en RAM
- **Tests de calcul paie** : `BulletinModeleService.CreerParDefaut` (résultats déterministes
  pour un salarié donné)
- **Tests workflow Recrutement** : `DemandeRecrutementInterim.SoumettreN1`, etc.
- **Tests dashboards** : services `EffectifDetailleDashboardService`,
  `RemunerationDashboardService` avec fixtures
- **Tests permissions** : `RolesGRHInitializer.Initialize` idempotence sur OS mocké

## Pattern recommandé pour tests intégration XPO

```csharp
public class BulletinPublicationServiceIntegrationTests : IDisposable
{
    private readonly Session _session;

    public BulletinPublicationServiceIntegrationTests()
    {
        var dictionary = new ReflectionDictionary();
        dictionary.GetDataStoreSchema(
            typeof(Bulletin), typeof(Salarie), typeof(BulletinLigne), ...);
        var dataStore = new InMemoryDataStore(AutoCreateOption.DatabaseAndSchema);
        var dataLayer = new SimpleDataLayer(dictionary, dataStore);
        _session = new Session(dataLayer);
    }

    [Fact]
    public void Publier_FaitPasserAuStatutEnvoye()
    {
        // Arrange : créer Salarie + Bulletin Validé
        // Act : appeler Publier
        // Assert : Statut == Envoye, DatePublication != null, PdfArchive != null
    }

    public void Dispose() => _session.Dispose();
}
```

## CI/CD (à ajouter)

Workflow GitHub Actions `.github/workflows/build.yml` :

```yaml
name: Build & Test
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - run: dotnet restore
      - run: dotnet build --no-restore --configuration Release
      - run: dotnet test --no-build --configuration Release --logger "trx;LogFileName=test_results.trx"
```

Note : les builds GitHub Actions auront besoin du DevExpress NuGet feed configuré
(token NuGet en secret repo) car DevExpress.ExpressApp 25.1.9 est sur un feed privé.
