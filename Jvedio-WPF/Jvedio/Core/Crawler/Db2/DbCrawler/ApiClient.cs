using Newtonsoft.Json.Linq;
using SuperUtils.NetWork;
using SuperUtils.NetWork.Entity;
using SuperUtils.NetWork.Enums;
using SuperUtils.Values;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Jvedio.Crawler
{
    /// <summary>
    /// JavDB App API 客户端（镜像 jdforrepam.com）。
    /// 请求协议移植自 javdb-cli（MIT）：固定设备参数 + jdsignature 请求头
    /// （{unix秒}.lpw6vgqzsp.{md5(秒+前缀hex)}）+ 可选 Bearer token。
    /// 搜索与详情端点当前要求登录 token，匿名请求报 JWTVerificationError；
    /// 调用方（DBCrawler）在自动模式下先走网页解析，失败才回落到本 API。
    /// API 不提供 Director/Rating/Publisher/磁力，不输出即不覆盖既有值；
    /// API 的影片 id 未经核实与网页 /v/ 码一致，故不输出 DataCode（不写 UrlCode 缓存），
    /// WebUrl 保守地指向 JavDB 搜索页。
    /// </summary>
    internal static class JavDbApiClient
    {
        private const string API_BASE = "https://jdforrepam.com";
        private const string WEB_BASE = "https://javdb.com";
        private const string SIGN_PREFIX = "71cf27bb3c0bcdf207b64abecddc970098c7421ee7203b9cdae54478478a199e7d5a6e1a57691123c1a931c057842fb73ba3b3c83bcd69c17ccf174081e3d8aa";
        private const string SIGN_SUFFIX = "lpw6vgqzsp";
        private const int REQUEST_INTERVAL_MS = 500;

        // 每次进程启动生成一次设备身份（与 javdb-cli 的 device_uuid 语义一致）
        private static readonly string DeviceUuid = Guid.NewGuid().ToString();

        private static readonly SemaphoreSlim RateGate = new SemaphoreSlim(1, 1);
        private static DateTime _LastRequest = DateTime.MinValue;

        private static async Task RateLimitAsync()
        {
            await RateGate.WaitAsync().ConfigureAwait(false);
            try {
                TimeSpan elapsed = DateTime.Now - _LastRequest;
                int wait = REQUEST_INTERVAL_MS - (int)elapsed.TotalMilliseconds;
                if (wait > 0)
                    await Task.Delay(wait).ConfigureAwait(false);
                _LastRequest = DateTime.Now;
            } finally {
                RateGate.Release();
            }
        }

        private static string Signature(long unixSeconds)
        {
            using (MD5 md5 = MD5.Create()) {
                byte[] bytes = Encoding.UTF8.GetBytes(unixSeconds.ToString() + SIGN_PREFIX);
                byte[] hash = md5.ComputeHash(bytes);
                string hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                return unixSeconds + "." + SIGN_SUFFIX + "." + hex;
            }
        }

        private static RequestHeader BuildHeader(RequestHeader source, string apiToken, long unixSeconds)
        {
            RequestHeader result = new RequestHeader();
            if (source != null) {
                result.WebProxy = source.WebProxy;
                result.TimeOut = source.TimeOut;
            }
            result.AllowAutoRedirect = false;
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["User-Agent"] = "Dart/3.4 (dart:io)";
            headers["Accept"] = "application/json";
            headers["Accept-Language"] = "zh-TW";
            headers["jdsignature"] = Signature(unixSeconds);
            if (!string.IsNullOrWhiteSpace(apiToken))
                headers["Authorization"] = "Bearer " + apiToken.Trim();
            result.Headers = headers;
            return result;
        }

        private static string CommonQuery()
        {
            return "app_channel=official&app_version=1.9.28&app_version_number=10928&platform=android" +
                "&system_version=13&device_model=" + Uri.EscapeDataString("Pixel 6") +
                "&device_name=Pixel&device_uuid=" + Uri.EscapeDataString(DeviceUuid);
        }

        private static async Task<JObject> GetJson(string pathAndQuery, RequestHeader source, string apiToken, List<string> logs)
        {
            long unixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            string url = API_BASE + pathAndQuery + (pathAndQuery.Contains("?") ? "&" : "?") + CommonQuery();
            logs?.Add("javdb-api get: " + pathAndQuery);
            await RateLimitAsync().ConfigureAwait(false);
            HttpResult result = await HttpClient.Get(url, BuildHeader(source, apiToken, unixSeconds), (HttpMode)0).ConfigureAwait(false);
            if (result == null)
                throw new Exception("javdb-api: 空响应");
            if (result.StatusCode != System.Net.HttpStatusCode.OK || string.IsNullOrEmpty(result.SourceCode))
                throw new Exception("javdb-api: HTTP " + (int)result.StatusCode + (string.IsNullOrEmpty(result.Error) ? "" : " " + result.Error));
            JObject envelope;
            try {
                envelope = JObject.Parse(result.SourceCode);
            } catch (Exception ex) {
                throw new Exception("javdb-api: 响应非 JSON（" + ex.Message + "）");
            }
            string success = envelope["success"]?.ToString();
            if (success != "1" && !string.Equals(success, "true", StringComparison.OrdinalIgnoreCase))
                throw new Exception("javdb-api: " + (envelope["action"]?.ToString() ?? "请求被拒绝") +
                    (string.IsNullOrEmpty(envelope["message"]?.ToString()) ? "" : "（" + envelope["message"]?.ToString() + "）"));
            JToken data = envelope["data"];
            if (data == null || data.Type == JTokenType.Null)
                throw new Exception("javdb-api: 响应缺少 data");
            return (JObject)data;
        }

        private static void AddIfValue(Dictionary<string, object> info, string key, object value)
        {
            if (value == null)
                return;
            string text = value.ToString();
            if (string.IsNullOrWhiteSpace(text) || text == "N/A")
                return;
            info[key] = text;
        }

        private static string ResolveUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "";
            url = url.Trim();
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;
            if (url.StartsWith("//"))
                return "https:" + url;
            return WEB_BASE.TrimEnd('/') + "/" + url.TrimStart('/');
        }

        /// <summary>
        /// 按番号刮削：搜索精确匹配取影片 id，再取详情。
        /// 失败抛异常（含原因），成功返回含 Title 的信息字典。
        /// </summary>
        public static async Task<Dictionary<string, object>> GetInfo(string vid, RequestHeader header, string apiToken, List<string> logs)
        {
            Dictionary<string, object> info = new Dictionary<string, object>();
            List<string> apiLogs = logs ?? new List<string>();
            try {
                // JavDB 检索 FC2 番号不带 PPV 段，其余分隔符保持原样（javdb-cli 语义）
                string queryCode = vid;
                Match fc2 = Regex.Match(vid ?? "", "^FC2-(?:PPV-)?([0-9]+)$", RegexOptions.IgnoreCase);
                if (fc2.Success)
                    queryCode = "FC2-" + fc2.Groups[1].Value;

                JObject search = await GetJson("/api/v2/search?q=" + Uri.EscapeDataString(queryCode) + "&page=1&limit=100", header, apiToken, apiLogs).ConfigureAwait(false);
                JArray movies = search["movies"] as JArray;
                if (movies == null || movies.Count == 0)
                    throw new Exception("javdb-api: 搜索无结果");
                string movieId = null;
                foreach (JToken item in movies) {
                    string number = item?["number"]?.ToString();
                    if (string.IsNullOrEmpty(number) ||
                        !string.Equals(number.Trim(), queryCode.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;
                    string id = item["id"]?.ToString();
                    if (string.IsNullOrEmpty(id)) {
                        movieId = null;
                        break;
                    }
                    if (movieId != null && movieId != id) {
                        // 分隔符属于番号的一部分：多命中视为歧义，宁可不刮
                        movieId = null;
                        break;
                    }
                    movieId = id;
                }
                if (string.IsNullOrEmpty(movieId))
                    throw new Exception("javdb-api: 搜索结果中无精确匹配番号");

                JObject detail = await GetJson("/api/v4/movies/" + Uri.EscapeDataString(movieId), header, apiToken, apiLogs).ConfigureAwait(false);
                JObject movie = (detail["movie"] as JObject) ?? detail;
                if (movie == null)
                    throw new Exception("javdb-api: 详情响应结构异常");
                string number2 = movie["number"]?.ToString();
                if (!string.IsNullOrEmpty(number2) &&
                    !string.Equals(number2.Trim(), queryCode.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("javdb-api: 详情番号不匹配（" + number2 + "）");
                string title = movie["origin_title"]?.ToString();
                if (string.IsNullOrWhiteSpace(title))
                    title = movie["title"]?.ToString();
                if (string.IsNullOrWhiteSpace(title))
                    throw new Exception("javdb-api: 详情缺少标题");

                AddIfValue(info, "Title", title);
                AddIfValue(info, "ReleaseDate", movie["release_date"]?.ToString());
                AddIfValue(info, "Duration", movie["duration"]?.ToString());
                AddIfValue(info, "Studio", movie["maker_name"]?.ToString());
                AddIfValue(info, "Series", movie["series_name"]?.ToString());

                JArray tags = movie["tags"] as JArray;
                if (tags != null && tags.Count > 0) {
                    List<string> genres = new List<string>();
                    foreach (JToken tag in tags) {
                        string name = tag?["name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(name))
                            genres.Add(name);
                    }
                    if (genres.Count > 0)
                        info["Genre"] = string.Join(ConstValues.SeparatorString, genres);
                }

                JArray actors = movie["actors"] as JArray;
                if (actors != null && actors.Count > 0) {
                    List<string> actorNames = new List<string>();
                    foreach (JToken actor in actors) {
                        // gender=="0" 为女优（javdb-cli 语义）
                        if (actor?["gender"]?.ToString() != "0")
                            continue;
                        string name = actor["name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(name))
                            actorNames.Add(name);
                    }
                    if (actorNames.Count > 0)
                        info["ActorNames"] = actorNames;
                }

                string coverUrl = ResolveUrl(movie["cover_url"]?.ToString());
                AddIfValue(info, "BigImageUrl", coverUrl);

                JArray previews = movie["preview_images"] as JArray;
                if (previews != null && previews.Count > 0) {
                    List<string> extraImages = new List<string>();
                    foreach (JToken preview in previews) {
                        string large = ResolveUrl(preview?["large_url"]?.ToString());
                        if (string.IsNullOrEmpty(large))
                            large = ResolveUrl(preview?["thumb_url"]?.ToString());
                        if (!string.IsNullOrEmpty(large) && !extraImages.Contains(large))
                            extraImages.Add(large);
                    }
                    if (extraImages.Count > 0)
                        info["ExtraImageUrl"] = extraImages;
                }

                info["WebUrl"] = WEB_BASE + "/search?q=" + Uri.EscapeDataString(vid) + "&f=all";
                info["WebType"] = "db";
                info["StatusCode"] = 200;
                return info;
            } catch (Exception ex) {
                info["Error"] = ex.Message;
                if (!info.ContainsKey("StatusCode"))
                    info["StatusCode"] = 0;
                return info;
            } finally {
                info["Logs"] = apiLogs;
            }
        }
    }
}
