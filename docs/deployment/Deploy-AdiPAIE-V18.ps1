<#
.SYNOPSIS
    Script de déploiement AdiPAIE V1.8 en production ELTON.

.DESCRIPTION
    Automatise les 7 étapes du déploiement :
      1. Vérifications préalables (chemins, IIS, BDD accessible)
      2. Sauvegarde des fichiers de config + base SQL
      3. Arrêt IIS (site + pool d'application)
      4. Copie du nouveau build (output de "dotnet publish")
      5. Restauration des fichiers de config préservés
      6. Nettoyage du cache modèle XAF utilisateur
      7. Redémarrage IIS + checklist post-déploiement affichée

.NOTES
    Version  : 1.8.0 (juin 2026)
    Auteur   : Abdoulaye DIENG — DSI ELTON
    Requiert : exécution en tant qu'Administrateur (PowerShell admin)
               + module WebAdministration (rôle "IIS Management Scripts and Tools")

.EXAMPLE
    .\Deploy-AdiPAIE-V18.ps1

    Lance le déploiement avec les valeurs par défaut (cf. section CONFIGURATION).

.EXAMPLE
    .\Deploy-AdiPAIE-V18.ps1 -SkipBackup

    Saute la sauvegarde BDD (déconseillé sauf si déjà faite manuellement).
#>

[CmdletBinding()]
param(
    # Chemin du dossier IIS de production où le site est déployé
    [string]$ProdPath = "C:\inetpub\wwwroot\AdiPAIE",

    # Chemin du build à déployer (output dotnet publish)
    [string]$PublishPath = "C:\Dev\AdiPAIE_V02\AdiPAIE_V02\AdiPAIE_V02.Blazor.Server\bin\Release\net8.0\win-x64\publish",

    # Dossier de sauvegarde (un sous-dossier horodaté sera créé)
    [string]$BackupRoot = "C:\Backup\AdiPAIE",

    # Nom du site IIS
    [string]$SiteName = "AdiPAIE",

    # Nom du pool d'application IIS
    [string]$AppPoolName = "AdiPAIE",

    # Nom du serveur SQL (-S pour sqlcmd)
    [string]$SqlServer = "grh",

    # Nom de la base SQL à sauvegarder
    [string]$DatabaseName = "SunuPaie_Prod",

    # Sauter la sauvegarde BDD (déconseillé)
    [switch]$SkipBackup,

    # Mode "dry-run" : affiche les actions sans les exécuter
    [switch]$DryRun
)

# ──────────────────────────────────────────────────────────────────────────
# CONFIGURATION INTERNE (ne pas modifier sans raison)
# ──────────────────────────────────────────────────────────────────────────

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyy-MM-dd_HHmm"
$backupPath = Join-Path $BackupRoot "Deploy_V18_$timestamp"

# Fichiers à PRÉSERVER (sauvegarder + restaurer après copie du publish)
$filesToPreserve = @(
    "dbconfig.json",
    "appsettings.json",
    "web.config"
)

# Dossiers utilisateur à conserver (uploads, archives)
$foldersToPreserve = @(
    "App_Data"
)

# Fichiers à SUPPRIMER après déploiement (caches modèle XAF utilisateur)
$filesToCleanup = @(
    "Model.User.xafml"
)

# ──────────────────────────────────────────────────────────────────────────
# HELPERS
# ──────────────────────────────────────────────────────────────────────────

function Write-Step {
    param([string]$Message, [string]$Color = "Cyan")
    $ts = Get-Date -Format "HH:mm:ss"
    Write-Host ""
    Write-Host "[$ts] $Message" -ForegroundColor $Color
    Write-Host ("─" * 70) -ForegroundColor DarkGray
}

function Write-Ok {
    param([string]$Message)
    $ts = Get-Date -Format "HH:mm:ss"
    Write-Host "[$ts] OK  $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    $ts = Get-Date -Format "HH:mm:ss"
    Write-Host "[$ts] ATT $Message" -ForegroundColor Yellow
}

function Invoke-OrDry {
    param([scriptblock]$Action, [string]$Description)
    if ($DryRun) {
        Write-Host "  [DRY] $Description" -ForegroundColor Magenta
    } else {
        & $Action
        Write-Ok $Description
    }
}

# ──────────────────────────────────────────────────────────────────────────
# DÉBUT DU DÉPLOIEMENT
# ──────────────────────────────────────────────────────────────────────────

Clear-Host
Write-Host @"
╔══════════════════════════════════════════════════════════════════════╗
║         DÉPLOIEMENT AdiPAIE V1.8 — Production ELTON                  ║
║         $(Get-Date -Format 'dddd dd MMMM yyyy HH:mm:ss')                              ║
╚══════════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

Write-Host ""
Write-Host "  Site IIS           : $SiteName"
Write-Host "  App Pool           : $AppPoolName"
Write-Host "  Dossier production : $ProdPath"
Write-Host "  Source publish     : $PublishPath"
Write-Host "  Backup destination : $backupPath"
Write-Host "  BDD à sauvegarder  : $DatabaseName  (sur $SqlServer)"
if ($DryRun) { Write-Warn "Mode DRY-RUN : aucune action ne sera exécutée." }
Write-Host ""

# ─── ÉTAPE 1 : Vérifications préalables ─────────────────────────────────
Write-Step "ÉTAPE 1/7 — Vérifications préalables"

if (-not (Test-Path $ProdPath)) {
    throw "Dossier de production introuvable : $ProdPath"
}
Write-Ok "Dossier production trouvé : $ProdPath"

if (-not (Test-Path $PublishPath)) {
    throw "Dossier publish introuvable : $PublishPath. Lancez d'abord : dotnet publish -c Release -r win-x64"
}
Write-Ok "Build publish trouvé : $PublishPath"

# Module WebAdministration
try {
    Import-Module WebAdministration -ErrorAction Stop
    Write-Ok "Module WebAdministration chargé"
} catch {
    throw "Module WebAdministration introuvable. Installer 'IIS Management Scripts and Tools' via Server Manager."
}

# Site IIS existe
if (-not (Get-Website -Name $SiteName -ErrorAction SilentlyContinue)) {
    throw "Site IIS '$SiteName' introuvable. Vérifier le nom."
}
Write-Ok "Site IIS '$SiteName' OK"

# Pool IIS existe
if (-not (Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue)) {
    throw "App Pool '$AppPoolName' introuvable."
}
Write-Ok "App Pool '$AppPoolName' OK"

# Vérification fichiers à préserver présents
foreach ($f in $filesToPreserve) {
    $p = Join-Path $ProdPath $f
    if (-not (Test-Path $p)) {
        Write-Warn "Fichier à préserver introuvable : $f (sera ignoré)"
    }
}

# ─── ÉTAPE 2 : Sauvegardes ──────────────────────────────────────────────
Write-Step "ÉTAPE 2/7 — Sauvegardes (config + BDD)"

Invoke-OrDry -Action {
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
} -Description "Dossier de sauvegarde créé : $backupPath"

# Sauvegarde fichiers config
foreach ($f in $filesToPreserve) {
    $src = Join-Path $ProdPath $f
    if (Test-Path $src) {
        Invoke-OrDry -Action {
            Copy-Item $src $backupPath -Force
        } -Description "Sauvegardé : $f"
    }
}

# Sauvegarde dossiers utilisateur
foreach ($d in $foldersToPreserve) {
    $src = Join-Path $ProdPath $d
    if (Test-Path $src) {
        Invoke-OrDry -Action {
            Copy-Item $src (Join-Path $backupPath $d) -Recurse -Force
        } -Description "Sauvegardé (dossier) : $d"
    }
}

# Sauvegarde BDD
if (-not $SkipBackup) {
    $bakFile = Join-Path $backupPath "BDD_AVANT_V18_$timestamp.bak"
    $bakSql = "BACKUP DATABASE [$DatabaseName] TO DISK = N'$bakFile' WITH COMPRESSION, INIT, FORMAT, NAME = N'AdiPAIE V1.8 - Backup avant déploiement';"

    Invoke-OrDry -Action {
        $output = & sqlcmd -S $SqlServer -E -Q $bakSql 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Erreur sauvegarde BDD : $output"
        }
    } -Description "BDD '$DatabaseName' sauvegardée dans $bakFile"
} else {
    Write-Warn "SkipBackup : sauvegarde BDD sautée (à faire manuellement !)"
}

