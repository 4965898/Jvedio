using Jvedio.Core.Config;
using Jvedio.Core.Global;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jvedio.Core.Backup
{
    /// <summary>Creates consistent SQLite snapshots and stages a restore before any mapper is opened.</summary>
    public static class BackupService
    {
        private const string ManifestName = "backup-manifest.json";
        private const string PendingName = ".pending-restore";
        private const string StageName = ".restore-stage";
        private static readonly string[] DatabaseNames = { "app_configs.sqlite", "app_datas.sqlite" };
        private static readonly string[] DatabaseSidecars = { "-wal", "-shm", "-journal" };
        private static readonly SemaphoreSlim BackupGate = new SemaphoreSlim(1, 1);

        /// <summary>只读取快照名称，兼容 ZIP 和旧文件夹，不在周期检查时解压或校验整库。</summary>
        public static DateTime GetLatestLocalBackupTime()
        {
            string root = LocalRoot;
            if (!Directory.Exists(root)) return DateTime.MinValue;
            DateTime latest = DateTime.MinValue;
            foreach (string entry in Directory.EnumerateFileSystemEntries(root)) {
                bool folder = Directory.Exists(entry);
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) continue;
                if (!folder && !string.Equals(Path.GetExtension(entry), ".zip", StringComparison.OrdinalIgnoreCase))
                    continue;
                string name = folder ? Path.GetFileName(entry) : Path.GetFileNameWithoutExtension(entry);
                if (DateTime.TryParseExact(name,
                    new[] { "yyyy-MM-dd_HHmmss_fff", "yyyy-MM-dd_HHmmss", "yyyy-MM-dd" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime created) && created > latest)
                    latest = created;
            }
            return latest;
        }

        private sealed class Manifest
        {
            public int Format { get; set; } = 1;
            public DateTime CreatedUtc { get; set; }
            public Dictionary<string, string> DatabaseSha256 { get; set; }
            public Dictionary<string, string> ImageSha256 { get; set; }
        }

        public sealed class BackupResult
        {
            public string Mode { get; internal set; }
            public string LocalFolder { get; internal set; }
            public string RemoteType { get; internal set; }
            public string RemoteError { get; internal set; }
            public string CleanupError { get; internal set; }
            public string RetentionError { get; internal set; }

            /// <summary>在线保留份数清理失败的原因（上传成功后清理远端历史 ZIP）。</summary>
            public string RemoteRetentionError { get; internal set; }
        }

        public static string LocalRoot {
            get {
                string configured = ConfigManager.Settings.BackupDirectory?.Trim();
                string root = string.IsNullOrWhiteSpace(configured) ? PathManager.BackupPath : configured;
                if (!Path.IsPathRooted(root)) throw new InvalidOperationException("备份目录必须使用绝对路径");
                string fullRoot = Path.GetFullPath(root);
                if (fullRoot.Length > Path.GetPathRoot(fullRoot).Length)
                    fullRoot = fullRoot.TrimEnd(Path.DirectorySeparatorChar);
                string imageRoot = Path.GetFullPath(Path.Combine(PathManager.CurrentUserFolder, "image"))
                    .TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(fullRoot, imageRoot, StringComparison.OrdinalIgnoreCase) ||
                    fullRoot.StartsWith(imageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("备份目录不能放在图片目录内");
                return fullRoot;
            }
        }

        public static async Task<BackupResult> CreateAsync()
        {
            // 自动备份已移到后台，用户可同时点手动备份；串行化以免保留份数清理删除正在生成的快照。
            await BackupGate.WaitAsync();
            try {
                return await CreateCoreAsync();
            } finally {
                BackupGate.Release();
            }
        }

        private static async Task<BackupResult> CreateCoreAsync()
        {
            string mode = ConfigManager.Settings.BackupMode;
            if (mode != "LocalOnly" && mode != "RemoteOnly" && mode != "Both")
                throw new InvalidOperationException("未知备份方式");
            bool keepLocal = mode != "RemoteOnly";
            bool uploadRemote = mode != "LocalOnly";
            string provider = ConfigManager.Settings.BackupRemoteType;
            if (uploadRemote && provider != "WebDAV" && provider != "S3")
                throw new InvalidOperationException("仅在线备份或本地及在线备份时，请先选择 WebDAV 或 S3");

            var result = new BackupResult { Mode = mode, RemoteType = uploadRemote ? provider : null };
            string temporaryRoot = null;
            try {
                string folder;
                string localArchive = null;
                if (keepLocal) {
                    folder = await Task.Run(() => CreateLocal());
                    // 本地备份统一为压缩包：快照文件夹压成同名 ZIP 后只保留 ZIP（与在线备份一致），
                    // 压缩失败时退回文件夹形态（原有行为）
                    try {
                        localArchive = await Task.Run(() => CreateArchive(folder));
                        result.LocalFolder = localArchive;
                        try { Directory.Delete(folder, true); }
                        catch (Exception ex) { result.CleanupError = ex.Message; }
                    } catch (Exception ex) {
                        Jvedio.Core.Logs.Logger.Instance.Error(ex);
                        result.LocalFolder = folder;
                    }
                    try {
                        int keep = Math.Max(1, Math.Min(10, ConfigManager.Settings.MaxLocalBackups));
                        await Task.Run(() => PruneLocalSnapshots(LocalRoot, keep));
                    } catch (Exception ex) { result.RetentionError = ex.Message; }
                } else {
                    temporaryRoot = Path.Combine(Path.GetTempPath(), "Jvedio-online-backup-" + Guid.NewGuid().ToString("N"));
                    string root = temporaryRoot;
                    folder = await Task.Run(() => CreateSnapshot(root));
                }
                if (uploadRemote) {
                    string archive = null;
                    bool archiveIsLocalBackup = false;
                    try {
                        if (localArchive != null) {
                            // 本地及在线模式：直接上传刚生成的本地 ZIP，不再重复压缩
                            archive = localArchive;
                            archiveIsLocalBackup = true;
                        } else {
                            archive = await Task.Run(() => CreateArchive(folder));
                        }
                        await RemoteBackupStore.UploadAsync(archive);
                        // 在线保留份数：上传成功后清理远端多余的历史 ZIP
                        try {
                            int remoteKeep = Math.Max(1, Math.Min(30, ConfigManager.Settings.RemoteMaxBackups));
                            await RemoteBackupStore.PruneRemoteAsync(remoteKeep);
                        } catch (Exception ex) { result.RemoteRetentionError = ex.Message; }
                    } catch (Exception ex) {
                        result.RemoteError = ex.Message;
                    } finally {
                        // 仅在线模式的 ZIP 在临时目录，上传后删除；
                        // 本地及在线模式的 ZIP 就是本地备份本体，保留
                        if (archive != null && File.Exists(archive) && !archiveIsLocalBackup) {
                            try { File.Delete(archive); }
                            catch (Exception ex) { result.CleanupError = ex.Message; }
                        }
                    }
                }
            } finally {
                if (temporaryRoot != null && Directory.Exists(temporaryRoot)) {
                    try { await Task.Run(() => Directory.Delete(temporaryRoot, true)); }
                    catch (Exception ex) { result.CleanupError = ex.Message; }
                }
            }
            return result;
        }

        public static string CreateLocal()
        {
            return CreateSnapshot(LocalRoot);
        }

        private static void PruneLocalSnapshots(string root, int keep)
        {
            if (!Directory.Exists(root)) return;
            string rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var snapshots = new List<Tuple<string, DateTime>>();
            // 2026-10-01 起本地备份为同名 ZIP；此前的日期文件夹仍按快照参与保留份数清理
            foreach (string archive in Directory.EnumerateFiles(root, "*.zip")) {
                string name = Path.GetFileNameWithoutExtension(archive);
                if (!DateTime.TryParseExact(name,
                    new[] { "yyyy-MM-dd_HHmmss_fff", "yyyy-MM-dd_HHmmss", "yyyy-MM-dd" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime zipCreated)) continue;
                snapshots.Add(Tuple.Create(archive, zipCreated));
            }
            foreach (string folder in Directory.EnumerateDirectories(root)) {
                var info = new DirectoryInfo(folder);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                string name = info.Name;
                if (!DateTime.TryParseExact(name,
                    new[] { "yyyy-MM-dd_HHmmss_fff", "yyyy-MM-dd_HHmmss", "yyyy-MM-dd" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime created)) continue;
                if (DatabaseNames.Any(db => !File.Exists(Path.Combine(folder, db)))) continue;
                var allowed = new HashSet<string>(DatabaseNames, StringComparer.OrdinalIgnoreCase) {
                    "image", ManifestName
                };
                foreach (string db in DatabaseNames)
                    foreach (string suffix in DatabaseSidecars) allowed.Add(db + suffix);
                if (Directory.EnumerateFileSystemEntries(folder).Any(entry =>
                    !allowed.Contains(Path.GetFileName(entry)))) continue;
                snapshots.Add(Tuple.Create(folder, created));
            }
            foreach (var old in snapshots.OrderByDescending(item => item.Item2).ThenByDescending(item => item.Item1)
                .Skip(keep)) {
                string full = Path.GetFullPath(old.Item1);
                if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("备份清理路径越界");
                if (Directory.Exists(full))
                    Directory.Delete(full, true);
                else if (File.Exists(full))
                    File.Delete(full);
            }
        }

        private static string CreateSnapshot(string root)
        {
            Directory.CreateDirectory(root);
            string folder = Path.Combine(root, DateTime.Now.ToString("yyyy-MM-dd_HHmmss_fff"));
            Directory.CreateDirectory(folder);
            try {
                var manifest = new Manifest {
                    CreatedUtc = DateTime.UtcNow,
                    DatabaseSha256 = new Dictionary<string, string>(),
                    ImageSha256 = new Dictionary<string, string>()
                };
                for (int i = 0; i < DatabaseNames.Length; i++) {
                    string source = GetLiveDatabase(DatabaseNames[i]);
                    string target = Path.Combine(folder, DatabaseNames[i]);
                    CopySqliteSnapshot(source, target);
                    CheckSqlite(target);
                    manifest.DatabaseSha256[DatabaseNames[i]] = Sha256(target);
                }
                string image = Path.Combine(PathManager.CurrentUserFolder, "image");
                if (Directory.Exists(image)) {
                    CopyDirectory(image, Path.Combine(folder, "image"));
                    string imageSnapshot = Path.Combine(folder, "image");
                    foreach (string file in Directory.EnumerateFiles(imageSnapshot, "*", SearchOption.AllDirectories)) {
                        string relative = "image/" + file.Substring(imageSnapshot.Length)
                            .TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/');
                        manifest.ImageSha256[relative] = Sha256(file);
                    }
                }
                File.WriteAllText(Path.Combine(folder, ManifestName),
                    JsonConvert.SerializeObject(manifest, Formatting.Indented), Encoding.UTF8);
                return folder;
            } catch {
                try { Directory.Delete(folder, true); } catch { }
                throw;
            }
        }

        private static string GetLiveDatabase(string name)
        {
            return Path.Combine(PathManager.CurrentUserFolder, name);
        }

        private static void CopySqliteSnapshot(string sourcePath, string targetPath)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("数据库文件不存在", sourcePath);
            using (var source = new SQLiteConnection("Data Source=" + sourcePath + ";Version=3;"))
            using (var target = new SQLiteConnection("Data Source=" + targetPath + ";Version=3;")) {
                source.Open();
                target.Open();
                source.BackupDatabase(target, "main", "main", -1, null, 0);
            }
        }

        private static void CheckSqlite(string path)
        {
            using (var connection = new SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;")) {
                connection.Open();
                using (var command = new SQLiteCommand("PRAGMA integrity_check", connection)) {
                    if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("备份数据库完整性校验失败: " + Path.GetFileName(path));
                }
            }
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.EnumerateFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (string dir in Directory.EnumerateDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }

        public static string CreateArchive(string folder)
        {
            ValidateFolder(folder);
            string archive = folder.TrimEnd(Path.DirectorySeparatorChar) + ".zip";
            string temporary = archive + ".partial";
            try {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create)) {
                    foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)) {
                        if (file.EndsWith("-wal", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith("-shm", StringComparison.OrdinalIgnoreCase)) continue;
                        string relative = file.Substring(folder.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/');
                        var entry = zip.CreateEntry(relative, CompressionLevel.Optimal);
                        using (var source = File.OpenRead(file))
                        using (var destination = entry.Open())
                            source.CopyTo(destination);
                    }
                }
                if (File.Exists(archive)) File.Delete(archive);
                File.Move(temporary, archive);
                return archive;
            } finally {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static void ValidateFolder(string folder)
        {
            var manifestPath = Path.Combine(folder, ManifestName);
            Manifest manifest = null;
            if (File.Exists(manifestPath)) {
                manifest = JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
                if (manifest == null || manifest.Format != 1 || manifest.DatabaseSha256 == null)
                    throw new InvalidDataException("无法识别备份清单");
            }
            foreach (string name in DatabaseNames) {
                string file = Path.Combine(folder, name);
                if (!File.Exists(file)) throw new InvalidDataException("备份缺少 " + name);
                CheckSqlite(file);
                if (manifest != null && (!manifest.DatabaseSha256.TryGetValue(name, out string expected) ||
                    !string.Equals(Sha256(file), expected, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("备份校验值不一致: " + name);
            }
            if (manifest?.ImageSha256 != null) {
                string prefix = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (var image in manifest.ImageSha256) {
                    string file = Path.GetFullPath(Path.Combine(folder,
                        image.Key.Replace('/', Path.DirectorySeparatorChar)));
                    if (!image.Key.StartsWith("image/", StringComparison.Ordinal) ||
                        !file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                        !File.Exists(file) ||
                        !string.Equals(Sha256(file), image.Value, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("备份图片校验失败: " + image.Key);
                }
            }
        }

        public static void StageRestore(string source)
        {
            string stage = Path.Combine(PathManager.CurrentUserFolder, StageName);
            if (Directory.Exists(source)) {
                string sourcePrefix = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullStage = Path.GetFullPath(stage);
                if (string.Equals(fullStage, Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase) ||
                    fullStage.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("请选择备份快照文件夹，而不是当前数据目录");
                ValidateFolder(source);
            }
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            Directory.CreateDirectory(stage);
            try {
                if (Directory.Exists(source)) CopyDirectory(source, stage);
                else if (File.Exists(source) && Path.GetExtension(source).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    ExtractArchive(source, stage);
                else throw new FileNotFoundException("请选择备份文件夹或 ZIP 文件", source);
                ValidateFolder(stage);
                File.WriteAllText(Path.Combine(PathManager.CurrentUserFolder, PendingName), stage, Encoding.UTF8);
            } catch {
                Directory.Delete(stage, true);
                throw;
            }
        }

        private static void ExtractArchive(string archive, string stage)
        {
            string stagePrefix = Path.GetFullPath(stage).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (var file = File.OpenRead(archive))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read)) {
                foreach (var entry in zip.Entries) {
                    string destination = Path.GetFullPath(Path.Combine(stage, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(stagePrefix, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("备份包含非法路径");
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (var input = entry.Open())
                    using (var output = File.Create(destination)) input.CopyTo(output);
                }
            }
        }

        /// <summary>Must run before MapperManager.Init; the previous files remain in restore-rollback on failure.</summary>
        public static void ApplyPendingRestore()
        {
            string marker = Path.Combine(PathManager.CurrentUserFolder, PendingName);
            if (!File.Exists(marker)) return;
            string stage = File.ReadAllText(marker, Encoding.UTF8).Trim();
            if (!string.Equals(Path.GetFullPath(stage),
                Path.GetFullPath(Path.Combine(PathManager.CurrentUserFolder, StageName)),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("恢复标记包含非法路径");
            ValidateFolder(stage);
            string rollback = Path.Combine(PathManager.CurrentUserFolder,
                "restore-rollback-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rollback);
            var touchedDatabases = new HashSet<string>();
            bool touchedImage = false;
            try {
                foreach (string name in DatabaseNames) {
                    string live = GetLiveDatabase(name);
                    touchedDatabases.Add(name);
                    if (File.Exists(live)) File.Move(live, Path.Combine(rollback, name));
                    foreach (string suffix in DatabaseSidecars) {
                        if (File.Exists(live + suffix))
                            File.Move(live + suffix, Path.Combine(rollback, name + suffix));
                    }
                    File.Copy(Path.Combine(stage, name), live);
                }
                string liveImage = Path.Combine(PathManager.CurrentUserFolder, "image");
                string stagedImage = Path.Combine(stage, "image");
                if (Directory.Exists(stagedImage)) {
                    touchedImage = true;
                    if (Directory.Exists(liveImage)) Directory.Move(liveImage, Path.Combine(rollback, "image"));
                    CopyDirectory(stagedImage, liveImage);
                }
                File.Delete(marker);
                Directory.Delete(stage, true);
            } catch {
                foreach (string name in touchedDatabases) {
                    string old = Path.Combine(rollback, name);
                    string live = GetLiveDatabase(name);
                    if (File.Exists(live)) File.Delete(live);
                    if (File.Exists(old)) File.Move(old, live);
                    foreach (string suffix in DatabaseSidecars) {
                        string oldSidecar = Path.Combine(rollback, name + suffix);
                        if (File.Exists(live + suffix)) File.Delete(live + suffix);
                        if (File.Exists(oldSidecar)) File.Move(oldSidecar, live + suffix);
                    }
                }
                string oldImage = Path.Combine(rollback, "image");
                string liveImage = Path.Combine(PathManager.CurrentUserFolder, "image");
                if (touchedImage) {
                    if (Directory.Exists(liveImage)) Directory.Delete(liveImage, true);
                    if (Directory.Exists(oldImage)) Directory.Move(oldImage, liveImage);
                }
                throw;
            }
        }
    }
}
