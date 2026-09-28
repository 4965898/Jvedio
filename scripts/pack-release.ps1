param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifacts = Join-Path $repoRoot 'artifacts'
$source = Join-Path $repoRoot 'Jvedio-WPF\Jvedio\bin\Release'
$compressionDll = Join-Path $repoRoot 'Jvedio-WPF\packages\System.IO.Compression.4.3.0\lib\net46\System.IO.Compression.dll'
$pluginAssets = Join-Path $repoRoot 'release-assets\plugins\crawlers'
$busDll = Join-Path $repoRoot 'Jvedio-WPF\Jvedio\Core\Crawler\Bus2\BusCrawler\bin\Release\BusCrawler.dll'
$dbDll = Join-Path $repoRoot 'Jvedio-WPF\Jvedio\Core\Crawler\Db2\DbCrawler\bin\Release\DBCrawler.dll'
$stage = Join-Path $artifacts ('.stage-' + [guid]::NewGuid().ToString('N'))
$package = Join-Path $stage ('Jvedio-' + $Version)
$zip = Join-Path $artifacts ('Jvedio-' + $Version + '.zip')

function Assert-StagePath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath($stage) + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the temporary package: $full"
    }
}

foreach ($path in @($source, $compressionDll, $pluginAssets, $busDll, $dbDll)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required release input is missing: $path"
    }
}

$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $source 'Jvedio.exe')).Version.ToString()
if ($assemblyVersion -ne $Version) {
    throw "Assembly version $assemblyVersion does not match package version $Version"
}

New-Item -ItemType Directory -Path $package -Force | Out-Null
try {
    Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $package -Recurse -Force
    Copy-Item -LiteralPath $compressionDll -Destination $package -Force

    foreach ($name in @('app.publish', 'data', 'Temp', 'TEMP', 'plugins')) {
        $target = Join-Path $package $name
        Assert-StagePath $target
        if (Test-Path -LiteralPath $target) {
            Remove-Item -LiteralPath $target -Recurse -Force
        }
    }

    Get-ChildItem -LiteralPath $package -Recurse -File | Where-Object {
        $_.Extension -in @('.pdb', '.xml') -or
        $_.Name -in @('Jvedio.application', 'Jvedio.exe.manifest') -or
        $_.Name -match '^Jvedio\d+(?:\.\d+)?\.exe$'
    } | ForEach-Object {
        Assert-StagePath $_.FullName
        Remove-Item -LiteralPath $_.FullName -Force
    }

    $crawlerDest = Join-Path $package 'plugins\crawlers'
    New-Item -ItemType Directory -Path $crawlerDest -Force | Out-Null
    Get-ChildItem -LiteralPath $pluginAssets -Force |
        Copy-Item -Destination $crawlerDest -Recurse -Force

    $references = Join-Path $repoRoot 'Jvedio-WPF\Jvedio\Reference'
    Copy-Item -LiteralPath (Join-Path $references 'CommonNet.dll') -Destination $crawlerDest -Force
    Copy-Item -LiteralPath (Join-Path $references 'HtmlAgilityPack.dll') -Destination $crawlerDest -Force
    Copy-Item -LiteralPath $busDll -Destination (Join-Path $crawlerDest 'bus\BusCrawler.dll') -Force
    Copy-Item -LiteralPath $dbDll -Destination (Join-Path $crawlerDest 'db\DBCrawler.dll') -Force

    $required = @(
        'Jvedio.exe', 'Jvedio.exe.config', 'SuperUtils.dll',
        'SuperControls.Style.dll', 'MediaInfo.dll', 'System.Data.SQLite.dll',
        'System.IO.Compression.dll',
        'x64\SQLite.Interop.dll', 'x86\SQLite.Interop.dll',
        'AvalonEdit\Highlighting\LogRule.xshd',
        'plugins\crawlers\CommonNet.dll', 'plugins\crawlers\HtmlAgilityPack.dll',
        'plugins\crawlers\bus\main.json', 'plugins\crawlers\bus\BusCrawler.dll',
        'plugins\crawlers\db\main.json', 'plugins\crawlers\db\DBCrawler.dll',
        'plugins\crawlers\fc2\main.json', 'plugins\crawlers\fc2\FC2Crawler.dll',
        'plugins\crawlers\library\main.json', 'plugins\crawlers\library\LibraryCrawler.dll'
    )
    foreach ($relative in $required) {
        $path = Join-Path $package $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-Item -LiteralPath $path).Length -eq 0) {
            throw "Release package is missing: $relative"
        }
    }

    Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal -Force

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        foreach ($relative in $required) {
            $entry = "Jvedio-$Version/" + $relative.Replace('\', '/')
            if ($entry -notin $entryNames) {
                throw "ZIP verification failed: $entry"
            }
        }
    } finally {
        $archive.Dispose()
    }

    Write-Output (Get-Item -LiteralPath $zip)
} finally {
    $stageFull = [IO.Path]::GetFullPath($stage)
    $artifactPrefix = [IO.Path]::GetFullPath($artifacts) + [IO.Path]::DirectorySeparatorChar
    if (-not $stageFull.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside artifacts: $stageFull"
    }
    if (Test-Path -LiteralPath $stageFull) {
        Remove-Item -LiteralPath $stageFull -Recurse -Force
    }
}
