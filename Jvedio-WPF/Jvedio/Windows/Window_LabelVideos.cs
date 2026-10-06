using Jvedio.Core.Library;
using SuperControls.Style.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using LangManager = SuperControls.Style.LangManager;

namespace Jvedio.Windows
{
    public sealed class Window_LabelVideos : Window
    {
        public sealed class Row : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public long DataID { get; set; }
            public string VID { get; set; }
            public string Title { get; set; }
            public bool Original { get; set; }
            public Action<Row> Changed { get; set; }
            private bool _Selected;
            public bool Selected {
                get => _Selected;
                set {
                    if (_Selected == value) return;
                    _Selected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
                    Changed?.Invoke(this);
                }
            }
        }

        private readonly long _DbId;
        private readonly string _Name;
        private readonly TextBox _Search = new TextBox { MinWidth = 300 };
        private readonly CheckBox _Assigned = new CheckBox { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _Status = new TextBlock { Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap };
        private readonly DataGrid _Grid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            EnableRowVirtualization = true, EnableColumnVirtualization = true, SelectionMode = DataGridSelectionMode.Extended };
        private readonly Dictionary<long, bool> _Changes = new Dictionary<long, bool>();
        private readonly DispatcherTimer _Debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        private readonly Button _Apply = new Button();
        private readonly Button _Previous = new Button();
        private readonly Button _Next = new Button();
        private readonly TextBlock _Pages = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
        private readonly StackPanel _Toolbar;
        private List<Row> _Rows = new List<Row>();
        private int _Page = 1, _Version;
        private long _Total;
        private bool _Busy;

