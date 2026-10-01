using Jvedio.Core.Config;
using LangManager = SuperControls.Style.LangManager;
using SuperUtils.WPF.VisualTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Jvedio.MapperManager;

namespace Jvedio.Windows
{
    /// <summary>
    /// 统计仪表盘：库内影片/演员/容量总览 + 年份/类别/评分/片商/系列/演员分布条形图。
    /// 纯 WPF 原生控件绘制，不引入图表库（符合项目零第三方依赖风格）。
    /// </summary>
    public sealed class Window_Statistics : Window
    {
        /// <summary>
        /// 一条分布数据。注意必须用「属性」而非字段：WPF Binding 不支持公有字段，
        /// 上一版用字段导致图表全部渲染为空（用户实测"统计界面没有显示任何图表"）
        /// </summary>
        private sealed class BarItem
        {
            public string Label { get; set; }
            public long Count { get; set; }
            public double Ratio { get; set; }
        }

        private static readonly Color BAR_COLOR = (Color)ColorConverter.ConvertFromString("#409EFF");

        private readonly long _DbId;
        private readonly StackPanel _Panel = new StackPanel { Margin = new Thickness(15) };
        private readonly TextBlock _Status = new TextBlock { Margin = new Thickness(0, 8, 0, 0), Opacity = 0.8 };

        public Window_Statistics(long dbId)
        {
            _DbId = dbId;
            Title = LangManager.GetValueByKey("Statistics");
            Width = 860;
            Height = 640;
            MinWidth = 640;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "Window.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");

