param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Jvedio-WPF\Jvedio\bin\Release'),
    [string]$FixtureDirectory = (Join-Path $PSScriptRoot '..\build-output\clipper-fixtures'),
    [string]$CompilerPath = 'D:\Visual Studio IDE\MSBuild\Current\Bin\Roslyn\csc.exe',
    [string]$ReferenceDirectory = (Join-Path $PSScriptRoot '..\build-output\refasm-net472\build\.NETFramework\v4.7.2'),
    [string]$ScratchRoot = [IO.Path]::GetTempPath(),
    [switch]$Serve
)
$ErrorActionPreference = 'Stop'
$taskBuild = (Resolve-Path -LiteralPath $BuildDirectory).Path
$taskRefs = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
$taskFixtures = (Resolve-Path -LiteralPath $FixtureDirectory).Path
$taskScratch = Join-Path ([IO.Path]::GetFullPath($ScratchRoot)) ('jvedio-clipper-check-' + [guid]::NewGuid().ToString('N'))
$taskRuntime = Join-Path $taskScratch 'runtime'
New-Item -ItemType Directory -Path $taskRuntime -Force | Out-Null
Get-ChildItem -LiteralPath $taskBuild -File | Where-Object { $_.Extension -in @('.dll', '.config', '.ico') -or $_.Name -eq 'Jvedio.exe' } | Copy-Item -Destination $taskRuntime
foreach ($taskName in @('x64', 'x86', 'AvalonEdit')) {
    Copy-Item -LiteralPath (Join-Path $taskBuild $taskName) -Destination (Join-Path $taskRuntime $taskName) -Recurse
}
$taskExecutable = Join-Path $taskRuntime 'BrowserClipperRegression.exe'
$taskArguments = @('/nologo', '/target:exe', '/langversion:9.0', "/out:$taskExecutable")
$taskResources = Join-Path $PSScriptRoot '..\Jvedio-WPF\Jvedio\obj\Release\Jvedio.g.resources'
$taskArguments += "/resource:$taskResources,browserclipperregression.g.resources"
foreach ($taskName in @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml', 'System', 'System.Core', 'System.Data')) { $taskArguments += "/reference:$(Join-Path $taskRefs ($taskName + '.dll'))" }
foreach ($taskName in @('Jvedio.exe', 'SuperUtils.dll', 'SuperControls.Style.dll', 'Newtonsoft.Json.dll', 'System.Data.SQLite.dll')) { $taskArguments += "/reference:$(Join-Path $taskBuild $taskName)" }
$taskArguments += Join-Path $PSScriptRoot 'BrowserClipperRegression.cs'
& $CompilerPath @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Browser clipper regression harness did not compile' }
Copy-Item -LiteralPath (Join-Path $taskBuild 'Jvedio.exe.config') -Destination ($taskExecutable + '.config')
if ($Serve) {
    Remove-Item -LiteralPath (Join-Path $taskFixtures 'test-connection.json') -ErrorAction SilentlyContinue
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\browser-extension\icons\128.png') -Destination (Join-Path $taskFixtures 'test-image.png') -Force
    $taskServer = Start-Process -FilePath $taskExecutable -ArgumentList @(('"' + (Join-Path $taskScratch 'user') + '"'), ('"' + $taskFixtures + '"'), 'serve') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskFixtures 'server.log') -RedirectStandardError (Join-Path $taskFixtures 'server-error.log')
    Write-Output "Isolated receiver PID: $($taskServer.Id)"
    exit 0
}
& $taskExecutable (Join-Path $taskScratch 'user') $taskFixtures
if ($LASTEXITCODE -ne 0) { throw "Browser clipper checks failed ($taskScratch)" }
Write-Output "PASS: browser clipper regression checks ($taskScratch)"
