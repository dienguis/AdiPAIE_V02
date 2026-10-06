# Migration Cowork -> Claude Code

Guide pratique pour Abdoulaye - migration de Cowork (abandonne le 6 octobre 2026)
vers Claude Code, adapte au contexte multi-projets .NET/XAF Windows.

---

## 1. Pourquoi migrer

**Cowork** (le panneau dans l'app Claude Desktop) est abandonne par Anthropic
le 6 octobre 2026. Toutes les nouvelles taches "sur ordinateur" passent par
**Claude Code**, un CLI qui fait la meme chose mais dans un terminal.

Aucune perte de fonctionnalite :
- Meme modele Claude (Sonnet 4.7, Opus, etc.)
- Meme capacite de lecture/ecriture fichiers
- Meme execution PowerShell / Bash
- Captures d'ecran supportees (via Alt+V dans Windows Terminal)
- Historique de conversation persistant

En plus, Claude Code apporte : skills reutilisables, sous-agents, integration
VS Code, plugins, meilleur support Git natif.

---

## 2. Installation (une fois)

### Etape 1 - Node.js LTS

Prerequis pour npm.

- Telecharger : https://nodejs.org/ (bouton **LTS** - version 20 ou +)
- Installer avec les options par defaut (garder "Add to PATH")
- **Fermer et rouvrir PowerShell** (pour prise en compte du PATH)

Verification :
```powershell
node --version   # doit afficher v20.x ou +
npm --version    # doit afficher 10.x ou +
```

### Etape 2 - Windows Terminal (recommande)

Meilleur support des couleurs, du copier-coller image, des onglets.

```powershell
winget install Microsoft.WindowsTerminal
```

Ou depuis Microsoft Store.

### Etape 3 - Claude Code

```powershell
npm install -g @anthropic-ai/claude-code
```

Verification :
```powershell
claude --version
```

### Etape 4 - Authentification

Au premier lancement :
```powershell
claude
```

Le navigateur s'ouvre, tu te connectes avec ton compte Anthropic. Une fois
la connexion faite, tu peux fermer le navigateur.

---

## 3. Utilisation quotidienne

### Ouvrir un projet

Depuis n'importe quel projet, dans PowerShell :

```powershell
cd C:\Dev\AdiPAIE_V02   # ou POS, TicketFlow, KEUR_DIARRA, etc.
claude
```

Claude Code s'ouvre avec le dossier courant comme contexte de travail.

### Structure d'un prompt type

Comme dans Cowork - langage naturel :

```
Regarde le bug affiche dans cette capture [Alt+V pour coller]
et corrige-moi le calcul IR dans Bulletin.cs
```

Claude Code lit les fichiers necessaires, propose un plan, applique les
modifs. Meme pattern qu'en Cowork.

### Raccourcis clavier Windows Terminal

| Action | Raccourci |
|---|---|
| Coller une **image** (capture Win+Shift+S) | **Alt+V** |
| Coller du **texte** | Ctrl+V |
| Nouvelle ligne dans le prompt | Shift+Enter |
| Envoyer le message | Enter |
| Interrompre Claude en cours | Ctrl+C (1 fois) |
| Quitter | Ctrl+C x2 ou `/exit` |

### Commandes utiles (slash commands)

Dans le chat Claude Code :

| Commande | Effet |
|---|---|
| `/help` | Liste toutes les commandes |
| `/clear` | Efface l'historique de la session |
| `/init` | Cree un fichier CLAUDE.md dans le projet (recommande) |
| `/model` | Change de modele (Sonnet / Opus / Haiku) |
| `/cost` | Affiche la consommation de tokens de la session |
| `/exit` | Ferme Claude Code |

---

## 4. Reprise de contexte projet - le fichier CLAUDE.md

**Cle de la migration** : Claude Code lit automatiquement le fichier
`CLAUDE.md` a la racine du projet au demarrage. Il sert de memoire persistante
au projet.

### Creer un CLAUDE.md pour chaque projet

Dans chaque dossier projet, au 1er lancement :

```powershell
cd C:\Dev\AdiPAIE_V02
claude
# Puis dans Claude Code :
> /init
```

`/init` scanne le projet et cree un CLAUDE.md initial avec :
- Structure du projet
- Technos utilisees
- Conventions de code observees
- Commandes usuelles (build, test, deploy)

Tu peux ensuite l'editer pour ajouter du contexte specifique.

### Template CLAUDE.md pour AdiPAIE

```markdown
# Projet AdiPAIE_V02 (SunuPaie)

Application de paie XAF/DevExpress pour ELTON Oil Senegal.

## Contexte
- Voir docs/dashboards/MISSION_STATE.md pour l'historique complet des versions
  (actuellement en V1.10)
- Le fichier COMMIT_V19.txt dans docs/deployment/ contient le dernier commit
  consolide

## Stack
- .NET 8 / DevExpress XAF 25.1.10 / XPO
- Blazor Server + IIS
- SQL Server (base SunuPaie_Prod)

## Conventions
- Commentaires en francais
- Pas d'em-dash, en-dash ou box-drawing (que du ASCII)
- Nom de branche : `dev`
- Deploiement prod via docs/deployment/Deploy-SunuPaie-Server.ps1

## Commandes usuelles
- `dotnet build` : build complet
- `dotnet run` : lance en dev (depuis AdiPAIE_V02.Blazor.Server)
- Deploy prod : script PowerShell sur le serveur

## Fichiers cles
- MISSION_STATE.md : historique complet
- docs/deployment/ : scripts et SQL de deploiement
- AdiPAIE_V02.Module/BusinessObjects/Bulletin.cs : moteur de paie
- AdiPAIE_V02.Module/DatabaseUpdate/Updater.cs : migrations XAF

## En cours
- V1.10 en cours de deploiement prod (import 747 interimaires, sortie
  interimaire, controle d'integrite, etc.)
```

### Template CLAUDE.md pour POS / TicketFlow / KEUR_DIARRA

Cree le meme pattern pour chaque projet - Claude Code aura le contexte
immediat au demarrage.

---

## 5. Workflow recommande - VS Code + Claude Code

Meilleure experience : VS Code ouvert avec le projet, terminal integre pour
Claude Code.

### Setup

```powershell
# Installer l'extension Claude Code pour VS Code (optionnel mais confortable)
# Recherche "Claude Code" dans le marketplace VS Code

# Ouvrir un projet
code C:\Dev\AdiPAIE_V02

# Dans VS Code : Ctrl+` (backtick) pour ouvrir le terminal integre
# Puis :
claude
```

### Avantages

- Voir les fichiers modifies en live (VS Code affiche les diffs)
- Coller les images depuis le presse-papier avec Alt+V
- Git integre pour committer facilement
- Extensions .NET (C# Dev Kit, DevExpress) restent disponibles

---

## 6. Gestion multi-projets

Tu as plusieurs projets : AdiPAIE_V02, POS, TicketFlow, KEUR_DIARRA.
Chacun aura son propre `CLAUDE.md` a la racine.

### Basculer entre projets

Ferme Claude Code (`/exit`), change de dossier, relance :

```powershell
cd C:\Dev\POS
claude          # Claude lit le CLAUDE.md de POS

# Plus tard
cd C:\Dev\TicketFlow
claude          # Claude lit le CLAUDE.md de TicketFlow
```

Chaque session est isolee - Claude Code n'a pas de "memoire globale" entre
projets. Tout le contexte est dans le CLAUDE.md du projet + les fichiers
lus pendant la session.

### Sessions parallelles

Tu peux ouvrir **plusieurs terminaux** en meme temps, chacun avec un projet
different. Windows Terminal supporte les onglets - tres pratique pour
switcher.

---

## 7. Gestion de la memoire longue - MISSION_STATE.md

Ta convention actuelle (le MISSION_STATE.md qui trace toutes les versions
V1.0 -> V1.10) est **exactement ce qu'il faut** pour Claude Code. Continue
comme ca :

- Nouvelle version -> ajouter section dans MISSION_STATE.md
- Nouveau bug fixe -> documenter le fix
- Nouvelle decision metier -> tracer avec date et contexte

Au demarrage de chaque session Claude Code, ma premiere commande sera
souvent :

```
Lis docs/dashboards/MISSION_STATE.md pour avoir le contexte a jour
```

Ca me remet en memoire tout l'historique du projet - identique a ce qu'on
fait en Cowork.

---

## 8. Captures d'ecran - workflow complet

Le workflow reste identique a Cowork :

1. Windows key + Shift + S : outil capture (rectangle)
2. Selectionner la zone (bug, popup, erreur)
3. L'image va dans le presse-papier automatiquement
4. Dans Claude Code (Windows Terminal ouvert) : Alt+V
5. Ajouter une description : "j'ai ce bug quand je clique sur..."
6. Enter

Claude lit l'image, analyse, propose un fix.

---

## 9. Sauvegarde de session

Contrairement a Cowork, les sessions Claude Code sont stockees **localement**
dans ton profil Windows (pas sur les serveurs Anthropic).

Emplacement : `%USERPROFILE%\.claude\projects\<hash-du-projet>\`

Tu peux :
- Consulter l'historique d'une session precedente
- Reprendre une session avec `claude --continue` ou `claude -c`
- Lister les sessions avec `claude --list-sessions`

---

## 10. Aide-memoire pour la migration

### Avant le 6 octobre

- [ ] Installer Node.js LTS
- [ ] Installer Windows Terminal (si pas deja fait)
- [ ] Installer Claude Code : `npm install -g @anthropic-ai/claude-code`
- [ ] Se connecter : `claude` (auth navigateur)
- [ ] Tester avec un petit bug pour valider le workflow
- [ ] Creer un CLAUDE.md dans chaque projet actif :
  - AdiPAIE_V02
  - POS
  - TicketFlow
  - KEUR_DIARRA
- [ ] Bookmarker cette doc pour reference

### Apres le 6 octobre

- Les taches Cowork actives continuent jusqu'a leur cloture
- Aucune nouvelle tache Cowork possible
- Tout se passe dans Claude Code

---

## 11. Ressources officielles

- **Documentation** : https://docs.claude.com/en/docs/claude-code
- **Quickstart** : https://docs.claude.com/en/docs/claude-code/quickstart
- **CLAUDE.md guide** : https://docs.claude.com/en/docs/claude-code/memory
- **Skills** : https://docs.claude.com/en/docs/claude-code/skills
- **Extension VS Code** : marketplace VS Code, chercher "Claude Code"

---

## 12. Notes personnelles

*(A completer au fur et a mesure de ton usage)*

- Raccourcis qui marchent chez moi :
  - Alt+V : coller image (Windows Terminal)
  - Ctrl+V : coller texte
- Astuces decouvertes :
  -
- Commandes personnalisees :
  -
- Problemes rencontres :
  -

---

*Document redige le 30 septembre 2026 - migration Cowork -> Claude Code*
*Auteur : session Cowork finale, projet AdiPAIE_V02*
