using Jvedio.Core.Library;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using LangManager = SuperControls.Style.LangManager;

namespace Jvedio.Core.UserControls
{
    /// <summary>Native WPF charts that redraw at their available width and DPI.</summary>
    public sealed class StatisticsChart : FrameworkElement
    {
        private readonly StatisticChart _Data;
        private readonly double _Total;
        private static readonly Brush[] Colors = {
            new SolidColorBrush(Color.FromRgb(64,158,255)), new SolidColorBrush(Color.FromRgb(40,180,140)),
            new SolidColorBrush(Color.FromRgb(245,170,65)), new SolidColorBrush(Color.FromRgb(155,115,240)),
            new SolidColorBrush(Color.FromRgb(235,100,125)), new SolidColorBrush(Color.FromRgb(60,190,205))
        };
        public StatisticsChart(StatisticChart data, double total)
        {
            _Data = data; _Total = total;
            Height = data.Kind == "Bar" || data.Kind == "Coverage" ? Math.Max(150, data.Items.Count * 28 + 15) : 250;
            SnapsToDevicePixels = true;
            ToolTip = string.Join(Environment.NewLine, data.Items.Select(item => Name(item) + ": " + item.Count.ToString("N0")));
        }
        private string Name(StatisticItem item) => item.Localized ? LangManager.GetValueByKey(item.Label) : item.Label;
        private Brush Foreground => TryFindResource("Window.Foreground") as Brush ?? Brushes.Black;
        private double TextSize => (TryFindResource("GlobalFontSize") is double size ? size : 14) * 0.86;
        private FormattedText Text(string value, double? size = null) => new FormattedText(value,
            CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei"), size ?? TextSize,
            Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        private void Label(DrawingContext dc, string value, double x, double y, double maxWidth = double.PositiveInfinity)
        {
            var text = Text(value);
            if (!double.IsInfinity(maxWidth)) { text.MaxTextWidth = Math.Max(1, maxWidth); text.Trimming = TextTrimming.CharacterEllipsis; text.MaxLineCount = 1; }
            dc.DrawText(text, new Point(x, y));
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth <= 0) return;
            if (_Data.Items.Count == 0) { Label(dc, LangManager.GetValueByKey("StatNoData"), 10, 45); return; }
            if (_Data.Kind == "Donut") DrawDonut(dc);
            else if (_Data.Kind == "Bar" || _Data.Kind == "Coverage") DrawBars(dc);
            else DrawCartesian(dc, _Data.Kind == "Line");
        }
        private void DrawBars(DrawingContext dc)
        {
            double labels = Math.Min(155, ActualWidth * .34);
            double width = Math.Max(20, ActualWidth - labels - 100);
            double maximum = _Data.Kind == "Coverage" ? _Total : _Data.Items.Max(item => item.Count);
            for (int i = 0; i < _Data.Items.Count; i++) {
                StatisticItem item = _Data.Items[i];
                double y = i * 28 + 6;
                Label(dc, Name(item), 0, y, labels - 8);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(22,128,128,128)), null, new Rect(labels, y + 3, width, 13), 3, 3);
                double ratio = maximum <= 0 ? 0 : item.Count / maximum;
                if (ratio > 0) dc.DrawRoundedRectangle(Colors[i % Colors.Length], null, new Rect(labels, y + 3, width * ratio, 13), 3, 3);
                string value = item.Count.ToString("N0") + (_Data.Kind == "Coverage" ? " · " + ratio.ToString("P0") : "");
                Label(dc, value, labels + width + 8, y, 92);
            }
        }
        private void DrawDonut(DrawingContext dc)
        {
            double sum = _Data.Items.Sum(item => item.Count);
            double radius = Math.Min(77, ActualWidth * .18);
            var center = new Point(radius + 14, 112);
            double thickness = radius * .32;
            if (sum <= 0) dc.DrawEllipse(null, new Pen(Brushes.Gray, thickness), center, radius, radius);
            double angle = -90;
            for (int i = 0; i < _Data.Items.Count; i++) {
                double fraction = sum == 0 ? 0 : _Data.Items[i].Count / sum;
                double end = angle + fraction * 360;
                if (fraction >= .999999) dc.DrawEllipse(null, new Pen(Colors[i % Colors.Length], thickness), center, radius, radius);
                else if (fraction > 0) {
                    var geometry = new StreamGeometry();
                    using (var context = geometry.Open()) {
                        context.BeginFigure(Polar(center, radius, angle), false, false);
                        context.ArcTo(Polar(center, radius, end), new Size(radius, radius), 0, fraction > .5,
                            SweepDirection.Clockwise, true, false);
                    }
                    geometry.Freeze();
                    dc.DrawGeometry(null, new Pen(Colors[i % Colors.Length], thickness), geometry);
                }
                angle = end;
            }
            var total = Text(sum.ToString("N0"), TextSize * 1.65);
            dc.DrawText(total, new Point(center.X - total.Width / 2, center.Y - total.Height / 2));
            double x = center.X + radius + thickness / 2 + 22;
            for (int i = 0; i < _Data.Items.Count; i++) {
                StatisticItem item = _Data.Items[i];
                double y = 55 + i * 49;
                dc.DrawRoundedRectangle(Colors[i % Colors.Length], null, new Rect(x, y + 4, 10, 10), 2, 2);
                Label(dc, Name(item), x + 17, y, Math.Max(20, ActualWidth - x - 17));
                Label(dc, item.Count.ToString("N0") + " · " + (sum == 0 ? 0 : item.Count / sum).ToString("P1"), x + 17, y + 21);
            }
        }
        private static Point Polar(Point center, double radius, double angle) =>
            new Point(center.X + radius * Math.Cos(angle * Math.PI / 180), center.Y + radius * Math.Sin(angle * Math.PI / 180));
        private void DrawCartesian(DrawingContext dc, bool line)
        {
            double left = 48, top = 16, width = Math.Max(30, ActualWidth - left - 15), height = 175;
            double max = Math.Max(1, _Data.Items.Max(item => item.Count));
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(45,128,128,128)), 1);
            for (int i = 0; i <= 4; i++) {
                double y = top + height - height * i / 4;
                dc.DrawLine(gridPen, new Point(left, y), new Point(left + width, y));
                Label(dc, (max * i / 4).ToString("0"), 0, y - 8, left - 6);
            }
            double step = width / _Data.Items.Count;
            Point? previous = null;
            int stride = Math.Max(1, (int)Math.Ceiling(_Data.Items.Count / 8d));
            for (int i = 0; i < _Data.Items.Count; i++) {
                StatisticItem item = _Data.Items[i];
                double x = left + step * (i + .5), y = top + height - height * item.Count / max;
                if (line) {
                    if (previous.HasValue) dc.DrawLine(new Pen(Colors[0], 2.5), previous.Value, new Point(x, y));
                    dc.DrawEllipse(Colors[0], null, new Point(x, y), 3.5, 3.5);
                    previous = new Point(x, y);
                } else if (item.Count > 0) {
                    dc.DrawRoundedRectangle(Colors[i % Colors.Length], null,
                        new Rect(x - step * .31, y, Math.Max(2, step * .62), top + height - y), 2, 2);
                }
                if (_Data.Items.Count <= 12 && item.Count > 0) {
                    var value = Text(item.Count.ToString("N0"));
                    dc.DrawText(value, new Point(x - value.Width / 2, Math.Max(0, y - value.Height - 3)));
                }
                if (i % stride == 0 || i == _Data.Items.Count - 1) {
                    string label = Name(item);
                    if (line && label.Length == 7) label = label.Substring(2);
                    var text = Text(label);
                    text.MaxTextWidth = Math.Max(25, step * stride);
                    text.MaxLineCount = 2;
                    text.Trimming = TextTrimming.CharacterEllipsis;
                    dc.DrawText(text, new Point(Math.Max(left, x - text.Width / 2), top + height + 9));
                }
            }
        }
    }
}
