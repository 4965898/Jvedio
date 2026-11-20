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
    /// 站点开启 Wordfence 限速（2026-11-19 实测：404 过多触发 "Exceeded the maximum number of
    /// page not found errors per minute" → 临时 503 拦截，会自动恢复），故番号候选发行日期新→旧、
    /// 连续 404 提前止损；检测到拦截页只中止当前演员（不做进程级冷却——实测限速短暂、批次内
    /// 其他演员仍可正常爬取），全部失败后按名字站内搜索兜底（同样消歧）。
    /// 仅供「演员资料自动补齐」任务通过 GetActorInfo 调用；
    /// IsPluginAvailable 恒返回原因字符串，保证影片刮削链自动跳过本插件。
    /// </summary>
    public class JavdatabaseCrawler
    {
        private const string DEFAULT_BASE_URL = "https://www.javdatabase.com";
        private const string CHROME_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        /// <summary>连续 404 达到该数即止损换演员：站内大概率缺这批影片，继续只会给 Wordfence 攒 404 计数</summary>
        private const int MAX_CONSECUTIVE_404 = 5;

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

            // 候选番号：Vids(全部关联影片,发行日期新→旧) 优先,退回单个 Vid;清洗去重,404 自动换下一个
            List<string> candidates = new List<string>();
            if (dataInfo.ContainsKey("Vids") && dataInfo["Vids"] is System.Collections.IEnumerable list)
                foreach (object item in list)
                    if (item?.ToString()?.Trim() is string s && s != "") {
                        string clean = NormalizeVid(s);
                        if (clean != "" && !candidates.Contains(clean))
                            candidates.Add(clean);
                    }
            string single = GetStrVal(dataInfo, "Vid").Trim();
            if (single != "") {
                string cleanSingle = NormalizeVid(single);
                if (cleanSingle != "" && !candidates.Contains(cleanSingle))
                    candidates.Insert(0, cleanSingle);
            }
            if (candidates.Count == 0) {
                result["Error"] = "缺少关联影片番号（Vid）：该演员没有可定位的影片";
                result["Logs"] = logs;
                return result;
            }

            // 1. 逐个番号尝试影片页 → 演员区块:唯一直接用,多人合演/无区块降级收集候选按名字消歧
            string actressUrl = "";
            List<string> ambiguous = new List<string>();
            string actorName = GetStrVal(dataInfo, "ActorName");
            string actorNameEN = GetStrVal(dataInfo, "ActorNameEN");
            int tried = 0, notFound = 0, requestFailed = 0, noActressBlock = 0, consecutive404 = 0;
            bool siteBlocked = false;
            foreach (string vid in candidates) {
                string movieUrl = $"{baseUrl}/movies/{vid.ToLowerInvariant()}/";
                logs.Add("movie: " + movieUrl);
                var movie = await GetHtmlAsync(header, movieUrl);
                tried++;
                if (movie.statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(movie.sourceCode)) {
                    string blockMsg = DetectSiteBlock(movie.statusCode, movie.sourceCode);
                    if (blockMsg != null) {
                        // Wordfence 临时拦截（404 过多触发，会自动恢复）：当前演员立即止损
                        //（继续只会全线 503），不做进程级冷却——批次内其他演员随时可能恢复
                        logs.Add(blockMsg);
                        siteBlocked = true;
                        break;
                    }
                    if (movie.statusCode == 404) {
                        notFound++;
                        consecutive404++;
                        logs.Add($"影片页站内缺失({vid}): 404");
                        if (consecutive404 >= MAX_CONSECUTIVE_404) {
                            logs.Add($"连续 {MAX_CONSECUTIVE_404} 个番号 404，止损不再尝试剩余候选（避免触发站点 404 限速）");
                            break;
                        }
                    } else {
                        requestFailed++;
                        consecutive404 = 0;
                        logs.Add($"影片页失败({vid}): status={movie.statusCode}, error={movie.error ?? "(空响应)"}");
                    }
                    await Task.Delay(400);
                    continue;
                }
                consecutive404 = 0;
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
                } else {
                    // 200 但无演员区块：区块标签变体/改版场景，降级全页搜 idol 链接（仍按名字消歧防误配）
                    noActressBlock++;
                    List<string> pageLinks = FindIdolLinksOnPage(movie.sourceCode, baseUrl);
                    if (pageLinks.Count > 0)
                        logs.Add($"影片页({vid})无演员区块，降级全页搜索 idol 链接 {pageLinks.Count} 个按名字消歧");
                    else
                        logs.Add($"影片页({vid})无演员区块且全页无 idol 链接");
                    foreach (string link in pageLinks)
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
                    string blockMsg = DetectSiteBlock(page.statusCode, page.sourceCode);
                    if (blockMsg != null) {
                        logs.Add(blockMsg);
                        siteBlocked = true;
                        break;
                    }
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
            if (actressUrl == "" && !siteBlocked) {
                // 2. 全部番号失败 → 按名字站内搜索兜底（结果页收集 /idols/ 候选,同样精确消歧防误配）
                Tuple<string, bool> searchResult = await TrySearchActressUrl(header, baseUrl, actorName, actorNameEN, logs);
                actressUrl = searchResult.Item1;
                siteBlocked = searchResult.Item2;
            }
            if (string.IsNullOrEmpty(actressUrl)) {
                if (siteBlocked) {
                    result["Error"] = "javdatabase 站点临时限速（Wordfence：404 过多触发，通常几分钟内自动恢复）：本演员已中止本次尝试，同批其他演员可能仍可正常爬取；若频繁出现请降低批量频率或更换代理出口";
                } else {
                    string summary = $"尝试了 {tried} 个关联番号未找到演员链接：站内缺失(404)={notFound}、请求失败={requestFailed}、无演员区块={noActressBlock}";
                    if (requestFailed > 0)
                        summary += "（请求失败可能被拦截，请在源配置检查 Cookie/代理）";
                    if (ambiguous.Count > 0)
                        summary = $"多人合演候选中无名字匹配者（{actorName}）。" + summary;
                    result["Error"] = summary;
                }
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

        /// <summary>Wordfence 临时拦截检测：503 + 封禁页特征文本（拦截会自动恢复，只中止当前演员不做进程冷却）</summary>
        private static string DetectSiteBlock(int statusCode, string sourceCode)
        {
            if (statusCode == (int)HttpStatusCode.ServiceUnavailable && !string.IsNullOrEmpty(sourceCode) &&
                sourceCode.IndexOf("access to this site has been limited", StringComparison.OrdinalIgnoreCase) >= 0)
                return "站点触发 Wordfence 临时限速（HTTP 503，多为 404 过多累积触发）：中止当前演员，稍后自动恢复";
            return null;
        }

        /// <summary>番号清洗（与宿主 NormalizeVid 同语义）：NFKC、连字符统一、空白/下划线转连字符、去多分片后缀、FC2 归一</summary>
        private static string NormalizeVid(string raw)
        {
            try {
                string code = (raw ?? "").Normalize(System.Text.NormalizationForm.FormKC).Trim().ToUpperInvariant();
                if (code == "")
                    return "";
                code = Regex.Replace(code, "[‐‑‒–—−]", "-");
                code = Regex.Replace(code, "[\\s_]+", "-");
                code = Regex.Replace(code, "-{2,}", "-").Trim('-');
                code = Regex.Replace(code, "(?i)-?(?:CD|C|PART|DISC)\\d+$", "");
                Match fc2 = Regex.Match(code, "^FC2-?(?:PPV-?)?(\\d{5,10})$");
                if (fc2.Success)
                    return "FC2-PPV-" + fc2.Groups[1].Value;
                return code;
            } catch {
                return (raw ?? "").Trim();
            }
        }

        /// <summary>影片页无演员区块时的降级：全文档搜 /idols/ 链接（排除目录页/带查询串），去重后 ≤3 个供名字消歧</summary>
        private static List<string> FindIdolLinksOnPage(string html, string baseUrl)
        {
            try {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);
                List<string> links = (doc.DocumentNode.SelectNodes("//a[contains(@href,'/idols/')]") ?? new HtmlNodeCollection(doc.DocumentNode))
                    .Select(a => a.GetAttributeValue("href", "").Trim())
                    .Where(h => !string.IsNullOrEmpty(h) && !h.EndsWith("/idols/", StringComparison.OrdinalIgnoreCase) && !h.Contains("?"))
                    .Select(h => h.StartsWith("http") ? h : baseUrl + (h.StartsWith("/") ? "" : "/") + h)
                    .Distinct().Take(3).ToList();
                return links;
            } catch {
                return new List<string>();
            }
        }

        /// <summary>
        /// 按名字站内搜索兜底（全部关联番号都失败时）：GET /?s={名字}（WordPress/ElasticPress 搜索），
        /// 从结果页收集 /idols/ 候选（≤3）逐个抓页精确消歧；日文名优先、罗马字兜底，
        /// 返回 (演员页 url 或空, 搜索期间是否被站点临时拦截)；搜索词无效时静默返回空
        /// </summary>
        private static async Task<Tuple<string, bool>> TrySearchActressUrl(RequestHeader header, string baseUrl, string actorName, string actorNameEN, List<string> logs)
        {
            bool searchBlocked = false;
            foreach (string name in new[] { actorName, actorNameEN }) {
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                await Task.Delay(500);
                string searchUrl = $"{baseUrl}/?s={Uri.EscapeDataString(name.Trim())}";
                logs.Add("search: " + searchUrl);
                var page = await GetHtmlAsync(header, searchUrl);
                string blockMsg = DetectSiteBlock(page.statusCode, page.sourceCode);
                if (blockMsg != null) {
                    logs.Add(blockMsg);
                    return Tuple.Create("", true);
                }
                if (page.statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(page.sourceCode)) {
                    logs.Add($"搜索页失败: status={page.statusCode}");
                    continue;
                }
                List<string> candidates = FindIdolLinksOnPage(page.sourceCode, baseUrl);
                if (candidates.Count == 0) {
                    logs.Add($"搜索结果({name})中无 idol 链接");
                    continue;
                }
                foreach (string candidate in candidates) {
                    await Task.Delay(500);
                    logs.Add("search-disambiguate: " + candidate);
                    var idol = await GetHtmlAsync(header, candidate);
                    string idolBlock = DetectSiteBlock(idol.statusCode, idol.sourceCode);
                    if (idolBlock != null) {
                        logs.Add(idolBlock);
                        return Tuple.Create("", true);
                    }
                    if (idol.statusCode != (int)HttpStatusCode.OK || string.IsNullOrEmpty(idol.sourceCode))
                        continue;
                    var names = ParseActressNames(idol.sourceCode);
                    if (NameMatches(actorName, actorNameEN, names.Item1, names.Item2)) {
                        logs.Add($"搜索兜底命中: roman={names.Item1} jp={names.Item2}");
                        return Tuple.Create(candidate, false);
                    }
                }
            }
            return Tuple.Create("", searchBlocked);
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
