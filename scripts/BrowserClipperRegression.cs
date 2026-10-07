// Uses the actual application schema and receiver in an isolated directory.
using Jvedio;
using Jvedio.Core.Clipper;
using Jvedio.Core.Global;
using Jvedio.Core.DataBase;
using Jvedio.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class BrowserClipperRegression
{
    private sealed class TestApp : App { protected override void OnStartup(StartupEventArgs e) { } }
    private static int failures;
    private static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS: " : "FAIL: ") + name); if (!ok) failures++; }
    private static SQLiteConnection Open()
    {
        var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = SqlManager.DEFAULT_SQLITE_PATH, DefaultTimeout = 5 }.ConnectionString);
        connection.Open(); return connection;
    }
    private static object Scalar(string sql)
    {
        using (var connection = Open()) using (var command = new SQLiteCommand(sql, connection)) return command.ExecuteScalar();
    }
    private static void Sql(string sql)
    {
        using (var connection = Open()) using (var command = new SQLiteCommand(sql, connection)) command.ExecuteNonQuery();
    }
    private static BrowserClipRequest Read(string fixtures, string name, long library = 1)
    {
        var request = JsonConvert.DeserializeObject<BrowserClipRequest>(File.ReadAllText(Path.Combine(fixtures, name + ".json")));
        request.LibraryId = library; return request;
    }
    private static void Rejected(string name, BrowserClipRequest request)
    {
        long before = Convert.ToInt64(Scalar("select count(*) from metadata"));
        bool rejected = false;
        try { BrowserClipperService.Import(request); } catch (ArgumentException) { rejected = true; }
        Check(name, rejected && before == Convert.ToInt64(Scalar("select count(*) from metadata")));
    }
    private static Tuple<int, JObject> Send(BrowserClipperService service, string method, string route, object body = null, string token = null, string origin = null)
    {
        var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + service.Port + route);
        request.Method = method; request.Timeout = 10000; request.Proxy = null;
        if (token != null) request.Headers["X-Jvedio-Token"] = token;
        if (origin != null) request.Headers["Origin"] = origin;
        if (body != null) {
            request.ContentType = "application/json";
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));
            request.ContentLength = bytes.Length;
            using (var stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
        }
        HttpWebResponse response;
        try { response = (HttpWebResponse)request.GetResponse(); }
        catch (WebException ex) { response = (HttpWebResponse)ex.Response; }
        using (response) using (var reader = new StreamReader(response.GetResponseStream()))
            return Tuple.Create((int)response.StatusCode, JObject.Parse(reader.ReadToEnd()));
    }
    private static void Preview(FrameworkElement content, string path)
    {
        foreach (var box in LogicalTree(content).OfType<SuperControls.Style.SearchBox>())
            if (System.Text.RegularExpressions.Regex.IsMatch(box.Text, "^[a-f0-9]{64}$")) box.Text = new string('*', 64);
        foreach (var box in LogicalTree(content).OfType<TextBox>())
            if (System.Text.RegularExpressions.Regex.IsMatch(box.Text, "^[a-f0-9]{64}$")) box.Text = new string('*', 64);
        var border = new Border { Width = 690, Height = 650, Child = content };
        border.SetResourceReference(Border.BackgroundProperty, "Window.Background");
        border.Measure(new Size(690, 650)); border.Arrange(new Rect(0, 0, 690, 650)); border.UpdateLayout();
        var bitmap = new RenderTargetBitmap(690, 650, 96, 96, PixelFormats.Pbgra32); bitmap.Render(border);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
        border.Child = null;
    }
    private static void PreviewScanAndImport(Window_Settings settings, string path)
    {
        var tab = (TabItem)settings.FindName("ScanImportTab");
        foreach (var box in LogicalTree(tab).OfType<SuperControls.Style.SearchBox>())
            if (System.Text.RegularExpressions.Regex.IsMatch(box.Text ?? "", "^[a-f0-9]{64}$")) box.Text = new string('*', 64);
        var content = (FrameworkElement)tab.Content;
        ((ScrollViewer)content).SetResourceReference(Control.BackgroundProperty, "Window.Background");
        content.Measure(new Size(960, 1050)); content.Arrange(new Rect(0, 0, 960, 1050)); content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(960, 1050, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
    [STAThread]
    public static int Main(string[] args)
    {
        try {
            var app = new TestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string resource in new[] {
                "/SuperControls.Style;component/Lang/zh-CN.xaml", "/Jvedio;component/Core/Lang/zh-CN.xaml",
                "/SuperControls.Style;component/XAML/Skin/DefaultColor.xaml", "/SuperControls.Style;component/Themes/SuperControls.xaml",
                "/Jvedio;component/CustomStyle/SuperControls/Basic.xaml", "/Jvedio;component/CustomStyle/SuperControls/Pagination.xaml",
                "/Jvedio;component/CustomStyle/SuperControls/PathRadioButton.xaml"
            }) app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(resource, UriKind.Relative) });
            app.Resources["GlobalFontSize"] = 14d;
            foreach (int size in new[] { 7, 8, 10, 12, 13, 15, 16, 18, 20, 24, 25 }) app.Resources["GlobalFontSize" + size] = (double)size;
            string user = Path.GetFullPath(args[0]), fixtures = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(user);
            PathManager.CurrentUserFolder = user;
            PathManager.LogPath = Path.Combine(user, "log"); PathManager.PicPath = Path.Combine(user, "pic");
            PathManager.BackupPath = Path.Combine(user, "backup"); PathManager.ProjectImagePath = Path.Combine(user, "image");
            PathManager.AllOldDataPath = Path.Combine(user, "old"); PathManager.oldDataPath = Path.Combine(user, "absent");
            PathManager.InitDirs = new[] { user, PathManager.LogPath, PathManager.PicPath, PathManager.BackupPath, PathManager.ProjectImagePath, PathManager.AllOldDataPath };
            File.WriteAllText(Path.Combine(user, "translation.config.json"), "{}");
            SqlManager.Init(); MapperManager.Init(); ConfigManager.Init(() => { });
            ConfigManager.Main.CurrentDBId = 1;
            Sql("insert into app_databases(DBId,Name,DataType) values(1,'剪藏测试库',0),(2,'另一影片库',0),(3,'图片库',1)");
            foreach (var pair in new[] { Tuple.Create("JavBus-list",30), Tuple.Create("JavDB-list",40), Tuple.Create("JAVLibrary-list",20) }) {
                var request = Read(fixtures, pair.Item1);
                var first = BrowserClipperService.Import(request);
                var repeat = BrowserClipperService.Import(request);
                Check(pair.Item1 + " imports or enriches all extracted identifiers and repeated saves add nothing", first.Added + first.Updated + first.Skipped == pair.Item2 && repeat.Added == 0 && repeat.Skipped == pair.Item2);
            }
            var other = BrowserClipperService.Import(Read(fixtures, "JavBus-list", 2));
            Check("deduplication is scoped to the chosen library", other.Added == 30);
            var bus = BrowserClipperService.Import(Read(fixtures, "JavBus-detail"));
            var detailId = Convert.ToInt64(Scalar("select m.DataID from metadata m join metadata_video v on v.DataID=m.DataID where m.DBId=1 and v.VID='GVG-107'"));
            Check("detail import saves primary movie, dates, duration, director and seven genres", bus.Added == 1 &&
                Convert.ToString(Scalar("select ReleaseDate from metadata where DataID=" + detailId)) == "2015-02-19" &&
                Convert.ToInt64(Scalar("select Duration from metadata_video where DataID=" + detailId)) == 160 &&
                Convert.ToString(Scalar("select Director from metadata_video where DataID=" + detailId)) == "ひょん" &&
                Convert.ToString(Scalar("select Genre from metadata where DataID=" + detailId)).Split((char)7).Length == 7);
            Check("actor records and movie associations are stored", Convert.ToString(Scalar("select a.ActorName from actor_info a join metadata_to_actor ma on ma.ActorID=a.ActorID where ma.DataID=" + detailId)) == "本田莉子");
            var db = BrowserClipperService.Import(Read(fixtures, "JavDB-detail"));
            Check("another detail source fills missing rating without replacing existing studio or actor", db.Added == 0 && db.Updated == 1 &&
                Convert.ToDouble(Scalar("select Rating from metadata where DataID=" + detailId)) == 4 &&
                Convert.ToInt64(Scalar("select RatingCount from metadata where DataID=" + detailId)) == 27 &&
                Convert.ToString(Scalar("select Studio from metadata_video where DataID=" + detailId)) == "グローリークエスト" &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_actor where DataID=" + detailId)) == 1);
            var library = BrowserClipperService.Import(Read(fixtures, "JAVLibrary-detail"));
            Check("complete source snapshots retain all three sources and explicit actor aliases", Convert.ToInt64(Scalar("select count(*) from browser_clip_sources where DataID=" + detailId)) == 3 &&
                Convert.ToString(Scalar("select SnapshotJson from browser_clip_sources where DataID=" + detailId + " and Site='JAVLibrary'")).Contains("仲里紗羽"));
            var imageJson = JObject.Parse(Convert.ToString(Scalar("select ImageUrls from metadata_video where DataID=" + detailId)));
            Check("captured covers and all twenty sample images use Jvedio's image schema", imageJson["BigImageUrl"].ToString().EndsWith("4oky_b.jpg") && imageJson["ExtraImageUrl"].Count() == 20);
            Check("opaque JavDB and JAVLibrary page IDs are saved for later synchronization", Convert.ToString(Scalar("select RemoteValue from common_url_code where LocalValue='GVG-107' and WebType='db'")) == "wqmpe" &&
                Convert.ToString(Scalar("select RemoteValue from common_url_code where LocalValue='GVG-107' and WebType='library'")) == "javliiib7m");
            var aliases = Read(fixtures, "JAVLibrary-detail", 2);
            BrowserClipperService.Import(aliases);
            Check("explicit aliases reuse an existing actor instead of adding a duplicate", Convert.ToInt64(Scalar("select count(*) from actor_info")) == 1);
            Check("a repeated detail save leaves metadata unchanged", BrowserClipperService.Import(Read(fixtures, "JavBus-detail")).Skipped == 1);

            var pictureVideo = MapperManager.videoMapper.SelectVideoByID(detailId);
            long oldPictureMode = ConfigManager.Settings.PicPathMode;
            ConfigManager.Settings.PicPathMode = 2;
            Check("virtual clipped videos have usable image paths in relative-to-video mode", !string.IsNullOrEmpty(pictureVideo.GetBigImage(".jpg", false)) &&
                pictureVideo.GetBigImage(".jpg", false).StartsWith(user, StringComparison.OrdinalIgnoreCase));
            ConfigManager.Settings.PicPathMode = oldPictureMode;
            var pictureRequest = new BrowserClipImage { LibraryId = 2, DataId = detailId, Kind = "cover", Data = "" };
            bool wrongPictureLibrary = false;
            try { BrowserClipperImages.Save(pictureRequest); } catch (ArgumentException) { wrongPictureLibrary = true; }
            Check("image upload cannot write pictures for a different library", wrongPictureLibrary);
            ConfigManager.BrowserClipperConfig.SaveImages = false; pictureRequest.LibraryId = 1;
            bool disabledPicture = false;
            try { BrowserClipperImages.Save(pictureRequest); } catch (ArgumentException) { disabledPicture = true; }
            Check("the image master switch is enforced by the desktop receiver", disabledPicture);
            ConfigManager.BrowserClipperConfig.SaveImages = true;

            var mediaSettings = ConfigManager.BrowserClipperConfig;
            var mediaRequest = Read(fixtures, "JavBus-detail"); mediaRequest.Items[0].Code = "MEDIA-001";
            mediaSettings.SaveImages = false;
            Check("disabling image saving queues no media while metadata still saves", BrowserClipperService.Import(mediaRequest).ImageDataIds.Count == 0);
            mediaSettings.SaveImages = true; mediaSettings.SaveCoverImages = false; mediaSettings.SavePreviewImages = false;
            Check("cover and preview switches can both be off", BrowserClipperService.Import(mediaRequest).ImageDataIds.Count == 0);
            mediaSettings.SaveCoverImages = true;
            Check("cover-only saving queues the captured cover", BrowserClipperService.Import(mediaRequest).ImageDataIds.Count == 1);
            mediaSettings.SaveCoverImages = false; mediaSettings.SavePreviewImages = true;
            Check("preview-only saving works independently of cover saving", BrowserClipperService.Import(mediaRequest).ImageDataIds.Count == 1);
            mediaSettings.SaveImages = false; mediaSettings.SavePreviewVideos = true;
            mediaRequest.Items[0].Metadata.PreviewVideoUrls = new List<string> { "http://127.0.0.1:9/fixture.mp4" };
            Check("preview video saving is independent of the images master switch", BrowserClipperService.Import(mediaRequest).ImageDataIds.Count == 1);
            mediaSettings.SaveImages = true; mediaSettings.SaveCoverImages = true; mediaSettings.SavePreviewImages = false; mediaSettings.SavePreviewVideos = false;
            var mediaVideo = MapperManager.videoMapper.SelectVideoByID(Convert.ToInt64(Scalar("select DataID from metadata_video where VID='MEDIA-001'")));
            var mediaTask = new Jvedio.Core.Net.DownLoadTask(mediaVideo) { CapturedMedia = new Jvedio.Core.Net.CapturedMediaOptions { PreviewVideos = true } };
            var mediaDownloader = new Jvedio.Core.Net.VideoDownLoader(mediaVideo, System.Threading.CancellationToken.None, null);
            Check("captured-media task skips metadata scraping and disabled cover downloads", mediaTask.GetDataInfo(mediaVideo, mediaDownloader, null, null).GetAwaiter().GetResult() == null &&
                mediaTask.DownloadPoster(mediaVideo, null, mediaDownloader, null).GetAwaiter().GetResult() && mediaTask.DownloadThumbnail(mediaVideo, null, mediaDownloader, null).GetAwaiter().GetResult());
            var recordMethod = typeof(Jvedio.Core.Tasks.DownloadManager).GetMethod("ToRecord", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var recordJson = JObject.FromObject(recordMethod.Invoke(null, new object[] { mediaTask }));
            Check("media choices persist with interrupted download tasks", (bool)recordJson["CapturedMedia"]["PreviewVideos"] && !(bool)recordJson["CapturedMedia"]["CoverImages"]);

            var invalid = Read(fixtures, "JavBus-list"); invalid.LibraryId = 3; Rejected("picture libraries reject clips without side effects", invalid);
            invalid.LibraryId = 999; Rejected("missing library rejects clips without side effects", invalid);
            invalid = Read(fixtures, "JavBus-list"); invalid.Items.Add(new BrowserClipItem { Code = "not a code" }); Rejected("a bad batch member rolls back the entire batch", invalid);
            invalid = Read(fixtures, "JavBus-detail"); invalid.Items[0].Metadata.Rating = 100; Rejected("ratings outside the five-point scale are rejected", invalid);
            invalid = Read(fixtures, "JavBus-detail"); invalid.Items[0].Metadata.CoverUrl = "file:///C:/secret"; Rejected("non-HTTP image URLs are rejected", invalid);
            var quoted = new BrowserClipRequest { LibraryId = 1, Site = "JavBus", Items = new List<BrowserClipItem> { new BrowserClipItem { Code = "TEST-001", Title = "O'Reilly; DROP TABLE metadata; -- 中文" } } };
            BrowserClipperService.Import(quoted);
            Check("quoted webpage titles are stored as data", Convert.ToString(Scalar("select Title from metadata where Title like 'O%Reilly%'")) == quoted.Items[0].Title);
            long quotedId = Convert.ToInt64(Scalar("select m.DataID from metadata m join metadata_video v on v.DataID=m.DataID where v.VID='TEST-001'"));
            MapperManager.videoMapper.deleteVideoByIds(new List<string> { quotedId.ToString() });
            Check("normal video deletion also removes captured source snapshots", Convert.ToInt64(Scalar("select count(*) from browser_clip_sources where DataID=" + quotedId)) == 0);
            Sql("create trigger fail_clipper_insert before insert on metadata_video when NEW.VID='FAIL-002' begin select raise(ABORT,'fixture'); end");
            long beforeFailure = Convert.ToInt64(Scalar("select count(*) from metadata"));
            bool failed = false;
            try { BrowserClipperService.Import(new BrowserClipRequest { LibraryId = 1, Site = "JavBus", Items = new List<BrowserClipItem> { new BrowserClipItem { Code = "FAIL-001" }, new BrowserClipItem { Code = "FAIL-002" } } }); }
            catch (SQLiteException) { failed = true; }
            Check("database failure rolls back both tables and the earlier batch item", failed && beforeFailure == Convert.ToInt64(Scalar("select count(*) from metadata")) && Convert.ToInt64(Scalar("select count(*) from metadata_video where VID like 'FAIL-%'")) == 0);

            using (var service = new BrowserClipperService()) {
                service.Start(0);
                Check("HTTP status requires a connection key", Send(service, "GET", "/v1/status").Item1 == 401);
                Check("wrong connection key is rejected", Send(service, "GET", "/v1/status", token: new string('a',64)).Item1 == 401);
                Check("ordinary website origins are rejected even with a correct key", Send(service, "GET", "/v1/status", token: service.Token, origin: "https://example.com").Item1 == 403);
                var status = Send(service, "GET", "/v1/status", token: service.Token);
                Check("authenticated status lists only video libraries", status.Item1 == 200 && status.Item2["libraries"].Count() == 2);
                var httpList = Read(fixtures, "JavBus-list", 2); httpList.BrowserImages = true;
                var clipped = Send(service, "POST", "/v1/clips", httpList, service.Token);
                Check("real HTTP save is idempotent and reports its result", clipped.Item1 == 200 && (int)clipped.Item2["added"] == 0 && (int)clipped.Item2["skipped"] == 30);
                Check("unknown HTTP endpoints are rejected", Send(service, "GET", "/unexpected", token: service.Token).Item1 == 404);
                int activePort = service.Port; service.Dispose();
                bool stopped = false;
                try { using (var client = new TcpClient()) client.Connect(IPAddress.Loopback, activePort); } catch (SocketException) { stopped = true; }
                Check("stopping the receiver releases the loopback port", stopped);
            }
            using (var credential = new BrowserClipperService(true)) {
                string key = credential.Token;
                ConfigManager.BrowserClipperConfig.Read();
                using (var reload = new BrowserClipperService(true)) Check("connection key survives configuration reload without being stored in plain text", reload.Token == key && !ConfigManager.BrowserClipperConfig.ProtectedToken.Contains(key));
            }
            Jvedio.Core.Plugins.Crawler.CrawlerManager.PluginMetaDatas = new List<SuperControls.Style.Plugin.PluginMetaData>();
            var settings = new Window_Settings();
            Check("browser clipper controls are embedded in Scan and Import settings", LogicalTree(settings).OfType<TabItem>().Any(tab => Convert.ToString(tab.Header) == "扫描与导入" && LogicalTree(tab).OfType<BrowserClipperSettings>().Any()));
            ((TabControl)settings.FindName("TabControl")).SelectedItem = settings.FindName("ScanImportTab");
            PreviewScanAndImport(settings, Path.Combine(fixtures, "scan-import-settings.png"));
            settings.Close();
            Preview(new BrowserClipperSettings(), Path.Combine(fixtures, "desktop-settings.png"));
            Console.WriteLine("Isolated data: " + user);
            Console.WriteLine(failures == 0 ? "All browser clipper regression checks passed" : failures + " checks failed");
            if (failures == 0 && args.Length > 2 && args[2] == "serve") {
                ConfigManager.BrowserClipperConfig.SaveImages = false;
                ConfigManager.BrowserClipperConfig.SavePreviewVideos = true;
                ConfigManager.DownloadConfig.DownloadPreviewImage = false;
                // Pre-existing harmless fixture images keep browser integration tests offline.
                foreach (long libraryId in new long[] { 1, 2 }) {
                    long id = Convert.ToInt64(Scalar("select m.DataID from metadata m join metadata_video v on v.DataID=m.DataID where m.DBId=" + libraryId + " and v.VID='GVG-107'"));
                    var video = MapperManager.videoMapper.SelectVideoByID(id);
                    foreach (string imagePath in new[] { video.GetSmallImage(), video.GetBigImage() }) {
                        Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
                        File.Copy(Path.Combine(fixtures, "test-image.png"), imagePath, true);
                    }
                }
                BrowserClipperService.Instance.Start(0);
                var credentialPath = Path.Combine(fixtures, "test-connection.json");
                var previewVideoPaths = new long[] { 1, 2 }.Select(libraryId => {
                    long id = Convert.ToInt64(Scalar("select m.DataID from metadata m join metadata_video v on v.DataID=m.DataID where m.DBId=" + libraryId + " and v.VID='GVG-107'"));
                    return Path.Combine(MapperManager.videoMapper.SelectVideoByID(id).GetExtraImage(), "preview-video-1.mp4");
                }).ToList();
                var coverImagePaths = new[] { MapperManager.videoMapper.SelectVideoByID(detailId).GetBigImage(".jpg", false), MapperManager.videoMapper.SelectVideoByID(detailId).GetSmallImage(".jpg", false) };
                File.WriteAllText(credentialPath, JsonConvert.SerializeObject(new { port = BrowserClipperService.Instance.Port, token = BrowserClipperService.Instance.Token, previewVideoPaths, coverImagePaths }));
                var mediaTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                mediaTimer.Tick += (s, e) => {
                    string flag = Path.Combine(fixtures, "test-enable-images.flag");
                    if (!File.Exists(flag)) return;
                    File.Delete(flag); ConfigManager.BrowserClipperConfig.SaveImages = true; ConfigManager.BrowserClipperConfig.SaveCoverImages = true; ConfigManager.BrowserClipperConfig.SaveActorImages = true;
                };
                mediaTimer.Start();
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
                timer.Tick += (s, e) => app.Shutdown(); timer.Start();
                app.Run();
                File.Delete(credentialPath);
            }
            return failures == 0 ? 0 : 1;
        } catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
    private static IEnumerable<DependencyObject> LogicalTree(DependencyObject root)
    {
        yield return root;
        foreach (object child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject dependency) foreach (var item in LogicalTree(dependency)) yield return item;
    }
}
