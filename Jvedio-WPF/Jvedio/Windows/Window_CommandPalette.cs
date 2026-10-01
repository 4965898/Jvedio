using LangManager = SuperControls.Style.LangManager;
using SuperUtils.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static Jvedio.MapperManager;

namespace Jvedio.Windows
{
    /// <summary>
    /// Ctrl+K 命令面板：快速搜索影片（VID/标题/中文标题，空格分词）并直达，
    /// 附带少量常用命令（设置/统计/资料体检/数据目录）。
    /// </summary>
    public sealed class Window_CommandPalette : Window
    {
        private sealed class PaletteItem
        {
            public string Kind;
            public string Title;
            public string Subtitle;
            public long DataID;
            public Action Command;
        }

        private readonly Main _Main;
        private readonly TextBox _Input = new TextBox {
            Margin = new Thickness(12, 12, 12, 6),
            Padding = new Thickness(6, 6, 6, 6),
            FontSize = 16,
        };
        private readonly ListBox _Results = new ListBox {
            Margin = new Thickness(12, 0, 12, 12),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
        };
        private readonly TextBlock _Hint = new TextBlock {
            Margin = new Thickness(14, 0, 14, 10),
            Opacity = 0.6,
        };
        private List<PaletteItem> _Items = new List<PaletteItem>();

        public Window_CommandPalette(Main main)
        {
            _Main = main;
            Title = "Ctrl+K";
            Width = 680;
            Height = 440;
            MinWidth = 520;
            MinHeight = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            SetResourceReference(BackgroundProperty, "Window.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _Input.KeyUp += OnInputKeyUp;
            Grid.SetRow(_Input, 0);
            _Results.PreviewMouseDoubleClick += (s, e) => ActivateSelected();
            Grid.SetRow(_Results, 1);
            _Hint.Text = LangManager.GetValueByKey("PaletteHint");
            Grid.SetRow(_Hint, 2);
            root.Children.Add(_Input);
            root.Children.Add(_Results);
            root.Children.Add(_Hint);
            Content = root;

            KeyDown += (s, e) => {
                if (e.Key == Key.Escape)
                    Close();
            };
            Loaded += (s, e) => {
                _Input.Focus();
                RunQuery(string.Empty);
            };
        }

        private void OnInputKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && _Results.Items.Count > 0) {
                _Results.SelectedIndex = Math.Max(0, _Results.SelectedIndex + (_Results.SelectedIndex < 0 ? 1 : 1));
                if (_Results.SelectedItem != null)
                    _Results.ScrollIntoView(_Results.SelectedItem);
                _Results.Focus();
                e.Handled = true;
            } else if (e.Key == Key.Up && _Results.Items.Count > 0) {
                _Results.SelectedIndex = Math.Max(0, _Results.SelectedIndex - 1);
                if (_Results.SelectedItem != null)
                    _Results.ScrollIntoView(_Results.SelectedItem);
                _Results.Focus();
                e.Handled = true;
            } else if (e.Key == Key.Enter) {
                ActivateSelected();
                e.Handled = true;
            } else {
                RunQuery(_Input.Text);
            }
        }

        private void ActivateSelected()
        {
            if (!(_Results.SelectedItem is ListBoxItem item) || !(item.Tag is PaletteItem entry))
                return;
            Close();
            if (entry.DataID > 0) {
                _Main.OpenVideoDetails(entry.DataID);
            } else {
                entry.Command?.Invoke();
            }
        }

