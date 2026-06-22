# ============================================================================
# Commit-V18.ps1  —  Commit Git consolidé V1.8
# ============================================================================
# Usage :
#   .\Commit-V18.ps1                # commit + push origin main
#   .\Commit-V18.ps1 -DryRun        # affiche la commande sans l'exécuter
#   .\Commit-V18.ps1 -NoPush        # commit seulement, pas de push
# ============================================================================

param(
    [switch]$DryRun,
    [switch]$NoPush
)

$ErrorActionPreference = "Stop"

$repoRoot       = "C:\Dev\AdiPAIE_V02"
$commitMsgFile  = Join-Path $repoRoot "docs\deployment\COMMIT_V18.txt"

if (-not (Test-Path $commitMsgFile)) {
    Write-Error "Fichier de message introuvable : $commitMsgFile"
    exit 1
}

Set-Location $repoRoot

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "   Commit V1.8 — Refonte calcul congés + ICCP + RBAC" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# 1. État Git
Write-Host "[1/4] Statut Git actuel..." -ForegroundColor Yellow
git status --short

Write-Host ""
Write-Host "[2/4] Branche courante..." -ForegroundColor Yellow
$branch = git rev-parse --abbrev-ref HEAD
Write-Host "    Branche : $branch"

if ($DryRun) {
    Write-Host ""
    Write-Host "[DRY-RUN] Commandes qui seraient exécutées :" -ForegroundColor Magenta
    Write-Host "    git add ."
    Write-Host "    git commit -F `"$commitMsgFile`""
    if (-not $NoPush) {
        Write-Host "    git push origin $branch"
    }
    Write-Host ""
    Write-Host "Aperçu du message de commit (50 premières lignes) :" -ForegroundColor Magenta
    Get-Content $commitMsgFile -TotalCount 50
    Write-Host "..."
    exit 0
}

# 3. Add + Commit
Write-Host ""
Write-Host "[3/4] git add . + git commit -F COMMIT_V18.txt..." -ForegroundColor Yellow
git add .
git commit -F "$commitMsgFile"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Échec du commit"
    exit 1
}

# 4. Push
if (-not $NoPush) {
    Write-Host ""
    Write-Host "[4/4] git push origin $branch..." -ForegroundColor Yellow
    git push origin $branch
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Push échoué — commit local OK, à pousser manuellement"
    }
}
else {
    Write-Host ""
    Write-Host "[4/4] Push ignoré (-NoPush)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "   Commit V1.8 terminé" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Prochaines étapes :" -ForegroundColor White
Write-Host "  1. dotnet build (dernière vérification)"
Write-Host "  2. .\docs\deployment\Deploy-AdiPAIE-V18.ps1 -DryRun (preview déploiement)"
Write-Host "  3. .\docs\deployment\Deploy-AdiPAIE-V18.ps1 (déploiement réel)"
Write-Host "  4. Post-deploy : Init. rôles GRH + Recharger référentiel paie + SQL AV_TEL"
Write-Host ""
