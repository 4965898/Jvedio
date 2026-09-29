using Jvedio.Core.Config;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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
                await SendFileAsync(client, type, fileName, archive);
                byte[] pointer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new LatestPointer {
                    FileName = fileName,
                    Sha256 = hash
                }));
                await SendBytesAsync(client, type, "latest.json", pointer);
            }
        }

        public static async Task<string> DownloadLatestAsync(string localDirectory)
        {
            string type = ConfigManager.Settings.BackupRemoteType;
            if (type != "WebDAV" && type != "S3")
                throw new InvalidOperationException("请先配置 WebDAV 或 S3 在线备份");
            Directory.CreateDirectory(localDirectory);
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) }) {
                byte[] pointerBytes = await GetBytesAsync(client, type, "latest.json");
                LatestPointer pointer = JsonConvert.DeserializeObject<LatestPointer>(Encoding.UTF8.GetString(pointerBytes));
                if (pointer == null || string.IsNullOrEmpty(pointer.FileName) ||
                    Path.GetFileName(pointer.FileName) != pointer.FileName || !pointer.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("在线备份清单无效");
                string target = Path.Combine(localDirectory, pointer.FileName);
                string temporary = target + ".partial";
                try {
                    using (var request = CreateRequest(type, HttpMethod.Get, pointer.FileName, HashBytes(new byte[0])))
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)) {
                        response.EnsureSuccessStatusCode();
                        using (var input = await response.Content.ReadAsStreamAsync())
                        using (var output = File.Create(temporary)) await input.CopyToAsync(output);
                    }
                    if (!string.Equals(HashFile(temporary), pointer.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("在线备份校验值不一致");
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(temporary, target);
                } finally { if (File.Exists(temporary)) File.Delete(temporary); }
                return target;
            }
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

        private static HttpRequestMessage CreateRequest(string type, HttpMethod method, string name, string hash)
        {
            var settings = ConfigManager.Settings;
            if (type == "WebDAV") {
                Uri root = RequireHttps(settings.BackupWebDavUrl);
                Uri url = new Uri(root.ToString().TrimEnd('/') + "/" + Uri.EscapeDataString(name));
                var request = new HttpRequestMessage(method, url);
                if (!string.IsNullOrEmpty(settings.BackupWebDavUser)) {
                    string credential = settings.BackupWebDavUser + ":" + Unprotect(settings.BackupWebDavPasswordProtected);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(Encoding.UTF8.GetBytes(credential)));
                }
                return request;
            }
            if (type != "S3") throw new InvalidOperationException("未知备份类型");
            if (string.IsNullOrWhiteSpace(settings.BackupS3Bucket) || string.IsNullOrWhiteSpace(settings.BackupS3AccessKey))
                throw new InvalidOperationException("请填写 S3 存储桶和访问密钥");
            string region = string.IsNullOrWhiteSpace(settings.BackupS3Region) ? "us-east-1" : settings.BackupS3Region.Trim();
            string endpoint = string.IsNullOrWhiteSpace(settings.BackupS3Endpoint)
                ? "https://s3." + region + ".amazonaws.com" : settings.BackupS3Endpoint.Trim();
            Uri rootUri = RequireHttps(endpoint);
            if (rootUri.AbsolutePath != "/")
                throw new InvalidOperationException("S3 Endpoint 请填写服务器根地址，不包含路径");
            string key = (settings.BackupS3Prefix ?? string.Empty).Trim('/') + "/" + name;
            key = key.TrimStart('/');
            string encodedPath = "/" + Uri.EscapeDataString(settings.BackupS3Bucket.Trim()) + "/" +
                string.Join("/", key.Split('/').Select(Uri.EscapeDataString));
            Uri urlS3 = new Uri(rootUri.ToString().TrimEnd('/') + encodedPath);
            var s3Request = new HttpRequestMessage(method, urlS3);
            DateTime now = DateTime.UtcNow;
            string stamp = now.ToString("yyyyMMddTHHmmssZ");
            string date = now.ToString("yyyyMMdd");
            string host = urlS3.IsDefaultPort ? urlS3.Host : urlS3.Host + ":" + urlS3.Port;
            string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
            string canonicalHeaders = "host:" + host + "\n" + "x-amz-content-sha256:" + hash + "\n" + "x-amz-date:" + stamp + "\n";
            string canonical = method.Method + "\n" + encodedPath + "\n\n" + canonicalHeaders + "\n" + signedHeaders + "\n" + hash;
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
