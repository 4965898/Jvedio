using Jvedio.Core.Crawler;
using Jvedio.Entity;
using LangManager = SuperControls.Style.LangManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Jvedio.Windows
{
    /// <summary>Explicit per-field preview for one manually selected scrape task.</summary>
    public sealed class Window_ScrapePreview : Window
    {
        private readonly Dictionary<string, CheckBox> _Choices = new Dictionary<string, CheckBox>();

        public HashSet<string> SelectedFields => new HashSet<string>(_Choices
            .Where(pair => pair.Value.IsChecked == true).Select(pair => pair.Key));

        public Window_ScrapePreview(Video video, Dictionary<string, object> scraped, bool protectExisting)
        {
            Title = LangManager.GetValueByKey("ScrapePreviewTitle") + " - " + video.VID;
            Width = 850;
            Height = 600;
            MinWidth = 650;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "Window.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");

            var root = new DockPanel { Margin = new Thickness(12) };
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(footer, Dock.Bottom);
            var cancel = new Button { Content = LangManager.GetValueByKey("ScrapePreviewCancel"), MinWidth = 100, Margin = new Thickness(5) };
            cancel.Click += (s, e) => { DialogResult = false; Close(); };
            var apply = new Button { Content = LangManager.GetValueByKey("ScrapePreviewApply"), MinWidth = 130, Margin = new Thickness(5) };
            apply.Click += (s, e) => { DialogResult = true; Close(); };
            footer.Children.Add(cancel);
            footer.Children.Add(apply);
            root.Children.Add(footer);

            var intro = new TextBlock { Text = LangManager.GetValueByKey("ScrapePreviewIntro"),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 4, 10) };
            DockPanel.SetDock(intro, Dock.Top);
            root.Children.Add(intro);
            var header = new Grid { Margin = new Thickness(4, 0, 4, 6) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddText(header, LangManager.GetValueByKey("ScrapePreviewField"), 0, true);
            AddText(header, LangManager.GetValueByKey("ScrapePreviewCurrent"), 1, true);
            AddText(header, LangManager.GetValueByKey("ScrapePreviewIncoming"), 2, true);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var rows = new StackPanel();
            foreach (string name in ScrapeFieldPolicy.Available(scraped)) {
                var row = new Grid { Margin = new Thickness(4, 2, 4, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var check = new CheckBox { Content = name, IsChecked = !protectExisting ||
                    !ScrapeFieldPolicy.HasExistingValue(video, name), VerticalAlignment = VerticalAlignment.Top };
                _Choices[name] = check;
                row.Children.Add(check);
                AddText(row, ScrapeFieldPolicy.Current(video, name), 1, false);
                AddText(row, ScrapeFieldPolicy.Proposed(scraped, name), 2, false);
                rows.Children.Add(row);
            }
            var scroll = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            root.Children.Add(scroll);
            Content = root;
        }

        private static void AddText(Grid grid, string value, int column, bool bold)
        {
            var text = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 0, 6, 0),
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal };
            Grid.SetColumn(text, column);
            grid.Children.Add(text);
        }
    }
}