        private void RunQuery(string query)
        {
            _Results.Items.Clear();
            _Items = new List<PaletteItem>();
            string keyword = query?.Trim() ?? string.Empty;
            var lower = keyword.ToLower();
            var tokens = keyword.Split(new[] { ' ', '\t', '　' }, StringSplitOptions.RemoveEmptyEntries);

            // 命令（有关键词时按包含过滤）
            foreach (PaletteItem cmd in BuildCommands()) {
                if (tokens.Length == 0 || cmd.Title.ToLower().Contains(lower))
                    _Items.Add(cmd);
                if (_Items.Count >= 8)
                    break;
            }

            // 影片：空格分词 AND 匹配 VID/Title/TitleCN，最多 10 条
            if (tokens.Length > 0 && _Items.Count < 20) {
                List<PaletteItem> videos = null;
                try {
                    videos = Task.Run(() => SearchVideos(tokens)).GetAwaiter().GetResult();
                } catch (Exception ex) {
                    App.Logger.Error(ex);
                }
                if (videos != null)
                    _Items.AddRange(videos);
            }

            foreach (PaletteItem entry in _Items) {
                var item = new ListBoxItem {
                    Tag = entry,
                    Padding = new Thickness(8, 5, 8, 5),
                };
                var stack = new StackPanel();
                var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
                var kindBadge = new Border {
                    Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(5, 1, 5, 1),
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = new TextBlock { Text = entry.Kind, FontSize = 11 },
                };
                titleRow.Children.Add(kindBadge);
                titleRow.Children.Add(new TextBlock { Text = entry.Title, FontWeight = FontWeights.Bold });
                stack.Children.Add(titleRow);
                if (!string.IsNullOrEmpty(entry.Subtitle))
                    stack.Children.Add(new TextBlock { Text = entry.Subtitle, Opacity = 0.7, TextTrimming = TextTrimming.CharacterEllipsis });
                item.Content = stack;
                _Results.Items.Add(item);
            }
            if (_Results.Items.Count > 0)
                _Results.SelectedIndex = 0;
        }

        private List<PaletteItem> BuildCommands()
        {
            return new List<PaletteItem>() {
                new PaletteItem() { Kind = LangManager.GetValueByKey("Settings"), Title = LangManager.GetValueByKey("Settings"), Command = () => new Window_Settings().Show() },
                new PaletteItem() { Kind = LangManager.GetValueByKey("Statistics"), Title = LangManager.GetValueByKey("Statistics"), Command = () => new Window_Statistics(ConfigManager.Main.CurrentDBId) { Owner = _Main }.Show() },
                new PaletteItem() { Kind = LangManager.GetValueByKey("LibraryHealth"), Title = LangManager.GetValueByKey("LibraryHealth"), Command = () => new Window_LibraryHealth(ConfigManager.Main.CurrentDBId) { Owner = _Main }.ShowDialog() },
                new PaletteItem() { Kind = LangManager.GetValueByKey("OpenPath"), Title = LangManager.GetValueByKey("OpenDataPath"), Command = () => FileHelper.TryOpenPath(Jvedio.Core.Global.PathManager.CurrentUserFolder) },
            };
        }

        private List<PaletteItem> SearchVideos(string[] tokens)
        {
            var wrapperParts = tokens.Select(t => $"(ifnull(metadata_video.VID,'') like '%{t.Replace("'", "")}%' " +
                $"or ifnull(metadata.Title,'') like '%{t.Replace("'", "")}%' " +
                $"or ifnull(metadata.TitleCN,'') like '%{t.Replace("'", "")}%')");
            string where = "where metadata.DBId=" + ConfigManager.Main.CurrentDBId + " and metadata.DataType=0 and " +
                string.Join(" and ", wrapperParts);
            string sql = "select metadata.DataID, metadata_video.VID, metadata.Title, metadata.TitleCN as TitleCN from metadata " +
                "join metadata_video on metadata_video.DataID=metadata.DataID " + where + " limit 10";
            List<Dictionary<string, object>> rows = metaDataMapper.Select(sql);
            var items = new List<PaletteItem>();
            if (rows == null)
                return items;
            foreach (Dictionary<string, object> row in rows) {
                long dataID = row.TryGetValue("DataID", out object id) ? Convert.ToInt64(id) : 0;
                string vid = row.TryGetValue("VID", out object v) ? v?.ToString() ?? "" : "";
                string title = row.TryGetValue("Title", out object t) ? t?.ToString() ?? "" : "";
                string titleCn = row.TryGetValue("TitleCN", out object tc) ? tc?.ToString() ?? "" : "";
                items.Add(new PaletteItem() {
                    Kind = LangManager.GetValueByKey("Movie"),
                    DataID = dataID,
                    Title = string.IsNullOrEmpty(vid) ? title : vid,
                    Subtitle = string.IsNullOrEmpty(titleCn) ? title : titleCn,
                });
            }
            return items;
        }
    }
}
