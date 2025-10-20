#!/usr/bin/env pwsh
# Post-checkout hook - restores dependencies after branch switch

param(
    [string]$PrevHead,
    [string]$NewHead,
    [string]$BranchCheckout
)

# Only run on branch checkouts (not file checkouts)
if ($BranchCheckout -ne "1") {
    exit 0
}

Write-Host "🔄 Post-checkout: Setting up branch..." -ForegroundColor Cyan

$currentBranch = git rev-parse --abbrev-ref HEAD
Write-Host "  Switched to branch: $currentBranch" -ForegroundColor Yellow

#############################################
# 1. RESTORE .NET DEPENDENCIES
#############################################
$slnFiles = Get-ChildItem -Path "." -Filter "*.sln" -Recurse -ErrorAction SilentlyContinue

if ($slnFiles) {
    Write-Host "`n📦 Restoring .NET dependencies..." -ForegroundColor Yellow

    $slnPath = $slnFiles[0].FullName
    $restoreResult = dotnet restore $slnPath --nologo --verbosity quiet 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Dependencies restored" -ForegroundColor Green
    }
    else {
        Write-Host "⚠️  Warning: Failed to restore dependencies" -ForegroundColor Yellow
        Write-Host "  Run 'dotnet restore' manually if needed" -ForegroundColor Yellow
    }
}

#############################################
# 2. CLEAN BUILD ARTIFACTS (optional)
#############################################
$binDirs = Get-ChildItem -Path "." -Filter "bin" -Recurse -Directory -ErrorAction SilentlyContinue
$objDirs = Get-ChildItem -Path "." -Filter "obj" -Recurse -Directory -ErrorAction SilentlyContinue

if ($binDirs -or $objDirs) {
    Write-Host "`n🧹 Build artifacts detected" -ForegroundColor Yellow
    Write-Host "  Tip: Run 'dotnet clean' if you encounter build issues" -ForegroundColor DarkGray
}

#############################################
# 3. SHOW RECENT COMMITS
#############################################
Write-Host "`n📜 Recent commits on this branch:" -ForegroundColor Yellow
git log --oneline -n 5 --color=always

Write-Host "`n✅ Branch setup complete!" -ForegroundColor Green

exit 0
