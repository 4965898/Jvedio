using Newtonsoft.Json.Linq;
using SuperUtils.IO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Jvedio.Core.Gfriends
{
    /// <summary>
    /// Gfriends 演员头像库（github.com/gfriends/gfriends，业界标准 JAV 头像源，按演员名索引、量大质高）。
    /// 索引 Filetree.json（约 6MB，Content[厂牌][演员名.jpg]=文件名）本地缓存 7 天，
    /// 「无本地头像时优先从此处取图」——「已有头像不覆盖」的保护在调用方，正好兼容。
    /// 任何失败静默返回 null，绝不阻断刮削主流程。
    /// </summary>
    public static class GfriendsAvatar
    {
        private const int INDEX_REFRESH_DAYS = 7;
        private const int HTTP_TIMEOUT_SECONDS = 30;

        /// <summary>索引与图床基址，依次回退（raw 直连 / jsDelivr 两条 CDN）</summary>
        private static readonly string[] BaseUrls = new string[] {
            "https://raw.githubusercontent.com/gfriends/gfriends/master/",
            "https://fastly.jsdelivr.net/gh/gfriends/gfriends@master/",
            "https://cdn.jsdelivr.net/gh/xinxin8816/gfriends/",
        };

        private const string USER_AGENT =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        /// <summary>厂牌 → (演员名.jpg → 文件名，可能带 ?t= 时间戳)；保持 JSON 顺序以便确定性取首个命中</summary>
        private static List<KeyValuePair<string, Dictionary<string, string>>> _Index;
        private static readonly object _IndexLock = new object();
        private static bool _IndexFailed;
        private static readonly ConcurrentDictionary<string, byte> _MissCache = new ConcurrentDictionary<string, byte>();

        private static string IndexFilePath {
            get { return Path.Combine(Jvedio.Core.Global.PathManager.CurrentUserFolder, "gfriends", "Filetree.json"); }
        }

        static GfriendsAvatar()
        {
            // raw.githubusercontent 要求 TLS 1.2+；只增不减，不影响其他网络路径
            try {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            } catch { }
        }

        /// <summary>设置开关（选项 → 同步信息里与演员头像保存并列）；关闭时全部调用直接返回 null</summary>
        public static bool Enabled {
            get { return ConfigManager.Settings?.GfriendsAvatar ?? false; }
        }

        /// <summary>
        /// 按演员名取头像字节。先日文名精确匹配，未命中做一次大小写/NFKC 归一线性扫描兜底。
        /// 拿不到返回 null（调用方回退到站点自己的头像 URL）
        /// </summary>
        public static async Task<byte[]> TryGetAvatarBytesAsync(string actorName, string actorNameEN = null)
        {
            if (!Enabled)
                return null;
            string relativePath = FindAvatarPath(actorName, actorNameEN);
            if (string.IsNullOrEmpty(relativePath))
                return null;
            return await DownloadBytesAsync(relativePath);
        }

        /// <summary>返回相对索引根的路径（如 Content/厂牌/文件名?t=xxx）；查无此人返回 null</summary>
        private static string FindAvatarPath(string actorName, string actorNameEN)
        {
            if (string.IsNullOrWhiteSpace(actorName) && string.IsNullOrWhiteSpace(actorNameEN))
                return null;
            if (!EnsureIndex())
                return null;

            // 同一演员只做一次全表兜底扫描：未命中结果缓存
            foreach (string name in new string[] { actorName, actorNameEN }) {
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                string key = name.Trim() + ".jpg";
                if (_MissCache.ContainsKey(key))
                    continue;

                // 1) 精确匹配（各厂牌按索引原序，首中即用）
                foreach (KeyValuePair<string, Dictionary<string, string>> lib in _Index) {
                    if (lib.Value != null && lib.Value.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))
                        return $"Content/{lib.Key}/{value}";
                }

                // 2) 兜底：大小写不敏感 + NFKC 归一的一次线性扫描
                string normalized = Normalize(key);
                foreach (KeyValuePair<string, Dictionary<string, string>> lib in _Index) {
                    if (lib.Value == null)
                        continue;
                    foreach (KeyValuePair<string, string> entry in lib.Value) {
                        if (Normalize(entry.Key).Equals(normalized, StringComparison.OrdinalIgnoreCase))
                            return $"Content/{lib.Key}/{entry.Value}";
                    }
                }
                _MissCache.TryAdd(key, 0);
            }
            return null;
        }

        private static string Normalize(string input)
        {
            return input.Normalize(NormalizationForm.FormKC);
        }

        /// <summary>加载索引：内存缓存 → 本地缓存文件（7 天内直接用）→ 下载（失败回退旧缓存文件）</summary>
        private static bool EnsureIndex()
        {
            if (_Index != null)
                return true;
            if (_IndexFailed)
                return false;
            lock (_IndexLock) {
                if (_Index != null)
                    return true;
                if (_IndexFailed)
                    return false;

                string cacheFile = IndexFilePath;
                bool cacheValid = false;
                try {
                    cacheValid = File.Exists(cacheFile) &&
                        (DateTime.Now - File.GetLastWriteTime(cacheFile)).TotalDays < INDEX_REFRESH_DAYS;
                } catch { }

                if (cacheValid) {
                    try {
                        _Index = ParseIndex(File.ReadAllBytes(cacheFile));
                        App.Logger.Info($"Gfriends 头像索引已从本地缓存加载（{_Index.Count} 个厂牌）");
                        return true;
                    } catch (Exception ex) {
                        App.Logger.Error($"Gfriends 本地索引解析失败，尝试重新下载：{ex.Message}");
                        _Index = null;
                    }
                }

                // 下载索引（base 列表逐个尝试；压缩不透明，交给 HttpClient 默认处理）
                foreach (string baseUrl in BaseUrls) {
                    try {
                        using (HttpClient http = CreateClient()) {
                            byte[] bytes = http.GetByteArrayAsync(baseUrl + "Filetree.json").GetAwaiter().GetResult();
                            if (bytes == null || bytes.Length < 1024)
                                continue;
                            List<KeyValuePair<string, Dictionary<string, string>>> parsed = ParseIndex(bytes);
                            if (parsed == null || parsed.Count == 0)
                                continue;
                            _Index = parsed;
                            FileHelper.ByteArrayToFile(bytes, cacheFile);
                            App.Logger.Info($"Gfriends 头像索引已下载并缓存（{_Index.Count} 个厂牌，{bytes.Length} 字节）");
                            return true;
                        }
                    } catch (Exception ex) {
                        App.Logger.Error($"Gfriends 索引下载失败（{baseUrl}）：{ex.Message}");
                    }
                }

                // 下载全败但有旧缓存文件：用过期数据兜底一次（防断网时完全不可用）
                if (!cacheValid && File.Exists(cacheFile)) {
                    try {
                        _Index = ParseIndex(File.ReadAllBytes(cacheFile));
                        App.Logger.Info($"Gfriends 索引下载失败，使用过期本地缓存（{_Index.Count} 个厂牌）");
                        return true;
                    } catch { }
                }

                _IndexFailed = true;
                App.Logger.Error("Gfriends 头像索引不可用，本次运行不再尝试");
                return false;
            }
        }

        /// <summary>解析 Filetree.json：Content[厂牌][演员名.jpg]=文件名；大小写重复键以 Newtonsoft 后者为准</summary>
        private static List<KeyValuePair<string, Dictionary<string, string>>> ParseIndex(byte[] bytes)
        {
            JObject root = JObject.Parse(Encoding.UTF8.GetString(bytes));
            JToken content = root["Content"];
            if (content == null)
                return null;
            List<KeyValuePair<string, Dictionary<string, string>>> result =
                new List<KeyValuePair<string, Dictionary<string, string>>>();
            foreach (KeyValuePair<string, JToken> lib in (JObject)content) {
                Dictionary<string, string> actors = new Dictionary<string, string>();
                if (lib.Value is JObject actorsJson) {
                    foreach (KeyValuePair<string, JToken> actor in actorsJson)
                        actors[actor.Key] = actor.Value?.ToString();
                }
                result.Add(new KeyValuePair<string, Dictionary<string, string>>(lib.Key, actors));
            }
            return result;
        }

        private static async Task<byte[]> DownloadBytesAsync(string relativePath)
        {
            // 值里可能带 ?t= 时间戳（缓存刷新用）：路径段逐段转义，查询串原样保留
            string pathPart = relativePath;
            string queryPart = string.Empty;
            int queryIndex = relativePath.IndexOf('?');
            if (queryIndex >= 0) {
                pathPart = relativePath.Substring(0, queryIndex);
                queryPart = relativePath.Substring(queryIndex);
            }
            string encodedPath = string.Join("/",
                pathPart.Split('/').Select(segment => Uri.EscapeDataString(segment)));
            string suffix = encodedPath + queryPart;

            foreach (string baseUrl in BaseUrls) {
                try {
                    using (HttpClient http = CreateClient()) {
                        byte[] bytes = await http.GetByteArrayAsync(baseUrl + suffix);
                        if (bytes != null && bytes.Length >= 1024)
                            return bytes;
                        App.Logger.Info($"Gfriends 头像下载内容异常（{bytes?.Length ?? 0} 字节）：{relativePath}");
                        return null;
                    }
                } catch (Exception ex) {
                    App.Logger.Error($"Gfriends 头像下载失败（{baseUrl}）：{ex.Message}");
                }
            }
            return null;
        }

        private static HttpClient CreateClient()
        {
            HttpClient http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SECONDS);
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", USER_AGENT);
            return http;
        }
    }
}
