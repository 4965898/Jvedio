using Jvedio.Core.CustomEventArgs;
using Jvedio.Core.Tasks;
using Jvedio.Entity;
using Jvedio.Entity.CommonSQL;
using Jvedio.Mapper;
using SuperControls.Style;
using SuperControls.Style.Windows;
using SuperUtils.Framework.ORM.Wrapper;
using SuperUtils.WPF.VisualTools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using static Jvedio.MapperManager;

namespace Jvedio.Core.UserControls
{
    /// <summary>
    /// Filter.xaml 的交互逻辑
    /// </summary>
    public partial class Filter : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        private const long MB_TO_B = 1024 * 1024;


        #region "事件"

        public event Action Close;

        public static Action<long> onTagStampDelete { get; set; }
        public static Action<long> onTagStampRefresh { get; set; }

        public event EventHandler OnApplyWrapper;

        private delegate void AsyncLoadItemDelegate(UIElementCollection collection, UIElement item);

        private void AsyncLoadItem(UIElementCollection collection, UIElement item) => collection.Add(item);


        #endregion

        #region "静态属性"

        private static Main MainWindow { get; set; }

        private static List<int> TimeList { get; set; } =
            new List<int>() { 0, 30, 60, 120, 240, 360 };

        /// <summary>
        /// 单位 MB
        /// </summary>
        private static List<long> SizeList { get; set; } =
            new List<long>() { 0L * MB_TO_B, 500L * MB_TO_B, 1000L * MB_TO_B, 2000L * MB_TO_B, 3000L * MB_TO_B };

        #endregion

        #region "属性"

        private ObservableCollection<TagStamp> _TagStamps = new ObservableCollection<TagStamp>();

        public ObservableCollection<TagStamp> TagStamps {
            get { return _TagStamps; }

            set {
                _TagStamps = value;
                RaisePropertyChanged();
            }
        }


        private int _GenreProgress;
        public int GenreProgress {
            get { return _GenreProgress; }
            set {
                _GenreProgress = value;
                RaisePropertyChanged();
            }
        }
        private int _SeriesProgress;
        public int SeriesProgress {
            get { return _SeriesProgress; }
            set {
                _SeriesProgress = value;
                RaisePropertyChanged();
            }
        }
        private int _DirectorProgress;
        public int DirectorProgress {
            get { return _DirectorProgress; }
            set {
                _DirectorProgress = value;
                RaisePropertyChanged();
            }
        }
        private int _StudioProgress;
        public int StudioProgress {
            get { return _StudioProgress; }
            set {
                _StudioProgress = value;
                RaisePropertyChanged();
            }
        }


        private LoadState _CommonLoad;
        public LoadState CommonLoad {
            get { return _CommonLoad; }
            set {
                _CommonLoad = value;
                RaisePropertyChanged();
            }
        }

        private LoadState _GenreLoad;
        public LoadState GenreLoad {
            get { return _GenreLoad; }
            set {
                _GenreLoad = value;
                RaisePropertyChanged();
            }
        }

        private LoadState _SeriesLoad;
        public LoadState SeriesLoad {
            get { return _SeriesLoad; }
            set {
                _SeriesLoad = value;
                RaisePropertyChanged();
            }
        }
        private LoadState _DirectorLoad;
        public LoadState DirectorLoad {
            get { return _DirectorLoad; }
            set {
                _DirectorLoad = value;
                RaisePropertyChanged();
            }
        }
        private LoadState _StudioLoad;
        public LoadState StudioLoad {
            get { return _StudioLoad; }
            set {
                _StudioLoad = value;
                RaisePropertyChanged();
            }
        }


        // ************************************
        // ************* 记忆是否展开 *********
        // ************************************


        private bool _ExpandTag;
        public bool ExpandTag {
            get { return _ExpandTag; }
            set {
                _ExpandTag = value;
                RaisePropertyChanged();
                if (ConfigManager.FilterConfig != null)
                    ConfigManager.FilterConfig.ExpandTag = value;
            }
        }

        private bool _ExpandCommon;
        public bool ExpandCommon {
            get { return _ExpandCommon; }
            set {
                _ExpandCommon = value;
                RaisePropertyChanged();
                if (ConfigManager.FilterConfig != null)
                    ConfigManager.FilterConfig.ExpandCommon = value;
            }
        }

        private bool _ExpandGenre;
        public bool ExpandGenre {
            get { return _ExpandGenre; }
            set {
                _ExpandGenre = value;
                RaisePropertyChanged();
                if (ConfigManager.FilterConfig != null)
                    ConfigManager.FilterConfig.ExpandGenre = value;
            }
        }

        private bool _ExpandSeries;
        public bool ExpandSeries {
            get { return _ExpandSeries; }
            set {
                _ExpandSeries = value;
                RaisePropertyChanged();
                if (ConfigManager.FilterConfig != null)
                    ConfigManager.FilterConfig.ExpandSeries = value;
            }
        }
        private bool _ExpandDirector;
        public bool ExpandDirector {
            get { return _ExpandDirector; }
            set {
                _ExpandDirector = value;
                RaisePropertyChanged();
                if (ConfigManager.FilterConfig != null)
                    ConfigManager.FilterConfig.ExpandDirector = value;
            }
        }
        private bool _ExpandStudio;
        public bool ExpandStudio {
            get { return _ExpandStudio; }
            set {
                _ExpandStudio = value;
                RaisePropertyChanged();
                if (ConfigManager.FilterConfig != null)
                    ConfigManager.FilterConfig.ExpandStudio = value;
            }
        }


        #endregion


        static Filter()
        {
            MainWindow = SuperUtils.WPF.VisualTools.WindowHelper.GetWindowByName("Main", App.Current.Windows) as Main;
        }


