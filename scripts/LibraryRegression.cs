using Jvedio;
using Jvedio.Core.Library;
using Jvedio.Core.UserControls;
using Jvedio.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class LibraryRegression
{
    private static int failures;
    private static void Check(string name, bool result)
    {
        Console.WriteLine((result ? "PASS: " : "FAIL: ") + name);
        if (!result) failures++;
    }
    private static void Sql(string sql)
    {
        if (MapperManager.metaDataMapper.ExecuteNonQuery(sql) < 0)
            throw new InvalidOperationException("Fixture database write failed");
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (s, e) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
    private static void Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.ElapsedMilliseconds < 15000) Pump();
        if (!condition()) throw new TimeoutException("Isolated UI did not finish loading");
    }
    private static void SaveImage(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        int width = (int)Math.Ceiling(element.ActualWidth), height = (int)Math.Ceiling(element.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }

    public static int Run(string output)
    {
        MapperManager.Init();
        ConfigManager.Init(() => { });
        ConfigManager.Main.CurrentDBId = 1;
        ConfigManager.Main.FirstRun = false;
        Sql("insert into app_databases(DBId,Name,DataType) values(1,'Fixture library',0),(2,'Other library',0),(3,'Pictures',1)");
        Sql("insert into metadata(DataID,DBId,DataType,Title,TitleCN,Size,ViewCount,Grade,Path,PathExist,SubtitleExist,Genre,Country,ReleaseDate,FirstScanDate,CreateDate) values" +
            "(1,1,0,'First video','Translated',2147483648,2,4.5,'X:/fixture.mp4',1,1,'A'||char(7)||'A'||char(7)||'B','JP','2020-01-01','2026-10-07','2026-10-07')," +
            "(2,1,0,'Second video','',0,0,0,'',1,0,'','','1900-01-01','','2026-10-06')," +
            "(3,1,0,'Third video','',10737418240,12,2,'X:/third.mp4',1,0,'A','JP','2022-01-01','2025-01-01','2025-01-01')," +
            "(4,2,0,'Other library video','',1073741824,0,0,'',0,0,'A','US','2024-01-01','2026-10-01','2026-10-01')," +
            "(5,3,1,'Picture','',107374182400,100,5,'',0,0,'C','US','2025-01-01','2026-10-01','2026-10-01')");
        Sql("insert into metadata_video(MVID,DataID,VID,Duration,FileDuration,VideoType,Studio,Series,Director,WebType) values" +
            "(1,1,'OLD-001',90,999999,1,'Old studio','S','D','db')," +
            "(2,1,'TEST-001',90,3600,1,'Maker','S','D','db')," +
            "(3,2,'TEST-002',30,0,0,'','','','')," +
            "(4,3,'TEST-003',0,0,2,'Maker'||char(7)||'Other','S','D','db')," +
            "(5,4,'TEST-004',10,0,1,'Maker','S','D','db')");
        Sql("insert into actor_info(ActorID,ActorName) values(9001,'Actor A'),(9002,'Actor B')");
        Sql("insert into metadata_to_actor(DataID,ActorID) values(1,9001),(2,9001),(3,9002)");

        LibraryLabelService.Create(1, "Empty label");
        LibraryLabelService.Create(2, "Empty label");
        Check("new unassigned labels persist and remain library-scoped",
            LibraryLabelService.List(1).Single().Count == 0 && LibraryLabelService.List(2).Single().Count == 0);
        LibraryLabelService.Assign(1, "O'Reilly (A)%_中文", new Dictionary<long, bool> { [1] = true, [2] = true, [4] = true });
        Check("quoted, parenthesized and Unicode names assign safely without touching another library",
            LibraryLabelService.List(1).Single(label => label.Name == "O'Reilly (A)%_中文").Count == 2 &&
            LibraryLabelService.QueryVideos(2, "O'Reilly (A)%_中文", "", true, 1).Total == 0);
        LibraryLabelService.Assign(1, "O'Reilly (A)%_中文", new Dictionary<long, bool> { [1] = true });
        Check("repeated assignment cannot create duplicate relations",
            LibraryLabelService.List(1).Single(label => label.Name == "O'Reilly (A)%_中文").Count == 2);
        var browse = new SuperUtils.Framework.ORM.Wrapper.SelectWrapper<Jvedio.Entity.Video>();
        browse.Eq("metadata_to_label.LabelName", "O'Reilly (A)%_中文");
        string browseSql = "select count(*) from metadata join metadata_to_label on metadata_to_label.DataID=metadata.DataID " + browse.ToWhere(true);
        Check("the existing movie-tab wrapper can browse quoted label names",
            MapperManager.metaDataMapper.SelectCount(browseSql) == 2);
        Check("assignment search treats percent and underscore as literal text",
            LibraryLabelService.QueryVideos(1, "O'Reilly (A)%_中文", "%_", false, 1).Total == 0);
        LibraryLabelService.Assign(2, "Shared", new Dictionary<long, bool> { [4] = true });
        LibraryLabelService.Assign(1, "Shared", new Dictionary<long, bool> { [2] = true });
        LibraryLabelService.Rename(1, "O'Reilly (A)%_中文", "Shared");
        Check("rename merges overlapping assignments and preserves identically named labels in other libraries",
            LibraryLabelService.List(1).Single(label => label.Name == "Shared").Count == 2 &&
            LibraryLabelService.List(2).Single(label => label.Name == "Shared").Count == 1 &&
            !LibraryLabelService.List(1).Any(label => label.Name == "O'Reilly (A)%_中文"));
        LibraryLabelService.SetLabels(1, new[] { "Shared", "New (with parentheses)", "New (with parentheses)" });
        Check("single-video editing deduplicates labels and keeps empty saved labels in suggestions",
            LibraryLabelService.Suggestions(1, "Empty").Single() == "Empty label(0)" &&
            LibraryLabelService.List(1).Single(label => label.Name == "New (with parentheses)").Count == 1);
        LibraryLabelService.SetLabels(1, new[] { "Shared" });
        Check("removing the last association retains the saved label",
            LibraryLabelService.List(1).Single(label => label.Name == "New (with parentheses)").Count == 0);
        bool invalidRejected = false;
        try { LibraryLabelService.Create(1, "Bad\nname"); }
        catch (ArgumentException) { invalidRejected = true; }
        Check("invalid control characters cannot become labels", invalidRejected);

        var stats = LibraryStatisticsService.Collect(1, new DateTime(2026, 10, 7));
        Check("statistics count each metadata video once despite duplicate video rows",
            stats.Metrics["Videos"] == 3 && stats.Metrics["Size"] == 12884901888);
        Check("play, rating and subtitle metrics have the correct denominators",
            stats.Metrics["Watched"] == 2 && stats.Metrics["Unwatched"] == 1 && stats.Metrics["PlayCount"] == 14 &&
            stats.Metrics["Favorites"] == 2 && stats.Metrics["AvgGrade"] == 3.25 && stats.Metrics["Subtitles"] == 1);
        Check("duration prefers recorded seconds, falls back to scraped minutes and excludes unknowns from averages",
            stats.Metrics["Duration"] == 5400 && stats.Metrics["DurationKnown"] == 2 && stats.Metrics["AvgDuration"] == 2700);
        Check("availability requires a path and import dates can fall back to creation time",
            stats.Metrics["Playable"] == 2 && stats.Metrics["Imported30"] == 2);
        Check("label totals include unassigned saved labels and actor totals are distinct",
            stats.Metrics["Labels"] == 3 && stats.Metrics["Labeled"] == 2 && stats.Metrics["Actors"] == 2);
        Check("genre duplicates within one video are counted only once",
            stats.Charts.Single(chart => chart.TitleKey == "StatByGenre").Items.Single(item => item.Label == "A").Count == 2);
        Check("all 19 charts are supplied and exclusive distribution buckets sum to the video total",
            stats.Charts.Count == 19 && stats.Charts.Where(chart => new[] { "Donut", "Column" }.Contains(chart.Kind))
                .All(chart => chart.Items.Sum(item => item.Count) == 3));
        var all = LibraryStatisticsService.Collect(null, new DateTime(2026, 10, 7));
        Check("all-library statistics include other video libraries but exclude pictures", all.Metrics["Videos"] == 4);
        var empty = LibraryStatisticsService.Collect(999, new DateTime(2026, 10, 7));
        Check("empty libraries produce zero metrics and finite averages",
            empty.Metrics["Videos"] == 0 && empty.Metrics.Values.All(value => !double.IsNaN(value) && !double.IsInfinity(value)));
        LibraryLabelService.Delete(1, "Shared");
        Check("deleting a label removes only its library assignments and never deletes videos",
            !LibraryLabelService.List(1).Any(label => label.Name == "Shared") &&
            LibraryLabelService.QueryVideos(2, "Shared", "", true, 1).Total == 1 &&
            LibraryLabelService.CountVideos(1) == 3);

        for (int i = 10; i < 120; i++) {
            Sql("insert into metadata(DataID,DBId,DataType,Title,Size,ReleaseDate,FirstScanDate) values(" + i + ",1,0,'Fixture " + i + "',2147483648,'202" +
                (i % 6) + "-01-01','2026-" + (1 + i % 10).ToString("00") + "-05')");
            Sql("insert into metadata_video(DataID,VID,Duration,VideoType,Studio,Series,Director) values(" + i + ",'TEST-" + i + "'," +
                (20 + i % 160) + "," + (i % 3) + ",'Maker " + i % 5 + "','Series " + i % 8 + "','Director " + i % 4 + "')");
        }
        var first = LibraryLabelService.QueryVideos(1, "Empty label", "", false, 1);
        var second = LibraryLabelService.QueryVideos(1, "Empty label", "", false, 2);
        Check("video assignment queries paginate without dropping or duplicating videos",
            first.Total == 113 && first.Videos.Count == 100 && second.Videos.Count == 13 &&
            !first.Videos.Select(video => video.DataID).Intersect(second.Videos.Select(video => video.DataID)).Any());
        LibraryLabelService.Assign(1, "待整理", Enumerable.Range(10, 20).ToDictionary(id => (long)id, id => true));
        LibraryLabelService.Assign(1, "已看", Enumerable.Range(30, 15).ToDictionary(id => (long)id, id => true));
        LibraryLabelService.Create(1, "喜欢的系列");
        LibraryLabelService.Create(1, "高画质收藏");

        Directory.CreateDirectory(output);
        System.Threading.SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var window = new Window_Statistics(1) { Left = -10000, ShowInTaskbar = false, ShowActivated = false };
        window.Show();
        var snapshotField = typeof(Window_Statistics).GetField("_Snapshot", BindingFlags.Instance | BindingFlags.NonPublic);
        Until(() => snapshotField.GetValue(window) != null);
        Pump();
        SaveImage(window, Path.Combine(output, "statistics-overview.png"));
        var tabs = (TabControl)typeof(Window_Statistics).GetField("_Tabs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        Check("statistics UI loads four navigable tabs with native charts", tabs.Items.Count == 4);
        tabs.SelectedIndex = 1; Pump();
        SaveImage(window, Path.Combine(output, "statistics-history.png"));
        tabs.SelectedIndex = 3; Pump();
        SaveImage(window, Path.Combine(output, "statistics-labels.png"));
        tabs.SelectedIndex = 0; Pump();
        var overview = (StackPanel)((ScrollViewer)((TabItem)tabs.Items[0]).Content).Content;
        overview.Children.OfType<Expander>().Single().IsExpanded = true;
        Pump();
        SaveImage(window, Path.Combine(output, "statistics-more-metrics.png"));
        window.Close();
        var labelView = new LibraryLabelView(1);
        var host = new Window { Content = labelView, Width = 1060, Height = 700, Left = -10000, ShowInTaskbar = false, ShowActivated = false };
        host.Show();
        var status = (TextBlock)typeof(LibraryLabelView).GetField("_Status", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(labelView);
        Until(() => status.Text != SuperControls.Style.LangManager.GetValueByKey("HealthWorking"));
        Pump();
        SaveImage(host, Path.Combine(output, "labels-page.png"));
        var cards = (WrapPanel)typeof(LibraryLabelView).GetField("_Cards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(labelView);
        Check("label page displays assigned and unassigned label cards", cards.Children.Count == LibraryLabelService.List(1).Count);
        host.Close();
        var picker = new Window_LabelVideos(1, "待整理") { Left = -10000, ShowInTaskbar = false, ShowActivated = false };
        picker.Show();
        var rows = (DataGrid)typeof(Window_LabelVideos).GetField("_Grid", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(picker);
        Until(() => rows.Items.Count > 0);
        Check("assignment UI loads a paginated first page", rows.Items.Count == 100);
        var changed = (Window_LabelVideos.Row)rows.Items[0];
        changed.Selected = !changed.Original;
        long changedId = changed.DataID;
        typeof(Window_LabelVideos).GetField("_Page", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(picker, 2);
        var load = picker.LoadAsync(); Until(() => load.IsCompleted); load.GetAwaiter().GetResult();
        typeof(Window_LabelVideos).GetField("_Page", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(picker, 1);
        load = picker.LoadAsync(); Until(() => load.IsCompleted); load.GetAwaiter().GetResult();
        var same = rows.Items.OfType<Window_LabelVideos.Row>().Single(row => row.DataID == changedId);
        Check("unsaved assignment changes survive paging", same.Selected != same.Original);
        var apply = (System.Threading.Tasks.Task)typeof(Window_LabelVideos)
            .GetMethod("ApplyAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(picker, null);
        Until(() => apply.IsCompleted); apply.GetAwaiter().GetResult();
        Check("assignment UI saves through the service and clears pending changes",
            LibraryLabelService.QueryVideos(1, "待整理", "TEST-" + changedId, true, 1).Videos.Any(video => video.DataID == changedId));
        Pump(); SaveImage(picker, Path.Combine(output, "label-videos.png"));
        picker.Close();
        Console.WriteLine("UI previews: " + output);
        return failures == 0 ? 0 : 1;
    }
}