# ─── ÉTAPE 3 : Arrêt IIS ─────────────────────────────────────────────────
Write-Step "ÉTAPE 3/7 — Arrêt IIS"

Invoke-OrDry -Action {
    Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
} -Description "Site '$SiteName' arrêté"

Invoke-OrDry -Action {
    Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    # Attente que le pool soit complètement arrêté (libère les .dll)
    Start-Sleep -Seconds 5
} -Description "App Pool '$AppPoolName' arrêté (+ 5s pour libération des DLL)"

# ─── ÉTAPE 4 : Déploiement du nouveau build ─────────────────────────────
Write-Step "ÉTAPE 4/7 — Copie du nouveau build"

Invoke-OrDry -Action {
    # Copie complète du publish dans le dossier prod (écrase les anciens fichiers)
    Copy-Item -Path (Join-Path $PublishPath "*") -Destination $ProdPath -Recurse -Force
} -Description "Nouveau build copié dans $ProdPath"

# ─── ÉTAPE 5 : Restauration des fichiers préservés ──────────────────────
Write-Step "ÉTAPE 5/7 — Restauration des fichiers préservés"

foreach ($f in $filesToPreserve) {
    $src = Join-Path $backupPath $f
    $dst = Join-Path $ProdPath $f
    if (Test-Path $src) {
        Invoke-OrDry -Action {
            Copy-Item $src $dst -Force
        } -Description "Restauré : $f (écrase la version du publish)"
    }
}

