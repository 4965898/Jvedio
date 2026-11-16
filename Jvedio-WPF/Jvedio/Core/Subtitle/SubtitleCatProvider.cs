using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Jvedio.Core.Subtitle
{
    /// <summary>
    /// 字幕查询结果候选（一个候选=站上一条字幕条目）
    /// </summary>
    public class SubtitleCandidate
    {
        public string DetailUrl { get; set; }
        public string Title { get; set; }
        public bool TranslatedFromChinese { get; set; }
        public long Downloads { get; set; }
    }

    /// <summary>
    /// 字幕下载结果
    /// </summary>
    public class SubtitleResult
    {
        public string FilePath { get; set; }
        public string Language { get; set; }
        public string SourceTitle { get; set; }
    }

    /// <summary>
    /// SubtitleCat（subtitlecat.com）公共字幕站抓取（移植自 JavBoss GPL-3.0 的
    /// internal/subtitle/subtitlecat/provider.go 流程，按本工程 C#/net472 重写）。
    /// 流程：/index.php?search=番号 → 结果表解析候选（中文翻译优先、下载量次之）→
    /// 详情页解析 -zh-CN.srt 等语言轨道链接 → 下载字节。
    /// 番号边界正则防 ABC-123 匹配到 ABC-1234；分隔符容错（-/_/空格）。
    /// </summary>
    public static class SubtitleCatProvider
    {
        public const string BASE_URL = "https://www.subtitlecat.com";

        /// <summary>请求间隔：搜索与详情、详情与轨道间都遵守（对公共站点礼貌一点）</summary>
        private const int REQUEST_INTERVAL_MS = 500;
        private const int HTTP_TIMEOUT_SECONDS = 30;
        private const int MAX_SEARCH_HTML = 2 << 20;
        private const int MAX_TRACK_HTML = 4 << 20;

        private static readonly object RateLock = new object();
        private static long _NextRequestTicks;

        private const string USER_AGENT =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        private static readonly Regex DownloadsPattern =
            new Regex(@"([0-9][0-9,]*)\s+downloads?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>详情页语言轨道链接的后缀（如 /subs/483/SSIS-699-zh-CN.srt）</summary>
        private static readonly Regex LanguageSuffix =
            new Regex(@"-([a-z]{2,3}(?:-[a-z0-9]{2,8})?)\.srt$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// 番号边界匹配：容忍分隔符与大小写差异，整体数字边界（查 ABC-123 不接受 ABC-1234）
        /// </summary>
        private static Regex CodePattern(string code)
        {
            string[] parts = code.Split(new char[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Regex.Escape).ToArray();
            if (parts.Length == 0)
                return null;
            return new Regex(@"(^|[^a-zA-Z0-9])" + string.Join("[-_ ]?", parts) + @"([^a-zA-Z0-9]|$)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        /// <summary>站点同源校验：重定向/相对链接只允许留在 subtitlecat.com 内</summary>
        private static string ResolveUrl(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return null;
            try {
                Uri uri = new Uri(new Uri(BASE_URL + "/"), reference.Trim());
                if (!uri.Host.Equals("www.subtitlecat.com", StringComparison.OrdinalIgnoreCase) &&
                    !uri.Host.Equals("subtitlecat.com", StringComparison.OrdinalIgnoreCase))
                    return null;
                return uri.ToString();
            } catch { return null; }
        }

        private static async Task<byte[]> GetAsync(string url, int limit, CancellationToken token)
        {
            string address = ResolveUrl(url);
            if (address == null)
                return null;

            // 简易限速：与上次请求至少间隔 REQUEST_INTERVAL_MS
            long wait;
            lock (RateLock) {
                long now = DateTime.UtcNow.Ticks;
                wait = Math.Max(0, _NextRequestTicks - now);
                _NextRequestTicks = Math.Max(now, _NextRequestTicks) + REQUEST_INTERVAL_MS * TimeSpan.TicksPerMillisecond;
            }
            if (wait > 0)
                await Task.Delay((int)(wait / TimeSpan.TicksPerMillisecond), token);

            using (HttpClient http = new HttpClient()) {
                http.Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SECONDS);
                http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", USER_AGENT);
                http.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
                    "text/html,application/octet-stream;q=0.9,*/*;q=0.8");
                byte[] data = await http.GetByteArrayAsync(address);
                if (token.IsCancellationRequested)
                    return null;
                if (data == null || data.Length == 0 || data.Length > limit)
                    return null;
                return data;
            }
        }

        /// <summary>
        /// 搜索字幕候选：页面上无搜索表单视为站点改版/被拦截（临时失败，不算「无字幕」）
        /// </summary>
        public static async Task<List<SubtitleCandidate>> SearchAsync(string code, CancellationToken token)
        {
            string url = $"{BASE_URL}/index.php?search={Uri.EscapeDataString(code)}";
            byte[] data = await GetAsync(url, MAX_SEARCH_HTML, token);
            if (data == null)
                return null;

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(Encoding.UTF8.GetString(data));
            if (doc.DocumentNode.SelectSingleNode("//input[@name='search']") == null)
                return null;

            Regex pattern = CodePattern(code);
            if (pattern == null)
                return null;

            List<SubtitleCandidate> candidates = new List<SubtitleCandidate>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HtmlNode a in doc.DocumentNode.SelectNodes("//a[@href]")) {
                if (token.IsCancellationRequested)
                    return null;
                string address = ResolveUrl(a.GetAttributeValue("href", ""));
                if (address == null)
                    continue;
                Uri uri = new Uri(address);
                string title = (a.InnerText ?? "").Trim();
                if (!uri.AbsolutePath.StartsWith("/subs/") || !uri.AbsolutePath.EndsWith(".html") ||
                    !pattern.IsMatch(title) || !seen.Add(address))
                    continue;

                SubtitleCandidate candidate = new SubtitleCandidate {
                    DetailUrl = address,
                    Title = title,
                };
                // 来源标注取站点自己的「(translated from Chinese)」，不信标题里的词
                HtmlNode cell = a.ParentNode; // <td>
                if (cell != null && cell.Name.Equals("td", StringComparison.OrdinalIgnoreCase)) {
                    string sourceText = cell.InnerText ?? "";
                    candidate.TranslatedFromChinese =
                        sourceText.IndexOf("translated from chinese", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                // HtmlAgilityPack 1.11 无 Closest：手写向上找 <tr>（下载量在行的其他单元格里）
                HtmlNode row = cell;
                while (row != null && !row.Name.Equals("tr", StringComparison.OrdinalIgnoreCase))
                    row = row.ParentNode;
                if (row != null) {
                    foreach (HtmlNode td in row.SelectNodes(".//td") ?? Enumerable.Empty<HtmlNode>()) {
                        Match match = DownloadsPattern.Match(td.InnerText ?? "");
                        if (match.Success && long.TryParse(match.Groups[1].Value.Replace(",", ""), out long downloads)) {
                            candidate.Downloads = downloads;
                            break;
                        }
                    }
                }
                candidates.Add(candidate);
            }

            // 排序：中文翻译优先 > 下载量降序 > 保持页面原序
            List<SubtitleCandidate> ranked = new List<SubtitleCandidate>(candidates);
            ranked.Sort((x, y) => {
                if (x.TranslatedFromChinese != y.TranslatedFromChinese)
                    return x.TranslatedFromChinese ? -1 : 1;
                return y.Downloads.CompareTo(x.Downloads);
            });
            return ranked;
        }

        /// <summary>
        /// 取详情页的语言轨道下载链接。语言按 BCP47 风格归一（zh-CN/zh-TW 小写归一为标准大小写）。
        /// 页面无搜索表单视为站点改版（返回 null 区别于「无轨道」空表）
        /// </summary>
        public static async Task<Dictionary<string, string>> GetTracksAsync(string detailUrl, CancellationToken token)
        {
            byte[] data = await GetAsync(detailUrl, MAX_TRACK_HTML, token);
            if (data == null)
                return null;

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(Encoding.UTF8.GetString(data));
            bool pageAlive = doc.DocumentNode.SelectSingleNode("//input[@name='search']") != null;

            // 忽略大小写：用户偏好可能是 zh-cn，轨道键是规范化的 zh-CN
            Dictionary<string, string> tracks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (HtmlNode a in doc.DocumentNode.SelectNodes("//a[@href]")) {
                string address = ResolveUrl(a.GetAttributeValue("href", ""));
                if (address == null)
                    continue;
                Uri uri = new Uri(address);
                Match m = LanguageSuffix.Match(uri.AbsolutePath);
                if (!uri.AbsolutePath.StartsWith("/subs/") || !m.Success)
                    continue;
                string lang = m.Groups[1].Value.ToLowerInvariant();
                if (lang == "zh-cn") lang = "zh-CN";
                else if (lang == "zh-tw") lang = "zh-TW";
                tracks[lang] = address; // 同语言多个版本时保留页面后出现者（站点通常把机器翻译放后面）
            }
            if (tracks.Count == 0 && !pageAlive)
                return null;
            return tracks;
        }

        /// <summary>下载轨道字幕原始字节</summary>
        public static async Task<byte[]> DownloadTrackAsync(string trackUrl, CancellationToken token)
        {
            return await GetAsync(trackUrl, SrtNormalizer.MAX_BYTES, token);
        }

        /// <summary>
        /// 一步到位：按番号搜索并下载指定语言字幕（未命中该语言则返回 null）。
        /// detailLimit 限制逐个候选抓详情的次数（JavBoss 上限 4）
        /// </summary>
        public static async Task<SubtitleResult> FetchAsync(string code, IEnumerable<string> languages,
            CancellationToken token, int detailLimit = 4)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;
            List<SubtitleCandidate> candidates = await SearchAsync(code, token);
            if (candidates == null || candidates.Count == 0)
                return null;
            string[] wanted = languages?.ToArray() ?? new string[0];
            if (wanted.Length == 0)
                return null;

            int checkedCount = 0;
            foreach (SubtitleCandidate candidate in candidates) {
                if (token.IsCancellationRequested || checkedCount >= detailLimit)
                    break;
                // 候选标题里的番号必须与请求番号一致（防标题擦边命中）
                if (!CodeEquals(candidate.Title, code))
                    continue;
                checkedCount++;
                Dictionary<string, string> tracks = await GetTracksAsync(candidate.DetailUrl, token);
                if (tracks == null)
                    continue; // 详情页失败（临时性），试下一个
                foreach (string language in wanted) {
                    if (tracks.TryGetValue(language, out string trackUrl)) {
                        byte[] raw = await DownloadTrackAsync(trackUrl, token);
                        if (raw != null && SrtNormalizer.TryNormalize(raw, out byte[] normalized))
                            return new SubtitleResult { Language = language, SourceTitle = candidate.Title, FilePath = null };
                        // 下载失败/规范化失败：该候选该语言不可用，继续下一个候选
                        break;
                    }
                }
            }
            return null;
        }

        /// <summary>番号归一比较（去分隔符+大写）</summary>
        private static bool CodeEquals(string a, string b)
        {
            string Normalize(string s) => (s ?? "").Trim().ToUpperInvariant()
                .Replace("-", "").Replace("_", "").Replace(" ", "");
            return Normalize(a).Equals(Normalize(b));
        }
    }
}
