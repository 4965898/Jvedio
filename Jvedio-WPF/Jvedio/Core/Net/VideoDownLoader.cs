using Jvedio.Core.Crawler;
using Jvedio.Core.Enums;
using Jvedio.Core.Exceptions;
using Jvedio.Core.Plugins.Crawler;
using Jvedio.Entity;
using SuperControls.Style;
using SuperControls.Style.Plugin;
using SuperUtils.Common;
using SuperUtils.Framework.ORM.Wrapper;
using SuperUtils.Framework.Tasks;
using SuperUtils.NetWork;
using SuperUtils.NetWork.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jvedio.Core.Net
{
    public class VideoDownLoader
    {

        #region "属性"
        public DownLoadState State { get; set; } = DownLoadState.DownLoading;

        private bool Canceled { get; set; }

        private CancellationToken cancellationToken { get; set; }

        public Video CurrentVideo { get; set; }

        public RequestHeader Header { get; set; }

        public List<CrawlerServer> CrawlerServers { get; set; } // 该资源支持的爬虫刮削器

        private TaskLogger TaskLogger { get; set; }

        #endregion

        public VideoDownLoader(Video video, CancellationToken token, TaskLogger Logger)
        {
            CurrentVideo = video;
            cancellationToken = token;
            // CrawlerHeader.Default 是静态共享实例：并发任务直接共用其 Headers 字典时，
            // 「一个任务在遍历、另一个任务在写入」会抛出「集合已修改；可能无法执行枚举操作」。
            // 每个任务独享一份拷贝，彻底消除跨任务竞态（6.0.0.1）。
            Header = new RequestHeader();
            Header.WebProxy = ConfigManager.ProxyConfig.GetWebProxy();
            Header.TimeOut = ConfigManager.ProxyConfig.HttpTimeout * 1000;
            Header.Headers = CrawlerHeader.Default?.Headers != null
                ? new Dictionary<string, string>(CrawlerHeader.Default.Headers)
                : new Dictionary<string, string>();
            TaskLogger = Logger;
        }

        /// <summary>
        /// 取消下载
        /// </summary>
        public void Cancel()
        {
            Canceled = true;
            State = DownLoadState.Fail;
        }


        /// <summary>
        /// 判断爬虫结果是否可用：有标题，或爬虫没有报告任何错误。
        /// 不可用的结果不再直接返回，而是记录原因后尝试下一个源。
        /// </summary>
        private static bool IsUsableResult(Dictionary<string, object> d)
        {
            if (d == null) return false;
            string error = d.TryGetValue("Error", out object errObj) ? errObj?.ToString() : "";
            bool errorEmpty = string.IsNullOrEmpty(error) || error.Equals(HttpResult.DEFAULT_ERROR_MSG);
            string title = d.TryGetValue("Title", out object titleObj) ? titleObj?.ToString() : "";
            return !string.IsNullOrEmpty(title) || errorEmpty;
        }

        /// <summary>把单个源的失败整理成「哪个源 · 哪一环 · 怎么办」的一句话。</summary>
        private static string DescribeFailure(Dictionary<string, object> d, CrawlerServer server)
        {
            string error = d != null && d.TryGetValue("Error", out object errObj) ? errObj?.ToString() : "";
            string reason = DownLoadTask.NormalizeCrawlerError(error);
            if (string.IsNullOrEmpty(reason)) {
                string statusCode = d != null && d.TryGetValue("StatusCode", out object statusObj) ? statusObj?.ToString() : "";
                reason = int.TryParse(statusCode, out int code) ? DownLoadTask.StatusCodeToMessage(code) : "未知原因";
                if (reason == DownLoadTask.StatusCodeToMessage(200))
                    reason = "页面响应 200 但未解析出影片信息";
            }
            string host = server?.Url;
            try { if (!string.IsNullOrEmpty(host)) host = new Uri(host).Host; } catch { }
            return string.IsNullOrEmpty(host) ? $"{server?.PluginID}：{reason}" : $"{server?.PluginID}（{host}）：{reason}";
        }

        /// <summary>
        /// 日志只是诊断信息，绝不能因为日志集合的并发竞争
        /// （如另一任务/UI 正在枚举同一日志列表）把整个刮削任务打断。
        /// </summary>
        private void LogSafely(Action logAction)
        {
            try { logAction(); } catch { }
        }

        public async Task<Dictionary<string, object>> GetInfo(Action<RequestHeader> headerCallBack)
        {
            // 下载信息
            State = DownLoadState.DownLoading;
            Dictionary<string, object> dataInfo = CurrentVideo.ToDictionary();
            // 获得所有可用服务器源
            Dictionary<PluginMetaData, List<CrawlerServer>> crawlers = GetCrawlerServer(dataInfo);

            List<string> failures = new List<string>();
            Dictionary<string, object> lastFailure = null;
            foreach (var key in crawlers.Keys) {
                List<CrawlerServer> crawlerServers = crawlers[key];
                foreach (CrawlerServer server in crawlerServers) {
                    if (server.Invoker == null) {
                        LogSafely(() => TaskLogger.Warning($"刮削器[{server.PluginID}] invoker 为空"));
                        failures.Add($"{server.PluginID}：插件未正确加载");
                        continue;
                    }
                    server.AttachToDict(dataInfo);
                    // Header 传递代理配置进去
                    object o = await server.Invoker.SetMethod("GetInfo").InvokeAsync(new object[] { Header, dataInfo });
                    if (!(o is Dictionary<string, object> d)) {
                        LogSafely(() => TaskLogger.Warning($"刮削器[{server.PluginID}] invoker 失败"));
                        failures.Add($"{server.PluginID}：插件调用失败");
                        continue;
                    }

                    if (d.ContainsKey("Logs") && d["Logs"] is List<string> logs) {
                        LogSafely(() => {
                            foreach (var item in logs) {
                                TaskLogger.Info($"[{server.PluginID}] {item}");
                            }
                        });
                    }

                    if (!IsUsableResult(d)) {
                        // 该源失败：写清原因，继续尝试下一个源
                        string reason = DescribeFailure(d, server);
                        failures.Add(reason);
                        LogSafely(() => TaskLogger.Warning($"刮削失败（{reason}），尝试下一个源"));
                        lastFailure = d;
                        continue;
                    }

                    // 成功一个立即返回
                    if (d.ContainsKey("Header") && d["Header"] is Dictionary<string, string> dict) {
                        // 合并爬虫回传的请求头（如 JavBus 图片防盗链的 Referer），
                        // 不再覆盖用户在服务器列表里保存的 Headers
                        var merged = Header.Headers != null
                            ? new Dictionary<string, string>(Header.Headers)
                            : new Dictionary<string, string>();
                        foreach (var kv in dict)
                            merged[kv.Key] = kv.Value;
                        Header.Headers = merged;
                    }
                    headerCallBack?.Invoke(Header);
                    d["PluginID"] = server.PluginID;
                    if (!string.IsNullOrEmpty(server.Url))
                        d["ServerUrl"] = server.Url;
                    return d;
                }
            }

            // 所有源都失败：保留最后一个源的原始 Error（上层与回归工具依赖它），
            // 多源失败时把每个源的原因另存为 ErrorAll 汇总，供任务提示展示
            if (lastFailure == null) {
                lastFailure = new Dictionary<string, object>();
                lastFailure["Error"] = "没有可用的刮削源，请检查「选项 → 同步信息」的服务器列表";
            }
            if (failures.Count > 1)
                lastFailure["ErrorAll"] = "全部 " + failures.Count + " 个源均失败：" + string.Join("；", failures);
            return lastFailure;
        }

        public Dictionary<PluginMetaData, List<CrawlerServer>> GetCrawlerServer(Dictionary<string, object> dataInfo)
        {
            // 获取信息类型，并设置爬虫类型
            if (ConfigManager.ServerConfig.CrawlerServers == null ||
                CrawlerManager.PluginMetaDatas == null ||
                ConfigManager.ServerConfig.CrawlerServers.Count == 0 ||
                CrawlerManager.PluginMetaDatas?.Count == 0)
                throw new CrawlerNotFoundException();

            List<PluginMetaData> pluginMetaDatas =
                CrawlerManager.PluginMetaDatas.Where(arg => arg.Enabled).ToList();

            if (pluginMetaDatas.Count == 0)
                throw new CrawlerNotFoundException();

            Dictionary<PluginMetaData, List<CrawlerServer>> result =
                new Dictionary<PluginMetaData, List<CrawlerServer>>();
            for (int i = 0; i < pluginMetaDatas.Count; i++) {
                // 一组支持刮削的网址列表
                PluginMetaData metaData = pluginMetaDatas[i];
                List<CrawlerServer> crawlerServers = ConfigManager.ServerConfig.CrawlerServers
                    .Where(arg => arg.Enabled && !string.IsNullOrEmpty(arg.PluginID) &&
                    arg.PluginID.ToLower().Equals(metaData.PluginID.ToLower())
                    && arg.Available == 1 && !string.IsNullOrEmpty(arg.Url)).ToList();

                if (crawlerServers == null || crawlerServers.Count == 0)
                    continue;

                // 过滤器仅可使用的刮削器
                try {
                    PluginInvoker invoker = new PluginInvoker(metaData.GetDllPath());
                    //Logger.Info($"threadID: {Thread.CurrentThread.ManagedThreadId}, vid: {dataInfo["VID"]}");
                    object reasonObject = invoker.SetMethod("IsPluginAvailable").Invoke(new object[] { dataInfo });
                    string reason = reasonObject as string;
                    if (string.IsNullOrEmpty(reason)) {
                        object webTypeObject = invoker.SetMethod("GetWebType").Invoke(null);
                        string webType = "";
                        if (webTypeObject != null)
                            webType = webTypeObject.ToString();
                        string urlCodeString = string.Empty;

                        // 索引是 ValueType, WebType, LocalValue
                        IWrapper<UrlCode> wrapper = new SelectWrapper<UrlCode>().Eq("ValueType", "video");
                        if (!string.IsNullOrEmpty(webType))
                            wrapper.Eq("WebType", webType);
                        wrapper.Eq("LocalValue", CurrentVideo.VID);
                        UrlCode urlCode = null;
                        for (int attempt = 0; attempt < 3 && urlCode == null; attempt++) {
                            try {
                                urlCode = MapperManager.urlCodeMapper.SelectOne(wrapper);
                            } catch (InvalidOperationException) when (attempt < 2) {
                                // SelectWrapper 的 whereConditions 是静态字典：并发首查时
                                // 「一个线程在枚举、另一个在写入」会瞬态抛出集合异常，重试即可恢复
                            }
                        }
                        if (urlCode == null) {
                            // 三次都失败（或确实抛错）：把真实的异常留给上层堆栈日志
                            urlCode = MapperManager.urlCodeMapper.SelectOne(wrapper);
                        }
                        foreach (var item in crawlerServers) {
                            item.UrlCode = urlCode;
                        }
                        foreach (var item in crawlerServers) {
                            item.Invoker = invoker;
                        }
                        result[metaData] = crawlerServers;
                    } else {
                        TaskLogger.Error($"刮削失败，原因：{reason}");
                    }
                } catch (Exception ex) {
                    MessageCard.Error($"插件 {metaData.PluginName} 发生了异常：" + ex.Message);
                    continue;
                }
            }

            if (result.Keys.Count == 0)
                throw new CrawlerNotFoundException();

            // todo 爬虫调度器
            return result;
        }

        /// <summary>
        /// 规范化图片 URL：部分爬虫插件返回的链接自带 "http:" 前缀，
        /// 形成 "http:https://..." 双写（hitchao/Jvedio#421/#371），直接下载必然失败。
        /// 取首个 "http(s)://" 位置截断清洗。
        /// </summary>
        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;
            url = url.Trim();
            int idx = url.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                idx = url.IndexOf("http://", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                url = url.Substring(idx);
            return url;
        }

        public async Task<byte[]> DownloadImage(string url, RequestHeader header, Action<string> onError = null)
        {
            try {
                url = NormalizeUrl(url);
                HttpClientHandler handler = new HttpClientHandler();
                handler.AllowAutoRedirect = true;
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                if (header != null && header.WebProxy != null) {
                    handler.Proxy = header.WebProxy;
                    handler.UseProxy = true;
                }

                using (var client = new System.Net.Http.HttpClient(handler, disposeHandler: true)) {
                    int timeoutMs = 8000;
                    if (header != null && header.TimeOut > 0)
                        timeoutMs = (int)header.TimeOut;
                    client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);

                    if (header != null && header.Headers != null) {
                        foreach (var kv in header.Headers) {
                            try { client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value); } catch { }
                        }
                    }

                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                    return await client.GetByteArrayAsync(url);
                }
            } catch (WebException ex) {
                onError?.Invoke(ex.Message);
            } catch (Exception ex) {
                onError?.Invoke(ex.Message);
            }

            return null;
        }

        public async Task<bool> DownloadPreviewVideo(string url, string target, RequestHeader header, Action<string> onError)
        {
            if (File.Exists(target)) return true;
            string partial = target + "." + Guid.NewGuid().ToString("N") + ".part";
            try {
                var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
                if (header?.WebProxy != null) { handler.Proxy = header.WebProxy; handler.UseProxy = true; }
                using (var client = new System.Net.Http.HttpClient(handler))
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
                    deadline.CancelAfter(TimeSpan.FromMinutes(2));
                    client.Timeout = TimeSpan.FromMinutes(2);
                    if (header?.Headers != null) foreach (var pair in header.Headers) client.DefaultRequestHeaders.TryAddWithoutValidation(pair.Key, pair.Value);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
                    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token)) {
                        response.EnsureSuccessStatusCode();
                        string mime = response.Content.Headers.ContentType?.MediaType ?? "";
                        if (!mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase) && mime != "application/octet-stream")
                            throw new IOException(LangManager.GetValueByKey("ClipperInvalidVideo"));
                        const long limit = 250L * 1024 * 1024;
                        if (response.Content.Headers.ContentLength > limit) throw new IOException(LangManager.GetValueByKey("ClipperVideoTooLarge"));
                        using (var input = await response.Content.ReadAsStreamAsync())
                        using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true)) {
                            byte[] buffer = new byte[65536]; long total = 0; int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, deadline.Token)) > 0) {
                                total += count;
                                if (total > limit) throw new IOException(LangManager.GetValueByKey("ClipperVideoTooLarge"));
                                await output.WriteAsync(buffer, 0, count, deadline.Token);
                            }
                            if (total == 0) throw new IOException(LangManager.GetValueByKey("ClipperInvalidVideo"));
                        }
                    }
                }
                if (!File.Exists(target)) File.Move(partial, target);
                return true;
            } catch (Exception ex) { onError?.Invoke(ex.Message); return false; }
            finally { if (File.Exists(partial)) File.Delete(partial); }
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void Pause()
        {
            throw new NotImplementedException();
        }
    }
}