foreach ($d in $foldersToPreserve) {
    $src = Join-Path $backupPath $d
    $dst = Join-Path $ProdPath $d
    if (Test-Path $src) {
        Invoke-OrDry -Action {
            Copy-Item $src $dst -Recurse -Force
        } -Description "Restauré (dossier) : $d"
    }
}

# ─── ÉTAPE 6 : Nettoyage cache modèle XAF ───────────────────────────────
Write-Step "ÉTAPE 6/7 — Nettoyage cache modèle XAF utilisateur"

foreach ($f in $filesToCleanup) {
    $p = Join-Path $ProdPath $f
    if (Test-Path $p) {
        Invoke-OrDry -Action {
            Remove-Item $p -Force
        } -Description "Supprimé : $f (force régénération layout au prochain démarrage)"
    } else {
        Write-Host "  Pas présent (OK) : $f" -ForegroundColor DarkGray
    }
}

# ─── ÉTAPE 7 : Redémarrage IIS ──────────────────────────────────────────
Write-Step "ÉTAPE 7/7 — Redémarrage IIS"

Invoke-OrDry -Action {
    Start-WebAppPool -Name $AppPoolName
} -Description "App Pool '$AppPoolName' démarré"

Invoke-OrDry -Action {
    Start-Website -Name $SiteName
} -Description "Site '$SiteName' démarré"

# ──────────────────────────────────────────────────────────────────────────
# CHECKLIST POST-DÉPLOIEMENT
# ──────────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host @"
╔══════════════════════════════════════════════════════════════════════╗
║  ✅ DÉPLOIEMENT TERMINÉ                                              ║
╚══════════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Green

Write-Host ""
Write-Host "📋 Checklist post-déploiement V1.8 — à vérifier manuellement :" -ForegroundColor Yellow
Write-Host ""
Write-Host "  [ ]  1. Login sur https://grh/AdiPAIE OK (chaîne de connexion préservée)"
Write-Host "  [ ]  2. ParametresPaie → onglet Fiscalité → 'CFCE – Mode base de calcul' visible"
Write-Host "  [ ]  3. ParametresPaie → onglet Référentiel paie → 'Mode bulletin de congé' visible"
Write-Host "  [ ]  4. ParametresPaie → onglet Fiscalité → 'IR – IMAB' affiche 30 (et non 3 000 %)"
Write-Host "  [ ]  5. Cliquer 'Recharger le référentiel paie' → vérifier création rubrique ICCP"
Write-Host "  [ ]  6. Fiche Salarié → 2 nouveaux boutons : 'Saisir solde initial' + 'Saisir un congé'"
Write-Host "  [ ]  7. Aide → ouvrir Congés/Prêts/Paramétrage/Offboarding → encadrés orange V1.8 visibles"
Write-Host "  [ ]  8. Test création d'un congé sur 1 salarié → vérifier calcul correct"
Write-Host ""
Write-Host "📁 Sauvegarde : $backupPath" -ForegroundColor DarkGray
Write-Host ""

if ($DryRun) {
    Write-Warn "Mode DRY-RUN actif : aucune action n'a été exécutée. Relancer sans -DryRun pour déployer réellement."
}

# ──────────────────────────────────────────────────────────────────────────
# PROCÉDURE DE ROLLBACK (en cas de problème)
# ──────────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "🔄 EN CAS DE PROBLÈME — Procédure de rollback :" -ForegroundColor Magenta
Write-Host ""
Write-Host "  1. Arrêter IIS :"
Write-Host "       Stop-Website -Name '$SiteName'; Stop-WebAppPool -Name '$AppPoolName'"
Write-Host ""
Write-Host "  2. Restaurer le dossier complet depuis votre dernière sauvegarde V1.7.x"
Write-Host "     (à conserver avant ce déploiement) :"
Write-Host "       Remove-Item '$ProdPath\*' -Recurse -Force"
Write-Host "       Copy-Item 'C:\Backup\AdiPAIE_V17\*' '$ProdPath\' -Recurse"
Write-Host ""
Write-Host "  3. Restaurer la BDD :"
Write-Host "       sqlcmd -S $SqlServer -E -Q \"USE master; ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$DatabaseName] FROM DISK = N'$backupPath\BDD_AVANT_V18_$timestamp.bak' WITH REPLACE; ALTER DATABASE [$DatabaseName] SET MULTI_USER;\""
Write-Host ""
Write-Host "  4. Redémarrer IIS :"
Write-Host "       Start-WebAppPool -Name '$AppPoolName'; Start-Website -Name '$SiteName'"
Write-Host ""
