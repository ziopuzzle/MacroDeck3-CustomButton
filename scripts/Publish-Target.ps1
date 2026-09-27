param(
    [Parameter(Mandatory)][ValidateSet('win-x64','osx-arm64','osx-x64','linux-x64','linux-arm64')][string]$Runtime,
    [switch]$Offline
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath((Join-Path $repo "src/bin/publish/store-$Runtime"))
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'src/bin/publish')) + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid publish output path.' }
# Only this explicitly checked generated target directory is cleaned.
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
$restoreArgs = @('--locked-mode')
if ($Offline) { $restoreArgs += @('--ignore-failed-sources', '-p:NuGetAudit=false') }
foreach ($project in @('src/Ziopuzzle.CustomButton.csproj','editor/Ziopuzzle.CustomButton.Editor.csproj')) {
    dotnet restore (Join-Path $repo $project) @restoreArgs
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
}
dotnet publish (Join-Path $repo 'src/Ziopuzzle.CustomButton.csproj') -c Release -r $Runtime --self-contained false -p:UseAppHost=false --no-restore -o $output
if ($LASTEXITCODE -ne 0) { throw 'Host publish failed.' }
& (Join-Path $PSScriptRoot 'Publish-Editor.ps1') -Runtime $Runtime -Destination $output
& (Join-Path $PSScriptRoot 'Collect-Notices.ps1') -Stage $output -RuntimeRoot $output
