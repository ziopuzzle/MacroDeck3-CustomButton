$resourceDirectory = Join-Path $PSScriptRoot '../src/Localization'
New-Item -ItemType Directory -Force $resourceDirectory | Out-Null
$entries = Get-Content (Join-Path $PSScriptRoot 'translations.tsv') | ForEach-Object { ,($_ -split "`t", 2) }
$document = [System.Xml.XmlDocument]::new()
$root = $document.CreateElement('root'); $null = $document.AppendChild($root)
foreach ($entry in $entries) {
    if ($entry.Length -ne 2) { throw "Invalid catalog entry: $entry" }
    $data = $document.CreateElement('data'); $data.SetAttribute('name', $entry[0]); $data.SetAttribute('xml:space', 'preserve')
    $value = $document.CreateElement('value')
    $value.InnerText = if ($entry[0].StartsWith('Message')) { $entry[1] } else { $entry[1].Replace('{', '{{').Replace('}', '}}') }
    $null = $data.AppendChild($value); $null = $root.AppendChild($data)
}
$document.Save((Join-Path $resourceDirectory 'Strings.resx'))
