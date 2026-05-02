# CHANGELOG — Module « Tableaux de Bord RH »

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
- **Hash** : _à renseigner après le `git commit` côté Windows_
- **Message attendu** :
  `feat(dashboards): architecture cible — composants partagés, rôle RH_Manager, entrée menu XAF`

### Commandes de rollback

```
git revert <hash_du_commit_etape_2>
# ou (en local non poussé) :
git reset --hard cb0e1c7c9836dc1c3a6bff386121086a4f77b807
```

### Validé par

_À renseigner — validation en cours côté utilisateur._

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
