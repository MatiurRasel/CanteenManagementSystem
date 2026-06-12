# Adds keys from a JSON file ([{ "k": "Key.Name", "en": "English value" }, ...])
# into a .resx file. Existing keys are left untouched (first writer wins).
# Usage: powershell -File tools/add-resx-keys.ps1 -Resx <path> -Json <path>
param(
    [Parameter(Mandatory = $true)][string]$Resx,
    [Parameter(Mandatory = $true)][string]$Json
)

$ErrorActionPreference = 'Stop'

[xml]$doc = Get-Content -LiteralPath $Resx -Encoding UTF8
$existing = @{}
foreach ($d in $doc.root.SelectNodes('data')) { $existing[$d.GetAttribute('name')] = $true }

$entries = Get-Content -LiteralPath $Json -Encoding UTF8 -Raw | ConvertFrom-Json
$added = 0
foreach ($e in $entries) {
    if ([string]::IsNullOrWhiteSpace($e.k) -or $existing.ContainsKey($e.k)) { continue }
    $data = $doc.CreateElement('data')
    $data.SetAttribute('name', $e.k)
    $spaceAttr = $doc.CreateAttribute('xml', 'space', 'http://www.w3.org/XML/1998/namespace')
    $spaceAttr.Value = 'preserve'
    $data.SetAttributeNode($spaceAttr) | Out-Null
    $value = $doc.CreateElement('value')
    $value.InnerText = "$($e.en)"
    $data.AppendChild($value) | Out-Null
    $doc.root.AppendChild($data) | Out-Null
    $existing[$e.k] = $true
    $added++
}

$settings = New-Object System.Xml.XmlWriterSettings
$settings.Indent = $true
$settings.Encoding = New-Object System.Text.UTF8Encoding($false)
$writer = [System.Xml.XmlWriter]::Create($Resx, $settings)
$doc.Save($writer)
$writer.Close()
Write-Output "Added $added new key(s) to $Resx"
