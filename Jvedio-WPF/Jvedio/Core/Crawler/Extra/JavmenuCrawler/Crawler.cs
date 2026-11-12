using HtmlAgilityPack;
using SuperUtils.NetWork.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Jvedio.Crawler
{
    /// <summary>
    /// JavMenu（javmenu）爬虫：详情页 URL 即 {站点}/{大写番号}，
    /// 信息集中在含「影片資料」的 div.card.rounded div.card-body 中（行 = div，标签 = span）。
    /// 作为备胎源排在内置源之后；系列/类别/演员等字段可补齐。
    /// </summary>
    public class JavmenuCrawler
    {
        private const string DEFAULT_BASE_URL = "https://javmenu.com";
        private const string CHROME_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        public static string GetWebType()
        {
            return "javmenu";
        }

        public static string IsPluginAvailable(Dictionary<string, object> dataInfo)
        {
            if (dataInfo == null)
                return "dataInfo is null";
            if (!dataInfo.ContainsKey("VID") || string.IsNullOrEmpty(dataInfo["VID"]?.ToString()))
                return "no VID";
            return "";
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

        public static async Task<Dictionary<string, object>> GetInfo(RequestHeader header, Dictionary<string, object> dataInfo)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            List<string> logs = new List<string>();
            if (dataInfo == null || !dataInfo.ContainsKey("VID")) {
                result["Error"] = "no VID";
                result["StatusCode"] = -1;
                return result;
            }
            string baseUrl = GetStrVal(dataInfo, "Url", DEFAULT_BASE_URL).TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = DEFAULT_BASE_URL;
            string vid = GetStrVal(dataInfo, "VID").Trim().ToUpper();
            string url = $"{baseUrl}/{Uri.EscapeDataString(vid)}";
            result["WebUrl"] = url;
            result["WebType"] = "javmenu";

            logs.Add("detail: " + url);
            var resp = await GetHtmlAsync(header, url, "zh-TW,zh;q=0.9,en;q=0.8");
            result["StatusCode"] = resp.statusCode;
            if (resp.statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(resp.sourceCode)) {
                result["Error"] = string.IsNullOrEmpty(resp.error) ? "详情页请求失败" : resp.error;
                logs.Add($"详情页失败: status={resp.statusCode}, error={resp.error ?? "(空响应)"}");
                result["Logs"] = logs;
                return result;
            }

            ParseDetail(resp.sourceCode, vid, result, logs);
            if (logs.Count > 0)
                result["Logs"] = logs;
            return result;
        }

        private static string NormalizeLabel(string label)
        {
            label = (label ?? "").Trim().ToLowerInvariant();
            label = label.TrimEnd(':', '：').Trim();
            return string.Join("", label.Split(new char[] { ' ', '\t', '\u00a0' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string CleanTitle(string title, string code)
        {
            title = (title ?? "").Trim();
            if (string.IsNullOrEmpty(title))
                return "";
            title = title.Replace("免費AV在線看", "").Replace("免费AV在线看", "").Trim();
            if (!string.IsNullOrEmpty(code))
                title = title.Trim().StartsWith(code, StringComparison.OrdinalIgnoreCase)
                    ? title.Substring(code.Length).Trim() : title;
            title = Regex.Replace(title, "(?i)^[a-z]{2,8}[-_ ]?\\d{2,6}[a-z]{0,3}\\s+", "");
            int idx = title.IndexOf(" | ", StringComparison.Ordinal);
            if (idx >= 0)
                title = title.Substring(0, idx).Trim();
            return title.Trim();
        }

        private static void ParseDetail(string html, string vid, Dictionary<string, object> result, List<string> logs)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 定位含「影片資料/影片资料」的 div.card-body
            HtmlNode cardBody = null;
            HtmlNodeCollection bodies = doc.DocumentNode.SelectNodes("//div[contains(@class,'card') and contains(@class,'rounded')]//div[contains(@class,'card-body')]");
            logs.Add($"card-bodies: {(bodies == null ? 0 : bodies.Count)}");
            if (bodies != null) {
                foreach (HtmlNode body in bodies) {
                    string text = body.InnerText ?? "";
                    if (text.Contains("影片資料") || text.Contains("影片资料")) {
                        cardBody = body;
                        break;
                    }
                }
            }
            if (cardBody == null) {
                result["Error"] = "详情页响应 200 但未找到影片資料卡片：可能被人机验证拦截、番号不存在或站点改版";
                return;
            }

            // 行结构：div > span(标签) + 值（链接文本优先）
            HtmlNodeCollection rows = cardBody.SelectNodes("./div");
            if (rows != null) {
                List<string> genres = new List<string>();
                List<string> actors = new List<string>();
                foreach (HtmlNode row in rows) {
                    HtmlNode labelNode = row.SelectSingleNode("./span");
                    if (labelNode == null)
                        continue;
                    string labelText = labelNode.InnerText?.Trim() ?? "";
                    string label = NormalizeLabel(labelText);
                    if (string.IsNullOrEmpty(label))
                        continue;
                    string rowText = row.InnerText?.Trim() ?? "";
                    string value = rowText.StartsWith(labelText) ? rowText.Substring(labelText.Length).Trim() : rowText;
                    HtmlNode firstLink = row.SelectSingleNode("./a");
                    string linkText = firstLink?.InnerText?.Trim();

                    if (label == "番號" || label == "番号" || label == "識別碼" || label == "识别码") {
                        // 番号行：仅用于校验（VID 已知）
                    } else if (label == "發佈於" || label == "发布于" || label == "發行日期" || label == "发行日期" ||
                        label == "発売日" || label == "releasedate") {
                        Match m = Regex.Match(value, "[0-9]{4}-[0-9]{2}-[0-9]{2}");
                        if (m.Success && !result.ContainsKey("ReleaseDate"))
                            result["ReleaseDate"] = m.Value;
                    } else if (label == "時長" || label == "时长" || label == "長度" || label == "长度" ||
                        label == "duration" || label == "runtime") {
                        Match m = Regex.Match(value, "[0-9]+");
                        if (m.Success && !result.ContainsKey("Duration"))
                            result["Duration"] = m.Value;
                    } else if (label == "出版" || label == "發行" || label == "发行" || label == "片商" ||
                        label == "製作商" || label == "制作商" || label == "studio" || label == "maker" || label == "publisher") {
                        if (!result.ContainsKey("Studio"))
                            result["Studio"] = string.IsNullOrEmpty(linkText) ? value : linkText;
                    } else if (label == "系列" || label == "series") {
                        if (!result.ContainsKey("Series"))
                            result["Series"] = string.IsNullOrEmpty(linkText) ? value : linkText;
                    } else if (label == "類別" || label == "类别" || label == "主題" || label == "主题" ||
                        label == "genre" || label == "genres" || label == "tags") {
                        HtmlNodeCollection genreLinks = row.SelectNodes(".//a[contains(@class,'genre')]");
                        if (genreLinks != null) {
                            foreach (HtmlNode a in genreLinks) {
                                string name = a.InnerText?.Trim();
                                if (!string.IsNullOrEmpty(name))
                                    genres.Add(name);
                            }
                        }
                    } else if (label == "女優" || label == "女优" || label == "演員" || label == "演员" ||
                        label == "actress" || label == "actor" || label == "actors") {
                        HtmlNodeCollection actressLinks = row.SelectNodes(".//a[contains(@class,'actress')]");
                        if (actressLinks != null) {
                            foreach (HtmlNode a in actressLinks) {
                                string name = a.InnerText?.Trim();
                                if (!string.IsNullOrEmpty(name))
                                    actors.Add(name);
                            }
                        }
                    }
                }
                if (genres.Count > 0) {
                    // 保留顺序去重
                    result["Genre"] = string.Join("\a", genres.Distinct());
                }
                if (actors.Count > 0)
                    result["ActorNames"] = actors.Distinct().ToList();
            }

            // 标题：h1 → 兜底 <title>
            string title = CleanTitle(doc.DocumentNode.SelectSingleNode("//h1")?.InnerText, vid);
            if (string.IsNullOrEmpty(title))
                title = CleanTitle(doc.DocumentNode.SelectSingleNode("//title")?.InnerText, vid);
            if (!string.IsNullOrEmpty(title))
                result["Title"] = title;

            // 封面（尽力而为）：javmenu 多聚合 DMM 图源
            HtmlNode coverImg = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'column-video-cover')]//img[@src]");
            string cover = coverImg?.GetAttributeValue("src", "");
            if (string.IsNullOrEmpty(cover)) {
                HtmlNodeCollection dmmImgs = doc.DocumentNode.SelectNodes("//img[contains(@src,'dmm.co.jp') or contains(@src,'pics.dmm')]");
                cover = dmmImgs?.FirstOrDefault()?.GetAttributeValue("src", "");
            }
            if (!string.IsNullOrEmpty(cover))
                result["BigImageUrl"] = cover;

            if (!result.ContainsKey("Title") && !result.ContainsKey("Error"))
                result["Error"] = "详情页响应 200 但未解析出影片信息：可能被人机验证/防爬页拦截或站点改版";
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
