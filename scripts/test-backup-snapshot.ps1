param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Jvedio-WPF\Jvedio\bin\Release')
)

$ErrorActionPreference = 'Stop'
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
Add-Type -Path (Join-Path $build 'System.Data.SQLite.dll')
$assembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $build 'Jvedio.exe'))
$type = $assembly.GetType('Jvedio.Core.Backup.BackupService', $true)
$flags = [System.Reflection.BindingFlags]'NonPublic,Static'
$copy = $type.GetMethod('CopySqliteSnapshot', $flags)
if ($null -eq $copy) { throw 'CopySqliteSnapshot not found' }

$scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('jvedio-backup-test-' + [guid]::NewGuid().ToString('N'))
$snapshot = Join-Path $scratch 'snapshot'
New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
$source = Join-Path $scratch 'live.sqlite'
$target = Join-Path $snapshot 'app_datas.sqlite'

$connection = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$source;Version=3;")
$connection.Open()
$command = $connection.CreateCommand()
$command.CommandText = 'PRAGMA journal_mode=WAL; CREATE TABLE items(value TEXT); INSERT INTO items(value) VALUES (''committed-in-wal'');'
[void]$command.ExecuteNonQuery()

# The source connection stays open so committed pages may still reside in the WAL.
[void]$copy.Invoke($null, [object[]]@([string]$source, [string]$target))
[void]$copy.Invoke($null, [object[]]@([string]$source, [string](Join-Path $snapshot 'app_configs.sqlite')))
$type.GetMethod('ValidateFolder').Invoke($null, [object[]]@([string]$snapshot))
$read = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$target;Version=3;Read Only=True;")
$read.Open()
$query = $read.CreateCommand()
$query.CommandText = 'SELECT value FROM items LIMIT 1'
$value = [string]$query.ExecuteScalar()
$query.Dispose()
$read.Dispose()
$connection.Dispose()
if ($value -ne 'committed-in-wal') { throw "WAL snapshot lost committed data: $value" }

$snapshotImage = Join-Path $snapshot 'image\library'
New-Item -ItemType Directory -Path $snapshotImage -Force | Out-Null
[System.IO.File]::WriteAllText((Join-Path $snapshotImage 'poster.jpg'), 'new-image')

$archive = [string]$type.GetMethod('CreateArchive').Invoke($null, [object[]]@([string]$snapshot))
if (-not (Test-Path -LiteralPath $archive)) { throw 'Archive was not created' }
$extracted = Join-Path $scratch 'extracted'
New-Item -ItemType Directory -Path $extracted | Out-Null
$type.GetMethod('ExtractArchive', $flags).Invoke($null, [object[]]@([string]$archive, [string]$extracted))
$type.GetMethod('ValidateFolder').Invoke($null, [object[]]@([string]$extracted))

# Restore into an isolated user data directory and check the old data rollback.
$liveRoot = Join-Path $scratch 'live-user'
New-Item -ItemType Directory -Path (Join-Path $liveRoot 'image\library') -Force | Out-Null
[System.IO.File]::WriteAllText((Join-Path $liveRoot 'image\library\poster.jpg'), 'old-image')
foreach ($name in @('app_configs.sqlite', 'app_datas.sqlite')) {
    $old = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$(Join-Path $liveRoot $name);Version=3;")
    $old.Open()
    $insert = $old.CreateCommand()
    $insert.CommandText = "CREATE TABLE items(value TEXT); INSERT INTO items(value) VALUES ('old-data');"
    [void]$insert.ExecuteNonQuery()
    $insert.Dispose()
    $old.Dispose()
}
$paths = $assembly.GetType('Jvedio.Core.Global.PathManager', $true)
$paths.GetProperty('CurrentUserFolder').SetValue($null, $liveRoot)
$settingsType = $assembly.GetType('Jvedio.Core.WindowConfig.Settings', $true)
$settings = $settingsType.GetMethod('CreateInstance').Invoke($null, @())
$configType = $assembly.GetType('Jvedio.ConfigManager', $true)
$configType.GetProperty('Settings').SetValue($null, $settings)
$settingsType.GetProperty('BackupDirectory').SetValue($settings, (Join-Path $scratch 'local-backups'))
$modeProperty = $settingsType.GetProperty('BackupMode')
if ($modeProperty.GetValue($settings) -ne 'LocalOnly') { throw 'New settings did not default to local backup' }
$settingsType.GetProperty('BackupRemoteType').SetValue($settings, 'WebDAV')
if ($modeProperty.GetValue($settings) -ne 'Both') { throw 'Existing remote configuration did not retain local plus online mode' }
$settingsType.GetProperty('BackupRemoteType').SetValue($settings, 'None')
$type.GetMethod('StageRestore').Invoke($null, [object[]]@([string]$archive))
$type.GetMethod('ApplyPendingRestore').Invoke($null, @())
$restored = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$(Join-Path $liveRoot 'app_datas.sqlite');Version=3;Read Only=True;")
$restored.Open()
$restoredCommand = $restored.CreateCommand()
$restoredCommand.CommandText = 'SELECT value FROM items LIMIT 1'
$restoredValue = [string]$restoredCommand.ExecuteScalar()
$restoredCommand.Dispose()
$restored.Dispose()
if ($restoredValue -ne 'committed-in-wal') { throw "Restore produced unexpected data: $restoredValue" }
if ([System.IO.File]::ReadAllText((Join-Path $liveRoot 'image\library\poster.jpg')) -ne 'new-image') {
    throw 'Restore did not replace images'
}
$rollback = Get-ChildItem $liveRoot -Directory -Filter 'restore-rollback-*' | Select-Object -First 1
if (-not $rollback -or -not (Test-Path (Join-Path $rollback.FullName 'app_datas.sqlite')) -or
    [System.IO.File]::ReadAllText((Join-Path $rollback.FullName 'image\library\poster.jpg')) -ne 'old-image') {
    throw 'Restore did not preserve the previous data'
}
$corrupt = Join-Path $scratch 'corrupt-backup'
Copy-Item -LiteralPath $extracted -Destination $corrupt -Recurse
[System.IO.File]::WriteAllBytes((Join-Path $corrupt 'app_datas.sqlite'), (New-Object byte[] 128))
try {
    $type.GetMethod('StageRestore').Invoke($null, [object[]]@([string]$corrupt))
    throw 'Corrupt backup was accepted'
} catch {
    if ($_.Exception.Message -notmatch 'database|integrity|校验') { throw }
}
if (Test-Path (Join-Path $liveRoot '.pending-restore')) { throw 'Corrupt backup left a restore marker' }

