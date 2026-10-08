using Jvedio.Core.Enums;
using Jvedio.Core.Library;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static Jvedio.Core.Library.LibraryDatabase;

namespace Jvedio.Core.Clipper
{
    public sealed class BrowserClipItem
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public BrowserClipMetadata Metadata { get; set; }
    }
    public sealed class BrowserClipActor
    {
        public string Name { get; set; }
        public List<string> Aliases { get; set; }
        public string Url { get; set; }
        public string ImageUrl { get; set; }
    }
    public sealed class BrowserClipMetadata
    {
        public string TitleCN { get; set; }
        public string ReleaseDate { get; set; }
        public int Duration { get; set; }
        public string Director { get; set; }
        public string Studio { get; set; }
        public string Publisher { get; set; }
        public List<string> Series { get; set; }
        public List<string> Genres { get; set; }
        public List<BrowserClipActor> Actors { get; set; }
        public double Rating { get; set; }
        public int RatingCount { get; set; }
        public string Plot { get; set; }
        public string CoverUrl { get; set; }
        public List<string> PreviewUrls { get; set; }
        public List<string> PreviewVideoUrls { get; set; }
        public string PreviewVideoEndpoint { get; set; }
    }
    public sealed class BrowserClipResult
    {
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<long> ImageDataIds { get; } = new List<long>();
        public List<object> SavedItems { get; } = new List<object>();
    }
    public sealed class BrowserClipRequest
    {
        public long LibraryId { get; set; }
        public string Site { get; set; }
        public string Kind { get; set; }
        public bool BrowserImages { get; set; }
        public string PageUrl { get; set; }
        public List<BrowserClipItem> Items { get; set; }
    }

    /// <summary>Explicitly enabled, authenticated loopback receiver; no administrator URL reservation needed.</summary>
    public sealed class BrowserClipperService : IDisposable
    {
        private static readonly Lazy<BrowserClipperService> Shared = new Lazy<BrowserClipperService>(() => new BrowserClipperService(true));
        public static BrowserClipperService Instance => Shared.Value;
        public event Action<long, int, int, int> Saved;
        public string Token { get; }
        public int Port { get; private set; }
        public bool Running => _Listener != null;
        private TcpListener _Listener;
        private readonly SemaphoreSlim _Clients = new SemaphoreSlim(4);
        private readonly object _Lifecycle = new object();
        private static readonly object ImportLock = new object();
        private static readonly Regex CodePattern = new Regex(@"^(?:\d{0,6}[A-Z]{2,15}[-_]\d{2,10}(?:[-_][A-Z0-9]{1,8})?|\d{6}[-_]\d{2,4})$", RegexOptions.CultureInvariant);

        public BrowserClipperService(bool persistCredential = false)
        {
            var config = persistCredential ? ConfigManager.BrowserClipperConfig : null;
            if (!string.IsNullOrEmpty(config?.ProtectedToken)) {
                try {
                    string stored = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(config.ProtectedToken), null, DataProtectionScope.CurrentUser));
                    if (Regex.IsMatch(stored, "^[a-f0-9]{64}$")) { Token = stored; return; }
                } catch (Exception ex) when (ex is CryptographicException || ex is FormatException) { }
            }
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            Token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            if (config != null) {
                config.ProtectedToken = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(Token), null, DataProtectionScope.CurrentUser));
                config.Save();
            }
        }

        public void Start(int port = 18888)
        {
            lock (_Lifecycle) {
                if (Running) return;
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Server.ExclusiveAddressUse = true;
                listener.Start(8);
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                _Listener = listener;
                _ = AcceptAsync(listener);
            }
        }

        private async Task AcceptAsync(TcpListener listener)
        {
            try {
                while (true) {
                    var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    if (!_Clients.Wait(0)) { client.Close(); continue; }
                    _ = Task.Run(() => {
                        try { Handle(client, listener); }
                        finally { client.Dispose(); _Clients.Release(); }
                    });
                }
            } catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        private void Handle(TcpClient client, TcpListener listener)
        {
            client.ReceiveTimeout = client.SendTimeout = 5000;
            using (var stream = client.GetStream()) {
                try {
                    var header = new List<byte>();
                    while (header.Count < 16384) {
                        int value = stream.ReadByte();
                        if (value < 0) return;
                        header.Add((byte)value);
                        int n = header.Count;
                        if (n >= 4 && header[n - 4] == 13 && header[n - 3] == 10 && header[n - 2] == 13 && header[n - 1] == 10) break;
                    }
                    if (header.Count >= 16384) { Reply(stream, 413, new { error = "Request headers too large" }); return; }
                    var lines = Encoding.ASCII.GetString(header.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
                    var requestLine = lines[0].Split(' ');
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string line in lines.Skip(1).Where(line => line.Length > 0)) {
                        int colon = line.IndexOf(':');
                        if (colon <= 0 || headers.ContainsKey(line.Substring(0, colon))) throw new ArgumentException("Invalid headers");
                        headers.Add(line.Substring(0, colon), line.Substring(colon + 1).Trim());
                    }
                    if (requestLine.Length != 3 || !headers.TryGetValue("Host", out string host) || host != "127.0.0.1:" + Port ||
                        headers.ContainsKey("Transfer-Encoding")) throw new ArgumentException("Invalid request");
                    string origin = headers.TryGetValue("Origin", out string originValue) ? originValue : "";
                    if (origin.Length > 0 && !Regex.IsMatch(origin, @"^chrome-extension://[a-p]{32}$")) {
                        Reply(stream, 403, new { error = "Browser extension origin required" }); return;
                    }
                    if (requestLine[0] == "OPTIONS") {
                        Reply(stream, 200, new { app = "Jvedio", protocol = 1 }, origin); return;
                    }
                    if (!headers.TryGetValue("X-Jvedio-Token", out string token) || !TokenEquals(token)) {
                        Reply(stream, 401, new { error = "连接密钥不正确，请从 Jvedio 重新复制。" }, origin); return;
                    }
                    // Stopping the receiver revokes requests already accepted by the old listener.
                    if (_Listener != listener) { Reply(stream, 503, new { error = "剪藏接收已关闭。" }, origin); return; }
                    if (requestLine[0] == "GET" && requestLine[1] == "/v1/status") {
                        EnsureSQLite();
                        var libraries = new List<object>();
                        using (var connection = Open(true))
                        using (var command = MakeCommand(connection, null, "select DBId,Name from app_databases where DataType=0 order by DBId"))
                        using (var reader = command.ExecuteReader())
                            while (reader.Read()) libraries.Add(new { id = reader.GetInt64(0), name = reader.GetString(1) });
                        var config = ConfigManager.BrowserClipperConfig;
                        Reply(stream, 200, new { app = "Jvedio", protocol = 1, currentLibraryId = ConfigManager.Main.CurrentDBId, libraries,
                            media = new { saveImages = config.SaveImages, saveCoverImages = config.SaveImages && config.SaveCoverImages,
                                saveActorImages = config.SaveImages && config.SaveActorImages,
                                savePreviewImages = config.SaveImages && config.SavePreviewImages, savePreviewVideos = config.SavePreviewVideos } }, origin);
                    } else if (requestLine[0] == "POST" && requestLine[1] == "/v1/clips") {
                        if (!headers.TryGetValue("Content-Length", out string size) || !int.TryParse(size, out int length) || length <= 0 || length > 1048576)
                            throw new ArgumentException("请选择不超过 500 个番号（请求上限 1 MB）。");
                        if (!headers.TryGetValue("Content-Type", out string type) || !type.Split(';')[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException("JSON request required");
                        var body = new byte[length];
                        int offset = 0;
                        while (offset < length) {
                            int count = stream.Read(body, offset, length - offset);
                            if (count == 0) throw new ArgumentException("Incomplete request");
                            offset += count;
                        }
                        var request = JsonConvert.DeserializeObject<BrowserClipRequest>(new UTF8Encoding(false, true).GetString(body),
                            new JsonSerializerSettings { MaxDepth = 16, TypeNameHandling = TypeNameHandling.None });
                        BrowserClipResult result;
                        lock (_Lifecycle) {
                            if (_Listener != listener) throw new ArgumentException("剪藏接收已关闭。");
                            result = Import(request);
                        }
                        int imagesQueued = 0;
                        int mediaAlreadyQueued = 0;
                        // The existing download queue reads captured image URLs and skips metadata scraping.
                        foreach (long id in result.ImageDataIds) {
                            try {
                                var video = MapperManager.videoMapper.SelectVideoByID(id);
                                if (video == null) continue;
                                var config = ConfigManager.BrowserClipperConfig;
                                var task = new Jvedio.Core.Net.DownLoadTask(video, config.SaveImages && config.SavePreviewImages, false) {
                                    CapturedMedia = new Jvedio.Core.Net.CapturedMediaOptions {
                                        CoverImages = !request.BrowserImages && config.SaveImages && config.SaveCoverImages,
                                        PreviewImages = !request.BrowserImages && config.SaveImages && config.SavePreviewImages,
                                        PreviewVideos = config.SavePreviewVideos,
                                        Referer = HttpUrl(request.PageUrl)
                                    }
                                };
                                if (request.BrowserImages && !config.SavePreviewVideos) continue;
                                App.Current.Dispatcher.Invoke(new Action(() => {
                                    var existingTask = App.DownloadManager.CurrentTasks.OfType<Jvedio.Core.Net.DownLoadTask>().FirstOrDefault(current => current.DataID == id);
                                    if (existingTask != null && (existingTask.Status == System.Threading.Tasks.TaskStatus.RanToCompletion ||
                                        existingTask.Status == System.Threading.Tasks.TaskStatus.Canceled || existingTask.Status == System.Threading.Tasks.TaskStatus.Faulted)) {
                                        App.DownloadManager.CurrentTasks.Remove(existingTask); existingTask = null;
                                    }
                                    if (existingTask == null) { App.DownloadManager.AddTask(task); imagesQueued++; }
                                    else mediaAlreadyQueued++;
                                }));
                            } catch (Exception ex) { App.Logger.Error(ex); }
                        }
                        Reply(stream, 200, new { app = "Jvedio", protocol = 1, added = result.Added, updated = result.Updated, skipped = result.Skipped, imagesQueued, mediaQueued = imagesQueued, mediaAlreadyQueued, savedItems = result.SavedItems }, origin);
                        try { Saved?.Invoke(request.LibraryId, result.Added, result.Updated, result.Skipped); }
                        catch (Exception ex) { App.Logger.Error(ex); }
                    } else if (requestLine[0] == "POST" && requestLine[1] == "/v1/images") {
                        if (!headers.TryGetValue("Content-Length", out string imageSize) || !int.TryParse(imageSize, out int length) || length <= 0 || length > 16000000)
                            throw new ArgumentException("图片请求大小超出范围。");
                        var body = new byte[length]; int offset = 0;
                        while (offset < length) { int count = stream.Read(body, offset, length - offset); if (count == 0) throw new ArgumentException("Incomplete image request"); offset += count; }
                        var image = JsonConvert.DeserializeObject<BrowserClipImage>(Encoding.UTF8.GetString(body));
                        int saved = BrowserClipperImages.Save(image);
                        Reply(stream, 200, new { app = "Jvedio", protocol = 1, saved }, origin);
                        try { Saved?.Invoke(image.LibraryId, 0, 1, 0); } catch (Exception ex) { App.Logger.Error(ex); }
                    } else Reply(stream, 404, new { error = "Unknown endpoint" }, origin);
                } catch (ArgumentException ex) { Reply(stream, 400, new { error = ex.Message }); }
                catch (JsonException) { Reply(stream, 400, new { error = "无效的剪藏数据。" }); }
                catch (IOException) { }
                catch (Exception ex) {
                    App.Logger.Error(ex);
                    Reply(stream, 503, new { error = "保存失败，请检查影片库是否可用，然后重试。" });
                }
            }
        }

        private bool TokenEquals(string candidate)
        {
            if (candidate == null || candidate.Length != Token.Length) return false;
            int mismatch = 0;
            for (int i = 0; i < Token.Length; i++) mismatch |= candidate[i] ^ Token[i];
            return mismatch == 0;
        }
        private static void Reply(Stream stream, int status, object data, string origin = "")
        {
            try {
                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
                string cors = origin.Length == 0 ? "" : "Access-Control-Allow-Origin: " + origin + "\r\nVary: Origin\r\n";
                var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Response\r\nContent-Type: application/json; charset=utf-8\r\n" +
                    "Content-Length: " + body.Length + "\r\nConnection: close\r\nCache-Control: no-store\r\n" + cors +
                    "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type, X-Jvedio-Token\r\n\r\n");
                stream.Write(header, 0, header.Length); stream.Write(body, 0, body.Length);
            } catch (IOException) { } catch (ObjectDisposedException) { }
        }
        private static void EnsureSQLite()
        {
            if (Main.CurrentDataBaseType != DataBaseType.SQLite)
                throw new ArgumentException("此版浏览器剪藏支持 SQLite 影片库，请切换至本地影片库。");
        }
        public static string NormalizeCode(string value)
        {
            string code = (value ?? "").Normalize(NormalizationForm.FormKC).Trim().ToUpperInvariant();
            code = Regex.Replace(code, "[‐‑–—−]", "-");
            var fc2 = Regex.Match(code, @"^FC2[\s_-]*(?:PPV[\s_-]*)?(\d{5,10})$");
            if (fc2.Success) return "FC2-PPV-" + fc2.Groups[1].Value;
            if (!CodePattern.IsMatch(code)) throw new ArgumentException("无效的番号：" + code.Substring(0, Math.Min(50, code.Length)));
            return code;
        }
        private static string HttpUrl(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Length > 4096 || !Uri.TryCreate(value, UriKind.Absolute, out Uri uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new ArgumentException("影片来源必须是 HTTP/HTTPS 地址。");
            return uri.AbsoluteUri;
        }
        private static string Key(string code) => code.Replace('_', '-').Replace("FC2-PPV-", "FC2-");

        private static void ValidateMetadata(BrowserClipMetadata info)
        {
            foreach (string value in new[] { info.TitleCN, info.Director, info.Studio, info.Publisher })
                if ((value?.Length ?? 0) > 1000 || (value?.Any(char.IsControl) ?? false)) throw new ArgumentException("无效的影片资料。");
            if ((info.Plot?.Length ?? 0) > 10000 || info.Duration < 0 || info.Duration > 10000 || double.IsNaN(info.Rating) ||
                double.IsInfinity(info.Rating) || info.Rating < 0 || info.Rating > 5 || info.RatingCount < 0 || info.RatingCount > 100000000)
                throw new ArgumentException("无效的时长或评分。");
            if (!string.IsNullOrEmpty(info.ReleaseDate) && !DateTime.TryParseExact(info.ReleaseDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                throw new ArgumentException("无效的发行日期。");
            foreach (var list in new[] { info.Series, info.Genres })
                if (list != null && (list.Count > 100 || list.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(char.IsControl))))
                    throw new ArgumentException("无效的类别或系列。");
            if ((info.Actors?.Count ?? 0) > 100 || (info.PreviewUrls?.Count ?? 0) > 100) throw new ArgumentException("影片资料过多。");
            foreach (var actor in info.Actors ?? new List<BrowserClipActor>()) {
                if (actor == null || string.IsNullOrWhiteSpace(actor.Name) || actor.Name.Length > 200 || actor.Name.Any(char.IsControl) ||
                    (actor.Aliases?.Count ?? 0) > 20 || (actor.Aliases?.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > 200 || name.Any(char.IsControl)) ?? false))
                    throw new ArgumentException("无效的演员信息。");
                actor.Url = HttpUrl(actor.Url); actor.ImageUrl = HttpUrl(actor.ImageUrl);
            }
            info.CoverUrl = HttpUrl(info.CoverUrl);
            info.PreviewUrls = (info.PreviewUrls ?? new List<string>()).Select(HttpUrl).Where(url => url.Length > 0).Distinct().ToList();
            if ((info.PreviewVideoUrls?.Count ?? 0) > 10) throw new ArgumentException("预览视频链接过多。");
            info.PreviewVideoUrls = (info.PreviewVideoUrls ?? new List<string>()).Select(HttpUrl).Where(url => url.Length > 0).Distinct().ToList();
            info.PreviewVideoEndpoint = HttpUrl(info.PreviewVideoEndpoint);
        }

        private static bool FillMetadata(SQLiteConnection connection, SQLiteTransaction transaction, long id, BrowserClipItem item, string webType)
        {
            var info = item.Metadata;
            bool changed = false;
            // Column names below are fixed application constants, never supplied by a webpage.
            Action<string, string, object, string> fill = (table, field, value, empty) => {
                if (value == null || Convert.ToString(value, CultureInfo.InvariantCulture).Length == 0 || value is int integer && integer == 0 || value is double number && number == 0) return;
                changed |= Execute(connection, transaction, "update " + table + " set " + field + "=@value where DataID=@id and (" + empty + ")",
                    "@value", value, "@id", id) > 0;
            };
            Action<string, string, string> str = (table, field, value) => fill(table, field, value, "trim(ifnull(" + field + ",''))=''");
            str("metadata", "Title", item.Title);
            str("metadata", "TitleCN", info.TitleCN);
            fill("metadata", "ReleaseDate", info.ReleaseDate, "ifnull(ReleaseDate,'')='' or ReleaseDate<='1900-01-01'");
            if (!string.IsNullOrEmpty(info.ReleaseDate)) fill("metadata", "ReleaseYear", int.Parse(info.ReleaseDate.Substring(0, 4)), "ifnull(ReleaseYear,0)<=1900");
            fill("metadata", "Rating", info.Rating, "ifnull(Rating,0)=0");
            fill("metadata", "RatingCount", info.RatingCount, "ifnull(RatingCount,0)=0");
            str("metadata", "Genre", string.Join(SuperUtils.Values.ConstValues.Separator.ToString(), info.Genres ?? new List<string>()));
            foreach (var pair in new[] { Tuple.Create("Director", info.Director), Tuple.Create("Studio", info.Studio), Tuple.Create("Publisher", info.Publisher),
                Tuple.Create("Plot", info.Plot), Tuple.Create("WebType", webType), Tuple.Create("WebUrl", item.Url),
                Tuple.Create("Series", string.Join(SuperUtils.Values.ConstValues.Separator.ToString(), info.Series ?? new List<string>())) })
                str("metadata_video", pair.Item1, pair.Item2);
            fill("metadata_video", "Duration", info.Duration, "ifnull(Duration,0)=0");
            var capturedUrls = new Dictionary<string, object> {
                ["BigImageUrl"] = info.CoverUrl, ["SmallImageUrl"] = info.CoverUrl,
                ["ExtraImageUrl"] = info.PreviewUrls, ["PreviewVideoUrl"] = info.PreviewVideoUrls
            };
            string oldImageJson;
            using (var command = MakeCommand(connection, transaction, "select ImageUrls from metadata_video where DataID=@id limit 1", "@id", id)) oldImageJson = Convert.ToString(command.ExecuteScalar());
            var merged = string.IsNullOrEmpty(oldImageJson) ? new Newtonsoft.Json.Linq.JObject() : Newtonsoft.Json.Linq.JObject.Parse(oldImageJson);
            bool mediaChanged = false;
            foreach (var pair in capturedUrls) {
                var value = Newtonsoft.Json.Linq.JToken.FromObject(pair.Value ?? "");
                var old = merged[pair.Key];
                bool absent = old == null || old.Type == Newtonsoft.Json.Linq.JTokenType.Null || old.Type == Newtonsoft.Json.Linq.JTokenType.Array && !old.HasValues || old.Type == Newtonsoft.Json.Linq.JTokenType.String && old.ToString().Length == 0;
                bool present = value.Type == Newtonsoft.Json.Linq.JTokenType.Array ? value.HasValues : value.ToString().Length > 0;
                if (present && (absent || pair.Key == "PreviewVideoUrl" && !Newtonsoft.Json.Linq.JToken.DeepEquals(old, value))) {
                    merged[pair.Key] = value; mediaChanged = true;
                }
            }
            if (mediaChanged) changed |= Execute(connection, transaction, "update metadata_video set ImageUrls=@json where DataID=@id", "@json", merged.ToString(Newtonsoft.Json.Formatting.None), "@id", id) > 0;
            long actorCount;
            using (var command = MakeCommand(connection, transaction, "select count(*) from metadata_to_actor where DataID=@id", "@id", id))
                actorCount = Convert.ToInt64(command.ExecuteScalar());
            if (actorCount == 0) {
                foreach (var actor in info.Actors ?? new List<BrowserClipActor>()) {
                    var names = new[] { actor.Name }.Concat(actor.Aliases ?? new List<string>()).Distinct().ToList();
                    long actorId = 0;
                    foreach (string name in names) {
                        using (var command = MakeCommand(connection, transaction, "select ActorID from actor_info where ActorName=@name order by ActorID limit 1", "@name", name)) {
                            actorId = Convert.ToInt64(command.ExecuteScalar());
                            if (actorId > 0) break;
                        }
                    }
                    if (actorId == 0) {
                        Execute(connection, transaction, "insert into actor_info(ActorName,ImageUrl,WebType) values(@name,@image,@type)",
                            "@name", actor.Name, "@image", actor.ImageUrl, "@type", webType);
                        using (var command = MakeCommand(connection, transaction, "select last_insert_rowid()")) actorId = Convert.ToInt64(command.ExecuteScalar());
                    }
                    changed |= Execute(connection, transaction, "insert or ignore into metadata_to_actor(DataID,ActorID) values(@id,@actor)", "@id", id, "@actor", actorId) > 0;
                }
            }
            if (!string.IsNullOrEmpty(item.Url)) {
                var uri = new Uri(item.Url);
                string remote = uri.AbsolutePath.TrimEnd('/').Split('/').Last();
                if (webType == "library") {
                    if (remote.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) remote = remote.Substring(0, remote.Length - 5);
                    var query = Regex.Match(uri.Query, @"(?:\?|&)v=([^&]+)");
                    if (query.Success) remote = Uri.UnescapeDataString(query.Groups[1].Value);
                }
                if (webType == "db" || webType == "library")
                    Execute(connection, transaction, "insert into common_url_code(LocalValue,RemoteValue,WebType,ValueType) " +
                        "select @code,@remote,@type,'video' where not exists(select 1 from common_url_code where LocalValue=@code and WebType=@type and ValueType='video')",
                        "@code", item.Code, "@remote", remote, "@type", webType);
            }
            return changed;
        }

        /// <summary>Atomic batches skip existing list entries; detail captures fill missing metadata and retain the source snapshot.</summary>
        public static BrowserClipResult Import(BrowserClipRequest request)
        {
            EnsureSQLite();
            if (request?.Items == null || request.Items.Count < 1 || request.Items.Count > 500)
                throw new ArgumentException("请选择 1–500 个番号。");
            string webType = request.Site == "JavBus" ? "bus" : request.Site == "JavDB" ? "db" : request.Site == "JAVLibrary" ? "library" : "";
            if (webType.Length == 0) throw new ArgumentException("此版支持 JavBus、JavDB、JAVLibrary。");
            string pageUrl = HttpUrl(request.PageUrl);
            var items = request.Items.Select(item => {
                if (item == null || (item.Title?.Length ?? 0) > 1000) throw new ArgumentException("无效的影片条目。");
                if (item.Metadata != null) ValidateMetadata(item.Metadata);
                return new BrowserClipItem { Code = NormalizeCode(item.Code), Title = item.Title ?? "", Url = HttpUrl(item.Url), Metadata = item.Metadata };
            }).ToList();
            lock (ImportLock)
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction()) {
                using (var command = MakeCommand(connection, transaction, "select count(*) from app_databases where DBId=@db and DataType=0", "@db", request.LibraryId))
                    if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new ArgumentException("目标影片库不存在，请重新连接并选择影片库。");
                var existing = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                using (var command = MakeCommand(connection, transaction,
                    "select v.VID,m.DataID from metadata_video v join metadata m on m.DataID=v.DataID where m.DBId=@db and m.DataType=0 order by m.DataID", "@db", request.LibraryId))
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) existing[Key(Convert.ToString(reader[0]).Trim().ToUpperInvariant())] = reader.GetInt64(1);
                Execute(connection, transaction, "create table if not exists browser_clip_sources(DataID integer not null, SourceUrl text not null, Site text not null, SnapshotJson text not null, CapturedAt text not null, primary key(DataID,SourceUrl))");
                Execute(connection, transaction, "create trigger if not exists browser_clip_sources_cleanup after delete on metadata begin delete from browser_clip_sources where DataID=OLD.DataID; end");
                var result = new BrowserClipResult();
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                foreach (var item in items) {
                    bool isExisting = existing.TryGetValue(Key(item.Code), out long id);
                    if (isExisting && item.Metadata == null) { result.Skipped++; continue; }
                    if (!isExisting) {
                        Execute(connection, transaction,
                            "insert into metadata(DBId,DataType,Title,Path,PathExist,Size,CreateDate,UpdateDate,FirstScanDate,LastScanDate) " +
                            "values(@db,0,@title,'',0,0,@now,@now,@now,@now)", "@db", request.LibraryId, "@title", item.Title, "@now", now);
                        using (var command = MakeCommand(connection, transaction, "select last_insert_rowid()")) id = Convert.ToInt64(command.ExecuteScalar());
                        Execute(connection, transaction,
                            "insert into metadata_video(DataID,VID,VideoType,WebType,WebUrl) values(@id,@code,0,@type,@url)",
                            "@id", id, "@code", item.Code, "@type", webType, "@url", item.Url.Length > 0 ? item.Url : pageUrl);
                        Execute(connection, transaction, "insert or ignore into metadata_to_tagstamp(DataID,TagID) values(@id,10000)", "@id", id);
                        existing[Key(item.Code)] = id; result.Added++;
                    }
                    if (item.Metadata != null) {
                        bool updated = FillMetadata(connection, transaction, id, item, webType);
                        if (isExisting) { if (updated) result.Updated++; else result.Skipped++; }
                        var media = ConfigManager.BrowserClipperConfig;
                        if (media.SaveImages && media.SaveCoverImages && !string.IsNullOrEmpty(item.Metadata.CoverUrl) ||
                            media.SaveImages && media.SavePreviewImages && item.Metadata.PreviewUrls.Count > 0 ||
                            media.SavePreviewVideos && item.Metadata.PreviewVideoUrls.Count > 0) result.ImageDataIds.Add(id);
                    }
                    var savedActors = new List<object>();
                    var knownActors = new List<BrowserClipActor>();
                    using (var command = MakeCommand(connection, transaction, "select SnapshotJson from browser_clip_sources where DataID=@id", "@id", id))
                    using (var reader = command.ExecuteReader())
                        while (reader.Read()) {
                            try { knownActors.AddRange(JsonConvert.DeserializeObject<BrowserClipItem>(reader.GetString(0))?.Metadata?.Actors ?? new List<BrowserClipActor>()); }
                            catch (JsonException) { }
                        }
                    foreach (var sourceActor in item.Metadata?.Actors ?? new List<BrowserClipActor>()) {
                        var names = new[] { sourceActor.Name }.Concat(sourceActor.Aliases ?? new List<string>()).ToList();
                        foreach (var known in knownActors)
                            if (names.Contains(known.Name) || (known.Aliases?.Any(names.Contains) ?? false)) {
                                names.Add(known.Name); names.AddRange(known.Aliases ?? new List<string>());
                            }
                        foreach (string name in names) {
                            using (var command = MakeCommand(connection, transaction, "select a.ActorID from actor_info a join metadata_to_actor ma on ma.ActorID=a.ActorID where ma.DataID=@id and a.ActorName=@name limit 1", "@id", id, "@name", name)) {
                                object actorId = command.ExecuteScalar();
                                if (actorId != null) { savedActors.Add(new { id = Convert.ToInt64(actorId), sourceName = sourceActor.Name }); break; }
                            }
                        }
                    }
                    result.SavedItems.Add(new { code = item.Code, id, actors = savedActors });
                    Execute(connection, transaction, "insert or replace into browser_clip_sources(DataID,SourceUrl,Site,SnapshotJson,CapturedAt) values(@id,@url,@site,@json,@now)",
                        "@id", id, "@url", item.Url.Length > 0 ? item.Url : pageUrl, "@site", request.Site, "@json", JsonConvert.SerializeObject(item), "@now", now);
                }
                transaction.Commit();
                return result;
            }
        }
        public void Dispose()
        {
            lock (_Lifecycle) {
                var listener = _Listener;
                _Listener = null;
                listener?.Stop();
            }
        }
    }
}
