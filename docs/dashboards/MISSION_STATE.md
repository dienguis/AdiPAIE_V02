# MISSION_STATE.md — Tableaux de Bord RH

> **Pour Claude (futur-moi) :** ce fichier est ma mémoire de travail
> persistante, pour survivre aux compressions de contexte. **À lire en
> tout premier au début de chaque session** avant toute autre action.
> À mettre à jour à chaque fin d'étape.
>
> **Pour humain :** snapshot synthétique de l'avancement et des décisions.

---

## 0. Contexte projet

- **Repo** : `C:\Dev\AdiPAIE_V02` (Windows host)
- **Stack** : DevExpress XAF 25.1.10 / Blazor Server / .NET 8 / XPO ORM
- **Sécurité** : SecuredXpo + ASP.NET Identity Cookies
- **Branche** : `feature/dashboards-rh` (NE PAS travailler sur `main`)
- **Société** : ELTON Oil — palette **Navy `#142E4D`**, **Orange `#F18A1C`**, **Rouge `#E63946`**
- **Mission** : 6 dashboards RH (INTERNE = Salariés CDI/CDD/Stage, EXTERNE = Intérimaires)

## 1. Progression — étapes

| # | Étape | Statut | Hash commit | Date |
|---|---|---|---|---|
| 0 | Préparation Git + journalisation | ✅ done | — | 2026-05-02 |
| 1 | Analyse de l'existant | ✅ done | — | 2026-05-02 |
| 2 | Architecture cible + DI | ✅ done | — | 2026-05-02 |
| 3 | Page d'accueil DashboardHome | ✅ done | `5d57771e` | 2026-05-02 |
| 4.1 | Tableau N°1 — Effectif détaillé | ✅ done | (cf. CHANGELOG) | 2026-05-02 |
| 4.2 | Tableau N°2 — Analyse de l'Effectif | ✅ done | `504cb1ee` | 2026-05-02 |
| 4.3 | Tableau N°3 — Mouvements | ✅ done | `2e1347bd` | 2026-05-02 |
| 4.4 | Tableau N°4 — Rémunération | ✅ done (build OK, valeurs validées user) | _hash à fournir_ | 2026-05-02 |
| 4.5 | Tableau N°5 — Suivi Absences | ✅ done (UI OK, seed sans absences) | `686fb3b8` | 2026-05-02 |
| 4.6 | Tableau N°6 — Bilan Social Mensuel | ✅ done (UI OK, valeurs cohérentes) | `cae0763` | 2026-05-03 |
| 7.1 | Aide en ligne — 7 pages HTML + bouton ? | ✅ done | _commit pending_ | 2026-05-03 |
| 7.2 | Export Excel (ClosedXML) — 6 tableaux | ✅ done | _commit pending_ | 2026-05-03 |
| 7.3 | Export PDF (QuestPDF) — 6 tableaux | ✅ done (licence Community pour dev) | _commit pending_ | 2026-05-03 |
| 7.4 | Bilan Social option B — ligne « dont Intérimaires » | ✅ done | _commit pending_ | 2026-05-03 |
| 7.x | Hot-fix nouvel onglet + lisibilité boutons | ✅ done | _commit pending_ | 2026-05-03 |
| 7.SEC | Guard d'authentification (faille critique) + Bootstrap Icons CDN | ✅ done | _commit pending_ | 2026-05-03 |
| 7.SEC RBAC | RBAC complet (Administrators / RH_Manager / RH / DAF) + helper partagé | ✅ done | `b7815bb` | 2026-05-03 |
| 7.UX | Nettoyage 3 menus « Tableaux de bord » legacy en doublon | ✅ done | `ce1d7af` | 2026-05-03 |
| Final | Livrables : README + install.sql | ✅ done | `8680d001` | 2026-05-03 |

## ⭐ MISSION V1.0 TERMINÉE — HEAD = `8680d001` ⭐

---

## 🆕 V1.1 — Refonte module Intérim (en cours)

