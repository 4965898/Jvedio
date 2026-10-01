using Jvedio.Core.Crawler;
using Jvedio.Core.Tasks;
using Jvedio.Core.Config;
using LangManager = SuperControls.Style.LangManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using static Jvedio.App;
using static Jvedio.MapperManager;

namespace Jvedio.Windows
{
    /// <summary>Actionable review of the current library's missing files and metadata.</summary>
    public sealed class Window_LibraryHealth : Window
    {
        public sealed class HealthIssue
        {
            public string Issue { get; set; }
            public string VID { get; set; }
            public string Title { get; set; }
            public string Path { get; set; }
        }

        private readonly long _DbId;
        private readonly ComboBox _Category = new ComboBox { Width = 160, Margin = new Thickness(5) };
        private readonly TextBlock _Status = new TextBlock { Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        private readonly DataGrid _Grid = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false,
            CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Extended,
            EnableRowVirtualization = true, EnableColumnVirtualization = true };
        private List<HealthIssue> _Issues = new List<HealthIssue>();
        private bool _Busy;
        private readonly string _OfflineLabel;
        private readonly string _MissingFileLabel;
        private readonly string _MissingPosterLabel;
        private readonly string _MissingSubtitleLabel;
        private readonly string _MissingTitleLabel;

        public Window_LibraryHealth(long dbId)
        {
            _DbId = dbId;
            _OfflineLabel = LangManager.GetValueByKey("HealthOffline");
            _MissingFileLabel = LangManager.GetValueByKey("HealthMissingFile");
            _MissingPosterLabel = LangManager.GetValueByKey("HealthMissingPoster");
            _MissingSubtitleLabel = LangManager.GetValueByKey("HealthMissingSubtitle");
            _MissingTitleLabel = LangManager.GetValueByKey("HealthMissingTitle");
            Title = LangManager.GetValueByKey("LibraryHealth");
            Width = 940;
            Height = 610;
            MinWidth = 650;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "Window.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");

            var root = new DockPanel { Margin = new Thickness(12) };
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(toolbar, Dock.Top);
            root.Children.Add(toolbar);
            _Category.Items.Add(LangManager.GetValueByKey("HealthAll"));
            _Category.Items.Add(_OfflineLabel);
            _Category.Items.Add(_MissingFileLabel);
            _Category.Items.Add(_MissingPosterLabel);
            _Category.Items.Add(_MissingSubtitleLabel);
            _Category.Items.Add(_MissingTitleLabel);
            _Category.SelectedIndex = 0;
            _Category.SelectionChanged += (s, e) => ApplyCategory();
            toolbar.Children.Add(_Category);
            var refresh = new Button { Content = LangManager.GetValueByKey("HealthRefresh"), Margin = new Thickness(5), MinWidth = 88 };
            refresh.Click += async (s, e) => await RefreshAsync(true);
            toolbar.Children.Add(refresh);
            var copy = new Button { Content = LangManager.GetValueByKey("HealthCopy"), Margin = new Thickness(5), MinWidth = 108 };
            copy.Click += (s, e) => Clipboard.SetText(string.Join(Environment.NewLine,
                _Grid.Items.OfType<HealthIssue>().Select(x => x.Issue + "\t" + x.VID + "\t" + x.Path)));
            toolbar.Children.Add(copy);
            toolbar.Children.Add(_Status);

            AddColumn(LangManager.GetValueByKey("HealthIssue"), "Issue", 135);
            AddColumn(LangManager.GetValueByKey("ID"), "VID", 125);
            _Grid.Columns[1].Header = new TextBlock {
                Text = LangManager.GetValueByKey("ID"),
                ToolTip = LangManager.GetValueByKey("HealthVIDCopyTip")
            };
            AddColumn(LangManager.GetValueByKey("Title"), "Title", 220);
            AddColumn(LangManager.GetValueByKey("HealthPath"), "Path", 390);
            _Grid.MouseDoubleClick += OnGridDoubleClick;
            root.Children.Add(_Grid);

