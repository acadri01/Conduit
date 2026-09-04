#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs Conduit's build, test suite, and CLI, capturing everything printed to the console into
    a single timestamped log file — so you can hand that file back to Claude for review instead
    of re-describing what happened.

.DESCRIPTION
    Run this whenever something looks wrong and you want Claude to see exactly what your machine
    saw, not a paraphrase of it. It does not require anything beyond what TESTING.md's tutorial
    already sets up (Git, the .NET 8 SDK).

    Runs, in order, and logs each step's full output:
      1. dotnet build
      2. dotnet test
      3. conduit optimize against every .cii file in -InputPath (default: fixtures\)

    Then prints where the log file landed. Commit that file (see below) and tell Claude which run
    to look at.

.PARAMETER InputPath
    A single .cii file, or a directory to run against every .cii file in it (non-recursive).
    Defaults to the repo's fixtures\ directory. If the directory also contains a caesar.cfg,
    Conduit picks that up automatically (see TESTING.md) — no extra step needed.

.PARAMETER LogDirectory
    Where to write the log file. Defaults to test-logs\ at the repo root (created if missing).

.EXAMPLE
    .\scripts\run-and-log.ps1
    Runs against the committed fixtures, writes test-logs\2026-08-24_153000-run.log.

.EXAMPLE
    .\scripts\run-and-log.ps1 -InputPath C:\path\to\your\real\job\folder
    Runs against every .cii file in that folder (e.g. one you exported from CAESAR II /
    generated with your own tooling), picking up a caesar.cfg there too if present.

.NOTES
    After it finishes: `git add test-logs\<the-new-file>.log` and commit it, then push (or just
    hand Claude the file another way) so it can read what actually happened on your machine.
#>
param(
    [string]$InputPath = (Join-Path $PSScriptRoot "..\fixtures"),
    [string]$LogDirectory = (Join-Path $PSScriptRoot "..\test-logs")
)

$ErrorActionPreference = "Continue"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
$timestamp = Get-Date -Format "yyyy-MM-dd_HHmmss"
$logPath = Join-Path $LogDirectory "$timestamp-run.log"

function Write-Section($title) {
    $line = "=" * 80
    "`n$line`n$title`n$line" | Tee-Object -FilePath $logPath -Append
}

"Conduit test run — $timestamp" | Tee-Object -FilePath $logPath
"Machine: $env:COMPUTERNAME  OS: $([System.Environment]::OSVersion.VersionString)" | Tee-Object -FilePath $logPath -Append
"dotnet: $(dotnet --version 2>&1)" | Tee-Object -FilePath $logPath -Append

Write-Section "dotnet build"
dotnet build 2>&1 | Tee-Object -FilePath $logPath -Append

Write-Section "dotnet test"
dotnet test 2>&1 | Tee-Object -FilePath $logPath -Append

$targets = if (Test-Path $InputPath -PathType Container) {
    Get-ChildItem -Path $InputPath -Filter "*.cii" -File
} else {
    Get-Item $InputPath
}

foreach ($target in $targets) {
    $outFile = Join-Path $LogDirectory "$timestamp-$($target.BaseName)-output.cii"
    Write-Section "conduit optimize $($target.FullName) -> $outFile"
    dotnet run --project src\Conduit.Cli -- optimize $target.FullName $outFile 2>&1 | Tee-Object -FilePath $logPath -Append
    "Exit code: $LASTEXITCODE" | Tee-Object -FilePath $logPath -Append
}

Write-Section "Done"
"Log written to: $logPath" | Tee-Object -FilePath $logPath -Append
Write-Host "`nLog file: $logPath"
Write-Host "Commit it (git add `"$logPath`") and push, then point Claude at it."
