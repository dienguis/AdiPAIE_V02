// =============================================================================
//  BulletinCreationTests.cs — V1.5.2 — DÉSACTIVÉ
//
//  Exemple de tests d'intégration XPO (avec InMemoryDataStore) pour tester
//  la création de Bulletin. Conservé pour référence si on décide d'investir
//  dans des tests d'intégration plus tard.
//
//  Pour activer :
//    1. Décommenter le code ci-dessous
//    2. Au premier run, lire les erreurs « class is not registered » et
//       ajouter les types correspondants à dictionary.GetDataStoreSchema(...)
//    3. Itérer jusqu'à ce que tout passe
//
//  Pourquoi désactivé : la fondation V1.5.2 se limite aux tests unitaires
//  d'enum + transitions (rapides, sans dépendance). L'intégration XPO
//  demande un sprint dédié (TestObjectSpaceFactory + injection PDF, etc.).
// =============================================================================

/*
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;

namespace AdiPAIE_V02.Tests.BusinessObjects;

public class BulletinCreationTests : IDisposable
{
    private readonly Session _session;

    public BulletinCreationTests()
    {
        var dictionary = new ReflectionDictionary();
        dictionary.GetDataStoreSchema(typeof(Bulletin), typeof(Salarie));
        var dataStore = new InMemoryDataStore(AutoCreateOption.DatabaseAndSchema);
        var dataLayer = new SimpleDataLayer(dictionary, dataStore);
        _session = new Session(dataLayer);
    }

    public void Dispose() => _session.Dispose();

    [Fact]
    public void Bulletin_AfterConstruction_InitialiseAnneeEtMois()
    {
        var bulletin = new Bulletin(_session);
        bulletin.Annee.Should().Be(DateTime.Today.Year);
        bulletin.Mois.Should().Be(DateTime.Today.Month);
    }

    // ... etc — 7 autres tests (voir historique Git pour le code complet)
}
*/
