using HtmlAgilityPack;
using SuperUtils.NetWork.Entity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Jvedio.Crawler
{
    /// <summary>
    /// JavDatabase 演员资料爬虫：按番号定位——GET /movies/{番号}/ → p.mb-1 区块中
    /// Idol(s)/Actress(es) 标签下的唯一演员链接 → /idols/{slug}/ 资料页解析
    /// （DOB/Height/Measurements(B-W-H)/Cup/JP 日文名/h1 罗马字名）。
    /// 仅供「演员资料自动补齐」任务通过 GetActorInfo 调用；
    /// IsPluginAvailable 恒返回原因字符串，保证影片刮削链自动跳过本插件。
    /// </summary>
    public class JavdatabaseCrawler
    {
        private const string DEFAULT_BASE_URL = "https://www.javdatabase.com";
        private const string CHROME_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        public static string GetWebType()
        {
            return "javdatabase";
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

        public static async Task<Dictionary<string, object>> GetActorInfo(RequestHeader header, Dictionary<string, object> dataInfo)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            List<string> logs = new List<string>();
            result["WebType"] = "javdatabase";
            string baseUrl = GetStrVal(dataInfo, "Url", DEFAULT_BASE_URL).TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = DEFAULT_BASE_URL;
            // 候选番号：Vids(全部关联影片,最多10个) 优先,退回单个 Vid;404 自动换下一个
            List<string> candidates = new List<string>();
            if (dataInfo.ContainsKey("Vids") && dataInfo["Vids"] is System.Collections.IEnumerable list)
                foreach (object item in list)
                    if (item?.ToString()?.Trim() is string s && s != "" && !candidates.Contains(s))
                        candidates.Add(s);
            string single = GetStrVal(dataInfo, "Vid").Trim();
            if (single != "" && !candidates.Contains(single))
                candidates.Insert(0, single);
            if (candidates.Count == 0) {
                result["Error"] = "缺少关联影片番号（Vid）：该演员没有可定位的影片";
                result["Logs"] = logs;
                return result;
            }

            // 1. 逐个番号尝试影片页 → 演员区块:唯一直接用,多人合演收集候选按名字消歧
            string actressUrl = "";
            List<string> ambiguous = new List<string>();
            string actorName = GetStrVal(dataInfo, "ActorName");
            string actorNameEN = GetStrVal(dataInfo, "ActorNameEN");
            foreach (string vid in candidates) {
                string movieUrl = $"{baseUrl}/movies/{vid.ToLowerInvariant()}/";
                logs.Add("movie: " + movieUrl);
                var movie = await GetHtmlAsync(header, movieUrl);
                if (movie.statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(movie.sourceCode)) {
                    logs.Add($"影片页失败({vid}): status={movie.statusCode}, error={movie.error ?? "(空响应)"}");
                    await Task.Delay(400);
                    continue;
                }
                List<string> links = FindActressLinks(movie.sourceCode, logs);
                if (links.Count == 1) {
                    actressUrl = links[0];
                    break;
                }
                if (links.Count > 1) {
                    // 多人合演：收集候选演员页,稍后按演员名(日文/罗马字)消歧
                    logs.Add($"影片页({vid})多位演员({links.Count}),收集候选按名字消歧");
                    foreach (string link in links)
                        if (!ambiguous.Contains(link) && ambiguous.Count < 3)
                            ambiguous.Add(link);
                }
                await Task.Delay(400);
            }
            if (actressUrl == "" && ambiguous.Count > 0) {
                // 逐个抓候选演员页,与演员名(日文名优先,罗马字兜底)匹配消歧
                foreach (string candidate in ambiguous) {
                    await Task.Delay(400);
                    logs.Add("disambiguate: " + candidate);
                    var page = await GetHtmlAsync(header, candidate);
                    if (page.statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(page.sourceCode))
                        continue;
                    var names = ParseActressNames(page.sourceCode);
                    if (NameMatches(actorName, actorNameEN, names.Item1, names.Item2)) {
                        actressUrl = candidate;
                        logs.Add($"名字匹配命中: roman={names.Item1} jp={names.Item2}");
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(actressUrl)) {
                result["Error"] = ambiguous.Count > 0
                    ? $"多人合演候选中无名字匹配者（{actorName}）：该演员在站内可能无资料页"
                    : $"尝试了 {candidates.Count} 个关联番号均未找到演员链接（番号站内缺失或无演员区块）";
                result["Logs"] = logs;
                return result;
            }

            await Task.Delay(500); // 两次请求防风控间隔
            logs.Add("actress: " + actressUrl);
            var actress = await GetHtmlAsync(header, actressUrl);
            if (actress.statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(actress.sourceCode)) {
                result["Error"] = DescribeFailure(actress.statusCode, actress.error, "演员页");
                logs.Add($"演员页失败: status={actress.statusCode}, error={actress.error ?? "(空响应)"}");
                result["Logs"] = logs;
                return result;
            }

            ParseActress(actress.sourceCode, actressUrl, result, logs);
            result["Logs"] = logs;
            return result;
        }

        /// <summary>p.mb-1 区块：b 标签含 actress/idol 的，返回区块内全部演员链接（0=无区块,1=唯一,>1=多人合演）</summary>
        private static List<string> FindActressLinks(string html, List<string> logs)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            HtmlNodeCollection paragraphs = doc.DocumentNode.SelectNodes("//p[contains(@class,'mb-1')]");
            if (paragraphs == null)
                return new List<string>();
            foreach (HtmlNode p in paragraphs) {
                HtmlNode b = p.SelectSingleNode("./b");
                if (b == null)
                    continue;
                string label = b.InnerText?.Trim() ?? "";
                if (label.IndexOf("actress", StringComparison.OrdinalIgnoreCase) < 0 &&
                    label.IndexOf("idol", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                List<string> links = (p.SelectNodes(".//a[@href]") ?? new HtmlNodeCollection(p))
                    .Select(a => a.GetAttributeValue("href", ""))
                    .Where(h => !string.IsNullOrEmpty(h))
                    .Distinct().ToList();
                logs.Add($"区块[{label}] 链接数={links.Count}");
                return links;
            }
            return new List<string>();
        }

        /// <summary>提取演员页的名字:(罗马字, 日文名)</summary>
        private static Tuple<string, string> ParseActressNames(string html)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            HtmlNode h1 = doc.DocumentNode.SelectSingleNode("//h1[contains(@class,'idol-name')]");
            string roman = CleanName(h1?.InnerText);
            string japanese = "";
            HtmlNodeCollection bolds = doc.DocumentNode.SelectNodes("//b");
            if (bolds != null)
                foreach (HtmlNode b in bolds) {
                    if ((b.InnerText?.Trim() ?? "") != "JP:")
                        continue;
                    string value = NextValue(b);
                    if (!string.IsNullOrEmpty(value)) {
                        japanese = value;
                        break;
                    }
                }
            return Tuple.Create(roman, japanese);
        }

        /// <summary>名字消歧判定：日文名精确/包含匹配优先,罗马字兜底（归一化后比较）</summary>
        private static bool NameMatches(string actorName, string actorNameEN, string roman, string japanese)
        {
            string wantJp = NormalizeName(actorName);
            string wantEn = NormalizeName(actorNameEN);
            string gotJp = NormalizeName(japanese);
            string gotEn = NormalizeName(roman);
            if (wantJp != "" && gotJp != "" && (gotJp == wantJp || (wantJp.Length >= 2 && gotJp.Contains(wantJp))))
                return true;
            if (wantEn != "" && gotEn != "" && (gotEn == wantEn || (wantEn.Length >= 4 && gotEn.Contains(wantEn))))
                return true;
            return false;
        }

        /// <summary>NFKC 归一 + 小写 + 去全部空白（名字比较用）</summary>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            return System.Text.RegularExpressions.Regex
                .Replace(name.Normalize(System.Text.NormalizationForm.FormKC), "\\s+", "")
                .ToLowerInvariant();
        }

        /// <summary>演员页解析：h1.idol-name(罗马字)、b 标签资料(DOB/Height/Measurements/Cup/JP/Shoe Size)</summary>
        private static void ParseActress(string html, string actressUrl, Dictionary<string, object> result, List<string> logs)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 罗马字名：h1.idol-name 去掉 "- JAV Profile" 后缀
            HtmlNode h1 = doc.DocumentNode.SelectSingleNode("//h1[contains(@class,'idol-name')]");
            string roman = CleanName(h1?.InnerText);
            if (!string.IsNullOrEmpty(roman)) {
                result["ActorNameEN"] = roman;
                logs.Add("roman: " + roman);
            }

            // 资料标签：b 标签文本即标签名，值 = b 后的 a.idol-box-link 文本或直接文本
            // 包容性原则:站点有什么就纳入什么("?" 占位跳过);生日缺失月份/日时只写已知部分
            string dob = "", height = "", measurements = "", cup = "", shoeSize = "", jpName = "", birthplace = "", blood = "";
            HtmlNodeCollection bolds = doc.DocumentNode.SelectNodes("//b");
            if (bolds != null) {
                foreach (HtmlNode b in bolds) {
                    string label = b.InnerText?.Trim() ?? "";
                    if (label.EndsWith(":"))
                        label = label.Substring(0, label.Length - 1).Trim();
                    string value = NextValue(b);
                    if (string.IsNullOrEmpty(value) || value == "?")
                        continue;
                    if (label == "DOB") dob = value;
                    else if (label == "Height") height = value;
                    else if (label == "Measurements") measurements = value;
                    else if (label == "Cup") cup = value;
                    else if (label == "Shoe Size") shoeSize = value;
                    else if (label == "JP") jpName = value;
                    else if (label == "Birthplace" && !result.ContainsKey("BirthPlace")) result["BirthPlace"] = value;
                    else if (label == "Blood" && !result.ContainsKey("BloodType"))
                        result["BloodType"] = value.Replace("578b", "").Trim();
                    else if (label == "Age" && !result.ContainsKey("Age")) result["Age"] = value;
                }
            }
            logs.Add($"fields: DOB={dob} Height={height} M={measurements} Cup={cup} Shoe={shoeSize} JP={jpName} BP={birthplace} Blood={blood}");

            // 生日包容性写入:完整 yyyy-MM-dd → 原样;缺月/日 → 只写已知部分(2005 / 2005-03)
            if (!string.IsNullOrEmpty(dob)) {
                string d = dob.Trim();
                if (DateTime.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) {
                    if (!result.ContainsKey("Birthday"))
                        result["Birthday"] = d;
                } else {
                    Match ym = Regex.Match(d, "^(\\d{4})-(\\d{2})(?!\\d)");
                    if (ym.Success && !ym.Groups[2].Value.Contains("?"))
                        result["Birthday"] = ym.Groups[1].Value + "-" + ym.Groups[2].Value;
                    else {
                        Match y = Regex.Match(d, "\\d{4}");
                        if (y.Success && !result.ContainsKey("Birthday"))
                            result["Birthday"] = y.Value;
                    }
                }
            }

            if (!string.IsNullOrEmpty(height)) {
                Match m = Regex.Match(height, "(\\d{2,3})");
                if (m.Success && !result.ContainsKey("Height"))
                    result["Height"] = m.Groups[1].Value;
            }

            // Measurements: B-W-H（如 92-55-80）
            Match mm = Regex.Match(measurements ?? "", "(\\d{2,3})\\s*-\\s*(\\d{2,3})\\s*-\\s*(\\d{2,3})");
            if (mm.Success) {
                result["Chest"] = mm.Groups[1].Value;
                result["Waist"] = mm.Groups[2].Value;
                result["Hipline"] = mm.Groups[3].Value;
            }

            if (!string.IsNullOrEmpty(cup) && !result.ContainsKey("Cup")) {
                Match c = Regex.Match(cup, "[A-Za-z]");
                if (c.Success)
                    result["Cup"] = c.Value.ToUpperInvariant();
            }

            // Shoe Size：javdatabase 以 cm 记录（? 表示未知）；数值转国内码（cm×2−10）
            Match shoe = Regex.Match(shoeSize ?? "", "(\\d{2}(?:\\.\\d)?)");
            if (shoe.Success && double.TryParse(shoe.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double footCm) &&
                footCm > 15 && footCm < 35)
                result["ShoeSize"] = Math.Round(footCm * 2 - 10).ToString();

            result["WebUrl"] = actressUrl;

            // 头像：idolimages 目录下的演员照片（full 优先，退回 thumb）；
            // 实测演员页首个 /idolimages/ 图片即本人头像（如 idolimages/full/saki-okuda.webp）
            HtmlNode idolImg = doc.DocumentNode.SelectSingleNode("//img[contains(@src,'/idolimages/full/')]")
                ?? doc.DocumentNode.SelectSingleNode("//img[contains(@src,'/idolimages/')]");
            string imageUrl = idolImg?.GetAttributeValue("src", "")?.Trim() ?? "";
            if (!string.IsNullOrEmpty(imageUrl)) {
                result["ImageUrl"] = imageUrl;
                logs.Add("image: " + imageUrl);
            }
        }

        private static string CleanName(string text)
        {
            text = (text ?? "").Trim();
            if (string.IsNullOrEmpty(text))
                return "";
            int idx = text.IndexOf("- JAV Profile", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                text = text.Substring(0, idx).Trim();
            return text;
        }

        /// <summary>b 标签的值：优先其后紧邻的 a.idol-box-link 文本，否则取 b 的后续文本节点</summary>
        private static string NextValue(HtmlNode b)
        {
            // 紧邻下一个元素是 a.idol-box-link 则取其文本(DOB/Height/Cup),否则取紧随文本(Measurements/JP/Shoe Size)
            HtmlNode nextEl = b.SelectSingleNode("following-sibling::*[1]");
            if (nextEl != null && nextEl.Name == "a" &&
                (nextEl.GetAttributeValue("class", "") ?? "").Contains("idol-box-link"))
                return nextEl.InnerText?.Trim() ?? "";
            HtmlNode text = b.SelectSingleNode("following-sibling::text()[1]");
            if (text == null)
                return "";
            string value = text.InnerText?.Trim(' ', '\t', '-', '\r', '\n') ?? "";
            if (value.StartsWith("- "))
                value = value.Substring(2).Trim();
            return value;
        }

        private static string DescribeFailure(int statusCode, string rawError, string stage)
        {
            if (!string.IsNullOrEmpty(rawError))
                return rawError;
            if (statusCode == 404)
                return $"{stage}返回 404：番号不存在或站内无该影片";
            return $"{stage}请求失败（HTTP {statusCode}）";
        }

        private static async Task<(int statusCode, string sourceCode, string error)> GetHtmlAsync(RequestHeader header, string url)
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
                    // 用户在源 Headers 里自带 User-Agent 时必须以其为准：TryAddWithoutValidation 是追加不是覆盖
                    bool hasUserUa = false;
                    if (header?.Headers != null)
                        foreach (var k in header.Headers.Keys)
                            if (k.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)) { hasUserUa = true; break; }
                    if (!hasUserUa)
                        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", CHROME_UA);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
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
