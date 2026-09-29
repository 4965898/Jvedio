param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Jvedio-WPF\Jvedio\bin\Release')
)

$ErrorActionPreference = 'Stop'
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
$assembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $build 'Jvedio.exe'))
$policy = $assembly.GetType('Jvedio.Core.Crawler.ScrapeFieldPolicy', $true)

$scraped = [System.Collections.Generic.Dictionary[string,object]]::new()
$scraped['Title'] = 'Remote title'
$scraped['Plot'] = 'Remote plot'
$scraped['VID'] = 'REMOTE-999'
$scraped['Path'] = 'C:\wrong-path.mp4'
$scraped['BigImageUrl'] = 'https://example.invalid/poster.jpg'

$selected = [string[]]@('Plot')
$merged = $policy.GetMethod('MergeDictionary').Invoke($null, [object[]]@($scraped, $selected))
if (-not $merged.ContainsKey('Plot') -or -not $merged.ContainsKey('BigImageUrl') -or
    $merged.ContainsKey('Title') -or $merged.ContainsKey('VID') -or $merged.ContainsKey('Path')) {
    throw 'Selected metadata or protected local fields were merged incorrectly'
}
Write-Output 'PASS: selected metadata and protected VID and Path'
