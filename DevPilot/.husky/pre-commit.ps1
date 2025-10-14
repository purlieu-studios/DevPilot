#!/usr/bin/env pwsh
# Pre-commit hook - enforces LOC limits, code formatting, and detects secrets

$ErrorActionPreference = "Stop"

Write-Host "🔍 Running pre-commit checks..." -ForegroundColor Cyan

# Load configuration
$configPath = Join-Path $PSScriptRoot "hooks-config.json"
$config = Get-Content $configPath | ConvertFrom-Json

# Get staged files
$stagedFiles = git diff --cached --name-only --diff-filter=ACM

if ($stagedFiles.Count -eq 0) {
    Write-Host "✓ No staged files to check" -ForegroundColor Green
    exit 0
}

$exitCode = 0

#############################################
# 1. CHECK LINES OF CODE (LOC) LIMIT
#############################################
Write-Host "`n📊 Checking lines of code..." -ForegroundColor Yellow

$totalLines = 0
$fileBreakdown = @()
$excludePatterns = $config.loc_limits.excludePatterns

foreach ($file in $stagedFiles) {
    # Skip if file doesn't exist (deleted files)
    if (-not (Test-Path $file)) {
        continue
    }

    # Check if file matches any exclude pattern
    $shouldExclude = $false
    foreach ($pattern in $excludePatterns) {
        if ($file -like $pattern) {
            $shouldExclude = $true
            Write-Host "  ↳ Excluded: $file" -ForegroundColor DarkGray
            break
        }
    }

    if ($shouldExclude) {
        continue
    }

    # Count lines added/modified (not deleted)
    $diff = git diff --cached --numstat $file
    if ($diff) {
        $parts = $diff -split "`t"
        $linesAdded = [int]$parts[0]

        if ($linesAdded -gt 0) {
            $totalLines += $linesAdded
            $fileBreakdown += [PSCustomObject]@{
                File = $file
                Lines = $linesAdded
            }
        }
    }
}

# Display breakdown
if ($fileBreakdown.Count -gt 0) {
    Write-Host "`n  File changes:" -ForegroundColor White
    foreach ($item in $fileBreakdown | Sort-Object -Property Lines -Descending) {
        Write-Host ("    {0,-60} {1,5} lines" -f $item.File, $item.Lines) -ForegroundColor Gray
    }
}

Write-Host "`n  Total lines changed: $totalLines" -ForegroundColor White

# Check limits
$maxLines = $config.loc_limits.max_lines
$warnLines = $config.loc_limits.warn_lines

if ($totalLines -gt $maxLines) {
    Write-Host "`n❌ COMMIT BLOCKED: Too many lines changed!" -ForegroundColor Red
    Write-Host "  Maximum allowed: $maxLines lines" -ForegroundColor Red
    Write-Host "  Your changes: $totalLines lines" -ForegroundColor Red
    Write-Host "`n💡 Suggestions:" -ForegroundColor Yellow
    Write-Host "  - Split this commit into smaller, focused commits" -ForegroundColor Yellow
    Write-Host "  - Each commit should have a single, clear purpose" -ForegroundColor Yellow
    Write-Host "  - Consider refactoring in a separate commit" -ForegroundColor Yellow
    Write-Host "`n  To bypass this check (use sparingly): git commit --no-verify" -ForegroundColor DarkGray
    $exitCode = 1
}
elseif ($totalLines -gt $warnLines) {
    Write-Host "⚠️  Warning: Approaching LOC limit ($warnLines+ lines)" -ForegroundColor Yellow
    Write-Host "  Consider splitting into smaller commits for easier review" -ForegroundColor Yellow
}
else {
    Write-Host "✓ LOC limit check passed" -ForegroundColor Green
}

#############################################
# 2. CHECK FOR SECRETS
#############################################
Write-Host "`n🔐 Scanning for secrets..." -ForegroundColor Yellow

$secretsFound = $false
$secretPatterns = $config.secrets_detection.patterns

foreach ($file in $stagedFiles) {
    if (-not (Test-Path $file)) {
        continue
    }

    # Only check text files
    $extension = [System.IO.Path]::GetExtension($file)
    $textExtensions = @('.cs', '.js', '.ts', '.json', '.xml', '.config', '.txt', '.md', '.yml', '.yaml', '.ps1', '.sh')

    if ($textExtensions -contains $extension) {
        $content = Get-Content $file -Raw -ErrorAction SilentlyContinue

        if ($content) {
            foreach ($pattern in $secretPatterns) {
                if ($content -match $pattern) {
                    if (-not $secretsFound) {
                        Write-Host "❌ Potential secrets detected!" -ForegroundColor Red
                        $secretsFound = $true
                    }
                    Write-Host "  ↳ $file" -ForegroundColor Red
                    $exitCode = 1
                    break
                }
            }
        }
    }
}

if (-not $secretsFound) {
    Write-Host "✓ No secrets detected" -ForegroundColor Green
}
else {
    Write-Host "`n💡 Please remove sensitive data before committing" -ForegroundColor Yellow
    Write-Host "  Use environment variables or secure configuration instead" -ForegroundColor Yellow
}

#############################################
# 3. CHECK FILE SIZES
#############################################
Write-Host "`n📦 Checking file sizes..." -ForegroundColor Yellow

$maxFileSize = 5MB
$largeFiles = @()

foreach ($file in $stagedFiles) {
    if (Test-Path $file) {
        $size = (Get-Item $file).Length
        if ($size -gt $maxFileSize) {
            $sizeMB = [math]::Round($size / 1MB, 2)
            $largeFiles += "$file ($sizeMB MB)"
        }
    }
}

if ($largeFiles.Count -gt 0) {
    Write-Host "⚠️  Warning: Large files detected:" -ForegroundColor Yellow
    foreach ($file in $largeFiles) {
        Write-Host "  ↳ $file" -ForegroundColor Yellow
    }
    Write-Host "  Consider using Git LFS for large files" -ForegroundColor Yellow
}
else {
    Write-Host "✓ File size check passed" -ForegroundColor Green
}

#############################################
# 4. RUN DOTNET FORMAT (if .NET solution exists)
#############################################
$slnFiles = Get-ChildItem -Path "." -Filter "*.sln" -Recurse -ErrorAction SilentlyContinue

if ($slnFiles) {
    Write-Host "`n🎨 Checking code formatting..." -ForegroundColor Yellow

    # Check if dotnet-format is available
    $formatInstalled = dotnet tool list --global | Select-String "dotnet-format"

    if ($formatInstalled) {
        $formatResult = dotnet format --verify-no-changes --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "⚠️  Code formatting issues detected" -ForegroundColor Yellow
            Write-Host "  Run 'dotnet format' to fix formatting issues" -ForegroundColor Yellow
            Write-Host "  Or configure your IDE to format on save" -ForegroundColor Yellow
        }
        else {
            Write-Host "✓ Code formatting check passed" -ForegroundColor Green
        }
    }
    else {
        Write-Host "ℹ️  Skipping format check (dotnet-format not installed)" -ForegroundColor DarkGray
    }
}

#############################################
# SUMMARY
#############################################
Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan

if ($exitCode -eq 0) {
    Write-Host "✅ All pre-commit checks passed!" -ForegroundColor Green
}
else {
    Write-Host "❌ Pre-commit checks failed!" -ForegroundColor Red
    Write-Host "`nPlease fix the issues above before committing." -ForegroundColor Yellow
}

Write-Host ("=" * 60) -ForegroundColor Cyan

exit $exitCode
