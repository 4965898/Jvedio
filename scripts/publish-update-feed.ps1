param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version,
    [string]$Repository = '4965898/Jvedio',
    [string]$ZipPath,
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $ZipPath) {
    $ZipPath = Join-Path $repoRoot "artifacts/Jvedio-$Version.zip"
}
$ZipPath = [IO.Path]::GetFullPath($ZipPath)
if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
    throw "Release ZIP is missing: $ZipPath"
}

$headers = @{
    'User-Agent' = 'Jvedio-update-feed'
    'Accept' = 'application/vnd.github+json'
}
if ($env:GH_TOKEN) {
    $headers.Authorization = "Bearer $env:GH_TOKEN"
}
$latest = if ($ValidateOnly) {
    [pscustomobject]@{
        tag_name = $Version
        published_at = [DateTimeOffset]::UtcNow
        body = "Jvedio $Version feed validation"
        assets = @([pscustomobject]@{ name = "Jvedio-$Version.zip" })
    }
} else {
    Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers
}
if ($latest.tag_name -ne $Version) {
    Write-Host "Latest published Release is $($latest.tag_name); skipping feed for $Version."
    return
}
if ("Jvedio-$Version.zip" -notin @($latest.assets | ForEach-Object name)) {
    throw "Published Release $Version is missing its complete ZIP."
}

$stage = Join-Path $repoRoot ('artifacts/.update-feed-' + [guid]::NewGuid().ToString('N'))
$extract = Join-Path $stage 'extract'
$checkout = Join-Path $stage 'checkout'
$temporaryBranch = 'update-feed-build-' + [guid]::NewGuid().ToString('N')
$package = Join-Path $extract "Jvedio-$Version"
$stageFull = [IO.Path]::GetFullPath($stage)
$checkoutFull = [IO.Path]::GetFullPath($checkout)
if (-not $checkoutFull.StartsWith($stageFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Temporary worktree is outside its staging directory: $checkoutFull"
}
New-Item -ItemType Directory -Path $extract -Force | Out-Null
try {
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $extract
    if (-not (Test-Path -LiteralPath (Join-Path $package 'Jvedio.exe') -PathType Leaf)) {
        throw 'Release ZIP does not contain the expected application folder.'
    }
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $package 'Jvedio.exe')).Version.ToString()
    if ($assemblyVersion -ne $Version) {
        throw "Release ZIP contains version $assemblyVersion, expected $Version."
    }

    git -C $repoRoot worktree add --detach $checkout HEAD | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not create temporary update-feed worktree.' }
    git -C $checkout checkout --orphan $temporaryBranch | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not create update-feed branch.' }
    git -C $checkout rm -rfq .
    if ($LASTEXITCODE -ne 0) { throw 'Could not clear temporary feed checkout.' }

    $feed = Join-Path $checkout 'jvedioupdate'
    $files = Join-Path $feed 'File'
    $updater = Join-Path $checkout 'SuperUpdate/File'
    New-Item -ItemType Directory -Path $files, $updater -Force | Out-Null
    $names = [Collections.Generic.List[string]]::new()
    $hashes = [Collections.Generic.List[string]]::new()
    foreach ($file in (Get-ChildItem -LiteralPath $package -Recurse -File | Sort-Object FullName)) {
        if ($file.Name -eq 'SuperUpdate.exe') { continue }
        $relative = $file.FullName.Substring($package.Length + 1).Replace('\', '/')
        $destination = Join-Path $files $relative
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
        $names.Add($relative)
        $hashes.Add((Get-FileHash -LiteralPath $file.FullName -Algorithm MD5).Hash.ToLowerInvariant())
    }

    $releaseNote = if ($latest.body) { [string]$latest.body } else { "Jvedio $Version" }
    $published = ([DateTimeOffset]$latest.published_at).ToOffset([TimeSpan]::FromHours(8)).ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
    @{
        LatestVersion = $Version
        ReleaseDate = $published
        ReleaseNote = @{ 'zh-CN' = $releaseNote; 'en-US' = $releaseNote }
    } | ConvertTo-Json -Depth 4 -Compress | Set-Content -LiteralPath (Join-Path $feed 'latest.json') -Encoding utf8 -NoNewline
    @{
        FileName = @($names)
        FileHash = @($hashes)
    } | ConvertTo-Json -Depth 3 -Compress | Set-Content -LiteralPath (Join-Path $feed 'list.json') -Encoding utf8 -NoNewline

    $updaterFile = Join-Path $package 'SuperUpdate.exe'
    if (-not (Test-Path -LiteralPath $updaterFile -PathType Leaf)) {
        throw 'Release ZIP has no SuperUpdate.exe.'
    }
    Copy-Item -LiteralPath $updaterFile -Destination (Join-Path $updater 'SuperUpdate.exe')
    @{ FileName = 'SuperUpdate.exe'; FileHash = (Get-FileHash -LiteralPath $updaterFile -Algorithm MD5).Hash.ToLowerInvariant() } |
        ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $checkout 'SuperUpdate/list.json') -Encoding utf8 -NoNewline

    $manifest = Get-Content -LiteralPath (Join-Path $feed 'list.json') -Raw | ConvertFrom-Json
    if ($manifest.FileName.Count -ne $names.Count -or
        $manifest.FileHash.Count -ne $names.Count -or
        'Jvedio.exe' -notin $manifest.FileName) {
        throw 'Generated update file list is incomplete.'
    }

    git -C $checkout add --all
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage update feed.' }
    git -C $checkout -c 'user.name=github-actions[bot]' -c 'user.email=41898282+github-actions[bot]@users.noreply.github.com' commit -m "Update feed for $Version" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not commit update feed.' }
    if (-not $ValidateOnly) {
        git -C $checkout push --force origin HEAD:refs/heads/update-feed
        if ($LASTEXITCODE -ne 0) { throw 'Could not publish update feed.' }
        Write-Host "Published update feed for $Version ($($names.Count) files)."
    } else {
        Write-Host "Validated update feed for $Version ($($names.Count) files)."
    }
} finally {
    git -C $repoRoot worktree remove --force $checkout 2>$null
    git -C $repoRoot branch -D $temporaryBranch 2>$null | Out-Null
    $artifactsPrefix = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    if (-not $stageFull.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove directory outside artifacts: $stageFull"
    }
    if (Test-Path -LiteralPath $stageFull) {
        Remove-Item -LiteralPath $stageFull -Recurse -Force
    }
}
