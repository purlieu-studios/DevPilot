#!/usr/bin/env pwsh
# Commit-msg hook - enforces Conventional Commits format

param(
    [Parameter(Mandatory=$true)]
    [string]$CommitMsgFile
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 Validating commit message..." -ForegroundColor Cyan

# Load configuration
$configPath = Join-Path $PSScriptRoot "hooks-config.json"
$config = Get-Content $configPath | ConvertFrom-Json

# Read commit message
$commitMsg = Get-Content $CommitMsgFile -Raw
$commitMsg = $commitMsg.Trim()

# Skip merge commits, revert commits, and commits with Co-Authored-By
if ($commitMsg -match "^Merge " -or $commitMsg -match "^Revert " -or $commitMsg -match "Co-Authored-By:") {
    Write-Host "✓ Special commit type - validation skipped" -ForegroundColor Green
    exit 0
}

$exitCode = 0

#############################################
# 1. CHECK FOR BLOCKED WORDS
#############################################
$blockedWords = $config.commit_message.blocked_words
foreach ($word in $blockedWords) {
    if ($commitMsg -match "(?i)\b$word\b") {
        Write-Host "❌ Commit message contains blocked word: '$word'" -ForegroundColor Red
        Write-Host "  Please use a meaningful commit message" -ForegroundColor Yellow
        $exitCode = 1
    }
}

#############################################
# 2. VALIDATE CONVENTIONAL COMMITS FORMAT
#############################################
$validTypes = $config.commit_message.types -join "|"
$conventionalCommitPattern = "^($validTypes)(\([a-z0-9-]+\))?!?: .+"

if ($commitMsg -notmatch $conventionalCommitPattern) {
    Write-Host "❌ Commit message does not follow Conventional Commits format!" -ForegroundColor Red
    Write-Host "`nExpected format:" -ForegroundColor Yellow
    Write-Host "  <type>[optional scope]: <description>" -ForegroundColor White
    Write-Host "`nValid types:" -ForegroundColor Yellow
    Write-Host "  feat     - A new feature" -ForegroundColor White
    Write-Host "  fix      - A bug fix" -ForegroundColor White
    Write-Host "  docs     - Documentation only changes" -ForegroundColor White
    Write-Host "  style    - Code style changes (formatting, etc.)" -ForegroundColor White
    Write-Host "  refactor - Code refactoring" -ForegroundColor White
    Write-Host "  perf     - Performance improvements" -ForegroundColor White
    Write-Host "  test     - Adding or updating tests" -ForegroundColor White
    Write-Host "  build    - Build system changes" -ForegroundColor White
    Write-Host "  ci       - CI/CD changes" -ForegroundColor White
    Write-Host "  chore    - Other changes (dependencies, configs, etc.)" -ForegroundColor White
    Write-Host "  revert   - Revert a previous commit" -ForegroundColor White
    Write-Host "`nExamples:" -ForegroundColor Yellow
    Write-Host "  feat: add user authentication" -ForegroundColor Gray
    Write-Host "  fix(api): resolve null reference exception" -ForegroundColor Gray
    Write-Host "  docs: update README with installation steps" -ForegroundColor Gray
    Write-Host "`nYour commit message:" -ForegroundColor Yellow
    Write-Host "  $($commitMsg -split "`n")[0]" -ForegroundColor Red
    $exitCode = 1
}

#############################################
# 3. VALIDATE SUBJECT LINE
#############################################
$lines = $commitMsg -split "`n"
$subjectLine = $lines[0]

# Extract description part (after type and scope)
if ($subjectLine -match "^[a-z]+(\([a-z0-9-]+\))?!?: (.+)$") {
    $description = $matches[2]

    # Check length
    $maxLength = $config.commit_message.max_subject_length
    if ($subjectLine.Length -gt $maxLength) {
        Write-Host "⚠️  Warning: Subject line exceeds $maxLength characters ($($subjectLine.Length) chars)" -ForegroundColor Yellow
        Write-Host "  Consider shortening: $subjectLine" -ForegroundColor Yellow
    }

    # Check if starts with uppercase (after type and scope)
    if ($description -match "^[a-z]") {
        Write-Host "⚠️  Warning: Description should start with lowercase" -ForegroundColor Yellow
        Write-Host "  Current: $description" -ForegroundColor Yellow
        Write-Host "  Better:  $($description.Substring(0,1).ToLower() + $description.Substring(1))" -ForegroundColor Yellow
    }

    # Check if ends with period
    if ($description -match "\.$") {
        Write-Host "⚠️  Warning: Subject line should not end with a period" -ForegroundColor Yellow
    }

    # Check for imperative mood (common mistakes)
    $nonImperativeForms = @("added", "fixed", "updated", "changed", "removed", "created")
    foreach ($form in $nonImperativeForms) {
        if ($description -match "(?i)^$form\b") {
            $imperative = $form -replace "d$", ""
            Write-Host "⚠️  Warning: Use imperative mood (command form)" -ForegroundColor Yellow
            Write-Host "  Instead of '$form', use '$imperative'" -ForegroundColor Yellow
            Write-Host "  Example: 'fix: add validation' not 'fix: added validation'" -ForegroundColor Yellow
        }
    }
}

#############################################
# 4. CHECK BODY FORMAT (if present)
#############################################
if ($lines.Count -gt 1) {
    # Check for blank line after subject
    if ($lines[1] -ne "") {
        Write-Host "⚠️  Warning: Add a blank line between subject and body" -ForegroundColor Yellow
    }
}

#############################################
# SUMMARY
#############################################
if ($exitCode -eq 0) {
    Write-Host "✅ Commit message validation passed!" -ForegroundColor Green
}
else {
    Write-Host "`n❌ Commit message validation failed!" -ForegroundColor Red
    Write-Host "Please fix your commit message and try again." -ForegroundColor Yellow
    Write-Host "To edit: git commit --amend" -ForegroundColor Yellow
    Write-Host "To bypass (not recommended): git commit --no-verify" -ForegroundColor DarkGray
}

exit $exitCode
