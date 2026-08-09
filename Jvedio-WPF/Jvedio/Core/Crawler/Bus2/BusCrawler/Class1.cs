using HtmlAgilityPack;
using SuperUtils.NetWork.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BusCrawler
{
    public class Crawler
    {
        public static string GetWebType()
        {
            return "bus";
        }

        public static string IsPluginAvailable(Dictionary<string, object> dataInfo)
        {
            if (dataInfo == null)
                return "dataInfo is null";
            if (!dataInfo.ContainsKey("VID"))
                return "no VID";
            return "";
        }

        private static string GetStrVal(Dictionary<string, object> dict, string key, string defaultVal = "")
        {
            if (!dict.ContainsKey(key)) return defaultVal;
            object val = dict[key];
            if (val == null) return defaultVal;
            return val.ToString();
        }

        public static async Task<Dictionary<string, object>> GetInfo(RequestHeader header, Dictionary<string, object> dataInfo)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            List<string> logs = new List<string>();
            if (dataInfo == null || !dataInfo.ContainsKey("VID"))
            {
                result["Error"] = "no VID";
                result["StatusCode"] = -1;
                return result;
            }
            string baseUrl = "https://www.busjav.bond/";
            string urlVal = GetStrVal(dataInfo, "Url");
            if (!string.IsNullOrEmpty(urlVal))
                baseUrl = urlVal;
            string vid = GetStrVal(dataInfo, "VID");
            string url = baseUrl;
            if (!url.EndsWith("/")) url += "/";
            url += vid;
            result["WebUrl"] = url;
            result["WebType"] = "bus";
            Dictionary<string, string> requestHeaders = new Dictionary<string, string>();
            requestHeaders["Referer"] = url;
            result["Header"] = requestHeaders;
            string videoType = GetStrVal(dataInfo, "VideoType");
            var (statusCode, sourceCode, errorMsg) = await GetHtmlAsync(header, url);
            result["StatusCode"] = statusCode;
            result["Error"] = "";
            if (statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(sourceCode))
            {
                result["Error"] = string.IsNullOrEmpty(errorMsg) ? "failed" : errorMsg;
                if (logs.Count > 0) result["Logs"] = logs;
                return result;
            }
            string html = sourceCode;
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            string host = new Uri(url).GetLeftPart(UriPartial.Authority);
            List<string> actors = new List<string>();
            List<string> actorIds = new List<string>();
            logs.Add($"html-length: {html.Length}");
            logs.Add($"host: {host}");

            HtmlNodeCollection headerNodes = doc.DocumentNode.SelectNodes("//span[@class='header']");
            logs.Add($"header-span-nodes: {(headerNodes == null ? 0 : headerNodes.Count)}");
            if (headerNodes != null)
            {
                foreach (var h in headerNodes)
                {
                    string text = h.InnerText?.Trim();
                    HtmlNode node = h.ParentNode;
                    if (string.IsNullOrEmpty(text) || node == null) continue;
                    if (text.Contains("發行日期"))
                    {
                        string content = node.InnerText;
                        var m1 = Regex.Match(content, "[0-9]{4}-[0-9]{2}-[0-9]{2}");
                        if (m1.Success) result["ReleaseDate"] = m1.Value;
                        var m2 = Regex.Match(content, "[0-9]{4}");
                        if (m2.Success) result["Year"] = m2.Value;
                    }
                    else if (text.Contains("長度"))
                    {
                        string content = node.InnerText;
                        var m = Regex.Match(content, "[0-9]+");
                        if (m.Success) result["Duration"] = m.Value;
                    }
                    else if (text.Contains("製作商"))
                    {
                        var a = node.SelectSingleNode("a");
                        if (a != null) result["Studio"] = a.InnerText?.Trim();
                    }
                    else if (text.Contains("系列"))
                    {
                        var a = node.SelectSingleNode("a");
                        if (a != null) result["Series"] = a.InnerText?.Trim();
                    }
                    else if (text.Contains("導演"))
                    {
                        var a = node.SelectSingleNode("a");
                        if (a != null) result["Director"] = a.InnerText?.Trim();
                    }
                }
            }

            var starDivLinks = doc.DocumentNode.SelectNodes("//div[@id='star-div']//a[contains(@href,'/star/')]");
            logs.Add($"star-div-links: {(starDivLinks == null ? 0 : starDivLinks.Count)}");
            if (starDivLinks != null)
            {
                foreach (var a in starDivLinks)
                {
                    string name = GetActorName(a);
                    if (!string.IsNullOrEmpty(name) && !actors.Contains(name)) actors.Add(name);
                    string href = a.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(href))
                    {
                        string id = href.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                        if (!string.IsNullOrEmpty(id) && !actorIds.Contains(id)) actorIds.Add(id);
                    }
                }
            }

            if (actors.Count == 0)
            {
                var genreStarLinks = doc.DocumentNode.SelectNodes("//span[contains(@class,'genre')]//a[contains(@href,'/star/')]");
                logs.Add($"genre-star-links: {(genreStarLinks == null ? 0 : genreStarLinks.Count)}");
                if (genreStarLinks != null)
                {
                    foreach (var a in genreStarLinks)
                    {
                        string name = a.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(name) && !actors.Contains(name)) actors.Add(name);
                        string href = a.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href))
                        {
                            string id = href.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                            if (!string.IsNullOrEmpty(id) && !actorIds.Contains(id)) actorIds.Add(id);
                        }
                    }
                }
            }

            if (actors.Count == 0)
            {
                var starNameLinks = doc.DocumentNode.SelectNodes("//div[contains(@class,'star-name')]//a[contains(@href,'/star/')]");
                logs.Add($"star-name-links: {(starNameLinks == null ? 0 : starNameLinks.Count)}");
                if (starNameLinks != null)
                {
                    foreach (var a in starNameLinks)
                    {
                        string name = a.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(name) && !actors.Contains(name)) actors.Add(name);
                        string href = a.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href))
                        {
                            string id = href.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                            if (!string.IsNullOrEmpty(id) && !actorIds.Contains(id)) actorIds.Add(id);
                        }
                    }
                }
            }

            if (actors.Count == 0)
            {
                var allStarLinks = doc.DocumentNode.SelectNodes("//a[contains(@href,'/star/')]");
                logs.Add($"all-star-links: {(allStarLinks == null ? 0 : allStarLinks.Count)}");
                if (allStarLinks != null)
                {
                    foreach (var a in allStarLinks)
                    {
                        string name = a.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(name) && !actors.Contains(name)) actors.Add(name);
                        string href = a.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href))
                        {
                            string id = href.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                            if (!string.IsNullOrEmpty(id) && !actorIds.Contains(id)) actorIds.Add(id);
                        }
                    }
                }
            }

            if (actors.Count == 0)
            {
                var reg = new Regex("href\\s*=\\s*['\"]([^'\"]*?/star/[^'\"\\s>]+)['\"][^>]*>\\s*([^<\\r\\n]+?)\\s*<", RegexOptions.IgnoreCase);
                var ms = reg.Matches(html);
                logs.Add($"regex-star: {ms.Count}");
                foreach (Match m in ms)
                {
                    if (m.Groups.Count >= 3)
                    {
                        string id = m.Groups[1].Value.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                        string name = m.Groups[2].Value.Trim();
                        if (!string.IsNullOrEmpty(name) && !actors.Contains(name)) actors.Add(name);
                        if (!string.IsNullOrEmpty(id) && !actorIds.Contains(id)) actorIds.Add(id);
                    }
                }
            }

            if (actors.Count == 0)
            {
                var infoDiv = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'info')]");
                if (infoDiv != null)
                {
                    var infoStarLinks = infoDiv.SelectNodes(".//a[contains(@href,'/star/')]");
                    logs.Add($"info-div-star-links: {(infoStarLinks == null ? 0 : infoStarLinks.Count)}");
                    if (infoStarLinks != null)
                    {
                        foreach (var a in infoStarLinks)
                        {
                            string name = a.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(name) && !actors.Contains(name)) actors.Add(name);
                            string href = a.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                            {
                                string id = href.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                                if (!string.IsNullOrEmpty(id) && !actorIds.Contains(id)) actorIds.Add(id);
                            }
                        }
                    }
                }
            }

            List<string> genres = new List<string>();
            HtmlNodeCollection genreNodes = doc.DocumentNode.SelectNodes("//span[@class='genre']/label/a");
            if (genreNodes != null)
            {
                foreach (var g in genreNodes) genres.Add(g.InnerText?.Trim());
            }
            result["Genre"] = genres;

            if (actors.Count > 0)
            {
                result["ActorNames"] = actors;
                if (actorIds.Count > 0)
                {
                    List<string> urls = new List<string>();
                    foreach (var id in actorIds)
                    {
                        if (string.IsNullOrEmpty(id)) continue;
                        if (string.Equals(videoType, "Censored", StringComparison.OrdinalIgnoreCase))
                            urls.Add($"{host}/pics/actress/{id}_a.jpg");
                        else if (string.Equals(videoType, "UnCensored", StringComparison.OrdinalIgnoreCase))
                            urls.Add($"{host}/imgs/actress/{id}.jpg");
                        else
                            urls.Add(host.Replace("www", "images") + "/actress/" + id + "_a.jpg");
                    }
                    if (urls.Count > 0) result["ActressImageUrl"] = urls;
                }
            }
            logs.Add($"actors: {actors.Count}, actorIds: {actorIds.Count}");

            HtmlNodeCollection titleNodes = doc.DocumentNode.SelectNodes("//h3");
            if (titleNodes != null && titleNodes.Count > 0)
            {
                string t = titleNodes[0].InnerText ?? "";
                string title = Regex.Replace(t, Regex.Escape(vid), "", RegexOptions.IgnoreCase).Trim();
                result["Title"] = title;
            }

            string bigimageurl = "";
            var bigNodes = doc.DocumentNode.SelectNodes("//a[@class='bigImage']");
            if (bigNodes != null && bigNodes.Count > 0)
            {
                string href = bigNodes[0].GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href))
                {
                    bigimageurl = href.StartsWith("http") ? href : $"{host}{href}";
                    result["BigImageUrl"] = bigimageurl;
                }
            }
            string small = "";
            bool isBus = host.Contains("busjav") || host.Contains("javbus");

            if (!string.IsNullOrEmpty(bigimageurl))
            {
                string movieid = GetMovidID(bigimageurl);
                if (bigimageurl.Contains("pics.dmm.co.jp"))
                {
                    small = bigimageurl.Replace("pl.jpg", "ps.jpg");
                }
                else if (!string.IsNullOrEmpty(movieid))
                {
                    if (bigimageurl.Contains("/pics/cover/") || bigimageurl.Contains("/pics/thumb/"))
                        small = $"{host}/pics/thumb/{movieid}.jpg";
                    else if (bigimageurl.Contains("/imgs/cover/") || bigimageurl.Contains("/imgs/thumbs/"))
                        small = $"{host}/imgs/thumbs/{movieid}.jpg";
                    else if (isBus)
                        small = $"{host}/pics/thumb/{movieid}.jpg";
                    else
                        small = $"{host}/thumb/{movieid}.jpg";
                }

                if (string.IsNullOrEmpty(small) && bigimageurl.EndsWith("_b.jpg", StringComparison.OrdinalIgnoreCase))
                    small = bigimageurl.Replace("_b.jpg", "_s.jpg");
            }

            if (string.IsNullOrEmpty(small) && !string.IsNullOrEmpty(vid) && isBus)
            {
                small = $"{host}/pics/thumb/{vid}.jpg";
            }

            if (!string.IsNullOrEmpty(small)) result["SmallImageUrl"] = small;
            List<string> extra = new List<string>();
            var sampleNodes = doc.DocumentNode.SelectNodes("//a[@class='sample-box']");
            if (sampleNodes != null)
            {
                foreach (var n in sampleNodes)
                {
                    string href = n.GetAttributeValue("href", "");
                    if (string.IsNullOrEmpty(href)) continue;
                    if (!href.StartsWith("http")) href = $"{host}{href}";
                    extra.Add(href);
                }
                if (extra.Count > 0) result["ExtraImageUrl"] = extra;
            }
            if (logs.Count > 0) result["Logs"] = logs;
            return result;
        }

        private static string GetMovidID(string url)
        {
            try
            {
                return url.Split('/').Last().Split('.').First().Replace("_b", "");
            }
            catch
            {
                return null;
            }
        }

        private static string GetActorName(HtmlNode aNode)
        {
            var img = aNode.SelectSingleNode(".//img");
            if (img != null)
            {
                string title = img.GetAttributeValue("title", "");
                if (!string.IsNullOrEmpty(title)) return title.Trim();
            }
            string aTitle = aNode.GetAttributeValue("title", "");
            if (!string.IsNullOrEmpty(aTitle)) return aTitle.Trim();
            return aNode.InnerText?.Trim() ?? "";
        }

        private static async Task<(int statusCode, string sourceCode, string error)> GetHtmlAsync(object header, string url)
        {
            HttpClientHandler handler = new HttpClientHandler();
            try
            {
                if (header != null)
                {
                    var proxyProp = header.GetType().GetProperty("WebProxy", BindingFlags.Public | BindingFlags.Instance);
                    if (proxyProp != null)
                    {
                        var proxy = proxyProp.GetValue(header) as IWebProxy;
                        if (proxy != null)
                        {
                            handler.Proxy = proxy;
                            handler.UseProxy = true;
                        }
                    }
                }
            }
            catch { }

            handler.AllowAutoRedirect = true;
            handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;

            using (var client = new HttpClient(handler, disposeHandler: true))
            {
                try
                {
                    if (header != null)
                    {
                        var timeoutProp = header.GetType().GetProperty("TimeOut", BindingFlags.Public | BindingFlags.Instance);
                        if (timeoutProp != null && timeoutProp.PropertyType == typeof(int))
                        {
                            int ms = (int)timeoutProp.GetValue(header);
                            if (ms > 0) client.Timeout = TimeSpan.FromMilliseconds(ms);
                        }
                        var headersProp = header.GetType().GetProperty("Headers", BindingFlags.Public | BindingFlags.Instance);
                        var headersVal = headersProp?.GetValue(header);
                        if (headersVal is IDictionary<string, string> dict)
                        {
                            foreach (var kv in dict)
                            {
                                try { client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value); } catch { }
                            }
                            bool hasCookie = false;
                            foreach (var kv in dict)
                            {
                                if (kv.Key.Equals("cookie", StringComparison.OrdinalIgnoreCase)) { hasCookie = true; break; }
                            }
                            if (hasCookie)
                            {
                                string existingCookie = dict.Where(kv => kv.Key.Equals("cookie", StringComparison.OrdinalIgnoreCase)).First().Value;
                                if (!existingCookie.Contains("existmag"))
                                {
                                    dict["cookie"] = existingCookie.TrimEnd() + "; existmag=all";
                                }
                            }
                            else
                            {
                                dict.Add("cookie", "existmag=all");
                            }
                            try { client.DefaultRequestHeaders.Remove("cookie"); } catch { }
                            try { client.DefaultRequestHeaders.TryAddWithoutValidation("cookie", dict["cookie"]); } catch { }
                        }
                        else
                        {
                            try { client.DefaultRequestHeaders.TryAddWithoutValidation("cookie", "existmag=all"); } catch { }
                        }
                    }
                    else
                    {
                        try { client.DefaultRequestHeaders.TryAddWithoutValidation("cookie", "existmag=all"); } catch { }
                    }
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");

                    using (var resp = await client.GetAsync(url))
                    {
                        var code = (int)resp.StatusCode;
                        var txt = await resp.Content.ReadAsStringAsync();
                        return (code, txt, null);
                    }
                }
                catch (Exception ex)
                {
                    return (-1, null, ex.Message);
                }
            }
        }
    }
}
