param(
    [switch]$SkipTests,
    [switch]$AllPlatforms,
    [switch]$Offline,
    [ValidateSet('win-x64','osx-arm64','osx-x64','linux-x64','linux-arm64')]
    [string[]]$Runtime = @('win-x64')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($AllPlatforms) { $Runtime = @('win-x64','osx-arm64','osx-x64','linux-x64','linux-arm64') }
Push-Location $PSScriptRoot
try {
    if (-not $Offline) {
        dotnet tool restore
        if ($LASTEXITCODE -ne 0) { throw 'CLI restore failed.' }
    }
    $restoreArgs = @('--locked-mode')
    if ($Offline) { $restoreArgs += @('--ignore-failed-sources', '-p:NuGetAudit=false') }
    foreach ($project in @('tests/Ziopuzzle.CustomButton.Tests.csproj','editor.tests/Ziopuzzle.CustomButton.Editor.Tests.csproj')) {
        dotnet restore $project @restoreArgs
        if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    }
    if (-not $SkipTests) {
        dotnet test tests/Ziopuzzle.CustomButton.Tests.csproj -c Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
        dotnet test editor.tests/Ziopuzzle.CustomButton.Editor.Tests.csproj -c Release --no-restore -p:UsedAvaloniaProducts=
        if ($LASTEXITCODE -ne 0) { throw 'Editor tests failed.' }
    }
    # A fresh, explicit staging directory excludes source, dev credentials and stale binaries.
    foreach ($rid in $Runtime) {
    $stage = Join-Path $PSScriptRoot ('work/package-' + $rid + '-' + [Guid]::NewGuid().ToString('N'))
    $runtimeDirectory = Join-Path $stage ('runtimes/' + $rid)
    New-Item -ItemType Directory -Force $runtimeDirectory, (Join-Path $stage 'Assets'), 'artifacts' | Out-Null
    dotnet publish src/Ziopuzzle.CustomButton.csproj -c Release -r $rid --self-contained false -p:UseAppHost=false --no-restore -o $runtimeDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    & (Join-Path $PSScriptRoot 'scripts/Publish-Editor.ps1') -Runtime $rid -Destination $runtimeDirectory
    $metadata = Get-Content -LiteralPath 'src/manifest.json' -Raw | ConvertFrom-Json
    $targetEntry = $metadata.entrypoints.$rid
    $metadata.entrypoints = @{ $rid = $targetEntry }
    $metadata | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding utf8
    Copy-Item -LiteralPath 'src/Assets/icon.svg' -Destination (Join-Path $stage 'Assets/icon.svg')
    & (Join-Path $PSScriptRoot 'scripts/Collect-Notices.ps1') -Stage $stage -RuntimeRoot $runtimeDirectory -NoticesRoot $runtimeDirectory
    $artifact = Join-Path $PSScriptRoot ('artifacts/' + $metadata.id + '-' + $metadata.version + '-' + $rid + '.macroDeckPlugin')
    dotnet tool run macrodeck-plugin pack --source $stage --output $artifact --force
    if ($LASTEXITCODE -ne 0) { throw 'Pack failed.' }
    dotnet tool run macrodeck-plugin validate --artifact $artifact
    if ($LASTEXITCODE -ne 0) { throw 'Artifact validation failed.' }
    Write-Host "Built: $artifact"
    }
} finally { Pop-Location }
