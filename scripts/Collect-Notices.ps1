param([Parameter(Mandatory)][string]$Stage, [string]$RuntimeRoot, [string]$NoticesRoot)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $NoticesRoot) { $NoticesRoot = $Stage }
$target = Join-Path $NoticesRoot 'ThirdParty'
New-Item -ItemType Directory -Force $target | Out-Null
$packages = @{}
$cache = $null
foreach ($project in @('src', 'editor')) {
    $assets = Get-Content (Join-Path $repo "$project/obj/project.assets.json") -Raw | ConvertFrom-Json
    $cache = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    foreach ($entry in $assets.libraries.PSObject.Properties) {
        if ($entry.Value.type -eq 'package') { $packages[$entry.Name] = $true }
    }
}
if (-not $RuntimeRoot) { $RuntimeRoot = Join-Path $Stage 'runtimes' }
foreach ($file in Get-ChildItem $RuntimeRoot -Filter '*.deps.json' -Recurse) {
    $deps = Get-Content $file.FullName -Raw | ConvertFrom-Json
    foreach ($entry in $deps.libraries.PSObject.Properties) {
        if ($entry.Name.StartsWith('runtimepack.')) { $packages[$entry.Name.Substring(12)] = $true }
    }
}
$index = [Collections.Generic.List[string]]::new()
$index.Add('# Dependency notices')
$index.Add('')
$index.Add('Generated from restored project assets and published runtime packs. Includes build-time packages and assets for other platforms conservatively. Package binaries are unmodified. See per-package notices and the shared license texts.')
$index.Add('')
$index.Add('| Package | License | Copyright / authors |')
$index.Add('| --- | --- | --- |')
foreach ($name in ($packages.Keys | Sort-Object)) {
    $path = Join-Path $cache $name.ToLowerInvariant()
    $spec = Get-ChildItem $path -Filter '*.nuspec' | Select-Object -First 1
    if (-not $spec) { throw "No NuGet metadata found for $name" }
    [xml]$xml = Get-Content $spec.FullName
    $m = $xml.package.metadata
    $folder = Join-Path $target ($name.Replace('/', '-'))
    New-Item -ItemType Directory -Force $folder | Out-Null
    Copy-Item $spec.FullName $folder
    $copyrightNode = $m.SelectSingleNode("*[local-name()='copyright']")
    $copyright = if ($copyrightNode) { $copyrightNode.InnerText } else { [string]$m.authors }
    $license = $m.license.InnerText
    $index.Add("| $name | $license | $($copyright.Replace('|','/')) |")
    foreach ($file in Get-ChildItem $path -File | Where-Object Name -Match '^(LICENSE|NOTICE|THIRD.PARTY.NOTICES)(\.|$)') {
        Copy-Item $file.FullName $folder
    }
    if ($m.license.type -eq 'file') {
        $licensePath = Join-Path $path $license
        if (-not (Test-Path $licensePath)) { throw "Missing declared license for $name" }
        Copy-Item $licensePath (Join-Path $folder 'DECLARED-LICENSE.txt')
    } elseif ($license -notin @('MIT','Apache-2.0','MS-PL')) {
        throw "Review unhandled license for ${name}: $license"
    }
}
$index | Set-Content (Join-Path $target 'INDEX.md') -Encoding utf8
Copy-Item (Join-Path $repo 'licenses/*.txt') $target
Copy-Item (Join-Path $repo 'LICENSE') $Stage
Copy-Item (Join-Path $repo 'docs/privacy.md') (Join-Path $Stage 'PRIVACY.md')
Copy-Item (Join-Path $repo 'docs/distribution.md') (Join-Path $Stage 'DISTRIBUTION.md')
