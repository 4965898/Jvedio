using Jvedio.Core.Clipper;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperControls.Style;
using LangManager = SuperControls.Style.LangManager;

namespace Jvedio.Windows
{
    public sealed class BrowserClipperSettings : StackPanel
    {
        private readonly BrowserClipperService _Service = BrowserClipperService.Instance;
        private readonly SearchBox _Port = new SearchBox { Text = "18888", MinWidth = 100, Width = 100, ShowClearButton = false, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly SearchBox _Token = new SearchBox { IsReadOnly = true, ShowClearButton = false };
        private readonly TextBlock _Status = new TextBlock { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Left, Margin = new Thickness(10, 6, 10, 6) };
        private readonly Button _Start = new Button();
        private readonly Button _Stop = new Button();
        public BrowserClipperSettings()
        {
            var root = this;
            Margin = new Thickness(5);
            Grid.SetIsSharedSizeScope(this, true);
            root.Children.Add(Description("ClipperInstructions"));
            foreach (var input in new[] { _Port, _Token }) {
                input.SetResourceReference(StyleProperty, "BaseSearchTextBox");
                input.Foreground = Brushes.White;
                input.Margin = new Thickness(0);
            }
            _Port.Text = (_Service.Running ? _Service.Port : ConfigManager.BrowserClipperConfig.Port).ToString();
            AddField("ClipperPort", _Port);
            _Token.Text = _Service.Token;
            AddField("ClipperToken", _Token);
            var buttons = new WrapPanel { Margin = new Thickness(5, 0, 5, 0) };
            var copy = new Button { Content = L("ClipperCopyToken") };
            _Start.Content = L("ClipperStart"); _Stop.Content = L("ClipperStop");
            foreach (var button in new[] { _Start, _Stop, copy }) {
                button.Foreground = Brushes.White;
                buttons.Children.Add(button);
            }
            _Status.SetResourceReference(StyleProperty, "BaseTextBlock");
            root.Children.Add(buttons); root.Children.Add(_Status);
            var media = ConfigManager.BrowserClipperConfig;
            var saveImages = AddMediaOption("ClipperSaveImages", media.SaveImages, value => media.SaveImages = value);
            var cover = AddMediaOption("ClipperSaveCoverImages", media.SaveCoverImages, value => media.SaveCoverImages = value);
            var previews = AddMediaOption("ClipperSavePreviewImages", media.SavePreviewImages, value => media.SavePreviewImages = value);
            var actors = AddMediaOption("ClipperSaveActorImages", media.SaveActorImages, value => media.SaveActorImages = value);
            cover.Margin = previews.Margin = new Thickness(30, 6, 10, 6);
            actors.Margin = new Thickness(30, 6, 10, 6);
            Action updateImageOptions = () => cover.IsEnabled = previews.IsEnabled = actors.IsEnabled = saveImages.IsChecked == true;
            saveImages.Checked += (s, e) => updateImageOptions();
            saveImages.Unchecked += (s, e) => updateImageOptions();
            updateImageOptions();
            AddMediaOption("ClipperSavePreviewVideos", media.SavePreviewVideos, value => media.SavePreviewVideos = value);
            root.Children.Add(Description("ClipperMediaHint"));
            _Start.Click += (s, e) => {
                try {
                    if (!int.TryParse(_Port.Text, out int port) || port < 1024 || port > 65535) throw new ArgumentException(L("ClipperInvalidPort"));
                    _Service.Start(port);
                    ConfigManager.BrowserClipperConfig.Port = port;
                    ConfigManager.BrowserClipperConfig.Enabled = true;
                    ConfigManager.BrowserClipperConfig.Save();
                    Refresh();
                } catch (Exception ex) { _Status.Text = L("ClipperStartFailed") + " " + ex.Message; }
            };
            _Stop.Click += (s, e) => {
                _Service.Dispose(); ConfigManager.BrowserClipperConfig.Enabled = false;
                ConfigManager.BrowserClipperConfig.Save(); Refresh();
            };
            copy.Click += (s, e) => {
                try { Clipboard.SetText(_Service.Token); _Status.Text = L("ClipperTokenCopied"); }
                catch (Exception ex) { _Status.Text = ex.Message; }
            };
            Loaded += (s, e) => { _Service.Saved -= OnSaved; _Service.Saved += OnSaved; Refresh(); };
            Unloaded += (s, e) => _Service.Saved -= OnSaved;
            Refresh();
        }
        private static string L(string key) => LangManager.GetValueByKey(key);
        private static TextBlock Description(string key)
        {
            var text = new TextBlock { Text = L(key), Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Left, Margin = new Thickness(10, 6, 10, 6) };
            text.SetResourceReference(StyleProperty, "BaseTextBlock");
            return text;
        }
        private void AddField(string key, SearchBox input)
        {
            var row = new Grid { Margin = new Thickness(10, 6, 10, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 110, SharedSizeGroup = "ClipperFieldLabels" });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            string fullLabel = L(key);
            var label = new TextBlock { Text = fullLabel.Split(new[] { '（', '(' })[0].Trim(), ToolTip = fullLabel,
                Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            label.SetResourceReference(StyleProperty, "BaseTextBlock");
            Grid.SetColumn(input, 1);
            row.Children.Add(label); row.Children.Add(input); Children.Add(row);
        }
        private CheckBox AddMediaOption(string key, bool value, Action<bool> apply)
        {
            var option = new CheckBox { Content = new TextBlock { Text = L(key), Foreground = Brushes.White }, Foreground = Brushes.White,
                IsChecked = value };
            option.Checked += (s, e) => { apply(true); ConfigManager.BrowserClipperConfig.Save(); };
            option.Unchecked += (s, e) => { apply(false); ConfigManager.BrowserClipperConfig.Save(); };
            Children.Add(option); return option;
        }
        private void Refresh()
        {
            _Start.IsEnabled = _Port.IsEnabled = !_Service.Running;
            _Stop.IsEnabled = _Service.Running;
            _Status.Text = _Service.Running ? L("ClipperRunning") + " 127.0.0.1:" + _Service.Port : L("ClipperStopped");
        }
        private void OnSaved(long dbId, int added, int updated, int skipped)
        {
            Dispatcher.BeginInvoke(new Action(() => _Status.Text = string.Format(L("ClipperSaved"), added, updated, skipped)));
        }
    }
}
