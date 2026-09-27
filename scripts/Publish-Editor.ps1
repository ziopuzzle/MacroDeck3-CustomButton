param(
    [Parameter(Mandatory)][string]$Runtime,
    [Parameter(Mandatory)][string]$Destination
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$staging = Join-Path $repo ('work/editor-publish-' + $Runtime + '-' + [Guid]::NewGuid().ToString('N'))
dotnet publish (Join-Path $repo 'editor/Ziopuzzle.CustomButton.Editor.csproj') -c Release -r $Runtime --self-contained true --no-restore -p:UsedAvaloniaProducts= -o $staging
if ($LASTEXITCODE -ne 0) { throw 'Editor publish failed.' }
& (Join-Path $PSScriptRoot 'Merge-PublishDirectory.ps1') -Source $staging -Destination $Destination