> **🚨 CHANGEMENT D'ORIENTATION (2026-05-03)** :
> Après revue métier ELTON, le modèle simpliste « Station + BU » s'avère
> insuffisant. Le module intérim doit modéliser :
> - 🏪 Stations service (~15 sites avec leurs 4 BU)
> - 🏢 Siège / Direction Générale (avec départements DSI / Commerciale /
>     Admin&Fin / RH, et sous-segments comme Consommateurs / BTP / Mines
>     pour la Commerciale)
> - 📦 Dépôts (plusieurs sites)
> - **Mouvements** entre tous ces types de lieux
> - **Multi-affectation** d'un intérimaire (plusieurs segments simultanés)
>
> Le projet étant **en dev (rien livré en prod)**, on a la liberté de
> tout casser/refaire sans contrainte de migration de données.
>
> **Phase 1 BusinessUnitType** (code prêt mais pas commité) → **abandonné**
> au profit du modèle hiérarchique unifié ci-dessous.

### Architecture cible V1.1

```
Site (existant, ENRICHI avec TypeSite)
├── TypeSite enum : StationService / Siege / Depot / Autre
└── XPCollection<UniteOrganisationnelle> Unites

UniteOrganisationnelle (NOUVELLE, récursive)
├── Site (FK obligatoire = racine)
├── Parent (FK self, nullable = hiérarchie illimitée)
├── TypeUnite enum : BU / Departement / Segment / Autre
└── Palette (CouleurPalette, héritée de la Phase 1 abandonnée)

ContratInterim (REFONDU)
├── Site (FK obligatoire)
└── Unites (collection N-N → UniteOrganisationnelle, multi-rattachement)

MouvementInterimaire (REFONDU)
├── SiteOrigine / SiteDestination
└── UniteOrigine / UniteDestination

Entités SUPPRIMÉES :
  - StationService (devient Site Type=StationService)
  - BusinessUnitStation (devient UniteOrganisationnelle Type=BU)
  - BusinessUnitType (Phase 1) — palette migrée sur UniteOrganisationnelle
```

### Sprints V1.1

| Sprint | Objet | Effort | Statut | Hash |
|---|---|---|---|---|
| 1A | Modèle Site enrichi + UniteOrganisationnelle (cohabitation) | 4h | ✅ done | `4ec43373` |
| 1A.2 | Vues XAF (xafml override pour exposer Site.Type) | 30 min | ✅ done | `ac2d2311` |
| 1B | Seed démo COMPLET (~100 entrées) + Controller wipe + flag appsettings + RBAC | 4h | ✅ done | _consolidé_ |
| 1B.2 | Hot-fix duplication grilles XAF | 15 min | ✅ done | `461c0b21` |
| 1C | Refonte 4 services + razor EXTERNE (filtres Site→Unité) | 2 jours | ✅ done | `515b2531` (final) |
| 1D | Masquage entités legacy (DefaultClassOptions retiré, [Legacy] sur FK) | 0.5 jour | ✅ done | `082da74` |
| 1D.3 | Finitions tardives : appsettings + Site + dashboards + KpiCard + CSS | 1h | ✅ done | `febfb1f` |
| 1E | Refonte 6 SQL + install.sql + README + MISSION_STATE | 0.5 jour | ✅ done | _voir prochaine MAJ_ |
| 1F (futur) | Nettoyage cosmétique : dead code Razor + suppression définitive | 1h | 🕒 plus tard | — |

### Sprints associés (parallèles à V1.1)

| Sprint | Objet | Statut | Hash |
|---|---|---|---|
| Help.A | Refonte interimaires.html + bulletins.html + salaries.html + conges.html + help-shared.css | ✅ done | _consolidé dans Help.B+C_ |
| Help.B+C | Refonte 19 pages help (modules secondaires + admin/imports) au format step-by-step | ✅ done | `e2806c2` |
| Rescue | Récupération 9 fichiers V1.1 perdus (Remuneration*, DemoDataSeeder, IDashboard*ExportService) depuis dangling stash `ea7826` | ✅ done | `8987dab` |
| Infra+Sec | dbconfig.json en %ProgramData% (survit clean) + chiffrement DPAPI password | ✅ done | `2865ac3` |

## ⭐ MISSION V1.1 TERMINÉE — HEAD `dev` = `2865ac3` (push origin/dev) ⭐

Branche `feature/dashboards-rh` supprimée après merge `c3dcade` dans `dev`.
Branche `feat` (orphan polluée) supprimée. État du repo final :

```
* dev          → c3dcade (merge V1.0+V1.1+Help) + febfb1f + e2806c2 + 8987dab + 2865ac3
                 = HEAD `2865ac3` (synced origin/dev)
* master       → dd67ea0 (init projet)
* origin/dev   → 2865ac3
* origin/master→ dd67ea0
```

Pour livrer en prod : merger `dev` → `master` (après validation utilisateur 1-2 jours).

