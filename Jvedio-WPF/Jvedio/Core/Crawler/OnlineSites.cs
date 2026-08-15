using System;
using System.Collections.Generic;
using Jvedio.Core.Config;

namespace Jvedio.Core.Crawler
{
    /// <summary>
    /// 在线观看跳转站点项（参考浏览器脚本「JAV 添加跳转在线观看」）
    /// </summary>
    public class OnlineSite
    {
        public string Name { get; set; }

        public string UrlTemplate { get; set; }

        public Func<string, string> CodeFormatter { get; set; }

        /// <summary>
        /// 用户在「选项-网络」中自定义的网址模板（含 {{code}}）；空 = 使用内置默认
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

        public string GetUrl(string code)
        {
            string c = CodeFormatter?.Invoke(code) ?? code;
            string template = UrlTemplate;
            if (!string.IsNullOrEmpty(UrlOverride))
                template = UrlOverride;
            return template.Replace("{{code}}", c);
        }
    }

    /// <summary>
    /// 在线观看跳转站点列表：右键菜单「在线观看」与详情页「在线观看」按钮组共用
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
                Name = "FANZA 動画",
                UrlTemplate = "https://www.dmm.co.jp/digital/videoa/-/detail/=/cid={{code}}/",
                CodeFormatter = FanzaFormat,
            },
            new OnlineSite() {
                Name = "Jable",
                UrlTemplate = "https://jable.tv/videos/{{code}}/",
            },
            new OnlineSite() {
                Name = "MISSAV",
                UrlTemplate = "https://missav.ws/{{code}}/",
            },
            new OnlineSite() {
                Name = "123av",
                UrlTemplate = "https://123av.com/zh/search?keyword={{code}}",
            },
            new OnlineSite() {
                Name = "Supjav",
                UrlTemplate = "https://supjav.com/zh/?s={{code}}",
            },
            new OnlineSite() {
                Name = "NETFLAV",
                UrlTemplate = "https://netflav5.com/search?type=title&keyword={{code}}",
            },
            new OnlineSite() {
                Name = "Avgle",
                UrlTemplate = "https://avgle.com/search/videos?search_query={{code}}&search_type=videos",
            },
            new OnlineSite() {
                Name = "JAVHHH",
                UrlTemplate = "https://javhhh.com/v/?wd={{code}}",
            },
            new OnlineSite() {
                Name = "BestJP",
                UrlTemplate = "https://www3.bestjavporn.com/search/{{code}}",
            },
            new OnlineSite() {
                Name = "JAVMENU",
                UrlTemplate = "https://javmenu.com/{{code}}",
            },
            new OnlineSite() {
                Name = "Jav.Guru",
                UrlTemplate = "https://jav.guru/?s={{code}}",
            },
            new OnlineSite() {
                Name = "JAVMOST",
                UrlTemplate = "https://javmost.cx/search/{{code}}/",
            },
            new OnlineSite() {
                Name = "HAYAV",
                UrlTemplate = "https://hayav.com/video/{{code}}/",
            },
            new OnlineSite() {
                Name = "AvJoy",
                UrlTemplate = "https://avjoy.me/search/videos/{{code}}",
            },
            new OnlineSite() {
                Name = "JAVFC2",
                UrlTemplate = "https://javfc2.net/?s={{code}}",
            },
            new OnlineSite() {
                Name = "baihuse",
                UrlTemplate = "https://paipancon.com/search/{{code}}",
            },
            new OnlineSite() {
                Name = "GGJAV",
                UrlTemplate = "https://ggjav.com/main/search?string={{code}}",
            },
            new OnlineSite() {
                Name = "AV01",
                UrlTemplate = "https://www.av01.tv/search/videos?search_query={{code}}",
            },
            new OnlineSite() {
                Name = "18sex",
                UrlTemplate = "https://www.18sex.org/cn/search/{{code}}/",
            },
            new OnlineSite() {
                Name = "highporn",
                UrlTemplate = "https://highporn.net/search/videos?search_query={{code}}",
            },
            new OnlineSite() {
                Name = "evojav",
                UrlTemplate = "https://evojav.pro/video/{{code}}/",
            },
            new OnlineSite() {
                Name = "18av",
                UrlTemplate = "https://18av.mm-cg.com/zh/fc_search/all/{{code}}/1.html",
            },
            new OnlineSite() {
                Name = "javgo",
                UrlTemplate = "https://javgo.to/zh/v/{{code}}",
            },
            new OnlineSite() {
                Name = "javhub",
                UrlTemplate = "https://javhub.net/search/{{code}}",
            },
            new OnlineSite() {
                Name = "JavBus",
                UrlTemplate = "https://javbus.com/{{code}}",
                CodeFormatter = JavBusFormat,
            },
            new OnlineSite() {
                Name = "JavDB",
                UrlTemplate = "https://javdb.com/search?q={{code}}",
            },
            new OnlineSite() {
                Name = "JAVLib",
                UrlTemplate = "https://www.javlibrary.com/cn/vl_searchbyid.php?keyword={{code}}",
            },
        };
    }
}
