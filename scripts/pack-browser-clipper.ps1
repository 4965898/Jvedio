param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts'), [switch]$ConnectorOnly)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskExtension = Join-Path $taskRoot 'browser-extension'
$taskManifest = Get-Content -LiteralPath (Join-Path $taskExtension 'manifest.json') -Raw | ConvertFrom-Json
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskStage = Join-Path $taskOutput ('connector-stage-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskStage -Force | Out-Null
try {
    foreach ($taskName in @('manifest.json', 'extractor.js', 'background.js', 'popup.html', 'popup.js', 'popup.css', 'README.md', 'icons')) {
        Copy-Item -LiteralPath (Join-Path $taskExtension $taskName) -Destination $taskStage -Recurse
    }
    Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE') -Destination $taskStage
    $taskZip = Join-Path $taskOutput ("Jvedio-Connector-$($taskManifest.version).zip")
    Compress-Archive -Path (Join-Path $taskStage '*') -DestinationPath $taskZip -Force
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $taskArchive = [IO.Compression.ZipFile]::OpenRead($taskZip)
    try {
        $taskNames = @($taskArchive.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
        foreach ($taskName in @('manifest.json', 'background.js', 'extractor.js', 'popup.html', 'popup.js', 'popup.css', 'icons/128.png')) {
            if ($taskName -notin $taskNames) { throw "Connector ZIP is missing $taskName" }
        }
        if ($taskNames | Where-Object { $_ -match 'node_modules|tests/|JAVBUS.txt|JAVDB.txt|GVG-107|test-connection' }) { throw 'Unexpected test or private data in connector package' }
    } finally { $taskArchive.Dispose() }
    if (-not $ConnectorOnly) {
        $taskExe = Join-Path $taskRoot 'Jvedio-WPF\Jvedio\bin\Release\Jvedio.exe'
        if (-not (Test-Path -LiteralPath $taskExe)) { throw 'Build the desktop application in Release mode first' }
        Copy-Item -LiteralPath $taskExe -Destination (Join-Path $taskOutput 'Jvedio-Connector-preview.exe') -Force
    }
    Copy-Item -LiteralPath (Join-Path $taskExtension 'README.md') -Destination (Join-Path $taskOutput 'Jvedio-Connector-使用说明.md') -Force
    Write-Output "Connector package: $taskZip"
    if (-not $ConnectorOnly) { Write-Output "Desktop preview: $(Join-Path $taskOutput 'Jvedio-Connector-preview.exe')" }
} finally {
    $taskResolvedStage = [IO.Path]::GetFullPath($taskStage)
    $taskExpectedPrefix = $taskOutput.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $taskResolvedStage.StartsWith($taskExpectedPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Temporary package path is outside the output directory' }
    Remove-Item -LiteralPath $taskResolvedStage -Recurse -Force
}