### Convention seed démo

- Tous les enregistrements seed ont **`Code` préfixé `DEMO_`** (ex: `DEMO_BANDIA`, `DEMO_BU_BOUTIQUE_BANDIA`, `DEMO_SIEGE`, `DEMO_DEPT_DSI`)
- Flag `appsettings.json` → `Dashboards:SeedDemoData` (true/false)
- Controller XAF « Vider données démo » → supprime tout `Code LIKE 'DEMO_%'`
- Pour passer en prod : `false` dans appsettings + clic sur le bouton wipe

## 2. Architecture du module — fichiers clés

```
AdiPAIE_V02/AdiPAIE_V02.Module/
├── Domain/DomainEnums.cs         (enums MotifDepart, ContratInterimStatut, Sexe...)
├── Models/Dashboards/
│   ├── PersonnelType.cs          (Interne / Externe / Global)
│   ├── AgeBucket.cs              (<25 / 25-34 / ... / 55+)
│   ├── AncienneteBucket.cs       (<1 / 1-4 / 5-9 / 10-14 / >=15 / vide  +  AgeBucketAnalyse)
│   ├── EffectifDetailleFilterModel.cs + EffectifDetailleDto.cs    (Tab 1)
│   ├── AnalyseEffectifFilterModel.cs + AnalyseEffectifDto.cs      (Tab 2 — incl. BarItemDto partagé)
│   └── MouvementsFilterModel.cs + MouvementsDto.cs                (Tab 3)
└── Services/Dashboards/
    ├── IEffectifDetailleDashboardService.cs + EffectifDetailleDashboardService.cs
    ├── IAnalyseEffectifDashboardService.cs + AnalyseEffectifDashboardService.cs
    └── IMouvementsDashboardService.cs + MouvementsDashboardService.cs

AdiPAIE_V02/AdiPAIE_V02.Blazor.Server/
├── Startup.cs                                  (DI : AddScoped<IXxxDashboardService>)
├── Pages/_Host.cshtml                          (link css/dashboards-elton.css)
├── wwwroot/css/dashboards-elton.css            (charte ELTON partagée — toutes classes .ed-*)
└── Pages/Dashboards/
    ├── DashboardHome.razor
    ├── Effectif/EffectifDetailleDashboard.razor      (Tab 1)
    ├── Effectif/AnalyseEffectifDashboard.razor       (Tab 2)
    └── Mouvements/MouvementsDashboard.razor          (Tab 3)

sql/dashboards/
├── 01_effectif_detaille.sql
├── 02_analyse_effectif.sql
└── 03_mouvements.sql

docs/dashboards/
├── CHANGELOG.md                  (audit log + hashes + rollback commands)
├── SPEC_PowerBI_DAX_to_SQL.sql   (spec utilisateur — 11 mesures de référence)
└── MISSION_STATE.md              (CE FICHIER)
```

## 3. Décisions/conventions VERROUILLÉES (ne pas réinventer)

### Données INTERNE (Salarie)
- **Filtre DateSortie sentinelle** : `>= 1900-01-01` (gère DateTime.MinValue + epoch Excel + null)
- **Effectif moyen INTERNE** : `(effectif au 1/1 + effectif au 31/12) / 2` (population stable)
- **Genre** : `Salarie.Sexe` (Masculin = 0, Féminin = 1)
- **Segment** : `Salarie.Departement.Nom` (accès défensif via réflexion `SafeDepartementNom` si propriété directe absente)
- **Catégorie** : `Salarie.Categories.Intitule`
- **Motif départ** : enum `MotifDepart` (0=Démission, 1=Licenciement, 2=Retraite, 3=FinCDD, 4=RuptureConventionnelle, 5=Décès, 6=Autre)

