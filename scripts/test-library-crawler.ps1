param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Jvedio-WPF\Jvedio\bin\Release'),
    [string]$PluginPath = (Join-Path $PSScriptRoot '..\Jvedio-WPF\Jvedio\Core\Crawler\Library2\LibraryCrawler\bin\Release\LibraryCrawler.dll'),
    [string]$CompilerPath = 'D:\Visual Studio IDE\MSBuild\Current\Bin\Roslyn\csc.exe',
    [string]$ReferenceDirectory = (Join-Path $PSScriptRoot '..\build-output\refasm-net472\build\.NETFramework\v4.7.2'),
    [switch]$Legacy
)
$ErrorActionPreference = 'Stop'
$taskBuild = (Resolve-Path -LiteralPath $BuildDirectory).Path
$taskRefs = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
$taskPlugin = (Resolve-Path -LiteralPath $PluginPath).Path
$taskScratch = Join-Path ([IO.Path]::GetTempPath()) ('jvedio-library-crawler-' + [guid]::NewGuid().ToString('N'))
$taskRuntime = Join-Path $taskScratch 'runtime'
$taskCrawler = Join-Path $taskRuntime 'plugins\crawlers\library'
New-Item -ItemType Directory -Path $taskCrawler -Force | Out-Null
Get-ChildItem -LiteralPath $taskBuild -File | Where-Object {
    $_.Extension -in @('.dll', '.config') -or $_.Name -eq 'Jvedio.exe'
} | Copy-Item -Destination $taskRuntime
foreach ($taskName in @('x64', 'x86')) {
    Copy-Item -LiteralPath (Join-Path $taskBuild $taskName) -Destination (Join-Path $taskRuntime $taskName) -Recurse
}
$taskStaging = if ($Legacy) { $taskCrawler } else { Join-Path $taskCrawler 'temp' }
New-Item -ItemType Directory -Path $taskStaging -Force | Out-Null
Copy-Item -LiteralPath $taskPlugin -Destination (Join-Path $taskStaging 'LibraryCrawler.dll')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\release-assets\plugins\crawlers\library\main.json') -Destination $taskStaging
[IO.File]::WriteAllText((Join-Path $taskCrawler 'config.json'), '{"enabled":true}')
$taskExecutable = Join-Path $taskRuntime 'LibraryCrawlerRegression.exe'
$taskArguments = @('/nologo', '/target:exe', '/langversion:9.0', "/out:$taskExecutable")
$taskResources = Join-Path $PSScriptRoot '..\Jvedio-WPF\Jvedio\obj\Release\Jvedio.g.resources'
$taskArguments += "/resource:$taskResources,librarycrawlerregression.g.resources"
foreach ($taskName in @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml', 'System', 'System.Core', 'System.Data', 'System.Net.Http')) {
    $taskArguments += "/reference:$(Join-Path $taskRefs ($taskName + '.dll'))"
}
foreach ($taskName in @('Jvedio.exe', 'SuperUtils.dll', 'SuperControls.Style.dll', 'Newtonsoft.Json.dll', 'System.Data.SQLite.dll')) {
    $taskArguments += "/reference:$(Join-Path $taskBuild $taskName)"
}
$taskArguments += Join-Path $PSScriptRoot 'LibraryCrawlerRegression.cs'
& $CompilerPath @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Library crawler regression harness did not compile' }
Copy-Item -LiteralPath (Join-Path $taskBuild 'Jvedio.exe.config') -Destination ($taskExecutable + '.config')
$taskMode = if ($Legacy) { 'legacy' } else { 'checks' }
& $taskExecutable (Join-Path $taskScratch 'user') $taskMode
if ($LASTEXITCODE -ne 0) { throw "Library crawler regression failed ($taskScratch)" }
Write-Output "PASS: library crawler regression ($taskScratch)"