        public Window_LabelVideos(long dbId, string name)
        {
            _DbId = dbId;
            _Name = name;
            Title = string.Format(L("LabelManageTitle"), name);
            Width = 980; Height = 680; MinWidth = 700; MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "Window.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");
            SetResourceReference(FontSizeProperty, "GlobalFontSize");
            var root = new DockPanel { Margin = new Thickness(16) };
            var top = new StackPanel();
            DockPanel.SetDock(top, Dock.Top);
            top.Children.Add(new TextBlock { Text = L("LabelAssignmentHint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
            _Toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            _Search.ToolTip = L("LabelMovieSearch");
            _Assigned.Content = L("LabelAssignedOnly");
            _Toolbar.Children.Add(_Search); _Toolbar.Children.Add(_Assigned);
            _Toolbar.Children.Add(MakeButton(L("LabelSelectPage"), () => SetPage(true)));
            _Toolbar.Children.Add(MakeButton(L("LabelClearPage"), () => SetPage(false)));
            top.Children.Add(_Toolbar);
            top.Children.Add(_Status);
            root.Children.Add(top);
            var bottom = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(bottom, Dock.Bottom);
            var paging = new StackPanel { Orientation = Orientation.Horizontal };
            _Previous.Content = L("LabelPreviousPage"); _Next.Content = L("LabelNextPage");
            _Previous.Click += async (s, e) => { if (!_Busy && _Page > 1) { _Page--; await LoadAsync(); } };
            _Next.Click += async (s, e) => { if (!_Busy && _Page * (long)LibraryLabelService.PageSize < _Total) { _Page++; await LoadAsync(); } };
            paging.Children.Add(_Previous); paging.Children.Add(_Pages); paging.Children.Add(_Next);
            bottom.Children.Add(paging);
            _Apply.Content = L("LabelSaveChanges");
            _Apply.SetResourceReference(StyleProperty, "ButtonSuccess");
            _Apply.Padding = new Thickness(14, 7, 14, 7);
            _Apply.HorizontalAlignment = HorizontalAlignment.Right;
            _Apply.Click += async (s, e) => await ApplyAsync();
            bottom.Children.Add(_Apply);
            root.Children.Add(bottom);
            var checkStyle = new Style(typeof(CheckBox));
            checkStyle.Setters.Add(new Setter(IsHitTestVisibleProperty, true));
            checkStyle.Setters.Add(new Setter(HorizontalAlignmentProperty, HorizontalAlignment.Center));
            _Grid.Columns.Add(new DataGridCheckBoxColumn {
                Header = L("LabelAssignedColumn"),
                Binding = new Binding(nameof(Row.Selected)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                ElementStyle = checkStyle,
                Width = 85
            });
            _Grid.Columns.Add(new DataGridTextColumn { Header = L("LabelMovieCode"), Binding = new Binding(nameof(Row.VID)), IsReadOnly = true, Width = 150 });
            _Grid.Columns.Add(new DataGridTextColumn { Header = L("Title"), Binding = new Binding(nameof(Row.Title)), IsReadOnly = true,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            root.Children.Add(_Grid);
            Content = root;
            _Debounce.Tick += async (s, e) => { _Debounce.Stop(); _Page = 1; await LoadAsync(); };
            _Search.TextChanged += (s, e) => { _Debounce.Stop(); _Debounce.Start(); };
            _Assigned.Checked += async (s, e) => { _Page = 1; await LoadAsync(); };
            _Assigned.Unchecked += async (s, e) => { _Page = 1; await LoadAsync(); };
            Loaded += async (s, e) => await LoadAsync();
            Closing += (s, e) => {
                if (_Busy) { e.Cancel = true; return; }
                if (_Changes.Count > 0 && new MsgBox(L("LabelDiscardConfirm")).ShowDialog(this) != true) e.Cancel = true;
            };
            Closed += (s, e) => { _Version++; _Debounce.Stop(); };
        }

        private static string L(string key) => LangManager.GetValueByKey(key);
        private Button MakeButton(string text, Action action)
        {
            var button = new Button { Content = text, Margin = new Thickness(10, 0, 0, 0), Padding = new Thickness(10, 5, 10, 5) };
            button.Click += (s, e) => action();
            return button;
        }
        private void SetPage(bool selected)
        {
            if (_Busy) return;
            _Grid.CommitEdit(DataGridEditingUnit.Cell, true);
            foreach (Row row in _Rows) row.Selected = selected;
            UpdateStatus();
        }
        private void RowChanged(Row row)
        {
            if (row.Selected == row.Original) _Changes.Remove(row.DataID);
            else _Changes[row.DataID] = row.Selected;
            UpdateStatus();
        }
        private void UpdateStatus()
        {
            _Status.Text = string.Format(L("LabelPendingChanges"), _Changes.Count, _Total);
            _Apply.IsEnabled = !_Busy && _Changes.Count > 0;
        }

        public async Task LoadAsync()
        {
            int version = ++_Version;
            string search = _Search.Text;
            bool assigned = _Assigned.IsChecked == true;
            int page = _Page;
            _Grid.CommitEdit(DataGridEditingUnit.Cell, true);
            _Status.Text = L("HealthWorking");
            try {
                LabelVideoPage data = await Task.Run(() => LibraryLabelService.QueryVideos(_DbId, _Name, search, assigned, page));
                if (version != _Version) return;
                _Total = data.Total;
                _Rows = data.Videos.Select(video => new Row {
                    DataID = video.DataID, VID = video.VID, Title = video.Title, Original = video.Assigned,
                    Selected = _Changes.TryGetValue(video.DataID, out bool desired) ? desired : video.Assigned
                }).ToList();
                foreach (Row row in _Rows) row.Changed = RowChanged;
                _Grid.ItemsSource = _Rows;
                _Pages.Text = string.Format(L("LabelPaging"), _Page, Math.Max(1, (_Total + LibraryLabelService.PageSize - 1) / LibraryLabelService.PageSize));
                _Previous.IsEnabled = _Page > 1;
                _Next.IsEnabled = _Page * (long)LibraryLabelService.PageSize < _Total;
                UpdateStatus();
            } catch (Exception ex) { if (version == _Version) _Status.Text = ex.Message; App.Logger.Error(ex); }
        }

        private async Task ApplyAsync()
        {
            if (_Busy) return;
            _Grid.CommitEdit(DataGridEditingUnit.Cell, true);
            var changes = new Dictionary<long, bool>(_Changes);
            if (changes.Count == 0) return;
            _Busy = true;
            _Version++;
            _Debounce.Stop();
            _Apply.IsEnabled = _Grid.IsEnabled = _Toolbar.IsEnabled = _Previous.IsEnabled = _Next.IsEnabled = false;
            try {
                await Task.Run(() => LibraryLabelService.Assign(_DbId, _Name, changes));
                _Changes.Clear();
                _Page = 1;
                await LoadAsync();
            } catch (Exception ex) { App.Logger.Error(ex); _Status.Text = ex.Message; }
            finally {
                _Busy = false; _Grid.IsEnabled = _Toolbar.IsEnabled = true;
                _Apply.IsEnabled = _Changes.Count > 0;
            }
        }
    }
}
