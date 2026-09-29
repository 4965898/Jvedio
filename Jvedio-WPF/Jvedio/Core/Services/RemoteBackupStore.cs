using Jvedio.Core.Config;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Jvedio.Core.Backup
{
    /// <summary>WebDAV and path-style S3 backup transport. Only the latest pointer is overwritten.</summary>
    public static class RemoteBackupStore
    {
        private sealed class LatestPointer
        {
            public string FileName { get; set; }
            public string Sha256 { get; set; }
        }

        public sealed class RemoteBackupItem
        {
            public string FileName { get; set; }
            public long Size { get; set; }
            public DateTime ModifiedUtc { get; set; }
            public string ModifiedText => ModifiedUtc == DateTime.MinValue ? string.Empty :
                ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            public string SizeText => Size <= 0 ? string.Empty : (Size / 1024d / 1024d).ToString("F1") + " MB";
        }

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return string.Empty;
            byte[] bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }

        public static string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText)) return string.Empty;
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedText),
                null, DataProtectionScope.CurrentUser));
        }

        public static async Task UploadAsync(string archive)
        {
            string type = ConfigManager.Settings.BackupRemoteType;
            string fileName = Path.GetFileName(archive);
            if (type != "WebDAV" && type != "S3") return;
            string hash = HashFile(archive);
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) }) {
                if (type == "WebDAV") await EnsureWebDavFolderAsync(client);
                await SendFileAsync(client, type, fileName, archive);
                byte[] pointer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new LatestPointer {
                    FileName = fileName,
                    Sha256 = hash
                }));
                await SendBytesAsync(client, type, "latest.json", pointer);
            }
        }

        /// <summary>
        /// Verify the same PUT/GET permissions used by backup and restore. Returns the name of
        /// a probe object only when reading and writing succeeded but cleanup was denied.
        /// </summary>
        public static async Task<string> TestConnectionAsync(string type)
        {
            if (type != "WebDAV" && type != "S3")
                throw new InvalidOperationException("未知备份类型");
            string name = ".jvedio-connection-test-" + Guid.NewGuid().ToString("N") + ".txt";
            byte[] expected = Encoding.UTF8.GetBytes("Jvedio backup connection test: " + name);
            bool uploaded = false;
            bool cleaned = true;
            Exception testFailure = null;
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) }) {
                try {
                    if (type == "WebDAV") await EnsureWebDavFolderAsync(client);
                    await SendBytesAsync(client, type, name, expected);
                    uploaded = true;
                    byte[] actual = await GetBytesAsync(client, type, name);
                    if (!actual.SequenceEqual(expected))
                        throw new InvalidDataException("在线存储返回的测试文件内容不一致");
                } catch (Exception ex) { testFailure = ex; }
                if (uploaded) {
                    try { await DeleteAsync(client, type, name); }
                    catch { cleaned = false; }
                }
            }
            if (testFailure != null) {
                if (!cleaned)
                    throw new InvalidOperationException("连接测试失败，且临时文件 " + name +
                        " 未能清理：" + testFailure.Message, testFailure);
                ExceptionDispatchInfo.Capture(testFailure).Throw();
            }
            return cleaned ? null : name;
        }

        public static async Task<IReadOnlyList<RemoteBackupItem>> ListBackupsAsync()
        {
            string type = ConfigManager.Settings.BackupRemoteType;
            if (type != "WebDAV" && type != "S3")
                throw new InvalidOperationException("请先配置 WebDAV 或 S3 在线备份");
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) }) {
                List<RemoteBackupItem> items = type == "WebDAV"
                    ? await ListWebDavAsync(client) : await ListS3Async(client);
                return items.GroupBy(item => item.FileName, StringComparer.Ordinal)
                    .Select(group => group.OrderByDescending(item => item.ModifiedUtc).First())
                    .OrderByDescending(item => item.ModifiedUtc)
                    .ThenByDescending(item => item.FileName, StringComparer.Ordinal).ToList();
            }
        }

        public static async Task<string> DownloadAsync(string fileName, string localDirectory)
        {
            string type = ConfigManager.Settings.BackupRemoteType;
            if (type != "WebDAV" && type != "S3")
                throw new InvalidOperationException("请先配置 WebDAV 或 S3 在线备份");
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
                return await DownloadFileAsync(client, type, fileName, localDirectory, null);
        }

        public static async Task<string> DownloadLatestAsync(string localDirectory)
        {
            string type = ConfigManager.Settings.BackupRemoteType;
            if (type != "WebDAV" && type != "S3")
                throw new InvalidOperationException("请先配置 WebDAV 或 S3 在线备份");
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) }) {
                byte[] pointerBytes = await GetBytesAsync(client, type, "latest.json");
                LatestPointer pointer = JsonConvert.DeserializeObject<LatestPointer>(Encoding.UTF8.GetString(pointerBytes));
                if (pointer == null || !SafeArchiveName(pointer.FileName))
                    throw new InvalidDataException("在线备份清单无效");
                return await DownloadFileAsync(client, type, pointer.FileName, localDirectory, pointer.Sha256);
            }
        }

        private static bool SafeArchiveName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(new[] { '/', '\\' }) < 0 &&
                name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !Path.IsPathRooted(name) &&
                name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<string> DownloadFileAsync(HttpClient client, string type,
            string fileName, string localDirectory, string expectedHash)
        {
            if (!SafeArchiveName(fileName)) throw new InvalidDataException("在线备份文件名无效");
            Directory.CreateDirectory(localDirectory);
            string target = Path.Combine(localDirectory, fileName);
            string temporary = target + ".partial";
            try {
                using (var request = CreateRequest(type, HttpMethod.Get, fileName, HashBytes(new byte[0])))
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)) {
                    response.EnsureSuccessStatusCode();
                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = File.Create(temporary)) await input.CopyToAsync(output);
                }
                if (!string.IsNullOrEmpty(expectedHash) &&
                    !string.Equals(HashFile(temporary), expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("在线备份校验值不一致");
                if (File.Exists(target)) File.Delete(target);
                File.Move(temporary, target);
                return target;
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static async Task<List<RemoteBackupItem>> ListWebDavAsync(HttpClient client)
        {
            Uri folder = WebDavCollectionUri();
            using (var request = CreateWebDavRequest(new HttpMethod("PROPFIND"), folder)) {
                request.Headers.TryAddWithoutValidation("Depth", "1");
                request.Content = new StringContent(
                    "<?xml version=\"1.0\"?><d:propfind xmlns:d=\"DAV:\"><d:prop>" +
                    "<d:getcontentlength/><d:getlastmodified/><d:resourcetype/>" +
                    "</d:prop></d:propfind>", Encoding.UTF8, "application/xml");
                using (var response = await client.SendAsync(request)) {
                    response.EnsureSuccessStatusCode();
                    XDocument document = ReadXml(await response.Content.ReadAsStringAsync());
                    var items = new List<RemoteBackupItem>();
                    string pathPrefix = folder.AbsolutePath;
                    foreach (XElement entry in document.Descendants().Where(x => x.Name.LocalName == "response")) {
                        string href = entry.Elements().FirstOrDefault(x => x.Name.LocalName == "href")?.Value;
                        if (string.IsNullOrWhiteSpace(href)) continue;
                        Uri url = Uri.TryCreate(href, UriKind.Absolute, out Uri absolute)
                            ? absolute : new Uri(folder, href);
                        if (url.Scheme != folder.Scheme || url.Host != folder.Host || url.Port != folder.Port ||
                            !url.AbsolutePath.StartsWith(pathPrefix, StringComparison.Ordinal)) continue;
                        string relative = url.AbsolutePath.Substring(pathPrefix.Length);
                        if (relative.IndexOf('/') >= 0) continue;
                        string name = Uri.UnescapeDataString(relative);
                        if (!SafeArchiveName(name)) continue;
                        long.TryParse(entry.Descendants().FirstOrDefault(x => x.Name.LocalName == "getcontentlength")?.Value,
                            NumberStyles.Integer, CultureInfo.InvariantCulture, out long size);
                        DateTime modified = ParseRemoteDate(entry.Descendants()
                            .FirstOrDefault(x => x.Name.LocalName == "getlastmodified")?.Value, name);
                        items.Add(new RemoteBackupItem { FileName = name, Size = size, ModifiedUtc = modified });
                    }
                    return items;
                }
            }
        }

        private static async Task<List<RemoteBackupItem>> ListS3Async(HttpClient client)
        {
            string[] segments = FolderSegments(ConfigManager.Settings.BackupS3Prefix);
            string prefix = segments.Length == 0 ? string.Empty : string.Join("/", segments) + "/";
            string bucketPath = "/" + Uri.EscapeDataString(S3BucketName());
            var items = new List<RemoteBackupItem>();
            var seenTokens = new HashSet<string>(StringComparer.Ordinal);
            string continuation = null;
            while (true) {
                var parameters = new Dictionary<string, string> { { "list-type", "2" } };
                if (prefix.Length > 0) parameters["prefix"] = prefix;
                if (!string.IsNullOrEmpty(continuation)) parameters["continuation-token"] = continuation;
                string query = CanonicalQuery(parameters);
                using (var request = CreateS3Request(HttpMethod.Get, bucketPath, query, HashBytes(new byte[0])))
                using (var response = await client.SendAsync(request)) {
                    response.EnsureSuccessStatusCode();
                    XDocument document = ReadXml(await response.Content.ReadAsStringAsync());
                    foreach (XElement entry in document.Descendants().Where(x => x.Name.LocalName == "Contents")) {
                        string key = entry.Elements().FirstOrDefault(x => x.Name.LocalName == "Key")?.Value;
                        if (key == null || !key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                        string name = key.Substring(prefix.Length);
                        if (!SafeArchiveName(name)) continue;
                        long.TryParse(entry.Elements().FirstOrDefault(x => x.Name.LocalName == "Size")?.Value,
                            NumberStyles.Integer, CultureInfo.InvariantCulture, out long size);
                        DateTime modified = ParseRemoteDate(entry.Elements()
                            .FirstOrDefault(x => x.Name.LocalName == "LastModified")?.Value, name);
                        items.Add(new RemoteBackupItem { FileName = name, Size = size, ModifiedUtc = modified });
                    }
                    bool truncated = string.Equals(document.Descendants()
                        .FirstOrDefault(x => x.Name.LocalName == "IsTruncated")?.Value, "true",
                        StringComparison.OrdinalIgnoreCase);
                    if (!truncated) return items;
                    continuation = document.Descendants()
                        .FirstOrDefault(x => x.Name.LocalName == "NextContinuationToken")?.Value;
                    if (string.IsNullOrEmpty(continuation) || !seenTokens.Add(continuation))
                        throw new InvalidDataException("S3 备份列表分页标记无效");
                }
            }
        }

        private static XDocument ReadXml(string content)
        {
            using (var text = new StringReader(content))
            using (var reader = XmlReader.Create(text, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null
            })) return XDocument.Load(reader);
        }

        private static DateTime ParseRemoteDate(string value, string fileName)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
                return parsed;
            string stem = Path.GetFileNameWithoutExtension(fileName);
            if (DateTime.TryParseExact(stem, new[] { "yyyy-MM-dd_HHmmss_fff", "yyyy-MM-dd_HHmmss" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local))
                return local.ToUniversalTime();
            return DateTime.MinValue;
        }

        private static async Task SendFileAsync(HttpClient client, string type, string name, string path)
        {
            string hash = HashFile(path);
            using (var stream = File.OpenRead(path))
            using (var request = CreateRequest(type, HttpMethod.Put, name, hash)) {
                request.Content = new StreamContent(stream);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                using (var response = await client.SendAsync(request)) response.EnsureSuccessStatusCode();
            }
        }

        private static async Task SendBytesAsync(HttpClient client, string type, string name, byte[] bytes)
        {
            using (var request = CreateRequest(type, HttpMethod.Put, name, HashBytes(bytes))) {
                request.Content = new ByteArrayContent(bytes);
                using (var response = await client.SendAsync(request)) response.EnsureSuccessStatusCode();
            }
        }

        private static async Task<byte[]> GetBytesAsync(HttpClient client, string type, string name)
        {
            using (var request = CreateRequest(type, HttpMethod.Get, name, HashBytes(new byte[0])))
            using (var response = await client.SendAsync(request)) {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync();
            }
        }

        private static async Task DeleteAsync(HttpClient client, string type, string name)
        {
            using (var request = CreateRequest(type, HttpMethod.Delete, name, HashBytes(new byte[0])))
            using (var response = await client.SendAsync(request)) response.EnsureSuccessStatusCode();
        }

        private static string[] FolderSegments(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return Array.Empty<string>();
            if (folder.Contains("://") || folder.Contains(":\\"))
                throw new InvalidOperationException("在线备份文件夹请填写相对路径，不要填写完整 URL 或本地路径");
            string[] segments = folder.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => segment == "." || segment == ".."))
                throw new InvalidOperationException("在线备份文件夹不能包含 . 或 .. 路径段");
            return segments;
        }

        private static string WebDavBaseUrl()
        {
            Uri root = RequireHttps(ConfigManager.Settings.BackupWebDavUrl);
            if (!string.IsNullOrEmpty(root.Query) || !string.IsNullOrEmpty(root.Fragment))
                throw new InvalidOperationException("WebDAV URL 不能包含查询参数或片段");
            return root.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }

        private static Uri WebDavCollectionUri()
        {
            string url = WebDavBaseUrl();
            foreach (string segment in FolderSegments(ConfigManager.Settings.BackupWebDavFolder))
                url += "/" + Uri.EscapeDataString(segment);
            return new Uri(url + "/");
        }

        private static HttpRequestMessage CreateWebDavRequest(HttpMethod method, Uri url)
        {
            var settings = ConfigManager.Settings;
            var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrEmpty(settings.BackupWebDavUser)) {
                string credential = settings.BackupWebDavUser + ":" + Unprotect(settings.BackupWebDavPasswordProtected);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(credential)));
            }
            return request;
        }

        private static async Task EnsureWebDavFolderAsync(HttpClient client)
        {
            string[] segments = FolderSegments(ConfigManager.Settings.BackupWebDavFolder);
            if (segments.Length == 0) return;
            string current = WebDavBaseUrl();
            var mkcol = new HttpMethod("MKCOL");
            foreach (string segment in segments) {
                current += "/" + Uri.EscapeDataString(segment);
                using (var request = CreateWebDavRequest(mkcol, new Uri(current + "/")))
                using (var response = await client.SendAsync(request)) {
                    // An existing WebDAV collection responds with 405 per RFC 4918.
                    if (response.StatusCode != HttpStatusCode.MethodNotAllowed)
                        response.EnsureSuccessStatusCode();
                }
            }
        }

        private static HttpRequestMessage CreateRequest(string type, HttpMethod method, string name, string hash)
        {
            var settings = ConfigManager.Settings;
            if (type == "WebDAV") {
                return CreateWebDavRequest(method, new Uri(WebDavCollectionUri(), Uri.EscapeDataString(name)));
            }
            if (type != "S3") throw new InvalidOperationException("未知备份类型");
            string encodedPath = "/" + Uri.EscapeDataString(S3BucketName()) + "/" +
                string.Join("/", FolderSegments(settings.BackupS3Prefix).Concat(new[] { name }).Select(Uri.EscapeDataString));
            return CreateS3Request(method, encodedPath, string.Empty, hash);
        }

        private static string S3BucketName()
        {
            var settings = ConfigManager.Settings;
            if (string.IsNullOrWhiteSpace(settings.BackupS3Bucket) || string.IsNullOrWhiteSpace(settings.BackupS3AccessKey))
                throw new InvalidOperationException("请填写 S3 存储桶和访问密钥");
            return settings.BackupS3Bucket.Trim();
        }

        private static string CanonicalQuery(IEnumerable<KeyValuePair<string, string>> parameters)
        {
            return string.Join("&", parameters.Select(pair => new {
                Key = Uri.EscapeDataString(pair.Key), Value = Uri.EscapeDataString(pair.Value ?? string.Empty)
            }).OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ThenBy(pair => pair.Value, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value));
        }

        private static HttpRequestMessage CreateS3Request(HttpMethod method, string encodedPath,
            string canonicalQuery, string hash)
        {
            var settings = ConfigManager.Settings;
            S3BucketName();
            string region = string.IsNullOrWhiteSpace(settings.BackupS3Region) ? "us-east-1" : settings.BackupS3Region.Trim();
            string endpoint = string.IsNullOrWhiteSpace(settings.BackupS3Endpoint)
                ? "https://s3." + region + ".amazonaws.com" : settings.BackupS3Endpoint.Trim();
            Uri rootUri = RequireHttps(endpoint);
            if (rootUri.AbsolutePath != "/" || rootUri.Query.Length > 0 || rootUri.Fragment.Length > 0)
                throw new InvalidOperationException("S3 Endpoint 请填写服务器根地址，不包含路径");
            Uri urlS3 = new Uri(rootUri.ToString().TrimEnd('/') + encodedPath +
                (string.IsNullOrEmpty(canonicalQuery) ? string.Empty : "?" + canonicalQuery));
            var s3Request = new HttpRequestMessage(method, urlS3);
            DateTime now = DateTime.UtcNow;
            string stamp = now.ToString("yyyyMMddTHHmmssZ");
            string date = now.ToString("yyyyMMdd");
            string host = urlS3.IsDefaultPort ? urlS3.Host : urlS3.Host + ":" + urlS3.Port;
            string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
            string canonicalHeaders = "host:" + host + "\n" + "x-amz-content-sha256:" + hash + "\n" + "x-amz-date:" + stamp + "\n";
            string canonical = method.Method + "\n" + encodedPath + "\n" + canonicalQuery + "\n" +
                canonicalHeaders + "\n" + signedHeaders + "\n" + hash;
            string scope = date + "/" + region + "/s3/aws4_request";
            string toSign = "AWS4-HMAC-SHA256\n" + stamp + "\n" + scope + "\n" + HashBytes(Encoding.UTF8.GetBytes(canonical));
            byte[] secret = Encoding.UTF8.GetBytes("AWS4" + Unprotect(settings.BackupS3SecretKeyProtected));
            byte[] signing = Hmac(Hmac(Hmac(Hmac(secret, date), region), "s3"), "aws4_request");
            string signature = ToHex(Hmac(signing, toSign));
            s3Request.Headers.Host = host;
            s3Request.Headers.TryAddWithoutValidation("x-amz-content-sha256", hash);
            s3Request.Headers.TryAddWithoutValidation("x-amz-date", stamp);
            s3Request.Headers.TryAddWithoutValidation("Authorization", "AWS4-HMAC-SHA256 Credential=" +
                settings.BackupS3AccessKey.Trim() + "/" + scope + ", SignedHeaders=" + signedHeaders + ", Signature=" + signature);
            return s3Request;
        }

        private static Uri RequireHttps(string address)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
                throw new InvalidOperationException("在线备份地址必须使用 HTTPS（本机地址可用 HTTP）");
            return uri;
        }

        private static byte[] Hmac(byte[] key, string data)
        {
            using (var hmac = new HMACSHA256(key)) return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create()) return ToHex(hash.ComputeHash(stream));
        }

        private static string HashBytes(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return ToHex(hash.ComputeHash(bytes));
        }

        private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