### Données EXTERNE (Interimaire)
- **Pas de Sexe** sur Interimaire → filtre Genre masqué côté EXTERNE
- **Contrat actif** = `DateDebut > 1900-01-01 AND DateDebut <= dateRef AND (DateFin < 1900-01-01 OR DateFin > dateRef)`
  → **PAS de filtre sur `Statut`** (la seed a 40/41 contrats Statut=Termine alors qu'ils sont actifs)
- **Site EXTERNE** = `ContratInterim.Station` (StationService, pas Site)
- **Catégorie EXTERNE** = `ContratInterim.PosteOccupe.Libelle` (PosteInterimaire)
- **Société intérim** = `ContratInterim.SocieteInterim` (1 seule en seed : SEN-INTERIM)
- **Effectif moyen EXTERNE** (Tab 3) : `MAX(ETP_pondéré_13_dates, NbDistinctInterimsAnnee)` — anti-aberration ramp-up
- **ContratInterimStatut** : Brouillon=0, EnCours=1, Terminé=2, Résilié=3
- **Mapping INTERNE↔EXTERNE** documenté dans CHANGELOG Étape 4.2

### Code patterns
- **DI** : `INonSecuredObjectSpaceFactory` injecté dans la page Razor → la page passe `IObjectSpace` au service en paramètre. **NE PAS** essayer d'injecter `XafApplication` (impossible dans services scopés).
- **Cache KPI** : `IMemoryCache` TTL 5 min, clé via `filter.ToCacheKey()`, `InvalidateCache(filter)` exposé.
- **Lookups défensifs** : try/catch `os.GetObjectsQuery<T>().ToList()` car certaines collections (Contrats, Categories) peuvent throw selon état.
- **Razor `else { }` blocks** : code mode direct, **PAS de `@{ }`** dedans (cause RZ1010). Écrire `var x = ...;` direct.
- **DevExpress 25.1** : `GridTextAlignment.Right` (pas End), pas de `WordWrapMode`, pas de `ChartElementFormat.Percent`. Stacked bars → préférer HTML/CSS pour robustesse.
- **DxComboBox / DxTagBox** : toujours expliciter `TData="..." TValue="..."`.

### CSS partagé (`wwwroot/css/dashboards-elton.css`)
Variables CSS clés :
- `--ed-bg-dark : #0F1F38` / `--ed-bg-dark-2 : #142E4D` (Navy ELTON) / `--ed-bg-dark-3 : #1F4376`
- `--ed-orange : #F18A1C` / `--ed-orange-soft : rgba(241,138,28,.12)`
- `--ed-red : #E63946` / `--ed-up : #2EA75B` / `--ed-down : #E63946`
- `--ed-blue : #4DA3FF` (silhouette Hommes) / `--ed-pink : #FF6FB5` (silhouette Femmes)
- `--ed-text : #142E4D` / `--ed-text-soft : #5C6679` / `--ed-text-dark : #E6EAF1`

Classes utilisables (préfixe `.ed-*`) : `header`, `kpigroup`, `tile`, `kpi-effectif`, `filters`, `toggle`, `kpiinline`, `vbar-chart`, `hbar-chart`, `curve`, `footer`.

## 4. Pièges connus / hot-fixes appliqués

- **EspaceSalarieHelper** : `FindUserByName` + `FindSalarieByCriteria` wrappés en try/catch ArgumentException → return null (cassait sur NonPersistentObjectSpace).
- **Updater RH_Manager** : ajout du rôle avec `using AdiPAIE_V02.Module.NonPersistent;` et TypePermissions Read.
- **`Microsoft.Extensions.Caching.Memory` 8.0.1** : ajouté en PackageReference au projet Module.
- **Pré-existant en dev** : erreurs CS0246 sur Site / CentreImports / BilanSocialFormulaire — résolus par stash pop initial.
- **Ne PAS écrire `@{` dans les `else` Razor** (cause RZ1010).
- **virtiofs cache stale** : si bash semble voir une vieille branche git, utiliser le `Read` tool plutôt que bash.
- **Sandbox sans dotnet** : pas de build local possible. La validation se fait côté Windows par le user.

## 5. Workflow Git

Le user lance les commandes Git depuis PowerShell (Visual Studio peut tenir un lock). Mon rôle : préparer la liste exacte de fichiers + le message de commit avec ASCII pur (pas de markdown link, pas d'emojis).

Pattern :
```
git add <fichiers...>
git commit -m "<type>(dashboards): <description>"
git rev-parse HEAD
```

Le user me retourne le hash, j'enregistre dans CHANGELOG.md + ce fichier.

## 6. Étape suivante : 4.4 — Tableau N°4 Rémunération (EXTERNE focus)

D'après SPEC_PowerBI mesures 6, 7, 8 :
- **Masse Brute** (= sum salaire + heures supp + primes)
- **Coût Employeur** (Masse Brute × charges patronales)
- **Écart Salarial H/F**
- Évolution par mois sur 12 mois
- Décomposition par catégorie / site / contrat type

Source EXTERNE : `ContratInterim.TauxJournalier` × jours travaillés (`ContratInterim.NbJoursTravailles` ?) → vérifier le modèle d'entité avant codage.

**Validation à demander au user avant codage** :
1. Source du salaire INTERNE (Salaire brut mensuel sur Salarie ? ou via une entité Bulletin de paie ?)
2. Source des charges patronales (taux unique configurable ? ou calcul par rubrique ?)
3. Périodicité : mensuel glissant 12 mois ? annuel ?
4. Pour EXTERNE : `TauxJournalier × NbJoursTravailles` ou autre formule ?

---

_Dernière MAJ : 2026-05-03 — Mission V1.1 clôturée (Sprint 1E SQL/README/MISSION_STATE).
HEAD `dev` = `2865ac3` synced origin/dev. Toutes les étapes V1.0 + V1.1 + Help.B+C
sont commitées et poussées. Sprint 1F (cleanup dead code) reporté._

## 7. Annexes — sources data validées par utilisateur (Étape 4.4 & au-delà)

### Tab 4 — Rémunération

**Salaire INTERNE — entité `Bulletin`** (table dbo.Bulletin, ~1756 lignes seed) :
- `Bulletin.Annee` (int) + `Bulletin.Mois` (int)
- `Bulletin.Salarie` (FK)
- `Bulletin.BrutFiscal` (montant brut imposable)
- `Bulletin.NetAPayer`
- `Bulletin.TotalCotisationsSociales`
- `Bulletin.TotalRetenuesFiscales`
- `Bulletin.GCRecord` (soft-delete XPO — TOUJOURS filtrer `IS NULL`)

**Charges patronales — entité `BulletinLigne`** (table dbo.BulletinLigne, ~22991 lignes seed) :
- `BulletinLigne.Bulletin` (FK)
- `BulletinLigne.Rubrique` (FK)
- `BulletinLigne.Montant` (part salariale)
- `BulletinLigne.MontantEmployeur` (charges patronales — la colonne clé Tab 4)
- `BulletinLigne.GCRecord`

**Décomposition rubriques — entité `Rubrique` + `RubriqueTypeRef`** :
- `RubriqueTypeRef.Code` ∈ {BRUTE, INDEM_IMPOSA, INDEM_NON_IMPOSA, AV_NATURE_IMPOSABLE/AvNatImpos, AV_NATURE_NON_IMPOSABLE, COTSOC, COTFISC, RETENUE}
- 7 familles macro pour Tab 4 page 2 / Tab 6 (Bilan Social).

**Salaire EXTERNE (intérimaires)** :
- Formule validée : `ContratInterim.TauxJournalier × 22 jours × DATEDIFF(MONTH, DateDebut, COALESCE(DateFin, GETDATE()))`
- ~41 contrats actifs

**KPI Tab 4 cibles** (cf. SPEC_Custom_Dashboard_SQL.sql mesures 6, 7, 8, 9, 10) :
1. Masse Brute (`SUM(Bulletin.BrutFiscal)` filtré année)
2. Coût Employeur (Masse Brute + `SUM(BulletinLigne.MontantEmployeur)`)
3. Net total (`SUM(NetAPayer)`)
4. Charges patronales totales
5. Écart salarial H/F par catégorie
6. Évolution mensuelle 12 mois (Bulletin.Mois)
7. Décomposition par famille de rubrique (RubriqueTypeRef.Code)

**Périodicité Tab 4** : année calendaire avec slicer Année.

**Référence SQL utilisateur** : `C:\Dev\AdiPAIE_V02\TestData\PowerBI\SPEC_Custom_Dashboard_SQL.sql`
(266 lignes — j'ai cartographié les requêtes prêtes à transposer en C#).

## 8. Pièges PowerShell pour le commit (à NE PAS reproduire)

- **Ne PAS utiliser `^`** comme line-continuation — c'est `cmd.exe`, pas PowerShell. PowerShell utilise le **backtick `` ` ``** ou les **lignes uniques**.
- **Ne PAS coller des paths au format markdown** (`[CHANGELOG.md](http://CHANGELOG.md)`) → l'autolink du terminal Claude transforme `.md` en lien et le user copie-colle ça par erreur.
  → **Toujours fournir les commandes git en bloc <code> brut, en lignes individuelles, sans markdown.**
- Préférer **plusieurs `git add` séparés** ou **`git add docs/dashboards/`** par dossier plutôt qu'une longue commande multi-ligne.
