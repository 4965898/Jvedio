using System;
using System.Collections.Generic;
using Jvedio.Core.Config;

namespace Jvedio.Core.Crawler
{
    /// <summary>
    /// 在线观看跳转站点项（参考浏览器脚本「JAV 添加跳转在线观看」）
    /// 根地址（域名）影片页与搜索页共用，路径各自独立：
    ///   影片页 = 根地址 + MoviePath（用番号）
    ///   搜索页 = 根地址 + SearchPath（用演员名/关键词）
    /// </summary>
    public class OnlineSite
    {
        public string Name { get; set; }

        /// <summary>
        /// 根地址（域名），影片页与搜索页共用；用户在「选项-网络」中可覆盖（如换镜像站）
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// 影片页路径模板（含 {{code}}），如 {{code}} 或 search?q={{code}}
        /// </summary>
        public string MoviePath { get; set; }

        /// <summary>
        /// 搜索页路径模板（含 {{code}}），如 search/{{code}}
        /// </summary>
        public string SearchPath { get; set; }

        public Func<string, string> CodeFormatter { get; set; }

        /// <summary>
        /// 用户在「选项-网络」中自定义的根地址；空 = 使用内置 BaseUrl
        /// </summary>
        public string UrlOverride {
            get {
                if (ConfigManager.OnlineConfig?.UrlOverrides != null &&
                    ConfigManager.OnlineConfig.UrlOverrides.TryGetValue(Name, out string v))
                    return v;
                return string.Empty;
            }

            set {
                if (ConfigManager.OnlineConfig?.UrlOverrides == null)
                    return;
                if (string.IsNullOrEmpty(value))
                    ConfigManager.OnlineConfig.UrlOverrides.Remove(Name);
                else
                    ConfigManager.OnlineConfig.UrlOverrides[Name] = value;
            }
        }

        /// <summary>
        /// 生效的根地址：优先用户自定义；兼容旧配置（旧格式是完整网址模板，只取协议+域名部分）
        /// </summary>
        private string GetBaseUrl()
        {
            if (string.IsNullOrEmpty(UrlOverride))
                return BaseUrl;
            string v = UrlOverride.Replace("{{code}}", "x");
            if (Uri.TryCreate(v, UriKind.Absolute, out Uri uri))
                return uri.GetLeftPart(UriPartial.Authority);
            return UrlOverride;
        }

        /// <summary>
        /// 影片页跳转地址（用番号）
        /// </summary>
        public string GetMovieUrl(string code)
        {
            string c = CodeFormatter?.Invoke(code) ?? code;
            return Combine(GetBaseUrl(), MoviePath).Replace("{{code}}", c);
        }

        /// <summary>
        /// 搜索页跳转地址（用演员名/关键词）
        /// </summary>
        public string GetSearchUrl(string code)
        {
            string c = CodeFormatter?.Invoke(code) ?? code;
            return Combine(GetBaseUrl(), SearchPath).Replace("{{code}}", c);
        }

        private static string Combine(string baseUrl, string path)
        {
            if (string.IsNullOrEmpty(path))
                return baseUrl;
            if (path.StartsWith("http://") || path.StartsWith("https://"))
                return path;
            return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        }
    }

    /// <summary>
    /// 在线观看跳转站点列表：右键菜单「在线观看」、影片详情页「在线观看」按钮组、演员详情页「在线搜索」按钮组共用
    /// </summary>
    public static class OnlineSites
    {
        /// <summary>
        /// FANZA 番号格式化：AB-1234 → AB01234（数字补足 5 位）；START 开头 → 1startxxxxx
        /// </summary>
        public static string FanzaFormat(string code)
        {
            if (string.IsNullOrEmpty(code))
                return code;
            int idx = code.IndexOf('-');
            if (idx <= 0 || idx >= code.Length - 1)
                return code;
            string pre = code.Substring(0, idx);
            string num = code.Substring(idx + 1);
            string padNum = num.PadLeft(5, '0');
            if (pre.ToLower().StartsWith("start"))
                return "1" + pre.ToLower() + padNum;
            return pre + padNum;
        }

        /// <summary>
        /// JavBus 番号格式化：MIUM 前缀需要补 300 前缀（站点特殊收录规则）
        /// </summary>
        public static string JavBusFormat(string code)
        {
            if (string.IsNullOrEmpty(code))
                return code;
            if (code.StartsWith("MIUM", StringComparison.OrdinalIgnoreCase))
                return "300" + code;
            return code;
        }

