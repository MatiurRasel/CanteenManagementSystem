# Applies translated values from JSON files ([{ "k": "Key", "bn": "value" }, ...])
# onto an existing .resx, overwriting the <value> of each matching key.
# Usage: powershell -File tools/apply-resx-values.ps1 -Resx <path> -Json <path1>[,<path2>,...] -Field bn
param(
    [Parameter(Mandatory = $true)][string]$Resx,
    [Parameter(Mandatory = $true)][string[]]$Json,
    [string]$Field = 'bn'
)

$ErrorActionPreference = 'Stop'

[xml]$doc = Get-Content -LiteralPath $Resx -Encoding UTF8
$nodes = @{}
foreach ($d in $doc.root.SelectNodes('data')) { $nodes[$d.GetAttribute('name')] = $d }

$applied = 0; $missing = 0
foreach ($file in $Json) {
    $entries = Get-Content -LiteralPath $file -Encoding UTF8 -Raw | ConvertFrom-Json
    foreach ($e in $entries) {
        $val = $e.$Field
        if ([string]::IsNullOrWhiteSpace($e.k) -or $null -eq $val) { continue }
        if ($nodes.ContainsKey($e.k)) {
            $nodes[$e.k].SelectSingleNode('value').InnerText = "$val"
            $applied++
        } else {
            Write-Output "WARN missing key in resx: $($e.k)"
            $missing++
        }
    }
}

$settings = New-Object System.Xml.XmlWriterSettings
$settings.Indent = $true
$settings.Encoding = New-Object System.Text.UTF8Encoding($false)
$writer = [System.Xml.XmlWriter]::Create($Resx, $settings)
$doc.Save($writer)
$writer.Close()
Write-Output "Applied $applied value(s) to $Resx ($missing missing keys)"
