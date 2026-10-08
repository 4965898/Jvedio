// Media reads are real; all database writes use StartupRegression's isolated fixture directory.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Jvedio;
using Jvedio.Entity;
using Jvedio.ViewModel;

internal static class VideoInfoRegression
{
    private static int failures;
    private static void Check(string name, bool result)
    {
        Console.WriteLine((result ? "PASS: " : "FAIL: ") + name);
        if (!result) failures++;
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (s, e) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static void Until(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();
        while (!done() && clock.ElapsedMilliseconds < 15000) Pump();
        if (!done()) throw new TimeoutException("Media information did not finish loading");
        Pump();
    }
    private static void Sql(string sql)
    {
        if (MapperManager.metaDataMapper.ExecuteNonQuery(sql) < 0)
            throw new InvalidOperationException("Fixture write failed");
    }
    private static void InsertVideo(long id, string path)
    {
        Sql("INSERT INTO metadata(DataID,DBId,DataType,Title,Path) VALUES(" + id + ",1,0,'Media fixture','" + path.Replace("'", "''") + "')");
        Sql("INSERT INTO metadata_video(DataID,VID) VALUES(" + id + ",'TEST-" + id + "')");
    }
    private static void SaveImage(FrameworkElement panel, string path)
    {
        panel.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(panel.ActualWidth), (int)Math.Ceiling(panel.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(panel);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
    private static TabControl InfoTabs(Window_Details window)
    {
        return Descendants(window).OfType<TabControl>().Single(tab =>
            BindingOperations.GetBinding(tab, Selector.SelectedIndexProperty)?.Path?.Path == "InfoSelectedIndex");
    }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var item in Descendants(child)) yield return item;
        }
    }
    private static bool VisibleFields(Window_Details window)
    {
        var panel = (StackPanel)window.FindName("infoStackPanel");
        var fields = Descendants(panel).OfType<TextBox>().ToDictionary(
            box => BindingOperations.GetBinding(box, TextBox.TextProperty).Path.Path, box => box.Text);
        return new[] { "Format", "Duration", "Resolution", "FrameRate", "AudioFormat", "Extension", "FileSize", "HasSubtitle" }
            .All(name => fields.TryGetValue("VideoInfo." + name, out string value) && !string.IsNullOrEmpty(value));
    }
    public static int Run(string output, string samplePath)
    {
        // Model navigation from a WPF event, including its dispatcher synchronization context.
        System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Application.Current.Dispatcher));
        MapperManager.Init(); ConfigManager.Init(() => { });
        ConfigManager.Main.CurrentDBId = 1; ConfigManager.Main.FirstRun = false;
        string first = Path.Combine(output, "first.mp4"), second = Path.Combine(output, "second.mp4");
        var info = Video.GetMediaInfo(first);
        Check("native reader returns video, audio and file metadata",
            info.Format == "MPEG-4" && info.Resolution == "160x120" && info.AudioFormat == "AAC" &&
            info.Extension == "MP4" && !string.IsNullOrEmpty(info.FrameRate) && !string.IsNullOrEmpty(info.FileSize));
        File.WriteAllText(Path.ChangeExtension(first, ".srt"), "1\n00:00:00,000 --> 00:00:00,500\nFixture\n");
        Check("external subtitle path is preserved", Video.GetMediaInfo(first).SubtitlePath == Path.ChangeExtension(first, ".srt"));

        ConfigManager.Detail.InfoSelectedIndex = 0;
        var vm = new VieModel_Details(null) { CurrentVideo = new Video { Path = "" } };
        bool uiThread = true;
        vm.PropertyChanged += (s, e) => { if (e.PropertyName == "VideoInfo" || e.PropertyName == "LoadingVideoInfo") uiThread &= Application.Current.Dispatcher.CheckAccess(); };
        vm.InfoSelectedIndex = 1; Until(() => vm.VideoInfo != null && !vm.LoadingVideoInfo);
        vm.InfoSelectedIndex = 0; vm.CurrentVideo = new Video { Path = first };
        Check("changing the current movie clears a cached empty result", vm.VideoInfo == null);
        vm.InfoSelectedIndex = 1; Until(() => vm.VideoInfo != null && !vm.LoadingVideoInfo);
        Check("selecting video info after an unavailable movie displays the local file", vm.VideoInfo.Resolution == "160x120" && vm.VideoInfo.Extension == "MP4");

        vm.InfoSelectedIndex = 0; vm.CurrentVideo = new Video { Path = first }; vm.LoadVideoInfo();
        vm.CurrentVideo = new Video { Path = second }; vm.InfoSelectedIndex = 1;
        Until(() => vm.VideoInfo != null && !vm.LoadingVideoInfo);
        // Allow the superseded read to complete as well.
        for (int i = 0; i < 10; i++) Pump();
        Check("rapid movie changes only publish the latest movie", vm.VideoInfo.Resolution == "320x180" && vm.VideoInfo.FileName == "second");
        Check("media property notifications run on the UI dispatcher", uiThread);

        vm.InfoSelectedIndex = 0;
        string appeared = Path.Combine(output, "appeared.mp4");
        vm.CurrentVideo = new Video { Path = appeared }; vm.InfoSelectedIndex = 1;
        Until(() => vm.VideoInfo != null && !vm.LoadingVideoInfo);
        File.Copy(first, appeared);
        vm.InfoSelectedIndex = 0; vm.InfoSelectedIndex = 1;
        Until(() => vm.VideoInfo != null && !vm.LoadingVideoInfo);
        Check("reselecting the tab retries a previously unavailable file", vm.VideoInfo.Resolution == "160x120");

        vm.InfoSelectedIndex = 0;
        vm.CurrentVideo = new Video { Path = Path.Combine(output, "missing.mp4"), SubSection = first + SuperUtils.Values.ConstValues.Separator + second };
        vm.InfoSelectedIndex = 1; Until(() => vm.VideoInfo != null && !vm.LoadingVideoInfo);
        Check("segmented movies use an existing section when the primary path is unavailable", vm.VideoInfo.Resolution == "160x120");

        Sql("INSERT INTO app_databases(DBId,Name,DataType) VALUES(1,'Media fixture',0)");
        InsertVideo(1, ""); InsertVideo(2, first); InsertVideo(3, second);
        ConfigManager.Detail.InfoSelectedIndex = 1;
        var window = new Window_Details(1, null) { Left = -10000, ShowInTaskbar = false, ShowActivated = false };
        window.Show(); Until(() => window.DataContext != null);
        var details = (VieModel_Details)window.DataContext;
        Until(() => details.VideoInfo != null && !details.LoadingData && !details.LoadingVideoInfo);
        var tabs = InfoTabs(window);
        tabs.SelectedIndex = 0; Pump(); details.Load(2); Until(() => !details.LoadingData);
        tabs.SelectedIndex = 1;
        Until(() => details.VideoInfo != null && !details.LoadingVideoInfo);
        Check("actual detail controls display metadata after navigating from an unavailable movie", VisibleFields(window) && details.VideoInfo.Resolution == "160x120");
        SaveImage((StackPanel)window.FindName("infoStackPanel"), Path.Combine(output, "video-info-fields.png"));
        details.Load(3); Until(() => !details.LoadingData && !details.LoadingVideoInfo && details.VideoInfo != null);
        Check("navigation while the video tab stays selected refreshes the visible resolution", VisibleFields(window) && details.VideoInfo.Resolution == "320x180");
        window.Close();

        ConfigManager.Detail.InfoSelectedIndex = 1;
        window = new Window_Details(2, null) { Left = -10000, ShowInTaskbar = false, ShowActivated = false };
        window.Show(); Until(() => window.DataContext != null);
        details = (VieModel_Details)window.DataContext;
        Until(() => details.VideoInfo != null && !details.LoadingVideoInfo);
        Check("reopening details with the saved video tab loads all visible fields", VisibleFields(window));
        window.Close();

        if (!string.IsNullOrEmpty(samplePath)) {
            InsertVideo(4, samplePath);
            window = new Window_Details(4, null) { Left = -10000, ShowInTaskbar = false, ShowActivated = false };
            window.Show(); Until(() => window.DataContext != null);
            details = (VieModel_Details)window.DataContext;
            Until(() => details.VideoInfo != null && !details.LoadingVideoInfo);
            Check("the supplied local video displays metadata through the actual detail controls", File.Exists(samplePath) && VisibleFields(window));
            SaveImage((StackPanel)window.FindName("infoStackPanel"), Path.Combine(output, "local-video-info-fields.png"));
            window.Close();
        }
        return failures == 0 ? 0 : 1;
    }
}
