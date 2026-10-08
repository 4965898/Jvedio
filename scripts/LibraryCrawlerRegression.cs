// Exercises the packaged DLL through the application's plugin selector and HTTP client.
// All databases and HTTP responses are isolated fixtures; no user configuration is read.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Jvedio;
using Jvedio.Core.Crawler;
using Jvedio.Core.Enums;
using Jvedio.Core.Global;
using Jvedio.Core.Net;
using Jvedio.Core.Plugins.Crawler;
using Jvedio.Entity;
using SuperControls.Style.Plugin;
using SuperUtils.Framework.ORM.Enums;
using SuperUtils.Framework.Tasks;
using SuperUtils.NetWork.Entity;

internal static class LibraryCrawlerRegression
{
    private sealed class TestApp : App
    {
        protected override void OnStartup(StartupEventArgs e) { }
    }

    private static int checks;
    private static void Check(string name, bool ok)
    {
        if (!ok) throw new Exception("FAIL: " + name);
        checks++;
        Console.WriteLine("PASS: " + name);
    }

    private static PluginInvoker Invoker() => new PluginInvoker(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "plugins", "crawlers", "library", "LibraryCrawler.dll"));

    private static string Available(Dictionary<string, object> data) =>
        (string)Invoker().SetMethod("IsPluginAvailable").Invoke(new object[] { data });

    private static Dictionary<string, object> Data(object type = null, string vid = "OREC-473")
    {
        var data = new Dictionary<string, object> { ["VID"] = vid };
        if (type != null) data["VideoType"] = type;
        return data;
    }

    private static void AvailabilityChecks()
    {
        Check("VID only is eligible", Available(Data()) == "");
        Check("new Video defaults to Normal and is eligible",
            Available(new Video { VID = "OREC-473" }.ToDictionary()) == "");
        Check("known censored movie stays eligible", Available(Data(VideoType.Censored)) == "");
        Check("numeric unknown movie type is eligible", Available(Data(0)) == "");
        Check("numeric censored movie type is eligible", Available(Data(2)) == "");
        Check("null type is treated as unknown", Available(new Dictionary<string, object> {
            ["VID"] = "OREC-473", ["VideoType"] = null }) == "");
        Check("empty type is treated as unknown", Available(Data("")) == "");
        Check("explicit uncensored type is still rejected", Available(Data(VideoType.UnCensored)).Contains("仅可"));
        Check("explicit European type is still rejected", Available(Data(VideoType.Europe)).Contains("仅可"));
        Check("FC2 is still rejected even without a type", Available(Data(null, "fc2-ppv-123456")).Contains("FC2"));
        Check("FC2 is still rejected when marked censored", Available(Data(VideoType.Censored, "FC2-123456")).Contains("FC2"));
        Check("blank VID is rejected", Available(Data(VideoType.Normal, " ")).Contains("VID"));
        Check("null and empty inputs are rejected", Available(null) != "" && Available(new Dictionary<string, object>()) != "");
        Check("invalid type is rejected", Available(Data("bad-type")) != "" && Available(Data(99)) != "");
    }

    private sealed class FixtureSite : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly Task worker;
        public readonly List<string> Requests = new List<string>();
        public readonly string Url;
        public string Mode = "redirect";

        public FixtureSite()
        {
            var port = new TcpListener(IPAddress.Loopback, 0);
            port.Start();
            int number = ((IPEndPoint)port.LocalEndpoint).Port;
            port.Stop();
            Url = "http://127.0.0.1:" + number + "/zh-cn/";
            listener.Prefixes.Add("http://127.0.0.1:" + number + "/");
            listener.Start();
            worker = Task.Run(async () => {
                while (listener.IsListening) {
                    HttpListenerContext context;
                    try { context = await listener.GetContextAsync(); }
                    catch (HttpListenerException) { break; }
                    catch (ObjectDisposedException) { break; }
                    string path = context.Request.RawUrl;
                    lock (Requests) Requests.Add(path);
                    string body = "";
                    if (path.Contains("vl_searchbyid.php")) {
                        if (Mode == "redirect") {
                            context.Response.StatusCode = 302;
                            context.Response.RedirectLocation = "./?v=fixture473";
                        } else if (Mode == "list") {
                            body = "<a href='./?v=other'><div class='id'>OREC-474</div></a>" +
                                "<a href='./?v=fixture473'><div class='id'>OREC-473</div></a>";
                        } else {
                            body = "<p>No matching videos</p>";
                        }
                    } else if (path.EndsWith("?v=fixture473", StringComparison.Ordinal)) {
                        body = "<h3 class='post-title text'><a>OREC-473 Fixture title</a></h3>" +
                            "<div id='video_info'><div><table>" +
                            "<tr><td>发行日期</td><td>2026-10-08</td></tr>" +
                            "<tr><td>长度</td><td><span>120</span></td></tr>" +
                            "<tr><td>演员</td><td><span><span><a>Fixture actor</a></span></span></td></tr>" +
                            "</table></div></div>";
                    } else context.Response.StatusCode = 404;
                    byte[] bytes = Encoding.UTF8.GetBytes(body);
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    context.Response.Close();
                }
            });
        }

        public void Reset(string mode) { Mode = mode; lock (Requests) Requests.Clear(); }
        public string[] Paths() { lock (Requests) return Requests.ToArray(); }
        public void Dispose() { listener.Close(); worker.GetAwaiter().GetResult(); }
    }

    private static void SetPaths(string root)
    {
        Directory.CreateDirectory(root);
        PathManager.CurrentUserFolder = root;
        PathManager.LogPath = Path.Combine(root, "log");
        PathManager.BackupPath = Path.Combine(root, "backup");
        PathManager.PicPath = Path.Combine(root, "pic");
        PathManager.ProjectImagePath = Path.Combine(root, "image", "library");
        PathManager.AllOldDataPath = Path.Combine(root, "olddata");
        PathManager.oldDataPath = Path.Combine(root, "legacy-absent");
        PathManager.InitDirs = new[] { root, PathManager.LogPath, PathManager.BackupPath,
            PathManager.ProjectImagePath, PathManager.AllOldDataPath, PathManager.PicPath };
        File.WriteAllText(Path.Combine(root, "translation.config.json"), "{}");
    }

    private static VideoDownLoader Downloader(Video video, List<string> logs)
    {
        return new VideoDownLoader(video, CancellationToken.None, new TaskLogger(logs)) {
            Header = new RequestHeader { TimeOut = 5000, Headers = new Dictionary<string, string>() }
        };
    }

    private static void PipelineChecks()
    {
        MapperManager.Init();
        ConfigManager.Init(() => { });
        using (var site = new FixtureSite()) {
            CrawlerManager.BaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "crawlers");
            CrawlerManager.Init(true);
            Check("startup applies the staged plugin update", File.Exists(Path.Combine(CrawlerManager.BaseDir,
                "library", "LibraryCrawler.dll")) && !Directory.Exists(Path.Combine(CrawlerManager.BaseDir, "library", "temp")));
            Check("restored DLL is discovered by the plugin manager", CrawlerManager.PluginMetaDatas.Count == 1);
            ConfigManager.ServerConfig.CrawlerServers = new List<CrawlerServer> {
                new CrawlerServer { PluginID = "crawler/library", Url = site.Url, Available = 1, Enabled = true }
            };
            var video = new Video { DataID = 1, VID = "OREC-473" };
            var logs = new List<string>();
            bool headerReceived = false;
            var downloader = Downloader(video, logs);
            var result = downloader.GetInfo(h => headerReceived = h != null).GetAwaiter().GetResult();
            Check("unknown type reaches search then the redirected detail page", site.Paths().SequenceEqual(new[] {
                "/zh-cn/vl_searchbyid.php?keyword=OREC-473", "/zh-cn/?v=fixture473" }));
            Check("metadata is returned to the application", (string)result["Title"] == "FIXTURE TITLE" &&
                (string)result["ReleaseDate"] == "2026-10-08" && (string)result["Duration"] == "120" &&
                ((List<string>)result["ActorNames"]).Single() == "Fixture actor");
            Check("plugin ID, web type and remote code are returned", (string)result["PluginID"] == "crawler/library" &&
                (string)result["WebType"] == "library" && (string)result["DataCode"] == "fixture473");
            Check("header and plugin logs are propagated", headerReceived && logs.Any(l => l.Contains("crawler recv vid: OREC-473")));
            Check("movie type and VID are unchanged", video.VideoType == VideoType.Normal && video.VID == "OREC-473");
            site.Reset("list");
            result = Downloader(video, new List<string>()).GetInfo(null).GetAwaiter().GetResult();
            Check("multiple search results use the exact VID", site.Paths().SequenceEqual(new[] {
                "/zh-cn/vl_searchbyid.php?keyword=OREC-473", "/zh-cn/?v=fixture473" }) &&
                (string)result["Title"] == "FIXTURE TITLE");
            var scraped = result;

            site.Reset("missing");
            result = Downloader(video, new List<string>()).GetInfo(null).GetAwaiter().GetResult();
            Check("missing VID does not return unrelated metadata", !result.ContainsKey("Title") &&
                site.Paths().Length == 1 && Convert.ToInt32(result["Error"]) == 404);

            var task = new DownLoadTask(video);
            Check("download task accepts the scraped metadata", task.CheckDataInfo(video, scraped, downloader, downloader.Header)
                .GetAwaiter().GetResult());
            Check("task validation preserves the unknown movie type", video.VideoType == VideoType.Normal);

            MapperManager.urlCodeMapper.Insert(new UrlCode { ValueType = "video", WebType = "library",
                LocalValue = video.VID, RemoteValue = "fixture473" }, InsertMode.Replace);
            site.Reset("redirect");
            video.VideoType = VideoType.Censored;
            result = Downloader(video, new List<string>()).GetInfo(null).GetAwaiter().GetResult();
            Check("known censored type and cached remote code still work", site.Paths().SequenceEqual(new[] {
                "/zh-cn/?v=fixture473" }) && (string)result["Title"] == "FIXTURE TITLE");
        }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try {
            if (args.Length > 0 && args[0].EndsWith("LibraryCrawlerRegression.exe", StringComparison.OrdinalIgnoreCase))
                args = args.Skip(1).ToArray();
            if (args[1] == "legacy") {
                Check("legacy DLL reproduces the reported Normal-type rejection",
                    Available(Data(VideoType.Normal)) == "该刮削器仅可刮削修正影片");
                return 0;
            }
            var app = new TestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("/SuperControls.Style;component/Lang/zh-CN.xaml", UriKind.Relative) });
            SetPaths(Path.GetFullPath(args[0]));
            PipelineChecks();
            AvailabilityChecks();
            Console.WriteLine("PASS: " + checks + " library crawler checks");
            return 0;
        } catch (Exception ex) {
            Console.WriteLine(ex);
            return 1;
        }
    }
}
