param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Destination
)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path -LiteralPath $Source).Path
$destinationRoot = (Resolve-Path -LiteralPath $Destination).Path
$copies = @()
# Check every collision before copying anything. Different runtime/dependency versions
# must be reconciled in the projects, never silently selected by publish order.
foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($sourceRoot, $file.FullName)
    $target = Join-Path $destinationRoot $relative
    if (Test-Path -LiteralPath $target) {
        if (!(Test-Path -LiteralPath $target -PathType Leaf) -or
            (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) {
            throw "Conflicting publish output: $relative"
        }
    } else { $copies += @{ Source = $file.FullName; Target = $target } }
}
foreach ($copy in $copies) {
    New-Item -ItemType Directory -Force (Split-Path $copy.Target -Parent) | Out-Null
    Copy-Item -LiteralPath $copy.Source -Destination $copy.Target
}
