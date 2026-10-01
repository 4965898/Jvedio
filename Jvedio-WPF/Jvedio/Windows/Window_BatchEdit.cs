using Jvedio.Entity;
using Jvedio.Entity.CommonSQL;
using SuperControls.Style;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Jvedio.MapperManager;

namespace Jvedio.Windows
{
    /// <summary>
    /// 批量编辑：对选中的多部影片统一赋值（勾选要改的字段 + 填值）。
    /// 字段均为 metadata_video 单值列（制作商/发行商/导演/系列），另有追加标记。
    /// </summary>
    public sealed class Window_BatchEdit : Window
    {
        private sealed class FieldRow
        {
            public string FieldName;
            public string Label;
            public CheckBox Check = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            public TextBox Input = new TextBox { MinWidth = 260, Padding = new Thickness(4, 3, 4, 3) };
        }

        private readonly List<Video> _Videos;
        private readonly List<FieldRow> _Rows = new List<FieldRow>();
        private readonly TextBlock _Status = new TextBlock { Margin = new Thickness(8, 4, 0, 0), Opacity = 0.8 };

        /// <summary>勾选的标记（追加），null = 未勾选任何标记</summary>
        private readonly List<TagStamp> _CheckedTagStamps = new List<TagStamp>();
        private readonly ItemsControl _TagList = new ItemsControl { Margin = new Thickness(24, 0, 0, 0) };
        private readonly CheckBox _TagCheck = new CheckBox {
            Content = LangManager.GetValueByKey("AddTagStamp"),
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 4, 0, 0),
        };

        public Window_BatchEdit(List<Video> videos)
        {
            _Videos = videos ?? new List<Video>();
            Title = LangManager.GetValueByKey("BatchEdit");
            Width = 560;
            MinHeight = 420;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "Window.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");

            var root = new StackPanel { Margin = new Thickness(15) };

            var head = new TextBlock {
                Text = string.Format(LangManager.GetValueByKey("BatchEditTip"), _Videos.Count),
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
            };
            root.Children.Add(head);

            AddFieldRow("Studio", "Studio");
            AddFieldRow("Publisher", "Publisher");
            AddFieldRow("Director", "Director");
            AddFieldRow("Series", "Series");

            foreach (FieldRow row in _Rows) {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                line.Children.Add(row.Check);
                var label = new TextBlock {
                    Text = row.Label,
                    Width = 70,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                line.Children.Add(label);
                line.Children.Add(row.Input);
                row.Check.Checked += (s, e) => row.Input.IsEnabled = true;
                row.Check.Unchecked += (s, e) => row.Input.IsEnabled = false;
                row.Input.IsEnabled = false;
                root.Children.Add(line);
            }

            // 追加标记
            root.Children.Add(_TagCheck);
            _TagCheck.Checked += (s, e) => _TagList.IsEnabled = true;
            _TagCheck.Unchecked += (s, e) => _TagList.IsEnabled = false;
            _TagList.IsEnabled = false;
            var tagPanel = new StackPanel();
            foreach (TagStamp stamp in TagStamp.TagStamps ?? new List<TagStamp>()) {
                TagStamp local = stamp;
                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                var check = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                var label = new TextBlock { Text = local.TagName, VerticalAlignment = VerticalAlignment.Center };
                var preview = new Border {
                    Width = 14,
                    Height = 14,
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(6, 0, 0, 0),
                    Background = TryBrush(local.Background),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                check.Checked += (s, e) => { if (!_CheckedTagStamps.Contains(local)) _CheckedTagStamps.Add(local); };
                check.Unchecked += (s, e) => _CheckedTagStamps.Remove(local);
                line.Children.Add(check);
                line.Children.Add(preview);
                line.Children.Add(label);
                tagPanel.Children.Add(line);
            }
            _TagList.ItemsSource = new List<object> { tagPanel };
            root.Children.Add(_TagList);

            _Status.Text = string.Empty;
            root.Children.Add(_Status);

            var buttons = new StackPanel {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0),
            };
            var cancel = new Button { Content = LangManager.GetValueByKey("Cancel"), MinWidth = 90, Margin = new Thickness(0, 0, 10, 0) };
            cancel.Click += (s, e) => DialogResult = false;
            var apply = new Button {
                Content = LangManager.GetValueByKey("Apply"),
                MinWidth = 90,
                Style = (Style)App.Current.Resources["ButtonSuccess"],
            };
            apply.Click += async (s, e) => await ApplyAsync();
            buttons.Children.Add(cancel);
            buttons.Children.Add(apply);
            root.Children.Add(buttons);

            Content = new ScrollViewer { VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto, Content = root };
        }

        private static Brush TryBrush(string serialized)
        {
            try {
                if (!string.IsNullOrEmpty(serialized)) {
                    var converter = new BrushConverter();
                    return (Brush)converter.ConvertFromString(serialized);
                }
            } catch { }
            return Brushes.Gray;
        }

        private void AddFieldRow(string fieldName, string langKey)
        {
            string label = LangManager.GetValueByKey(langKey);
            if (string.IsNullOrEmpty(label) || label == langKey)
                label = fieldName;
            _Rows.Add(new FieldRow() { FieldName = fieldName, Label = label });
        }

        private System.Threading.Tasks.Task ApplyAsync()
        {
            var fields = _Rows.Where(r => (bool)r.Check.IsChecked && !string.IsNullOrWhiteSpace(r.Input.Text)).ToList();
            bool addTags = (bool)_TagCheck.IsChecked && _CheckedTagStamps.Count > 0;
            if (fields.Count == 0 && !addTags) {
                _Status.Text = LangManager.GetValueByKey("BatchEditNothingSelected");
                return System.Threading.Tasks.Task.CompletedTask;
            }

            int videoCount = _Videos.Count;
            return System.Threading.Tasks.Task.Run(() => {
                try {
                    foreach (FieldRow row in fields) {
                        foreach (Video video in _Videos) {
                            if (video == null || video.DataID <= 0)
                                continue;
                            videoMapper.UpdateFieldById(row.FieldName, row.Input.Text.Trim(), video.DataID);
                        }
                    }
                    if (addTags) {
                        foreach (TagStamp stamp in _CheckedTagStamps) {
                            var values = _Videos.Where(v => v != null && v.DataID > 0)
                                .Select(v => $"({v.DataID},{stamp.TagID})").ToList();
                            if (values.Count == 0)
                                continue;
                            string sql = $"insert or replace into metadata_to_tagstamp (DataID,TagID) values {string.Join(",", values)}";
                            tagStampMapper.ExecuteNonQuery(sql);
                        }
                    }
                    Dispatcher.Invoke(() => {
                        MessageNotify.Success($"{LangManager.GetValueByKey("Message_Success")}: {videoCount}");
                        DialogResult = true;
                    });
                } catch (Exception ex) {
                    App.Logger.Error(ex);
                    Dispatcher.Invoke(() => {
                        _Status.Text = ex.Message;
                    });
                }
            });
        }
    }
}
