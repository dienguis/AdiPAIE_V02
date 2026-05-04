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

---

## 🚀 V1.2 — Roadmap Pilotage Stratégique DAF + DRH (cadrage 2026-05-04)

> **Origine** : revue stratégique post-CODIR. Les 6 dashboards V1.0 couvrent
> le bilan social classique. Pour transformer AdiPAIE en outil de pilotage,
> il manque les rapports financiers (DAF) et stratégiques RH (DRH).
>
> **Cible** : V1.2 = 4 rapports prioritaires (quick wins). V1.3 = 4 rapports
> avancés (R&D nécessaire). Les autres = V2.0 / backlog.

### 9.1 Module **Budget RH** (prérequis V1.2)

**Pourquoi** : sans budget saisi, pas de Budget vs Réalisé. Module socle.

**Entité nouvelle** : `BudgetMasseSalariale`
```
Année (int)              -- 2026
Mois (int 1-12)          -- 1..12
Site (FK Site, nullable) -- NULL = budget global non ventilé
Rubrique (enum)          -- SalairesBase / Primes / TreiziemeMois /
                            Gratifications / Indemnites / ChargesPatronales /
                            AvantagesNature / Formation / Recrutement
Montant (decimal)        -- en FCFA
Source (enum)            -- SaisieManuelle / ImportExcel / RecopieN1 /
                            AutoMensualise
Commentaire (string?)
```

**Écran XAF** : *Paramétrage > Budget RH*
- Bouton "Saisir budget annuel" (formulaire 8 rubriques × 1 montant annuel,
  avec règle de mensualisation par rubrique)
- Bouton "Importer Excel" (template à télécharger : rubrique × mois × site)
- Bouton "Recopier N-1 + inflation %" (gain de temps année 2+)
- Vue tableau croisé mois × rubrique × site (relecture)

**Règles de mensualisation** (auto par défaut, surchargeable) :
- SalairesBase, Indemnites, AvantagesNature → /12 linéaire
- TreiziemeMois → 100 % en décembre
- Gratifications → 50 % juin + 50 % décembre (paramétrable)
- ChargesPatronales → calculé à partir du brut budgété × taux moyen patronal
- Formation, Recrutement → libre, à saisir au mois

**RBAC** : seul DAF + RH_Manager peuvent saisir/importer. RH lecture seule.

**Effort estimé** : 3-4 jours dev + 1 jour test.

---

### 9.2 Dashboard **Budget vs Réalisé Masse Salariale** ⭐ V1.2 — Priorité #1

**Pour qui** : DAF (principal), DG, Contrôle de gestion.
**Quand** : revue mensuelle de gestion.

**KPIs principaux** :
- Budget mois M (FCFA) | Réalisé mois M (FCFA) | Écart valeur | Écart %
- Budget cumulé YTD | Réalisé cumulé YTD | Écart cumulé
- Projection annuelle (réalisé YTD + budget restant) vs Budget annuel
- Top 3 rubriques en dépassement | Top 3 rubriques en sous-consommation
- Ventilation par site (carte de chaleur si écart > seuil)

**Visualisations** :
- Barres groupées Budget/Réalisé par mois (12 mois)
- Tableau écart par rubrique × mois (rouge si > +5 %, vert si < -5 %)
- Graphique cumul YTD (courbe budget vs courbe réalisé)
- Donut écart par site

**Données sources** :
- Réalisé : `Bulletin.BrutFiscal` + `BulletinLigne.MontantEmployeur` (groupé année/mois/site)
- Budget : nouvelle entité `BudgetMasseSalariale`

**Filtres** : Année, Site, Unité organisationnelle, Rubrique.
**Exports** : Excel (template DAF avec mise en forme conditionnelle), PDF (synthèse 1 page).
**RBAC** : DAF + DG uniquement (info sensible).

**Effort estimé** : 5 jours dev (backend + frontend + tests).

---

### 9.3 Dashboard **Provisions Sociales (IDR + Congés Payés)** ⭐ V1.2 — Priorité #2

**Pour qui** : DAF (clôture comptable), Commissaire aux comptes, Audit.
**Quand** : trimestriel obligatoire (clôtures), mensuel idéal.

**Pourquoi critique** : obligation **SYSCOHADA** + **IFRS** (IAS 19 si filiale
groupe coté). Aujourd'hui calculé manuellement en fin d'année → mauvaise
surprise du CAC garantie.

**KPIs** :
- Provision IDR totale au [date] (FCFA)
- Détail par salarié (ancienneté × salaire × barème conventionnel SN)
- Évolution provision IDR mois par mois (12 mois glissants)
- Provision congés payés acquis non pris (FCFA)
- Provision gratifications proratisées (FCFA)
- Top 10 salariés à plus forte provision IDR

