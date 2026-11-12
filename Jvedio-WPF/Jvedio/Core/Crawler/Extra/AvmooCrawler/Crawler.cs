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
    /// AvMoo（avmoo）爬虫：HTML 解析（AvMoo 与 JavBus 同族模板：
    /// #waterfall 列表、div.movie 详情、span.header 字段、#avatar-waterfall 演员）。
    /// 作为备胎源排在内置 bus/db/library 之后，仅当它们失败时才会被请求。
    /// </summary>
    public class AvmooCrawler
    {
        private const string DEFAULT_BASE_URL = "https://avmoo.shop";
        private const string CHROME_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        public static string GetWebType()
        {
            return "avmoo";
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
            result["WebType"] = "avmoo";
            result["StatusCode"] = 0;

            // 1. 搜索页：/tw/search/{番号}，#waterfall > div.item 的 date 文本即番号
            string searchUrl = $"{baseUrl}/tw/search/{Uri.EscapeDataString(vid)}";
            logs.Add("search: " + searchUrl);
            var search = await GetHtmlAsync(header, searchUrl, "zh-TW,zh;q=0.9,en;q=0.8");
            result["StatusCode"] = search.statusCode;
            if (search.statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(search.sourceCode)) {
                result["Error"] = string.IsNullOrEmpty(search.error) ? "搜索页请求失败" : search.error;
                logs.Add($"搜索页失败: status={search.statusCode}, error={search.error ?? "(空响应)"}");
                result["Logs"] = logs;
                return result;
            }

            string detailUrl = FindDetailUrl(search.sourceCode, searchUrl, vid, logs);
            if (string.IsNullOrEmpty(detailUrl)) {
                // 响应 200 但列表里没有精确番号：番号可能不存在/下架，或被人机验证拦截
                result["Error"] = "搜索页未找到该番号：番号可能不存在或已下架，也可能是站点被拦截或改版（404）";
                result["StatusCode"] = 404;
                result["Logs"] = logs;
                return result;
            }

            // 2. 详情页（ AvMoo 与 JavBus 同族：div.movie / span.header / #avatar-waterfall / a.bigImage / a.sample-box）
            await Task.Delay(1200); // 防风控间隔
            logs.Add("detail: " + detailUrl);
            var detail = await GetHtmlAsync(header, detailUrl, "zh-TW,zh;q=0.9,en;q=0.8");
            result["StatusCode"] = detail.statusCode;
            result["WebUrl"] = detailUrl;
            if (detail.statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(detail.sourceCode)) {
                result["Error"] = string.IsNullOrEmpty(detail.error) ? "详情页请求失败" : detail.error;
                logs.Add($"详情页失败: status={detail.statusCode}, error={detail.error ?? "(空响应)"}");
                result["Logs"] = logs;
                return result;
            }
            string host = new Uri(detailUrl).GetLeftPart(UriPartial.Authority);
            ParseDetail(detail.sourceCode, host, vid, result, logs);
            if (logs.Count > 0)
                result["Logs"] = logs;
            return result;
        }

        /// <summary>番号归一：仅保留字母数字（AvMoo 的 date 节点文本可能带全角/空白）</summary>
        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return "";
            return new string(code.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        private static string FindDetailUrl(string html, string pageUrl, string vid, List<string> logs)
        {
            string want = NormalizeCode(vid);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            HtmlNodeCollection items = doc.DocumentNode.SelectNodes("//div[@id='waterfall']/div[contains(@class,'item')]");
            logs.Add($"waterfall-items: {(items == null ? 0 : items.Count)}");
            if (items == null)
                return "";
            foreach (HtmlNode item in items) {
                HtmlNode dateNode = item.SelectSingleNode(".//date");
                if (dateNode == null)
                    continue;
                if (NormalizeCode(dateNode.InnerText) != want)
                    continue;
                HtmlNode link = item.SelectSingleNode(".//a[@href]");
                if (link == null)
                    continue;
                string href = link.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href))
                    continue;
                if (!href.StartsWith("http"))
                    href = new Uri(new Uri(pageUrl), href).AbsoluteUri;
                return href;
            }
            return "";
        }

        private static void ParseDetail(string html, string host, string vid, Dictionary<string, object> result, List<string> logs)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 定位包含 div.movie 的 container（防爬页没有该结构）
            HtmlNode container = null;
            HtmlNodeCollection containers = doc.DocumentNode.SelectNodes("//div[contains(@class,'container')]");
            if (containers != null) {
                foreach (HtmlNode node in containers) {
                    if (node.SelectSingleNode(".//div[contains(@class,'movie')]") != null) {
                        container = node;
                        break;
                    }
                }
            }
            logs.Add($"movie-container: {(container == null ? 0 : 1)}");
            if (container == null) {
                result["Error"] = "详情页响应 200 但未解析出影片信息：可能被人机验证/防爬页拦截、番号不存在或站点改版";
                return;
            }

            // 标题：container 内首个 h3，去掉前缀番号
            HtmlNode titleNode = container.SelectSingleNode(".//h3");
            if (titleNode != null) {
                string title = Regex.Replace(titleNode.InnerText ?? "", Regex.Escape(vid), "", RegexOptions.IgnoreCase).Trim();
                if (!string.IsNullOrEmpty(title))
                    result["Title"] = title;
            }

            // 信息字段：div.info 下 p > span.header + 值
            HtmlNode infoNode = container.SelectSingleNode(".//div[contains(@class,'info')]");
            if (infoNode != null) {
                List<string> genres = new List<string>();
                HtmlNodeCollection genreLinks = infoNode.SelectNodes(".//span[@class='genre']/label/a");
                if (genreLinks != null) {
                    foreach (HtmlNode a in genreLinks) {
                        string name = a.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(name))
                            genres.Add(name);
                    }
                }
                if (genres.Count > 0)
                    result["Genre"] = string.Join("\a", genres);

                HtmlNodeCollection paragraphs = infoNode.SelectNodes(".//p");
                if (paragraphs != null) {
                    foreach (HtmlNode p in paragraphs) {
                        HtmlNode headerSpan = p.SelectSingleNode("span[@class='header']");
                        if (headerSpan == null)
                            continue;
                        string label = headerSpan.InnerText?.Trim() ?? "";
                        HtmlNode a = p.SelectSingleNode("a");
                        string value = (a != null ? a.InnerText : p.InnerText)?.Trim();
                        if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(value))
                            continue;
                        if (label.Contains("識別碼") || label.Contains("识别码") || label.Contains("番號") || label.Contains("番号")) {
                            if (!result.ContainsKey("Title"))
                                continue; // 番号行不产出额外字段（VID 已知）
                        } else if (label.Contains("發行日期") || label.Contains("发行日期") || label.Contains("発売日") || label.ToLower().Contains("release date")) {
                            Match m = Regex.Match(value, "[0-9]{4}-[0-9]{2}-[0-9]{2}");
                            if (m.Success && !result.ContainsKey("ReleaseDate"))
                                result["ReleaseDate"] = m.Value;
                        } else if (label.Contains("長度") || label.Contains("长度") || label.Contains("時長") || label.Contains("时长") ||
                            label.ToLower().Contains("duration") || label.ToLower().Contains("runtime")) {
                            Match m = Regex.Match(value, "[0-9]+");
                            if (m.Success && !result.ContainsKey("Duration"))
                                result["Duration"] = m.Value;
                        } else if (label.Contains("系列")) {
                            if (!result.ContainsKey("Series"))
                                result["Series"] = a != null ? a.InnerText?.Trim() : value;
                        } else if (label.Contains("製作商") || label.Contains("制作商") || label.Contains("片商") || label.ToLower().Contains("studio") || label.ToLower().Contains("maker")) {
                            if (!result.ContainsKey("Studio"))
                                result["Studio"] = a != null ? a.InnerText?.Trim() : value;
                        } else if (label.Contains("發行商") || label.Contains("发行商") || label.ToLower().Contains("publisher")) {
                            if (!result.ContainsKey("Publisher"))
                                result["Publisher"] = a != null ? a.InnerText?.Trim() : value;
                        } else if (label.Contains("導演") || label.Contains("导演") || label.ToLower().Contains("director")) {
                            if (!result.ContainsKey("Director"))
                                result["Director"] = a != null ? a.InnerText?.Trim() : value;
                        }
                    }
                }
            }

            // 演员：#avatar-waterfall 下的 a（名字在 img@title / a@title / 文本）
            List<string> actors = new List<string>();
            HtmlNodeCollection actorLinks = doc.DocumentNode.SelectNodes("//div[@id='avatar-waterfall']//a");
            if (actorLinks != null) {
                foreach (HtmlNode a in actorLinks) {
                    string name = GetActorName(a);
                    if (!string.IsNullOrEmpty(name) && !actors.Contains(name))
                        actors.Add(name);
                }
            }
            if (actors.Count > 0)
                result["ActorNames"] = actors;
            logs.Add($"actors: {actors.Count}");

            // 封面：a.bigImage href
            HtmlNodeCollection bigNodes = doc.DocumentNode.SelectNodes("//a[@class='bigImage']");
            if (bigNodes != null && bigNodes.Count > 0) {
                string href = bigNodes[0].GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href)) {
                    if (!href.StartsWith("http"))
                        href = $"{host}{href}";
                    result["BigImageUrl"] = href;
                }
            }

            // 样本图：a.sample-box href
            List<string> extra = new List<string>();
            HtmlNodeCollection sampleNodes = doc.DocumentNode.SelectNodes("//a[@class='sample-box']");
            if (sampleNodes != null) {
                foreach (HtmlNode n in sampleNodes) {
                    string href = n.GetAttributeValue("href", "");
                    if (string.IsNullOrEmpty(href))
                        continue;
                    if (!href.StartsWith("http"))
                        href = $"{host}{href}";
                    extra.Add(href);
                }
                if (extra.Count > 0)
                    result["ExtraImageUrl"] = extra;
            }

            if (!result.ContainsKey("Title") && !result.ContainsKey("Error"))
                result["Error"] = "详情页响应 200 但未解析出影片信息：可能被人机验证/防爬页拦截或站点改版";
        }

        private static string GetActorName(HtmlNode aNode)
        {
            HtmlNode img = aNode.SelectSingleNode(".//img");
            if (img != null) {
                string title = img.GetAttributeValue("title", "");
                if (!string.IsNullOrEmpty(title))
                    return title.Trim();
            }
            string aTitle = aNode.GetAttributeValue("title", "");
            if (!string.IsNullOrEmpty(aTitle))
                return aTitle.Trim();
            return aNode.InnerText?.Trim() ?? "";
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
