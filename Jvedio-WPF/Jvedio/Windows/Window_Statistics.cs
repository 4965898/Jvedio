using Jvedio.Core.Library;
using Jvedio.Core.UserControls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using LangManager = SuperControls.Style.LangManager;

namespace Jvedio.Windows
{
    public sealed class Window_Statistics : Window
    {
        private sealed class LibraryChoice
        {
            public long? DbId { get; set; }
            public string Name { get; set; }
        }
        private readonly ComboBox _Library = new ComboBox { MinWidth = 220, DisplayMemberPath = "Name" };
        private readonly Button _Refresh = new Button();
        private readonly Button _Export = new Button();
        private readonly TextBlock _Status = new TextBlock { Margin = new Thickness(12, 6, 0, 6), TextWrapping = TextWrapping.Wrap };
        private readonly TabControl _Tabs = new TabControl { Margin = new Thickness(0, 8, 0, 0) };
        private readonly List<UniformGrid> _Grids = new List<UniformGrid>();
        private LibraryStatistics _Snapshot;
        private string _SnapshotScope;
        private int _Version;

        public Window_Statistics(long dbId)
        {
            Title = L("Statistics");
            Width = 1130; Height = 790; MinWidth = 700; MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "Window.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");
            SetResourceReference(FontSizeProperty, "GlobalFontSize");
            _Tabs.SetResourceReference(BackgroundProperty, "Window.Background");
            _Tabs.SetResourceReference(ForegroundProperty, "Window.Foreground");
            var root = new DockPanel { Margin = new Thickness(16) };
            var header = new StackPanel();
            DockPanel.SetDock(header, Dock.Top);
            var tools = new StackPanel { Orientation = Orientation.Horizontal };
            tools.Children.Add(new TextBlock { Text = L("StatLibraryScope"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            tools.Children.Add(_Library);
            _Refresh.Content = L("Refresh"); _Export.Content = L("StatExport");
            foreach (Button button in new[] { _Refresh, _Export }) {
                button.Margin = new Thickness(10, 0, 0, 0); button.Padding = new Thickness(12, 6, 12, 6);
                tools.Children.Add(button);
            }
            header.Children.Add(tools);
            header.Children.Add(_Status);
            header.Children.Add(new TextBlock { Text = L("StatDataHint"), TextWrapping = TextWrapping.Wrap, Opacity = .8 });
            root.Children.Add(header); root.Children.Add(_Tabs); Content = root;
            var choices = new List<LibraryChoice> { new LibraryChoice { Name = L("StatAllLibraries"), DbId = null } };
            foreach (var database in MapperManager.appDatabaseMapper.SelectList() ?? new List<Jvedio.Entity.AppDatabase>())
                if ((int)database.DataType == 0) choices.Add(new LibraryChoice { DbId = database.DBId, Name = database.Name });
            _Library.ItemsSource = choices;
            _Library.SelectedItem = choices.FirstOrDefault(choice => choice.DbId == dbId) ?? choices[0];
            _Library.SelectionChanged += async (s, e) => { if (IsLoaded) await LoadAsync(); };
            _Refresh.Click += async (s, e) => await LoadAsync();
            _Export.Click += (s, e) => Export();
            SizeChanged += (s, e) => { foreach (UniformGrid grid in _Grids) grid.Columns = ActualWidth >= 980 ? 2 : 1; };
            Loaded += async (s, e) => await LoadAsync();
            Closed += (s, e) => _Version++;
        }
        private static string L(string key) => LangManager.GetValueByKey(key);
        private static ControlTemplate TabHeaderTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border)) { Name = "TabChrome" };
            border.SetValue(Border.PaddingProperty, new Thickness(14, 8, 14, 8));
            border.SetValue(Border.MarginProperty, new Thickness(0, 0, 5, 0));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4, 4, 0, 0));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(35,128,128,128)));
            var caption = new FrameworkElementFactory(typeof(ContentPresenter)) { Name = "TabCaption" };
            caption.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            border.AppendChild(caption);
            var template = new ControlTemplate(typeof(TabItem)) { VisualTree = border };
            var selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(64,158,255)), "TabChrome"));
            selected.Setters.Add(new Setter(System.Windows.Documents.TextElement.ForegroundProperty, Brushes.White, "TabCaption"));
            selected.Setters.Add(new Setter(System.Windows.Documents.TextElement.FontWeightProperty, FontWeights.Bold, "TabCaption"));
            template.Triggers.Add(selected);
            return template;
        }
        public async Task LoadAsync()
        {
            int version = ++_Version;
            long? scope = (_Library.SelectedItem as LibraryChoice)?.DbId;
            string scopeName = (_Library.SelectedItem as LibraryChoice)?.Name ?? "";
            _Library.IsEnabled = _Refresh.IsEnabled = _Export.IsEnabled = false;
            _Status.Text = L("HealthWorking");
            try {
                var snapshot = await Task.Run(() => LibraryStatisticsService.Collect(scope));
                if (version != _Version) return;
                _Snapshot = snapshot;
                _SnapshotScope = scopeName;
                Build(snapshot);
                _Status.Text = string.Format(L("StatUpdated"), snapshot.CollectedAt.ToString("HH:mm:ss"), snapshot.Metrics["Videos"].ToString("N0"));
            } catch (Exception ex) {
                App.Logger.Error(ex);
                if (version == _Version) { _Snapshot = null; _Tabs.Items.Clear(); _Status.Text = ex.Message; }
            }
            finally {
                if (version == _Version) {
                    _Library.IsEnabled = _Refresh.IsEnabled = true;
                    _Export.IsEnabled = _Snapshot != null;
                }
            }
        }
        private void Build(LibraryStatistics data)
        {
            int selected = Math.Max(0, _Tabs.SelectedIndex);
            _Tabs.Items.Clear(); _Grids.Clear();
            foreach (string group in new[] { "StatOverview", "StatHistory", "StatFiles", "StatPreferences" }) {
                var panel = new StackPanel { Margin = new Thickness(8) };
                if (group == "StatOverview") {
                    panel.Children.Add(Summary(data));
                    panel.Children.Add(new Expander { Header = L("StatMoreMetrics"), Content = Summary(data, true),
                        Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 12) });
                }
                if (group == "StatFiles") {
                    panel.Children.Add(new TextBlock { Text = string.Format(L("StatKnownDurationHint"),
                        data.Metrics["DurationKnown"].ToString("N0"), data.Metrics["Videos"].ToString("N0")),
                        Margin = new Thickness(0, 8, 0, 14), TextWrapping = TextWrapping.Wrap, Opacity = .8 });
                }
                var grid = new UniformGrid { Columns = ActualWidth >= 980 ? 2 : 1 };
                _Grids.Add(grid);
                foreach (StatisticChart chart in data.Charts.Where(chart => chart.GroupKey == group)) {
                    var card = new StackPanel { Margin = new Thickness(14) };
                    var title = new TextBlock { Text = L(chart.TitleKey), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) };
                    title.SetResourceReference(TextBlock.FontSizeProperty, "GlobalFontSize16");
                    card.Children.Add(title);
                    card.Children.Add(new StatisticsChart(chart, data.Metrics["Videos"]));
                    grid.Children.Add(new Border { Child = card, Margin = new Thickness(0, 0, 12, 12),
                        CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromArgb(16,128,128,128)) });
                }
                panel.Children.Add(grid);
                var viewer = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                viewer.SetResourceReference(BackgroundProperty, "Window.Background");
                var tab = new TabItem { Header = L(group), Content = viewer };
                tab.Template = TabHeaderTemplate();
                tab.SetResourceReference(ForegroundProperty, "Window.Foreground");
                tab.SetResourceReference(BackgroundProperty, "Window.Background");
                _Tabs.Items.Add(tab);
            }
            _Tabs.SelectedIndex = Math.Min(selected, _Tabs.Items.Count - 1);
        }
        private WrapPanel Summary(LibraryStatistics data, bool additional = false)
        {
            var panel = new WrapPanel { Margin = new Thickness(0, 6, 0, 14) };
            foreach (string metric in additional
                ? new[] { "AvgGrade", "AvgSize", "AvgDuration", "Playable", "Subtitles", "Translated", "Actors", "Labels" }
                : new[] { "Videos", "Favorites", "Watched", "Unwatched", "Size", "Duration", "PlayCount", "Imported30" }) {
                double value = data.Metrics[metric];
                string text = metric == "Size" || metric == "AvgSize" ? (value / 1073741824d).ToString("N2") + " GB"
                    : metric == "Duration" ? (value / 3600d).ToString("N1") + " h"
                    : metric == "AvgDuration" ? (value / 60d).ToString("N1") + " min"
                    : metric == "AvgGrade" ? (data.Metrics["Favorites"] == 0 ? "—" : value.ToString("F2") + " / 5")
                    : value.ToString("N0");
                var stack = new StackPanel();
                var number = new TextBlock { Text = text, FontWeight = FontWeights.Bold };
                if (additional) number.Foreground = Brushes.White;
                number.SetResourceReference(TextBlock.FontSizeProperty, "GlobalFontSize24");
                stack.Children.Add(number);
                var caption = new TextBlock { Text = L(MetricKey(metric)), Margin = new Thickness(0, 8, 0, 0), Opacity = .8, TextWrapping = TextWrapping.Wrap };
                if (additional) caption.Foreground = Brushes.White;
                stack.Children.Add(caption);
                panel.Children.Add(new Border { Width = 220, MinHeight = 94, Child = stack, Padding = new Thickness(14),
                    Margin = new Thickness(0, 0, 10, 10), CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromArgb(23,128,128,128)) });
            }
            return panel;
        }
        private static string MetricKey(string metric)
        {
            if (metric == "Videos") return "StatVideos";
            if (metric == "Favorites") return "StatFavorites";
            if (metric == "Size") return "StatTotalSize";
            if (metric == "Duration") return "StatDurationTotal";
            return "Stat" + metric;
        }
        private static string Csv(string text) => "\"" + (text ?? "").Replace("\"", "\"\"") + "\"";
        private void Export()
        {
            if (_Snapshot == null) return;
            var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "Jvedio-statistics-" + DateTime.Now.ToString("yyyyMMdd") + ".csv" };
            if (dialog.ShowDialog(this) != true) return;
            var output = new StringBuilder();
            output.AppendLine(Csv(L("StatSection")) + "," + Csv(L("StatMetric")) + "," + Csv(L("StatValue")));
            output.AppendLine(Csv(L("StatOverview")) + "," + Csv(L("StatLibraryScope")) + "," + Csv(_SnapshotScope));
            foreach (var metric in _Snapshot.Metrics)
                output.AppendLine(Csv(L("StatOverview")) + "," + Csv(L(MetricKey(metric.Key))) + "," + Csv(metric.Value.ToString(CultureInfo.InvariantCulture)));
            foreach (var chart in _Snapshot.Charts)
                foreach (var item in chart.Items)
                    output.AppendLine(Csv(L(chart.TitleKey)) + "," + Csv(item.Localized ? L(item.Label) : item.Label) + "," + Csv(item.Count.ToString(CultureInfo.InvariantCulture)));
            try { File.WriteAllText(dialog.FileName, output.ToString(), new UTF8Encoding(true)); }
            catch (Exception ex) { App.Logger.Error(ex); _Status.Text = ex.Message; }
        }
    }
}
