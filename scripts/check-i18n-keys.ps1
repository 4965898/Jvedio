# i18n regression check (log section 4.5 / 6):
#   1. FAIL: key sets of the three language files must be identical
#      (the historical bug: en-US missing Actors/HasPoster/NoPoster made English UI fall back to key names)
#   2. WARN: literal GetValueByKey("...") keys referenced in C# but missing from zh-CN
#      (warn only: some keys live in the SuperControls.Style language dictionary)
#   3. WARN: DynamicResource keys referenced in Jvedio XAML but found in none of the
#      three language files nor any Jvedio resource dictionary
# Usage: powershell -File scripts/check-i18n-keys.ps1
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$langDir = Join-Path $root "Jvedio-WPF/Jvedio/Core/Lang"
$files = @{
    "zh-CN" = Join-Path $langDir "zh-CN.xaml"
    "en-US" = Join-Path $langDir "en-US.xaml"
    "ja-JP" = Join-Path $langDir "ja-JP.xaml"
}

function Get-KeySet([string] $path) {
    $content = Get-Content -Raw -Encoding UTF8 $path
    $keys = [System.Collections.Generic.HashSet[string]]::new()
    if ([string]::IsNullOrEmpty($content)) { return $keys }
    foreach ($m in [regex]::Matches($content, 'x:Key="([^"]+)"')) {
        [void]$keys.Add($m.Groups[1].Value)
    }
    return $keys
}

function Get-FileText([string] $path) {
    if (-not (Test-Path $path)) { return $null }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($null -eq $bytes -or $bytes.Length -eq 0) { return $null }
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

$keySets = @{}
foreach ($name in $files.Keys) {
    $keySets[$name] = Get-KeySet $files[$name]
}

$failed = $false
$names = @($files.Keys | Sort-Object)
for ($i = 0; $i -lt $names.Count; $i++) {
    for ($j = $i + 1; $j -lt $names.Count; $j++) {
        $a = $keySets[$names[$i]]
        $b = $keySets[$names[$j]]
        $onlyA = $a | Where-Object { -not $b.Contains($_) }
        $onlyB = $b | Where-Object { -not $a.Contains($_) }
        if ($onlyA.Count -gt 0 -or $onlyB.Count -gt 0) {
            $failed = $true
            Write-Host "[FAIL] key mismatch between $($names[$i]) and $($names[$j]):"
            if ($onlyA.Count -gt 0) { Write-Host ("  only in {0}: {1}" -f $names[$i], ($onlyA -join ", ")) }
            if ($onlyB.Count -gt 0) { Write-Host ("  only in {0}: {1}" -f $names[$j], ($onlyB -join ", ")) }
        }
    }
}
if (-not $failed) {
    Write-Host ("[OK] three language files share the same {0} keys" -f $keySets["zh-CN"].Count)
}

$zhKeys = $keySets["zh-CN"]

# 2. literal keys used from code (warn-only, deduplicated)
$warn = 0
$reported = [System.Collections.Generic.HashSet[string]]::new()
Get-ChildItem -Path (Join-Path $root "Jvedio-WPF/Jvedio") -Recurse -Filter *.cs | ForEach-Object {
    $text = Get-FileText $_.FullName
    if ([string]::IsNullOrEmpty($text)) { return }
    foreach ($m in [regex]::Matches($text, 'GetValueByKey\("([^"]+)"\)')) {
        $key = $m.Groups[1].Value
        if (-not $zhKeys.Contains($key) -and $reported.Add($key)) {
            Write-Warning ("key '{0}' used in code but not in zh-CN.xaml (may come from SuperControls.Style)" -f $key)
            $warn++
        }
    }
}

# 3. DynamicResource keys resolved nowhere we can see (warn-only, deduplicated)
$knownKeys = [System.Collections.Generic.HashSet[string]]::new()
foreach ($k in $keySets["zh-CN"]) { [void]$knownKeys.Add($k) }
foreach ($k in $keySets["en-US"]) { [void]$knownKeys.Add($k) }
foreach ($k in $keySets["ja-JP"]) { [void]$knownKeys.Add($k) }

$xamlFiles = Get-ChildItem -Path (Join-Path $root "Jvedio-WPF/Jvedio") -Recurse -Filter *.xaml
$allText = @{}
foreach ($f in $xamlFiles) {
    $t = Get-FileText $f.FullName
    if (-not [string]::IsNullOrEmpty($t)) { $allText[$f.FullName] = $t }
    foreach ($m in [regex]::Matches($t, 'x:Key="([^"]+)"')) {
        [void]$knownKeys.Add($m.Groups[1].Value)
    }
}
$reported2 = [System.Collections.Generic.HashSet[string]]::new()
foreach ($f in $xamlFiles) {
    $text = $allText[$f.FullName]
    if ([string]::IsNullOrEmpty($text)) { continue }
    foreach ($m in [regex]::Matches($text, '\{DynamicResource ([^}\s]+)\}')) {
        $key = $m.Groups[1].Value
        if (-not $knownKeys.Contains($key) -and $reported2.Add($key)) {
            Write-Warning ("DynamicResource '{0}' ({1}) not defined in Jvedio resources (may come from SuperControls.Style)" -f $key, $f.Name)
            $warn++
        }
    }
}

Write-Host ("[INFO] {0} warnings" -f $warn)
if ($failed) { exit 1 }
exit 0
