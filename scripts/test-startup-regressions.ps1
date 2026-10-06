param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Jvedio-WPF\Jvedio\bin\Release'),
    [string]$CompilerPath = 'D:\Visual Studio IDE\MSBuild\Current\Bin\Roslyn\csc.exe',
    [string]$ReferenceDirectory = (Join-Path $PSScriptRoot '..\build-output\refasm-net472\build\.NETFramework\v4.7.2')
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
$output = Join-Path $runtime 'StartupRegression.exe'
$arguments = @('/nologo', '/target:exe', '/main:StartupRegression', "/out:$output")
$resources = Join-Path $PSScriptRoot '..\Jvedio-WPF\Jvedio\obj\Release\Jvedio.g.resources'
$arguments += "/resource:$resources,startupregression.g.resources"
foreach ($name in @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml', 'System', 'System.Core')) {
    $arguments += "/reference:$(Join-Path $refs ($name + '.dll'))"
}
foreach ($name in @('Jvedio.exe', 'SuperUtils.dll', 'SuperControls.Style.dll', 'Newtonsoft.Json.dll')) {
    $arguments += "/reference:$(Join-Path $build $name)"
}
$arguments += Join-Path $PSScriptRoot 'StartupRegression.cs'
& $CompilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Startup regression harness did not compile' }
Copy-Item -LiteralPath (Join-Path $build 'Jvedio.exe.config') -Destination ($output + '.config') -Force
Push-Location $runtime
try {
    & $output checks (Join-Path $scratch 'fixture')
    if ($LASTEXITCODE -ne 0) { throw "Startup regression checks failed ($scratch)" }
} finally { Pop-Location }
Write-Output "PASS: startup configuration, ZIP period, concurrent backups and tag keyboard menus ($scratch)"