**Calcul IDR Sénégal** (Convention Collective Interprofessionnelle) :
```
< 5 ans   : 25 % du salaire mensuel × ancienneté en années
5-10 ans  : 30 % du salaire mensuel × ancienneté en années
> 10 ans  : 40 % du salaire mensuel × ancienneté en années
(plafonné selon CCI applicable)
```

**Calcul Congés Payés** :
```
Solde acquis non pris × (Salaire mensuel / 22 jours)
```

**Données sources** :
- `Salarie.DateEmbauche` (ancienneté)
- `Bulletin.BrutFiscal` (salaire de référence, moyenne 12 derniers mois)
- `SoldeConge` (solde acquis - solde pris)
- Paramètre Société : barème IDR par tranche d'ancienneté (paramétrable)

**Exports** : Excel détaillé par salarié (pour CAC), PDF synthèse.
**RBAC** : DAF + RH_Manager.

**Effort estimé** : 4 jours dev (calcul barème CCI complexe).

---

### 9.4 Dashboard **Coût Complet par Salarié (Fully Loaded Cost)** ⭐ V1.2 — Priorité #3

**Pour qui** : DAF, DRH, Managers (business case embauche).

**Pourquoi** : aujourd'hui personne ne sait ce que coûte vraiment un salarié.
Un Brut de 500 000 FCFA = ~720 000 FCFA en coût complet (charges + avantages).

**KPIs par salarié** :
- Salaire brut moyen mensuel
- Charges patronales (IPRES + CSS + IPM + FPS + autres)
- Avantages en nature évalués (logement, véhicule, téléphone, carburant)
- Formation N (montant)
- Total **Coût Employeur Annuel**
- Ratio Coût Total / Salaire Net (le multiplicateur magique)

**KPIs agrégés** :
- Coût moyen ETP par catégorie pro (Cadre / Maîtrise / Employé / Ouvrier)
- Coût moyen par site / unité
- Top 10 coûts les plus élevés (avec contexte poste/ancienneté)

**Visualisations** :
- Donut décomposition coût (Salaire net | Cotisations salariales | Charges patronales | Avantages | Formation)
- Boxplot coût par catégorie pro
- Tableau Top 10 + Bottom 10

**Données sources** :
- `Bulletin.BrutFiscal`, `Bulletin.NetAPayer`
- `BulletinLigne.MontantEmployeur` (toutes les charges)
- `BulletinLigne` famille AV_NATURE_* (avantages nature)
- `Formation.CoutTotal` (à créer ou récupérer si existe)

**Filtres** : Année, Site, Unité, Catégorie pro, Sexe.
**Exports** : Excel (1 ligne / salarié pour analyses RH).
**RBAC** : DAF + RH_Manager + DG.

**Effort estimé** : 4 jours dev.

---

### 9.5 Dashboard **Conformité Sénégal** ⭐ V1.2 — Priorité #4

**Pour qui** : DRH, DAF, Audit interne, Inspection du Travail.

**Pourquoi** : un seul écran qui dit "êtes-vous audit-ready ?".

**Indicateurs OK/KO (feux tricolores)** :
- ✅/❌ Tous les salariés au-dessus du SMIG (60 000 FCFA actuel)
- ✅/❌ Toutes les déclarations IPRES du trimestre faites
- ✅/❌ Toutes les déclarations CSS du trimestre faites
- ✅/❌ Toutes les déclarations IPM/Mutuelle santé faites
- ⚠️ Salariés avec > 30 jours de congés acquis (risque légal)
- ⚠️ CDD au-delà de 2 ans (requalification CDI possible)
- ⚠️ Heures supplémentaires > seuil mensuel légal (15h/sem)
- ⚠️ Salariés sans contrat scanné dans le SI
- ⚠️ Stagiaires au-delà de 6 mois (transformation obligatoire)

**Visualisations** : tableau de bord type "checklist" avec compteurs et liens
vers la liste détaillée des cas non conformes.

**Données sources** : transverse (Salarie, Bulletin, ContratSalarie, SoldeConge,
MouvementHeuresSup, ParametresPaie pour les seuils).

**RBAC** : DAF + RH_Manager + DRH.

**Effort estimé** : 3 jours dev (peu de calcul, beaucoup de jointures).

---

### 9.6 Roadmap V1.3 (R&D nécessaire)

