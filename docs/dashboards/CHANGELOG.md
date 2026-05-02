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
- **Hash Étape 4.3** : _à renseigner après le `git commit` (commit pas encore réalisé — l'Étape 4.2 occupe HEAD `504cb1ee`)_
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