            ScrollViewer viewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _Panel };
            Content = viewer;
            Loaded += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            _Status.Text = LangManager.GetValueByKey("HealthWorking");
            try {
                Dictionary<string, object> summary = await Task.Run(() => CollectSummary());
                Dictionary<string, List<BarItem>> charts = await Task.Run(() => CollectCharts());
                _Panel.Children.Clear();
                BuildSummary(summary);
                BuildChart(LangManager.GetValueByKey("StatByYear"), GetChart(charts, "Year"));
                BuildChart(LangManager.GetValueByKey("StatByRating"), GetChart(charts, "Rating"));
                BuildChart(LangManager.GetValueByKey("StatByGenre"), GetChart(charts, "Genre"));
                BuildChart(LangManager.GetValueByKey("StatByStudio"), GetChart(charts, "Studio"));
                BuildChart(LangManager.GetValueByKey("StatBySeries"), GetChart(charts, "Series"));
                BuildChart(LangManager.GetValueByKey("StatByActor"), GetChart(charts, "Actor"));
                _Status.Text = LangManager.GetValueByKey("Message_Success");
            } catch (Exception ex) {
                App.Logger.Error(ex);
                _Status.Text = string.Format(LangManager.GetValueByKey("HealthFailed"), ex.Message);
            }
        }

        private static List<BarItem> GetChart(Dictionary<string, List<BarItem>> charts, string key)
        {
            return charts.TryGetValue(key, out List<BarItem> bars) ? bars : null;
        }

        private string BaseWhere()
        {
            return $"metadata.DBId={_DbId} and metadata.DataType=0";
        }

        private Dictionary<string, object> CollectSummary()
        {
            var result = new Dictionary<string, object>();
            string sql = "select " +
                "(select count(*) from metadata where " + BaseWhere() + ") as VideoCount, " +
                "(select count(*) from metadata where " + BaseWhere() + " and metadata.Grade>0) as FavoriteCount, " +
                "(select ifnull(sum(metadata.Size),0) from metadata where " + BaseWhere() + ") as TotalSize, " +
                "(select count(*) from metadata_to_actor mta join metadata on metadata.DataID=mta.DataID where " + BaseWhere() + ") as ActorRef, " +
                "(select ifnull(sum(metadata_video.Duration),0) from metadata join metadata_video on metadata_video.DataID=metadata.DataID where " + BaseWhere() + ") as TotalDuration";
            List<Dictionary<string, object>> rows = metaDataMapper.Select(sql);
            if (rows != null && rows.Count > 0) {
                foreach (string key in rows[0].Keys)
                    result[key] = rows[0][key];
            }
            return result;
        }

        private Dictionary<string, List<BarItem>> CollectCharts()
        {
            var charts = new Dictionary<string, List<BarItem>>();

            // 年份分布 top 15
            List<BarItem> years = ToBars(metaDataMapper.Select(
                "select substr(metadata.ReleaseDate,1,4) as Label, count(*) as Cnt from metadata where " + BaseWhere() +
                " and length(metadata.ReleaseDate)>=4 group by Label order by Cnt desc limit 15"), 15);
            charts["Year"] = years;

            // 评分分布 1~5
            List<BarItem> ratings = new List<BarItem>();
            List<Dictionary<string, object>> ratingRows = metaDataMapper.Select(
                "select cast(metadata.Grade as int) as Label, count(*) as Cnt from metadata where " + BaseWhere() +
                " and metadata.Grade>0 group by Label order by Label");
            if (ratingRows != null) {
                foreach (Dictionary<string, object> row in ratingRows)
                    ratings.Add(new BarItem() { Label = row["Label"] + " ★", Count = ToLong(row["Cnt"]), Ratio = 0 });
            }
            Normalize(ratings);
            charts["Rating"] = ratings;

            // 类别 top 15（多值列，内存拆分统计）
            var genreCounter = new Dictionary<string, long>();
            List<Dictionary<string, object>> genreRows = metaDataMapper.Select(
                "select metadata.Genre from metadata where " + BaseWhere() + " and ifnull(metadata.Genre,'')<>''");
            if (genreRows != null) {
                foreach (Dictionary<string, object> row in genreRows) {
                    foreach (string g in row["Genre"].ToString().Split(SuperUtils.Values.ConstValues.Separator)) {
                        if (string.IsNullOrWhiteSpace(g))
                            continue;
                        genreCounter[g] = genreCounter.TryGetValue(g, out long c) ? c + 1 : 1;
                    }
                }
            }
            charts["Genre"] = ToBars(genreCounter, 15);

            // 片商 / 系列 top 10（单值列）
            charts["Studio"] = ToBars(metaDataMapper.Select(
                "select metadata_video.Studio as Label, count(*) as Cnt from metadata join metadata_video on metadata_video.DataID=metadata.DataID where " + BaseWhere() +
                " and ifnull(metadata_video.Studio,'')<>'' group by Label order by Cnt desc limit 10"), 10);
            charts["Series"] = ToBars(metaDataMapper.Select(
                "select metadata_video.Series as Label, count(*) as Cnt from metadata join metadata_video on metadata_video.DataID=metadata.DataID where " + BaseWhere() +
                " and ifnull(metadata_video.Series,'')<>'' group by Label order by Cnt desc limit 10"), 10);

            // 演员 top 10
            charts["Actor"] = ToBars(metaDataMapper.Select(
                "select ifnull(actor_info.ActorName, 'Actor ' || mta.ActorID) as Label, count(*) as Cnt from metadata_to_actor mta " +
                "join metadata on metadata.DataID=mta.DataID left join actor_info on actor_info.ActorID=mta.ActorID " +
                "where " + BaseWhere() + " group by mta.ActorID order by Cnt desc limit 10"), 10);

            return charts;
        }

        private static long ToLong(object value)
        {
            return value == null ? 0 : (long)Convert.ChangeType(value, typeof(long));
        }

        private List<BarItem> ToBars(List<Dictionary<string, object>> rows, int limit)
        {
            var bars = new List<BarItem>();
            if (rows == null)
                return bars;
            foreach (Dictionary<string, object> row in rows) {
                string label = row.TryGetValue("Label", out object l) ? l?.ToString() ?? "" : "";
                if (string.IsNullOrEmpty(label))
                    continue;
                bars.Add(new BarItem() { Label = label, Count = ToLong(row["Cnt"]), Ratio = 0 });
            }
            Normalize(bars);
            return bars;
        }

        private List<BarItem> ToBars(Dictionary<string, long> counter, int limit)
        {
            List<BarItem> bars = counter.Select(kv => new BarItem() { Label = kv.Key, Count = kv.Value, Ratio = 0 })
                .OrderByDescending(b => b.Count).Take(limit).ToList();
            Normalize(bars);
            return bars;
        }

        private static void Normalize(List<BarItem> bars)
        {
            long max = bars.Count == 0 ? 0 : bars.Max(b => b.Count);
            if (max <= 0)
                return;
            foreach (BarItem bar in bars)
                bar.Ratio = (double)bar.Count / max;
        }

        private void BuildSummary(Dictionary<string, object> summary)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            wrap.Children.Add(SummaryCard(LangManager.GetValueByKey("StatVideos"), summary.TryGetValue("VideoCount", out object v1) ? v1?.ToString() ?? "0" : "0"));
            wrap.Children.Add(SummaryCard(LangManager.GetValueByKey("StatFavorites"), summary.TryGetValue("FavoriteCount", out object v2) ? v2?.ToString() ?? "0" : "0"));
            double totalSize = summary.TryGetValue("TotalSize", out object v3) ? ToLong(v3) : 0;
            wrap.Children.Add(SummaryCard(LangManager.GetValueByKey("StatTotalSize"), (totalSize / 1024d / 1024d / 1024d).ToString("F2") + " GB"));
            long totalDuration = summary.TryGetValue("TotalDuration", out object v4) ? ToLong(v4) : 0;
            wrap.Children.Add(SummaryCard(LangManager.GetValueByKey("StatTotalDuration"), (totalDuration / 60d).ToString("F0") + " min"));
            _Panel.Children.Add(wrap);
            _Panel.Children.Add(_Status);
        }

        private Border SummaryCard(string label, string value)
        {
            var stack = new StackPanel { Margin = new Thickness(10, 6, 10, 6) };
            stack.Children.Add(new TextBlock { Text = value, FontSize = 22, FontWeight = FontWeights.Bold });
            stack.Children.Add(new TextBlock { Text = label, Opacity = 0.75 });
            return new Border {
                Child = stack,
                Margin = new Thickness(0, 0, 10, 6),
                Padding = new Thickness(14, 8, 14, 8),
                MinWidth = 150,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(24, 128, 128, 128)),
            };
        }

        private void BuildChart(string title, List<BarItem> bars)
        {
            if (bars == null || bars.Count == 0)
                return;
            var card = new Border {
                Margin = new Thickness(0, 4, 0, 10),
                Padding = new Thickness(12, 8, 12, 12),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(14, 128, 128, 128)),
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });

            var items = new ItemsControl { ItemsSource = bars };
            items.ItemTemplate = BuildBarTemplate();
            stack.Children.Add(items);
            card.Child = stack;
            _Panel.Children.Add(card);
        }

        /// <summary>
        /// 一行 = [标签 140px][比例条][数量]；用水平 StackPanel 而非 Grid（代码构建更简单可靠）
        /// </summary>
        private DataTemplate BuildBarTemplate()
        {
            var template = new DataTemplate();

            var rowFactory = new FrameworkElementFactory(typeof(StackPanel));
            rowFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            rowFactory.SetValue(StackPanel.MarginProperty, new Thickness(0, 2, 0, 2));

            var labelFactory = new FrameworkElementFactory(typeof(TextBlock));
            labelFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Label"));
            labelFactory.SetValue(TextBlock.WidthProperty, 140d);
            labelFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
            labelFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            labelFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
            labelFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            rowFactory.AppendChild(labelFactory);

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.HeightProperty, 12d);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(BAR_COLOR));
            borderFactory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.SetBinding(Border.WidthProperty, new System.Windows.Data.Binding("Ratio") {
                Converter = new RatioToWidthConverter(),
                ConverterParameter = 420d,
            });
            rowFactory.AppendChild(borderFactory);

            var countFactory = new FrameworkElementFactory(typeof(TextBlock));
            countFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Count") {
                StringFormat = " {0}",
            });
            countFactory.SetValue(TextBlock.MarginProperty, new Thickness(6, 0, 0, 0));
            countFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            rowFactory.AppendChild(countFactory);

            template.VisualTree = rowFactory;
            return template;
        }

        private class RatioToWidthConverter : System.Windows.Data.IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                double ratio = value is double d ? d : 0;
                double maxWidth = parameter is double m ? m : 420;
                return Math.Max(2, ratio * maxWidth);
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                return null;
            }
        }
    }
}
