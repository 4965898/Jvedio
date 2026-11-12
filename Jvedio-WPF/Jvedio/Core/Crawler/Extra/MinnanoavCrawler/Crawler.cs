using HtmlAgilityPack;
using SuperUtils.NetWork.Entity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Jvedio.Crawler
{
    /// <summary>
    /// MinnanoAV（みんなのAV）演员资料爬虫：按名字精确搜索 → /actress{id}.html 资料页，
    /// 解析 div.act-profile 的 サイズ（T/B/W/H/罩杯）与 生年月日，h1 附带罗马字名。
    /// 仅供「演员资料自动补齐」任务通过 GetActorInfo 调用；
    /// IsPluginAvailable 恒返回原因字符串，保证影片刮削链自动跳过本插件。
    /// </summary>
    public class MinnanoavCrawler
    {
        private const string DEFAULT_BASE_URL = "https://www.minnano-av.com";
        private const string CHROME_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        private static readonly Regex BirthDateRegex = new Regex("(\\d{4})年\\s*(\\d{1,2})月\\s*(\\d{1,2})日", RegexOptions.Compiled);
        private static readonly Regex HeightRegex = new Regex("(?i)T\\s*(\\d{2,3})", RegexOptions.Compiled);
        private static readonly Regex BustRegex = new Regex("(?i)B\\s*(\\d{2,3})", RegexOptions.Compiled);
        private static readonly Regex WaistRegex = new Regex("(?i)W\\s*(\\d{2,3})", RegexOptions.Compiled);
        private static readonly Regex HipsRegex = new Regex("(?i)H\\s*(\\d{2,3})", RegexOptions.Compiled);
        private static readonly Regex CupRegex = new Regex("(?i)B\\s*\\d{2,3}\\s*\\(\\s*([A-Z])\\s*カップ", RegexOptions.Compiled);
        // S25 = 鞋码 25cm（日本码）；站点只给 cm，转换成国内码（cm×2−10）存入 ShoeSize
        private static readonly Regex ShoeSizeRegex = new Regex("(?i)S\\s*(\\d{2}(?:\\.\\d)?)", RegexOptions.Compiled);
        private static readonly Regex ActressPathRegex = new Regex("^/actress\\d+\\.html$", RegexOptions.Compiled);

        public static string GetWebType()
        {
            return "minnanoav";
        }

        /// <summary>恒不可用于影片刮削（返回非空 = 跳过），仅作为演员资料源</summary>
        public static string IsPluginAvailable(Dictionary<string, object> dataInfo)
        {
            return "本插件仅用于演员资料自动补齐（GetActorInfo），不参与影片刮削";
        }

        private static string GetStrVal(Dictionary<string, object> dict, string key, string defaultVal = "")
        {
            if (!dict.ContainsKey(key))
                return defaultVal;
            object val = dict[key];
            if (val == null)
                return defaultVal;
            return val.ToString();
        }

        /// <summary>
        /// 演员资料查询。入参：ActorName（必填）、Url（源站点，可选）。
        /// 出参：ActorNameEN（罗马字）、Birthday（yyyy-MM-dd）、Height/Chest/Waist/Hipline（数字字符串）、
        /// Cup、WebUrl（资料页）、WebType="minnanoav"、Error/Logs。查无此人时 Error 说明原因。
        /// </summary>
        public static async Task<Dictionary<string, object>> GetActorInfo(RequestHeader header, Dictionary<string, object> dataInfo)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            List<string> logs = new List<string>();
            result["WebType"] = "minnanoav";
            string name = NormalizeName(GetStrVal(dataInfo, "ActorName"));
            if (string.IsNullOrEmpty(name)) {
                result["Error"] = "缺少演员名（ActorName）";
                result["Logs"] = logs;
                return result;
            }
            string baseUrl = GetStrVal(dataInfo, "Url", DEFAULT_BASE_URL).TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = DEFAULT_BASE_URL;

            // 1. 搜索（search_scope=actress；名字精确匹配才采用）
            string searchUrl = $"{baseUrl}/search_result.php?search_scope=actress&search_word={Uri.EscapeDataString(name)}&search=Go";
            logs.Add("search: " + searchUrl);
            var search = await GetHtmlAsync(header, searchUrl, "ja-JP,ja;q=0.9,en;q=0.7");
            if (search.statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(search.sourceCode)) {
                result["Error"] = DescribeFailure(search.statusCode, search.error, "搜索页");
                logs.Add($"搜索页失败: status={search.statusCode}, error={search.error ?? "(空响应)"}");
                result["Logs"] = logs;
                return result;
            }

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(search.sourceCode);

            // 直接落在资料页（重定向）或需从结果列表挑唯一精确匹配
            HtmlNode profileNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'act-profile')]");
            string profileUrl = "";
            if (profileNode == null) {
                profileUrl = FindProfileUrl(doc, searchUrl, name, logs);
                if (string.IsNullOrEmpty(profileUrl)) {
                    result["Error"] = "搜索结果中无精确同名演员（名字需与站点一致，可尝试日文原名）";
                    result["Logs"] = logs;
                    return result;
                }
                await Task.Delay(500); // 两次请求防风控间隔
                logs.Add("profile: " + profileUrl);
                var profile = await GetHtmlAsync(header, profileUrl, "ja-JP,ja;q=0.9,en;q=0.7");
                if (profile.statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(profile.sourceCode)) {
                    result["Error"] = DescribeFailure(profile.statusCode, profile.error, "资料页");
                    logs.Add($"资料页失败: status={profile.statusCode}, error={profile.error ?? "(空响应)"}");
                    result["Logs"] = logs;
                    return result;
                }
                doc = new HtmlDocument();
                doc.LoadHtml(profile.sourceCode);
                profileNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'act-profile')]");
            } else {
                // 直接重定向到资料页：尽量取 canonical 链接作为 WebUrl
                HtmlNode canonical = doc.DocumentNode.SelectSingleNode("//link[@rel='canonical' and @href]");
                string canonicalHref = canonical?.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(canonicalHref))
                    profileUrl = canonicalHref.StartsWith("http") ? canonicalHref : baseUrl + canonicalHref;
            }
            if (profileNode == null) {
                result["Error"] = "资料页缺少 act-profile 结构（站点改版？）";
                result["Logs"] = logs;
                return result;
            }
            if (string.IsNullOrEmpty(profileUrl))
                profileUrl = searchUrl;

            ParseProfile(doc, profileNode, profileUrl, result, logs);
            result["Logs"] = logs;
            return result;
        }

        /// <summary>
        /// 把请求失败翻译成可操作的原因：403 = Cloudflare 人机验证（站点对非浏览器请求
        /// 返回 "Just a moment" 拦截页，2026-11-12 实测），需要在源设置 Headers 里填入
        /// 浏览器 Cookie（含 cf_clearance）与相同 User-Agent 才能通过
        /// </summary>
        private static string DescribeFailure(int statusCode, string rawError, string stage)
        {
            if (!string.IsNullOrEmpty(rawError))
                return rawError;
            if (statusCode == 403)
                return $"{stage}被 Cloudflare 人机验证拦截（HTTP 403）：请在该源的 Headers 中填入浏览器访问 minnano-av.com 的 Cookie（含 cf_clearance）与相同的 User-Agent 后重试";
            return $"{stage}请求失败（HTTP {statusCode}）";
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            // NFKC 归一（全角→半角等）+ 去空白；与站点名精确比对
            string normalized = name.Normalize(NormalizationForm.FormKC).Trim();
            return Regex.Replace(normalized, "\\s+", "");
        }

        private static string FindProfileUrl(HtmlDocument doc, string pageUrl, string name, List<string> logs)
        {
            // h2.ttl a 文本与名字精确相等且唯一才采用（避免同名别字误挂资料）
            HashSet<string> matches = new HashSet<string>();
            HtmlNodeCollection links = doc.DocumentNode.SelectNodes("//h2[contains(@class,'ttl')]//a[@href]");
            logs.Add($"ttl-links: {(links == null ? 0 : links.Count)}");
            if (links == null)
                return "";
            foreach (HtmlNode link in links) {
                string text = NormalizeName(link.InnerText);
                if (text != name)
                    continue;
                string href = link.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href))
                    continue;
                string resolved = href.StartsWith("http") ? href : new Uri(new Uri(pageUrl), href).AbsoluteUri;
                Uri uri = new Uri(resolved);
                if (!ActressPathRegex.IsMatch(uri.AbsolutePath))
                    continue;
                matches.Add(uri.GetLeftPart(UriPartial.Query).TrimEnd('?'));
            }
            return matches.Count == 1 ? matches.First() : "";
        }

        private static void ParseProfile(HtmlDocument doc, HtmlNode profileNode, string profileUrl, Dictionary<string, object> result, List<string> logs)
        {
            // 行结构：td > span(标签) + p(值)
            Dictionary<string, string> fields = new Dictionary<string, string>();
            HtmlNodeCollection rows = profileNode.SelectNodes(".//tr");
            if (rows != null) {
                foreach (HtmlNode row in rows) {
                    HtmlNode cell = row.SelectSingleNode("./td");
                    if (cell == null)
                        continue;
                    HtmlNode labelNode = cell.SelectSingleNode("./span");
                    HtmlNode valueNode = cell.SelectSingleNode("./p");
                    if (labelNode == null || valueNode == null)
                        continue;
                    string label = labelNode.InnerText?.Trim() ?? "";
                    string value = valueNode.InnerText?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(value) && !fields.ContainsKey(label))
                        fields[label] = value;
                }
            }
            logs.Add($"profile-fields: {fields.Count}");

            // h1：日文名 + span 内罗马字（「日本語 / ROMAN」格式）
            HtmlNode heading = doc.DocumentNode.SelectSingleNode("//h1");
            if (heading != null) {
                HtmlNode span = heading.SelectSingleNode("./span");
                string roman = span?.InnerText?.Trim() ?? "";
                if (!string.IsNullOrEmpty(roman))
                    result["ActorNameEN"] = ParseRomanName(roman);
                logs.Add($"roman-name: {roman}");
            }

            if (fields.TryGetValue("サイズ", out string size)) {
                Match m = HeightRegex.Match(size);
                if (m.Success && !result.ContainsKey("Height"))
                    result["Height"] = m.Groups[1].Value;
                m = BustRegex.Match(size);
                if (m.Success && !result.ContainsKey("Chest"))
                    result["Chest"] = m.Groups[1].Value;
                m = WaistRegex.Match(size);
                if (m.Success && !result.ContainsKey("Waist"))
                    result["Waist"] = m.Groups[1].Value;
                m = HipsRegex.Match(size);
                if (m.Success && !result.ContainsKey("Hipline"))
                    result["Hipline"] = m.Groups[1].Value;
                m = CupRegex.Match(size);
                if (m.Success && !result.ContainsKey("Cup"))
                    result["Cup"] = m.Groups[1].Value.ToUpperInvariant();
                // S25 = 25cm（日本码）→ 国内码 = cm×2−10（25cm = 40 码），与 ShoeSize 字段惯例一致
                m = ShoeSizeRegex.Match(size);
                if (m.Success && !result.ContainsKey("ShoeSize") &&
                    double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double footCm) &&
                    footCm > 15 && footCm < 35) {
                    result["ShoeSize"] = Math.Round(footCm * 2 - 10).ToString();
                }
            }

            if (fields.TryGetValue("生年月日", out string birth)) {
                Match m = BirthDateRegex.Match(birth);
                if (m.Success) {
                    string iso = $"{m.Groups[1].Value}-{int.Parse(m.Groups[2].Value):00}-{int.Parse(m.Groups[3].Value):00}";
                    if (DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        result["Birthday"] = iso;
                }
            }

            result["WebUrl"] = profileUrl;
        }

        /// <summary>「MIKAMI Yua」→「Yua Mikami」（姓后置，与站点书写习惯相反）</summary>
        private static string ParseRomanName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            string[] parts = value.Split('/');
            string target = parts.Length > 1 ? parts[parts.Length - 1] : parts[0];
            string[] words = target.Split(new char[] { ' ', '\t', '\u00a0' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return "";
            if (words.Length == 1)
                return words[0];
            return string.Join(" ", words.Skip(1).Concat(new string[] { words[0] }));
        }

        private static async Task<(int statusCode, string sourceCode, string error)> GetHtmlAsync(RequestHeader header, string url, string acceptLanguage)
        {
            HttpClientHandler handler = new HttpClientHandler();
            try {
                if (header?.WebProxy != null) {
                    handler.Proxy = header.WebProxy;
                    handler.UseProxy = true;
                }
            } catch { }
            handler.AllowAutoRedirect = true;
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            using (HttpClient client = new HttpClient(handler, disposeHandler: true)) {
                try {
                    try {
                        long timeoutMs = Convert.ToInt64(header?.TimeOut ?? 0);
                        if (timeoutMs > 0)
                            client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
                    } catch { }
                    if (header?.Headers != null) {
                        foreach (var kv in header.Headers) {
                            try { client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value); } catch { }
                        }
                    }
                    // 用户在源 Headers 里自带 User-Agent 时（如过 Cloudflare 的场景）必须以其为准：TryAddWithoutValidation 是追加不是覆盖，会形成双 UA 头被 CF 拒绝
                    bool hasUserUa = false;
                    if (header?.Headers != null)
                        foreach (var k in header.Headers.Keys)
                            if (k.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)) { hasUserUa = true; break; }
                    if (!hasUserUa)
                        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", CHROME_UA);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", acceptLanguage);
                    using (var resp = await client.GetAsync(url)) {
                        int code = (int)resp.StatusCode;
                        string txt = await resp.Content.ReadAsStringAsync();
                        return (code, txt, null);
                    }
                } catch (Exception ex) {
                    return (-1, null, ex.Message);
                }
            }
        }
    }
}