$localBackup = [string]$type.GetMethod('CreateLocal').Invoke($null, @())
$type.GetMethod('ValidateFolder').Invoke($null, [object[]]@([string]$localBackup))
[System.IO.File]::AppendAllText((Join-Path $localBackup 'image\library\poster.jpg'), 'tampered')
try {
    $type.GetMethod('ValidateFolder').Invoke($null, [object[]]@([string]$localBackup))
    throw 'Corrupt backup image was accepted'
} catch {
if ($_.Exception.Message -like '*was accepted*') { throw }
}
$modeProperty.SetValue($settings, 'LocalOnly')
$localModeResult = $type.GetMethod('CreateAsync').Invoke($null, @()).GetAwaiter().GetResult()
if ($localModeResult.Mode -ne 'LocalOnly' -or -not (Test-Path -LiteralPath $localModeResult.LocalFolder) -or
    $localModeResult.RemoteType -or $localModeResult.RemoteError) {
    throw 'Local-only backup mode produced the wrong result'
}

# Exercise both transports against a loopback object store. The mock checks the
# uploaded payload and returns the same objects for remote restore.
$tcp = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
$tcp.Start()
$port = ([System.Net.IPEndPoint]$tcp.LocalEndpoint).Port
$tcp.Stop()
$storeRoot = Join-Path $scratch 'object-store'
New-Item -ItemType Directory -Path $storeRoot | Out-Null
New-Item -ItemType Directory -Path (Join-Path $storeRoot 'dav') | Out-Null
$server = Start-Job -ArgumentList $port, $storeRoot -ScriptBlock {
    param($listenPort, $root)
    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add("http://127.0.0.1:$listenPort/")
    $listener.Start()
    try {
        while ($listener.IsListening) {
            $context = $listener.GetContext()
            Write-Output "request $($context.Request.HttpMethod) $($context.Request.Url.AbsolutePath)"
            $stop = $context.Request.Url.AbsolutePath -eq '/__stop'
            try {
                $key = [Uri]::UnescapeDataString($context.Request.Url.AbsolutePath.TrimStart('/'))
                $file = Join-Path $root ($key.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
                if ($context.Request.Url.AbsolutePath -eq '/__ready' -or $stop) {
                    $context.Response.StatusCode = 200
                } elseif ($context.Request.HttpMethod -eq 'MKCOL') {
                    $collection = $file.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
                    if ([System.IO.Directory]::Exists($collection)) { $context.Response.StatusCode = 405 }
                    elseif ([System.IO.Directory]::Exists([System.IO.Path]::GetDirectoryName($collection))) {
                        [System.IO.Directory]::CreateDirectory($collection) | Out-Null
                        $context.Response.StatusCode = 201
                    } else { $context.Response.StatusCode = 409 }
                } elseif ($context.Request.HttpMethod -eq 'PUT') {
                    $parent = [System.IO.Path]::GetDirectoryName($file)
                    if ($key -like '*/upload-denied/*') {
                        $context.Request.InputStream.CopyTo([System.IO.Stream]::Null)
                        $context.Response.StatusCode = 403
                    } elseif ($key.StartsWith('dav/') -and -not [System.IO.Directory]::Exists($parent)) {
                        $context.Response.StatusCode = 409
                    } else {
                        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
                        $output = [System.IO.File]::Create($file)
                        try { $context.Request.InputStream.CopyTo($output) } finally { $output.Dispose() }
                        $context.Response.StatusCode = 201
                    }
                } elseif ($context.Request.HttpMethod -eq 'GET' -and [System.IO.File]::Exists($file)) {
                    $bytes = [System.IO.File]::ReadAllBytes($file)
                    $context.Response.ContentLength64 = $bytes.Length
                    $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                } elseif ($context.Request.HttpMethod -eq 'DELETE' -and [System.IO.File]::Exists($file)) {
                    if ($key -like '*/cleanup-denied/*') { $context.Response.StatusCode = 403 }
                    else {
                        [System.IO.File]::Delete($file)
                        $context.Response.StatusCode = 204
                    }
                } else { $context.Response.StatusCode = 404 }
            } catch { $context.Response.StatusCode = 500 }
            finally { $context.Response.Close() }
            Write-Output 'response complete'
            if ($stop) { break }
        }
    } finally { $listener.Stop() }
}
for ($attempt = 0; $attempt -lt 40; $attempt++) {
    if ($server.State -ne 'Running') {
        $details = Receive-Job $server -ErrorAction SilentlyContinue | Out-String
        throw "Mock server failed to start: $($server.State) $details"
    }
    try {
        [void](Invoke-WebRequest "http://127.0.0.1:$port/__ready" -TimeoutSec 1 -UseBasicParsing)
        break
    } catch {
        if ($attempt -eq 39) { throw "Mock server did not become ready on port $port" }
        Start-Sleep -Milliseconds 150
    }
}
try {
    $remote = $assembly.GetType('Jvedio.Core.Backup.RemoteBackupStore', $true)
    $folderName = -join @([char]0x5728, [char]0x7EBF, [char]0x5907, [char]0x4EFD)
    $settingsType.GetProperty('BackupRemoteType').SetValue($settings, 'WebDAV')
    $settingsType.GetProperty('BackupWebDavUrl').SetValue($settings, "http://127.0.0.1:$port/dav")
    $settingsType.GetProperty('BackupWebDavFolder').SetValue($settings, "Jvedio/$folderName")
    $modeProperty.SetValue($settings, 'RemoteOnly')
    $localBefore = @(Get-ChildItem -LiteralPath (Join-Path $scratch 'local-backups') -Directory).Count
    $tempBefore = @(Get-ChildItem -LiteralPath ([System.IO.Path]::GetTempPath()) -Directory -Filter 'Jvedio-online-backup-*' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)
    $remoteOnlyResult = $type.GetMethod('CreateAsync').Invoke($null, @()).GetAwaiter().GetResult()
    $localAfter = @(Get-ChildItem -LiteralPath (Join-Path $scratch 'local-backups') -Directory).Count
    $tempAfter = @(Get-ChildItem -LiteralPath ([System.IO.Path]::GetTempPath()) -Directory -Filter 'Jvedio-online-backup-*' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)
    if ($remoteOnlyResult.Mode -ne 'RemoteOnly' -or $remoteOnlyResult.LocalFolder -or
        $remoteOnlyResult.RemoteError -or $remoteOnlyResult.CleanupError -or $localAfter -ne $localBefore -or
        @($tempAfter | Where-Object { $_ -notin $tempBefore }).Count -gt 0) {
        throw 'Online-only backup retained a local snapshot or failed to upload'
    }
    Write-Output 'Testing WebDAV upload'
    $upload = $remote.GetMethod('UploadAsync').Invoke($null, [object[]]@($archive))
    try {
        if (-not $upload.Wait(10000)) { throw 'WebDAV upload timeout' }
    } catch { throw "WebDAV upload failed: $($_.Exception.ToString())" }
    $upload.GetAwaiter().GetResult()
    Write-Output 'Testing WebDAV download'
    $dav = $remote.GetMethod('DownloadLatestAsync').Invoke($null, [object[]]@([string](Join-Path $scratch 'dav-download'))).GetAwaiter().GetResult()
    if ((Get-FileHash -LiteralPath $dav).Hash -ne (Get-FileHash -LiteralPath $archive).Hash) { throw 'WebDAV download differs' }
    if (-not (Test-Path -LiteralPath (Join-Path $storeRoot "dav\Jvedio\$folderName\latest.json"))) {
        throw 'WebDAV backup was not stored in the selected remote folder'
    }
    $davTest = $remote.GetMethod('TestConnectionAsync').Invoke($null, [object[]]@('WebDAV')).GetAwaiter().GetResult()
    if ($davTest) { throw "WebDAV test object was not removed: $davTest" }
    $modeProperty.SetValue($settings, 'Both')
    $bothResult = $type.GetMethod('CreateAsync').Invoke($null, @()).GetAwaiter().GetResult()
    if ($bothResult.Mode -ne 'Both' -or -not (Test-Path -LiteralPath $bothResult.LocalFolder) -or
        $bothResult.RemoteType -ne 'WebDAV' -or $bothResult.RemoteError -or
        (Test-Path -LiteralPath ($bothResult.LocalFolder + '.zip'))) {
        throw 'Local-and-online backup mode produced the wrong result'
    }
    $settingsType.GetProperty('BackupWebDavFolder').SetValue($settings, '')
    $remote.GetMethod('UploadAsync').Invoke($null, [object[]]@($archive)).GetAwaiter().GetResult()
    if (-not (Test-Path -LiteralPath (Join-Path $storeRoot 'dav\latest.json'))) {
        throw 'Empty WebDAV subfolder did not preserve the original backup location'
    }

    $settingsType.GetProperty('BackupRemoteType').SetValue($settings, 'S3')
    $settingsType.GetProperty('BackupS3Endpoint').SetValue($settings, "http://127.0.0.1:$port")
    $settingsType.GetProperty('BackupS3Region').SetValue($settings, 'us-east-1')
    $settingsType.GetProperty('BackupS3Bucket').SetValue($settings, 'jvedio-test')
    $settingsType.GetProperty('BackupS3Prefix').SetValue($settings, "snapshots/$folderName")
    $settingsType.GetProperty('BackupS3AccessKey').SetValue($settings, 'TESTACCESS')
    $secret = $remote.GetMethod('Protect').Invoke($null, [object[]]@('test-secret'))
    $settingsType.GetProperty('BackupS3SecretKeyProtected').SetValue($settings, $secret)
    Write-Output 'Testing S3 upload'
    $remote.GetMethod('UploadAsync').Invoke($null, [object[]]@($archive)).GetAwaiter().GetResult()
    Write-Output 'Testing S3 download'
    $s3 = $remote.GetMethod('DownloadLatestAsync').Invoke($null, [object[]]@([string](Join-Path $scratch 's3-download'))).GetAwaiter().GetResult()
    if ((Get-FileHash -LiteralPath $s3).Hash -ne (Get-FileHash -LiteralPath $archive).Hash) { throw 'S3 download differs' }
    if (-not (Test-Path -LiteralPath (Join-Path $storeRoot "jvedio-test\snapshots\$folderName\latest.json"))) {
        throw 'S3 backup was not stored under the selected folder prefix'
    }
    $s3Test = $remote.GetMethod('TestConnectionAsync').Invoke($null, [object[]]@('S3')).GetAwaiter().GetResult()
    if ($s3Test) { throw "S3 test object was not removed: $s3Test" }
    if (Get-ChildItem -LiteralPath $storeRoot -Filter '.jvedio-connection-test-*' -File -Recurse -Force) {
        throw 'A connection test object was left behind'
    }
    $modeProperty.SetValue($settings, 'Both')
    $settingsType.GetProperty('BackupS3Prefix').SetValue($settings, 'upload-denied')
    $partialResult = $type.GetMethod('CreateAsync').Invoke($null, @()).GetAwaiter().GetResult()
    if (-not (Test-Path -LiteralPath $partialResult.LocalFolder) -or -not $partialResult.RemoteError -or
        (Test-Path -LiteralPath ($partialResult.LocalFolder + '.zip'))) {
        throw 'Partial backup failure did not retain the local snapshot and report remote failure'
    }
    $settingsType.GetProperty('BackupS3Prefix').SetValue($settings, 'cleanup-denied')
    $denyTest = $remote.GetMethod('TestConnectionAsync').Invoke($null, [object[]]@('S3')).GetAwaiter().GetResult()
    $denyFile = Join-Path $storeRoot "jvedio-test\cleanup-denied\$denyTest"
    if (-not $denyTest -or -not (Test-Path -LiteralPath $denyFile)) {
        throw 'Delete denial was not reported with the remaining test object name'
    }
    Remove-Item -LiteralPath $denyFile
} finally {
    Write-Output "mock state: $($server.State)"
    Receive-Job $server -ErrorAction SilentlyContinue
    try { [void](Invoke-WebRequest "http://127.0.0.1:$port/__stop" -TimeoutSec 2 -UseBasicParsing) } catch { }
    [void](Wait-Job $server -Timeout 3)
    Remove-Job $server -Force -ErrorAction SilentlyContinue
}
Write-Output "PASS: WAL snapshot, restore, three backup modes, WebDAV/S3 transport and connection tests ($scratch)"
