param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Jvedio-WPF\Jvedio\bin\Release'),
    [string]$CompilerPath = 'D:\Visual Studio IDE\MSBuild\Current\Bin\Roslyn\csc.exe',
    [string]$ReferenceDirectory = (Join-Path $PSScriptRoot '..\build-output\refasm-net472\build\.NETFramework\v4.7.2'),
    [switch]$Library,
    [switch]$VideoInfo,
    [string]$FFmpegPath,
    [string]$SampleVideoPath,
    [string]$PreviewDirectory = (Join-Path $PSScriptRoot '..\build-output\library-previews')
)
$ErrorActionPreference = 'Stop'
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
$refs = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('jvedio-startup-check-' + [guid]::NewGuid().ToString('N'))
$runtime = Join-Path $scratch 'runtime'
New-Item -ItemType Directory -Path (Join-Path $runtime 'plugins\crawlers') -Force | Out-Null
Get-ChildItem -LiteralPath $build -File |
    Where-Object { $_.Extension -in @('.dll', '.config', '.ico') -or $_.Name -eq 'Jvedio.exe' } |
    Copy-Item -Destination $runtime
foreach ($name in @('x64', 'x86', 'AvalonEdit')) {
    Copy-Item -LiteralPath (Join-Path $build $name) -Destination (Join-Path $runtime $name) -Recurse
}
$probeExecutable = Join-Path $runtime 'StartupRegression.exe'
$arguments = @('/nologo', '/target:exe', '/main:StartupRegression', "/out:$probeExecutable")
if ($VideoInfo.IsPresent) { $arguments += '/platform:x86' } # The bundled native MediaInfo.dll is x86.
$resources = Join-Path $PSScriptRoot '..\Jvedio-WPF\Jvedio\obj\Release\Jvedio.g.resources'
$arguments += "/resource:$resources,startupregression.g.resources"
foreach ($name in @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml', 'System', 'System.Core')) {
    $arguments += "/reference:$(Join-Path $refs ($name + '.dll'))"
}
foreach ($name in @('Jvedio.exe', 'SuperUtils.dll', 'SuperControls.Style.dll', 'Newtonsoft.Json.dll', 'System.Data.SQLite.dll')) {
    $arguments += "/reference:$(Join-Path $build $name)"
}
$arguments += Join-Path $PSScriptRoot 'StartupRegression.cs'
$arguments += Join-Path $PSScriptRoot 'LibraryRegression.cs'
$arguments += Join-Path $PSScriptRoot 'VideoInfoRegression.cs'
& $CompilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Startup regression harness did not compile' }
Copy-Item -LiteralPath (Join-Path $build 'Jvedio.exe.config') -Destination ($probeExecutable + '.config') -Force
if ($VideoInfo.IsPresent) {
    if ($Library.IsPresent) { throw 'Select either -VideoInfo or -Library' }
    if (-not $FFmpegPath) { $FFmpegPath = (Get-Command ffmpeg -ErrorAction Stop).Source }
    $mediaOutput = Join-Path $scratch 'media'
    New-Item -ItemType Directory -Path $mediaOutput -Force | Out-Null
    foreach ($media in @(@('first', '160x120'), @('second', '320x180'))) {
        & $FFmpegPath -hide_banner -loglevel error -f lavfi -i "testsrc=size=$($media[1]):rate=25" -f lavfi -i 'sine=frequency=440:sample_rate=44100' -t 1 -c:v libx264 -pix_fmt yuv420p -c:a aac -y (Join-Path $mediaOutput ($media[0] + '.mp4'))
        if ($LASTEXITCODE -ne 0) { throw 'Could not generate the isolated media fixture' }
    }
    & $probeExecutable 'video-info' (Join-Path $scratch 'fixture') $mediaOutput $SampleVideoPath
    New-Item -ItemType Directory -Path $PreviewDirectory -Force | Out-Null
    Get-ChildItem -LiteralPath $mediaOutput -Filter '*.png' | Copy-Item -Destination $PreviewDirectory
} elseif ($Library.IsPresent) {
    & $probeExecutable 'library' (Join-Path $scratch 'fixture') ([IO.Path]::GetFullPath($PreviewDirectory))
} else { & $probeExecutable 'checks' (Join-Path $scratch 'fixture') }
if ($LASTEXITCODE -ne 0) { throw "Regression checks failed ($scratch)" }
Write-Output "PASS: regression checks ($scratch)"
