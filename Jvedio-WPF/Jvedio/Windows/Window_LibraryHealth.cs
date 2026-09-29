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
            Content = root;
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
    }
}
