// Runs against the built application in an isolated user directory. Never reads user data.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Jvedio;
using Jvedio.Core.Backup;
using Jvedio.Core.Crawler;
using Jvedio.Core.Global;
using Jvedio.Core.Plugins.Crawler;
using Jvedio.Core.UserControls;
using Jvedio.Entity;
using Jvedio.Entity.Common;
using Jvedio.Entity.CommonSQL;
using Jvedio.ViewModel;
using Newtonsoft.Json;
using SuperControls.Style.Plugin;
using SuperUtils.Framework.ORM.Enums;
using SuperUtils.Framework.ORM.Wrapper;

internal static class StartupRegression
{
    private sealed class TestApp : App
    {
        // Isolated probes must not contend with the user's running application's single-instance handle.
        protected override void OnStartup(StartupEventArgs e) { }
    }
    private static int failures;

    private static void Check(string name, bool ok)
    {
        Console.WriteLine((ok ? "PASS: " : "FAIL: ") + name);
        if (!ok) failures++;
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
        string translation = Path.Combine(root, "translation.config.json");
        if (!File.Exists(translation)) File.WriteAllText(translation, "{}");
    }

    private static void WriteServers(string json)
    {
        MapperManager.appConfigMapper.Insert(new AppConfig {
            ConfigName = "Servers", ConfigValue = json
        }, InsertMode.Replace);
    }

    private static string ReadServers()
    {
        return MapperManager.appConfigMapper.SelectOne(new SelectWrapper<AppConfig>().Eq("ConfigName", "Servers")).ConfigValue;
    }

    private static void Pump()
    {
        DispatcherFrame frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static KeyEventArgs KeyUp(Window host, Key key)
    {
        return new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(host), 0, key) {
            RoutedEvent = Keyboard.KeyUpEvent
        };
    }

