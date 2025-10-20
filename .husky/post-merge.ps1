#!/usr/bin/env pwsh
# Post-merge hook - updates dependencies and rebuilds after merge

$ErrorActionPreference = "Stop"

Write-Host "🔄 Post-merge: Updating after merge..." -ForegroundColor Cyan

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
        Write-Host "  Run 'dotnet restore' manually" -ForegroundColor Yellow
    }

    #############################################
    # 2. REBUILD SOLUTION (optional)
    #############################################
    Write-Host "`n🔨 Rebuilding solution..." -ForegroundColor Yellow

    $buildResult = dotnet build $slnPath --configuration Debug --nologo --verbosity quiet 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Build successful" -ForegroundColor Green
    }
    else {
        Write-Host "⚠️  Warning: Build failed after merge" -ForegroundColor Yellow
        Write-Host "  You may need to resolve merge conflicts in project files" -ForegroundColor Yellow
        Write-Host "  Run 'dotnet build' to see detailed errors" -ForegroundColor Yellow
    }
}

#############################################
# 3. CHECK FOR CONFLICTS
#############################################
$conflicts = git diff --name-only --diff-filter=U

if ($conflicts) {
    Write-Host "`n⚠️  Merge conflicts detected:" -ForegroundColor Red
    $conflicts | ForEach-Object {
        Write-Host "  ↳ $_" -ForegroundColor Red
    }
    Write-Host "`n💡 Resolve conflicts and commit the resolution" -ForegroundColor Yellow
}

Write-Host "`n✅ Post-merge tasks complete!" -ForegroundColor Green

exit 0
