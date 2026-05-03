# CHANGELOG — Module « Tableaux de Bord RH »

> **🤖 NOTE pour Claude (futur-moi) :** lire **EN PREMIER** le fichier
> [`MISSION_STATE.md`](./MISSION_STATE.md) — il contient l'état complet,
> les décisions verrouillées, les pièges connus et l'étape suivante. Ce
> CHANGELOG est l'audit log historique ; le STATE est la mémoire de
> travail.

Journal des modifications apportées dans le cadre de la mission de génération
du module **Tableaux de Bord RH** sur la branche `feature/dashboards-rh`.

Chaque entrée précise :

- la date/heure et l'étape concernée ;
- les fichiers créés / modifiés / supprimés (avec sauvegarde `.bak` dans
  `docs/dashboards/backup/<YYYY-MM-DD_HHmm>/` pour toute modification de
  fichier existant) ;
- le hash Git du commit associé (renseigné après le commit côté Windows) ;
- la commande Git de rollback exacte ;
- la personne ayant validé le commit.

> **Format de date :** `YYYY-MM-DD HHmm` (heure locale de l'opérateur).
> **Convention de message de commit :** `feat(dashboards): ...`,
> `chore(dashboards): ...`, `docs(dashboards): ...`, `fix(dashboards): ...`.

---

## [V1.1 — Sprint 1A] 2026-05-03 0930 — Modèle Site + UniteOrganisationnelle (cohabitation)

**Stratégie de migration** : pas de big-bang. Les nouvelles entités/champs
**coexistent avec les anciennes** (Station, BU, EstDG) pour ne rien casser.
La suppression définitive aura lieu en Sprint 1D quand les dashboards auront
basculé sur le nouveau modèle.

### Fichiers créés/modifiés (3)

| Fichier | Type | Nature |
|---|---|---|
| `BusinessObjects/Site.cs` | modif | + enum `TypeSite` (StationService/Siege/Depot/Autre) + champ `Type` + collection `Unites` + collection `ContratsInterim` |
| `BusinessObjects/RH/UniteOrganisationnelle.cs` | nouveau | Entité hiérarchique récursive (Site/Parent/Enfants) avec enum `TypeUnite` (BU/Departement/Segment/Autre) + Palette + helpers Niveau/Chemin |
| `BusinessObjects/RH/Interimaire.cs` | modif | `ContratInterim` + `Site` (FK) + `Unites` (collection N-N) ; `MouvementInterimaire` + `SiteOrigineV1` + `UniteOrigineV1` + `SiteDestinationV1` + `UniteDestinationV1` |

### Convention de nommage cohabitation

Les nouveaux champs sont suffixés `V1` (ex: `SiteOrigineV1`) ou marqués
`(V1.1)` dans le `XafDisplayName` pour les distinguer visuellement des
anciens champs en attendant la suppression définitive.

### Sprint 1B (à suivre)

- Updater seed démo avec préfixe `DEMO_`
- Controller XAF « Vider données démo »
- Flag `appsettings.json` → `Dashboards:SeedDemoData`
- Étendre RBAC pour `UniteOrganisationnelle`

### Sprints 1C / 1D / 1E (cf. tâches V1.1)

- 1C : Adapter les 6 dashboards
- 1D : Supprimer StationService / BusinessUnitStation / BusinessUnitType
- 1E : Adapter les 6 SQL + install.sql + README

---

## [V1.1 — Phase 1 abandonnée] 2026-05-03 0830 — Référentiel BusinessUnitType (regroupement transversal des BU)

**⚠️ Cette Phase 1 est ABANDONNÉE** au profit du modèle Sprint 1A
(Site + UniteOrganisationnelle) qui est plus complet et couvre les
besoins ELTON révélés ensuite (Direction Générale + dépôts +
multi-affectation segments). Le code BusinessUnitType existe mais
sera supprimé au Sprint 1D.

---

## [V1.1 — Phase 1 originale] 2026-05-03 0830 — Référentiel BusinessUnitType (regroupement transversal des BU)

**Constat utilisateur** : impossible de produire des KPI cross-stations
(« Boutique = somme de toutes les Boutique de toutes les stations »)
car chaque `BusinessUnitStation` est propre à une station et le seul
lien transversal était le texte du `Libelle` (sensible casse, espaces,
variantes orthographiques).

### Décision (Option B validée)

Création d'un **référentiel partagé `BusinessUnitType`** avec FK depuis
`BusinessUnitStation` → permet le GROUP BY robuste sur l'OID du Type.

### Implémentation Phase 1 (modèle + seed + migration)

**Nouvelle entité `BusinessUnitType`** (`BusinessObjects/RH/BusinessUnitType.cs`) :
- `Code` (unique, indexed) — clé technique normalisée (BOUTIQUE, PISTE…)
- `Libelle` — affichage UX (Boutique, Piste, E-Service…)
- `Palette` — **enum `CouleurPalette` rendue en combobox visuelle** avec
  carrés colorés Unicode dans les libellés (🟧 Orange ELTON, 🟦 Navy ELTON,
  🟥 Rouge ELTON, 🟦 Bleu clair, 🟩 Vert, 🟪 Violet, 🟨 Jaune, 🟫 Marron,
  🌸 Rose, 🩶 Gris). 11 valeurs y compris « ⬛ Aucune » (défaut navy).
- `CouleurHex` — propriété calculée `NonPersistent` qui retourne le hex
  associé via `CouleurPaletteHelper.GetHex()` — utilisée par les services
  dashboards pour styler les bar charts.
- `Ordre` — tri d'affichage
- `Actif` — désactivation sans suppression
- Association inverse `BUType-BUs` → `XPCollection<BusinessUnitStation>`
- Navigation : « GRH - Administration »

**Avantage du choix enum + emoji vs champ texte hex** :
- ✅ Combobox visuelle native (l'utilisateur voit le carré coloré)
- ✅ Pas de risque de typo (pas de saisie « ##F18A1C » ou « rgb(241,138,28) »)
- ✅ Robuste à l'évolution (ajouter une couleur = ajouter une valeur enum + une ligne dans le helper)
- ✅ Auto-localisable via `[XafDisplayName]`

**FK ajoutée sur `BusinessUnitStation`** :
- `public BusinessUnitType Type { get; set; }`
- Association `BUType-BUs`
- **Pas de RuleRequiredField** pour le moment (transition douce — sera
  activé en V1.2 une fois tous les BU historiques rattachés)

**Updater.cs** — 3 nouvelles méthodes idempotentes :
1. `EnsureBusinessUnitTypesSeed()` — crée les 4 types initiaux s'ils
   n'existent pas (Boutique, Piste, E-Service, Espace Auto)
2. `MigrateBUsToTypes()` — pour chaque BU sans Type, assigne le bon
   Type en matchant le Libelle (case-insensitive, trim, variantes
   « Espace Auto »/« EspaceAuto »)
3. `GrantBusinessUnitTypeReadAccess()` — étend les permissions Read
   sur `BusinessUnitType` aux rôles RH_Manager / RH / DAF

### Fichiers créés / modifiés (3)

| Fichier | Type | Nature |
|---|---|---|
| `BusinessObjects/RH/BusinessUnitType.cs` | nouveau | Entité référentiel |
| `BusinessObjects/RH/StationService.cs` | modif | + FK `Type` sur `BusinessUnitStation` |
| `DatabaseUpdate/Updater.cs` | modif | + 3 méthodes (seed, migration, RBAC) |

### Test de migration au démarrage

Après build & lancement de l'app :
1. Vérifier dans XAF → GRH - Administration → "Type de Business Unit"
   que les 4 types sont créés
2. Vérifier dans XAF → GRH - Intérimaires → Stations → ouvrir une
   station → liste BUs → chaque BU a maintenant un `Type` rattaché
3. Si une BU n'a pas de Type, c'est que son Libelle ne matche aucun
   type seed → corriger manuellement (assigner le bon Type, ou créer
   un nouveau Type si besoin)

### Phase 2 (à venir, ~2h)

Adapter les dashboards pour exploiter le nouveau Type BU :
- Tab 2 (Analyse Effectif EXTERNE) — + filtre « Type BU »
- Tab 3 (Mouvements EXTERNE) — + bar chart « par Type BU »
- Tab 4 (Rémunération EXTERNE) — + tableau Égalité par Type BU ⭐
- Tab 6 (Bilan Social ligne intérimaires) — + ventilation par Type BU

### Branche Git / Commit

- **Hash V1.1 Phase 1** : _à renseigner après commit_
- **Message attendu** :
  `feat(rh): V1.1 Phase 1 - referentiel BusinessUnitType (Boutique/Piste/E-Service/Espace Auto) + migration auto + RBAC`

---

## [Étape FINAL] 2026-05-03 0700 — Livrables : README + install.sql

**Objet** : clôture de la mission Tableaux de Bord RH avec 2 livrables :

1. **`docs/dashboards/README.md`** (~370 lignes) — documentation
   en 3 parties :
   - **Partie DRH** : accès, RBAC, 6 tableaux en 1 coup d'œil, boutons communs
   - **Partie Dev** : architecture, build, dépendances, patterns clés, pièges
   - **Partie Annexes** : scripts SQL, limitations, évolutions futures
   - ToC complète + style markdown propre

2. **`sql/dashboards/install.sql`** (~600 lignes) — script consolidé
   regroupant les 6 modules SQL avec :
   - En-tête commenté (rôle du script, usages prévus)
   - **Table des matières** numérotée § 0 à § 6
   - Bloc paramètres globaux (`@annee`, `@debut`, `@fin`, `@sentinelle`)
   - 1 sélection des requêtes essentielles par module (KPI + charts clés)
   - Renvois vers les fichiers détaillés `01_*.sql` à `06_*.sql`

3. **`docs/dashboards/MISSION_STATE.md`** : tableau d'avancement final
   mis à jour avec tous les hashes des commits.

### Fichiers créés (2)

| Fichier | Lignes | Type |
|---|---|---|
| `docs/dashboards/README.md` | ~370 | Markdown documentation |
| `sql/dashboards/install.sql` | ~600 | SQL consolidé |

### Récapitulatif final de la mission

**11 étapes complétées** :

| # | Étape | Hash | Date |
|---|---|---|---|
| 0 | Préparation Git + journalisation | — | 2026-05-02 |
| 1 | Analyse de l'existant | — | 2026-05-02 |
| 2 | Architecture cible + DI + rôle RH_Manager | — | 2026-05-02 |
| 3 | Page d'accueil DashboardHome (6 cartes) | `5d57771e` | 2026-05-02 |
| 4.1 | Tableau N°1 — Effectif détaillé | `45bb075` | 2026-05-02 |
| 4.2 | Tableau N°2 — Analyse de l'Effectif (Interne+Externe) | `504cb1ee` | 2026-05-02 |
| 4.3 | Tableau N°3 — Mouvements (Arrivées/Départs) | `2e1347bd` | 2026-05-02 |
| 4.4 | Tableau N°4 — Rémunération (Égalité salaires) | _consolidé_ | 2026-05-02 |
| 4.5 | Tableau N°5 — Suivi des Absences | `686fb3b8` | 2026-05-02 |
| 4.6 | Tableau N°6 — Bilan Social Mensuel | `cae0763` | 2026-05-03 |
| 7.1+7.2+7.4 | Aide en ligne + Excel + Bilan Social option B | _consolidé_ | 2026-05-03 |
| 7.3 | Export PDF QuestPDF | _consolidé_ | 2026-05-03 |
| 7.SEC | Hot-fix nouvel onglet + lisibilité boutons + Bootstrap Icons | `6faecfc` | 2026-05-03 |
| 7.SEC RBAC | RBAC complet 4 rôles + helper partagé | `b7815bb` | 2026-05-03 |
| 7.UX | Nettoyage 3 menus legacy en doublon | `ce1d7af` | 2026-05-03 |
| FINAL | Livrables README + install.sql | `8680d001` | 2026-05-03 |

### Statistiques

- **6 dashboards** Blazor Server fonctionnels (3 INTERNE-only, 3 toggle Interne/Externe)
- **~50 fichiers** créés ou modifiés (Models, Services, Razor, CSS, SQL, Help)
- **~5 000 lignes** de code C# / Razor / CSS / SQL
- **2 export formats** (Excel ClosedXML + PDF QuestPDF)
- **7 pages d'aide HTML** statiques (1 hub + 6 spécifiques)
- **4 rôles RBAC** supportés (Administrators / RH_Manager / RH / DAF)
- **3 dépendances NuGet** ajoutées (`ClosedXML` déjà présent, `QuestPDF` 2024.7.3, `Microsoft.Extensions.Caching.Memory` 8.0.1)
- **6 fichiers SQL** d'audit + 1 consolidé `install.sql`

### Mission TERMINÉE ✅

Cette entrée clôt formellement la mission « Tableaux de Bord RH » sur la
branche `feature/dashboards-rh`.

**HEAD final** : `8680d0011e8566cf01013c6da81a59e86ed08e59`

Pour merger sur `main` : voir README § « Maintenance / contact ».

---

## [Étape 7.UX] 2026-05-03 0600 — Nettoyage menus « Tableaux de bord » en doublon

**Constat utilisateur** : 4 entrées « Tableaux de bord » apparaissaient dans
le menu de gauche (3 héritées d'anciens BO/menus, 1 nouveau).

### Inventaire
| Source | Caption affiché | Décision |
|---|---|---|
| `DashboardsRHMenu.cs` (Étape 2) | **GRH - Tableaux de Bord** | ✅ GARDER |
| `Model.DesignedDiffs.xafml` Item `GRH_Dashboards` | Tableaux de bord | Masqué |
| Auto `[NavigationItem("Tableaux de Bord")]` sur `TableauBordInterimaire` + `TableauBordEffectif` | Tableaux de Bord | Masqué |
| Auto `[NavigationItem("Tableaux de bord")]` sur `RapportPowerBI` | Tableaux de bord | Masqué |

### Implémentation
3 entrées ajoutées dans `Model.DesignedDiffs.xafml` au niveau du
NavigationItems root :
```xml
<Item Id="Tableaux de Bord" Visible="False" />
<Item Id="Tableaux de bord" Visible="False" />
<Item Id="GRH_Dashboards" ... Visible="False"> ... </Item>
```

Les BusinessObjects legacy (`TableauBordInterimaire`, `TableauBordEffectif`,
`RapportPowerBI`) **ne sont pas supprimés** — la suppression définitive
est différée pour permettre un rollback rapide si besoin.

### Fichier modifié

| Fichier | Nature |
|---|---|
| `Model.DesignedDiffs.xafml` | + 3 `Visible="False"` sur les groupes legacy |

### Suppression définitive (à faire plus tard)

Quand validé en prod sur quelques semaines :
1. Supprimer l'attribut `[NavigationItem("Tableaux de bord")]` sur `RapportPowerBI.cs`
2. Supprimer `[NavigationItem("Tableaux de Bord")]` sur `TableauBordInterimaire.cs` + `TableauBordEffectif.cs`
3. Supprimer le bloc `<Item Id="GRH_Dashboards"...>` de `Model.DesignedDiffs.xafml`
4. Optionnel : si les BusinessObjects legacy ne servent plus du tout, marquer
   `[NonPersistent]` ou les supprimer du modèle XPO (attention à la migration de schéma SQL)

---

## [Étape 7.SEC RBAC] 2026-05-03 0530 — RBAC complet (4 rôles autorisés)

**Demande utilisateur** : ajouter un vrai check RBAC (rôle) au-delà du simple
check d'authentification — éviter qu'un salarié logué quelconque puisse
voir le Bilan Social complet et les données rémunération de tous ses
collègues (risque RGPD).

### Décisions

**Rôles autorisés à voir les dashboards** :
| Rôle | Cible métier | Politique XAF |
|---|---|---|
| `Administrators` | Sysadmin | IsAdministrative |
| `RH_Manager` | DG / COMEX (consultation pure) | DenyAllByDefault + Read sources |
| `RH` | Équipe RH opérationnelle | AllowAllByDefault sauf Compta |
| `DAF` | Direction financière (masse salariale) | Permissions ciblées + dashboards |

### Implémentation

1. **Helper partagé** `AdiPAIE_V02.Blazor.Server/Services/DashboardAuthHelper.cs` :
   - Méthode `CanAccessDashboards(httpCtx, osFactory)`
   - Vérifie auth cookie ASP.NET + appartenance à un rôle de la liste
   - Charge `ApplicationUser` via NonSecuredObjectSpace (évite récursion permission)
   - **Fail closed** : en cas d'erreur, refuse l'accès

2. **Refactor des 7 pages** : remplacement du check ad-hoc par
   `DashboardAuthHelper.CanAccessDashboards(HttpCtx, ObjectSpaceFactory)`
   uniformisé.

3. **`Updater.cs` étendu** : nouvelle méthode `GrantDashboardAccessToExistingRole(roleName)`
   appelée pour `RH` et `DAF` à chaque démarrage. Ajoute idempotemment :
   - Navigate + Read sur `DashboardsRHMenu`
   - Read sur les 13 entités sources (Salarie, Bulletin, BulletinLigne,
     ContratInterim, Site, StationService, Departement, Categories,
     CongeDemande, CongeType, etc.)

4. **`_Imports.razor`** : ajout de `@using AdiPAIE_V02.Blazor.Server.Services`
   pour exposer le helper globalement.

### Fichiers créés / modifiés

| Fichier | Type | Nature |
|---|---|---|
| `Blazor.Server/Services/DashboardAuthHelper.cs` | nouveau | Helper RBAC partagé |
| `Module/Services/Dashboards/DashboardAuthHelper.cs` | modif | Vide (commentaire de redirection — l'implem est côté Blazor.Server à cause de la dép. INonSecuredObjectSpaceFactory) |
| `Module/DatabaseUpdate/Updater.cs` | modif | + méthode `GrantDashboardAccessToExistingRole` + appels pour RH et DAF |
| `Blazor.Server/_Imports.razor` | modif | + @using Blazor.Server.Services |
| `Blazor.Server/Pages/Dashboards/DashboardHome.razor` | modif | utilise helper |
| `Blazor.Server/Pages/Dashboards/Effectif/EffectifDetailleDashboard.razor` | modif | idem |
| `Blazor.Server/Pages/Dashboards/Effectif/AnalyseEffectifDashboard.razor` | modif | idem |
| `Blazor.Server/Pages/Dashboards/Mouvements/MouvementsDashboard.razor` | modif | idem |
| `Blazor.Server/Pages/Dashboards/Remuneration/RemunerationDashboard.razor` | modif | idem |
| `Blazor.Server/Pages/Dashboards/Absences/SuiviAbsencesDashboard.razor` | modif | idem |
| `Blazor.Server/Pages/Dashboards/BilanSocial/BilanSocialDashboard.razor` | modif | idem |

### Tests à effectuer

1. Avec un utilisateur **`RH_Manager`** : tous les dashboards doivent s'ouvrir ✓
2. Avec **`RH`** : idem ✓ (était déjà OK car AllowAllByDefault, mais maintenant explicite)
3. Avec **`DAF`** : doit pouvoir s'ouvrir (les nouvelles permissions XPO le permettent)
4. Avec **`Default`** seul : doit être **redirigé vers /LoginPage** (RBAC bloque)
5. **Sans login** (navigation privée + URL directe) : doit être **redirigé vers /LoginPage**

---

## [Étape 7.2 enrichissement] 2026-05-03 0445 — Onglet Synthèse + DataBars visuels (Excel)

**Constat utilisateur** : l'export Excel multi-onglets fonctionne mais
l'utilisateur découvrant l'export ne voyait que la 1ʳᵉ feuille (KPI) et
ne réalisait pas que les autres sections étaient dans des onglets
adjacents. Demande aussi : retrouver le visuel des barres orange comme
sur la page web.

### Améliorations apportées

1. **Onglet « Synthese » en première position** dans chaque export :
   - Titre principal sur fond navy ELTON (banner 14pt)
   - Sous-titre (filtres actifs)
   - Grille de KPI sur 1 ligne (header navy + valeurs en gras 12pt)
   - Bordure gauche orange ELTON sur chaque cellule de valeur
   - Note pied de page renvoyant vers les onglets détaillés
   - Mise en page paysage prête à imprimer

2. **Synthèse enrichie pour Tab 4 Rémunération** (le plus complexe) :
   - KPI + tableau « Égalité par catégorie professionnelle » + tableau
     « Égalité par segment » empilés sur la même feuille
   - 2 sections avec sous-titre encadré bordure orange
   - DataBars natifs Excel sur les colonnes Total

3. **DataBars (barres visuelles dans cellule)** sur :
   - Colonne `Total` des tableaux Égalité (Rémunération)
   - Colonne `Valeur` des bar charts (Mouvements, Absences, Analyse Effectif)
   - Couleur orange ELTON (`#F18A1C`)
   - Reproduit visuellement le rendu des bar charts de la page web

### Fichier modifié

| Fichier | Nature |
|---|---|
| `AdiPAIE_V02.Module/Services/Dashboards/DashboardExcelExportService.cs` | + helpers `BuildSyntheseGenerique`, `BuildSyntheseRemuneration`, `AppendEgaliteRows` ; ajout DataBars dans `WriteEgaliteSheet` et `WriteBarSheet` ; appel de la synthèse dans les 6 méthodes Export* |

### Ce qui change pour l'utilisateur final

Avant : `[KPI] [Egalite par segment] [Egalite par categorie] [Evolution mensuelle] [Decompos rubriques]`

Après : `[Synthese] [KPI] [Egalite par segment] [Egalite par categorie] [Evolution mensuelle] [Decompos rubriques]`

Le 1er onglet « Synthese » donne un panorama complet en une page (idéal
pour impression / copie dans rapport Word). Les onglets suivants restent
disponibles pour analyse détaillée et création de TCD.

---

## [Étape 7 hot-fix sécurité] 2026-05-03 0400 — Guard d'authentification + Bootstrap Icons CDN

**🚨 Faille de sécurité corrigée** : les pages `/dashboards/*` étaient
accessibles **sans authentification**. Cause racine : `App.razor` utilise
`<RouteView>` (et non `<AuthorizeRouteView>`), donc l'attribut
`@attribute [Authorize]` placé sur chaque dashboard était silencieusement
ignoré.

**Solution** (sans toucher à `App.razor` pour ne pas impacter le reste
de l'app XAF) : ajout d'un **guard d'authentification** dans le
`OnInitialized()` de chaque page dashboard.

```csharp
@inject IHttpContextAccessor HttpCtx

protected override void OnInitialized()
{
    if (HttpCtx?.HttpContext?.User?.Identity?.IsAuthenticated != true)
    {
        Nav.NavigateTo("/LoginPage", forceLoad: true);
        return;
    }
    LoadLookups();
    LoadData();
}
```

`IHttpContextAccessor` est déjà enregistré dans `Startup.cs`
(`services.AddHttpContextAccessor()`).

### Fichiers modifiés (7)

| Fichier | Nature |
|---|---|
| `Pages/Dashboards/DashboardHome.razor` | + guard auth |
| `Pages/Dashboards/Effectif/EffectifDetailleDashboard.razor` | + guard auth |
| `Pages/Dashboards/Effectif/AnalyseEffectifDashboard.razor` | + guard auth |
| `Pages/Dashboards/Mouvements/MouvementsDashboard.razor` | + guard auth |
| `Pages/Dashboards/Remuneration/RemunerationDashboard.razor` | + guard auth |
| `Pages/Dashboards/Absences/SuiviAbsencesDashboard.razor` | + guard auth |
| `Pages/Dashboards/BilanSocial/BilanSocialDashboard.razor` | + guard auth |

### Bonus : Bootstrap Icons (icônes invisibles)

Les boutons header (Refresh / PDF / Excel / ?) ainsi que les pictos KPI
n'affichaient rien car `bootstrap-icons.css` n'était pas chargé.

**Fix** : ajout dans `_Host.cshtml` du CDN jsDelivr :
```html
<link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" rel="stylesheet" />
```

À défaut, hébergement local `wwwroot/lib/bootstrap-icons/` recommandé
pour la prod (si serveur sans accès Internet).

### Note méthodologique sur le guard

Le pattern `@attribute [Authorize]` reste présent sur chaque page (au
cas où App.razor serait corrigé plus tard pour utiliser
`<AuthorizeRouteView>`), mais le guard côté code C# fournit la
protection effective immédiate. Solution **défense en profondeur**.

---

## [Étape 7.3] 2026-05-03 0330 — Export PDF (QuestPDF) + lisibilité boutons header

**Objet** : finalisation des exports avec génération PDF native (QuestPDF)
pour les 6 tableaux + amélioration du contraste des boutons icônes du
header (Refresh / PDF / Excel / Aide) qui étaient peu lisibles sur le fond
navy ELTON.

### Décisions techniques

- **Stack PDF** : QuestPDF 2024.7.3 (fluent C# API). Licence Community
  gratuite pour CA < 1 M$ ou usage open-source ; Professional ≈ 699 $/an
  pour ELTON en prod (à arbitrer juridiquement avant déploiement).
  `Settings.License = LicenseType.Community` au démarrage du service.
- **Format** : A4 paysage, en-tête navy ELTON avec titre + sous-titre +
  date génération, pied de page avec « Page x/y » + horodatage.
- **Layout** : KPI en cartes 1 ligne × N colonnes, sections data en
  tableaux à en-têtes navy avec lignes alternées.
- **Téléchargement** : réutilise `window.AdiPAIE.downloadFile`
  (mime `application/pdf`).

### Lisibilité boutons (correctif visuel)

CSS `.ed-btn` modifié dans `dashboards-elton.css` :
- Fond passé de `rgba(255,255,255,0.08)` à `rgba(255,255,255,0.18)`
- Bordure passée de `0.18` à `0.45` opacité
- Hover : orange ELTON (au lieu de rouge) avec lift `translateY(-1px)` +
  ombre `0 2px 8px rgba(241,138,28,0.35)`
- Icône `font-size` 1rem → 1.15rem
- Surface cliquable mini 2.4 × 2.4 rem (carrée, plus accessible)
- Support `<a class="ed-btn">` aligné via `display: inline-flex`

### Fichiers créés (2)

- `AdiPAIE_V02.Module/Services/Dashboards/IDashboardPdfExportService.cs`
- `AdiPAIE_V02.Module/Services/Dashboards/DashboardPdfExportService.cs`
  (~330 lignes — 6 méthodes Export*, helpers `BuildDocument`, `Kpi`,
  `SectionTitle`, `Th/Td/TdTotal`, `BarTable`, `EgaliteTable`,
  format `Fcfa/Pct/Ans`)

### Fichiers modifiés (9)

| Fichier | Nature |
|---|---|
| `AdiPAIE_V02.Module/AdiPAIE_V02.Module.csproj` | + PackageReference QuestPDF 2024.7.3 |
| `wwwroot/css/dashboards-elton.css` | contraste boutons header |
| `Startup.cs` | DI `IDashboardPdfExportService` |
| `Pages/Dashboards/Effectif/EffectifDetailleDashboard.razor` | inject Pdf + handler câblé |
| `Pages/Dashboards/Effectif/AnalyseEffectifDashboard.razor` | idem |
| `Pages/Dashboards/Mouvements/MouvementsDashboard.razor` | idem |
| `Pages/Dashboards/Remuneration/RemunerationDashboard.razor` | idem |
| `Pages/Dashboards/Absences/SuiviAbsencesDashboard.razor` | idem |
| `Pages/Dashboards/BilanSocial/BilanSocialDashboard.razor` | idem |

### Limitations / TODO

- **Licence QuestPDF** : à passer en Professional pour prod ELTON
  (CA > 1 M$). Code à modifier : `Settings.License = LicenseType.Professional`
  + ajouter la clé via `Settings.LicenseKey = "..."`.
- **Charts en image** : le PDF affiche les bar charts sous forme de
  tableaux et non d'images SVG. Pour intégrer les graphiques visuels,
  prochaine étape = générer des SVG inline avec `Svg()` de QuestPDF
  ou utiliser SkiaSharp.
- **Mémoire** : les PDF sont sérialisés en base64 dans la connexion
  SignalR — taille raisonnable pour < 5 MB. Pour très gros bilans,
  basculer sur un endpoint MVC `FileResult`.

### Branche Git / Commit

- **Branche** : `feature/dashboards-rh`
- **Hash Étape 7.3** : _à renseigner_
- **Message attendu** :
  `feat(dashboards): etape 7.3 export PDF QuestPDF 6 tableaux + fix lisibilite boutons header`

---

## [Étape 7 hot-fix] 2026-05-03 0230 — Tableaux de bord ouvrent dans un nouvel onglet

**Constat utilisateur** : depuis le menu XAF principal, le clic sur
« Ouvrir les Tableaux de Bord » naviguait dans la même fenêtre
(`location.assign`), ce qui masquait l'application XAF. L'utilisateur
devait utiliser le bouton « Précédent » du navigateur pour retourner
à l'application.

**Correctif** : `DashboardsRHNavigationController.cs` modifié pour
utiliser `window.open(url, '_blank')` à la place de `location.assign`.
Pattern aligné sur le help SunuPaie (qui ouvre toujours dans un nouvel
onglet via `target="_blank"`).

**Effet** :
- L'utilisateur reste dans son onglet XAF d'origine.
- Les tableaux de bord s'ouvrent dans un nouvel onglet indépendant.
- Plus besoin du bouton « Précédent » : on ferme simplement l'onglet
  pour revenir à l'application.
- Cohérent avec le pattern Help du projet.

### Fichier modifié

| Fichier | Nature |
|---|---|
| `AdiPAIE_V02.Module/Controllers/RH/DashboardsRHNavigationController.cs` | `js.InvokeVoidAsync("open", "/dashboards/", "_blank")` au lieu de `location.assign` |

### Note technique

Si un bloqueur de popup empêche l'ouverture (rare car déclenchée par
clic utilisateur), une amélioration future consistera à ajouter une
fonction JS `window.AdiPAIE.openInNewTab(url)` avec fallback sur
`location.href` en cas d'échec.

---

## [Étape 7.1 + 7.2 + 7.4] 2026-05-03 0200 — Aide en ligne + Export Excel + Bilan Social option B

**Objet** : trio d'améliorations transversales sur les 6 tableaux :
- **7.1** : 7 pages d'aide HTML statiques sous `/wwwroot/help/dashboards/` +
  CSS partagé + bouton « ? » dans le header de chaque dashboard (ouvre dans
  nouvel onglet).
- **7.2** : service partagé `IDashboardExcelExportService` (ClosedXML) qui
  produit un `.xlsx` propre par tableau (1 worksheet par section : KPI,
  bar charts, tableaux, évolution, etc.) avec en-tête navy, format FCFA,
  format pourcentage, ligne TOTAL stylée.
- **7.4** : ligne complémentaire « dont Intérimaires » sur le Bilan Social
  Mensuel (option B validée par utilisateur — info managériale hors DTSS officiel).

### Fichiers créés (10)

- `wwwroot/help/dashboards/dashboards-help.css` (charte ELTON partagée)
- `wwwroot/help/dashboards/index.html` (hub avec 6 cartes)
- `wwwroot/help/dashboards/effectif-detaille.html`
- `wwwroot/help/dashboards/analyse-effectif.html`
- `wwwroot/help/dashboards/mouvements.html`
- `wwwroot/help/dashboards/remuneration.html`
- `wwwroot/help/dashboards/suivi-absences.html`
- `wwwroot/help/dashboards/bilan-social.html`
- `AdiPAIE_V02.Module/Services/Dashboards/IDashboardExcelExportService.cs`
- `AdiPAIE_V02.Module/Services/Dashboards/DashboardExcelExportService.cs`
  (ClosedXML, ~330 lignes, 6 méthodes Export*)

### Fichiers modifiés (10)

| Fichier | Nature |
|---|---|
| `Startup.cs` | DI export Excel |
| `Pages/Dashboards/Effectif/EffectifDetailleDashboard.razor` | bouton ? + Export Excel câblé |
| `Pages/Dashboards/Effectif/AnalyseEffectifDashboard.razor` | idem |
| `Pages/Dashboards/Mouvements/MouvementsDashboard.razor` | idem |
| `Pages/Dashboards/Remuneration/RemunerationDashboard.razor` | idem |
| `Pages/Dashboards/Absences/SuiviAbsencesDashboard.razor` | idem |
| `Pages/Dashboards/BilanSocial/BilanSocialDashboard.razor` | idem + section « dont Intérimaires » |
| `Module/Models/Dashboards/BilanSocialDto.cs` | + classe `IntemRowDto` |
| `Module/Services/Dashboards/BilanSocialDashboardService.cs` | + méthode `ComputeDontInterimaires` |
| `docs/dashboards/CHANGELOG.md` | cette entrée |

### Décisions techniques

- **Téléchargement Excel** : utilise la fonction JS existante
  `window.AdiPAIE.downloadFile(fileName, mimeType, base64)` déjà présente
  dans `wwwroot/js/adipaie.js` — pas de nouveau JS à ajouter.
- **Pages d'aide** : indépendantes de Blazor (HTML statique) → ouvrent
  instantanément dans un nouvel onglet, ne sollicitent pas le serveur.
- **Charte help** : navy ELTON pour cohérence visuelle (l'aide générale
  SunuPaie reste verte ; la nav cross-link les deux univers).
- **Bilan Social option B** : ne touche pas au calcul DTSS officiel ; la
  ligne intérimaires n'apparaît que si `NbContrats > 0`.

### Étape suivante : 7.3 Export PDF

Reste à implémenter via QuestPDF ou DevExpress XtraReport (choix utilisateur).

---

## [Étape 4.6] 2026-05-03 0030 — Tableau N°6 « Bilan Social Mensuel »

**Objet** : implémentation du dernier dashboard — synthèse mensuelle DTSS-style
sur le périmètre INTERNE. Tableau 12 mois × 8 indicateurs principaux + ligne
TOTAL en queue, le tout consolidant les sources Salarie / Bulletin /
BulletinLigne / CongeDemande déjà cartographiées dans les Tab 1-5.

### Décisions validées (avant codage)

- **Périmètre INTERNE uniquement** : le Bilan Social DTSS est légalement
  pour les salariés permanents.
- **Indicateurs codés (8)** : Effectif fin mois, Embauches, Départs,
  Masse Salariale, Charges Patronales, Coût Employeur (calc), Employés
  Absents, Jours d'Absence.
- **Indicateurs N/A** (renvoyés `null` côté DTO, affichés « — » côté UI) :
  Mouvements emplois, Mesures disciplinaires, Accidents (travail/trajet),
  Maladies professionnelles, Budget Formation, Heures Formation, Employés
  Formés. Section « TODO méthodologique » dans la page documente quelles
  entités créer pour les activer.
- **Ligne TOTAL** : fond navy ELTON + bordure orange dorée + texte clair —
  visuellement distincte des 12 lignes mensuelles.
- **KPI complétude** : ratio cellules non vides sur les 12 mois × 5
  indicateurs principaux (indicateur qualité données pour le RH).
- **Pas de filtre Genre/Famille/Catégorie** : la vue se veut transversale
  (ces granularités sont disponibles dans les Tab 1-5).

### Fichiers créés (5)

- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/BilanSocialFilterModel.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/BilanSocialDto.cs`
  (incl. `KpiBilanSocialDto`, `MoisBilanSocialDto` avec `CoutEmployeur`
  calculé en propriété get-only)
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/IBilanSocialDashboardService.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/BilanSocialDashboardService.cs`
  (incl. helpers `Compute`, `ProrataJoursDansMois`, `IsActif` soft-delete)
- `sql/dashboards/06_bilan_social.sql` (3 requêtes : récap mensuel + total
  annuel + taux d'absentéisme)

### Fichiers modifiés (3)

| Fichier | Sauvegarde `.bak` | Nature |
|---|---|---|
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/BilanSocial/BilanSocialDashboard.razor` | `docs/dashboards/backup/2026-05-03_0030/.../BilanSocialDashboard.razor.bak` | Réécriture complète : remplacement du placeholder par UI ELTON (5 KPI, tableau 12 mois × 8 colonnes + ligne TOTAL stylée navy/orange, section TODO méthodologique). |
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Startup.cs` | `docs/dashboards/backup/2026-05-03_0030/.../Startup.cs.bak` | DI : `services.AddScoped<IBilanSocialDashboardService, BilanSocialDashboardService>()`. |
| `docs/dashboards/CHANGELOG.md` | (suivi git) | Cette entrée. |

### Limitations connues / TODO

- **Indicateurs absents du modèle** : Formations, Accidents, Disciplinaire.
  Documenté dans le footer de la page + cette entrée. À traiter en Étape 8.
- **Boutons Export PDF/Excel** : toast « à venir » (Étape 7). À noter que
  pour ce tableau, l'export Excel sera particulièrement utile (tableau
  prêt à coller dans le formulaire DTSS .docx).
- **Optimisation** : si Bulletin > 50 000 lignes, basculer le calcul des
  charges côté SQL via vue (actuellement aggrégation en mémoire).

### Branche Git / Commit

- **Branche** : `feature/dashboards-rh`
- **Hash Étape 4.6** : _à renseigner après le `git commit` côté Windows_
- **Message attendu** :
  `feat(dashboards): tableau N6 bilan social mensuel (recap 12 mois x 8 indicateurs INTERNE, ligne TOTAL stylee, section TODO entites manquantes)`

### Commandes de rollback

```
git revert <hash_du_commit_etape_4_6>
# ou (en local non poussé) :
git reset --hard 686fb3b8
```

### Validé par

_À renseigner — validation en cours côté utilisateur après build + test._

---

## [Étape 4.5] 2026-05-02 2330 — Tableau N°5 « Suivi des Absences »

**Objet** : implémentation complète du Tableau N°5 sur le périmètre INTERNE
uniquement (les intérimaires n'ont pas de système de demande de congé).
Source = `CongeDemande` filtrée par défaut sur `CongeStatut.Accordee` (20).
Famille de congé issue de `CongeType.Famille` (enum 6 valeurs).

### Décisions validées (avant codage)

- **Périmètre statut** : par défaut `Accordee` uniquement (vue « absences
  réellement prises »). Toggle pour inclure `EnAttenteN1/N2 + Soumise +
  Accordee` (vue large).
- **8 KPI** : NbAbsences, TotalJours, TauxAbsenteisme, DureeMoyenne,
  %Justifiees, TopAbsent (nom + jours), MoisPic, CoutEstimeImpaye.
- **Familles** : Annuel / Maladie / Maternité / Événement familial /
  Sans solde / Récupération / Autres.
- **Filtres** : Année, Site, Genre, Segment (Département), Catégorie pro,
  Famille (filtre spécifique tab 5).
- **Charts** : bar vertical mensuel 12 mois + 3 bar horizontaux (Famille /
  Catégorie Top 10 / Département) + tableau Top 10 absents.
- **Heatmap** : volontairement omis pour limiter la complexité visuelle.
- **Taux d'absentéisme** : `Σ DureeJours / (Effectif × 264 jours ouvrables)
  × 100`. Le 264 = 22 j × 12 mois (constante métier).
- **Coût impayé estimé** : pour les familles `SansSolde` uniquement,
  `DureeJours × salaire_jour_moyen` où le salaire_jour est dérivé du
  `Bulletin.BrutFiscal` annuel divisé par 22 jours/mois.

### Fichiers créés (5)

- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/SuiviAbsencesFilterModel.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/SuiviAbsencesDto.cs`
  (incl. `KpiAbsencesDto`, `MoisAbsenceDto`, `TopAbsentRowDto`)
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/ISuiviAbsencesDashboardService.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/SuiviAbsencesDashboardService.cs`
  (incl. helpers `Compute`, `MonthOfMid`, `CountSalariesActifs`,
  `LabelFamille`, `SafeDepartementNom`)
- `sql/dashboards/05_suivi_absences.sql` (8 requêtes alignées spec)

### Fichiers modifiés (3)

| Fichier | Sauvegarde `.bak` | Nature |
|---|---|---|
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/Absences/SuiviAbsencesDashboard.razor` | `docs/dashboards/backup/2026-05-02_2330/.../SuiviAbsencesDashboard.razor.bak` | Réécriture complète : remplacement du placeholder par UI ELTON (toggle statut, 6 filtres dont Famille, 8 KPI inline, évolution 12 mois, 3 bar charts horizontaux, Top 10 absents). |
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Startup.cs` | `docs/dashboards/backup/2026-05-02_2330/.../Startup.cs.bak` | DI : `services.AddScoped<ISuiviAbsencesDashboardService, SuiviAbsencesDashboardService>()`. |
| `docs/dashboards/CHANGELOG.md` | (suivi git) | Cette entrée. |

### Limitations connues / TODO

- **Heatmap (jour de la semaine × mois)** : non implémenté. À ajouter en
  Étape 7 (Qualité) si besoin métier confirmé.
- **Coût impayé** : approximation simple. Pour des chiffres de paie
  exacts, il faudrait croiser avec `BulletinLigne` ayant un type retenue
  liée à l'absence (rubrique « retenue absence »).
- **Famille « Autres »** : si l'enum `FamilleConge` est étendu ultérieurement,
  les nouvelles valeurs tomberont automatiquement dans le bucket
  `(Autres)` du Razor.
- Boutons Export PDF / Excel : toast « à venir » (Étape 7).

### Branche Git / Commit

- **Branche** : `feature/dashboards-rh`
- **Hash Étape 4.5** : _à renseigner après le `git commit` côté Windows_
- **Message attendu** :
  `feat(dashboards): tableau N5 suivi absences (CongeDemande Accordee, 8 KPI, evolution 12 mois, par famille/categorie/segment, Top 10)`

### Commandes de rollback

```
git revert <hash_du_commit_etape_4_5>
# ou (en local non poussé) :
git reset --hard <hash_etape_4_4>
```

### Validé par

_À renseigner — validation en cours côté utilisateur après build + test._

---

## [Étape 4.4] 2026-05-02 2200 — Tableau N°4 « Rémunération (Égalité des salaires) »

**Objet** : implémentation complète du Tableau N°4 sur les périmètres
INTERNE (Bulletin / BulletinLigne) et EXTERNE (ContratInterim) — avec
toggles Personnel et Mode (Coût Employeur / Rémunération Nette). UI
alignée sur la maquette PowerBI utilisateur (5 KPI cartes circulaires,
2 tableaux Égalité des salaires par Segment et Catégorie avec barres de
progression colorées orange ELTON, évolution mensuelle 12 mois,
décomposition par 7 familles macro de rubrique).

### Décisions validées (avant codage)

- **Salaire INTERNE** : `Bulletin.BrutFiscal` (Annee + Mois + GCRecord IS NULL).
- **Charges patronales** : `SUM(BulletinLigne.MontantEmployeur)` (jointure
  Bulletin via `Bulletin.Oid`).
- **Rémunération nette** : `Bulletin.NetAPayer` (Mode RemunerationNette).
- **Coût employeur** : `BrutFiscal + ChargesPatronales` (Mode CoutEmployeur).
- **Salaire EXTERNE** : `TauxJournalier × 22 jours × NbMois` où NbMois est la
  durée du recouvrement contrat/année (DateDebut..min(DateFin, finAnnee)).
- **Périodicité** : année calendaire avec slicer Année.
- **Décomposition rubriques** : 7 familles macro (BRUTE, INDEM_IMPOSA,
  INDEM_NON_IMPOSA, AV_NATURE, COTSOC, COTFISC, RETENUE) — INTERNE seulement.
- **Format FCFA** : NumberGroupSeparator = espace ` `, 0 décimales.
- **Source** : SPEC_PowerBI_DAX_to_SQL.sql + TestData/PowerBI/SPEC_Custom_Dashboard_SQL.sql (utilisateur).

### Fichiers créés (5)

- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/RemunerationFilterModel.cs`
  (incl. enum `RemunerationMode` CoutEmployeur / RemunerationNette)
- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/RemunerationDto.cs`
  (incl. `KpiRemunerationDto`, `EgaliteSalaireRowDto`, `MasseMensuelleDto`,
  `DecompositionRubriqueDto`)
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/IRemunerationDashboardService.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/RemunerationDashboardService.cs`
  (incl. helpers `ComputeForInterne`, `ComputeForExterne`, `BuildEgaliteRow`,
  `BuildEgaliteRowExterne`, `ComputeDecompositionRubriques`,
  `GetFamilleMacro`, `IsActif` pour soft-delete XPO,
  `SafeDepartementNom` réflexion)
- `sql/dashboards/04_remuneration.sql` (10 requêtes : 7 INTERNE + 3 EXTERNE)

### Fichiers modifiés (3)

| Fichier | Sauvegarde `.bak` | Nature |
|---|---|---|
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/Remuneration/RemunerationDashboard.razor` | `docs/dashboards/backup/2026-05-02_2200/.../RemunerationDashboard.razor.bak` | Réécriture complète : remplacement du placeholder par UI ELTON (header, toggles Personnel + Mode, 5 KPI circulaires, 2 tableaux Égalité avec barres, évolution 12 mois, décomposition rubriques INTERNE). |
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Startup.cs` | `docs/dashboards/backup/2026-05-02_2200/.../Startup.cs.bak` | DI : `services.AddScoped<IRemunerationDashboardService, RemunerationDashboardService>()`. |
| `docs/dashboards/CHANGELOG.md` | (suivi git) | Cette entrée. |

### Limitations connues / TODO

- **Mode Rémunération Nette en EXTERNE** : pas de notion de Net pour intérim
  (pas de Bulletin associé). Le toggle est désactivé en EXTERNE.
- **Égalité H/F en EXTERNE** : colonnes Moy. ♂ / ♀ vides (Interimaire n'a pas
  de Sexe). Si métier le demande : ajouter le champ via migration XPO.
- **Filtre Ancienneté** : volontairement omis pour ce tableau (peu pertinent
  côté rémunération annuelle ; restera disponible en Tab 5/6).
- **Décomposition rubriques** : agrégation côté mémoire (pas SQL) — un peu
  coûteuse pour des bulletins très volumineux. Si > 50 000 lignes, basculer
  côté SQL via vue.
- Boutons Export PDF / Excel : toast « à venir » (Étape 7).

### Branche Git / Commit

- **Branche** : `feature/dashboards-rh`
- **Hash Étape 4.4** : _à renseigner après le `git commit` côté Windows_
- **Message attendu** :
  `feat(dashboards): tableau N4 - remuneration egalite salaires (toggle Interne/Externe + CoutEmployeur/Net, 5 KPI, 2 tableaux egalite, evolution 12 mois, decomposition rubriques)`

### Commandes de rollback

```
git revert <hash_du_commit_etape_4_4>
# ou (en local non poussé) :
git reset --hard 2e1347bd
```

### Validé par

_À renseigner — validation en cours côté utilisateur après build + test._

---

## [Étape 4.3] 2026-05-02 1930 — Tableau N°3 « Mouvements (Arrivées / Départs) »

**Objet** : implémentation complète du Tableau N°3 sur les périmètres
INTERNE (Salarie) et EXTERNE (Interimaire / ContratInterim) — avec toggle.
7 KPI cartes (Arrivées, Départs, Solde net, %Arrivées, %Départs, Effectif
Début, Effectif Fin), section Arrivées (12 mois + Site + Catégorie), section
Départs (12 mois + Motif + Site + Catégorie). Charte ELTON Oil héritée du
CSS partagé.

### Décisions validées (avant codage)

- **INTERNE Arrivées** : `Salarie.DateEmbauche` dans l'année (filtre simple,
  pas de jointure HistoriquePoste).
- **INTERNE Départs** : `Salarie.DateSortie` dans l'année (avec sentinelle
  `>= 1900-01-01`) + **Motif** issu de l'enum `MotifDepart`
  (Démission / Licenciement / Fin de CDD / Retraite / Décès / Rupture
  conventionnelle).
- **EXTERNE Arrivées** : `ContratInterim.DateDebut` dans l'année (recommandé
  vs entité `MouvementInterimaire` qui reste utilisable pour évolutions).
- **EXTERNE Départs** : `ContratInterim` clôturés (Statut ∈
  {Resilie, Termine}) dont `DateFinReelle ?? DateFin` tombe dans l'année.
- **Effectif Début / Fin** : utilisés pour les taux %Arrivées et %Départs ;
  diviseur = effectif moyen `(début+fin)/2`.
- **CSS** : pas de nouveau fichier — utilisation directe du
  `dashboards-elton.css` partagé + petites variantes locales (`--blue` =
  Arrivées, `--red` = Départs).

### Fichiers créés (5)

- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/MouvementsFilterModel.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/MouvementsDto.cs`
  (incl. `KpiMouvementsDto` avec `Solde` calculé)
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/IMouvementsDashboardService.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/MouvementsDashboardService.cs`
  (incl. helpers `ComputeParMois<T>`, `ComputeBar<T>`, `GetMotifDepartLibelle`,
  `CountInterimsActifs`)
- `sql/dashboards/03_mouvements.sql` (17 requêtes : 10 INTERNE + 7 EXTERNE,
  alignées SPEC PowerBI mesures 3 et 4)

### Fichiers modifiés (3)

| Fichier | Sauvegarde `.bak` | Nature |
|---|---|---|
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/Mouvements/MouvementsDashboard.razor` | `docs/dashboards/backup/2026-05-02_1930/.../MouvementsDashboard.razor.bak` | Réécriture complète : remplacement du placeholder par l'UI ELTON (header sombre, toggle Interne/Externe, 7 KPI inline, 2 sections Arrivées/Départs avec 7 charts au total). |
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Startup.cs` | `docs/dashboards/backup/2026-05-02_1930/.../Startup.cs.bak` | DI : `services.AddScoped<IMouvementsDashboardService, MouvementsDashboardService>()`. |
| `docs/dashboards/CHANGELOG.md` | (suivi git) | Cette entrée. |

### Patch post-validation EXTERNE — Dénominateur anti-aberration

Constat utilisateur après build :
1. Première version : `% Arrivées = 228,6 %` (formule `(0 + 35) / 2 = 17,5`).
2. Après ETP pondéré 13 points : `% Arrivées = 162 %` — encore visuellement
   choquant car ramp-up complet (équipe entièrement créée en 2024).

**Correctif final** :
- Ajout de la méthode `ComputeEffectifMoyenPondere(...)` (moyenne sur
  13 dates : 1ᵉʳ de chaque mois + 31/12 = approx. ETP intérimaire annuel).
- Ajout de la méthode `CountInterimsAyantContratDansAnnee(...)` (nb distinct
  d'intérimaires ayant eu au moins un contrat actif sur l'année).
- **Dénominateur EXTERNE** = `MAX(ETP pondéré, nb distinct annuel)` :
  - année stable → l'ETP gagne (KPI rotation classique) ;
  - année de ramp-up → le nb distinct gagne, plafonne le taux à ≈ 100 %
    de manière honnête (« renouvellement total de l'équipe »).

INTERNE inchangé : la formule `(debut+fin)/2` reste pertinente pour les
salariés (population stable).

### Limitations connues / TODO

- **EXTERNE — Motifs de départ** : à défaut d'un champ libre sur
  `ContratInterim`, on regroupe par `Statut` (Termine / Resilie). Pour un
  détail plus fin, il faudrait croiser `MouvementInterimaire.TypeMouvement`
  + le champ `Motif` (texte libre) déjà présent sur `MouvementInterimaire`.
- **Filtre Genre** : appliqué uniquement côté INTERNE (`Salarie.Sexe`).
  Masqué automatiquement en mode EXTERNE (Interimaire n'a pas de Sexe).
- Si **tous** les contrats EXTERNE démarrent en cours d'année (cas seed
  actuel : 40 contrats 2024, 0 antérieurs), même la moyenne pondérée
  donnera un % Arrivées > 100 %. C'est mathématiquement correct (turn-over
  > 100 %) mais signale plutôt un problème de seed historique qu'un KPI
  pathologique.
- Boutons Export PDF / Excel : toast « à venir » (Étape 7).

### Branche Git / Commit

- **Branche** : `feature/dashboards-rh`
- **Hash Étape 4.3** : `2e1347bd5bbf7ce91a4c83221a787fa936b30d8c`
- **Message attendu** :
  `feat(dashboards): tableau N3 - mouvements arrivees/departs + dénominateur EXTERNE pondéré + MISSION_STATE`

### Commandes de rollback

```
git revert <hash_du_commit_etape_4_3>
# ou (en local non poussé) :
git reset --hard <hash_etape_4_2>
```

### Validé par

_À renseigner — validation en cours côté utilisateur après build + test._

---

## [Étape 4.2] 2026-05-02 1800 — Tableau N°2 « Analyse de l'Effectif »

**Objet** : implémentation complète du Tableau N°2 sur les périmètres
INTERNE (Salarie) et EXTERNE (Interimaire) — avec toggle. Mode Global /
Moyen, 7 KPI cartes, courbe d'évolution 8 ans, 5 bar charts (tranche d'âge,
ancienneté, segment, catégorie Top 5, type contrat). Charte ELTON Oil.

### Fichiers créés (7)

- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/AncienneteBucket.cs` (incl. `AgeBucketAnalyse` pour 4 tranches <30/30-39/40-49/≥50/vide)
- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/AnalyseEffectifFilterModel.cs` (incl. enum `EffectifMode` Global / Moyen)
- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/AnalyseEffectifDto.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/IAnalyseEffectifDashboardService.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/AnalyseEffectifDashboardService.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/wwwroot/css/dashboards-elton.css` (charte ELTON partagée — sera utilisée par les 6 tableaux)
- `sql/dashboards/02_analyse_effectif.sql`

### Fichiers modifiés (4)

| Fichier | Sauvegarde `.bak` | Nature |
|---|---|---|
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/Effectif/AnalyseEffectifDashboard.razor` | `docs/dashboards/backup/2026-05-02_1800/.../AnalyseEffectifDashboard.razor.bak` | Réécriture complète : UI Power BI ELTON + 7 KPI + courbe 8 ans + 5 bar charts. |
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/_Host.cshtml` | `docs/dashboards/backup/2026-05-02_1800/.../_Host.cshtml.bak` | Ajout `<link href="css/dashboards-elton.css" />`. |
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Startup.cs` | `docs/dashboards/backup/2026-05-02_1800/.../Startup.cs.bak` | DI : `services.AddScoped<IAnalyseEffectifDashboardService, AnalyseEffectifDashboardService>()`. |
| `docs/dashboards/CHANGELOG.md` | (suivi git) | Cette entrée. |
| `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/EffectifDetailleDashboardService.cs` | (modifs antérieures déjà sauvegardées) | Filtre DateSortie aligné sur SPEC PowerBI : `< 1900-01-01` au lieu de `== DateTime.MinValue`. |

### Décisions validées (avant codage)

- **Segment** : Département (`Salarie.Departement.Nom`) pour INTERNE ;
  `BusinessUnitStation.Libelle` pour EXTERNE.
- **Catégorie pro** : `Categories.Intitule` (cohérent Tableau N°1).
- **Effectif Moyen** : formule `(effectif 1/1 + effectif 31/12) / 2` (alignée SPEC PowerBI mesure 5).
- **CSS partagé** : extrait dans `wwwroot/css/dashboards-elton.css` —
  réutilisable par Tableaux N°3 → N°6 (réduit la dette technique de design).

### Périmètre EXTERNE — implémentation complète

Mappings métier confirmés et codés :

| Concept INTERNE | Concept EXTERNE équivalent |
|---|---|
| Salarie | Interimaire |
| Site (`Salarie.Site`) | StationService (`ContratInterim.Station`) |
| Département | (peut servir à un futur regroupement BU) |
| Categories.Intitule | PosteInterimaire.Libelle |
| TypeContrat (CDI/CDD/Stage) | ContratInterimType (PremiereMission, Renouvellement…) |
| DateEmbauche / DateSortie | ContratInterim.DateDebut / DateFin |
| Sortie = Salarie.MotifDepart != null | ContratInterim.Statut ∈ { Resilie, Termine } |

**Filtres EXTERNE** : Année / Station service / Genre (no-op faute de Sexe) / Société Intérim / Poste / Ancienneté contrat. Les filtres INTERNE (Département / Catégorie) sont automatiquement masqués en mode EXTERNE.

**Charts EXTERNE** : tous calculés (KPI, évolution 8 ans sur intérimaires actifs au 31/12, tranche d'âge depuis `Interimaire.DateNaissance`, ancienneté depuis `ContratInterim.DateDebut`, station, poste, type contrat).

### Limitations connues / TODO

- KPI **%Femmes / %Hommes** restent à `0` côté EXTERNE : `Interimaire` n'a pas de champ `Sexe` dans le projet. Si métier le demande, ajouter une propriété `Sexe` à `Interimaire` (migration XPO) ou dériver via `Civilite` si présent.
- Champ `Departement` de `Salarie` : accédé en réflexion (`SafeDepartementNom`) pour gérer l'absence éventuelle de la propriété directe.
- Le bouton Export PDF/Excel reste un toast « à venir » (Étape 7).

### Branche Git / Commit

- **Branche** : `feature/dashboards-rh`
- **Hash** : _à renseigner après le `git commit` côté Windows_
- **Message attendu** :
  `feat(dashboards): tableau N2 - analyse effectif (toggle Interne/Externe, mode Global/Moyen, 7 KPI, evolution 8 ans, 5 bar charts) + charte ELTON partagee`

### Commandes de rollback

```
git revert <hash_du_commit_etape_4_2>
# ou (en local non poussé) :
git reset --hard <hash_etape_4_1>
```

### Validé par

_À renseigner — validation en cours côté utilisateur après build + test._

---

## [Étape 4.1] 2026-05-02 1530 — Tableau N°1 « Effectif détaillé »

**Objet** : implémentation complète du Tableau N°1 sur le périmètre INTERNE.
KPI âge & ancienneté (global / hommes / femmes), évolution effectif sur
3 ans, 5 tableaux par tranche d'âge × catégorie professionnelle, bar chart
horizontal empilé Femmes / Hommes.

### Fichiers créés (6)

- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/AgeBucket.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/EffectifDetailleFilterModel.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/EffectifDetailleDto.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/IEffectifDetailleDashboardService.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Services/Dashboards/EffectifDetailleDashboardService.cs`
- `sql/dashboards/01_effectif_detaille.sql`

### Fichiers modifiés (3)

| Fichier | Sauvegarde `.bak` | Nature |
|---|---|---|
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/Effectif/EffectifDetailleDashboard.razor` | `docs/dashboards/backup/2026-05-02_1530/.../EffectifDetailleDashboard.razor.bak` | Réécriture : UI complète (filtres + 6 KPI + évolution + 5 tableaux + bar chart). |
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Startup.cs` | `docs/dashboards/backup/2026-05-02_1530/.../Startup.cs.bak` | DI : `services.AddScoped<IEffectifDetailleDashboardService, EffectifDetailleDashboardService>()`. |
| `AdiPAIE_V02/AdiPAIE_V02.Module/AdiPAIE_V02.Module.csproj` | `docs/dashboards/backup/2026-05-02_1530/.../AdiPAIE_V02.Module.csproj.bak` | Ajout `<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.1" />` (nécessaire pour `IMemoryCache` côté service). |
| `AdiPAIE_V02/AdiPAIE_V02.Module/Controllers/EspaceSalarieHelper.cs` | `docs/dashboards/backup/2026-05-02_1530/.../EspaceSalarieHelper.cs.bak` | Bug-fix défensif : `FindUserByName` ET `FindSalarieByCriteria` enveloppent leurs `FindObject<T>` dans un try/catch retournant null sur `ArgumentException`, conformément au commentaire de la méthode. Exposé par l'ouverture des ListView/DetailView des classes non persistantes (`DashboardsRHMenu`) qui utilisent `NonPersistentObjectSpace` ne contenant pas `ApplicationUser` ni `Salarie`. |
| `docs/dashboards/CHANGELOG.md` | (suivi git) | Hash Étape 3 renseigné + cette entrée. |

### Décisions validées (avant codage)

- **Tranche `<25 ans`** ajoutée (5 tranches au total : `<25`, `25-34`, `35-44`, `45-54`, `55+`).
- **Date de référence** : aujourd'hui pour l'année en cours, 31/12 pour les années passées.
- **Mensualisation** : KPIs en années entières (calcul jour-précis via `Birthday.AddYears(age)`).
- **Cache** : `IMemoryCache`, TTL 5 min, clé = filtres sérialisés.
- **Source** : `XafApplication.CreateObjectSpace(typeof(Salarie))` (aligné sur `BulkBulletinSenderService`, `AuditService`).

### Limitations connues / TODO

- Boutons **Export PDF** et **Export Excel** : non câblés (toast « à venir »). Implémentation à l'Étape 7 (UI commune).
- Filtre **TypeContrat** ET filtre **DateSortie** : appliqués en post-filtrage en mémoire (LINQ-to-Objects après `.ToList()`). Raison pour DateSortie : `DateTime.MinValue` (sentinelle des salariés actifs) est en dehors de la plage `SqlDateTime` (1753–9999) — l'envoyer comme paramètre déclenche `SqlDateTime overflow`. À optimiser via SQL natif si la volumétrie l'exige (>10k salariés).
- Le bar chart F/H est rendu en HTML/CSS pur (pas de `DxChart`) — robustesse vs. évolutions DevExpress Blazor.

### Décisions techniques imposées par l'environnement

- **IObjectSpace** : la page Razor injecte `INonSecuredObjectSpaceFactory` (DevExpress.ExpressApp.Blazor.Services) et passe l'`IObjectSpace` en paramètre des méthodes du service. Le service est ainsi agnostique XAF Blazor (testable hors XAF) et la résolution de scope `XafApplication` (qui n'est pas injectable directement dans un service scoped) est évitée.
- **Filtres défensifs** : `GetAnneesDisponibles` retourne toujours au minimum les 6 dernières années (current-5..current), même si la table Salarie est vide ou si la requête min() échoue. `GetSitesActifs` fait un fallback sur l'intégralité des sites si aucun n'est marqué Actif=true.

### Design

Le rendu visuel actuel est **fonctionnel mais minimaliste**. La référence Power BI fournie par l'utilisateur (KPI tiles groupées G/H/F, toggles Effectif Moyen/Total et Temps plein, layout 2 colonnes Power BI-style, sparklines par tranche, couleurs orange/gris pour F/H) sera implémentée dans une **passe design globale à l'Étape 6 (UI commune)** — après que les 6 tableaux soient fonctionnellement validés.

### Branche Git / Commit

- **Branche** : `feature/dashboards-rh`
- **Hash** : _à renseigner après le `git commit` côté Windows_
- **Message attendu** :
  `feat(dashboards): tableau N°1 - effectif detaille (KPI age/anciennete, evolution 3 ans, tranches x categorie, bar F/H)`

### Commandes de rollback

```
git revert <hash_du_commit_etape_4_1>
# ou (en local non poussé) :
git reset --hard 5d57771ea16fb84a1fbc5d80aa9d17c1108053d6
```

### Validé par

_À renseigner — validation en cours côté utilisateur après build + test._

---

## [Étape 3] 2026-05-02 1500 — Page d'accueil DashboardHome (6 cartes)

**Objet** : remplacement du placeholder par la grille responsive des
6 cartes cliquables, et création de 6 pages-placeholders correspondantes
(les implémentations finales arriveront à l'Étape 4, une par tableau).

### Fichiers créés

- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/Effectif/EffectifDetailleDashboard.razor` (Tableau N°1)
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/Effectif/AnalyseEffectifDashboard.razor` (Tableau N°2)
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/Mouvements/MouvementsDashboard.razor` (Tableau N°3)
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/Remuneration/RemunerationDashboard.razor` (Tableau N°4)
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/Absences/SuiviAbsencesDashboard.razor` (Tableau N°5)
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/BilanSocial/BilanSocialDashboard.razor` (Tableau N°6)

### Fichiers modifiés

| Fichier | Sauvegarde `.bak` | Nature |
|---|---|---|
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/DashboardHome.razor` | `docs/dashboards/backup/2026-05-02_1500/.../DashboardHome.razor.bak` | Réécriture complète : grille responsive 6 cartes (titre, badge périmètre, description, icône, route). |
| `docs/dashboards/CHANGELOG.md` | (suivi git) | Hashes Étape 2 / WIP / Hotfix renseignés + entrée Étape 3. |

### Routes nouvelles ajoutées

| Route | Page | Périmètre |
|---|---|---|
| `/dashboards/effectif-detaille` | Tableau N°1 | Interne |
| `/dashboards/analyse-effectif` | Tableau N°2 | Interne / Externe |
| `/dashboards/mouvements` | Tableau N°3 | Interne / Externe |
| `/dashboards/remuneration` | Tableau N°4 | Externe |
| `/dashboards/suivi-absences` | Tableau N°5 | Interne |
| `/dashboards/bilan-social` | Tableau N°6 | Interne / Externe / Global |

### Branche Git / Commit

- **Branche** : `feature/dashboards-rh`
- **Hash** : `5d57771ea16fb84a1fbc5d80aa9d17c1108053d6`
- **Message** :
  `feat(dashboards): page d'accueil 6 cartes + placeholders dashboards 1-6`

### Commandes de rollback

```
git revert 5d57771ea16fb84a1fbc5d80aa9d17c1108053d6
# ou (en local non poussé) :
git reset --hard a9b010eabc6ada2ded41473ff5f129c0bf4b34e0
```

### Validé par

Abdoulaye Dieng &lt;dienguis@hotmail.com&gt; (capture d'écran transmise — 6 cartes affichées correctement).

---

## [Hotfix] 2026-05-02 1430 — Doublon de route @page

**Objet** : suppression d'une directive `@page "/dashboards/"` redondante
dans `DashboardHome.razor` qui causait `System.InvalidOperationException:
The following routes are ambiguous: 'dashboards'` au démarrage de l'app
(découvert lors de la première compilation post-Étape 2).

### Fichiers modifiés

- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/DashboardHome.razor` (-1 ligne)

### Branche Git / Commit

- **Hash** : `a9b010eabc6ada2ded41473ff5f129c0bf4b34e0`
- **Message** :
  `fix(dashboards): remove duplicate @page route on DashboardHome (Etape 2 hotfix)`

### Commandes de rollback

```
git revert a9b010eabc6ada2ded41473ff5f129c0bf4b34e0
```

### Validé par

Abdoulaye Dieng &lt;dienguis@hotmail.com&gt; (build vert + page Login OK).

---

## [WIP integration] 2026-05-02 1330 — Réintégration des fichiers untracked du stash

**Objet** : à la première tentative de build post-Étape 2, on a découvert
que la branche `dev` (au commit `83ef23c`) contenait des modifications
référençant des classes (`Site`, `CentreImports`, `BilanSocialFormulaire`,
…) jamais commitées. Ces classes existaient en untracked dans un stash WIP
hérité, ainsi qu'un module Dashboards parallèle non terminé (PowerBI,
RapportCEO, DashboardEffectif/Interimaire, etc.).

Décision validée avec l'utilisateur : `git stash pop` + commit en bloc
de tous les fichiers untracked sur `feature/dashboards-rh`. Les fichiers
Dashboards parallèles seront **remplacés** progressivement par notre
nouveau module (Tableaux 1 → 6 de la mission).

### Fichiers créés (50)

- 3 entités critiques pour la compilation :
  `BusinessObjects/Site.cs`, `BusinessObjects/CentreImports.cs`,
  `NonPersistent/BilanSocialFormulaire.cs`
- Module Dashboards parallèle (à remplacer) :
  `Controllers/DashboardEffectifController.cs`,
  `Controllers/DashboardInterimaireController.cs`,
  `Services/DashboardEffectifService.cs`,
  `Services/DashboardInterimaireService.cs`,
  `Services/DashboardExportExcelService.cs`,
  `Services/DashboardExportInterimaireService.cs`,
  `BusinessObjects/TableauBordEffectif.cs`,
  `BusinessObjects/TableauBordInterimaire.cs`,
  `Controllers/TableauBordNonPersistentController.cs`,
  `Dashboards/Dash_RH_Effectifs.xml`,
  `Dashboards/Dash_RH_Mouvements.xml`,
  `Dashboards/Dash_RH_SyntheseMensuelle.xml`,
  `Blazor.Server/Controllers/DashboardExportController.cs`
- Module PowerBI (statut à définir) :
  `Blazor.Server/Components/PowerBIReportView.razor`,
  `Blazor.Server/Controllers/PowerBIEmbeddedController.cs`,
  `Blazor.Server/Controllers/RapportPowerBIViewController.cs`,
  `Blazor.Server/Editors/PowerBIIFrameComponent.razor`,
  `Blazor.Server/Editors/PowerBIReportViewItem.cs`,
  `BusinessObjects/PowerBIReportView.cs`,
  `BusinessObjects/RapportPowerBI.cs`,
  `Controllers/PowerBIController.cs`,
  `Services/PowerBIConfigService.cs`
- Module RapportCEO :
  `BusinessObjects/ParamRapportCEO.cs`,
  `Controllers/RapportCEOController.cs`,
  `Services/RapportCEOData.cs`,
  `Services/RapportCEODataService.cs`,
  `Services/RapportCEOEmailService.cs`,
  `Services/RapportCEOExcelGenerator.cs`,
  `Services/RapportCEOPdfGenerator.cs`
- Imports & misc :
  `Controllers/ImportCompteBancaireController.cs`,
  `Controllers/ImportConjointController.cs`,
  `Controllers/CreerModelesEnMasseController.cs`,
  `Controllers/BulletinListViewCleanupController.cs`,
  `Controllers/PeriodePaieEtatsController.cs`,
  `Controllers/SalarieListCountController.cs`,
  `Services/ImportCompteBancaireService.cs`,
  `Services/ImportConjointService.cs`,
  `Services/BulletinModeleService.cs`,
  `Services/DbConfigHelper.cs`,
  `Services/ExcelChartInjector.cs`,
  `NonPersistent/BilanSocialAnneeSelection.cs`,
  `Blazor.Server/wwwroot/help/import-comptes-bancaires.html`,
  `Blazor.Server/wwwroot/help/import-conjoints.html`,
  `Blazor.Server/dbconfig.json`,
  `Module/UnusableNodes44.xml`

### Fichiers modifiés

- `AdiPAIE_V02/AdiPAIE_V02.Module/NonPersistent/DashboardsRHMenu.cs` (+1 using)
- `AdiPAIE_V02/AdiPAIE_V02.Module/Controllers/RH/DashboardsRHNavigationController.cs` (+1 using)

### Branche Git / Commit

- **Hash** : `ca8c3355985243e92c0607ab9497c344831e4520`
- **Message** :
  `chore(wip): integrate untracked entity classes and parallel dashboards module (to be refactored later)`

### Commandes de rollback

```
git revert ca8c3355985243e92c0607ab9497c344831e4520
# Note : ce revert provoquera une re-cassure du build (Site, CentreImports,
# BilanSocialFormulaire à nouveau introuvables). À n'utiliser que si on
# accepte un retour à l'état non-buildable du commit 83ef23c.
```

### Validé par

Abdoulaye Dieng &lt;dienguis@hotmail.com&gt;.

---

## [Étape 2] 2026-05-02 1300 — Architecture cible (squelette)

**Objet** : poser l'arborescence Pages/Shared/Models, l'entrée de menu XAF
qui ouvre les dashboards, le rôle d'accès `RH_Manager` et l'enregistrement DI
du `IMemoryCache` utilisé par les services à venir. Aucun service métier
n'est encore branché — chaque tableau apportera son service au cours de
l'Étape 4.

### Fichiers créés

- `AdiPAIE_V02/AdiPAIE_V02.Module/Models/Dashboards/PersonnelType.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/NonPersistent/DashboardsRHMenu.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Module/Controllers/RH/DashboardsRHNavigationController.cs`
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/_Imports.razor`
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Pages/Dashboards/DashboardHome.razor`
  *(placeholder ; la page d'accueil 6 cartes sera livrée à l'Étape 3)*
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Shared/Dashboards/DashboardLayout.razor`
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Shared/Dashboards/PersonnelTypeToggle.razor`
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Shared/Dashboards/KpiCard.razor`
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Shared/Dashboards/DashboardActions.razor`
- `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Shared/Dashboards/DashboardFiltersBar.razor`
- `sql/dashboards/.gitkeep`

### Fichiers modifiés

| Fichier | Sauvegarde `.bak` | Nature de la modification |
|---|---|---|
| `AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Startup.cs` | `docs/dashboards/backup/2026-05-02_1300/AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/Startup.cs.bak` | Ajout de `services.AddMemoryCache()` (lignes 36-41). |
| `AdiPAIE_V02/AdiPAIE_V02.Module/DatabaseUpdate/Updater.cs` | `docs/dashboards/backup/2026-05-02_1300/AdiPAIE_V02/AdiPAIE_V02.Module/DatabaseUpdate/Updater.cs.bak` | Ajout du `using AdiPAIE_V02.Module.NonPersistent;` (ligne 4) et insertion du bloc rôle `RH_Manager` (lignes 311-392). |
| `docs/dashboards/CHANGELOG.md` | (création initiale, pas de `.bak`) | Renseignement du hash Étape 0, ajout des entrées Étape 1 et Étape 2. |

### Fichiers supprimés

_Aucun._

### Décisions architecturales validées (Étape 1)

- **Point d'entrée** : `NavigationItem` XAF via la classe non persistante
  `DashboardsRHMenu` ; le contrôleur `DashboardsRHNavigationController`
  ajoute un bouton « Ouvrir les Tableaux de Bord » qui redirige
  (JSInterop) vers la page Razor `/dashboards/`.
- **Rôle d'accès** : nouveau rôle `RH_Manager` avec
  `PermissionPolicy = DenyAllByDefault`, lecture seule sur les entités
  sources des dashboards (Salarié, Intérimaire, Contrats, Bulletins,
  Congés, Sites, etc.).
- **Coût intérimaire** : exposé par `ContratInterim.TauxJournalier` (FCFA/jour)
  et `CoutTotalEstime` (NonPersistent). Pour les KPI Min/Max/Moy, on
  mensualise `TauxJournalier × 22 jours ouvrés`.
- **Mapping `Type de contrat`** virtuel — `Salarie.TypeContrat` réel pour
  Interne (CDI/CDD/Stage), valeur fixe `INTERIM` pour Externe. Aucune
  modification du schéma `DomainEnums.TypeContrat`.

### Branche Git / Commit

- **Branche** : `feature/dashboards-rh`
- **Hash** : `9cce1dd34bb407e97da0491650d93546a3b75f59`
- **Message** :
  `feat(dashboards): architecture cible - composants partages, role RH_Manager, entree menu XAF`

### Commandes de rollback

```
git revert 9cce1dd34bb407e97da0491650d93546a3b75f59
# ou (en local non poussé) :
git reset --hard cb0e1c7c9836dc1c3a6bff386121086a4f77b807
```

### Validé par

Abdoulaye Dieng &lt;dienguis@hotmail.com&gt;

---

## [Étape 1] 2026-05-02 1230 — Analyse de l'existant

**Objet** : cartographier le projet AdiPAIE_V02, identifier les conventions
et le mapping entités → KPI à utiliser. **Aucune modification de fichier
projet.** Le rapport et les décisions sont conservés ici pour traçabilité.

### Fichiers créés

_Aucun._

### Fichiers modifiés

_Aucun._

### Synthèse rapide (cf. `README_Dashboards.md` à venir Étape finale)

- Stack : DevExpress XAF 25.1.10 / Blazor Server / .NET 8 / ORM XPO.
- Auth : XAF Security Standard + ASP.NET Identity Cookies.
- INTERNE = `Salarie` (TypeContrat CDI/CDD/Stage). EXTERNE = `Interimaire`
  (entité distincte ; coût via `ContratInterim.TauxJournalier`).
- Site INTERNE = `Site` (Siège / Dépôt CDB / Dépôt Hann).
  Site EXTERNE = `StationService` (réseau de stations service ELTON).
- Existant exploitable pour Tableau N°6 : `Services/BilanSocialService.cs`
  (méthodes statiques `PreRemplir` / `Generer`).
- Aucun composant DevExpress Blazor utilisé en `.razor` jusqu'ici — base
  visuelle à établir.
- Aucune migration DB nécessaire — toutes les KPI sont dérivables des
  entités existantes.

### Commit

_Pas de commit dédié_ — l'analyse Étape 1 est consignée dans cette entrée
et accompagne le commit Étape 2.

### Validé par

Abdoulaye Dieng &lt;dienguis@hotmail.com&gt; (validation des 4 questions
bloquantes : point d'entrée, rôle, coût intérim, mapping contrat).

---

## [Étape 0] 2026-05-02 — Initialisation du module

**Objet** : préparation Git, mise en place de la structure de journalisation
et du dossier de sauvegardes.

### Fichiers créés

- `docs/dashboards/CHANGELOG.md` (ce fichier)
- `docs/dashboards/backup/.gitkeep` (placeholder pour suivre le dossier vide)

### Fichiers modifiés

_Aucun._

### Fichiers supprimés

_Aucun._

### Branche Git

`feature/dashboards-rh` créée à partir de `dev` au commit
`85f717c — GRH v27 - Audit complet, consultation RH bulletins…`.
Le travail en cours précédent a été mis de côté :
`stash@{0}: On dev: WIP avant feature/dashboards-rh`.

### Commit

- **Hash** : `cb0e1c7c9836dc1c3a6bff386121086a4f77b807`
- **Message** :
  `chore(dashboards): initialise module dashboards (CHANGELOG + backup/)`

### Commandes de rollback

- Annuler proprement le commit (recommandé, conserve l'historique) :

  ```
  git revert <hash_du_commit_etape_0>
  ```

- Réinitialiser dur (à n'utiliser que si la branche est encore locale et non
  poussée) :

  ```
  git reset --hard 85f717c
  ```

### Validé par

Abdoulaye Dieng &lt;dienguis@hotmail.com&gt;

---

<!--
  Les entrées suivantes (Étape 1, Tableaux N°1 à N°6, livrables finaux)
  seront ajoutées au-dessus de cette ligne, dans l'ordre chronologique
  inverse (le plus récent en premier).
-->
