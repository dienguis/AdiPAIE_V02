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

| # | Rapport | Pourquoi V1.3 (pas V1.2) | Effort | Priorité |
|---|---|---|---|---|
| **9.6.0** | **🔥 Coût Réel Intérimaires (Import facture société d'intérim)** | Nouveau modèle de données (BulletinInterim + ImportBatch), import Excel mensuel, écart vs contrat | 8 j | ⭐⭐⭐ #1 |
| 9.7 | **GPEC / Skill Matrix** | Nécessite refonte modèle Compétences (entité absente aujourd'hui) | 7 j | #2 |
| 9.8 | **Plan de relève (Succession Planning)** | Nouveau workflow : identifier postes critiques + 2 successeurs avec readiness | 5 j | #3 |
| 9.9 | **Pay Equity Gap H/F** | Méthodologie à définir avec RH (poste équivalent ?), sensible juridiquement | 4 j | #4 |
| 9.10 | **Suivi entretiens annuels** | EntretienAnnuel existe mais workflow incomplet (objectifs N+1, plan d'action) | 4 j | #5 |

### 9.6.0 Détail — Coût Réel Intérimaires (cadrage 2026-05-05)

**Origine** : revue avec Abdoulaye 2026-05-05. Le coût intérim actuel est
théorique (`TauxJournalier × 22 × N mois`) alors que la VRAIE dépense ELTON
est le TTC de la facture envoyée par chaque société d'intérim chaque mois.

**Fichier source** : `LIVRE DE PAIE interimaire.xlsx` analysé — structure
type 254 intérimaires × 53 colonnes. Colonnes critiques :
- col 49 **Débours** : ce que la société d'intérim a payé (brut + charges + indemnités)
- col 50 **Commissions agence** : la marge de la société d'intérim (~9 %)
- col 51 **Montant HT** = Débours + Commissions
- col 52 **TVA** (18 %)
- col 53 **TTC** = HT + TVA = **coût réel ELTON**

**Ordre de grandeur observé** (mars 2026) :
- 254 intérimaires
- Brut imposable total : 33,8 M FCFA
- TTC total facturé : **63,1 M FCFA**
- Multiplicateur Brut → TTC : **×1,87**
- Annualisé : ~757 M FCFA / an (chiffre majeur non visible aujourd'hui)

**Modèle de données proposé** :

```csharp
// 1 ligne = 1 intérimaire × 1 mois × 1 société émettrice
public class BulletinInterim : BaseObject
{
    public int Annee, Mois;
    public SocieteInterim SocieteEmettrice;
    public Interimaire Interimaire;          // FK lookup par Matricule
    public string MatriculeOriginal;          // Conserve "PRESTATAIRE" si non matché
    public string NomComplet, Fonction, Site; // Snapshot fichier
    public decimal Trentieme;                 // Présence (0..30)

    // Éléments salaire
    public decimal SalaireBase, BrutImposable, NetAPayer;
    public decimal IpresSal, IpresPat, CssAll, CssAcc, IpmSal, IpmPat;
    public decimal CFCE, RetenueIR, RetenueTRIMF;
    public decimal PrimeTransport, PrimePanier, IndemnitesDiverses;

    // BLOC FACTURATION (cœur métier)
    public decimal Debours;
    public decimal CommissionAgence;
    public decimal MontantHT;
    public decimal TVA;
    public decimal TTC;                       // Coût réel pour ELTON

    // Traçabilité import
    public DateTime DateImport;
    public string FichierSource, ImportePar;
    public Guid ImportBatchId;
}

// Audit/grouping
public class ImportBulletinInterimBatch : BaseObject
{
    public DateTime DateImport;
    public SocieteInterim Societe;
    public int Annee, Mois;
    public string FichierSource, ImportePar;
    public int NbLignesImportees;
    public decimal TotalTTC;
    public string Notes;
}
```

**Workflow utilisateur** :
1. Société d'intérim envoie facture mensuelle (Excel)
2. RH/DAF clique *Intérim → Charger livre de paie*
3. Wizard 4 étapes : Année/Mois/Société → Upload → Preview + mapping → Confirm
4. Système crée 1 `ImportBulletinInterimBatch` + N `BulletinInterim`
5. Idempotence : si batch existant pour (Année, Mois, Société) → confirm écraser
6. Dashboard N°11 affiche le coût réel + écart vs contrat

**Dashboard N°11 — Coût Réel Intérimaires** :
- 4 KPIs : TTC mois, TTC YTD, Coût moyen par intérimaire, Multiplicateur Brut→TTC
- Décomposition donut : Brut / Charges pat / Commission agence / TVA
- Top 10 intérimaires les plus coûteux (TTC)
- Comparaison **Contrat (TauxJournalier×22×mois) vs TTC réel** par intérimaire (écart valeur + %)
- Évolution mensuelle 12 mois TTC
- Filtres : Année, Mois, Site, Société d'intérim

**Bénéfices attendus** :
- Validation factures société d'intérim avant paiement
- Détection intérimaires "fantômes" (TTC élevé + 30ème faible)
- Comparaison sociétés d'intérim (commission agence à renégocier)
- Bilan social externe avec coût réel (au lieu du théorique actuel)
- Argument différenciant fort vs Fafadie Paie

**Edge cases identifiés** :
- Matricule `PRESTATAIRE` (1 ligne sur 254) — pas de lookup Intérimaire possible
- Matricules orphelins (le fichier facture peut contenir des intérimaires non encore créés dans le SI) → option "Créer auto" ou "Skip avec warning"
- Mapping colonnes : titre des colonnes est stable mais attention aux variations ("Débours" vs "Debours")
- Lignes vides, totaux en bas du fichier → détecter et ignorer

**Effort total** : 8 jours dev + 1 jour UAT = ~2 semaines.

#### Décisions métier validées (2026-05-05) — réponses Abdoulaye

1. **Sociétés d'intérim multiples** : ELTON travaille avec PLUSIEURS sociétés
   d'intérim simultanément. Chaque facture est liée à une société émettrice
   précise via la FK `BulletinInterim.SocieteEmettrice`.

2. **Création automatique d'Intérimaire si non existant** : si le matricule
   du fichier importé ne correspond à aucun `Interimaire` existant en base,
   on **crée automatiquement la fiche** avec le minimum (Nom, Prénom,
   Matricule, Sexe, Fonction). Un **rapport post-import** liste les fiches
   créées automatiquement → le RH doit ensuite **compléter ces fiches +
   créer le contrat correspondant**. Status d'import à prévoir :
     - `Importé OK` (fiche existante, contrat existant)
     - `Fiche créée auto` (fiche créée, contrat manquant — alerte RH)
     - `Prestataire` (cas spécifique, cf. point 3)

3. **Cas "PRESTATAIRE"** (ligne du fichier où Matricule = "PRESTATAIRE") :
   il s'agit d'un **prestataire indépendant mis à disposition par la société
   d'intérim**. Pas de fiche Interimaire à créer. Le coût TTC est compté
   dans les totaux mais flaggé comme prestataire :
     - `BulletinInterim.IsPrestataire = true`
     - `BulletinInterim.Interimaire = null`
     - `BulletinInterim.MatriculeOriginal = "PRESTATAIRE"`
   Dans le dashboard N°11, prévoir un toggle "Inclure prestataires" ou une
   ligne séparée pour distinguer.

4. **Conservation historique** : **TOUS** les `BulletinInterim` sont
   conservés ad vitam. Pas de purge automatique. Permet l'analyse
   historique sur 5+ ans (utile pour bilan social externe consolidé,
   benchmarks vs N-1/N-2).

#### Pipeline d'import (workflow détaillé)

```
1. Wizard étape 1 : choix Année + Mois + Société émettrice (FK)
2. Wizard étape 2 : upload fichier .xlsx
3. Côté serveur :
   a. Parser le fichier (entête ligne 10, données à partir ligne 11)
   b. Pour chaque ligne :
      - Lookup Interimaire by Matricule
      - Si Matricule == "PRESTATAIRE" → flag IsPrestataire = true
      - Sinon si non trouvé → CREATE_AUTO (Nom, Prénom, Matricule, Sexe, Fonction)
      - Créer BulletinInterim avec tous les champs (TTC, débours, commission, etc.)
   c. Idempotence : si batch (Année, Mois, Société) existe déjà → demander écraser
   d. Créer 1 ImportBulletinInterimBatch avec stats (NbLignes, TotalTTC, NbCreees)
4. Wizard étape 3 : preview résultat
   - X bulletins OK
   - Y fiches Interimaire créées auto (à compléter par RH)
   - Z lignes Prestataire
   - Total TTC = M FCFA
5. Wizard étape 4 : confirmation utilisateur → commit
6. Notification RH automatique pour les Y fiches créées (workflow d'alerte
   à brancher sur AlerteInterimaireService existant)
```

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

## ✅ V1.3.2 — REFONTE DASHBOARD N°5 SUIVI ABSENCES (2026-05-05)

> **Décision** : aligner le Dashboard N°5 sur le niveau de l'Excel
> "SUIVI ABSENCES 2026" ELTON (référence métier ~50% supérieur à l'existant).
> Build à valider par le user.

### Changements appliqués

**Nouveau enum `MotifAbsence`** (10 valeurs) :
- Absentéisme : AUT, NAUT, EVENT, MAL, AT
- Programmées : CPAYE, FORM, MAT, PAT
- Autre

**DTO refondu** (`SuiviAbsencesDto`) :
- 6 KPIs Excel-style : `EffectifMoyen`, `NbSalariesAbsents`, `TotalJours`,
  `TauxAbsenteismePct`, `JOPerdus`, `RespTempsTravailPct`
- Nouvelles collections : `ParMotif` (10 lignes), `ParAnciennete`,
  `ParCategorie`/`ParDepartement` (DimRowDto), `Employes` (liste détaillée
  avec décomposition par motif)
- Suppression : `ParFamille`, `ParSegment`, `Top10Absents`
- `MoisAbsenceDto` : 2 séries (`JoursAbsenteisme` + `JoursProgrammees`)
  au lieu d'une seule (`NbJours`)

**FilterModel refondu** (`SuiviAbsencesFilterModel`) — multi-sélection :
- `List<int> Annees`
- `List<Guid> SiteOids`
- `List<int> Mois`
- `List<Sexe> GenreSet`
- `List<string> DepartementsNoms`
- `List<Guid> CategorieOids`
- `List<MotifAbsence> MotifsActifs`

**Service refondu** (`SuiviAbsencesDashboardService`) :
- Mapping CongeType → MotifAbsence via Code (case-insensitive) + fallback
  Famille (ex: Maternite → MAT/PAT selon Sexe)
- Calcul Effectif Moyen (count actifs sur la période)
- Calcul JOPerdus (= jours d'absentéisme uniquement, hors programmées)
- Calcul Resp Temps Travail = 100 - TauxAbsenteismePct
- Filtres multi-dimensionnels appliqués correctement
- Décomposition par motif sur 9 colonnes pour la liste employés

**Page Razor refondue** (`SuiviAbsencesDashboard.razor`) :
- 6 KPIs en cards (avec drapeau couleur sur Resp Tmp Travail)
- Toolbar filtres collapsible avec **chips multi-sélection**
- Évolution mensuelle 2 séries (barres rouge + verte)
- 4 tableaux côte à côte (Motif/Catégorie/Département/Ancienneté)
- Liste détaillée employés avec **18 colonnes** (Site, Dept, Cat, Anc,
  Abs/Prog/Total, Resp Tmp, AUT, NAUT, EVENT, MAL, AT, CPAYE, FORM, MAT, PAT)
- Sticky table scrollable (max-height 500px)

**Exports adaptés** :
- `DashboardExcelExportService.ExportSuiviAbsences` : 7 onglets
  (Synthèse, KPI, Par motif, Par catégorie, Par département, Par ancienneté,
  Évolution mensuelle, Détail employés)
- `DashboardPdfExportService.ExportSuiviAbsences` : 6 KPIs en 2 rangées,
  table Par Motif, Top 10 absents

### Limites V1.3.2

- Mapping motifs basé sur le `Code` du `CongeType` ; nécessite que les types
  de congé seedés aient des codes type "MAL", "AT", "EVENT" etc. À défaut,
  fallback sur `FamilleConge` (peut donner "Autre" pour des cas exotiques).
- `SafeDepartement` et `SafeFonction` utilisent réflection pour rester
  compatibles si les nav properties Salarie.Departement / Salarie.Fonction
  ne s'appellent pas exactement comme attendu.

---

## ✅ V1.3 SPRINT 1 — COÛT RÉEL INTÉRIMAIRES (2026-05-05) — BUILD OK + IMPORT TESTÉ

> **Status final** : module **complet et fonctionnel**. Build OK (toutes erreurs
> CS résolues). Import du fichier de test ELTON validé : 31 bulletins importés
> (30 fiches Intérimaire créées auto + 1 prestataire), Total TTC =
> 7 784 839 FCFA, multiplicateur ×1.91 — cohérent avec les ratios réels.

### Fixes appliqués pendant les tests d'intégration

| Bug | Fix | Détail |
|---|---|---|
| `Sexe` enum / `Interimaire.Sexe` introuvables | Suppression du code | L'enum est dans `BusinessObjects` (pas `DomainEnums`) ; `Interimaire` n'a pas `Sexe` (seul `Salarie` l'a). Fiches créées auto sans Sexe. |
| Filtre Site sur Interimaire (`b.Interimaire.Site` introuvable) | Filtrage par `SiteAffectation` (string snapshot) | Site est sur ContratInterim, pas sur Interimaire. On compare au Nom du site sélectionné, normalisé `.Trim().ToUpperInvariant()`. |
| Razor RZ1010 (`@{` dans bloc `else { }`) | Suppression `@{}` | Dans bloc Razor déjà en code (else, foreach, etc.), pas besoin de `@{}` pour passer en C#. |
| Recherche fuzzy ne matche pas les accents | Normalisation Unicode FormD + filtre NonSpacingMark | "Prénoms" → "prenoms", "débours" → "debours". `RemoveAccents()` appliquée des 2 côtés (titre + keyword). |
| **Valeurs gonflées ×91-100** au commit (XLCellValue.ToString → virgule = milliers) | Refactor `SafeDecimal/SafeStr` pour prendre `IXLCell` | Utilise directement `cell.Value.GetNumber()` (double natif), évite `ToString()` parasité par culture. |
| `int? → int` (BuildHeaderMap) | Ajout `.Value` | Rejet collatéral du `replace_all` `.Value)` → `)`. |
| `IObjectSpace.Session` introuvable | Cast vers `XPObjectSpace` | `Session` est exposée par `DevExpress.ExpressApp.Xpo.XPObjectSpace`, pas par l'interface générique. |
| **Conflit contrainte unique** au ré-import (écrasement) | Commit en 2 phases (DELETE puis INSERT) + double-check | XPO accumulait DELETE + INSERT dans la même unité de travail → violation `UX_Batch_Annee_Mois_Societe`. Fix : `CommitChanges()` après les deletes + `PurgeDeletedObjects()` + vérification post-delete. |
| Cellules avec formules / format décimal masqué | `cell.HasFormula ? cell.CachedValue : cell.Value` | Lit la valeur cachée Excel pour les formules ; le format d'affichage n'a aucune influence sur la valeur lue brute. |

### Résultats réels du test d'intégration (fichier `LIVRE_DE_PAIE_TEST_MARS2026.xlsx`)

```
Lot d'import (Batch) : a0921cc5-9d5d-4b8c-baa1-ffb4ca435388
Bulletins créés      : 31
Fiches Intérimaire créées auto : 30
Prestataires         : 1
Total TTC            : 7 784 839 FCFA
Multiplicateur Brut→TTC : ×1.91
```

### Workflow validé end-to-end

```
[Excel facture mensuelle] → [Wizard 4 étapes] → [Service parsing fuzzy/accents]
  → [Création auto fiches Interimaire] → [Commit transactionnel avec écrasement]
  → [Dashboard N°11 KPIs Cout Reel]
```

### Bonus : richesse du hub help

- Section "Modèles à télécharger" sur `/help/index.html` avec liens directs
  (download attribute) vers `Modele_Import_Salaries.xlsx` et `Modele_LivrePaieInterim.xlsx`
- Section "Tableaux de bord (Pilotage)" avec raccourcis vers les 5 dashboards
  V1.2/V1.3 + lien vers le hub
- Nav harmonisée "SunuPaie — Guide utilisateur" comme libellé du lien d'accueil

### Améliorations finales (2026-05-05 — V1.3 Sprint 1 PRODUCTION-READY)

**Suppression d'ancien batch via SQL natif** (la solution qui a marché) :
- Abandonné le LINQ `os.GetObjectsQuery<>().ToList().Where()` qui ratait à cause
  du cache d'identité XPO et du lazy loading sur `Societe`
- Passage à `xpoOs.Session.ExecuteNonQuery(sql)` avec **inlining sécurisé**
  (échappement apostrophes via `Replace("'", "''")` + Guid stringifié strict)
- DELETE des bulletins puis DELETE des batches matching (Année + Mois +
  Société par Oid OU par RaisonSociale pour couvrir le cas société recréée)
- Appel `session.DropIdentityMap()` pour vider le cache XPO avant l'INSERT
  du nouveau batch
- Ce pattern reproduit ce que fait déjà `Updater.cs` du projet
  (`session.ExecuteNonQuery(sql)` pour les opérations de schéma)

**Checkbox "Écraser" toujours visible à l'étape 3** :
- Avant : conditionnée à `_preview.BatchDejaExistant == true` (qui pouvait
  retourner `false` à cause du cache XPO)
- Après : toujours visible, avec un libellé adaptatif
  (« un lot existe déjà, il sera remplacé » VS « à cocher si l'import
  suivant échoue pour conflit de doublon »)

**Rapport étape 4 enrichi** :
- 3 nouveaux champs DTO : `EcrasementDemande`, `NbAnciensBatchesSupprimes`,
  `NbBulletinsAnciensSupprimes`
- Bandeau orange si écrasement réel (avec compteurs)
- Bandeau bleu si "écrasement activé par sécurité mais rien à écraser"
- 2 lignes supplémentaires dans le tableau de stats

### Test d'acceptance final passé

```
Action            : ré-import avec écrasement coché
Avant fix         : ConstraintViolationException UX_Batch_Annee_Mois_Societe
Après fix         : ✅ Bulletins créés 31 / Total TTC 7 784 839 FCFA / Écrasement effectué
                    1 ancien lot supprimé, 31 anciens bulletins remplacés
```

### Améliorations debug (2026-05-05 fin de session)

- **`ExtractSqlConstraintInfo()`** : helper qui extrait le nom d'index/table SQL
  impliqué dans une `ConstraintViolationException` (regex sur "index 'XXX'",
  "object 'YYY'", "contrainte 'ZZZ'"). Le message d'erreur affiché à
  l'utilisateur inclut désormais `[Détail technique : XXX]` pour faciliter
  le diagnostic en cas de violation de contrainte non gérée.
- **`PurgeDeletedObjects()`** appelée après chaque commit intermédiaire pour
  forcer XPO à nettoyer son cache d'objets supprimés (cast `os as XPObjectSpace`
  car `IObjectSpace` ne l'expose pas).
- **Détection élargie** : la branche "Matricule" du catch couvre désormais
  aussi le mot "Interimaire" pour capturer les violations d'index unique
  XPO auto-créés.

### Limites connues V1.3.1 (futur)

- Export Excel/PDF du dashboard N°11
- Seeder démo `BulletinInterim` (pour démo CODIR sans manipulation manuelle)
- Pro-rata mi-temps dans la comparaison contrat
- Notification automatique RH listant les fiches créées auto à compléter
- Filtre Site dans le dashboard N°11 actuellement basé sur SiteAffectation (string snapshot) — passage à un lookup ContratInterim.Site quand le module sera mature

---

## ✅ V1.3 SPRINT 1 — COÛT RÉEL INTÉRIMAIRES (2026-05-05) — code initial (déprécié, voir section ci-dessus)

> Status : code complet pour les 5 étapes (entités, service import, wizard,
> dashboard, help). Build à valider par le user.

### Fichiers créés / modifiés

| Étape | Fichier | Rôle |
|---|---|---|
| 1 | `BusinessObjects/Interim/BulletinInterim.cs` | Entité 30+ champs avec bloc facturation Débours/Commission/HT/TVA/TTC, statut import |
| 1 | `BusinessObjects/Interim/ImportBulletinInterimBatch.cs` | Audit batch 1 par triplet (Année, Mois, Société) |
| 2 | `Models/Interim/ImportBulletinInterimDtos.cs` | DTOs preview/result du wizard |
| 2 | `Services/Interim/IBulletinInterimImportService.cs` + impl | Parser ClosedXML fuzzy, lookup matricule, création auto, idempotence |
| 3 | `Pages/Interim/BulletinInterimImport.razor` | Wizard 4 étapes (route `/interim/import-livre-paie`) |
| 3 | `Controllers/BulletinInterimImportController.cs` | Bouton "Charger livre de paie" sur ListView batch |
| 4 | `Models/Dashboards/CoutReelInterimDto.cs` | DTO dashboard + sub-DTOs (KPI, Top10, Évolution, Écart, Société) |
| 4 | `Services/Dashboards/ICoutReelInterimDashboardService.cs` + impl | Calcul KPIs annuels/mensuels + comparaison contrat vs réel |
| 4 | `Pages/Dashboards/CoutReelInterim/CoutReelInterimDashboard.razor` | Dashboard route `/dashboards/cout-reel-interim` |
| 5 | `wwwroot/help/Modele_LivrePaieInterim.xlsx` | Template Excel 53 colonnes (généré via openpyxl) |
| 5 | `wwwroot/help/import-livre-paie-interim.html` | Help d'import (workflow 5 étapes + lien template) |
| 5 | `wwwroot/help/dashboards/cout-reel-interim.html` | Help dashboard (KPIs, formules, FAQ) |
| 5 | `wwwroot/help/dashboards/index.html` | Card N°11 ajoutée + nav harmonisée |
| 5 | `Pages/Dashboards/DashboardHome.razor` | Card "Coût Réel Intérimaires" ajoutée |
| 5 | `Startup.cs` | DI : `IBulletinInterimImportService` + `ICoutReelInterimDashboardService` |

### Workflow utilisateur final

```
Société intérim envoie facture .xlsx
    ↓
Intérimaires → Lots d'import → "Charger livre de paie"
    ↓
Wizard 4 étapes (Période/Société → Upload → Preview → Confirm)
    ↓
N BulletinInterim créés + fiches Interimaire manquantes auto
    ↓
Dashboard /dashboards/cout-reel-interim affiche KPIs + écart contrat/réel
```

### Limites V1.3 (à compléter en V1.3.1)

- Pas d'export Excel/PDF du dashboard N°11
- Comparaison contrat suppose mois standard 22 jours (pas de pro-rata mi-temps)
- Prestataires exclus de la comparaison contrat (pas de FK vers ContratInterim)
- Pas de seeder démo BulletinInterim (test uniquement avec fichier réel)
- Tableau Comparaison limité à 50 premiers écarts (par valeur absolue)

---

## ✅ V1.2.1 — REFONTE BUDGET ANNUEL (2026-05-05) — BUILD OK

> **Décision DAF** : passage à un modèle simplifié sur demande métier.
> AVANT : saisie mensuelle × 9 rubriques × site (~180 lignes/an)
> APRÈS : saisie ANNUELLE sur le BRUT × site (1-N lignes/an)
>
> Le DAF saisit une enveloppe brute annuelle validée en CODIR. La comparaison
> avec le réalisé se fait au global (annuel), pas mensuellement.

### Refonte appliquée

| Composant | Changement |
|---|---|
| `BudgetMasseSalariale` (entité) | Suppression de `Mois`, `Rubrique`. Renommage `Montant` → `MontantBrutAnnuel`. Ajout index unique `(Annee, Site)`. `BudgetSource.AutoMensualise` remplacé par `Demo`. |
| `BudgetVsRealiseDto` | Suppression `Mensuel`, `CumulYtd`, `ParRubrique`. Conservation `Kpis`, `ParSite`. Ajout `Evolution` (5 ans glissants). |
| `BudgetVsRealiseFilterModel` | Suppression `SeuilAlertePct`. Conservation `Annee`, `SiteOid`. |
| `BudgetVsRealiseDashboardService` | Refonte calcul : 4 KPIs annuels + 5 années glissants + ventilation site. Plus de mensualisation. |
| `BudgetVsRealiseDashboard.razor` | Plus de tableau mensuel ni cumul YTD. Focus sur tableau "Évolution 5 ans" + tableau "Ventilation par site". |
| `DemoDataSeeder` | 12 lignes (3 années × 4 lignes : 1 global + 3 sites). Plus de méthode `MensualiserBudget`. |
| `budget-vs-realise.html` (help) | Mise à jour complète : saisie annuelle, formules KPI, FAQ. |

### Migration DB

L'entité a été modifiée (suppression de colonnes). Au prochain `UpdateSchema()` :
- Les colonnes `Mois`, `Rubrique`, `Montant` seront supprimées
- La colonne `MontantBrutAnnuel` sera créée
- L'index `IX_BudgetMS_Annee_Mois_Site_Rubrique` sera supprimé
- L'index unique `UX_BudgetMS_Annee_Site` sera créé

⚠️ **Les données seedées V1.2 seront perdues** au passage. Re-lancer "Charger
données démo" après le rebuild pour avoir les 12 nouvelles lignes seedées.

---

## ✅ V1.2 SPRINTS 1-5 — CODE IMPLÉMENTÉ (2026-05-04)

> Status : **code écrit, build à valider** (le user fait le build dans VS le
> 2026-05-05 et renvoie les erreurs éventuelles).

### Fichiers créés

| Sprint | Fichier | Rôle |
|---|---|---|
| 1 | `BusinessObjects/Budget/BudgetMasseSalariale.cs` | Entité XPO + enum BudgetRubrique + enum BudgetSource |
| 2 | `Models/Dashboards/BudgetVsRealiseDto.cs` | DTO + sub-DTOs (Mensuel, CumulYtd, Rubrique, Site) |
| 2 | `Models/Dashboards/BudgetVsRealiseFilterModel.cs` | Filtre (Année, Site, SeuilAlerte) |
| 2 | `Services/Dashboards/IBudgetVsRealiseDashboardService.cs` | Interface |
| 2 | `Services/Dashboards/BudgetVsRealiseDashboardService.cs` | Service avec cache 5 min, calculs mensuel/YTD/projection |
| 2 | `Pages/Dashboards/Budget/BudgetVsRealiseDashboard.razor` | Page Razor (route `/dashboards/budget-vs-realise`) |
| 3 | `Models/Dashboards/ProvisionsSocialesDto.cs` | DTO IDR + CP |
| 3 | `Services/Dashboards/IProvisionsSocialesDashboardService.cs` + impl | Calcul barème CCI Sénégal (25/30/40%) |
| 3 | `Pages/Dashboards/Provisions/ProvisionsSocialesDashboard.razor` | Page (route `/dashboards/provisions-sociales`) |
| 4 | `Models/Dashboards/CoutCompletDto.cs` | DTO Fully Loaded Cost |
| 4 | `Services/Dashboards/ICoutCompletDashboardService.cs` + impl | Net + Cotis + Charges + Avantages + Formation |
| 4 | `Pages/Dashboards/Cout/CoutCompletDashboard.razor` | Page (route `/dashboards/cout-complet`) |
| 5 | `Models/Dashboards/ConformiteSenegalDto.cs` | DTO + enum StatutConformite |
| 5 | `Services/Dashboards/IConformiteSenegalDashboardService.cs` + impl | 9 indicateurs réglementaires |
| 5 | `Pages/Dashboards/Conformite/ConformiteSenegalDashboard.razor` | Page (route `/dashboards/conformite-senegal`) |

### Fichiers modifiés

- `Blazor.Server/Startup.cs` — DI : 4 nouveaux `AddScoped<I*, *>()` après le bloc V1.0
- `Blazor.Server/Pages/Dashboards/DashboardHome.razor` — 4 nouvelles `CardInfo` ajoutées dans `_cards`

### Limites connues V1.2 (à compléter en V1.2.1)

- **Provisions sociales** : congés payés calculés sur estimation théorique
  faute d'entité `SoldeConge` mappée. À brancher quand le module congés sera
  intégré.
- **Coût complet** : `Formation` à 0 (entité Formation pas encore mappée
  côté Bulletin). Avantages nature détectés via `RubriqueTypeRef.Code` AV_NATURE_*
  — fallback à 0 si non trouvé.
- **Conformité SN** : 5 indicateurs sur 9 sont actifs (SMIG, CDD, Stages,
  Congés, Contrats scannés en NonEvalue). Les 3 déclarations sociales
  (IPRES/CSS/IPM) et HSup détaillé sont en NonEvalue (entités à créer).
- **Budget vs Réalisé** : ventilation par rubrique du réalisé est faite au
  prorata du budget (heuristique). En V1.3, mapping `BudgetRubrique ↔
  RubriqueTypeRef` à formaliser pour ventilation exacte.

### Build attendu — points de vigilance

- Vérifier que `Salarie.Categories?.Intitule` compile (utilisé dans
  `CoutCompletDashboardService`). Si la nav property s'appelle autrement,
  adapter.
- `Salarie.FullName` : provient de l'héritage `Person` (DevExpress base impl).
- Le service `ConformiteSenegalDashboardService` lit `TypeContrat` via
  réflexion (au cas où la propriété ne serait pas exactement nommée ainsi).
- Module XAF : la nouvelle entité `BudgetMasseSalariale` doit faire l'objet
  d'une `dotnet ef migrations add V12_BudgetRH` (si EF) OU d'un
  `os.UpdateSchema()` (XPO standard) au prochain démarrage.

### Données de seed (à ajouter en V1.2.1)

`DemoDataSeeder.EnsureAll()` n'a PAS été modifié. Pour tester les dashboards
V1.2 avec des valeurs réalistes, il faudra ajouter :
- `EnsureBudgetMasseSalariale()` : 1 année × 12 mois × 9 rubriques × 1-3 sites
  (≈ 200 lignes de budget pour 2026)

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

---

## ✅ V1.4.3 — WORKFLOW PUBLICATION BULLETIN + ESPACE SALARIÉ (2026-05-07)

> **Décision CODIR mai 2026** : plus d'envoi PDF par email. Le salarié se
> connecte à l'Espace Salarié authentifié pour télécharger son bulletin.
> L'email devient une simple notification d'information.

### Workflow

```
Brouillon ─[Valider]→ Validé ─[Publier]→ Envoyé ─[Clôturer]→ Cloturé
                                ├─[Dépublier]→ Validé (correction)
                                └─[Notifier]→ ré-envoi email
```

### Décisions clés

- Réutilisation du statut `Envoye` (sémantique « Publié ») → zéro migration
- `DatePublication != NULL` = critère de visibilité Espace Salarié
- Plus de clé PDF — auth Espace Salarié remplace la clé
- PDF archivé dans `Bulletin.PdfArchive` (FileData) au Publier

### Fichiers nouveaux

- `Services/BulletinPublicationService.cs` — Publier / Depublier / Notifier (async)
- `Controllers/BulletinPublishController.cs` — actions RH multi-sélection
- `Controllers/BulletinEspaceSalarieNoDetailController.cs` — bloque drill-down
- `Reports/BulletinPaie.repx` — design custom embedded (87 580 octets)

### Fichiers modifiés majeurs

- `BusinessObjects/Bulletin.cs` — DatePublication, PublieParUser, EstPublie
- `Controllers/BulletinSalarieFilterController.cs` — filtre `DatePublication != null`
- `Controllers/BulletinValiderEnvoyerController.cs` — actions obsolètes désactivées
- `Controllers/BulletinTelechargerController.cs` — scoped à vue Espace Salarié
- `Services/BulletinPdfService.cs` — auto-bootstrap rapport (REPX embedded + fallback)
- `DatabaseUpdate/Updater.cs` — `SeedBulletinReportIfMissing` au démarrage
- `Module.csproj` — EmbeddedResource BulletinPaie.repx
- `Model.DesignedDiffs.xafml` — vue Bulletin_EspaceSalarie_ListView, menu, captions

### Fix culture FCFA

- `Blazor.Server/Startup.cs` — clone fr-FR, override `CurrencySymbol = "FCFA"`,
  `CurrencyDecimalDigits = 0`, format `1 234 FCFA`. Plus de `€` partout.

### Fix UI Blazor

- Tous les handlers SMTP passent par `SendAsync` (extension threadpool)
  pour ne pas bloquer le thread SignalR. Plus de "Loading..." infini sur Notifier.

### Procédure d'export REPX (à utiliser quand le rapport est modifié dans designer)

```powershell
# Adapter Server selon environnement
$conn = New-Object System.Data.SqlClient.SqlConnection "Server=CPC-adien-ZA8ZN\SQLEXPRESS;Database=SunuPaie_Recette;Integrated Security=true;TrustServerCertificate=true"
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Content FROM ReportDataV2 WHERE Name='BulletinPaie' AND GCRecord IS NULL"
$bytes = $cmd.ExecuteScalar()
[System.IO.File]::WriteAllBytes("C:\Dev\AdiPAIE_V02\AdiPAIE_V02\AdiPAIE_V02.Module\Reports\BulletinPaie.repx", $bytes)
$conn.Close()
```

Puis `git add Reports/BulletinPaie.repx && git commit`.

### Bulletins legacy (pré-V1.4.3)

- Critère `DatePublication IS NOT NULL` les exclut de la vue salarié
- RH publie rétroactivement via Bulletin_ListView → multi-sélection → Publier
- Pas de migration automatique au startup (volontaire — évite envois massifs)

### Email notification

- Sujet : `[SunuPaie] Votre bulletin de paie [Mois] [Année] est disponible`
- Corps HTML simple, **0 PJ, 0 clé**, instructions step-by-step Espace Salarié

### Vue Espace Salarié

- `Bulletin_EspaceSalarie_ListView` (nouvelle, AllowEdit/New/Delete=False)
- Colonnes : Année, Mois, Période, NetAPayer, DatePublication
- Action unique : **Télécharger** (stream PdfArchive sans clé)
- Drill-down vers DetailView bloqué
- Filtre forcé : `Salarie.Oid = currentUser AND DatePublication IS NOT NULL`

### Toolbar RH après V1.4.3

```
[ Valider ]  [ Publier ]  [ Dépublier ]  [ Notifier ]  [ Clôturer ]  [ Imprimer ]  [ Recalc ]
```

---

## ✅ V1.5 / V1.5.1 / V1.5.2 — STABILISATION (2026-05-08 / 09)

### V1.5 — Module Mouvements Intérim
- Workflow : AC → AssistantRH → RH → DAF → Apply
- 8 transitions audit-trailées dans `DemandeMouvementService`
- Email notification AC fire-and-forget
- StationOrigine/StationDestination capturées sur MouvementInterimaire (historique)

### V1.5.1 — Dashboard Rémunération filtre Échelon
- Filtre Échelon ajouté sur RemunerationFilterModel
- Table ParEchelon (INTERNE only) dans le dashboard
- ViewModel `EchelonOption(Guid, string Label)` pour bypass PersistentAlias

### V1.5.2 — Quick Wins QW1-QW6 + xUnit foundation + Warnings cleanup
- QW1 : `RolesGRHInitializer` static refactor (suppression doublons)
- QW2 : Cleanup `[Browsable(false)]` sur propriétés calculées
- QW3 : DPAPI LocalMachine pour password encryption
- QW4 : `EmailSender` retry exponentiel + filter transient errors (SmtpException 4xx/5xx)
- QW5 : `AlerteFinMissionInterimHostedService` cron 06:00 quotidien (alerte si DateFin ≤ 30j)
- QW6 : 5 SQL indexes performance (Bulletin, Salarie, Pret, Conge, Mouvement)
- xUnit 2.9 + FluentAssertions + Moq + DevExpress.Xpo (15 tests stabilité enums)
- **Warnings cleanup Lot 1 (csproj)** : Nullable=annotations + NoWarn XAF0020;CA1416;XAF0022 → ~150 warnings éliminés
- **Warnings cleanup Lot 2 (code)** : 26 corrections (CS0105 doublons using, CS0162 dead code, CS0169/CS0414 champs morts, CS0184 type checks impossibles, CS0618 obsolète, XAF0018 UnitOfWork, XAF0025 attrs XPO sur non-XPO)
- **Validation finale : 0 warning, 0 erreur sur 4 projets** (Module + Tests + Win + Blazor.Server)
- HEAD `dev` après V1.5.2 = `6c486cb`

---

## ✅ V1.6 — FICHE SALARIÉ ENRICHIE (2026-05-09)

### Champs ajoutés
- **Salarie.Telephone** (NVARCHAR(20)) — téléphone perso (différent de ContactUrgenceTel)
- **Conjoint.DateNaissance** + Age calculé (oubli initial corrigé)

### Nouvelle entité `Enfant`
- Collection agrégée `Salarie.Enfants` (calque Conjoints)
- Champs : NomComplet, DateNaissance, Sexe, Situation, ACharge
- Age calculé à partir de DateNaissance
- Enum `SituationEnfant` : NonScolarise / Eleve / Etudiant / Apprenti / Travailleur / SansActivite / Autre
- Anticipe la logique TRIMF (parts fiscales : limite âge sauf étudiant/apprenti)
- ImageName BO_Salutation, NavigationItem masqué (accès via Salarie)

### Layout Salarie_DetailView
- Onglet Famille : **Conjoints (50%) | Enfants (50%) côte à côte**
- ListView Conjoints : ajout colonne DateNaissance + widths fixes
- ListView Enfants : NomComplet, DateNaissance, Age, Sexe, Situation, ACharge avec widths fixes
- **`CaptionLocation=Top`** sur rangées à 4 champs (Poste occupé, Classification, Salaire, Primes & avantages, Calculs fiscaux, Situation familiale) → labels au-dessus, plus de troncature horizontale
- Telephone intégré dans le groupe Téléphone/Notes onglet Identification

### Règle métier matricule
- `Salarie.Matricule` verrouillé après création via `[Appearance(IsNewObject(this))]`
- Justification : clé de mapping JDE + référencé dans bulletins/contrats/audit
- Reste éditable en saisie initiale puis grisé à vie (préserve traçabilité)

### Wizard création Salarié — TENTATIVE ABANDONNÉE
- Conçu en 4 étapes (Identité / Contrat / Affectation / Récap)
- Lookups ne se chargeaient pas correctement dans `NonPersistentObjectSpace` malgré `AdditionalObjectSpaces.Add(XPObjectSpace)`
- Décision utilisateur : ne pas garder pour éviter régression — la fiche standard suffit

### Migration auto XPO au démarrage
- ALTER TABLE Salarie ADD Telephone NVARCHAR(20) NULL
- ALTER TABLE Conjoint ADD DateNaissance datetime NULL
- CREATE TABLE Enfant (Oid, OptimisticLockField, Salarie, NomComplet, DateNaissance, Sexe, Situation, ACharge, GCRecord)

HEAD `dev` après V1.6 = `e424cbc`

---

## ✅ V1.6.1 — UX QUICK WINS FICHE SALARIÉ (2026-05-09)

### Bandeau KPI en en-tête onglet Identification
5 propriétés calculées NonPersistent sur Salarie :
- `StatutAffichage` (badge coloré)
- `AncienneteAffichage` (ex: "24 ans")
- `SalaireBaseAffichage` (ex: "451 556 FCFA")
- `EchelonAffichage` (code)
- `SiteAffichage` (code)

→ Aperçu instantané sans avoir à parcourir les 7 onglets

### Badges colorés via `[Appearance]` sur Statut
- 🟢 **Vert** (PaleGreen + DarkGreen bold) si IsActif et hors période d'essai
- 🟠 **Orange** (Moccasin + DarkOrange bold) si DateConfirmation > today (période d'essai)
- ⚪ **Gris** (Gainsboro + DimGray bold) si Inactif

### Alertes visuelles sur dates critiques (5 règles)
- 🔴 **Rouge** : CNI expirée (DateExpirationCNI < today)
- 🟠 **Orange** : CNI expire ≤ 60 jours
- 🔴 **Rouge** : Passeport expiré
- 🟠 **Orange** : Passeport expire ≤ 90 jours
- 🟠 **Orange** : Fin période d'essai ≤ 15 jours (DateConfirmation approche)

### Layout en-tête enrichi
- Photo agrandie (50% → 75% de sa colonne, RelativeSize 35 → 40)
- Top row plus haut (30 → 40) — effet "fiche d'identité" marqué

### Emojis sur les 7 onglets Salarie_DetailView
- 👤 Identification / 📋 Contrat & Poste / 💰 Rémunération
- 👨‍👩‍👧 Famille / 📄 Pièces d'identité / 🏦 Comptes bancaires / ℹ️ Informations RH

### Type FontStyle correct pour XAF 25.1
- `DevExpress.Drawing.DXFontStyle.Bold` (pas `System.Drawing.FontStyle`)

HEAD `dev` après V1.6.1 = `8c4002a`

---

## ✅ V1.6.2 — PATTERN BADGES SUR 7 ENTITÉS WORKFLOW (2026-05-09)

Application du pattern V1.6.1 (badges colorés sur Statut + alertes dates) à 7 entités à workflow :

| Entité | Badges Statut | Alertes dates |
|--------|--------------|---------------|
| **Bulletin** | Brouillon (gris) / Validé (bleu) / Envoyé (vert) / Comptabilisé (violet) / Clôturé (gris foncé) | — |
| **PeriodePaie** | Brouillon (gris) / Ouverte (vert) / Clôturée (gris foncé) | — |
| **ContratInterim** | (existant FontColor) | 🔴 Mission dépassée • 🟠 Fin ≤ 7j urgent • 🟡 Fin 8-30j proche |
| **CongeDemande** | Brouillon / En attente / Accordée (vert) / Refusée (rouge) / Annulée (gris foncé) | — |
| **DemandeAttestation** | En attente (orange) / Traitée (vert) / Rejetée (rouge) | — |
| **DemandeMouvementInterim** | Brouillon / En attente AC→AssistantRH→RH→DAF / Appliquée (vert) / Rejetée (rouge) / Annulée (gris foncé) | — |
| **Pret** | Brouillon / En cours (bleu) / Terminé (vert) / Suspendu (orange) | — |
| **EntretienAnnuel** | Brouillon-Planifié / En cours saisie / Soumise RH (vert) / Clôturé (gris foncé) | — |

### Code couleur unifié (référence projet)
- ⚪ **Gainsboro / DimGray** = Brouillon
- 🔵 **LightSkyBlue / DarkBlue** = En cours / Validé
- 🟠 **Moccasin / DarkOrange** = En attente validation
- 🟢 **PaleGreen / DarkGreen** = Finalisé / Accordé / Actif
- 🔴 **LightCoral / DarkRed** = Refusé / Expiré
- ⚫ **DarkGray / White** = Clôturé / Annulé
- 🟣 **Plum / Indigo** = Comptabilisé (paie)
- 🟡 **LightSalmon / DarkRed** = Urgence (≤ 7 jours)

### Règles d'application
- Toujours `TargetItems = "Statut"` (badge sur la cellule, pas la ligne entière)
- Toujours `FontStyle = DevExpress.Drawing.DXFontStyle.Bold` (visibilité)
- Combinaison BackColor + FontColor pour contraste WCAG accessible

### Convention pour futures entités à workflow
1. Identifier l'enum de Statut dans `DomainEnums.cs`
2. Appliquer 3-5 `[Appearance]` au niveau classe avec `TargetItems = "Statut"`
3. Choisir les couleurs dans la palette unifiée ci-dessus
4. Tester la visibilité en mode liste ET en mode détail

### Permissions RH ajoutées (RolesGRHInitializer)
Bug détecté : la "Consultation bulletins" RH n'affichait que Statut + NetAPayer
parce que le rôle RH n'avait aucune permission explicite sur Bulletin/Salarie.
Corrigé en ajoutant :
- `Salarie` : rw (lire + corriger fiches salariés)
- `Bulletin` : rwc (lire + valider + créer)
- `BulletinLigne` : rw (détail des lignes de paie)
- `PeriodePaie` : rw (gérer périodes)
- `Conjoint` + `Enfant` : rwcd (famille / TRIMF)

**Action requise** : après déploiement, relancer l'init des rôles
(Paramètres de Paie → Administration → "Init. rôles GRH") OU laisser
l'Updater le faire au prochain démarrage.

### ✅ ISSUE RÉSOLUE V1.6.2 (confirmé 2026-05-09) — Multi-rôle RH + RH_Manager bloque colonnes Bulletin

**Test sur base fresh** (créée le 2026-05-09 avec V1.6.2 + V1.7) → **toutes les
colonnes Bulletin sont correctement visibles** pour un user RH neuf. Le code
est sain. Le bug observé en base actuelle vient de la persistance d'anciennes
configurations (RH_Manager assigné à ababacar.diallo en plus de RH).

### 🚧 ISSUE D'ORIGINE — Multi-rôle RH + RH_Manager bloque colonnes Bulletin

**Symptôme** : sur Consultation bulletins, les colonnes Matricule, NomComplet,
BrutFiscal, BrutSocial, TRIMF restent invisibles pour un user qui a les 2 rôles
RH + RH_Manager, malgré nos perms RH ajoutées.

**Cause racine identifiée** :
- `RH` dans `RolesGRHInitializer` = **AllowAllByDefault** (ligne Updater.cs:280)
- `RH_Manager` dans `Updater.cs` = **DenyAllByDefault** (ligne 347)
- En XAF multi-rôle : DenyAllByDefault l'emporte sur AllowAllByDefault pour les
  membres non explicitement Allow dans le rôle Deny → toutes les colonnes que
  RH_Manager n'a pas listées explicitement sont cachées.

**Vérifié en DB** :
- Type permissions RH(Bulletin r/w/c) + RH(Salarie r/w) → présentes ✓
- Aucune Member permission Deny → cause = policy DenyAllByDefault de RH_Manager

**Tentatives faites** :
1. Ajout perms Type sur RH (Bulletin/Salarie/etc.) → en DB mais pas suffisant
2. Logout/login + re-Init rôles → ne corrige pas
3. Suppression role RH_Manager du user via SQL → à confirmer / non débuggé à fond

**Solutions possibles à tester quand on y reviendra** :
- A. Retirer définitivement RH_Manager des users qui ont déjà RH (RH a déjà
  les dashboards via `GrantDashboardAccessToExistingRole("RH")` ligne 399)
- B. Changer Updater.cs:347 → `RH_Manager.PermissionPolicy = AllowAllByDefault`
- C. Ajouter dans RH_Manager les Member permissions manquantes (Bulletin.BrutFiscal,
  Salarie.Matricule, etc.) — fastidieux

**Option recommandée** : A (retirer RH_Manager pour les users qui ont déjà RH)
puis investiguer si RH_Manager doit exister isolément pour d'autres users.

HEAD `dev` après V1.6.2 = (à pousser)

---

## 📋 Roadmap post-V1.6.2

### Court terme (V1.7)
- CI/CD GitHub Actions (build + tests à chaque PR)
- Tests d'intégration XPO sur `BulletinPublicationService` et `DemandeMouvementService`
- Migration `Validator.RuleSet` → `IValidator` service (warning CS0618 actuellement supprimé via #pragma)
- Migration `ReportDataProvider.ReportsStorage` → `IReportStorage` injecté

### Moyen terme (V2.0)
- Refacto wizards multi-popups vers `e.ShowViewParameters` (suppression XAF0022)
- Évaluer un wizard de création Salarié full-Blazor (custom Razor component) plutôt que XAF NonPersistent

### Tag de release
- `v1.6.2` à poser sur `dev` après merge sur `main`

---

## ✅ V1.7 — ANNUAIRE FAMILLE + PROVISION CONGÉS LÉGALE (2026-05-09)

### 1. Annuaire famille hiérarchique (menu Gestion du personnel)
- Entité non-persistante `FamilleAnnuaire` avec 1 ligne par membre
  (Salarié / Conjoint / Enfant) groupée par MatriculeSalarie
- Service `FamilleAnnuaireService.LoaderAnnuaire(persistentOs, nonPersistentOs)`
- Contrôleur avec hook `ObjectsGetting` (pattern XAF canonique non-persistant)
- Badges colorés palette projet :
  - 🔵 Salarié (LightSkyBlue)
  - 🟣 Conjoint (Plum)
  - 🟠 Enfant (Moccasin)
  - 🟢 ACharge=true (PaleGreen)
- Permissions RH : `AddType<FamilleAnnuaire>(rh, "r")`
- Visible : Matricule, Nom complet, Date naissance, Âge, Sexe, Statut, À charge

### 2. Architecture des congés — VUE D'ENSEMBLE COMPLÈTE

Le module Congés repose sur **2 vues complémentaires** qui partagent les mêmes
données opérationnelles mais répondent à 2 besoins métiers distincts.

#### A. `SoldeConge` — Vue OPÉRATIONNELLE RH (existant V1.4)

**Localisation** : Menu **Congés et absences → Soldes de congés (opérationnel RH)**

**Modèle de données** : Persistant, granularité (Salarié × Année × CongeType).
```
SoldeConge
├── Salarie (FK)
├── Annee (int)
├── Type (FK CongeType)
├── JoursAcquis     ← alimenté MOIS PAR MOIS par le cron
├── JoursReportes   ← report N-1 lors de la clôture d'exercice
├── JoursPris       ← décrémenté quand CongeDemande=Accordée et DateReprise passée
├── JoursEnAttente  ← réservé sur soumission, libéré sur refus/annulation
├── SoldeDisponible (computed) = Acquis + Reportés - Pris
└── SoldeReel (computed)       = Acquis + Reportés - Pris - EnAttente
```

**Service** : `SoldeCongeCalculService` avec 8 méthodes :
- `AcquerirMensuel(os, salarie, type, annee, mois)` — crédit idempotent
- `AcquerirTousSalaries(os, annee, mois)` — batch mensuel (cron à brancher)
- `Reporter(os, annee)` — clôture N → N+1
- `VerifierSolde`, `ReserverJours`, `DebiterJours`, `AnnulerDebite`
- `OuvrirNouvelExercice(os, annee)` — initialise pour une nouvelle année

**Historique** : Chaque mouvement tracé dans `MouvementSolde` (table fille)
avec `MouvementSoldeType` ∈ { AcquisitionMensuelle, PriseCongé, Report,
AjustementManuel, AnnulationCongé, **Initialisation** }.

#### B. `ProvisionConges` — Vue COMPTABLE DAF (V1.7 nouveau)

**Localisation** : Menu **Congés et absences → Provision annuelle (DAF)**

**Modèle de données** : Non-persistante, recalculée à la volée pour les 3
dernières années (N-2, N-1, N), granularité (Salarié × Année).

**Formule légale conforme Code du Travail Sénégal Loi 97-17 Art. L.149**
(source : https://africapaierh.com/juridique/les-conges-payes-au-senegal/) :

```
NbreJourTotal = (2 × NbreMois)        ← Base CCT : 2 j ouvrables / mois travaillé
              + BonusAnciennete       ← Palier ancienneté
              + BonusEnfants          ← Mère de famille (3 règles cumulables)

ProvisionFCFA = NbreJourTotal × (BrutMensuelMoyen / 22 jours ouvrés)
              où BrutMensuelMoyen = Σ Gain rubriques BrutFiscal=true / 12
```

**Palier ancienneté** (corrigé conformément CCT, l'ancienne requête SQL
ELTON avait une erreur sur > 25 ans) :
- ≤ 10 ans : 0 jour
- 11-15 ans : +1 jour
- 16-20 ans : +2 jours
- 21-25 ans : +3 jours
- > 25 ans : **+7 jours** (PAS +6)

**Bonus mère de famille** (3 règles CUMULABLES, femmes uniquement) :
- **Règle A** : +1 j / enfant < 14 ans à l'état-civil (toutes mères)
- **Règle B** : +2 j / enfant à charge si mère < 21 ans au 31/12
- **Règle C** : +2 j / enfant mineur à partir du 4ème si mère > 21 ans

**Réconciliation théorique vs réel** (colonnes V1.7) :
- `SoldeReelAcquis` = Σ SoldeConge.JoursAcquis du salarié pour l'année
- `Ecart` = NbreJourTotal (théorique) − SoldeReelAcquis
- 🟢 vert si |Ecart| ≤ 1 jour (tolérance arrondi)
- 🟥 rouge si |Ecart| > 1 jour (anomalie : cron en retard, ou bonus
  ancienneté/enfants pas appliqué côté Solde, ou ajustement manuel non documenté)

#### C. Quand utiliser laquelle ?

| Use case | Vue à consulter |
|----------|-----------------|
| RH valide une demande (le salarié a-t-il assez de jours ?) | **Solde des congés** (SoldeReel) |
| RH cherche le solde d'un salarié | **Solde des congés** |
| DAF clôture l'exercice et passe la provision comptable | **Provision annuelle** (ProvisionFCFA) |
| Audit conformité Code du Travail | **Provision annuelle** (formule explicite + sources) |
| Réconciliation cohérence opérationnel ↔ comptable | **Provision annuelle** colonne Écart |
| Calcul indemnité de départ congés non pris | Cumul **Solde des congés** + valorisation via Provision |

### 3. ⚠️ MIGRATION PRODUCTION (V1.7.1 à venir)

**Contexte** : La mise en production se fera sans récupération automatique de
l'historique de congés depuis l'ancien système (JDE / paie historique).
Il y aura donc un **ajustement manuel initial** par salarié pour caler
les soldes au moment du go-live.

**Architecture déjà prête** :
- `MouvementSoldeType.Initialisation` (enum existant V1.4) prévu pour
  tracer ces mouvements de cadrage initial
- Champ `SoldeConge.JoursReportes` peut accueillir le solde initial
  (équivalent d'un report N-1 fictif)

**Processus de migration au go-live** (V1.7.1 — à coder) :
1. RH récupère depuis l'ancien système la liste : Matricule + Solde acquis +
   Solde reporté (au 31/12 de l'année précédente)
2. Service `InitialiserSoldesProdService.ImporterDepuisExcel(file)` :
   - Pour chaque salarié, créer `SoldeConge` (Année=N, Type=Annuel)
   - Renseigner `JoursReportes` avec le solde historique
   - Créer `MouvementSolde` avec `TypeMouvement=Initialisation`,
     Commentaire = "Migration go-live AdiPAIE depuis [système précédent]
     le [date]"
3. La cron `AcquerirTousSalaries` continue à alimenter `JoursAcquis`
   mensuellement à partir du mois courant
4. Le tableau **Provision annuelle** affichera l'écart entre la formule
   théorique pleine année et le réel (qui n'aura que les mois écoulés
   depuis go-live) — **comportement attendu** pour l'année de transition

**À NE PAS faire** :
- ❌ Faire du backfill de `MouvementSolde` mois par mois pour tout
  l'historique (perte de temps, données pas fiables côté ancien système)
- ❌ Stocker le solde historique directement dans `JoursAcquis` (ça
  fausserait les statistiques d'acquisition mensuelle)

**Fichier import attendu** (CSV / Excel) :
```
Matricule;TypeCongeCode;JoursReportes;DateInitialisation;Commentaire
99001;ANNUEL;18.5;2026-06-01;Migration JDE
99002;ANNUEL;7.0;2026-06-01;Migration JDE
...
```

### 4. Hypothèses ouvertes à valider avec RH ELTON

- **Âge limite enfant à charge** : 14 ans (article CCT) — confirmer cap
- **Père de famille** : a-t-il droit à un bonus ? (CCT muet, à valider)
- **Période d'absence maladie ≤ 6 mois assimilée** : comment le cron
  `AcquerirMensuel` gère ces mois (génération bulletin malgré absence) ?
- **Plafond annuel** : 30 jours max ? Ou pas de cap (24 base + bonus
  cumulés peuvent dépasser 30) ?

HEAD `dev` après V1.7 = (à pousser)

---

## ✅ V1.7.0a — Hotfix critique déploiement prod (2026-05-10)

### Bug découvert au 1er déploiement Windows Server 2022

Sur la base fraîche créée par l'`Updater` XAF en prod (Release build),
**aucun utilisateur Admin ni rôle Administrators n'était créé** → la page
de login retournait `Login failed for 'Admin'. User name or password is
incorrect.` quel que soit le mot de passe testé.

### Cause racine

Dans `AdiPAIE_V02.Module/DatabaseUpdate/Updater.cs`, le bloc qui crée le
rôle `Administrators` + l'utilisateur `Admin` (mot de passe vide) était
entouré d'une directive `#if !RELEASE / #endif`. Conséquence : ce code
était **compilé en DEBUG mais EXCLU en RELEASE** (= production).

```csharp
#if !RELEASE                    // ⛔ exclu en prod !
    var adminRole = CreateAdminRole();
    var userManager = ObjectSpace.ServiceProvider.GetRequiredService<UserManager>();
    if (userManager.FindUserByName<ApplicationUser>(ObjectSpace, "Admin") == null)
        userManager.CreateUser<ApplicationUser>(ObjectSpace, "Admin", "", u => u.Roles.Add(adminRole));
    ObjectSpace.CommitChanges();
#endif
```

### Fix appliqué

Suppression des directives `#if !RELEASE / #endif`. Le bloc est :
- **Idempotent** : `FindUserByName` puis `FirstOrDefault` sur le rôle
  garantissent qu'on ne recrée jamais ce qui existe déjà.
- **Safe en prod** : login `Admin` créé **avec mot de passe vide** au
  1er lancement → l'admin DOIT le changer immédiatement après login.

### Workaround utilisé pendant la résolution

L'utilisateur a contourné en **restaurant un .bak** d'une base de dev
contenant déjà les tables Permission peuplées (Admin/Administrators).
Cela a permis de valider toute la chaîne IIS / SQL / connection string
avant le hotfix code.

### Action requise post-déploiement

Lors du **prochain déploiement sur une base vraiment vide** (nouveau
client, nouvel environnement), l'`Updater` créera automatiquement
l'utilisateur Admin. Aucun script SQL manuel n'est nécessaire.

### Note : SSMS 18.2 vs SQL Express 2025

Au passage, l'utilisateur a **désinstallé SSMS 22 et installé SSMS 18.2**
pour contourner les soucis de TLS strict du driver `ODBC 18` avec les
certificats auto-signés de SQL Server 2025. SSMS 18.2 utilise le driver
legacy → connexion directe sans cocher `Trust server certificate` à chaque
fois. À conserver pour le quotidien admin tant que SSMS 20+ n'est pas
disponible.

---

## ✅ V1.7.1 — Unicité email Salarié (2026-05-10)

### Besoin métier

À la création d'un salarié, l'email doit être **unique** dans toute la base
(insensible à la casse, après trim). Plusieurs salariés sans email restent
toutefois autorisés (cas historique : anciens salariés sans adresse pro).

### Implémentation à 2 niveaux

#### Niveau 1 — Validation app `Salarie.OnSaving`

Dans `BusinessObjects/Salarie.cs`, après la normalisation et la validation
de format déjà existante, recherche d'un autre `Salarie` avec le même email
(autre `Oid`) :

```csharp
var doublon = Session.FindObject<Salarie>(
    CriteriaOperator.Parse("Email = ? AND Oid <> ?", Email, Oid));
if (doublon != null)
    throw new UserFriendlyException(
        $"L'email « {Email} » est déjà utilisé par le salarié " +
        $"{doublon.Matricule} – {doublon.FirstName} {doublon.LastName}. " +
        $"L'email doit être unique pour chaque salarié.");
```

→ Message UX clair indiquant **qui** détient déjà cet email.

#### Niveau 2 — Index SQL unique filtré (`Updater.EnsurePerformanceIndexes`)

```sql
CREATE UNIQUE INDEX UX_Salarie_Email
ON Salarie(Email)
WHERE Email IS NOT NULL AND Email <> '' AND GCRecord IS NULL
```

Filtre essentiel :
- `Email IS NOT NULL AND Email <> ''` → plusieurs vides autorisés
- `GCRecord IS NULL` → soft-deletes ignorés

Bloque les doublons même via imports CSV directs ou SQL manuel.

### Détection préalable des doublons existants

Avant de pousser V1.7.1 en prod ELTON (fait via `.bak` template), lancer :

```sql
SELECT LOWER(LTRIM(RTRIM(Email))) AS EmailNormalise, COUNT(*) AS Nb,
       STRING_AGG(Matricule + ' - ' + FirstName + ' ' + LastName, ' | ') AS Salaries
FROM Salarie
WHERE Email IS NOT NULL AND Email <> '' AND GCRecord IS NULL
GROUP BY LOWER(LTRIM(RTRIM(Email)))
HAVING COUNT(*) > 1;
```

Si doublons → vider l'email du mauvais salarié avant restart, sinon
l'index échoue silencieusement (la protection app reste opérationnelle,
mais la couche SQL est manquante).

### Pourquoi pas un `[RuleUniqueValue]` direct sur Email ?

`Salarie` hérite de `DevExpress.Persistent.BaseImpl.Person`, et `Email`
est une propriété de la classe parent. Override par `new` casserait le
mapping XPO. La validation `OnSaving` + index SQL filtré offre la même
protection avec une meilleure UX (message contenant le matricule du
salarié en doublon) et accepte les emails vides multiples.

### Fichiers modifiés

- `AdiPAIE_V02.Module/BusinessObjects/Salarie.cs` (OnSaving étendu)
- `AdiPAIE_V02.Module/DatabaseUpdate/Updater.cs` (index `UX_Salarie_Email`)
- `docs/deployment/DEPLOIEMENT_WINDOWS_SERVER_2022.md` (troubleshooting)
- `docs/deployment/DEPLOIEMENT_WINDOWS_SERVER_2022.pdf` (régénéré)

---

## ✅ V1.7.2 — 13ième MOIS + GRATIFICATION (2026-05-11)

### Origine du besoin

Question utilisateur initiale (2026-05-11) :
> « il peut arriver dans l'année que des bonus soit payé qui peuvent être
> égal à 2,5 ou 3 mois de salaire net ou brut ; au 31 décembre aussi il
> peut y avoir un 13ième mois — comment on peut gérer cela ? »

### Cadrage RH ELTON (grille validée 2026-05-11)

Grille de cadrage Excel `docs/specifications/Grille_RH_Primes_13eMois_V1.7.2.xlsx`
remplie par RRH + DAF. **Règles métier figées** :

#### 13ième mois — Automatique, droit conventionnel pour tous

| Règle | Valeur figée |
|---|---|
| Bénéficiaires | TOUS les salariés (sans condition d'ancienneté) |
| Calcul | `BrutRecurrent × MoisPresence ÷ 12` |
| Base | « Dernier brut récurrent perçu, hors congés et exceptionnels » |
| Multiplicateur | 1 mois (100 %) |
| Versement | Bulletin de **décembre** uniquement |
| Fiscalité | IR + CSS + IPRES + IPM (tout soumis, **sans lissage**) |
| Provision mensuelle | Sur **brut récurrent du mois en cours** (1/12) |
| Prorata départ | `BR / 12 × MoisPresence` versé sur **STC** (PAS en décembre) |

#### Gratification — Discrétionnaire, ad hoc

| Règle | Valeur figée |
|---|---|
| Déclenchement | Décision DG (lien évaluations en V1.8+) |
| Bénéficiaires | Liste manuelle saisie par RH |
| Base | **Net OU Brut au choix de la saisie** (toujours hors congés) |
| Multiplicateur | Libre (saisie numérique) |
| Fréquence | Ad hoc, sans calendrier |
| Fiscalité | Identique au 13ième mois |
| Workflow | 1️⃣ RH saisit liste → 2️⃣ DAF valide montants → 3️⃣ **RH intègre bulletin** |
| Reporting | A posteriori (pas de provision en amont) |

### Architecture implémentée

```
┌─ BrutRecurrentService (V1.7.2a) ──────────────────────────┐
│ Helper partagé, calcul du « brut récurrent »              │
│ INCLUS : SB, Sursalaire, PrimeAnciennete, IndemniteLogement│
│          PrimeTransport, AvantageNatureVehicule, customs   │
│ EXCLUS : HS, congés (CONGE_*, CP_*), gratifications, 13ième│
│ Méthodes : GetBrutRecurrent(bulletin),                    │
│            GetDernierBrutRecurrent(salarie, annee, mois), │
│            GetMoisPresence(salarie, annee),                │
│            GetCumulBrutRecurrent(salarie, annee)           │
└──────────────────┬─────────────────────────────────────────┘
                   │
        ┌──────────┴──────────┐
        ▼                     ▼
┌─ 13ième Mois (V1.7.2b)──┐ ┌─ Gratification (V1.7.2d) ─┐
│ Entité TreiziemeMois    │ │ (à coder)                  │
│   + workflow + badges   │ │ Workflow RH→DAF→RH         │
│ TreiziemeMoisService    │ │ BaseCalcul Net/Brut/Forfait│
│   .CalculerPourAnnee()  │ │                            │
│   .CalculerProrataSTC() │ │                            │
│ IntegrationService      │ │                            │
│   bulletin décembre/STC │ │                            │
│ Controller : 2 actions  │ │                            │
│   "Calculer / Intégrer" │ │                            │
└──────────┬──────────────┘ └────────────────────────────┘
           │
           ▼ (à coder)
┌─ Provision DAF (V1.7.2c) ─┐
│ Vue non-persistante       │
│ ProvisionTreiziemeMois    │
│ Cumul mensuel × salarié   │
└───────────────────────────┘
```

### Enums ajoutés (`DomainEnums.cs`)

```csharp
public enum RubriqueCanonique
{
    // ... existants ...
    HeuresSupplementaires = 600,
    TreiziemeMois = 700,    // 🆕 V1.7.2
    Gratification = 710     // 🆕 V1.7.2
}

public enum TreiziemeMoisStatut { Calcule, IntegreeBulletin, Annule }
public enum GratificationStatut {
    BrouillonRH, EnAttenteValidationDAF, ValideeDAF,
    IntegreeBulletin, Payee, Annule
}
public enum GratificationBaseCalcul { BrutRecurrent, NetRecurrent, Forfait }
```

### Sémantique BulletinLigne 13EME générée

| Champ XPO | Valeur | Affichage |
|---|---|---|
| `Rubrique` | rubrique `13EME` (canon `TreiziemeMois`) | « 13e mois » |
| `Base` | `BrutRecurrentReference` | base brute |
| `Taux` | `MoisPresence` (decimal) | mois présence |
| `Montant` | `MontantBrut` (=Base×Taux÷12) | montant final |

Note : `BulletinLigne` n'a pas de propriété `Quantite` (≠ erreur initiale).
Les 3 valeurs utilisées sont `Base`, `Taux` et `Montant`.

### État d'avancement V1.7.2 (2026-05-11 fin de journée)

| Sous-tâche | Description | Statut |
|---|---|---|
| **#65 V1.7.2a** | BrutRecurrentService (helper) | ✅ Complété |
| **#66 V1.7.2b** | Module 13ième (entité + service + bulletin + controller) | ✅ Complété |
| **#67 V1.7.2c** | Provision 13ième mensuelle (vue DAF) | ✅ Complété |
| **#68 V1.7.2d** | Module Gratification (entité + workflow + bulletin) | ✅ Complété |
| **#69 V1.7.2e** | Reporting Gratification a posteriori | ✅ Complété |
| **#70 V1.7.2f** | Help + MISSION_STATE final + commit + placement menu | ✅ Complété |
| **#71 V1.7.2b-bis** | 13ième prorata sur STC (départ en cours d'année) | ⏸️ Reporté V1.7.3 (besoin point d'accroche STC existant) |

### Fichiers créés / modifiés (état final V1.7.2)

**Créés** :
- `AdiPAIE_V02.Module/Services/BrutRecurrentService.cs`
- `AdiPAIE_V02.Module/BusinessObjects/TreiziemeMois.cs`
- `AdiPAIE_V02.Module/Services/TreiziemeMoisService.cs`
- `AdiPAIE_V02.Module/Services/TreiziemeMoisIntegrationService.cs`
- `AdiPAIE_V02.Module/Controllers/TreiziemeMoisController.cs`
- `AdiPAIE_V02.Module/NonPersistent/ProvisionTreiziemeMois.cs`
- `AdiPAIE_V02.Module/Services/ProvisionTreiziemeMoisService.cs`
- `AdiPAIE_V02.Module/Controllers/ProvisionTreiziemeMoisController.cs`
- `AdiPAIE_V02.Module/BusinessObjects/Gratification.cs`
- `AdiPAIE_V02.Module/Services/GratificationService.cs`
- `AdiPAIE_V02.Module/Controllers/GratificationController.cs`
- `AdiPAIE_V02.Module/NonPersistent/RapportGratification.cs`
- `AdiPAIE_V02.Module/Services/RapportGratificationService.cs`
- `AdiPAIE_V02.Module/Controllers/RapportGratificationController.cs`
- `AdiPAIE_V02.Blazor.Server/wwwroot/help/primes-13mois.html`
- `docs/specifications/Grille_RH_Primes_13eMois_V1.7.2.xlsx`

**Modifiés** :
- `AdiPAIE_V02.Module/Domain/DomainEnums.cs` (ajout 3 enums + 2 codes canoniques)
- `AdiPAIE_V02.Module/DatabaseUpdate/Updater.cs` (rubriques `13EME` + `GRATIF` marquées canon)
- `AdiPAIE_V02.Module/BusinessObjects/Salarie.cs` (collections `TreiziemesMois` + `Gratifications`)
- `AdiPAIE_V02.Blazor.Server/wwwroot/help/index.html` (card vers nouvelle page)

### Pièges & décisions à retenir

1. **`Quantite` n'existe pas sur `BulletinLigne`** → propriétés réelles =
   `Base` (decimal), `Taux` (decimal?), `Montant` (decimal). Erreur de
   build CS1061 rencontrée et corrigée.

2. **Le bloc d'enum 700/710 n'impose PAS l'ordre d'affichage** sur le
   bulletin. C'est `Rubrique.OrdreAffichage` qui pilote ça (rubrique
   `13EME` placée à `ordre: 70`, entre indemnités et brut total).

3. **Auto-exclusion 13ième / Gratification du brut récurrent** : géré
   via `BrutRecurrentService.CodesExclus` (HashSet d'enum). Évite la
   récursivité « un 13ième dans le BR du prochain 13ième ».

4. **Idempotence partout** : `TreiziemeMoisService.CalculerPourAnnee()`
   ne re-touche pas un calcul déjà `IntegreeBulletin`. L'intégration
   à `BulletinLigne` met à jour la ligne existante au lieu de créer
   un doublon.

5. **STC départ en cours d'année (#71)** : un salarié sorti dans l'année
   est EXCLU du calcul de décembre. Son prorata sera fait par
   `TreiziemeMoisService.CalculerProrataSTC()` puis intégré sur le
   bulletin de sortie via `IntegrationService.IntegrerSurSTC()`.
   Point d'accroche dans le workflow STC existant à identifier
   pour automatiser.

---

### Menu Reports réactivé

Pour permettre l'édition du rapport `BulletinPaie` dans le designer XAF.
Visibilité gérée par RBAC : Admin et rôles avec `CanEditModel = true` voient
le menu, autres rôles non.

### Désactivés / nettoyés

- `BulletinArchivePdfBulkController.cs` — fichier vidé (action redondante avec Publier)
- `ValiderEtEnvoyer`, `RenvoyerBulletin`, `EnvoyerClePDF`, `EnvoyerBulletinEmail` —
  actions obsolètes masquées via `IsVisible="False"` XAFML + `Active.SetItemValue`

### Roadmap V1.5+

- Action UI « Exporter rapport REPX » pour automatiser l'export PowerShell
- Suppression définitive des controllers obsolètes après stabilisation
- Help bulletins.html + espace-salarie.html mis à jour

---

## ✅ V1.4.3 — WORKFLOW PUBLICATION BULLETIN + ESPACE SALARIÉ (2026-05-07)

> **Décision CODIR mai 2026** : plus d'envoi PDF par email. Le salarié se
> connecte à l'Espace Salarié authentifié pour télécharger son bulletin.
> L'email devient une simple notification d'information.

### Workflow Bulletin V1.4.3

```
Brouillon ─[Valider]→ Validé ─[Publier]→ Envoyé ─[Clôturer]→ Cloturé
                                ↑
                                ├─[Dépublier]→ Validé (correction)
                                └─[Notifier]→ ré-envoi email seul
```

### Décisions clés

- **Réutilisation du statut `Envoye`** (sémantique « Publié ») plutôt que
  d'ajouter un nouveau statut → zéro migration de données.
- **`DatePublication != NULL`** = critère unique de visibilité côté Espace
  Salarié → bulletin n'apparaît dans Mes bulletins QUE si RH a explicitement
  cliqué Publier. Pas de migration silencieuse des bulletins legacy.
- **Plus de clé PDF** : le salarié est authentifié dans son espace, l'auth
  remplace la clé. Email contient juste un lien vers l'espace.
- **PDF archivé** dans `Bulletin.PdfArchive` (FileData) au moment du Publier.
  Stable, immuable, audit-friendly.

### Modèle de données

**`Bulletin.cs`** (nouveau champs) :
- `DatePublication : DateTime?` — date du Publier (audit)
- `PublieParUser : string` — utilisateur qui a publié (audit)
- `EstPublie : bool` (NonPersistent) — `Statut >= Envoye`

**`PdfArchive`** (FileData, déjà existant) — masqué dans ListView/DetailView
(`VisibleInListView(false)`, `VisibleInDetailView(false)`).

### Services

- **`BulletinPublicationService.cs`** (nouveau) — orchestration :
  - `Publier(bulletin, os, user)` — génère PDF + archive + statut Envoye +
    DatePublication + email notification (best-effort, non bloquant)
  - `Depublier(bulletin, os, user)` — repasse à Validé, conserve PdfArchive
  - `EnvoyerNotification(bulletin, os)` — re-envoi email seul

### Controllers

**Côté RH (`Bulletin_ListView`) :**
- `BulletinPublishController.cs` (nouveau) — actions Publier / Dépublier /
  Notifier (sélection multiple supportée)
- `BulletinValiderEnvoyerController.cs` — `ValiderEtEnvoyer` et
  `RenvoyerBulletin` désactivés (V143_Obsolete)
- `EnvoyerClePayslipController.cs` — `EnvoyerClePDF` désactivé
- `BulletinTelechargerController.cs` — limité à `Bulletin_EspaceSalarie_ListView`

**Côté Espace Salarié (vue dédiée `Bulletin_EspaceSalarie_ListView`) :**
- `BulletinSalarieFilterController.cs` — filtre forcé
  `Salarie.Oid = currentUser AND DatePublication IS NOT NULL`
- `BulletinEspaceSalarieReadOnlyController.cs` — désactive toutes actions RH
  (Publier, Dépublier, Notifier, Imprimer, Recalc, etc.)
- `BulletinEspaceSalarieNoDetailController.cs` (nouveau) — bloque le
  drill-down vers le DetailView (pas d'accès au détail technique)

### Vue Espace Salarié

**`Bulletin_EspaceSalarie_ListView`** (nouvelle, dans `Model.DesignedDiffs.xafml`)
- Lecture seule absolue (`AllowEdit/AllowNew/AllowDelete = False`)
- Colonnes : Année, Mois, Période, NetAPayer, DatePublication
- Action unique : **Télécharger** (stream du `PdfArchive` sans clé)
- Pas de DetailViewID utilisable (drill-down bloqué par controller)

Menu navigation : `Mon espace > Mes bulletins` → pointe vers cette vue.

### Rapport BulletinPaie — sécurisation

- **REPX embarqué** dans le projet (`Reports/BulletinPaie.repx`, ~87 Ko) —
  versionné Git, déployable.
- **Auto-seed** dans `Updater.SeedBulletinReportIfMissing` — au démarrage,
  si `BulletinPaie` absent en DB → recréé depuis la ressource embarquée.
  Idempotent : ne touche jamais le rapport custom existant.
- **Filet runtime** dans `BulletinPdfService.LoadOrCreateReport` — si
  suppression accidentelle en cours de session, recréation au prochain appel.
- **Menu Reports réactivé** (admin uniquement par RBAC) pour permettre la
  modification du design dans le designer XAF.

### Procédure d'export REPX (pour versionner les modifs designer)

Quand le rapport est modifié dans le designer XAF, exporter le `.repx` vers
le repo pour pouvoir le redéployer :

```powershell
# Adapter Server selon environnement (CPC-adien-ZA8ZN\SQLEXPRESS dev, R24PROD prod)
$conn = New-Object System.Data.SqlClient.SqlConnection "Server=CPC-adien-ZA8ZN\SQLEXPRESS;Database=SunuPaie_Recette;Integrated Security=true;TrustServerCertificate=true"
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Content FROM ReportDataV2 WHERE Name='BulletinPaie' AND GCRecord IS NULL"
$bytes = $cmd.ExecuteScalar()
[System.IO.File]::WriteAllBytes("C:\Dev\AdiPAIE_V02\AdiPAIE_V02\AdiPAIE_V02.Module\Reports\BulletinPaie.repx", $bytes)
$conn.Close()
Write-Host "Exporté : $($bytes.Length) octets"
```

Puis `git add Reports/BulletinPaie.repx && git commit -m "feat(reports): MAJ design BulletinPaie"`.

### Email de notification

Sujet : `[SunuPaie] Votre bulletin de paie [Mois] [Année] est disponible`
Corps HTML simple, **0 PJ, 0 clé**, lien vers Espace Salarié, instructions
step-by-step pour le télécharger.

### Bulletins legacy (pré-V1.4.3)

- Critère `DatePublication IS NOT NULL` les exclut → invisibles pour le salarié
- Pour les rendre visibles : RH les sélectionne sur Bulletin_ListView →
  Publier (multi-sélection supportée). Le service génère le PDF rétroactivement,
  set DatePublication, envoie l'email. Idempotent.
- Pas de migration automatique au startup (volontaire, évite envois massifs
  involontaires).

### Décisions UI

- Action « Re-notifier » renommée **« Notifier »** (sémantiquement plus juste
  car couvre 1ʳᵉ notif et ré-envoi).
- Bouton « Mon bulletin » sur Bulletin_ListView (RH) → supprimé. RH a
  désormais Imprimer / Publier à la place.
- Bouton « Télécharger » sur la vue Espace Salarié (cliché "Action_Export").

### Fichiers impactés

**Nouveaux :**
- `Services/BulletinPublicationService.cs`
- `Controllers/BulletinPublishController.cs`
- `Controllers/BulletinEspaceSalarieNoDetailController.cs`
- `Reports/BulletinPaie.repx` (binaire embedded resource)

**Modifiés :**
- `BusinessObjects/Bulletin.cs` (+ DatePublication, PublieParUser, EstPublie)
- `Domain/DomainEnums.cs` (commentaire sémantique BulletinStatut.Envoye)
- `Controllers/BulletinSalarieFilterController.cs` (filtre DatePublication)
- `Controllers/BulletinEspaceSalarieReadOnlyController.cs` (+ actions masquées)
- `Controllers/BulletinValiderEnvoyerController.cs` (désactivation V143_Obsolete)
- `Controllers/EnvoyerClePayslipController.cs` (désactivation)
- `Blazor.Server/Controllers/BulletinTelechargerController.cs` (TargetViewId
  + suppression fallback clé)
- `Services/BulletinPdfService.cs` (LoadOrCreateReport + TrySeedFromEmbeddedRepx)
- `DatabaseUpdate/Updater.cs` (SeedBulletinReportIfMissing)
- `Module/AdiPAIE_V02.Module.csproj` (EmbeddedResource BulletinPaie.repx)
- `Module/Model.DesignedDiffs.xafml` (vue Espace Salarié + menu + actions)

### À faire après stabilisation

- [ ] Suppression définitive des controllers obsolètes (`BulletinEmailController`,
      `EnvoyerClePayslipController`) après vérification 0 régression
- [ ] Suppression du fichier `BulletinArchivePdfBulkController.cs` (vidé)
- [ ] Documenter dans help/bulletins.html et help/espace-salarie.html
- [ ] Roadmap V1.5 : action UI « Exporter rapport REPX » pour automatiser
      l'export PowerShell


---

## V1.4.3 — WORKFLOW PUBLICATION BULLETIN + ESPACE SALARIE (2026-05-07)

**Decision CODIR mai 2026** : plus d'envoi PDF par email. Le salarie se
connecte a l'Espace Salarie authentifie pour telecharger son bulletin.
L'email devient une simple notification d'information.

### Workflow

Brouillon -> Valider -> Valide -> Publier -> Envoye -> Cloturer -> Cloture
                                  Depublier (correction)
                                  Notifier (re-envoi email)

### Decisions cles

- Reutilisation du statut Envoye (semantique "Publie") - zero migration
- DatePublication != NULL = critere de visibilite Espace Salarie
- Plus de cle PDF - auth Espace Salarie remplace la cle
- PDF archive dans Bulletin.PdfArchive (FileData) au Publier

### Fichiers nouveaux

- Services/BulletinPublicationService.cs : Publier / Depublier / Notifier (async)
- Controllers/BulletinPublishController.cs : actions RH multi-selection
- Controllers/BulletinEspaceSalarieNoDetailController.cs : bloque drill-down
- Reports/BulletinPaie.repx : design custom embedded (87 580 octets)

### Fichiers modifies majeurs

- BusinessObjects/Bulletin.cs : DatePublication, PublieParUser, EstPublie
- Controllers/BulletinSalarieFilterController.cs : filtre DatePublication != null
- Controllers/BulletinValiderEnvoyerController.cs : actions obsoletes desactivees
- Blazor.Server/Controllers/BulletinTelechargerController.cs : scoped vue Espace Salarie
- Services/BulletinPdfService.cs : auto-bootstrap rapport (REPX embedded + fallback)
- DatabaseUpdate/Updater.cs : SeedBulletinReportIfMissing au demarrage
- Module.csproj : EmbeddedResource BulletinPaie.repx
- Model.DesignedDiffs.xafml : vue Bulletin_EspaceSalarie_ListView, menu, captions

### Fix culture FCFA

Blazor.Server/Startup.cs : clone fr-FR, override CurrencySymbol = "FCFA",
CurrencyDecimalDigits = 0, format "1 234 FCFA". Plus de EUR partout.

### Fix UI Blazor

Tous les handlers SMTP passent par SendAsync (extension threadpool)
pour ne pas bloquer le thread SignalR. Plus de "Loading..." infini sur Notifier.

### Procedure d'export REPX (a utiliser quand le rapport est modifie dans designer)

```powershell
# Adapter Server selon environnement
$conn = New-Object System.Data.SqlClient.SqlConnection "Server=CPC-adien-ZA8ZN\SQLEXPRESS;Database=SunuPaie_Recette;Integrated Security=true;TrustServerCertificate=true"
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Content FROM ReportDataV2 WHERE Name='BulletinPaie' AND GCRecord IS NULL"
$bytes = $cmd.ExecuteScalar()
[System.IO.File]::WriteAllBytes("C:\Dev\AdiPAIE_V02\AdiPAIE_V02\AdiPAIE_V02.Module\Reports\BulletinPaie.repx", $bytes)
$conn.Close()
```

Puis git add Reports/BulletinPaie.repx && git commit.

### Bulletins legacy (pre-V1.4.3)

- Critere DatePublication IS NOT NULL les exclut de la vue salarie
- RH publie retroactivement via Bulletin_ListView -> multi-selection -> Publier
- Pas de migration automatique au startup (volontaire - evite envois massifs)

### Toolbar RH apres V1.4.3

[ Valider ] [ Publier ] [ Depublier ] [ Notifier ] [ Cloturer ] [ Imprimer ] [ Recalc ]

Anciens boutons masques : Valider+envoyer, Renvoyer PDF, Envoyer cle PDF.

### Vue Espace Salarie

- Bulletin_EspaceSalarie_ListView (nouvelle, AllowEdit/New/Delete=False)
- Colonnes : Annee, Mois, Periode, NetAPayer, DatePublication
- Action unique : Telecharger (stream PdfArchive sans cle)
- Drill-down vers DetailView bloque
- Filtre force : Salarie.Oid = currentUser AND DatePublication IS NOT NULL

### Menu Reports reactive

Pour permettre l'edition du rapport BulletinPaie dans le designer XAF.
Visibilite geree par RBAC : Admin et roles avec CanEditModel = true voient
le menu, autres roles non.

### Roadmap V1.5+

- Action UI "Exporter rapport REPX" pour automatiser l'export PowerShell
- Suppression definitive des controllers obsoletes apres stabilisation
- Help bulletins.html + espace-salarie.html mis a jour

---

## ✅ V1.8 — REFONTE CALCUL CONGÉS + ICCP + RBAC AUTONOMES (juin 2026)

### Contexte métier

Préparation de la mise en production ELTON. Plusieurs points découverts par
le RH lors des tests de recette :

1. **Formule indemnité de congé** : besoin d'aligner sur la CCT Sénégal Art. 57
   et sur le calcul ELTON existant (validé via fichier `Congés.xlsx` Abdoulaye DIENG).
2. **Provision congés** dans AdiPAIE utilisait `/22` (jours ouvrés CCT théorique)
   alors qu'ELTON paie avec `/24` (= 2 j/mois × 12). Écart de ~8 %.
3. **ICCP DossierOffboarding** sous-évaluée (basée sur `SalaireBase / 26` uniquement).
4. **Bonus ancienneté** : valeurs codées avec seuils `<=` incorrects, bonus 25+ ans à 7
   alors que CCT dit +6 (ELTON applique +7 — convention plus favorable).
5. **Bonus mère** : règle A (+1j/enfant <14ans) codée à tort — pas dans CCT L150.
6. **Mise en prod** : besoin d'un mécanisme pour saisir le solde initial de chaque
   salarié (depuis Excel RH) et pour rétro-saisir les bulletins de congé déjà payés
   dans l'ancien système.
7. **RBAC** : combo RH+Employé créait des bugs UX (vue restreinte, colonnes masquées,
   actions cachées) à cause de controllers qui se déclenchaient sur tout user lié
   à un Salarié, même les managers.

### Bloc A — Corrections directes du moteur de paie

| Fichier | Modification |
|---|---|
| `ProvisionCongesService.cs` | Diviseur `JoursOuvresMois` : **22 → 24** |
| `ProvisionCongesService.CalculerBonusAnciennete()` | Refactor en seuils `≥` (inclusifs). Bonus : 0/1/2/3/**7** |
| `ProvisionCongesService.CalculerBonusEnfants()` | Suppression de la règle A (1j/enfant <14ans). Garde B et C |
| `DossierOffboarding.CalculerSoldeToutCompte()` | ICCP refait : `(Σ Brut 12 mois / 12) × Solde / 24` au lieu de `SalaireBase × Solde / 26` |
| `DossierOffboarding.CalculerBrutImposableMoyen12Mois()` | Nouvelle méthode privée (clone de ProvisionCongesService) |

### Bloc B — Modèle enrichi pour la mise en prod

**SoldeConge** — 3 nouveaux champs persistants :
- `SoldeArreteAu` (DateTime?) — date de constat du solde initial (varie selon Excel RH)
- `SoldeAVerifier` (bool) — flag pour les 16 lignes "bleues" du fichier ELTON sans solde fiable
- `SourceInitialisation` (string 120) — origine de la donnée

**ParametresPaie** — 2 nouvelles options :
- `ModeBulletinConges` enum {BulletinUnique, BulletinSepare} — pratique ELTON = BulletinUnique
- `ModeBaseCFCE` enum {AvecAvantagesNature, SansAvantagesNature} — décision DAF = Avec

**Rubrique** — nouvelle rubrique seedée par `SeedService` :
- Code : `ICCP`
- Libellé : "Indemnité de congés compensatrice"
- Ordre : 24 (juste après CONGE_PAYE ordre 23)
- TypeRef : tIndImpos (imposable IR/TRIMF/CFCE, soumise IPRES/CSS)
- Distinction sémantique : CONGE_PAYE = allocation versée quand on part en congé,
  ICCP = compensation monétaire (rachat ou STC)

### Bloc C — Service + UI (popups)

**Service `BulletinCongeService.cs`** (nouveau) :
- `CalculerAllocationAuto(os, salarie, joursDus, annee, mois)` :
  formule CCT Art. 57 = `(Σ brut imposable 12 mois / 12) × jours / 24`
- `CreerLigneAllocationConge(...)` : ajoute ligne CONGE_PAYE
- `CreerLigneRachatICCP(...)` : ajoute ligne ICCP
- `NettoyerRubriquesSalaireMensuel(os, bulletin)` : bascule bulletin mensuel
  en bulletin de congé en supprimant SB/SURSAL/LOGT/TRANS/ANC/HS/etc. tout
  en conservant avantages en nature + cotisations
- Codes nettoyés : `SB, SURSAL, ANC, 13EME, GRATIF, LOGT, TRANS, INDEM_GEN_IMP,
  INDEM_GEN_NON_IMP, PRIME_GEN, HS25, HS50, HS100`

**Popup "Saisir un congé"** sur fiche Salarié :
- DTO : `NonPersistent/SaisieCongeRequest.cs`
- Controller : `Controllers/SaisieCongeController.cs`
- 2 modes opération : Allocation de congé (départ/régularisation) / Rachat ICCP
- 2 modes calcul : AUTO (depuis historique 12 mois) / MANUEL (rétroactif)
- Checkbox "Basculer en bulletin de congé" cochée par défaut → nettoie le bulletin
- Le mode bulletin (unique/séparé) lu depuis ParametresPaie.ModeBulletinConges

**Popup "Saisir solde initial"** sur fiche Salarié :
- DTO : `NonPersistent/SaisieSoldeInitialRequest.cs`
- Controller : `Controllers/SaisieSoldeInitialController.cs`
- Saisie : TypeConge, Année, JoursReportes (accepte négatif), SoldeArreteAu,
  SoldeAVerifier, Source, Commentaire
- Crée/met à jour le SoldeConge avec traçabilité complète

### Bloc D — Boutons sur DetailView Bulletin

**`BulletinRecalcController.cs`** refactorisé — 2 actions distinctes :
- **"Recharger bulletin"** (anciennement "Recalculer") → `RecalculerDepuisParametrage()`
  Réinitialise depuis profil salarié (SB, SURSAL, LOGT, TRANS rajoutés + cotisations)
  + ConfirmationMessage pour éviter écrasement accidentel
- **"Recalculer cotisations"** (NOUVEAU) → `RecalculerSurGrilleExistante()`
  Recalcule UNIQUEMENT cotisations + totaux, sans toucher aux rubriques de gain.
  Idéal pour bulletin de congé personnalisé.

**`BulletinAjouterLigneController.cs`** (NOUVEAU) :
- Workaround pour bug XAF Blazor sur grille Aggregated : bouton "Nouveau"
  natif ne s'affichait pas même avec permission Create
- Action "Ajouter une ligne" dans la barre Edit du DetailView Bulletin
- Ouvre popup avec Bulletin pré-rempli automatiquement

### Bloc E — Fixes RBAC

**Combo RH+Employé décombiné** :
- `HideEspaceSalarieController.cs` (NOUVEAU) : masque le menu "Mon espace"
  pour les rôles RH/DAF/DG via modification runtime du Model.NavigationItems
  (le Deny déclaratif XAF ne fonctionnait pas pour les sub-items).
- `OnDeactivated()` restore `Visible=true` pour éviter persistence dans
  ModelDifference user.

**Permissions ajoutées** dans `InitialiserRolesGRHController.cs` :
- RH : `BulletinLigne` "rw" → **"rwcd"** (Create + Delete pour bouton "Nouveau")
- RH : `DemandeDeplacement` "rw" → **"rwcd"**
- RH : `CongeDemande` "rw" → **"rwcd"**
- DAF : `DemandeDeplacement` "r" → **"rwcd"**
- DAF : `CongeDemande` "r" → **"rwcd"**
- DAF : `DemandeAttestation` (ajouté) → **"rwcd"**
- DG : `CongeDemande` "r" → **"rwcd"**
- DG : `DemandeAttestation` "r" → **"rwcd"**
- DG : `DemandeDeplacement` "r" → **"rwcd"**

**Bug critique corrigé** — `EstSalarieConnecte` vs `DoitRestreindreEspaceSalarie` :
- `EstSalarieConnecte` retourne true pour TOUT user lié à un Salarié (par Email),
  y compris managers RH/DAF/DG/Admin.
- `DoitRestreindreEspaceSalarie` exclut les rôles managers.
- Fix appliqué à :
  - `BulletinEspaceSalarieReadOnlyController.cs` (masquait toutes les actions Edit)
  - `EspaceSalarieColumnsController.cs` (masquait colonnes BrutFiscal/BrutSocial/Matricule/etc.)
- Les autres controllers (SoldeCongeController, etc.) gardent EstSalarieConnecte
  pour l'instant — à corriger au cas par cas si bugs UX remontés.

### Bloc F — Modèle et UI

**`BulletinLigne.cs`** :
- Nouvelle contrainte `[RuleCombinationOfPropertiesIsUnique]` sur (Bulletin, Rubrique)
- Empêche la double saisie d'une même rubrique sur un bulletin

**`Bulletin.cs`** :
- Nouvelle méthode `CalculerBaseCFCE_SelonParametres(brutFiscal)` qui lit
  ParametresPaie.ModeBaseCFCE
- Si SansAvantagesNature : exclut les rubriques dont TypeRef.Code commence par "AV_NAT"
- IR/TRIMF/IRPP continuent à utiliser bf complet (cohérent)

**`ParametresPaie.cs`** corrections affichage :
- Format `R_IR_Abattement_TauxPercent` : "p0" → "N0" (3000% → 30%)
- Format `R_IR_ReductionFamille_Pourcentage` : "p0" → "N0" (idem)

**`Model.DesignedDiffs.xafml`** :
- Onglet Fiscalité : ajout LayoutItem "ModeBaseCFCE" dans groupe Options
- Onglet Référentiel paie : ajout LayoutItem "ModeBulletinConges"
- Actions renommées : "Recalculer" → "Recharger bulletin"
- Nouvelle action déclarée : "Bulletin_RecalculerCotisations"
- Hidden actions sur Bulletin_EspaceSalarie_ListView mises à jour
- `BulletinLigne_ListView` : AllowNew=True/AllowEdit=True/AllowDelete=True forcés

### Bloc G — Help mis à jour

4 pages help enrichies avec section orange "🆕 V1.8 (juin 2026)" :

1. **`wwwroot/help/conges.html`** : formule CCT Art. 57, bonus ancienneté/mère,
   popups Saisir solde initial + Saisir un congé, contrainte d'unicité
2. **`wwwroot/help/prets.html`** : procédure reprise prêt en cours (taux=0%,
   PrincipalConstant) + exemple chiffré
3. **`wwwroot/help/parametrage.html`** : ModeBaseCFCE, ModeBulletinConges,
   correctifs format affichage, rubrique ICCP
4. **`wwwroot/help/offboarding.html`** : correction ICCP (brut moyen 12 mois × solde / 24)
   + impact pour forts cumuls (cas DAF/top management : pas de rachat en cours
   de carrière, ICCP à la retraite)

### Bloc H — Déploiement

**Script `docs/deployment/Deploy-AdiPAIE-V18.ps1`** créé :
- Automatise les 7 étapes (vérifs préalables → backup → stop IIS → copie →
  restore config → cleanup cache → restart)
- Mode `-DryRun` pour test sans exécution
- Conserve `dbconfig.json`, `appsettings.json`, `web.config`, `App_Data/`
- Backup SQL automatique avec horodatage
- Checklist post-déploiement affichée à la fin
- Procédure de rollback documentée

### Décisions métier validées avec le RH

| Question | Réponse RH | Décision code |
|---|---|---|
| Formule indemnité | (Σ brut 12 mois / 12) × jours / 24 | Implémentée |
| Diviseur provision comptable | 24 (pas 22) | `JoursOuvresMois = 24m` |
| Bonus ancienneté 25+ ans | +7 (convention ELTON plus favorable que CCT +6) | `return 7` |
| Bonus mère règle A (1j/<14ans) | Supprimer (pas dans CCT) | Supprimée |
| 16 lignes bleues Excel | Démarrer avec solde "à vérifier" | Flag `SoldeAVerifier` |
| Bulletins de congé déjà émis (5 cas Janv-Mai 2026) | Saisis manuellement par RH | Pas d'import auto |
| Bulletins mensuels Janv→date de mise en prod | Saisis par RH (en rétroactif) | Pas d'import auto |
| Bulletin unique ou séparé pour congé ? | Unique aujourd'hui, mais paramétrable | `ModeBulletinConges` |
| Code rubrique pour rachat | Code distinct "Indemnité de congés compensatrice" | Nouvelle rubrique ICCP |
| Soldes négatifs autorisés | Oui (cas Gueladio BA -8 j) | Aucune contrainte ajoutée |
| RH a-t-il besoin de "Mon espace" ? | Non, décombiner du rôle Employé | HideEspaceSalarieController |

### Procédure de déploiement V1.8

**Avant** :
1. BACKUP BDD
2. Audit duplications BulletinLigne (SQL fourni dans docs/deployment)
3. Si duplications → nettoyer avant déploiement (sinon contrainte unicité bloque)
4. Lancer `Deploy-AdiPAIE-V18.ps1`

**Premier accès** :
- Updater XAF ajoute automatiquement les colonnes SQL :
  - `SoldeConge.SoldeArreteAu` / `.SoldeAVerifier` / `.SourceInitialisation`
  - `ParametresPaie.ModeBulletinConges` / `.ModeBaseCFCE`
- Contrainte XPO `(Bulletin, Rubrique)` unicité appliquée

**Actions manuelles admin** :
1. ParametresPaie → bouton **"Init. rôles GRH"** (re-pose les permissions Create
   sur BulletinLigne, CongeDemande, DemandeDeplacement pour RH/DAF/DG)
2. ParametresPaie → bouton **"Recharger le référentiel paie"** (crée rubrique ICCP)
3. (Optionnel) Décombiner les users RH+Employé via Administration → Users
4. (Optionnel) Reset ModelDifference si bugs d'affichage chez certains users

### Fichiers impactés V1.8

**Modifiés** :
- `Services/ProvisionCongesService.cs`
- `Services/SeedService.cs`
- `BusinessObjects/Bulletin.cs`
- `BusinessObjects/BulletinLigne.cs`
- `BusinessObjects/SoldeConge.cs`
- `BusinessObjects/ParametresPaie.cs`
- `BusinessObjects/RH/DossierOffboarding.cs`
- `Domain/DomainEnums.cs`
- `Controllers/InitialiserRolesGRHController.cs`
- `Controllers/BulletinRecalcController.cs`
- `Controllers/BulletinEspaceSalarieReadOnlyController.cs`
- `Controllers/EspaceSalarieColumnsController.cs`
- `Controllers/EspaceSalarieHelper.cs`
- `Model.DesignedDiffs.xafml`
- `wwwroot/help/{conges,prets,parametrage,offboarding}.html`

**Créés** :
- `Services/BulletinCongeService.cs`
- `NonPersistent/SaisieSoldeInitialRequest.cs`
- `NonPersistent/SaisieCongeRequest.cs`
- `Controllers/SaisieSoldeInitialController.cs`
- `Controllers/SaisieCongeController.cs`
- `Controllers/HideEspaceSalarieController.cs`
- `Controllers/BulletinAjouterLigneController.cs`
- `docs/deployment/Deploy-AdiPAIE-V18.ps1`

### Issues résiduelles à corriger en V1.9 (optionnel)

1. **`EstSalarieConnecte` → `DoitRestreindreEspaceSalarie`** : 12 autres
   occurrences à auditer au cas par cas dans :
   - `SoldeCongeController.cs` (2x)
   - `CongeDemandeEspaceSalarieController.cs` ligne 181 (la 95 est OK : pré-remplissage)
   - `DeplacementEspaceSalarieController.cs`
   - `EspaceSalarieReadOnlyReferentielsController.cs` (3x)
   - `DemandeAttestationEspaceSalarieController.cs`
   - `EspaceSalarieListViewDeleteController.cs` (3x)

2. **Bouton "Nouveau" natif de la grille Lignes du Bulletin** : non résolu (bug
   XAF Blazor sur les Aggregated collections). Workaround = action "Ajouter une
   ligne" dans la barre Edit. À ré-investiguer si DevExpress publie un fix.

3. **HideEspaceSalarieController** : approche runtime (modification Model.NavigationItems
   via réflexion). Fonctionne mais nécessite OnDeactivated → RestoreVisible pour
   éviter persistence dans ModelDifference user. Si un jour XAF Blazor corrige
   le bug Deny+Allow sur Navigation Permissions, on pourra revenir à l'approche
   déclarative (plus propre).

4. **Reprise des prêts** : RH a remonté que les échéances calculées diffèrent
   de l'ancien système. Cause identifiée (rajout intérêts au-dessus du solde
   restant TTC + arrondis). Procédure documentée dans `help/prets.html`
   (taux=0%, PrincipalConstant). Si bug persiste à la mise en prod : ajouter
   action "Reprendre un prêt en cours" avec saisie explicite de la mensualité.

5. **MD utilisateurs** : la table `ModelDifference` peut accumuler des layouts
   cassés au fil des sessions (notamment lors de Column Chooser maladroits).
   Prévoir un bouton admin "Vider mes customisations" qui supprime le MD de
   l'user courant.

### Tests validés en recette

- ✅ Build sans erreur ni warning
- ✅ Allocation de congé Abdoulaye 2026 = 5 879 859 FCFA (cohérent fichier RH)
- ✅ Bouton "Saisir un congé" sur fiche Salarié (mode AUTO + MANUEL)
- ✅ Bouton "Saisir solde initial" (avec flag à vérifier)
- ✅ Bouton "Recharger bulletin" + ConfirmationMessage
- ✅ Bouton "Recalculer cotisations" (préserve lignes manuelles)
- ✅ Bouton "Ajouter une ligne" sur DetailView Bulletin
- ✅ Combo RH+Employé : "Mon espace" masqué, toutes les colonnes visibles,
  toutes les actions Edit présentes
- ✅ Contrainte d'unicité BulletinLigne : empêche les doublons
- ✅ Format affichage IMAB : 30 % au lieu de 3 000 %
- ✅ Paramètres CFCE + Mode bulletin visibles dans ParametresPaie
- ✅ Rubrique ICCP créée par "Recharger le référentiel paie"

### Cas particuliers métier ELTON identifiés

- **DAF** (Adama TANDJIGORA) : a cumulé ~220 j de CP non pris. Pratique ELTON :
  pas de rachat en cours de carrière. Sera payé en ICCP au départ retraite via
  DossierOffboarding (formule corrigée V1.8).
- **Gueladio BA** : solde négatif -8 j (jours pris en avance). Accepté en base.
- **16 lignes "bleues"** Excel : 16 salariés dont le RH ne connaît pas le solde.
  Démarrage avec SoldeAVerifier=true, JoursReportes=0. À régulariser après mise
  en prod si info trouvée.
- **5 bulletins de congé** déjà émis Janv-Mai 2026 : à re-saisir manuellement
  par RH via popup "Saisir un congé" mode MANUEL (montant lu sur ancien système).
- **Cumul Janv→mois de mise en prod** : tous les bulletins mensuels sont saisis
  par le RH via le calcul automatique standard (PeriodePaie → Calculer).

## ⭐ MISSION V1.8 TERMINÉE — prête pour déploiement recette/prod ⭐

---

### Clôture Git V1.8 (22 juin 2026)

**Commit consolidé V1.8** effectué via le fichier `docs/deployment/COMMIT_V18.txt`
(8 blocs A→H + décisions métier + procédure de déploiement + 25 fichiers
impactés + tests validés + issues V1.9).

Script automatisé disponible : `docs/deployment/Commit-V18.ps1`
(modes `-DryRun` / `-NoPush`).

**Hash & métadonnées du commit** :

| Élément | Valeur |
|---|---|
| Branche | `dev` |
| Hash initial | `201ad09` (avant amend) |
| Hash final | `4bd896e` (après amend incluant MISSION_STATE.md) |
| Date | 22 juin 2026 |
| Fichiers changés | 49 (48 code/docs + MISSION_STATE.md) |
| Insertions | +6 531 (+ deltas amend) |
| Suppressions | -333 |
| Remote | `origin/dev` (pushé avec `--force-with-lease`) |
| Titre | `V1.8 — Refonte calcul congés + ICCP + RBAC autonomes + Net hors AvNature` |

**Procédure exécutée** :

```powershell
cd C:\Dev\AdiPAIE_V02
git add .
git commit -F docs\deployment\COMMIT_V18.txt
# git push origin dev    (à exécuter quand prêt à pousser sur origin)
```

**Fichiers exclus du repo** : `Congés*.xlsx` à la racine ajoutés au `.gitignore`
(données réelles ELTON, gérées hors Git Teams/Drive).

**Suppressions notables** : `COMMIT_MSG.txt` à la racine (ancien fichier de
travail, remplacé par `docs/deployment/COMMIT_V18.txt` versionné).

**Post-commit côté serveur recette/prod** :

1. `dotnet build` (vérification finale)
2. `.\docs\deployment\Deploy-AdiPAIE-V18.ps1` (déploiement automatisé)
3. Login admin → **Init. rôles GRH** (re-pose permissions Create RBAC)
4. Login admin → **Recharger le référentiel paie** (crée rubrique ICCP +
   TypeRef `AV_NAT_NON_IMP`)
5. SQL one-shot pour corriger `AV_TEL` existant (cf. COMMIT_V18.txt §6)

### Issues résiduelles tracées pour V1.9

- Audit des 12 autres occurrences `EstSalarieConnecte` →
  `DoitRestreindreEspaceSalarie` dans 7 controllers Espace Salarié
- Bouton "Nouveau" natif grille Aggregated Bulletin.Lignes (workaround
  actuel suffit : action "Ajouter une ligne")
- Reset ModelDifference par-user (bouton admin "Vider mes customisations")
- Action "Reprendre un prêt en cours" avec saisie explicite de la mensualité
- Retrait du fallback heuristique `EstCadre` (libellé commence par "Cadre")
  une fois toutes les catégories migrées sur le booléen explicite — voir
  V1.8.1 ci-dessous

---

## 🔧 V1.8.1 — HOTFIX IPRES Régime Cadre faussement appliqué aux non-cadres (22 juin 2026)

### Bug

Sur un bulletin de salarié non-cadre, le système ajoutait à tort la cotisation
**IPRES Régime Cadre** (IPRES_RC) en plus de l'IPRES Régime Général. Cause
racine dans `Bulletin.EstCadre(Salarie)` :

```csharp
return lib != null && lib.IndexOf("cadre", StringComparison.OrdinalIgnoreCase) >= 0;
```

`IndexOf("cadre")` est une recherche de sous-chaîne. Le libellé **"Non cadre"**
contient bien la chaîne "cadre" → le test retourne `true` → le salarié est
traité comme cadre → IPRES_RC appliqué à tort.

### Correctif

**Approche** : ajout d'un drapeau booléen explicite `EstCadre` sur l'entité
`Categories`, et bascule de la détection sur ce drapeau (avec fallback compat
ascendante sur libellé commençant par "Cadre" hors "Non").

| Fichier | Modification |
|---|---|
| `BusinessObjects/Categories.cs` | Nouveau champ `EstCadre` (bool) avec `XafDisplayName` et `ToolTip` |
| `BusinessObjects/Bulletin.cs` — `EstCadre(Salarie s)` | Lit `Categories.EstCadre` en priorité. Fallback : `Intitule.StartsWith("cadre")` ET pas de mot `\bnon\b` |
| `BusinessObjects/Bulletin.cs` — `CalculerIPRES_CSS` | Pour les non-cadres, **supprime** la ligne IPRES_RC (`Lignes.Remove(rc)` + `rc.Delete()`) au lieu de la laisser à zéro |
| `DatabaseUpdate/Updater.cs` — `EnsureCategoriesEstCadreInitialized()` | Pré-coche `EstCadre = true` pour les catégories existantes dont le libellé commence par "Cadre" (hors "Non"). Idempotent : ne décoche jamais une catégorie déjà cochée |
| `docs/deployment/sql/V181_INIT_EstCadre.sql` (NEW) | SQL one-shot de secours si l'updater n'a pas tourné, avec audit avant/après + bloc commenté pour effacer les lignes IPRES_RC mal créées |

### Comportement avant / après

| Cas | Avant V1.8.1 | Après V1.8.1 |
|---|---|---|
| Salarié catégorie "Cadre" | IPRES_RG + IPRES_RC ✓ | IPRES_RG + IPRES_RC ✓ |
| Salarié catégorie "Cadre supérieur" | IPRES_RG + IPRES_RC ✓ | IPRES_RG + IPRES_RC ✓ |
| Salarié catégorie "Non cadre" | IPRES_RG + IPRES_RC ❌ (bug) | IPRES_RG seul ✓ |
| Salarié catégorie "Employé" / "Ouvrier" | IPRES_RG seul ✓ | IPRES_RG seul ✓ |
| Bulletin existant avec ligne IPRES_RC à 0 | Ligne visible (Base=0) | Ligne **supprimée** au prochain recalcul |

### Procédure de déploiement V1.8.1

1. `dotnet build` (vérification compilation)
2. Déploiement IIS (cf. `Deploy-AdiPAIE-V18.ps1`)
3. Au premier démarrage XAF :
   - colonne `EstCadre` ajoutée automatiquement à la table `Categories`
   - Updater `EnsureCategoriesEstCadreInitialized` pré-coche les catégories
4. **Vérification RH** : Référentiels → Catégories → contrôler la colonne
   "Catégorie cadre ?" et corriger manuellement si besoin
5. (Si auto-init non effective) Exécuter `V181_INIT_EstCadre.sql`
6. **Recalculer les bulletins** des non-cadres impactés via le bouton
   "Recalculer cotisations" pour purger les lignes IPRES_RC mal créées

### Tests à valider en recette

- Salarié "Non cadre" → bulletin sans ligne IPRES_RC
- Salarié "Cadre" → bulletin avec lignes IPRES_RG + IPRES_RC correctement calculées
- Bascule manuelle d'une catégorie de `EstCadre=true` → `false` → recalcul
  bulletin → ligne IPRES_RC disparaît
- Cas limite : `Categories.Intitule = null` ou vide → pas de crash, retourne `false`

---

## 🔧 V1.8.2 — HOTFIX IR calculé avec des parts fiscales figées (22 juin 2026)

### Bug

Cas réel : **Papa Souleymane DIOP** (matricule 420023, AM3) — fiche salarié
indique `NombrePartsFiscales = 3,5` mais le bulletin janvier 2026 calcule
l'IR avec **3 parts** au lieu de 3,5.

| Élément | Ancien système | AdiPAIE V1.8.1 | Écart |
|---|---|---|---|
| Brut fiscal | 965 547 | 965 547 | 0 ✓ |
| Parts utilisées | 3,5 | **3,0** ❌ | -0,5 |
| Réduction familiale (annuelle) | 994 680 (30%) | 828 900 (25%) | -165 780 |
| Retenue IR mensuelle | 193 410 | **207 225** | **+13 815** |
| Net à payer | 643 139 | 629 324 | -13 815 |

### Cause racine

`Bulletin.CalculerIRPP()` lignes 1285-1288 (V1.8.1) :

```csharp
decimal parts = (ir.Taux.HasValue && ir.Taux.Value > 0m)
    ? ir.Taux.Value                                          // 1. existant prioritaire
    : Math.Max(1m, Salarie?.NombrePartsFiscales ?? 1m);      // 2. fallback fiche
ir.Taux = parts;
```

Le code prenait `ir.Taux` (le champ Taux de la ligne IR du bulletin) **en
priorité** s'il était déjà renseigné. Si la ligne IR avait été créée avec un
Taux figé (par un précédent calcul, un BulletinModele, un import, ou un
ajout manuel), ce Taux restait verrouillé **à jamais**, même si le RH mettait
ensuite à jour `NombrePartsFiscales` sur la fiche salarié.

Pour Papa Souleymane DIOP, la ligne IR avait été créée à 3 parts à une époque
antérieure. Le RH a corrigé la fiche à 3,5 (suite naissance / changement de
situation familiale), mais le bulletin a continué à utiliser 3 parts au
recalcul → réduction 25% au lieu de 30% → +13 815 FCFA d'IR.

### Correctif

| Fichier | Modification |
|---|---|
| `BusinessObjects/Bulletin.cs` — `CalculerIRPP()` | La fiche salarié est **toujours** la source de vérité : `parts = Math.Max(1m, Salarie?.NombrePartsFiscales ?? 1m)`. `ir.Taux` n'est plus qu'un reflet d'affichage écrasé à chaque recalcul. |

Note : `SimulationSursalaire.cs` n'est pas affecté — il lit directement
`Salarie.NombrePartsFiscales` (pas via une ligne figée).

### Comportement avant / après

| Cas | Avant V1.8.2 | Après V1.8.2 |
|---|---|---|
| RH met à jour NombrePartsFiscales sur la fiche | Bulletin existant garde l'ancienne valeur | Prochain recalcul prend la nouvelle |
| Bulletin créé à neuf | ir.Taux null → fallback fiche ✓ | ir.Taux écrasé par fiche ✓ |
| Bulletin importé avec Taux=1 | Reste à 1 part (faux) | Écrasé par fiche au 1er recalcul ✓ |
| Override manuel par l'admin | Possible (ir.Taux respecté) | Plus possible — toujours écrasé. Si besoin futur d'override, ajouter un flag `IsManual` (V1.9). |

### Procédure post-déploiement V1.8.2

1. `dotnet build` (vérification)
2. Déploiement IIS
3. **Pour tous les bulletins déjà calculés avec un Taux IR incorrect** :
   - Ouvrir le bulletin → cliquer **"Recharger bulletin"** → le calcul
     réécrit ir.Taux avec la valeur courante de NombrePartsFiscales
   - Ou en masse : audit SQL puis "Recalculer cotisations" en batch
4. **Audit SQL pour identifier les bulletins impactés** :

```sql
-- Bulletins où le Taux IR diffère du NombrePartsFiscales actuel du salarié
SELECT
    b.Annee, b.Mois, s.Matricule, s.Prenom + ' ' + s.Nom AS Salarie,
    bl.Taux AS Parts_Bulletin,
    s.NombrePartsFiscales AS Parts_Fiche,
    bl.Montant AS IR_Calcule,
    b.Oid AS Bulletin_Oid
FROM BulletinLigne bl
INNER JOIN Bulletin b ON b.Oid = bl.Bulletin
INNER JOIN Salarie s ON s.Oid = b.Salarie
INNER JOIN Rubrique r ON r.Oid = bl.Rubrique
INNER JOIN RubriqueCanonique rc ON rc.Oid = r.Canonique
WHERE bl.GCRecord IS NULL AND b.GCRecord IS NULL
  AND rc.Code = 'IRPP'  -- adapter au code canonique réel
  AND ABS(ISNULL(bl.Taux, 0) - ISNULL(s.NombrePartsFiscales, 0)) > 0.001
ORDER BY b.Annee DESC, b.Mois DESC, s.Matricule;
```

### Tests à valider en recette

- Bulletin Papa Souleymane DIOP recalculé → ir.Taux = 3,5 et IR = 193 410 ✓
- Changer NombrePartsFiscales sur une fiche → recalcul bulletin → IR mis à jour
- Bulletin nouvellement créé pour un salarié à 2 parts → IR cohérent
- Salarié sans NombrePartsFiscales défini → fallback à 1 part (pas de crash)

