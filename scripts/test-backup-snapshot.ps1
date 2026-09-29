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
$archiveDb = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$(Join-Path $extracted 'app_datas.sqlite');Version=3;Read Only=True;")
$archiveDb.Open()
$archiveQuery = $archiveDb.CreateCommand()
$archiveQuery.CommandText = 'SELECT value FROM items LIMIT 1'
$archiveValue = [string]$archiveQuery.ExecuteScalar()
$archiveQuery.Dispose()
$archiveDb.Dispose()
if ($archiveValue -ne 'committed-in-wal') { throw "ZIP snapshot lost WAL data: $archiveValue" }

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
if ($settingsType.GetProperty('MaxLocalBackups').GetValue($settings) -ne 10) {
    throw 'New settings did not default to a 10-snapshot retention limit'
}
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
$backupRoot = Join-Path $scratch 'local-backups'
$unmanaged = Join-Path $backupRoot '2000-01-01'
New-Item -ItemType Directory -Path $unmanaged | Out-Null
Copy-Item -LiteralPath (Join-Path $localModeResult.LocalFolder 'app_configs.sqlite') -Destination $unmanaged
Copy-Item -LiteralPath (Join-Path $localModeResult.LocalFolder 'app_datas.sqlite') -Destination $unmanaged
[System.IO.File]::WriteAllText((Join-Path $unmanaged 'notes.txt'), 'preserve this folder')
$settingsType.GetProperty('MaxLocalBackups').SetValue($settings, 2)
Start-Sleep -Milliseconds 5
$retained1 = $type.GetMethod('CreateAsync').Invoke($null, @()).GetAwaiter().GetResult()
Start-Sleep -Milliseconds 5
$retained2 = $type.GetMethod('CreateAsync').Invoke($null, @()).GetAwaiter().GetResult()
$managed = @(Get-ChildItem -LiteralPath $backupRoot -Directory | Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}_\d{6}_\d{3}$' })
if ($managed.Count -ne 2 -or -not (Test-Path -LiteralPath $retained2.LocalFolder) -or
    -not (Test-Path -LiteralPath (Join-Path $unmanaged 'notes.txt')) -or $retained2.RetentionError) {
    throw "Local retention failed: managed=$($managed.Count), latest=$($retained2.LocalFolder), error=$($retained2.RetentionError), unmanaged=$(Test-Path -LiteralPath (Join-Path $unmanaged 'notes.txt'))"
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
                } elseif ($context.Request.HttpMethod -eq 'PROPFIND' -and [System.IO.Directory]::Exists($file)) {
                    $xml = '<d:multistatus xmlns:d="DAV:">'
                    $xml += '<d:response><d:href>' + [System.Security.SecurityElement]::Escape($context.Request.Url.AbsolutePath) + '</d:href></d:response>'
                    foreach ($item in Get-ChildItem -LiteralPath $file -File) {
                        $href = $context.Request.Url.AbsolutePath.TrimEnd('/') + '/' + [Uri]::EscapeDataString($item.Name)
                        $xml += '<d:response><d:href>' + [System.Security.SecurityElement]::Escape($href) + '</d:href><d:propstat><d:prop>'
                        $xml += '<d:getcontentlength>' + $item.Length + '</d:getcontentlength>'
                        $xml += '<d:getlastmodified>' + $item.LastWriteTimeUtc.ToString('r') + '</d:getlastmodified>'
                        $xml += '</d:prop></d:propstat></d:response>'
                    }
                    $xml += '</d:multistatus>'
                    $bytes = [System.Text.Encoding]::UTF8.GetBytes($xml)
                    $context.Response.StatusCode = 207
                    $context.Response.ContentType = 'application/xml; charset=utf-8'
                    $context.Response.ContentLength64 = $bytes.Length
                    $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                } elseif ($context.Request.HttpMethod -eq 'GET' -and $context.Request.Url.Query.Contains('list-type=2')) {
                    $prefix = ''
                    $token = ''
                    foreach ($pair in $context.Request.Url.Query.TrimStart('?').Split('&')) {
                        if ($pair.StartsWith('prefix=')) { $prefix = [Uri]::UnescapeDataString($pair.Substring(7)) }
                        if ($pair.StartsWith('continuation-token=')) { $token = [Uri]::UnescapeDataString($pair.Substring(19)) }
                    }
                    $bucketRoot = Join-Path $root $key
                    $objects = @(Get-ChildItem -LiteralPath $bucketRoot -File -Filter '*.zip' -Recurse | Sort-Object FullName | Where-Object {
                        $_.FullName.Substring($bucketRoot.Length).TrimStart('\').Replace('\', '/').StartsWith($prefix)
                    })
                    $page = if ($token -eq 'page2') { @($objects | Select-Object -Skip 1) } else { @($objects | Select-Object -First 1) }
                    $truncated = $token -ne 'page2' -and $objects.Count -gt 1
                    $xml = '<ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><IsTruncated>' + $truncated.ToString().ToLowerInvariant() + '</IsTruncated>'
                    if ($truncated) { $xml += '<NextContinuationToken>page2</NextContinuationToken>' }
                    foreach ($item in $page) {
                        $objectKey = $item.FullName.Substring($bucketRoot.Length).TrimStart('\').Replace('\', '/')
                        $xml += '<Contents><Key>' + [System.Security.SecurityElement]::Escape($objectKey) + '</Key>'
                        $xml += '<Size>' + $item.Length + '</Size><LastModified>' + $item.LastWriteTimeUtc.ToString('yyyy-MM-ddTHH:mm:ss.fffZ') + '</LastModified></Contents>'
                    }
                    $xml += '</ListBucketResult>'
                    $bytes = [System.Text.Encoding]::UTF8.GetBytes($xml)
                    $context.Response.ContentType = 'application/xml; charset=utf-8'
                    $context.Response.ContentLength64 = $bytes.Length
                    $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
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
    $davList = $remote.GetMethod('ListBackupsAsync').Invoke($null, @()).GetAwaiter().GetResult()
    if ($davList.Count -lt 2 -or -not ($davList | Where-Object FileName -eq 'snapshot.zip')) {
        throw 'WebDAV backup list did not include older and newer ZIP files'
    }
    $davSelected = $remote.GetMethod('DownloadAsync').Invoke($null, [object[]]@('snapshot.zip', [string](Join-Path $scratch 'dav-selected'))).GetAwaiter().GetResult()
    if ((Get-FileHash -LiteralPath $davSelected).Hash -ne (Get-FileHash -LiteralPath $archive).Hash) {
        throw 'Selected older WebDAV backup differs'
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
    # Independently verify that the signed canonical query matches the URL sent to S3.
    function Get-HmacBytes([byte[]]$key, [string]$value) {
        $hmac = [System.Security.Cryptography.HMACSHA256]::new($key)
        try { return $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($value)) }
        finally { $hmac.Dispose() }
    }
    function Get-Sha256Hex([string]$value) {
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try { return [BitConverter]::ToString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($value))).Replace('-', '').ToLowerInvariant() }
        finally { $sha.Dispose() }
    }
    $payloadHash = Get-Sha256Hex ''
    $listQuery = 'continuation-token=page%2F2&list-type=2&prefix=snapshots%2F' + [Uri]::EscapeDataString($folderName) + '%2F'
    $signer = $remote.GetMethod('CreateS3Request', [System.Reflection.BindingFlags]'NonPublic,Static')
    $signed = $signer.Invoke($null, [object[]]@([System.Net.Http.HttpMethod]::Get, '/jvedio-test', $listQuery, $payloadHash))
    try {
        $stamp = ($signed.Headers.GetValues('x-amz-date') | Select-Object -First 1)
        $scope = $stamp.Substring(0, 8) + '/us-east-1/s3/aws4_request'
        $canonical = "GET`n$($signed.RequestUri.AbsolutePath)`n$($signed.RequestUri.Query.TrimStart('?'))`n"
        $canonical += "host:$($signed.Headers.Host)`nx-amz-content-sha256:$payloadHash`nx-amz-date:$stamp`n`n"
        $canonical += "host;x-amz-content-sha256;x-amz-date`n$payloadHash"
        $toSign = "AWS4-HMAC-SHA256`n$stamp`n$scope`n$(Get-Sha256Hex $canonical)"
        $kDate = Get-HmacBytes ([System.Text.Encoding]::UTF8.GetBytes('AWS4test-secret')) $stamp.Substring(0, 8)
        $kRegion = Get-HmacBytes $kDate 'us-east-1'
        $kService = Get-HmacBytes $kRegion 's3'
        $kSigning = Get-HmacBytes $kService 'aws4_request'
        $expectedSignature = [BitConverter]::ToString((Get-HmacBytes $kSigning $toSign)).Replace('-', '').ToLowerInvariant()
        $authorization = ($signed.Headers.GetValues('Authorization') | Select-Object -First 1)
        if ($authorization -notlike "*Signature=$expectedSignature") {
            throw 'S3 list request signature did not match the transmitted path and query'
        }
    } finally { $signed.Dispose() }
    Write-Output 'Testing S3 upload'
    $remote.GetMethod('UploadAsync').Invoke($null, [object[]]@($archive)).GetAwaiter().GetResult()
    Write-Output 'Testing S3 download'
    $s3 = $remote.GetMethod('DownloadLatestAsync').Invoke($null, [object[]]@([string](Join-Path $scratch 's3-download'))).GetAwaiter().GetResult()
    if ((Get-FileHash -LiteralPath $s3).Hash -ne (Get-FileHash -LiteralPath $archive).Hash) { throw 'S3 download differs' }
    if (-not (Test-Path -LiteralPath (Join-Path $storeRoot "jvedio-test\snapshots\$folderName\latest.json"))) {
        throw 'S3 backup was not stored under the selected folder prefix'
    }
    $modeProperty.SetValue($settings, 'RemoteOnly')
    $s3ModeResult = $type.GetMethod('CreateAsync').Invoke($null, @()).GetAwaiter().GetResult()
    if ($s3ModeResult.RemoteError -or $s3ModeResult.LocalFolder) { throw 'S3 online-only backup failed' }
    $s3List = $remote.GetMethod('ListBackupsAsync').Invoke($null, @()).GetAwaiter().GetResult()
    if ($s3List.Count -lt 2 -or -not ($s3List | Where-Object FileName -eq 'snapshot.zip')) {
        throw 'S3 paged backup list did not include older and newer ZIP files'
    }
    $s3Selected = $remote.GetMethod('DownloadAsync').Invoke($null, [object[]]@('snapshot.zip', [string](Join-Path $scratch 's3-selected'))).GetAwaiter().GetResult()
    if ((Get-FileHash -LiteralPath $s3Selected).Hash -ne (Get-FileHash -LiteralPath $archive).Hash) {
        throw 'Selected older S3 backup differs'
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
Write-Output "PASS: WAL snapshot, restore, retention, three modes, remote lists and transports ($scratch)"