        public static List<OnlineSite> Sites { get; } = new List<OnlineSite>() {
            new OnlineSite() {
                Name = "JavDB",
                BaseUrl = "https://javdb.com",
                MoviePath = "search?q={{code}}",
                SearchPath = "search?q={{code}}",
            },
            new OnlineSite() {
                Name = "JavBus",
                BaseUrl = "https://javbus.com",
                MoviePath = "{{code}}",
                SearchPath = "search/{{code}}",
                CodeFormatter = JavBusFormat,
            },
            new OnlineSite() {
                Name = "JAVLib",
                BaseUrl = "https://www.javlibrary.com",
                MoviePath = "cn/vl_searchbyid.php?keyword={{code}}",
                SearchPath = "cn/searchstar.php?keyword={{code}}",
            },
            new OnlineSite() {
                Name = "MISSAV",
                BaseUrl = "https://missav.ws",
                MoviePath = "{{code}}/",
                SearchPath = "search/{{code}}",
            },
            new OnlineSite() {
                Name = "FANZA 動画",
                BaseUrl = "https://www.dmm.co.jp",
                MoviePath = "digital/videoa/-/detail/=/cid={{code}}/",
                SearchPath = "digital/videoa/-/search/=/searchstr={{code}}/",
                CodeFormatter = FanzaFormat,
            },
            new OnlineSite() {
                Name = "Jable",
                BaseUrl = "https://jable.tv",
                MoviePath = "videos/{{code}}/",
                SearchPath = "search/{{code}}/",
            },
            new OnlineSite() {
                Name = "123av",
                BaseUrl = "https://123av.com",
                MoviePath = "zh/search?keyword={{code}}",
                SearchPath = "cn/search?keyword={{code}}",
            },
            new OnlineSite() {
                Name = "Supjav",
                BaseUrl = "https://supjav.com",
                MoviePath = "zh/?s={{code}}",
                SearchPath = "zh/?s={{code}}",
            },
            new OnlineSite() {
                Name = "NETFLAV",
                BaseUrl = "https://netflav5.com",
                MoviePath = "search?type=title&keyword={{code}}",
                SearchPath = "search?type=title&keyword={{code}}",
            },
            new OnlineSite() {
                Name = "Avgle",
                BaseUrl = "https://avgle.com",
                MoviePath = "search/videos?search_query={{code}}&search_type=videos",
                SearchPath = "search/videos?search_query={{code}}&search_type=videos",
            },
            new OnlineSite() {
                Name = "JAVHHH",
                BaseUrl = "https://javhhh.com",
                MoviePath = "v/?wd={{code}}",
                SearchPath = "v/?wd={{code}}",
            },
            new OnlineSite() {
                Name = "BestJP",
                BaseUrl = "https://www3.bestjavporn.com",
                MoviePath = "search/{{code}}",
                SearchPath = "search/{{code}}",
            },
            new OnlineSite() {
                Name = "JAVMENU",
                BaseUrl = "https://javmenu.com",
                MoviePath = "{{code}}",
                SearchPath = "zh/search?wd={{code}}",
            },
            new OnlineSite() {
                Name = "Jav.Guru",
                BaseUrl = "https://jav.guru",
                MoviePath = "?s={{code}}",
                SearchPath = "jav-actress-list/?taxonomy_search={{code}}",
            },
            new OnlineSite() {
                Name = "JAVMOST",
                BaseUrl = "https://javmost.cx",
                MoviePath = "search/{{code}}/",
                SearchPath = "search/{{code}}/",
            },
            new OnlineSite() {
                Name = "HAYAV",
                BaseUrl = "https://hayav.com",
                MoviePath = "video/{{code}}/",
                SearchPath = "search/{{code}}/",
            },
            new OnlineSite() {
                Name = "AvJoy",
                BaseUrl = "https://avjoy.me",
                MoviePath = "search/videos/{{code}}",
                SearchPath = "search/videos/{{code}}",
            },
            new OnlineSite() {
                Name = "JAVFC2",
                BaseUrl = "https://javfc2.net",
                MoviePath = "?s={{code}}",
                SearchPath = "?s={{code}}",
            },
            new OnlineSite() {
                Name = "baihuse",
                BaseUrl = "https://paipancon.com",
                MoviePath = "search/{{code}}",
                SearchPath = "search/{{code}}",
            },
            new OnlineSite() {
                Name = "GGJAV",
                BaseUrl = "https://ggjav.com",
                MoviePath = "main/search?string={{code}}",
                SearchPath = "main/search?string={{code}}",
            },
            new OnlineSite() {
                Name = "AV01",
                BaseUrl = "https://www.av01.tv",
                MoviePath = "search/videos?search_query={{code}}",
                SearchPath = "search/videos?search_query={{code}}",
            },
            new OnlineSite() {
                Name = "18sex",
                BaseUrl = "https://www.18sex.org",
                MoviePath = "cn/search/{{code}}/",
                SearchPath = "cn/search/{{code}}/",
            },
            new OnlineSite() {
                Name = "highporn",
                BaseUrl = "https://highporn.net",
                MoviePath = "search/videos?search_query={{code}}",
                SearchPath = "search/videos?search_query={{code}}",
            },
            new OnlineSite() {
                Name = "evojav",
                BaseUrl = "https://evojav.pro",
                MoviePath = "video/{{code}}/",
                SearchPath = "search/{{code}}/",
            },
            new OnlineSite() {
                Name = "18av",
                BaseUrl = "https://18av.mm-cg.com",
                MoviePath = "zh/fc_search/all/{{code}}/1.html",
                SearchPath = "zh/fc_search/all/{{code}}/1.html",
            },
            new OnlineSite() {
                Name = "javgo",
                BaseUrl = "https://javgo.to",
                MoviePath = "zh/v/{{code}}",
                SearchPath = "zh/search/{{code}}",
            },
            new OnlineSite() {
                Name = "javhub",
                BaseUrl = "https://javhub.net",
                MoviePath = "search/{{code}}",
                SearchPath = "search/{{code}}",
            },
        };
    }
}