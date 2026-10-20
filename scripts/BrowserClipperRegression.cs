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

            Sql("insert into metadata_label_catalog(DBId,LabelName) values(1,'收藏'),(1,'待看'),(2,'另库专用'),(2,'浏览器测试')");
            var tagged = new BrowserClipRequest { LibraryId = 1, Site = "JavBus", Kind = "list", PageUrl = "https://www.javbus.com/",
                Labels = new List<string> { "收藏", "待看", "收藏" }, Items = new List<BrowserClipItem> {
                    new BrowserClipItem { Code = "TAG-001", Title = "标签测试" }, new BrowserClipItem { Code = "TAG-002" } } };
            bool labelsNotified = false;
            Action<long> onLabelsChanged = id => { labelsNotified = id == 1 && Convert.ToInt64(Scalar("select count(*) from metadata_to_label l join metadata_video v on v.DataID=l.DataID where v.VID='TAG-001'")) == 2; };
            Jvedio.Core.Library.LibraryLabelService.Changed += onLabelsChanged;
            Check("each new movie receives multiple distinct labels in the import transaction", BrowserClipperService.Import(tagged).Added == 2 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_label l join metadata_video v on v.DataID=l.DataID where v.VID in('TAG-001','TAG-002')")) == 4);
            Jvedio.Core.Library.LibraryLabelService.Changed -= onLabelsChanged;
            Check("label pages are notified after their associations have committed", labelsNotified);
            var confirmedLabels = BrowserClipperService.Import(tagged);
            Check("the receiver confirms all labels for every existing item", confirmedLabels.LabelsApplied == 2 && confirmedLabels.LabeledItems == 2);
            Check("reimporting the same labels creates no duplicate associations", BrowserClipperService.Import(tagged).Skipped == 2 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_label l join metadata_video v on v.DataID=l.DataID where v.VID='TAG-001'")) == 2);
            tagged.Items = new List<BrowserClipItem> { new BrowserClipItem { Code = "GVG-107" } };
            Check("existing movies without metadata can receive labels", BrowserClipperService.Import(tagged).Updated == 1 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_label where DataID=" + detailId)) == 2);
            tagged.Labels = new List<string> { "收藏" };
            Check("saving a subset of labels retains all old labels", BrowserClipperService.Import(tagged).Skipped == 1 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_label where DataID=" + detailId)) == 2);
            tagged.Items = new List<BrowserClipItem> { new BrowserClipItem { Code = "TAG-003" } };
            tagged.Labels = new List<string> { "另库专用" }; Rejected("labels from another library reject the entire import", tagged);
            tagged.Labels = new List<string> { "已删除" }; Rejected("deleted labels reject the entire import", tagged);
            tagged.Labels = new List<string> { "无\n效" }; Rejected("control characters in labels reject the entire import", tagged);
            tagged.Labels = Enumerable.Repeat("收藏", 101).ToList(); Rejected("overlarge label selections reject the entire import", tagged);
            tagged.Items = new List<BrowserClipItem> { new BrowserClipItem { Code = "TAG-999" } }; tagged.Labels = new List<string> { "收藏" };
            Sql("create trigger reject_clip_label before insert on metadata_to_label when exists(select 1 from metadata_video where DataID=NEW.DataID and VID='TAG-999') begin select raise(ignore); end");
            Rejected("a silently ignored label association rolls back the movie instead of reporting success", tagged);
            Sql("drop trigger reject_clip_label");

            var created = new BrowserClipRequest { LibraryId = 1, Site = "JavBus", Kind = "list", PageUrl = "https://www.javbus.com/",
                NewLabels = new List<string> { "新片单", "收藏", "新片单" }, Items = new List<BrowserClipItem> { new BrowserClipItem { Code = "LIST-001", Title = "清单测试" } } };
            Check("declared new labels are created with their associations in one import", BrowserClipperService.Import(created).LabelsApplied == 2 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_label_catalog where DBId=1 and LabelName in('新片单','收藏')")) == 2 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_label where DataID=(select DataID from metadata_video where VID='LIST-001')")) == 2);
            Check("created labels stay scoped to the target library", Convert.ToInt64(Scalar("select count(*) from metadata_label_catalog where DBId=2 and LabelName='新片单'")) == 0);
            Check("existing labels keep their original IDs when mixed with new ones",
                Convert.ToInt64(Scalar("select count(*) from metadata_to_label where DataID=(select DataID from metadata_video where VID='LIST-001') and LabelName='收藏'")) == 1);
            created.NewLabels = new List<string> { "无\n效" }; Rejected("control characters reject a new label too", created);
            created.NewLabels = Enumerable.Repeat("新片单", 101).ToList(); Rejected("overlarge new-label selections reject the entire import", created);

            Sql("insert into common_tagstamp(TagID,TagName,SortOrder,Foreground,Background) values" +
                "(21001,'AV',1,'255,255,255,255','154,88,183,255'),(21002,'套路直播',0,'255,255,255,255','0,140,90,255')," +
                "(21003,'其他',-1,'255,255,255,255','80,90,110,255'),(21004,'AV',2,'255,255,255,255','90,120,130,255')," +
                "(21005,'未使用标记',-1,'255,255,255,255','80,90,110,255')");
            var marked = new BrowserClipRequest { LibraryId = 1, Site = "JavBus", Kind = "list", PageUrl = "https://www.javbus.com/",
                MarkIds = new List<long> { 21001, 21002, 21001 }, Items = new List<BrowserClipItem> {
                    new BrowserClipItem { Code = "MARK-001", Title = "标记测试" }, new BrowserClipItem { Code = "MARK-002" } } };
            var markResult = BrowserClipperService.Import(marked);
            Check("filter markers use metadata_to_tagstamp and are confirmed for each new movie", markResult.Added == 2 && markResult.MarksApplied == 2 && markResult.MarkedItems == 2 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_tagstamp t join metadata_video v on v.DataID=t.DataID where v.VID in('MARK-001','MARK-002') and t.TagID in(21001,21002)")) == 4);
            Check("marker imports do not create text labels and retain the default new-item marker",
                Convert.ToInt64(Scalar("select count(*) from metadata_to_label l join metadata_video v on v.DataID=l.DataID where v.VID like 'MARK-%'")) == 0 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_tagstamp t join metadata_video v on v.DataID=t.DataID where v.VID in('MARK-001','MARK-002') and t.TagID=10000")) == 2);
            Check("repeated marker imports neither duplicate links nor change counts", BrowserClipperService.Import(marked).Skipped == 2 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_tagstamp t join metadata_video v on v.DataID=t.DataID where v.VID in('MARK-001','MARK-002') and t.TagID in(21001,21002)")) == 4);
            marked.Items = new List<BrowserClipItem> { new BrowserClipItem { Code = "GVG-107" } }; marked.MarkIds = new List<long> { 21003 };
            Check("existing movies can receive filter markers while retaining old text labels", BrowserClipperService.Import(marked).Updated == 1 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_tagstamp where DataID=" + detailId + " and TagID=21003")) == 1 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_label where DataID=" + detailId)) == 2);
            marked.LibraryId = 2; marked.Items[0].Code = "MARK-001"; marked.MarkIds = new List<long> { 21001, 21003 };
            Check("global marker choices are usable in another library without changing the first library movie", BrowserClipperService.Import(marked).Added == 1 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_tagstamp t join metadata m on m.DataID=t.DataID join metadata_video v on v.DataID=m.DataID where m.DBId=1 and v.VID='MARK-001' and t.TagID=21003")) == 0);
            marked.LibraryId = 1; marked.Items[0].Code = "MARK-002"; marked.MarkIds = new List<long> { 21001 };
            Sql("update common_tagstamp set TagName='AV改名' where TagID=21001");
            Check("renaming a marker keeps its ID association valid", BrowserClipperService.Import(marked).Skipped == 1);
            Sql("update common_tagstamp set TagName='AV' where TagID=21001");
            marked.Items[0].Code = "MARK-003"; marked.MarkIds = new List<long> { 21004 };
            BrowserClipperService.Import(marked);
            Check("identically named markers are assigned by ID rather than name", Convert.ToInt64(Scalar("select count(*) from metadata_to_tagstamp t join metadata_video v on v.DataID=t.DataID where v.VID='MARK-003' and t.TagID=21004")) == 1 &&
                Convert.ToInt64(Scalar("select count(*) from metadata_to_tagstamp t join metadata_video v on v.DataID=t.DataID where v.VID='MARK-003' and t.TagID=21001")) == 0);
            Sql("delete from metadata_to_tagstamp where TagID=21004; delete from common_tagstamp where TagID=21004");
            marked.Items[0].Code = "MARK-999"; marked.MarkIds = new List<long> { 999999 }; Rejected("deleted markers reject the entire import", marked);
            marked.MarkIds = new List<long> { 0 }; Rejected("invalid marker IDs reject the entire import", marked);
            marked.MarkIds = Enumerable.Repeat(21001L, 101).ToList(); Rejected("overlarge marker selections reject the entire import", marked);
            marked.MarkIds = new List<long> { 21001 };
            Sql("create trigger reject_clip_mark before insert on metadata_to_tagstamp when NEW.TagID=21001 and exists(select 1 from metadata_video where DataID=NEW.DataID and VID='MARK-999') begin select raise(ignore); end");
            Rejected("silently ignored marker inserts roll back the whole batch", marked); Sql("drop trigger reject_clip_mark");
            Jvedio.Entity.CommonSQL.TagStamp.Init();
            var filterMarks = Jvedio.Entity.CommonSQL.TagStamp.InitTagStamp();
            Check("the real filter reads the imported AV marker and its library-scoped count", filterMarks.Any(mark => mark.TagID == 21001 && mark.TagName == "AV" &&
                mark.Count == Convert.ToInt64(Scalar("select count(*) from metadata_to_tagstamp t join metadata m on m.DataID=t.DataID where m.DBId=1 and t.TagID=21001"))));

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
                Check("status returns the filter marker catalog in filter order including unused markers", (bool)status.Item2["markImport"] && (int)status.Item2["markImportVersion"] == 1 &&
                    status.Item2["marks"].Select(mark => (long)mark["id"]).SequenceEqual(MapperManager.tagStampMapper.GetAllTagStamp().Select(mark => mark.TagID)) &&
                    status.Item2["marks"].Any(mark => (string)mark["name"] == "AV") && status.Item2["marks"].Any(mark => (string)mark["name"] == "套路直播") &&
                    status.Item2["marks"].Any(mark => (long)mark["id"] == 21005));
                Check("HTTP status includes library-scoped labels, including unused labels", (bool)status.Item2["labelImport"] && (int)status.Item2["labelImportVersion"] == 3 &&
                    status.Item2["libraries"][0]["labels"].Values<string>().Contains("收藏") &&
                    !status.Item2["libraries"][0]["labels"].Values<string>().Contains("另库专用") &&
                    status.Item2["libraries"][1]["labels"].Values<string>().Contains("另库专用"));
                var httpList = Read(fixtures, "JavBus-list", 2); httpList.BrowserImages = true;
                var clipped = Send(service, "POST", "/v1/clips", httpList, service.Token);
                Check("real HTTP save is idempotent and reports its result", clipped.Item1 == 200 && (int)clipped.Item2["added"] == 0 && (int)clipped.Item2["skipped"] == 30);
                var httpTagged = Read(fixtures, "JavBus-list", 1); httpTagged.BrowserImages = true; httpTagged.Labels = new List<string> { "收藏", "待看" };
                var taggedReply = Send(service, "POST", "/v1/clips", httpTagged, service.Token);
                Check("HTTP save acknowledges two labels on all thirty movies", taggedReply.Item1 == 200 && (int)taggedReply.Item2["labelsApplied"] == 2 && (int)taggedReply.Item2["labeledItems"] == 30);
                var httpCreated = Read(fixtures, "JavBus-list", 1); httpCreated.BrowserImages = true; httpCreated.NewLabels = new List<string> { "HTTP片单" };
                var createdReply = Send(service, "POST", "/v1/clips", httpCreated, service.Token);
                Check("HTTP save creates a declared new label on all thirty movies", createdReply.Item1 == 200 && (int)createdReply.Item2["labelsApplied"] == 1 && (int)createdReply.Item2["labeledItems"] == 30 &&
                    Convert.ToInt64(Scalar("select count(*) from metadata_label_catalog where DBId=1 and LabelName='HTTP片单'")) == 1);
                var httpMarked = new BrowserClipRequest { LibraryId = 1, Site = "JavBus", Kind = "list", BrowserImages = true, PageUrl = "https://www.javbus.com/",
                    MarkIds = new List<long> { 21001, 21002 }, Items = new List<BrowserClipItem> { new BrowserClipItem { Code = "HTTPMARK-001" }, new BrowserClipItem { Code = "HTTPMARK-002" } } };
                var markReply = Send(service, "POST", "/v1/clips", httpMarked, service.Token);
                Check("HTTP imports confirm the real filter marker associations", markReply.Item1 == 200 && (int)markReply.Item2["marksApplied"] == 2 && (int)markReply.Item2["markedItems"] == 2);
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
                File.WriteAllText(credentialPath, JsonConvert.SerializeObject(new { port = BrowserClipperService.Instance.Port, token = BrowserClipperService.Instance.Token,
                    databasePath = Path.GetFullPath(SqlManager.DEFAULT_SQLITE_PATH), previewVideoPaths, coverImagePaths }));
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
