using SuperUtils.NetWork.Entity;
using System;
using System.Collections.Generic;

namespace Jvedio.Core.Crawler
{
    public static class CrawlerHeader
    {
        private static Dictionary<string, string> DEFAULT_HEADERS { get; set; } =
            new Dictionary<string, string>() {
                {
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36"
                },
            };

        public static RequestHeader GitHub { get; set; }

        public static RequestHeader Default { get; set; }

        public static System.Net.IWebProxy WebProxy { get; set; }

        static CrawlerHeader()
        {
            Init();
        }

        public static void Init()
        {
            WebProxy = ConfigManager.ProxyConfig.GetWebProxy();
            Default = new SuperUtils.NetWork.Crawler.CrawlerHeader(WebProxy).Default;
            GitHub = Default;
        }

        /// <summary>
        /// 判断网页标题是否为 Cloudflare 人机验证挑战页（"Just a moment..."）。
        /// 纯 HTTP 客户端无法执行浏览器 JS，必须由用户先在浏览器完成验证后，
        /// 把新鲜的 cf_clearance Cookie 与一致的 User-Agent 填入刮削器请求头。
        /// </summary>
        public static bool IsCloudflareChallengeTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return false;
            return title.IndexOf("just a moment", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("attention required", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("verify you are human", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
