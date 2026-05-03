# 📊 Tableaux de Bord RH — SunuPaie · ELTON Oil Company

Module Blazor Server (.NET 8 / DevExpress XAF 25.1) implémentant **6 tableaux
de bord analytiques** pour le pilotage RH : effectif, mouvements, rémunération,
absences, bilan social.

> **Branche Git** : `feature/dashboards-rh`
> **Mission** : achevée en 11 étapes (cf. `MISSION_STATE.md`)
> **Charte visuelle** : ELTON Oil — Navy `#142E4D`, Orange `#F18A1C`, Rouge `#E63946`

---

## 📑 Table des matières

- [Partie 1 — Pour la DRH (utilisateur métier)](#partie-1--pour-la-drh-utilisateur-métier)
  - [Comment accéder aux dashboards](#comment-accéder-aux-dashboards)
  - [Qui peut voir quoi (RBAC)](#qui-peut-voir-quoi-rbac)
  - [Les 6 tableaux en un coup d'œil](#les-6-tableaux-en-un-coup-dœil)
  - [Boutons communs à tous les tableaux](#boutons-communs-à-tous-les-tableaux)
- [Partie 2 — Pour les développeurs](#partie-2--pour-les-développeurs)
  - [Architecture](#architecture)
  - [Build & lancement](#build--lancement)
  - [Dépendances NuGet ajoutées](#dépendances-nuget-ajoutées)
  - [Patterns clés](#patterns-clés)
  - [Pièges connus](#pièges-connus)
- [Partie 3 — Annexes](#partie-3--annexes)
  - [Scripts SQL d'audit](#scripts-sql-daudit)
  - [Limitations connues](#limitations-connues)
  - [Évolutions futures](#évolutions-futures)

---

## Partie 1 — Pour la DRH (utilisateur métier)

### Comment accéder aux dashboards

1. Connectez-vous à SunuPaie (`https://[serveur]:44318`).
2. Dans le menu de gauche, cliquez sur **« GRH - Tableaux de Bord »**.
3. Cliquez sur **« Ouvrir les Tableaux de Bord »** (bouton orange).
4. Une **nouvelle fenêtre** s'ouvre avec la page d'accueil et 6 cartes cliquables.
5. Vos onglets SunuPaie restent ouverts en parallèle — pas besoin de revenir en arrière.

### Qui peut voir quoi (RBAC)

| Rôle | Accès aux dashboards |
|---|---|
| **`Administrators`** | ✅ Tout |
| **`RH_Manager`** (DG, COMEX, consultation pure) | ✅ Tout |
| **`RH`** (équipe RH opérationnelle) | ✅ Tout |
| **`DAF`** (direction financière) | ✅ Tout |
| Autres rôles (`Default`, `Comptable`, `Assistant`…) | ❌ Redirection vers la page de login |

> **Demande d'accès** : un administrateur doit ajouter le rôle `RH_Manager`
> (ou `RH`/`DAF`) à votre compte utilisateur via Admin → Users → Roles.

### Les 6 tableaux en un coup d'œil

| # | Tableau | Périmètre | À quoi ça sert |
|---|---|---|---|
| **1** | Effectif détaillé | INTERNE | Pyramide des âges, répartition par tranche × catégorie pro, distinction H/F |
| **2** | Analyse de l'Effectif | INTERNE + EXTERNE (toggle) | Effectif global/moyen, évolution 8 ans, KPI démographiques, 5 bar charts |
| **3** | Mouvements | INTERNE + EXTERNE (toggle) | Arrivées et départs : par mois, motif, site, catégorie ; solde net ; taux |
| **4** | Rémunération | INTERNE + EXTERNE (toggle) | Masse salariale, coût employeur, **égalité H/F par segment et catégorie** |
| **5** | Suivi des Absences | INTERNE | Congés et arrêts, taux d'absentéisme, Top 10 absents, par famille |
| **6** | Bilan Social Mensuel | INTERNE | Synthèse 12 mois × 8 indicateurs DTSS (effectif, masse, charges, absences) |

### Boutons communs à tous les tableaux

Dans le header sombre de chaque tableau, à droite :

| Icône | Action |
|---|---|
| ↻ **Refresh** | Vide le cache (TTL 5 min) et recharge depuis la base |
| 📄 **Export PDF** | Télécharge un PDF A4 paysage (en-tête ELTON, 1 page imprimable) |
| 📊 **Export Excel** | Télécharge un `.xlsx` avec **onglet Synthèse en 1ʳᵉ position** + onglets détaillés (KPI, tableaux, évolution mensuelle) + **DataBars orange** dans les colonnes Total |
| ❓ **Aide** | Ouvre la page d'aide spécifique du tableau dans un nouvel onglet |
| ← **Retour** | Retour à la page d'accueil `/dashboards` |

> **Astuce** : l'export Excel a maintenant un onglet **« Synthese »** en
> premier qui regroupe les KPI + les tableaux clés sur une seule page,
> idéal pour copier-coller dans un rapport Word ou PowerPoint.

---

## Partie 2 — Pour les développeurs

### Architecture

```
AdiPAIE_V02/
├── AdiPAIE_V02.Module/                                  ← Logique métier (XPO, services)
│   ├── Domain/DomainEnums.cs                            ← enums (MotifDepart, CongeStatut...)
│   ├── Models/Dashboards/                               ← DTOs et FilterModels (12 fichiers)
│   │   ├── PersonnelType.cs
│   │   ├── AgeBucket.cs / AncienneteBucket.cs
│   │   ├── EffectifDetailleDto.cs + FilterModel.cs       (Tab 1)
│   │   ├── AnalyseEffectifDto.cs + FilterModel.cs        (Tab 2)
│   │   ├── MouvementsDto.cs + FilterModel.cs             (Tab 3)
│   │   ├── RemunerationDto.cs + FilterModel.cs           (Tab 4)
│   │   ├── SuiviAbsencesDto.cs + FilterModel.cs          (Tab 5)
│   │   └── BilanSocialDto.cs + FilterModel.cs            (Tab 6)
│   ├── Services/Dashboards/                             ← Services + interfaces
│   │   ├── IXxxDashboardService.cs + XxxDashboardService.cs (×6)
│   │   ├── IDashboardExcelExportService.cs + impl. ClosedXML
│   │   └── IDashboardPdfExportService.cs + impl. QuestPDF
│   ├── NonPersistent/DashboardsRHMenu.cs                ← Entrée menu XAF
│   └── Controllers/RH/DashboardsRHNavigationController.cs ← Bouton ouverture
│
└── AdiPAIE_V02.Blazor.Server/                           ← UI Blazor
    ├── Services/DashboardAuthHelper.cs                   ← RBAC partagé
    ├── Pages/_Host.cshtml                                ← + Bootstrap Icons CDN
    ├── Pages/Dashboards/                                 ← 7 pages razor
    │   ├── DashboardHome.razor                           (page d'accueil 6 cartes)
    │   ├── Effectif/EffectifDetailleDashboard.razor       (Tab 1)
    │   ├── Effectif/AnalyseEffectifDashboard.razor        (Tab 2)
    │   ├── Mouvements/MouvementsDashboard.razor           (Tab 3)
    │   ├── Remuneration/RemunerationDashboard.razor       (Tab 4)
    │   ├── Absences/SuiviAbsencesDashboard.razor          (Tab 5)
    │   └── BilanSocial/BilanSocialDashboard.razor         (Tab 6)
    ├── Startup.cs                                        ← DI + AddMemoryCache
    └── wwwroot/
        ├── css/dashboards-elton.css                      ← Charte CSS partagée
        └── help/dashboards/                              ← 7 pages HTML d'aide
            ├── dashboards-help.css
            ├── index.html
            └── effectif-detaille.html, analyse-effectif.html, ...

docs/dashboards/
├── README.md (ce fichier)
├── CHANGELOG.md          ← audit log avec hashes Git par étape
├── MISSION_STATE.md      ← mémoire de travail (étapes, décisions, pièges)
└── SPEC_PowerBI_DAX_to_SQL.sql  ← spec utilisateur initiale

sql/dashboards/
├── 01_effectif_detaille.sql    (10 requêtes)
├── 02_analyse_effectif.sql     (12 requêtes)
├── 03_mouvements.sql           (17 requêtes : 10 INT + 7 EXT)
├── 04_remuneration.sql         (10 requêtes : 7 INT + 3 EXT)
├── 05_suivi_absences.sql       (8 requêtes)
├── 06_bilan_social.sql         (3 blocs)
└── install.sql                 ← script consolidé (ToC + 6 modules)
```

### Build & lancement

```powershell
cd C:\Dev\AdiPAIE_V02
dotnet restore
dotnet build
# Lancement (depuis Visual Studio : F5)
dotnet run --project AdiPAIE_V02/AdiPAIE_V02.Blazor.Server
# URL : https://localhost:44318
```

### Dépendances NuGet ajoutées

| Package | Version | Usage | Licence |
|---|---|---|---|
| `Microsoft.Extensions.Caching.Memory` | 8.0.1 | Cache KPI (TTL 5 min) | MIT |
| `ClosedXML` | 0.105.0 | Export Excel (déjà présent dans le projet) | MIT |
| `QuestPDF` | 2024.7.3 | Export PDF | **Community gratuit < 1 M$ CA, sinon Professional ≈ 699 $/an** ⚠️ |

> **⚠️ Licence QuestPDF** : pour la prod ELTON (CA > 1 M$), passer la licence
> en `Professional` via `QuestPDF.Settings.License = LicenseType.Professional`
> + clé d'activation. Cf. https://www.questpdf.com/pricing.html

### Patterns clés

#### Pattern Service + DI

Chaque tableau a son service avec interface :
```csharp
public interface IRemunerationDashboardService
{
    RemunerationDto GetData(RemunerationFilterModel filter, IObjectSpace os);
    List<int> GetAnneesDisponibles(IObjectSpace os);
    // ... lookups
    void InvalidateCache(RemunerationFilterModel filter);
}
```

Enregistrement DI dans `Startup.cs` :
```csharp
services.AddMemoryCache();
services.AddScoped<IRemunerationDashboardService, RemunerationDashboardService>();
```

Injection dans la page Razor :
```razor
@inject IRemunerationDashboardService Service
@inject INonSecuredObjectSpaceFactory ObjectSpaceFactory
```

#### Pattern Cache mémoire

Chaque service a un cache TTL 5 min sur `filter.ToCacheKey()` :
```csharp
var key = filter.ToCacheKey();
if (_cache.TryGetValue<RemunerationDto>(key, out var cached) && cached != null)
    return cached;

var dto = ComputeForExterne(filter, os);
_cache.Set(key, dto, TimeSpan.FromMinutes(5));
return dto;
```

Le bouton ↻ Refresh appelle `Service.InvalidateCache(_filter)` puis `LoadData()`.

#### Pattern RBAC (helper partagé)

`OnInitialized()` dans chaque dashboard :
```csharp
protected override void OnInitialized()
{
    if (!DashboardAuthHelper.CanAccessDashboards(HttpCtx, ObjectSpaceFactory))
    {
        Nav.NavigateTo("/LoginPage", forceLoad: true);
        return;
    }
    LoadLookups();
    LoadData();
}
```

#### Pattern sentinelle DateSortie

Sur INTERNE, la sortie d'un salarié est filtrée par `DateSortie >= 1900-01-01`
pour gérer `DateTime.MinValue` (XPO null) ET les epochs Excel d'import (`1899-12-30`) :
```csharp
private static readonly DateTime SortieSentinelle = new(1900, 1, 1);
// ...
.Where(s => s.DateSortie < SortieSentinelle || s.DateSortie > dateRef)
```

#### Pattern téléchargement (Excel/PDF)

Service produit `(byte[] bytes, string fileName)`, Razor déclenche le téléchargement via JS interop existant (`window.AdiPAIE.downloadFile`) :
```csharp
var (bytes, fileName) = ExcelExport.ExportRemuneration(_data, _filter);
await JS.InvokeVoidAsync("AdiPAIE.downloadFile", fileName,
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    Convert.ToBase64String(bytes));
```

### Pièges connus

1. **`@attribute [Authorize]` ne fonctionne PAS** — App.razor utilise `<RouteView>` et non `<AuthorizeRouteView>`. **Utiliser le helper `DashboardAuthHelper`** (cf. pattern RBAC).

2. **Ne pas mettre `@{ var x = ... }` dans un `else` Razor** — cause RZ1010. Écrire `var x = ...;` directement (déjà en mode code).

3. **`SqlDateTime overflow`** sur `DateTime.MinValue` — utiliser la sentinelle `1900-01-01` et filtrer côté mémoire (`.ToList()` puis `.Where()`).

4. **DevExpress 25.1** : `GridTextAlignment.Right` (pas End), pas de `WordWrapMode`, pas de `ChartElementFormat.Percent`. Stacked bars : préférer HTML/CSS pour robustesse.

5. **`INonSecuredObjectSpaceFactory`** existe dans **2 namespaces** : `DevExpress.ExpressApp` (le bon, utilisé par les Razor) et `DevExpress.ExpressApp.Blazor.Services` (à éviter).

6. **DxComboBox / DxTagBox** : toujours expliciter `TData` et `TValue`.

7. **Sandbox Linux sans dotnet** : pas de build local possible côté agent IA. La validation passe par l'utilisateur sur Windows + Visual Studio.

---

## Partie 3 — Annexes

### Scripts SQL d'audit

Les fichiers `sql/dashboards/0X_*.sql` ne sont **pas exécutés par l'app** — ce sont les **équivalents SQL** des KPI calculés en C#/XPO. Usage :

- **Audit** : DBA exécute SQL et compare avec le rendu écran
- **Plan B** : si l'app est down, requêtes utilisables directement sur SQL Server
- **Power BI / Excel direct** : analyste branche Power BI sur la base
- **Tests d'intégration** : base pour vérifier la parité C# ↔ SQL
- **Documentation métier** : 5 lignes SQL > 300 lignes C# pour comprendre

Le fichier consolidé `sql/dashboards/install.sql` regroupe les 6 modules avec
table des matières et paramètres globaux (`@annee`, `@dateRef`).

### Limitations connues

| Limitation | Impact | Mitigation |
|---|---|---|
| `Interimaire` n'a pas de champ `Sexe` | KPI %F/%H et filtre Genre vides en EXTERNE | Ajouter migration XPO si besoin métier |
| Bilan Social — entités Formation, Accidents, Disciplinaire absentes | Colonnes correspondantes affichent « — » | Créer ces entités progressivement |
| Cache mémoire (pas Redis) | KPI partagés 5 min entre utilisateurs sur le même process | Si charge > 100 users, basculer sur Redis |
| `BulletinLigne` (~22 991 lignes) chargé en mémoire pour la décomposition rubrique | Légère lenteur au 1er calcul | Au-delà de 50 000 lignes, basculer sur vue SQL |
| Licence QuestPDF Community pour ELTON | Risque légal si CA > 1 M$ | Acheter Professional ≈ 699 $/an |
| Bootstrap Icons via CDN public jsdelivr | Échec si serveur sans Internet | Hébergement local `wwwroot/lib/bootstrap-icons/` |

### Évolutions futures

- **Étape 8 (proposée)** : créer entités `Accident`, `Formation`, `MesureDisciplinaire` pour compléter le Bilan Social (les colonnes deviendront chiffrées au lieu de « — »)
- **Heatmap absences** (jour × mois) sur Tab 5
- **Charts SVG dans le PDF** au lieu de tableaux (via SkiaSharp)
- **Endpoint MVC pour gros exports** (au lieu de SignalR base64) pour fichiers > 5 MB
- **Notifications push** quand un KPI dépasse un seuil (ex: turnover > 30%)
- **Export Power BI .pbix** — générer un dataset .pbix prêt à ouvrir
- **Mode kiosque** plein écran pour TV de hall RH (rotation auto entre les 6 tableaux)

---

## 📞 Contact / maintenance

- **Branche Git** : `feature/dashboards-rh` (à merger sur `main` après validation prod)
- **Documentation interne** :
  - `docs/dashboards/CHANGELOG.md` — audit log par étape avec hashes Git
  - `docs/dashboards/MISSION_STATE.md` — mémoire de travail (décisions verrouillées, pièges, étapes restantes)
  - `docs/dashboards/SPEC_PowerBI_DAX_to_SQL.sql` — spec initiale utilisateur
- **Pages d'aide en ligne** : `https://[serveur]:44318/help/dashboards/`

_Mission terminée — ELTON Oil Company, mai 2026._
