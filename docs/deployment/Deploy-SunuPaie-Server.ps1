# ============================================================================
#  Deploy-SunuPaie-Server.ps1 - V1.10
#
#  Deploiement AdiPAIE (SunuPaie) sur serveur IIS Windows.
#  Execute directement sur le serveur de prod, en administrateur.
#
#  Sequence :
#    1. Verifications preflight (zip present, dossier cible, IIS)
#    2. Arret IIS (site + app pool)
#    3. Backup rapide de config et dossier logs
#    4. Suppression du contenu sauf : logs\, appsettings*.json, dbconfig*, web.config
#    5. Extraction du zip
#    6. Restauration des fichiers preserves si ecrasement
#    7. Redemarrage IIS
#    8. Test HTTP de sante
#
#  Parametres (avec valeurs par defaut) :
#    -ZipPath      Chemin du zip source (defaut: %USERPROFILE%\Downloads\sunuPaie.zip)
#    -TargetPath   Dossier cible IIS (defaut: C:\inetpub\wwwroot\SunuPaie)
#    -SiteName     Nom du site IIS (defaut: SunuPaie)
#    -AppPoolName  Nom de l'app pool IIS (defaut: SunuPaiePool)
#    -DryRun       Simuler sans rien modifier
#    -SkipBackup   Ne pas faire de backup (deconseille)
#    -HealthUrl    URL a tester apres redemarrage (defaut: https://localhost/)
#
#  Exemples :
#    .\Deploy-SunuPaie-Server.ps1 -DryRun
#    .\Deploy-SunuPaie-Server.ps1
#    .\Deploy-SunuPaie-Server.ps1 -ZipPath "D:\builds\sunuPaie-v1.10.zip"
# ============================================================================

[CmdletBinding()]
param(
    [string]$ZipPath      = "$env:USERPROFILE\Downloads\sunuPaie.zip",
    [string]$TargetPath   = "C:\inetpub\wwwroot\SunuPaie",
    [string]$SiteName     = "SunuPaie",
    [string]$AppPoolName  = "SunuPaiePool",
    [string]$HealthUrl    = "https://localhost/",
    [switch]$DryRun,
    [switch]$SkipBackup
)

$ErrorActionPreference = "Stop"
$script:StartTime = Get-Date

# ============================================================================
#  Utilitaires
# ============================================================================
function Write-Step {
    param([string]$Message)
    $ts = Get-Date -Format "HH:mm:ss"
    Write-Host ""
    Write-Host "[$ts] === $Message ===" -ForegroundColor Cyan
}
function Write-Info { param([string]$m) Write-Host "  [INFO] $m" -ForegroundColor Gray }
function Write-Ok   { param([string]$m) Write-Host "  [OK]   $m" -ForegroundColor Green }
function Write-Warn { param([string]$m) Write-Host "  [WARN] $m" -ForegroundColor Yellow }
function Write-Err  { param([string]$m) Write-Host "  [ERR]  $m" -ForegroundColor Red }

function Assert-Admin {
    $current = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($current)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Ce script doit etre lance en tant qu'administrateur (clic droit -> Run as admin)."
    }
}

# Fichiers/dossiers a preserver dans le dossier cible
$PreservePatterns = @(
    "logs",              # dossier
    "appsettings.json",
    "appsettings.*.json",
    "dbconfig*",
    "web.config"
)

function Test-Preserved {
    param([string]$RelativePath)
    foreach ($pat in $PreservePatterns) {
        if ($RelativePath -like $pat) { return $true }
    }
    return $false
}

# ============================================================================
#  1. Preflight
# ============================================================================
Write-Step "1/7  Verifications preflight"

if ($DryRun) { Write-Warn "MODE DRY-RUN active : aucune modification ne sera effectuee" }

Assert-Admin
Write-Ok "Execution en administrateur"

Write-Info "ZipPath      = $ZipPath"
Write-Info "TargetPath   = $TargetPath"
Write-Info "SiteName     = $SiteName"
Write-Info "AppPoolName  = $AppPoolName"
Write-Info "HealthUrl    = $HealthUrl"

if (-not (Test-Path $ZipPath)) {
    throw "Zip introuvable : $ZipPath"
}
$zipInfo = Get-Item $ZipPath
Write-Ok ("Zip trouve : {0:N0} octets, modifie le {1}" -f $zipInfo.Length, $zipInfo.LastWriteTime)

