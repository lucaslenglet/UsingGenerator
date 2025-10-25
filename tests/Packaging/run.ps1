#!/usr/bin/env pwsh
<#
.SYNOPSIS
    End-to-end check that the UsingGenerator package reaches a consumer transitively.

.DESCRIPTION
    Packs UsingGenerator into a local feed, packs Demo.Bundles (a library that only declares a
    bundle) against it, then builds Demo.App, which references Demo.Bundles and nothing else.
    Demo.App compiles only if the analyzer flowed through Demo.Bundles.

.PARAMETER Version
    Overrides the package version. Defaults to UsingGeneratorVersion from Directory.Build.props,
    so this script never carries a second copy of it.
#>
[CmdletBinding()]
param(
    [string] $Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$repo = Resolve-Path (Join-Path $root '..' '..')
$package = Join-Path $repo 'src' 'UsingGenerator.Abstractions'
$artifacts = Join-Path $root '.artifacts'
$feed = Join-Path $artifacts 'feed'

function Invoke-Step {
    param([string] $Name, [scriptblock] $Action)

    Write-Host "==> $Name" -ForegroundColor Cyan
    $result = & $Action

    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }

    $result
}

if (-not $Version) {
    $Version = (dotnet msbuild $package -getProperty:UsingGeneratorVersion).Trim()
}

# A stale package or restore cache would silently invalidate the result.
$stale = @(
    $artifacts
    Join-Path $root 'Demo.Bundles' 'obj'
    Join-Path $root 'Demo.Bundles' 'bin'
    Join-Path $root 'Demo.App' 'obj'
    Join-Path $root 'Demo.App' 'bin'
)

Remove-Item $stale -Recurse -Force -ErrorAction Ignore

Invoke-Step 'Pack UsingGenerator' {
    dotnet pack $package -c Release -o $feed "-p:UsingGeneratorVersion=$Version"
} | Out-Host

Invoke-Step 'Pack Demo.Bundles' {
    dotnet pack (Join-Path $root 'Demo.Bundles') -c Release -o $feed "-p:UsingGeneratorVersion=$Version"
} | Out-Host

$output = Invoke-Step 'Run Demo.App' {
    dotnet run --project (Join-Path $root 'Demo.App') | Select-Object -Last 1
}

# The generator is what makes Demo.App compile at all, so a successful run is most of the proof.
if ($output -ne '2') {
    throw "Demo.App printed '$output', expected '2'."
}

$generated = Get-ChildItem (Join-Path $root 'Demo.App' 'obj' 'GeneratedFiles') -Recurse -Filter 'SharedUsings.g.cs'

if (-not $generated) {
    throw 'No SharedUsings.g.cs was emitted in Demo.App.'
}

$content = Get-Content $generated[0].FullName -Raw

foreach ($expected in 'global using System.Linq;', 'global using static System.Console;') {
    if ($content -notmatch [regex]::Escape($expected)) {
        throw "Generated file is missing '$expected'.`n$content"
    }
}

Write-Host ''
Write-Host "PASS: UsingGenerator $Version reached Demo.App through Demo.Bundles." -ForegroundColor Green
Write-Host $content