        public Filter()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            InitProp();
            LoadAll();
            BindEvent();
        }

        private void BindEvent()
        {
            Window_Details.onRemoveTagStamp += onRemoveTagStamp;
        }


        private void onRemoveTagStamp()
        {
            this.InitTagStamp();
        }


        /// <summary>
        /// 用户控件的属性不能直接使用 ConfigManager
        /// </summary>
        public void InitProp()
        {
            ExpandTag = ConfigManager.FilterConfig.ExpandTag;
            ExpandCommon = ConfigManager.FilterConfig.ExpandCommon;
            ExpandGenre = ConfigManager.FilterConfig.ExpandGenre;
            ExpandSeries = ConfigManager.FilterConfig.ExpandSeries;
            ExpandDirector = ConfigManager.FilterConfig.ExpandDirector;
            ExpandStudio = ConfigManager.FilterConfig.ExpandStudio;
        }

        public void LoadAll()
        {
            if (ExpandTag)
                InitTagStamp();
            if (ExpandCommon)
                SetCommonFilter();
            if (ExpandGenre)
                LoadGenre();
            if (ExpandSeries)
                LoadSeries();
            if (ExpandDirector)
                LoadDirector();
            if (ExpandStudio)
                LoadStudio();

        }

        /// <summary>
        /// 加载默认
        /// </summary>
        private void SetCommonFilter()
        {
            if (CommonLoad == LoadState.Loaded)
                return;
            CommonLoad = LoadState.Loading;
            LoadYearMonth();
        }

        private void LoadTagStamp(List<TagStamp> beforeTagStamps = null)
        {
            Task.Run(async () => {
                await Task.Delay(200);

                Dispatcher.Invoke(() => {
                    TagStamps = TagStamp.InitTagStamp(beforeTagStamps);
                    TagStampItemsControl.ItemsSource = null;
                    TagStampItemsControl.ItemsSource = TagStamps;
                });
            });

        }

        private async void AddItem(ICollection<string> list, WrapPanel panel, Action complete = null, Action<int> onProgress = null)
        {
            panel.Children.Clear();
            int idx = 0;
            int total = list.Count;
            foreach (string item in list) {
                await App.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                   new AsyncLoadItemDelegate(AsyncLoadItem), panel.Children, buildToggleButton(item));
                idx++;
                float progress = ((float)idx / (float)total * 100);
                onProgress?.Invoke((int)progress);
            }
            complete?.Invoke();
        }

        private ToggleButton buildToggleButton(string content, bool isChecked = false)
        {
            ToggleButton toggleButton = new ToggleButton();

            toggleButton.Content = content;
            toggleButton.IsChecked = isChecked;
            toggleButton.Style = (System.Windows.Style)this.Resources["FilterToggleButton"];
            return toggleButton;
        }

        /// <summary>
        /// 加载过滤器
        /// </summary>
        private void LoadYearMonth()
        {
            string sql = $"SELECT DISTINCT ReleaseDate FROM metadata " +
                $"where metadata.DBId={ConfigManager.Main.CurrentDBId} and metadata.DataType={0}";

            List<Dictionary<string, object>> list = MapperManager.metaDataMapper.Select(sql);

            // 2020-02-10
            List<string> dates = list.Select(x => x["ReleaseDate"].ToString())
                .Where(arg => !string.IsNullOrEmpty(arg) && arg.LastIndexOf('-') > 5).ToList();

            HashSet<string> years = dates.Select(arg => arg.Split('-')[0]).ToHashSet().OrderBy(x => x).Where(arg => arg != "1900" && arg != "0001").ToHashSet();
            HashSet<string> months = dates.Select(arg => arg.Split('-')[1]).ToHashSet().OrderBy(x => x).ToHashSet();

            AddItem(years, yearWrapPanel);
            AddItem(months, monthWrapPanel, () => CommonLoad = LoadState.Loaded);
        }
        private void LoadSingleDataFromMetaData(WrapPanel wrapPanel, string field)
        {
            if (GenreLoad == LoadState.Loaded)
                return;
            GenreLoad = LoadState.Loading;
            string sql = $"SELECT DISTINCT {field} FROM metadata " +
                    $"where metadata.DBId={ConfigManager.Main.CurrentDBId} and metadata.DataType={0}";

            List<Dictionary<string, object>> list = MapperManager.metaDataMapper.Select(sql);
            List<string> dataList = list.Select(x => x[field].ToString())
                 .Where(arg => !string.IsNullOrEmpty(arg)).ToList();
            HashSet<string> set = new HashSet<string>();
            foreach (string item in dataList)
                foreach (string data in item.Split(SuperUtils.Values.ConstValues.Separator))
                    set.Add(data);
            AddItem(set, wrapPanel, () => {
                GenreLoad = LoadState.Loaded;
                // 加载完成后按当前关键词重新过滤一次（加载期间新加的标签默认全部可见）
                ApplyGenreFilter();
            }, (value) => GenreProgress = value);
        }

        private void LoadSingleData(WrapPanel wrapPanel, string field, Action before = null, Action complete = null, Action<int> onProgress = null)
        {
            before?.Invoke();
            string sql = $"SELECT DISTINCT {field} FROM metadata_video join metadata on metadata.DataID=metadata_video.DataID " +
                    $"where metadata.DBId={ConfigManager.Main.CurrentDBId} and metadata.DataType={0}";

            List<Dictionary<string, object>> list = MapperManager.metaDataMapper.Select(sql);
            List<string> dataList = list.Select(x => x[field].ToString())
                 .Where(arg => !string.IsNullOrEmpty(arg)).ToList();
            HashSet<string> set = new HashSet<string>();
            foreach (string item in dataList)
                foreach (string data in item.Split(SuperUtils.Values.ConstValues.Separator))
                    set.Add(data);

            AddItem(set, wrapPanel, () => complete?.Invoke(), (value) => onProgress?.Invoke(value));
        }

        private void HideGrid(object sender, RoutedEventArgs e)
        {
            Close?.Invoke();
        }

        private void Refresh(object sender, RoutedEventArgs e)
        {
            ResetToDefault();
            CommonLoad = LoadState.None;
            GenreLoad = LoadState.None;
            SeriesLoad = LoadState.None;
            DirectorLoad = LoadState.None;
            StudioLoad = LoadState.None;
            LoadAll();
            ApplyFilter();
        }

        private void PathCheckButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }



        private void NewTagStamp(object sender, RoutedEventArgs e)
        {
            Window_TagStamp window_TagStamp = new Window_TagStamp();
            window_TagStamp.Owner = MainWindow;
            bool? dialog = window_TagStamp.ShowDialog();
            if ((bool)dialog) {
                string name = window_TagStamp.TagName;
                if (string.IsNullOrEmpty(name))
                    return;
                SolidColorBrush backgroundBrush = window_TagStamp.BackgroundBrush;
                SolidColorBrush ForegroundBrush = window_TagStamp.ForegroundBrush;

                TagStamp tagStamp = new TagStamp() {
                    TagName = name,
                    Foreground = VisualHelper.SerializeBrush(ForegroundBrush),
                    Background = VisualHelper.SerializeBrush(backgroundBrush),
                };
                tagStampMapper.Insert(tagStamp);
                InitTagStamp();

            }
        }

        public void InitTagStamp()
        {
            // 记住之前的状态
            List<TagStamp> tagStamps = TagStamps.ToList();
            TagStamp.TagStamps = tagStampMapper.GetAllTagStamp();
            if (tagStamps != null && tagStamps.Count > 0) {
                foreach (var item in TagStamp.TagStamps) {
                    TagStamp tagStamp = tagStamps.FirstOrDefault(arg => arg.TagID == item.TagID);
                    if (tagStamp != null)
                        item.Selected = tagStamp.Selected;
                }
            }
            LoadTagStamp(tagStamps);
        }

        private void EditTagStamp(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            ContextMenu contextMenu = menuItem.Parent as ContextMenu;
            string tag = (contextMenu.PlacementTarget as PathCheckButton).Tag.ToString();
            long.TryParse(tag, out long id);
            if (id <= 0)
                return;

            TagStamp tagStamp = TagStamp.TagStamps.Where(arg => arg.TagID == id).FirstOrDefault();
            Window_TagStamp window_TagStamp = new Window_TagStamp(tagStamp.TagName, tagStamp.BackgroundBrush, tagStamp.ForegroundBrush);
            bool? dialog = window_TagStamp.ShowDialog();
            if ((bool)dialog) {
                string name = window_TagStamp.TagName;
                if (string.IsNullOrEmpty(name))
                    return;
                SolidColorBrush backgroundBrush = window_TagStamp.BackgroundBrush;
                SolidColorBrush ForegroundBrush = window_TagStamp.ForegroundBrush;
                tagStamp.TagName = name;
                tagStamp.Background = VisualHelper.SerializeBrush(backgroundBrush);
                tagStamp.Foreground = VisualHelper.SerializeBrush(ForegroundBrush);
                tagStampMapper.UpdateById(tagStamp);
                InitTagStamp();
                onTagStampRefresh?.Invoke(id);
            }
        }


        private void DeleteTagStamp(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            ContextMenu contextMenu = menuItem.Parent as ContextMenu;
            string tag = (contextMenu.PlacementTarget as PathCheckButton).Tag.ToString();
            long.TryParse(tag, out long id);
            if (id <= 0)
                return;
            TagStamp tagStamp = TagStamp.TagStamps.Where(arg => arg.TagID == id).FirstOrDefault();
            if (tagStamp.IsSystemTag()) {
                MessageNotify.Error(LangManager.GetValueByKey("CanNotDeleteDefaultTag"));
                return;
            }


            if (new MsgBox(SuperControls.Style.LangManager.GetValueByKey("IsToDelete") + $"{LangManager.GetValueByKey("TagStamp")} 【{tagStamp.TagName}】").ShowDialog() == true) {
                tagStampMapper.DeleteById(id);

                // 删除
                string sql = $"delete from metadata_to_tagstamp where TagID={tagStamp.TagID};";
                tagStampMapper.ExecuteNonQuery(sql);
                InitTagStamp();
                onTagStampDelete?.Invoke(tagStamp.TagID);
            }
        }


        private void SetTagStampsSelected(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;
            bool allChecked = (bool)toggleButton.IsChecked;
            ItemsControl itemsControl = TagStampItemsControl;
            for (int i = 0; i < itemsControl.Items.Count; i++) {
                ContentPresenter presenter = (ContentPresenter)itemsControl.ItemContainerGenerator.ContainerFromItem(itemsControl.Items[i]);
                if (presenter == null)
                    continue;
                PathCheckButton button = VisualHelper.FindElementByName<PathCheckButton>(presenter, "pathCheckButton");
                if (button == null)
                    continue;
                button.IsChecked = allChecked;
            }
            ApplyFilter();
        }


        private void TagStamp_Expand(object sender, EventArgs e)
        {
            if (sender is TogglePanel panel && panel.IsLoaded && panel.IsExpanded)
                InitTagStamp();
        }

        #region "标记拖拽排序"

        /// <summary>
        /// 拖拽传递的数据格式：标记 TagID
        /// </summary>
        private const string TagReorderFormat = "JvedioTagReorder";

        /// <summary>
        /// 按下手柄启动拖拽（手柄专用于拖动，按下即开始，无需位移阈值判断）
        /// </summary>
        private void TagGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is FrameworkElement grip) || !(grip.DataContext is TagStamp stamp))
                return;
            DataObject data = new DataObject(TagReorderFormat, stamp.TagID);
            DragDrop.DoDragDrop(grip, data, DragDropEffects.Move);
            e.Handled = true;
        }

        private void TagRow_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(TagReorderFormat)) {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        /// <summary>
        /// 放到某一行上：移动到目标行的位置（插到目标行之前）
        /// </summary>
        private void TagRow_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(TagReorderFormat))
                return;
            if (!(sender is FrameworkElement row) || !(row.DataContext is TagStamp target))
                return;
            long tagID;
            try {
                tagID = (long)e.Data.GetData(TagReorderFormat);
            } catch {
                return;
            }
            ReorderTag(tagID, TagStamps.IndexOf(target));
            e.Handled = true;
        }

        /// <summary>
        /// 放到列表空白处：移动到末尾
        /// </summary>
        private void TagList_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(TagReorderFormat))
                return;
            long tagID;
            try {
                tagID = (long)e.Data.GetData(TagReorderFormat);
            } catch {
                return;
            }
            ReorderTag(tagID, TagStamps.Count - 1);
            e.Handled = true;
        }

        /// <summary>
        /// 把标记移动到最终列表的 insertIndex 位置（移动的是同一实例，勾选/计数状态保留），并持久化排序
        /// </summary>
        private void ReorderTag(long tagID, int insertIndex)
        {
            TagStamp source = TagStamps.FirstOrDefault(arg => arg.TagID == tagID);
            if (source == null)
                return;
            int oldIndex = TagStamps.IndexOf(source);
            if (oldIndex < 0)
                return;
            if (insertIndex < 0)
                insertIndex = TagStamps.Count - 1;
            TagStamps.RemoveAt(oldIndex);
            if (insertIndex > oldIndex)
                insertIndex--;
            insertIndex = Math.Max(0, Math.Min(insertIndex, TagStamps.Count));
            TagStamps.Insert(insertIndex, source);
            if (insertIndex != oldIndex)
                PersistTagOrder();
        }

        /// <summary>
        /// 把当前显示顺序写入 common_tagstamp.SortOrder（一个事务批量更新），并同步全局缓存顺序
        /// </summary>
        private void PersistTagOrder()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder("begin;");
            for (int i = 0; i < TagStamps.Count; i++)
                builder.Append($"update common_tagstamp set SortOrder={i} where TagID={TagStamps[i].TagID};");
            builder.Append("commit;");
            try {
                tagStampMapper.ExecuteNonQuery(builder.ToString());
                // 同步全局缓存顺序（其他消费方如 InitTagStamp/右键标记菜单按此顺序展示）
                TagStamp.TagStamps = TagStamps.ToList();
            } catch (Exception ex) {
                App.Logger.Error(ex);
            }
        }

        #endregion

        private void Common_Expand(object sender, EventArgs e)
        {
            if (sender is TogglePanel panel && panel.IsLoaded && panel.IsExpanded)
                SetCommonFilter();
        }

        private void Genre_Expand(object sender, EventArgs e)
        {
            if (sender is TogglePanel panel && panel.IsLoaded && panel.IsExpanded)
                LoadGenre();

        }

        private void Series_Expand(object sender, EventArgs e)
        {
            if (sender is TogglePanel panel && panel.IsLoaded && panel.IsExpanded && SeriesLoad != LoadState.Loaded)
                LoadSeries();
        }

        private void Director_Expand(object sender, EventArgs e)
        {
            if (sender is TogglePanel panel && panel.IsLoaded && panel.IsExpanded && DirectorLoad != LoadState.Loaded)
                LoadDirector();

        }

        private void Studio_Expand(object sender, EventArgs e)
        {
            if (sender is TogglePanel panel && panel.IsLoaded && panel.IsExpanded && StudioLoad != LoadState.Loaded)
                LoadStudio();
        }

        public void LoadGenre()
        {
            LoadSingleDataFromMetaData(genreWrapPanel, "Genre"); // 类别
        }
        public void LoadSeries()
        {
            LoadSingleData(seriesWrapPanel, "Series", () => SeriesLoad = LoadState.Loading, () => SeriesLoad = LoadState.Loaded, (value) => SeriesProgress = value); // 系列
        }
        public void LoadDirector()
        {
            LoadSingleData(directorWrapPanel, "Director", () => DirectorLoad = LoadState.Loading, () => DirectorLoad = LoadState.Loaded, (value) => DirectorProgress = value); // 系列
        }
        public void LoadStudio()
        {
            LoadSingleData(studioWrapPanel, "Studio", () => StudioLoad = LoadState.Loading, () => StudioLoad = LoadState.Loaded, (value) => StudioProgress = value); // 系列
        }


        private void SetAllSelected(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton panel &&
                panel.Parent is FrameworkElement ele &&
                ele.Parent is DockPanel dockPanel &&
                dockPanel.Children.OfType<WrapPanel>().Last() is WrapPanel wrapPanel) {
                List<ToggleButton> list = wrapPanel.Children.OfType<ToggleButton>().ToList();

                bool all = (bool)panel.IsChecked;

                if (list != null) {
                    foreach (ToggleButton item in list) {
                        item.IsChecked = all;
                    }
                }


            }
        }

        private void ApplyFilter(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private async void SetPlayable(object sender, RoutedEventArgs e)
        {
            // 「可播放/不可播放」筛选依赖资源存在性索引（metadata.PathExist），而索引只是
            // 上次重建时的快照：文件被外部增删/移动、或建立索引时移动硬盘/网络盘未就绪，
            // 都会让索引与磁盘实际状态相反（可播放筛出不可播放、不可播放筛出可播放）。
            // 因此选中「不可播放/可播放」时，先按当前磁盘状态现场重建索引，再应用筛选。
            if (sender is RadioButton button &&
                playWrapPanel.Children.OfType<RadioButton>().ToList() is List<RadioButton> plays &&
                plays.IndexOf(button) > 0) {

                VideoList.onWaiting?.Invoke(LangManager.GetValueByKey("VerifyingFileStatus"), true);
                bool ok = false;
                try {
                    ok = await DataIndexManager.RebuildAsync();
                } finally {
                    VideoList.onWaiting?.Invoke("", false);
                }
                if (ok && !ConfigManager.Settings.PlayableIndexCreated) {
                    // 现场重建成功，索引已可用，无需用户再手动到【选项-库】建立
                    ConfigManager.Settings.PlayableIndexCreated = true;
                    ConfigManager.Settings.Save();
                }
            }

            if (!ConfigManager.Settings.PlayableIndexCreated) {
                MessageNotify.Error(LangManager.GetValueByKey("PleaseSetExistsIndex"));
                return;
            }
            ApplyFilter();
        }
        

        private RadioButton _lastCheckedPosterRadio = null;
        private RadioButton _lastCheckedThumbRadio = null;
        private RadioButton _lastCheckedActorRadio = null;
        private RadioButton _lastCheckedSubRadio = null;

        private void SetPosterExist(object sender, RoutedEventArgs e)
        {
            if (!ConfigManager.Settings.PictureIndexCreated) {
                MessageNotify.Error(LangManager.GetValueByKey("PleaseSetImageIndex"));
                return;
            }
            RadioButton clicked = sender as RadioButton;
            if (clicked == _lastCheckedPosterRadio) {
                clicked.IsChecked = false;
                _lastCheckedPosterRadio = null;
            } else {
                _lastCheckedPosterRadio = clicked;
            }
            ApplyFilter();
        }

        private void SetThumbnailExist(object sender, RoutedEventArgs e)
        {
            if (!ConfigManager.Settings.PictureIndexCreated) {
                MessageNotify.Error(LangManager.GetValueByKey("PleaseSetImageIndex"));
                return;
            }
            RadioButton clicked = sender as RadioButton;
            if (clicked == _lastCheckedThumbRadio) {
                clicked.IsChecked = false;
                _lastCheckedThumbRadio = null;
            } else {
                _lastCheckedThumbRadio = clicked;
            }
            ApplyFilter();
        }

        private void SetActorExist(object sender, RoutedEventArgs e)
        {
            RadioButton clicked = sender as RadioButton;
            if (clicked == _lastCheckedActorRadio) {
                clicked.IsChecked = false;
                _lastCheckedActorRadio = null;
            } else {
                _lastCheckedActorRadio = clicked;
            }
            ApplyFilter();
        }

        private async void SetSubtitleExist(object sender, RoutedEventArgs e)
        {
            // 与「可播放」同理：字幕索引（metadata.SubtitleExist）只是上次重建时的快照，
            // 点击「有字幕/无字幕」时先按当前磁盘状态现场重建，再应用筛选
            RadioButton clicked = sender as RadioButton;
            if (clicked == _lastCheckedSubRadio) {
                clicked.IsChecked = false;
                _lastCheckedSubRadio = null;
            } else {
                _lastCheckedSubRadio = clicked;
            }

            VideoList.onWaiting?.Invoke(LangManager.GetValueByKey("VerifyingFileStatus"), true);
            try {
                await DataIndexManager.RebuildAsync();
            } finally {
                VideoList.onWaiting?.Invoke("", false);
            }
            ApplyFilter();
        }

        
        private void ResetToDefault()
        {
            OnlyShowSubsection.IsChecked = false;

            var playRadios = playWrapPanel.Children.OfType<RadioButton>().ToList();
            for (int i = 0; i < playRadios.Count; i++) playRadios[i].IsChecked = (i == 0);

            var videoTypes = videoTypeWrapPanel.Children.OfType<ToggleButton>().ToList();
            for (int i = 0; i < videoTypes.Count; i++) videoTypes[i].IsChecked = (i == 0);

            var posterRadios = posterExistWrapPanel.Children.OfType<RadioButton>().ToList();
            posterRadios.ForEach(rb => rb.IsChecked = false);
            _lastCheckedPosterRadio = null;
            var thumbRadios = thumbnailExistWrapPanel.Children.OfType<RadioButton>().ToList();
            thumbRadios.ForEach(rb => rb.IsChecked = false);
            _lastCheckedThumbRadio = null;
            var actorRadios = actorExistWrapPanel.Children.OfType<RadioButton>().ToList();
            actorRadios.ForEach(rb => rb.IsChecked = false);
            _lastCheckedActorRadio = null;
            var subRadios = subtitleExistWrapPanel.Children.OfType<RadioButton>().ToList();
            subRadios.ForEach(rb => rb.IsChecked = false);
            _lastCheckedSubRadio = null;

            rateSlider.MinValue = rateSlider.Minimum;
            rateSlider.MaxValue = rateSlider.Maximum;

            var timeRadios = timeWrapPanel.Children.OfType<ToggleButton>().ToList();
            for (int i = 0; i < timeRadios.Count; i++) timeRadios[i].IsChecked = (i == 0);

            var sizeRadios = sizeWrapPanel.Children.OfType<RadioButton>().ToList();
            for (int i = 0; i < sizeRadios.Count; i++) sizeRadios[i].IsChecked = (i == 0);

            var yearToggles = yearWrapPanel.Children.OfType<ToggleButton>().ToList();
            yearToggles.ForEach(t => t.IsChecked = false);
            var monthToggles = monthWrapPanel.Children.OfType<ToggleButton>().ToList();
            monthToggles.ForEach(t => t.IsChecked = false);

            var genreToggles = genreWrapPanel.Children.OfType<ToggleButton>().ToList();
            genreToggles.ForEach(t => t.IsChecked = false);
            var seriesToggles = seriesWrapPanel.Children.OfType<ToggleButton>().ToList();
            seriesToggles.ForEach(t => t.IsChecked = false);
            var directorToggles = directorWrapPanel.Children.OfType<ToggleButton>().ToList();
            directorToggles.ForEach(t => t.IsChecked = false);
            var studioToggles = studioWrapPanel.Children.OfType<ToggleButton>().ToList();
            studioToggles.ForEach(t => t.IsChecked = false);

            // 全选按钮复位 + 清空类别搜索（TextChanged 会自动恢复所有标签可见）
            genreSelectAll.IsChecked = false;
            seriesSelectAll.IsChecked = false;
            directorSelectAll.IsChecked = false;
            studioSelectAll.IsChecked = false;
            if (genreSearchBox != null)
                genreSearchBox.Text = string.Empty;

            ItemsControl itemsControl = TagStampItemsControl;
            for (int i = 0; i < itemsControl.Items.Count; i++) {
                ContentPresenter presenter = (ContentPresenter)itemsControl.ItemContainerGenerator.ContainerFromItem(itemsControl.Items[i]);
                if (presenter == null) continue;
                PathCheckButton button = VisualHelper.FindElementByName<PathCheckButton>(presenter, "pathCheckButton");
                if (button == null) continue;
                button.IsChecked = true;
            }
        }


        public void ApplyFilter()
        {
            SelectWrapper<Video> wrapper = new SelectWrapper<Video>();
            // 1.标签戳

            string sql = "";



            // 标记
            if (TagStamps != null && TagStamps.Count > 0) {
                sql += VideoMapper.SQL_LEFT_JOIN_TAGSTAMP;
                bool allFalse = TagStamps.All(item => !item.Selected);
                bool allTrue = TagStamps.All(item => item.Selected);
                if (allFalse) {
                    wrapper.Eq("metadata.DataID", -1);
                } else if (!allTrue) {
                    wrapper.In("metadata_to_tagstamp.TagID", TagStamps.Where(item => item.Selected).Select(item => item.TagID.ToString()));
                }
            }


            // 分段视频

            // 是否可播放
            if (ConfigManager.Settings.PlayableIndexCreated) {
                List<RadioButton> plays = playWrapPanel.Children.OfType<RadioButton>().ToList();
                int idx = 0;
                for (int i = 0; i < plays.Count; i++) {
                    if ((bool)plays[i].IsChecked) {
                        idx = i;
                        break;
                    }
                }
                if (idx > 0) {
                    wrapper.Eq("metadata.PathExist", idx - 1);
                }
            }

            // 视频类型
            List<ToggleButton> allMenus = videoTypeWrapPanel.Children.OfType<ToggleButton>().ToList();
            List<ToggleButton> checkedMenus = allMenus.Where(t => (bool)t.IsChecked).ToList();
            int checkedCount = checkedMenus.Count;

            string field = "";

            if (checkedCount > 0 && checkedCount < 4) {
                field = "VideoType";
                if (checkedCount == 1) {
                    int idx = allMenus.IndexOf(checkedMenus[0]) - 1;
                    if (idx >= 0)
                        wrapper.Eq(field, idx);
                } else if (checkedCount == 2) {
                    int idx1 = allMenus.IndexOf(checkedMenus[0]) - 1;
                    int idx2 = allMenus.IndexOf(checkedMenus[1]) - 1;
                    if (idx1 >= 0 && idx2 >= 0)
                        wrapper.Eq(field, idx1).LeftBracket().Or().Eq(field, idx2).RightBracket();
                } else if (checkedCount == 3) {
                    int idx1 = allMenus.IndexOf(checkedMenus[0]) - 1;
                    int idx2 = allMenus.IndexOf(checkedMenus[1]) - 1;
                    int idx3 = allMenus.IndexOf(checkedMenus[2]) - 1;
                    if (idx1 >= 0 && idx2 >= 0 && idx3 >= 0)
                        wrapper.Eq(field, idx1).LeftBracket().Or().Eq(field, idx2).Or().Eq(field, idx3).RightBracket();
                }
            }


            // 1. 仅显示分段视频
            if ((bool)OnlyShowSubsection.IsChecked)
                wrapper.NotEq("SubSection", string.Empty);

            // 图片存在性
            if (ConfigManager.Settings.PictureIndexCreated) {
                int posterSel = -1;
                var posterRadios = posterExistWrapPanel.Children.OfType<RadioButton>().ToList();
                for (int i = 0; i < posterRadios.Count; i++) {
                    if ((bool)posterRadios[i].IsChecked) { posterSel = i; break; }
                }
                int thumbSel = -1;
                var thumbRadios = thumbnailExistWrapPanel.Children.OfType<RadioButton>().ToList();
                for (int i = 0; i < thumbRadios.Count; i++) {
                    if ((bool)thumbRadios[i].IsChecked) { thumbSel = i; break; }
                }
                if (posterSel >= 0) {
                    sql += $" LEFT JOIN common_picture_exist cpe_p on cpe_p.DataID=metadata.DataID AND cpe_p.PathType={ConfigManager.Settings.PicPathMode} AND cpe_p.ImageType=1 AND cpe_p.Exist=1";
                    if (posterSel == 0)
                        wrapper.Eq("cpe_p.Exist", 1);
                    else
                        wrapper.IsNull("cpe_p.DataID");
                }
                if (thumbSel >= 0) {
                    sql += $" LEFT JOIN common_picture_exist cpe_t on cpe_t.DataID=metadata.DataID AND cpe_t.PathType={ConfigManager.Settings.PicPathMode} AND cpe_t.ImageType=0 AND cpe_t.Exist=1";
                    if (thumbSel == 0)
                        wrapper.Eq("cpe_t.Exist", 1);
                    else
                        wrapper.IsNull("cpe_t.DataID");
                }
            }

            // 演员信息
            int actorSel = -1;
            var actorRadios = actorExistWrapPanel.Children.OfType<RadioButton>().ToList();
            for (int i = 0; i < actorRadios.Count; i++) {
                if ((bool)actorRadios[i].IsChecked) { actorSel = i; break; }
            }
            if (actorSel >= 0) {
                sql += " LEFT JOIN metadata_to_actor mta on mta.DataID=metadata.DataID";
                if (actorSel == 0)
                    wrapper.Ge("mta.ActorID", 1);
                else
                    wrapper.IsNull("mta.ActorID");
            }

            // 有无字幕（根据外挂 SRT 文件建立的索引）
            int subSel = -1;
            var subRadios = subtitleExistWrapPanel.Children.OfType<RadioButton>().ToList();
            for (int i = 0; i < subRadios.Count; i++) {
                if ((bool)subRadios[i].IsChecked) { subSel = i; break; }
            }
            if (subSel >= 0) {
                wrapper.Eq("metadata.SubtitleExist", subSel == 0 ? 1 : 0);
            }

            // 时长
            List<ToggleButton> timeList = timeWrapPanel.Children.OfType<ToggleButton>().ToList();
            ToggleButton timeButton = timeList.FirstOrDefault(item => (bool)item.IsChecked);
            if (timeButton != null && timeList.IndexOf(timeButton) is int timeIndex && timeIndex > 0) {
                field = "Duration";
                if (timeIndex < 5) {
                    wrapper.Ge(field, TimeList[timeIndex - 1]).Le(field, TimeList[timeIndex]);
                } else if (timeIndex == 5) {
                    wrapper.Ge(field, TimeList[timeIndex]);
                }
            }

            // 文件大小
            List<RadioButton> sizeList = sizeWrapPanel.Children.OfType<RadioButton>().ToList();
            RadioButton sizeButton = sizeList.FirstOrDefault(item => (bool)item.IsChecked);
            if (sizeButton != null && sizeList.IndexOf(sizeButton) is int sizeIndex && sizeIndex > 0) {
                field = "Size";
                if (sizeIndex < 5) {
                    wrapper.Ge(field, SizeList[sizeIndex - 1]).Le(field, SizeList[sizeIndex]);
                } else if (sizeIndex == 5) {
                    wrapper.Ge(field, SizeList[sizeIndex - 1]);
                }
            }

            // 评分
            double minRate = rateSlider.MinValue;
            double maxRate = rateSlider.MaxValue;
            if (minRate != rateSlider.Minimum || maxRate != rateSlider.Maximum) {
                field = "Grade";
                if (minRate == maxRate) {
                    wrapper.Eq(field, minRate);
                } else {
                    wrapper.Ge(field, minRate);
                    wrapper.Le(field, maxRate);
                }
            }

            // 年份（ReleaseYear 列全库为 0 从未填充，改用 ReleaseDate 前四位；
            // 单选年份不能走「Eq().LeftBracket().Or()」——wrapper 的 Where 按 值去重，
            // 单值时第二个 Eq 被去重、RightBracket 落回首个条件，Or=true 残留会让本组
            // 与其他筛选组之间变成 OR（并集）而非 AND（交集），跨组筛选结果错误）
            List<ToggleButton> yearList = yearWrapPanel.Children.OfType<ToggleButton>().Where(item => (bool)item.IsChecked).ToList();
            if (yearList.Count > 0 && yearList.Count != yearWrapPanel.Children.Count) {
                field = "substr(metadata.ReleaseDate, 1, 4)";
                int count = yearList.Count;
                List<int> list = yearList.Select(item => int.Parse(item.Content.ToString())).ToList();
                if (count == 1) {
                    wrapper.Eq(field, list[0]);
                } else {
                    wrapper.Eq(field, list[0]).LeftBracket().Or();
                    for (int i = 1; i < count - 1; i++) {
                        wrapper.Eq(field, list[i]).Or();
                    }
                    wrapper.Eq(field, list[count - 1]).RightBracket();
                }
            }

            // 月份
            //List<ToggleButton> monthList = monthWrapPanel.Children.OfType<ToggleButton>().Where(item => (bool)item.IsChecked).ToList();
            //if (monthList.Count > 0 && monthList.Count != monthWrapPanel.Children.Count) {
            //    field = "ReleaseDate";
            //    int count = monthList.Count;
            //    List<int> list = monthList.Select(item => int.Parse(item.Content.ToString())).ToList();
            //    wrapper.Eq(field, list[0]).LeftBracket().Or();
            //    for (int i = 1; i < count - 1; i++) {
            //        wrapper.Eq(field, list[i]).Or();
            //    }
            //    wrapper.Eq(field, list[count - 1]).RightBracket();

            //}

            // 类别（整标签匹配：勾选「高」只命中标签恰好为「高」的影片，不会命中「高画质」）
            List<ToggleButton> genreList = genreWrapPanel.Children.OfType<ToggleButton>().Where(item => (bool)item.IsChecked).ToList();
            if (genreList.Count > 0 && genreList.Count != genreWrapPanel.Children.Count) {
                List<string> list = genreList.Select(item => item.Content.ToString()).ToList();
                AppendExactTagMatch(wrapper, "Genre", list);
            }

            // 系列
            List<ToggleButton> seriesList = seriesWrapPanel.Children.OfType<ToggleButton>().Where(item => (bool)item.IsChecked).ToList();
            if (seriesList.Count > 0 && seriesList.Count != seriesWrapPanel.Children.Count) {
                List<string> list = seriesList.Select(item => item.Content.ToString()).ToList();
                AppendExactTagMatch(wrapper, "Series", list);
            }

            // 导演
            List<ToggleButton> directorList = directorWrapPanel.Children.OfType<ToggleButton>().Where(item => (bool)item.IsChecked).ToList();
            if (directorList.Count > 0 && directorList.Count != directorWrapPanel.Children.Count) {
                List<string> list = directorList.Select(item => item.Content.ToString()).ToList();
                AppendExactTagMatch(wrapper, "Director", list);
            }

            // 制作商
            List<ToggleButton> studioList = studioWrapPanel.Children.OfType<ToggleButton>().Where(item => (bool)item.IsChecked).ToList();
            if (studioList.Count > 0 && studioList.Count != studioWrapPanel.Children.Count) {
                List<string> list = studioList.Select(item => item.Content.ToString()).ToList();
                AppendExactTagMatch(wrapper, "Studio", list);
            }

            WrapperEventArg<Video> arg = new WrapperEventArg<Video>(wrapper);
            arg.SQL = sql;

            OnApplyWrapper?.Invoke(this, arg);
        }

        private void SetAllChecked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton && toggleButton.Parent is WrapPanel panel &&
                panel.Children.OfType<ToggleButton>().ToList() is List<ToggleButton> list &&
                list.IndexOf(toggleButton) is int idx &&
                idx >= 0) {
                if (idx == 0) {
                    list.ForEach((arg) => arg.IsChecked = false);
                    toggleButton.IsChecked = true;
                } else {
                    list[0].IsChecked = !list.Any(arg => (bool)arg.IsChecked);
                }
            }
        }

        private WrapPanel GetWrapPanel(FrameworkElement ele)
        {
            if (ele.Parent is DockPanel panel &&
                panel.Parent is StackPanel stackPanel &&
                stackPanel.Children.OfType<ScrollViewer>().Last() is ScrollViewer viewer &&
                viewer.Content is WrapPanel wrapPanel)
                return wrapPanel;
            return null;
        }

        private void SetAllLabelChecked(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleButton button))
                return;
            // 目标面板优先从 Tag 取（XAML 里以 ElementName 绑定），不再依赖逻辑树层级遍历——
            // 原实现按「父 DockPanel → 父 StackPanel → 最后一个 ScrollViewer」向上找面板，
            // 一旦运行时树结构与假设不符就静默返回 null（无日志无反馈），全选表现为「失效」
            WrapPanel wrapPanel = button.Tag as WrapPanel ?? GetWrapPanel(button);
            if (wrapPanel == null)
                return;
            bool isChecked = (bool)button.IsChecked;
            foreach (object child in wrapPanel.Children) {
                // 搜索过滤生效时只勾选当前可见的标签（所见即所选）
                if (child is ToggleButton item && item.Visibility == Visibility.Visible)
                    item.IsChecked = isChecked;
            }
            // 与「标记」面板全选一致：点击后立即应用筛选，给出可见反馈
            ApplyFilter();
        }

        private void GenreSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyGenreFilter();
        }

        /// <summary>
        /// 按关键词实时过滤类别标签的可见性（空关键词 = 全部显示）
        /// </summary>
        private void ApplyGenreFilter()
        {
            if (genreSearchBox == null || genreWrapPanel == null)
                return;
            string keyword = genreSearchBox.Text?.Trim();
            string lower = string.IsNullOrEmpty(keyword) ? null : keyword.ToLower();
            foreach (object child in genreWrapPanel.Children) {
                if (!(child is ToggleButton item))
                    continue;
                bool match = lower == null || (item.Content == null ? string.Empty : item.Content.ToString()).ToLower().Contains(lower);
                item.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 「整标签」匹配条件：Genre/Series/Director/Studio 等列为分隔符（\a）拼接的多值字符串，
        /// 原实现 Genre LIKE '%高%' 是子串匹配——勾选「高」会把「高画质」「高挑」等一切含「高」的标签都命中。
        /// 这里改为「列两侧补分隔符后再 LIKE '%\a高\a%'」，只命中完整的标签段；
        /// 对单个标签（Genre='高'）、首位、中位、末位标签均正确（NULL 列不命中任何标签）。
        /// 注意：单标签不能走「Like().LeftBracket().Or()」模式——wrapper 的 Where 按 值去重，
        /// 第二次 Like 被去重后 RightBracket 落回首个条件，Or=true 残留会使本组与
        /// 其他筛选组之间变成 OR（并集）而非 AND（交集）。
        /// </summary>
        private void AppendExactTagMatch(SelectWrapper<Video> wrapper, string field, List<string> tags)
        {
            char sep = SuperUtils.Values.ConstValues.Separator;
            // 字段表达式两侧补分隔符：char(7)||Genre||char(7)，使首尾标签与中间标签判定一致
            string wrappedField = $"char({(int)sep})||{field}||char({(int)sep})";
            int count = tags.Count;
            if (count == 1) {
                wrapper.Like(wrappedField, $"{sep}{tags[0]}{sep}");
                return;
            }
            wrapper.Like(wrappedField, $"{sep}{tags[0]}{sep}").LeftBracket().Or();
            for (int i = 1; i < count - 1; i++) {
                wrapper.Like(wrappedField, $"{sep}{tags[i]}{sep}").Or();
            }
            wrapper.Like(wrappedField, $"{sep}{tags[count - 1]}{sep}").RightBracket();
        }
    }



    public enum LoadState
    {
        None,
        Loading,
        Loaded,
    }



    public class LoadStatusConverter : IValueConverter
    {
        // 数字转换为选中项的地址
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || string.IsNullOrEmpty(value.ToString())) {
                return Visibility.Collapsed;
            }

            Enum.TryParse(value.ToString(), out LoadState state);

            if (state == LoadState.Loading) {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        // 选中项地址转换为数字
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