if (-not (Test-Path $TargetPath)) {
    throw "Dossier cible introuvable : $TargetPath"
}
Write-Ok "Dossier cible existe"

try {
    Import-Module WebAdministration -ErrorAction Stop
    Write-Ok "Module WebAdministration charge"
} catch {
    throw "Module WebAdministration indisponible. IIS est-il installe ?"
}

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    throw "Site IIS introuvable : $SiteName"
}
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    throw "AppPool IIS introuvable : $AppPoolName"
}
Write-Ok "Site et AppPool IIS trouves"

# ============================================================================
#  2. Arret IIS
# ============================================================================
Write-Step "2/7  Arret IIS"

if (-not $DryRun) {
    try {
        Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
        Write-Ok "Site $SiteName arrete"
    } catch { Write-Warn "Site $SiteName deja arrete" }

    try {
        Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
        Write-Ok "AppPool $AppPoolName arrete"
    } catch { Write-Warn "AppPool $AppPoolName deja arrete" }

    # Attendre que les fichiers soient liberes
    Start-Sleep -Seconds 3
    Write-Info "Attente 3s pour liberation des fichiers"
} else {
    Write-Info "(dry-run) Aurait execute Stop-Website + Stop-WebAppPool"
}

# ============================================================================
#  3. Backup
# ============================================================================
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = Join-Path (Split-Path $TargetPath) "_backup_$stamp"

if ($SkipBackup) {
    Write-Step "3/7  Backup"
    Write-Warn "Backup ignore (parametre -SkipBackup)"
} else {
    Write-Step "3/7  Backup config + logs -> $backupRoot"

    if (-not $DryRun) {
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null

        # Copier fichiers preserves + dossier logs vers le backup
        foreach ($pat in $PreservePatterns) {
            $items = Get-ChildItem -Path $TargetPath -Filter $pat -ErrorAction SilentlyContinue
            foreach ($it in $items) {
                if ($it.PSIsContainer) {
                    Copy-Item $it.FullName -Destination $backupRoot -Recurse -Force -ErrorAction SilentlyContinue
                } else {
                    Copy-Item $it.FullName -Destination $backupRoot -Force -ErrorAction SilentlyContinue
                }
                Write-Info "Backup : $($it.Name)"
            }
        }
        Write-Ok "Backup effectue dans $backupRoot"
    } else {
        Write-Info "(dry-run) Aurait cree $backupRoot avec logs + appsettings + web.config"
    }
}

# ============================================================================
#  4. Suppression (sauf preserves)
# ============================================================================
Write-Step "4/7  Suppression contenu (sauf preserves)"

$nbSupprimes = 0
Get-ChildItem -Path $TargetPath -Force | ForEach-Object {
    if (Test-Preserved $_.Name) {
        Write-Info "Preserve : $($_.Name)"
        return
    }
    if (-not $DryRun) {
        try {
            if ($_.PSIsContainer) {
                Remove-Item $_.FullName -Recurse -Force -ErrorAction Stop
            } else {
                Remove-Item $_.FullName -Force -ErrorAction Stop
            }
            $nbSupprimes++
        } catch {
            Write-Warn "Impossible de supprimer $($_.Name) : $($_.Exception.Message)"
        }
    } else {
        $nbSupprimes++
        Write-Info "(dry-run) Aurait supprime : $($_.Name)"
    }
}
Write-Ok "$nbSupprimes elements supprimes (sauf preserves)"

# ============================================================================
#  5. Extraction du zip
# ============================================================================
Write-Step "5/7  Extraction du zip dans $TargetPath"

