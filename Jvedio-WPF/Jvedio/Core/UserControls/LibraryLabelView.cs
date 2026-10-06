using Jvedio.Core.Library;
using Jvedio.Windows;
using SuperControls.Style.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LangManager = SuperControls.Style.LangManager;

namespace Jvedio.Core.UserControls
{
    public sealed class LibraryLabelView : UserControl, ITabItemControl
    {
        private long _DbId;
        private readonly TextBox _Search = new TextBox { MinWidth = 220, Margin = new Thickness(0, 0, 10, 0) };
        private readonly ComboBox _Sort = new ComboBox { MinWidth = 150, Margin = new Thickness(0, 0, 10, 0) };
        private readonly CheckBox _Unused = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        private readonly TextBlock _Summary = new TextBlock { Margin = new Thickness(0, 12, 0, 6), TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _Status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) };
        private readonly WrapPanel _Cards = new WrapPanel();
        private readonly ScrollViewer _Scroll;
        private readonly StackPanel _Toolbar;
        private List<LibraryLabel> _Labels = new List<LibraryLabel>();
        private bool _Subscribed;
        private bool _Busy;
        private int _Version;
        public Action<string, LabelType> OnBrowse;
        public Action OnBrowseUnlabeled;

        public LibraryLabelView(long dbId)
        {
            _DbId = dbId;
            SetResourceReference(BackgroundProperty, "Window.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");
            SetResourceReference(FontSizeProperty, "GlobalFontSize");
            var root = new DockPanel { Margin = new Thickness(16) };
            var top = new StackPanel();
            DockPanel.SetDock(top, Dock.Top);
            var heading = new TextBlock { Text = L("MyLabel"), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) };
            heading.SetResourceReference(TextBlock.FontSizeProperty, "GlobalFontSize18");
            top.Children.Add(heading);
            top.Children.Add(new TextBlock { Text = L("LabelPageHint"), TextWrapping = TextWrapping.Wrap, Opacity = 0.8, Margin = new Thickness(0, 0, 0, 12) });
            _Toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            _Search.ToolTip = L("LabelSearchHint");
            _Search.TextChanged += (s, e) => Render();
            _Toolbar.Children.Add(_Search);
            _Sort.Items.Add(L("LabelSortUsage"));
            _Sort.Items.Add(L("LabelSortName"));
            _Sort.SelectedIndex = 0;
            _Sort.SelectionChanged += (s, e) => Render();
            _Toolbar.Children.Add(_Sort);
            _Unused.Content = L("LabelOnlyUnused");
            _Unused.Checked += (s, e) => Render();
            _Unused.Unchecked += (s, e) => Render();
            _Toolbar.Children.Add(_Unused);
            _Toolbar.Children.Add(Button(L("LabelCreate"), async () => await CreateAsync(), true));
            _Toolbar.Children.Add(Button(L("Refresh"), async () => await ReloadAsync()));
            top.Children.Add(new ScrollViewer { Content = _Toolbar, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
            top.Children.Add(_Summary);
            var unlabeled = Button(L("LabelBrowseUnlabeled"), () => { OnBrowseUnlabeled?.Invoke(); return Task.CompletedTask; });
            unlabeled.HorizontalAlignment = HorizontalAlignment.Left;
            top.Children.Add(unlabeled);
            top.Children.Add(_Status);
            root.Children.Add(top);
            _Scroll = new ScrollViewer { Content = _Cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            root.Children.Add(_Scroll);
            Content = root;
            Loaded += async (s, e) => {
                if (!_Subscribed) { LibraryLabelService.Changed += Changed; _Subscribed = true; }
                await ReloadAsync();
            };
            Unloaded += (s, e) => {
                if (_Subscribed) { LibraryLabelService.Changed -= Changed; _Subscribed = false; }
                _Version++;
            };
        }

        private static string L(string key) => LangManager.GetValueByKey(key);
        private Button Button(string caption, Func<Task> action, bool success = false)
        {
            var button = new Button { Content = caption, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
            if (success) button.SetResourceReference(StyleProperty, "ButtonSuccess");
            button.Click += async (s, e) => { if (!_Busy) await action(); };
            return button;
        }

        private void Changed(long dbId)
        {
            if (dbId != _DbId || !IsLoaded) return;
            Dispatcher.BeginInvoke(new Action(async () => await ReloadAsync()));
        }

        public async Task ReloadAsync()
        {
            int version = ++_Version;
            _DbId = ConfigManager.Main.CurrentDBId;
            long dbId = _DbId;
            _Status.Text = L("HealthWorking");
            try {
                var result = await Task.Run(() => new {
                    Labels = LibraryLabelService.List(dbId),
                    Total = LibraryLabelService.CountVideos(dbId),
                    Untagged = LibraryLabelService.CountVideos(dbId, true)
                });
                if (version != _Version) return;
                _Labels = result.Labels;
                _Summary.Text = string.Format(L("LabelSummary"), _Labels.Count, result.Total - result.Untagged, result.Untagged);
                _Status.Text = "";
                Render();
            } catch (Exception ex) {
                if (version == _Version) _Status.Text = ex.Message;
                App.Logger.Error(ex);
            }
        }

        private void Render()
        {
            if (_Cards == null) return;
            IEnumerable<LibraryLabel> labels = _Labels;
            string search = _Search.Text?.Trim() ?? "";
            if (search.Length > 0) labels = labels.Where(label => label.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_Unused.IsChecked == true) labels = labels.Where(label => label.Count == 0);
            labels = _Sort.SelectedIndex == 1 ? labels.OrderBy(label => label.Name, StringComparer.CurrentCulture)
                : labels.OrderByDescending(label => label.Count).ThenBy(label => label.Name, StringComparer.CurrentCulture);
            _Cards.Children.Clear();
            foreach (LibraryLabel label in labels) {
                var panel = new StackPanel();
                var name = new Button { Content = label.Name, HorizontalContentAlignment = HorizontalAlignment.Left,
                    FontWeight = FontWeights.Bold, Padding = new Thickness(6, 8, 6, 8), ToolTip = label.Name };
                name.Click += (s, e) => OnBrowse?.Invoke(label.Name, LabelType.LabelName);
                panel.Children.Add(name);
                panel.Children.Add(new TextBlock { Text = string.Format(L("LabelVideoCount"), label.Count),
                    Margin = new Thickness(6, 6, 0, 10), Opacity = 0.8 });
                var actions = new WrapPanel();
                actions.Children.Add(Button(L("LabelManageVideos"), () => { Manage(label); return Task.CompletedTask; }, true));
                actions.Children.Add(Button(L("Rename"), async () => await RenameAsync(label)));
                actions.Children.Add(Button(L("Delete"), async () => await DeleteAsync(label)));
                panel.Children.Add(actions);
                _Cards.Children.Add(new Border { Width = 300, Padding = new Thickness(12), Margin = new Thickness(0, 0, 12, 12),
                    CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromArgb(22, 128, 128, 128)), Child = panel });
            }
            if (_Cards.Children.Count == 0)
                _Cards.Children.Add(new TextBlock { Text = _Labels.Count == 0 ? L("LabelEmptyHint") : L("LabelNoMatches"),
                    TextWrapping = TextWrapping.Wrap, MaxWidth = 640, Margin = new Thickness(10, 35, 10, 0), Opacity = 0.8 });
        }

        private async Task MutateAsync(Action action)
        {
            _Busy = true;
            _Toolbar.IsEnabled = false;
            _Status.Text = L("HealthWorking");
            try { await Task.Run(action); await ReloadAsync(); }
            catch (Exception ex) { App.Logger.Error(ex); _Status.Text = ex.Message; }
            finally { _Busy = false; _Toolbar.IsEnabled = true; }
        }

        private async Task CreateAsync()
        {
            var input = new DialogInput(L("LabelCreate"));
            if (input.ShowDialog(Window.GetWindow(this)) != true) return;
            string name;
            try { name = LibraryLabelService.ValidateName(input.Text); }
            catch (Exception ex) { _Status.Text = ex.Message; return; }
            await MutateAsync(() => { LibraryLabelService.Create(_DbId, name); });
            _Search.Text = name;
            _Unused.IsChecked = false;
            Render();
        }

        private async Task RenameAsync(LibraryLabel label)
        {
            var input = new DialogInput(L("LabelRenamePrompt"));
            if (input.ShowDialog(Window.GetWindow(this)) != true) return;
            string name;
            try { name = LibraryLabelService.ValidateName(input.Text); }
            catch (Exception ex) { _Status.Text = ex.Message; return; }
            if (name != label.Name && _Labels.Any(item => item.Name == name) &&
                new MsgBox(string.Format(L("LabelMergeConfirm"), label.Name, name)).ShowDialog(Window.GetWindow(this)) != true) return;
            await MutateAsync(() => LibraryLabelService.Rename(_DbId, label.Name, name));
            _Search.Clear();
        }

        private async Task DeleteAsync(LibraryLabel label)
        {
            if (new MsgBox(string.Format(L("LabelDeleteConfirm"), label.Name, label.Count)).ShowDialog(Window.GetWindow(this)) != true) return;
            await MutateAsync(() => LibraryLabelService.Delete(_DbId, label.Name));
        }

        private void Manage(LibraryLabel label)
        {
            new Window_LabelVideos(_DbId, label.Name) { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        public void Refresh(int page = -1) { _ = ReloadAsync(); }
        public void SetSearchFocus() => _Search.Focus();
        public void GoToTop() => _Scroll.ScrollToTop();
        public void GoToBottom() => _Scroll.ScrollToBottom();
        public void FirstPage() => GoToTop();
        public void LastPage() => GoToBottom();
        public void NextPage() => _Scroll.PageDown();
        public void PreviousPage() => _Scroll.PageUp();
    }
}
