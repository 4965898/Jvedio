using Jvedio.Core.Backup;
using LangManager = SuperControls.Style.LangManager;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Jvedio.Windows
{
    /// <summary>Choose a remote ZIP before downloading and staging a restore.</summary>
    public sealed class Window_RemoteBackupPicker : Window
    {
        private readonly DataGrid _Grid = new DataGrid {
            IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single, EnableRowVirtualization = true
        };

        public RemoteBackupStore.RemoteBackupItem SelectedBackup { get; private set; }

        public Window_RemoteBackupPicker(IEnumerable<RemoteBackupStore.RemoteBackupItem> backups)
        {
            Title = LangManager.GetValueByKey("RemoteBackupPickerTitle");
            Width = 760;
            Height = 470;
            MinWidth = 560;
            MinHeight = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "Window.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");

            var root = new DockPanel { Margin = new Thickness(12) };
            var hint = new TextBlock {
                Text = LangManager.GetValueByKey("RemoteBackupPickerHint"),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(5, 0, 5, 10)
            };
            DockPanel.SetDock(hint, Dock.Top);
            root.Children.Add(hint);

            var footer = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = LangManager.GetValueByKey("RemoteBackupCancel"),
                MinWidth = 100, Margin = new Thickness(5) };
            cancel.Click += (s, e) => { DialogResult = false; Close(); };
            var choose = new Button { Content = LangManager.GetValueByKey("RemoteBackupChoose"),
                MinWidth = 130, Margin = new Thickness(5) };
            choose.Click += (s, e) => Choose();
            footer.Children.Add(cancel);
            footer.Children.Add(choose);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            _Grid.Columns.Add(new DataGridTextColumn {
                Header = LangManager.GetValueByKey("RemoteBackupFileName"),
                Binding = new Binding("FileName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            _Grid.Columns.Add(new DataGridTextColumn {
                Header = LangManager.GetValueByKey("RemoteBackupModified"),
                Binding = new Binding("ModifiedText"), Width = 170
            });
            _Grid.Columns.Add(new DataGridTextColumn {
                Header = LangManager.GetValueByKey("RemoteBackupSize"),
                Binding = new Binding("SizeText"), Width = 110
            });
            _Grid.ItemsSource = backups?.ToList() ?? new List<RemoteBackupStore.RemoteBackupItem>();
            root.Children.Add(_Grid);
            Content = root;
            Loaded += (s, e) => { if (_Grid.Items.Count > 0) _Grid.SelectedIndex = 0; };
        }

        private void Choose()
        {
            SelectedBackup = _Grid.SelectedItem as RemoteBackupStore.RemoteBackupItem;
            if (SelectedBackup == null) return;
            DialogResult = true;
            Close();
        }
    }
}