if (-not $DryRun) {
    try {
        # Expand-Archive n'ecrase pas par defaut, on force
        Expand-Archive -Path $ZipPath -DestinationPath $TargetPath -Force
        Write-Ok "Zip extrait"
    } catch {
        throw "Echec extraction : $($_.Exception.Message)"
    }

    # ------------------------------------------------------------------------
    # Detection d'un dossier racine imbrique (cas ou le zip contient un dossier
    # portant le meme nom que le dossier cible, ex: SunuPaie.zip -> SunuPaie/...
    # extrait dans C:\inetpub\wwwroot\SunuPaie donne SunuPaie\SunuPaie\...)
    # On aplati automatiquement en remontant le contenu d'un niveau.
    # ------------------------------------------------------------------------
    $targetLeaf = Split-Path $TargetPath -Leaf
    $nested = Join-Path $TargetPath $targetLeaf
    if (Test-Path $nested -PathType Container) {
        Write-Warn "Dossier imbrique detecte : $nested"
        Write-Info "Aplatissement en cours (remontee du contenu d'un niveau)..."
        Get-ChildItem -Path $nested -Force | ForEach-Object {
            $dest = Join-Path $TargetPath $_.Name
            # Si le nom existe deja dans target (ex: preserve), on ecrase
            if (Test-Path $dest) {
                if ($_.PSIsContainer) {
                    Copy-Item $_.FullName -Destination $TargetPath -Recurse -Force
                    Remove-Item $_.FullName -Recurse -Force
                } else {
                    Move-Item $_.FullName -Destination $dest -Force
                }
            } else {
                Move-Item $_.FullName -Destination $TargetPath -Force
            }
        }
        Remove-Item -Path $nested -Recurse -Force -ErrorAction SilentlyContinue
        Write-Ok "Structure aplatie (contenu remonte a la racine de $TargetPath)"
    }
} else {
    Write-Info "(dry-run) Aurait execute Expand-Archive + detection dossier imbrique"
}

# ============================================================================
#  6. Restauration des fichiers preserves (au cas ou le zip les a ecrase)
# ============================================================================
Write-Step "6/7  Restauration fichiers preserves depuis backup"

if (-not $SkipBackup -and (Test-Path $backupRoot) -and -not $DryRun) {
    $nbRestaures = 0
    Get-ChildItem -Path $backupRoot -Force | ForEach-Object {
        $dest = Join-Path $TargetPath $_.Name
        try {
            if ($_.PSIsContainer) {
                # Dossier logs : merge non destructif
                Copy-Item $_.FullName -Destination $TargetPath -Recurse -Force
            } else {
                # Fichier config : ecraser ce que le zip a mis
                Copy-Item $_.FullName -Destination $dest -Force
            }
            $nbRestaures++
            Write-Info "Restaure : $($_.Name)"
        } catch {
            Write-Warn "Echec restauration $($_.Name) : $($_.Exception.Message)"
        }
    }
    Write-Ok "$nbRestaures fichiers/dossiers preserves restaures"
} elseif ($DryRun) {
    Write-Info "(dry-run) Aurait restaure appsettings + web.config + logs depuis $backupRoot"
} else {
    Write-Warn "Pas de backup a restaurer"
}

# ============================================================================
#  7. Redemarrage IIS
# ============================================================================
Write-Step "7/7  Redemarrage IIS"

if (-not $DryRun) {
    try {
        Start-WebAppPool -Name $AppPoolName
        Write-Ok "AppPool $AppPoolName redemarre"
    } catch { throw "Echec Start-WebAppPool : $($_.Exception.Message)" }

    try {
        Start-Website -Name $SiteName
        Write-Ok "Site $SiteName redemarre"
    } catch { throw "Echec Start-Website : $($_.Exception.Message)" }

    Write-Info "Attente 15s pour warmup"
    Start-Sleep -Seconds 15

    # Test HTTP
    Write-Info "Test HTTP : $HealthUrl"
    try {
        $resp = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 30 `
            -SkipCertificateCheck -ErrorAction Stop
        if ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 302) {
            Write-Ok "HTTP $($resp.StatusCode) - site repond"
        } else {
            Write-Warn "HTTP $($resp.StatusCode) - a verifier manuellement"
        }
    } catch {
        Write-Warn "Test HTTP a echoue : $($_.Exception.Message)"
        Write-Warn "Verifier manuellement dans un navigateur : $HealthUrl"
    }
} else {
    Write-Info "(dry-run) Aurait execute Start-WebAppPool + Start-Website + test HTTP"
}

# ============================================================================
#  Fin
# ============================================================================
$duree = (Get-Date) - $script:StartTime
Write-Host ""
Write-Host "========================================================================" -ForegroundColor Green
if ($DryRun) {
    Write-Host " SIMULATION TERMINEE en $([int]$duree.TotalSeconds)s (rien n'a ete modifie)" -ForegroundColor Yellow
} else {
    Write-Host " DEPLOIEMENT TERMINE en $([int]$duree.TotalSeconds)s" -ForegroundColor Green
    if (-not $SkipBackup) {
        Write-Host " Backup dispo : $backupRoot" -ForegroundColor Gray
    }
}
Write-Host "========================================================================" -ForegroundColor Green
Write-Host ""

