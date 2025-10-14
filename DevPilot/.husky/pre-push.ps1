#!/usr/bin/env pwsh
# Pre-push hook - runs build, tests, and branch validation

$ErrorActionPreference = "Stop"

Write-Host "🚀 Running pre-push checks..." -ForegroundColor Cyan

# Load configuration
$configPath = Join-Path $PSScriptRoot "hooks-config.json"
$config = Get-Content $configPath | ConvertFrom-Json

$exitCode = 0

#############################################
# 1. GET CURRENT BRANCH
#############################################
$currentBranch = git rev-parse --abbrev-ref HEAD

Write-Host "`n🌿 Current branch: $currentBranch" -ForegroundColor Yellow

# Check if pushing to protected branch
if ($currentBranch -eq "main" -or $currentBranch -eq "master") {
    Write-Host "❌ PUSH BLOCKED: Direct push to '$currentBranch' is not allowed!" -ForegroundColor Red
    Write-Host "`n💡 Please create a pull request instead:" -ForegroundColor Yellow
    Write-Host "  1. Create a feature branch: git checkout -b feat/my-feature" -ForegroundColor Yellow
    Write-Host "  2. Push your branch: git push -u origin feat/my-feature" -ForegroundColor Yellow
    Write-Host "  3. Create a pull request on GitHub" -ForegroundColor Yellow
    Write-Host "`nTo bypass (emergency only): git push --no-verify" -ForegroundColor DarkGray
    exit 1
}

#############################################
# 2. VALIDATE BRANCH NAME
#############################################
Write-Host "`n📝 Validating branch name..." -ForegroundColor Yellow

$branchPattern = $config.pre_push.branch_name_pattern
if ($currentBranch -notmatch $branchPattern) {
    Write-Host "⚠️  Warning: Branch name doesn't follow convention" -ForegroundColor Yellow
    Write-Host "  Expected format: type/description" -ForegroundColor Yellow
    Write-Host "  Examples:" -ForegroundColor Yellow
    Write-Host "    - feat/user-authentication" -ForegroundColor Gray
    Write-Host "    - fix/null-reference-bug" -ForegroundColor Gray
    Write-Host "    - docs/update-readme" -ForegroundColor Gray
    Write-Host "  Your branch: $currentBranch" -ForegroundColor Yellow
}
else {
    Write-Host "✓ Branch name follows convention" -ForegroundColor Green
}

#############################################
# 3. CHECK FOR .NET SOLUTION
#############################################
$slnFiles = Get-ChildItem -Path "." -Filter "*.sln" -Recurse -ErrorAction SilentlyContinue

if ($slnFiles) {
    $slnPath = $slnFiles[0].FullName
    Write-Host "`n📦 Found solution: $($slnFiles[0].Name)" -ForegroundColor Cyan

    #############################################
    # 4. RUN BUILD (if enabled)
    #############################################
    if ($config.pre_push.require_build -eq $true) {
        Write-Host "`n🔨 Building solution..." -ForegroundColor Yellow

        $buildResult = dotnet build $slnPath --configuration Release --nologo 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Build failed!" -ForegroundColor Red
            Write-Host $buildResult -ForegroundColor Red
            Write-Host "`n💡 Fix build errors before pushing" -ForegroundColor Yellow
            $exitCode = 1
        }
        else {
            Write-Host "✓ Build successful" -ForegroundColor Green
        }
    }
    else {
        Write-Host "`nℹ️  Build check disabled in configuration" -ForegroundColor DarkGray
    }

    #############################################
    # 5. RUN TESTS (if enabled)
    #############################################
    if ($config.pre_push.require_tests -eq $true) {
        Write-Host "`n🧪 Running tests..." -ForegroundColor Yellow

        $testResult = dotnet test $slnPath --configuration Release --nologo --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Tests failed!" -ForegroundColor Red
            Write-Host $testResult -ForegroundColor Red
            Write-Host "`n💡 Fix failing tests before pushing" -ForegroundColor Yellow
            $exitCode = 1
        }
        else {
            Write-Host "✓ All tests passed" -ForegroundColor Green
        }
    }
    else {
        Write-Host "`nℹ️  Test check disabled in configuration" -ForegroundColor DarkGray
    }
}
else {
    Write-Host "`nℹ️  No .NET solution found - skipping build and test checks" -ForegroundColor DarkGray
}

#############################################
# 6. CHECK FOR UNCOMMITTED CHANGES
#############################################
Write-Host "`n📂 Checking for uncommitted changes..." -ForegroundColor Yellow

$status = git status --porcelain
if ($status) {
    Write-Host "⚠️  Warning: You have uncommitted changes" -ForegroundColor Yellow
    Write-Host "  These changes will NOT be included in the push" -ForegroundColor Yellow
    Write-Host "`n  Modified files:" -ForegroundColor Yellow
    $status | ForEach-Object {
        Write-Host "    $_" -ForegroundColor Gray
    }
}
else {
    Write-Host "✓ Working directory clean" -ForegroundColor Green
}

#############################################
# SUMMARY
#############################################
Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan

if ($exitCode -eq 0) {
    Write-Host "✅ All pre-push checks passed!" -ForegroundColor Green
    Write-Host "Pushing to remote..." -ForegroundColor Cyan
}
else {
    Write-Host "❌ Pre-push checks failed!" -ForegroundColor Red
    Write-Host "`nPlease fix the issues above before pushing." -ForegroundColor Yellow
    Write-Host "To bypass (not recommended): git push --no-verify" -ForegroundColor DarkGray
}

Write-Host ("=" * 60) -ForegroundColor Cyan

exit $exitCode