    private static void RunChecks()
    {
        MapperManager.Init();
        ConfigManager.Init(() => { });
        ConfigManager.Main.FirstRun = false;
        var saved = new[] {
            new CrawlerServer { PluginID = "crawler/db", Url = "https://example.invalid/db", Enabled = true },
            new CrawlerServer { PluginID = "crawler/unavailable", Url = "https://example.invalid/mirror", Enabled = true }
        };
        string json = JsonConvert.SerializeObject(saved);
        WriteServers(json);
        CrawlerManager.PluginMetaDatas = null;
        ConfigManager.ServerConfig.Read();
        ConfigManager.SaveAll();
        Check("shutdown before plugin loading preserves all saved URLs",
            JsonConvert.DeserializeObject<List<CrawlerServer>>(ReadServers()).Count == 2);
        CrawlerManager.PluginMetaDatas = new List<PluginMetaData>();
        ConfigManager.ServerConfig.Read();
        ConfigManager.ServerConfig.Save();
        Check("temporarily unavailable plugins preserve URL configurations",
            JsonConvert.DeserializeObject<List<CrawlerServer>>(ReadServers()).Count == 2);
        var settings = new VieModel_Settings();
        settings.SaveServers();
        Check("settings save preserves sources outside the loaded plugin list",
            JsonConvert.DeserializeObject<List<CrawlerServer>>(ReadServers()).Count == 2);
        WriteServers("{invalid-json");
        ConfigManager.ServerConfig.Read();
        ConfigManager.ServerConfig.Save();
        Check("failed config read cannot overwrite stored data", ReadServers() == "{invalid-json");
        WriteServers(json);
        ConfigManager.ServerConfig.Read();
        ConfigManager.ServerConfig.CrawlerServers.Clear();
        ConfigManager.ServerConfig.Save();
        Check("deliberately removing all sources still saves an empty list", ReadServers() == "[]");

        var currentSources = Newtonsoft.Json.Linq.JArray.Parse(JsonConvert.SerializeObject(new[] { saved[0] }));
        currentSources[0]["Invoker"] = Newtonsoft.Json.Linq.JObject.Parse("{\"DllPath\":\"runtime-only.dll\"}");
        currentSources[0]["UrlCode"] = Newtonsoft.Json.Linq.JObject.Parse("{\"DataID\":123}");
        currentSources[0]["Cookies"] = "current-fixture-cookie";
        WriteServers(currentSources.ToString());
        string pending = Path.Combine(PathManager.CurrentUserFolder, Jvedio.Core.Config.ServerConfig.PendingRecoveryFileName);
        var pendingSources = Newtonsoft.Json.Linq.JArray.Parse(json);
        pendingSources[0]["Invoker"] = Newtonsoft.Json.Linq.JObject.Parse("{\"DllPath\":\"old-runtime-only.dll\"}");
        pendingSources[1]["UrlCode"] = Newtonsoft.Json.Linq.JObject.Parse("{\"DataID\":456}");
        File.WriteAllText(pending, pendingSources.ToString());
        int recoveredCount = Jvedio.Core.Config.ServerConfig.ApplyPendingRecovery();
        Check("staged recovery preserves current sources, adds missing URLs and runs once",
            recoveredCount == 1 && JsonConvert.DeserializeObject<List<CrawlerServer>>(ReadServers()).Count == 2 &&
            !File.Exists(pending) && Jvedio.Core.Config.ServerConfig.ApplyPendingRecovery() == 0 &&
            Directory.GetFiles(PathManager.CurrentUserFolder, "crawler-sources-before-recovery-*.json").Length == 1);
        var recoveredSources = JsonConvert.DeserializeObject<List<CrawlerServer>>(ReadServers());
        Check("legacy runtime objects are ignored and current source settings survive recovery",
            recoveredSources.All(source => source.Invoker == null && source.UrlCode == null) &&
            recoveredSources[0].Cookies == "current-fixture-cookie");

        foreach (string language in new[] { "zh-CN", "en-US", "ja-JP", "zh-CN" }) {
            SuperControls.Style.LangManager.SetLang(language);
            Jvedio.Core.Lang.LangManager.SetLang(language);
            bool mixed = Application.Current.Resources.MergedDictionaries
                .Where(dict => dict.Source != null && dict.Source.OriginalString.IndexOf("/Lang/", StringComparison.OrdinalIgnoreCase) >= 0)
                .SelectMany(dict => dict.Keys.Cast<object>().Select(key => dict[key] as string))
                .Where(value => value != null)
                .Any(value => System.Text.RegularExpressions.Regex.IsMatch(value, @"\([A-Za-z]\)|\([A-Za-z]）|（[A-Za-z]\)"));
            Check("all shortcut hint brackets remain full-width after switching to " + language,
                !mixed && SuperControls.Style.LangManager.GetValueByKey("AddTagStampGesture").EndsWith("（A）") &&
                SuperControls.Style.LangManager.GetValueByKey("Menu_DeleteInfo").Contains("（D）"));
        }

        string backupRoot = PathManager.BackupPath;
        Directory.CreateDirectory(backupRoot);
        var latest = typeof(BackupService).GetMethod("GetLatestLocalBackupTime");
        Check("missing backup root is treated as no backup",
            latest != null && (DateTime)latest.Invoke(null, null) == DateTime.MinValue);
        Directory.CreateDirectory(Path.Combine(backupRoot, "2026-10-01"));
        File.WriteAllText(Path.Combine(backupRoot, "2026-10-05_013356_362.zip"), "fixture");
        File.WriteAllText(Path.Combine(backupRoot, "2026-10-06_013356_362.zip.partial"), "incomplete");
        Directory.CreateDirectory(Path.Combine(backupRoot, "unrelated-folder"));
        Check("backup period recognizes ZIP and ignores incomplete/unrelated entries",
            (DateTime)latest.Invoke(null, null) == new DateTime(2026, 10, 5, 1, 33, 56, 362));
        Directory.CreateDirectory(Path.Combine(backupRoot, "2026-10-06_013356"));
        Check("backup period supports older dated folders",
            (DateTime)latest.Invoke(null, null) == new DateTime(2026, 10, 6, 1, 33, 56));

        ConfigManager.Settings.BackupDirectory = Path.Combine(PathManager.CurrentUserFolder, "concurrent-backups");
        ConfigManager.Settings.BackupMode = "LocalOnly";
        ConfigManager.Settings.MaxLocalBackups = 1;
        var tasks = new[] { BackupService.CreateAsync(), BackupService.CreateAsync(), BackupService.CreateAsync() };
        Task.WaitAll(tasks);
        Check("overlapping automatic/manual backups finish without retention races",
            tasks.All(t => string.IsNullOrEmpty(t.Result.CleanupError) && string.IsNullOrEmpty(t.Result.RetentionError)) &&
            Directory.GetFiles(ConfigManager.Settings.BackupDirectory, "*.zip").Length == 1);

        var videoList = new VideoList(new SelectWrapper<Video>(), new TabItemEx("Fixture", TabType.GeoVideo, false, false));
        var host = new Window { Content = videoList, Width = 600, Height = 400, Left = -10000, ShowInTaskbar = false };
        host.Show();
        Pump();
        var menu = new ContextMenu { PlacementTarget = videoList };
        var tags = new MenuItem { Name = "TagMenuItems", Header = "Add Tag (A)" };
        tags.Items.Add(new MenuItem { Header = "Fixture tag" });
        menu.Items.Add(tags);
        menu.IsOpen = true;
        Pump();
        var pressA = KeyUp(host, Key.A);
        videoList.ContextMenu_PreviewKeyUp(menu, pressA);
        Pump();
        Check("A opens the selected-video tag submenu without closing its menu",
            pressA.Handled && menu.IsOpen && tags.IsSubmenuOpen);
        videoList.ContextMenu_PreviewKeyUp(menu, KeyUp(host, Key.Down));
        Check("arrow navigation keeps the tag submenu open", menu.IsOpen && tags.IsSubmenuOpen);
        menu.IsOpen = false;

        var blank = new ContextMenu { PlacementTarget = videoList };
        var all = new MenuItem { Header = "All results" };
        var allTags = new MenuItem { Name = "AllTagMenuItems", Header = "Add Tag (A)" };
        allTags.Items.Add(new MenuItem { Header = "Fixture tag" });
        all.Items.Add(allTags);
        blank.Items.Add(all);
        blank.IsOpen = true;
        Pump();
        var blankA = KeyUp(host, Key.A);
        typeof(VideoList).GetMethod("BlankContextMenu_PreviewKeyUp", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(videoList, new object[] { blank, blankA });
        Pump();
        Check("A opens the nested tag submenu for the current results",
            blankA.Handled && blank.IsOpen && all.IsSubmenuOpen && allTags.IsSubmenuOpen);
        blank.IsOpen = false;
        host.Close();
    }

    private static void Prepare(string url)
    {
        MapperManager.Init();
        ConfigManager.Init(() => { });
        MapperManager.appDatabaseMapper.ExecuteNonQuery("INSERT INTO app_databases(DBId, Name, DataType, Hide) VALUES(1, 'Startup fixture', 0, 0)");
        MapperManager.metaDataMapper.ExecuteNonQuery("INSERT INTO metadata(DataID, DBId, DataType, Title, Path) VALUES(1, 1, 0, 'Startup fixture', '')");
        MapperManager.videoMapper.ExecuteNonQuery("INSERT INTO metadata_video(DataID, VID) VALUES(1, 'TEST-001')");
        ConfigManager.Main.FirstRun = false;
        ConfigManager.Main.CurrentDBId = 1;
        ConfigManager.Settings.OpenDataBaseDefault = true;
        ConfigManager.Settings.DefaultDBID = 1;
        ConfigManager.Settings.AutoBackup = true;
        ConfigManager.Settings.BackupMode = "Both";
        ConfigManager.Settings.BackupRemoteType = "WebDAV";
        ConfigManager.Settings.BackupWebDavUrl = url;
        ConfigManager.Settings.BackupWebDavFolder = "";
        ConfigManager.Settings.LastSuccessfulBackupUtc = DateTime.MinValue;
        ConfigManager.Settings.OpenUpgradeWindowAutomatically = false;
        ConfigManager.Settings.BackupDirectory = Path.Combine(PathManager.CurrentUserFolder, "startup-backups");
        ConfigManager.ScanConfig.ScanOnStartUp = false;
        ConfigManager.ScanConfig.DataExistsIndexAfterScan = false;
        ConfigManager.SaveAll();
        MapperManager.Dispose();
    }

    private static void RunStartup(App app, int port, bool expectFast)
    {
        using (var listener = new HttpListener()) {
            listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            listener.Start();
            bool replySent = false;
            bool requestStarted = false;
            Task server = Task.Run(async () => {
                var request = await listener.GetContextAsync();
                requestStarted = true;
                await Task.Delay(10000);
                request.Response.StatusCode = 500;
                request.Response.Close();
                replySent = true;
            });
            Stopwatch timer = Stopwatch.StartNew();
            long readyMs = -1;
            bool readyBeforeReply = false;
            int responsiveTicks = 0;
            var poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            poll.Tick += (s, e) => {
                var main = app.Windows.OfType<Main>().FirstOrDefault();
                if (main != null && main.IsLoaded) {
                    main.Left = -10000;
                    main.ShowInTaskbar = false;
                    var lists = ((VieModel_Main)main.DataContext).TabItemManager.GetAllVideoList();
                    bool loaded = lists.Any(list => {
                        var videos = list.DataContext.GetType().GetProperty("CurrentVideoList").GetValue(list.DataContext) as System.Collections.ICollection;
                        bool rendering = (bool)list.DataContext.GetType().GetProperty("Rendering").GetValue(list.DataContext);
                        return videos != null && videos.Count > 0 && !rendering;
                    });
                    if (loaded && readyMs < 0) {
                        readyMs = timer.ElapsedMilliseconds;
                        readyBeforeReply = !replySent;
                    }
                    if (readyMs >= 0 && requestStarted && !replySent) responsiveTicks++;
                }
                if (readyMs >= 0 && replySent) {
                    Console.WriteLine("STARTUP: first_page_ms=" + readyMs + ", ready_before_backup_reply=" + readyBeforeReply + ", responsive_ticks_during_backup=" + responsiveTicks);
                    if (expectFast)
                        Check("first page is usable while the remote backup request is stalled",
                            readyBeforeReply && readyMs < 10000 && responsiveTicks > 10);
                    poll.Stop();
                    app.Shutdown(failures);
                } else if (timer.ElapsedMilliseconds > 35000) {
                    Check("startup completed within the test timeout", false);
                    poll.Stop();
                    app.Shutdown(1);
                }
            };
            poll.Start();
            var start = new WindowStartUp { Left = -10000, ShowInTaskbar = false, ShowActivated = false };
            app.Run(start);
        }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        // Some Windows native launchers include argv[0] in the managed argument array.
        if (args.Length > 0 && args[0].EndsWith("StartupRegression.exe", StringComparison.OrdinalIgnoreCase))
            args = args.Skip(1).ToArray();
        try {
            return Run(args);
        } catch (Exception ex) {
            Console.WriteLine(ex.GetType().FullName + ": " + ex.Message);
            for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException)
                Console.WriteLine(inner.GetType().FullName + ": " + inner.Message);
            return 1;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Run(string[] args)
    {
        try {
            Console.WriteLine("Preparing isolated application: " + args[0]);
            var app = new TestApp();
            // Load the same resource dictionaries without registering a second automatic StartupUri window.
            foreach (string path in new[] {
                "/SuperControls.Style;component/Lang/zh-CN.xaml", "/Jvedio;component/Core/Lang/zh-CN.xaml",
                "/SuperControls.Style;component/XAML/Skin/DefaultColor.xaml",
                "/SuperControls.Style;component/Themes/SuperControls.xaml",
                "/Jvedio;component/CustomStyle/SuperControls/Basic.xaml",
                "/Jvedio;component/CustomStyle/SuperControls/Pagination.xaml",
                "/Jvedio;component/CustomStyle/SuperControls/PathRadioButton.xaml"
            }) app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });
            app.Resources["GlobalFontSize"] = 14d;
            foreach (int size in new[] { 7, 8, 10, 12, 13, 15, 16, 18, 20, 24, 25 })
                app.Resources["GlobalFontSize" + size] = (double)size;
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            SetPaths(Path.GetFullPath(args[1]));
            if (args[0] == "prepare") Prepare(args[2]);
            else if (args[0] == "startup") RunStartup(app, int.Parse(args[2]), args.Length > 3 && args[3] == "expect-fast");
            else if (args[0] == "library") return LibraryRegression.Run(args[2]);
            else if (args[0] == "video-info") return VideoInfoRegression.Run(args[2], args.Length > 3 ? args[3] : null);
            else RunChecks();
            return failures == 0 ? 0 : 1;
        } catch (Exception ex) {
            Console.WriteLine(ex.GetType().FullName + ": " + ex.Message);
            for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException)
                Console.WriteLine(inner.GetType().FullName + ": " + inner.Message);
            return 1;
        }
    }
}
