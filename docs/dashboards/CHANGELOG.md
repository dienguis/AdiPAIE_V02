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

- **Hash** : _à renseigner après le `git commit` côté Windows_
- **Message attendu** :
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
