using Jvedio.Core.Config;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jvedio.Core.Crawler
{
    /// <summary>
    /// 在线观看站点的可达性状态（3.16 遗留的「按钮可达性预检」）：
    ///   Ok          = 站点可达
    ///   MaybeBlocked = 站点有响应但疑似 Cloudflare 人机验证 / 风控（需要更新 Cookie 或换镜像）
    ///   Fail         = 连不上（域名失效 / 需要代理）
    /// 结果按站点缓存（默认 10 分钟），探测走「选项-网络」中配置的代理。
    /// </summary>
    public enum OnlineSiteState
    {
        Unknown = 0,
        Ok = 1,
        MaybeBlocked = 2,
        Fail = 3,
    }

    public static class OnlineSiteStatus
    {
        private static readonly TimeSpan CACHE_TTL = TimeSpan.FromMinutes(10);
        private const int PROBE_TIMEOUT_SECONDS = 6;
        private const int MAX_CONCURRENCY = 6;

        private class CacheEntry
        {
            public OnlineSiteState State;
            public DateTime Time;
        }

        private static readonly ConcurrentDictionary<string, CacheEntry> Cache =
            new ConcurrentDictionary<string, CacheEntry>();

        /// <summary>探测并发上限（同时 27 个请求容易触发风控）</summary>
        private static readonly SemaphoreSlim ProbeSemaphore = new SemaphoreSlim(MAX_CONCURRENCY);

        /// <summary>探测进行中的站点，避免重复发起</summary>
        private static readonly HashSet<string> Probing = new HashSet<string>();

        /// <summary>某站点状态变化（探测完成后），UI 据此刷新按钮标记</summary>
        public static event Action<string> OnSiteStateChanged;

        private static readonly HttpClient Client;

        static OnlineSiteStatus()
        {
            HttpClientHandler handler = new HttpClientHandler() {
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            };
            IWebProxy proxy = ConfigManager.ProxyConfig?.GetWebProxy();
            if (proxy != null) {
                handler.UseProxy = true;
                handler.Proxy = proxy;
            }
            Client = new HttpClient(handler) {
                Timeout = TimeSpan.FromSeconds(PROBE_TIMEOUT_SECONDS),
            };
            Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public static OnlineSiteState Get(string siteName)
        {
            if (Cache.TryGetValue(siteName, out CacheEntry entry) &&
                DateTime.Now - entry.Time < CACHE_TTL)
                return entry.State;
            return OnlineSiteState.Unknown;
        }

        public static void Invalidate(string siteName = null)
        {
            if (string.IsNullOrEmpty(siteName))
                Cache.Clear();
            else
                Cache.TryRemove(siteName, out _);
        }

        /// <summary>
        /// 批量探测启用的站点（已有新鲜缓存或正在探测的跳过），立即返回，结果经事件通知
        /// </summary>
        public static void ProbeAll(IEnumerable<OnlineSite> sites)
        {
            if (ConfigManager.OnlineConfig == null || !ConfigManager.OnlineConfig.ReachabilityPrecheck)
                return;
            foreach (OnlineSite site in sites) {
                if (site == null || !site.Enabled)
                    continue;
                if (Cache.TryGetValue(site.Name, out CacheEntry entry) &&
                    DateTime.Now - entry.Time < CACHE_TTL)
                    continue;
                ProbeAsync(site);
            }
        }

        private static async void ProbeAsync(OnlineSite site)
        {
            lock (Probing) {
                if (Probing.Contains(site.Name))
                    return;
                Probing.Add(site.Name);
            }
            try {
                await ProbeSemaphore.WaitAsync();
                try {
                    OnlineSiteState state = await ProbeOnce(site);
                    Cache[site.Name] = new CacheEntry() { State = state, Time = DateTime.Now };
                    OnSiteStateChanged?.Invoke(site.Name);
                } finally {
                    ProbeSemaphore.Release();
                }
            } catch (Exception) {
                Cache[site.Name] = new CacheEntry() { State = OnlineSiteState.Fail, Time = DateTime.Now };
                OnSiteStateChanged?.Invoke(site.Name);
            } finally {
                lock (Probing)
                    Probing.Remove(site.Name);
            }
        }

        private static Task<OnlineSiteState> ProbeOnce(OnlineSite site)
        {
            return ProbeUrl(site.BaseUrl);
        }

        /// <summary>
        /// 直接探测一个地址（不走缓存）：刮削源体检等场景共用
        /// </summary>
        public static async Task<OnlineSiteState> ProbeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return OnlineSiteState.Fail;
            try {
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(PROBE_TIMEOUT_SECONDS)))
                using (HttpResponseMessage response = await Client.GetAsync(url, cts.Token)) {
                    int code = (int)response.StatusCode;
                    if (code == 200) {
                        string title = await GetTitleAsync(response);
                        if (CrawlerHeader.IsCloudflareChallengeTitle(title))
                            return OnlineSiteState.MaybeBlocked;
                        return OnlineSiteState.Ok;
                    }
                    // 403/503/429 多为 Cloudflare / 风控，其余状态码视为不可用
                    if (code == 403 || code == 503 || code == 429)
                        return OnlineSiteState.MaybeBlocked;
                    return OnlineSiteState.Fail;
                }
            } catch (Exception) {
                return OnlineSiteState.Fail;
            }
        }

        private static async Task<string> GetTitleAsync(HttpResponseMessage response)
        {
            try {
                string html = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(html))
                    return string.Empty;
                int start = html.IndexOf("<title", StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                    return string.Empty;
                start = html.IndexOf('>', start);
                int end = html.IndexOf("</title", StringComparison.OrdinalIgnoreCase);
                if (start < 0 || end < 0 || end <= start)
                    return string.Empty;
                return html.Substring(start + 1, end - start - 1);
            } catch (Exception) {
                return string.Empty;
            }
        }
    }
}