            // 两个页签：资料体检（原有）+ 刮削源体检（逐源诊断卡片）
            TabControl tabControl = new TabControl();
            Style flatTab = App.Current.TryFindResource("FlatTabControl") as Style;
            if (flatTab != null)
                tabControl.Style = flatTab;
            Style flatTabItem = App.Current.TryFindResource("FlatTabItem") as Style;
            TabItem healthTab = new TabItem { Header = LangManager.GetValueByKey("LibraryHealth"), Content = root };
            TabItem crawlerTab = new TabItem { Header = LangManager.GetValueByKey("CrawlerCheck"), Content = BuildCrawlerCheckTab() };
            if (flatTabItem != null) {
                healthTab.Style = flatTabItem;
                crawlerTab.Style = flatTabItem;
            }
            tabControl.Items.Add(healthTab);
            tabControl.Items.Add(crawlerTab);
            Content = tabControl;
            Loaded += async (s, e) => await RefreshAsync(true);
        }

        private void AddColumn(string title, string field, double width)
        {
            _Grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(field), Width = width });
        }

        private static string Value(Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object value) ? value?.ToString() ?? string.Empty : string.Empty;
        }

        private async Task RefreshAsync(bool verify)
        {
            if (_Busy) return;
            _Busy = true;
            _Status.Text = LangManager.GetValueByKey("HealthWorking");
            try {
                if (verify && !await DataIndexManager.RebuildAsync())
                    throw new InvalidOperationException(LangManager.GetValueByKey("HealthVerifyFailed"));
                List<HealthIssue> issues = await Task.Run(() => CollectIssues());
                _Issues = issues;
                ApplyCategory();
                _Status.Text = string.Format(LangManager.GetValueByKey("HealthComplete"), issues.Count);
            } catch (Exception ex) {
                Logger.Error(ex);
                _Status.Text = string.Format(LangManager.GetValueByKey("HealthFailed"), ex.Message);
            } finally {
                _Busy = false;
            }
        }

        private List<HealthIssue> CollectIssues()
        {
            string sql = "select metadata.DataID, metadata.Path, metadata.Title, metadata.PathExist, " +
                "metadata.SubtitleExist, metadata_video.VID, " +
                "exists(select 1 from common_picture_exist p where p.DataID=metadata.DataID " +
                "and p.PathType=" + ConfigManager.Settings.PicPathMode + " and p.ImageType=1 and p.Exist=1) as HasPoster " +
                "from metadata join metadata_video on metadata_video.DataID=metadata.DataID " +
                "where metadata.DBId=" + _DbId + " and metadata.DataType=0";
            List<Dictionary<string, object>> rows = metaDataMapper.Select(sql);
            var issues = new List<HealthIssue>();
            foreach (var row in rows ?? new List<Dictionary<string, object>>()) {
                string path = Value(row, "Path");
                string title = Value(row, "Title");
                string vid = Value(row, "VID");
                Action<string> add = issue => issues.Add(new HealthIssue { Issue = issue, VID = vid, Title = title, Path = path });
                if (Value(row, "PathExist") != "1") {
                    string root = string.Empty;
                    try { root = Path.GetPathRoot(path); } catch { }
                    add(!string.IsNullOrEmpty(root) && !Directory.Exists(root)
                        ? _OfflineLabel : _MissingFileLabel);
                }
                if (Value(row, "HasPoster") != "1") add(_MissingPosterLabel);
                if (Value(row, "SubtitleExist") != "1") add(_MissingSubtitleLabel);
                if (string.IsNullOrWhiteSpace(title)) add(_MissingTitleLabel);
            }
            return issues;
        }

        private void ApplyCategory()
        {
            string category = _Category.SelectedItem as string;
            _Grid.ItemsSource = category == null || category == LangManager.GetValueByKey("HealthAll")
                ? _Issues : _Issues.Where(x => x.Issue == category).ToList();
        }

        private void OpenSelectedPath()
        {
            if (!(_Grid.SelectedItem is HealthIssue selected) || string.IsNullOrWhiteSpace(selected.Path)) return;
            try {
                if (File.Exists(selected.Path))
                    Process.Start("explorer.exe", "/select,\"" + selected.Path + "\"");
                else if (Directory.Exists(Path.GetDirectoryName(selected.Path)))
                    Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(selected.Path) + "\"");
            } catch (Exception ex) { Logger.Error(ex); }
        }

        private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null && !(source is DataGridCell)) {
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
            if (!(source is DataGridCell cell)) return;
            if (cell.Column == _Grid.Columns[1]) {
                if (_Grid.SelectedItem is HealthIssue issue && !string.IsNullOrWhiteSpace(issue.VID)) {
                    try {
                        Clipboard.SetText(issue.VID);
                        _Status.Text = string.Format(LangManager.GetValueByKey("HealthVIDCopied"), issue.VID);
                    } catch (Exception ex) {
                        Logger.Error(ex);
                        _Status.Text = LangManager.GetValueByKey("HealthCopyFailed");
                    }
                }
                e.Handled = true;
                return;
            }
            OpenSelectedPath();
        }

        #region "刮削源体检：逐源诊断卡片（代理 → 镜像可达性 → 人机验证 → Cookie）+ 修复提示"

        private sealed class CheckCard
        {
            public string Title;
            public string StateText;
            public OnlineSiteState State;   // Unknown=中性提示卡
            public string Detail;
            public string Hint;
        }

        private readonly StackPanel _CrawlerCardPanel = new StackPanel();
        private readonly TextBlock _CrawlerStatus = new TextBlock { Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        private bool _CrawlerChecking;

        /// <summary>维护中的爬虫默认站点（源码重建的 Bus2/Db2）；fc2/library 为封闭 DLL 无默认地址</summary>
        private static readonly Dictionary<string, string> DefaultCrawlerUrls = new Dictionary<string, string>() {
            { "bus", "https://www.busjav.bond" },
            { "db", "https://javdb.com" },
        };

        private UIElement BuildCrawlerCheckTab()
        {
            DockPanel root = new DockPanel { Margin = new Thickness(12) };
            StackPanel toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(toolbar, Dock.Top);
            root.Children.Add(toolbar);
            Button start = new Button {
                Content = LangManager.GetValueByKey("CrawlerCheckStart"),
                Margin = new Thickness(5),
                MinWidth = 88,
            };
            start.Click += async (s, e) => await RunCrawlerCheckAsync();
            toolbar.Children.Add(start);
            toolbar.Children.Add(_CrawlerStatus);
            ScrollViewer viewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _CrawlerCardPanel };
            root.Children.Add(viewer);
            return root;
        }

        private async System.Threading.Tasks.Task RunCrawlerCheckAsync()
        {
            if (_CrawlerChecking)
                return;
            _CrawlerChecking = true;
            _CrawlerStatus.Text = LangManager.GetValueByKey("CrawlerCheckWorking");
            _CrawlerCardPanel.Children.Clear();
            try {
                List<CheckCard> cards = await System.Threading.Tasks.Task.Run(() => CollectCrawlerCards());
                RenderCards(cards);
                _CrawlerStatus.Text = string.Format(LangManager.GetValueByKey("CrawlerCheckDone"), cards.Count);
            } catch (Exception ex) {
                Logger.Error(ex);
                _CrawlerStatus.Text = string.Format(LangManager.GetValueByKey("HealthFailed"), ex.Message);
            } finally {
                _CrawlerChecking = false;
            }
        }

        private List<CheckCard> CollectCrawlerCards()
        {
            var cards = new List<CheckCard>();

            // 1. 代理状态（决定所有探测与刮削的出口）
            long proxyMode = ConfigManager.ProxyConfig?.ProxyMode ?? 0;
            string proxyText = proxyMode == 2 ? LangManager.GetValueByKey("CrawlerProxyCustom")
                : proxyMode == 1 ? LangManager.GetValueByKey("CrawlerProxySystem")
                : LangManager.GetValueByKey("CrawlerProxyNone");
            cards.Add(new CheckCard() {
                Title = LangManager.GetValueByKey("CrawlerProxyCard"),
                StateText = proxyText,
                State = OnlineSiteState.Unknown,
                Detail = proxyMode == 2 ? $"{ConfigManager.ProxyConfig.Server}:{ConfigManager.ProxyConfig.Port}" : string.Empty,
                Hint = proxyMode == 0 ? LangManager.GetValueByKey("CrawlerProxyNoneHint") : string.Empty,
            });

            // 2. 逐爬虫源探测
            var servers = ConfigManager.ServerConfig?.CrawlerServers;
            if (servers == null || servers.Count == 0) {
                cards.Add(new CheckCard() {
                    Title = LangManager.GetValueByKey("CrawlerNoSource"),
                    StateText = string.Empty,
                    State = OnlineSiteState.Unknown,
                    Detail = string.Empty,
                    Hint = LangManager.GetValueByKey("CrawlerNoSourceHint"),
                });
                return cards;
            }

            foreach (var server in servers) {
                string name = CrawlerName(server.PluginID);
                if (!server.Enabled) {
                    cards.Add(new CheckCard() {
                        Title = name,
                        StateText = LangManager.GetValueByKey("CrawlerSourceDisabled"),
                        State = OnlineSiteState.Unknown,
                        Detail = string.Empty,
                        Hint = LangManager.GetValueByKey("CrawlerSourceDisabledHint"),
                    });
                    continue;
                }
                string url = string.IsNullOrEmpty(server.Url) && DefaultCrawlerUrls.TryGetValue(server.PluginID, out string def) ? def : server.Url;
                if (string.IsNullOrEmpty(url)) {
                    cards.Add(new CheckCard() {
                        Title = name,
                        StateText = string.Empty,
                        State = OnlineSiteState.Unknown,
                        Detail = string.Empty,
                        Hint = LangManager.GetValueByKey("CrawlerNoUrlHint"),
                    });
                    continue;
                }
                OnlineSiteState state = OnlineSiteStatus.ProbeUrl(url).GetAwaiter().GetResult();
                bool hasCookie = !string.IsNullOrEmpty(server.Cookies);
                cards.Add(new CheckCard() {
                    Title = name,
                    StateText = StateText(state),
                    State = state,
                    Detail = $"{url}  |  {(hasCookie ? LangManager.GetValueByKey("CrawlerCookieSet") : LangManager.GetValueByKey("CrawlerCookieNotSet"))}",
                    Hint = HintFor(state, hasCookie),
                });
            }
            return cards;
        }

        private static string CrawlerName(string pluginId)
        {
            switch (pluginId) {
                case "bus": return "Bus (JavBus)";
                case "db": return "DB (JavDB)";
                case "fc2": return "FC2";
                case "library": return "JavLibrary";
                default: return string.IsNullOrEmpty(pluginId) ? "?" : pluginId;
            }
        }

        private static string StateText(OnlineSiteState state)
        {
            switch (state) {
                case OnlineSiteState.Ok: return LangManager.GetValueByKey("CrawlerStateOk");
                case OnlineSiteState.MaybeBlocked: return LangManager.GetValueByKey("CrawlerStateBlocked");
                case OnlineSiteState.Fail: return LangManager.GetValueByKey("CrawlerStateFail");
                default: return string.Empty;
            }
        }

        private static string HintFor(OnlineSiteState state, bool hasCookie)
        {
            switch (state) {
                case OnlineSiteState.Ok:
                    return hasCookie ? string.Empty : LangManager.GetValueByKey("CrawlerHintOkNoCookie");
                case OnlineSiteState.MaybeBlocked:
                    return LangManager.GetValueByKey("CrawlerHintBlocked");
                case OnlineSiteState.Fail:
                    return LangManager.GetValueByKey("CrawlerHintFail");
                default:
                    return string.Empty;
            }
        }

        private void RenderCards(List<CheckCard> cards)
        {
            _CrawlerCardPanel.Children.Clear();
            foreach (CheckCard card in cards) {
                Color stripe = card.State switch {
                    OnlineSiteState.Ok => (Color)ColorConverter.ConvertFromString("#67C23A"),
                    OnlineSiteState.MaybeBlocked => (Color)ColorConverter.ConvertFromString("#E6A23C"),
                    OnlineSiteState.Fail => (Color)ColorConverter.ConvertFromString("#F56C6C"),
                    _ => Colors.Gray,
                };
                Border border = new Border {
                    Margin = new Thickness(2, 4, 2, 4),
                    Padding = new Thickness(10, 8, 10, 8),
                    CornerRadius = new CornerRadius(4),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
                    BorderThickness = new Thickness(1),
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var stripeRect = new Border { Background = new SolidColorBrush(stripe), CornerRadius = new CornerRadius(2) };
                Grid.SetColumn(stripeRect, 0);
                grid.Children.Add(stripeRect);
                var textPanel = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
                Grid.SetColumn(textPanel, 2);
                var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
                titleRow.Children.Add(new TextBlock { Text = card.Title, FontWeight = FontWeights.Bold });
                if (!string.IsNullOrEmpty(card.StateText))
                    titleRow.Children.Add(new TextBlock { Text = $"  {card.StateText}", Foreground = new SolidColorBrush(stripe), FontWeight = FontWeights.Bold });
                textPanel.Children.Add(titleRow);
                if (!string.IsNullOrEmpty(card.Detail))
                    textPanel.Children.Add(new TextBlock { Text = card.Detail, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
                if (!string.IsNullOrEmpty(card.Hint))
                    textPanel.Children.Add(new TextBlock { Text = card.Hint, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
                grid.Children.Add(textPanel);
                border.Child = grid;
                _CrawlerCardPanel.Children.Add(border);
            }
        }

        #endregion
    }
}