| # | Rapport | Pourquoi V1.3 (pas V1.2) | Effort |
|---|---|---|---|
| 9.7 | **GPEC / Skill Matrix** | Nécessite refonte modèle Compétences (entité absente aujourd'hui) | 7 j |
| 9.8 | **Plan de relève (Succession Planning)** | Nouveau workflow : identifier postes critiques + 2 successeurs avec readiness | 5 j |
| 9.9 | **Pay Equity Gap H/F** | Méthodologie à définir avec RH (poste équivalent ?), sensible juridiquement | 4 j |
| 9.10 | **Suivi entretiens annuels** | EntretienAnnuel existe mais workflow incomplet (objectifs N+1, plan d'action) | 4 j |

### 9.7 Backlog V2.0 (long terme)

- **Performance par station-service** (effectif/CA, masse salariale/CA) → nécessite intégration avec ERP commercial Fafadie
- **HSE / AT-MP** → nécessite module Sécurité (entités absentes)
- **Coût caché Turnover & Absentéisme** → calculs OK mais valeur ajoutée moindre que les 4 prioritaires
- **Climat social / NPS interne** → nécessite module Enquêtes/Sondages (R&D)

### 9.8 Synthèse priorisation V1.2

| # | Dashboard | DAF | DRH | Effort | ROI |
|---|---|:-:|:-:|:-:|:-:|
| 9.1 | Module Budget RH (socle) | ⭐⭐⭐ | ⭐ | 4 j | Prérequis 9.2 |
| 9.2 | Budget vs Réalisé MS | ⭐⭐⭐ | ⭐⭐ | 5 j | 🔥 Très fort |
| 9.3 | Provisions IDR + CP | ⭐⭐⭐ | ⭐ | 4 j | 🔥 Obligation comptable |
| 9.4 | Coût Complet Salarié | ⭐⭐⭐ | ⭐⭐⭐ | 4 j | 🔥 Argument différenciant |
| 9.5 | Conformité Sénégal | ⭐⭐ | ⭐⭐⭐ | 3 j | Audit-ready |

**Total V1.2** : ~20 jours dev (≈ 4-5 semaines avec validations métier).

**Workflow recommandé** :
1. **Semaine 1** : présenter cette roadmap au CODIR (DAF + DRH) → arbitrage ordre/scope
2. **Semaine 2-3** : Module Budget RH (socle) + Conformité Sénégal (parallèle, équipes différentes possibles)
3. **Semaine 4** : Budget vs Réalisé (dépend du module Budget)
4. **Semaine 5** : Provisions Sociales + Coût Complet
5. **Semaine 6** : tests UAT + ajustements + livraison V1.2

### 9.9 Pré-requis métier à valider AVANT dev

À soumettre au DAF :
- [ ] Liste des rubriques budget (alignée sur le PCS comptable ?)
- [ ] Règles de mensualisation par rubrique (saisonnalité primes/gratifications)
- [ ] Barème IDR applicable à ELTON (CCI standard ou avenant spécifique ?)
- [ ] Seuils d'alerte budget vs réalisé (5 % ? 10 % ?)
- [ ] Méthodo valorisation avantages nature (forfait fiscal ou réel ?)

À soumettre au DRH :
- [ ] Définition "poste équivalent" pour Pay Equity (V1.3)
- [ ] Liste des postes critiques pour Plan de relève (V1.3)
- [ ] Référentiel compétences pour GPEC (V1.3)
- [ ] Seuil heures sup mensuel selon convention collective applicable

---

## ⭐ NOTES POST-V1.1 (mai 2026)

- **Pyramide des âges H/F** : ajoutée au dashboard Effectif détaillé
  (style PPT slide 15, barres divergentes, tranches 55+ → <25). Code agrégé
  côté Razor à partir de `BarStackHommesFemmes` existant — pas de modif service.
- **Rapport CEO masqué** (V1.1) : l'action XAF `GenererRapportCEO`, les 4
  paramètres ParametresPaie (EmailCEO, SeuilTurnoverPct, SeuilAbsenteismePct,
  SeuilMasseSalariale) et le LayoutGroup `Tab_RapportCEO` sont désactivés via
  `[Browsable(false)]` + `IsVisible="False"` + `Active.SetItemValue(...)`. Code
  conservé pour éviter régression. Suppression définitive à prévoir dans un
  Sprint cleanup ultérieur (probablement post-V1.2).
- **DPAPI password** : depuis 2026-05-03, `dbconfig.json` est dans
  `C:\ProgramData\AdiPAIE_V02\` et le password SQL est chiffré DPAPI scope
  LocalMachine. **Ne JAMAIS faire `dotnet clean`** sans avoir vérifié que
  ProgramData contient bien le fichier (sinon perte connection string).

