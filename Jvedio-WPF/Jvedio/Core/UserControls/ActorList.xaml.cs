using Jvedio.Core.Utils;
using Jvedio.Entity;
using SuperControls.Style;
using SuperControls.Style.Windows;
using SuperUtils.Framework.ORM.Utils;
using SuperUtils.Framework.ORM.Wrapper;
using SuperUtils.IO;
using SuperUtils.WPF.VisualTools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using static Jvedio.App;
using static Jvedio.MapperManager;
using static SuperUtils.WPF.VisualTools.VisualHelper;

namespace Jvedio.Core.UserControls
{
    /// <summary>
    /// ActorsPage.xaml 的交互逻辑
    /// </summary>
    public partial class ActorList : UserControl, ITabItemControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #region "事件"

        public Action<long, float> onGradeChange;
        public event EventHandler PageChangedCompleted;
        public Action<long> onShowSameActor;
        public static Action onStatistic;


        private delegate void LoadActorDelegate(ActorInfo actor, int idx);



        #endregion

        #region "静态属性"



        public static Dictionary<int, string> SortDict { get; set; } = new Dictionary<int, string>();


        /// <summary>
        /// 排序字段表：索引与排序右键菜单项一一对应（0=我的收藏…17=鞋码）。
        /// 注意此前多了一项 Grade 且 BirthPlace/Birthday 错位，导致「血型」起全部字段错位一格、
        /// 「年龄」实际按评分排序——表现为排序无效，已对齐（SQL 复合查询的 ORDER BY 已实测
        /// 带表前缀/裸列名均可，见 build-output/theme-probe/SortProbe）
        /// </summary>
        public static List<string> SortDictList { get; set; } = new List<string>()
        {
            "actor_info.Grade",     // 0 我的收藏
            "actor_info.ActorName", // 1 名称
            "Count",                // 2 作品数量
            "actor_info.Country",   // 3 国家
            "Nation",               // 4 民族
            "Birthday",             // 5 生日
            "BloodType",            // 6 血型
            "Height",               // 7 高度
            "Weight",               // 8 体重
            "Gender",               // 9 性别
            "Hobby",                // 10 爱好
            "Cup",                  // 11 罩杯
            "Chest",                // 12 胸围
            "Waist",                // 13 腰围
            "Hipline",              // 14 臀围
            "actor_info.Grade",     // 15 评分
            "Age",                  // 16 年龄
            "ShoeSize",             // 17 鞋码
        };


        public static string[] SelectedField { get; set; } = new string[]
        {
            "count(ActorName) as Count",
            "actor_info.ActorID",
            "actor_info.ActorName",
            "actor_info.Country",
            "Nation",
            "BirthPlace",
            "Birthday",
            "BloodType",
            "Height",
            "Weight",
            "Gender",
            "Hobby",
            "Cup",
            "ShoeSize",
            "Chest",
            "Age",
            "Waist",
            "Hipline",
            "WebType",
            "WebUrl",
            "actor_info.Grade",
            "actor_info.ExtraInfo",
            "actor_info.CreateDate",
            "actor_info.UpdateDate",
        };
        
        public static string[] SelectedFieldUnion { get; set; }  = SelectedField.ToArray();


        public static Dictionary<string, string> Actor_SELECT_TYPE { get; set; } = new Dictionary<string, string>()
        {
            { "All", "  " },
            { "Favorite", "  " },
        };



        #endregion

        #region "属性"

        private Queue<int> PageQueue { get; set; } = new Queue<int>();
        private int firstIdx { get; set; } = -1;
        private int secondIdx { get; set; } = -1;
        private int actorFirstIdx { get; set; } = -1;
        private int actorSecondIdx { get; set; } = -1;

        public CancellationTokenSource RenderCTS { get; set; }

        public CancellationToken RenderCT { get; set; }

        private bool _Nothing = true;

        public bool Nothing {
            get { return _Nothing; }

            set {
                _Nothing = value;
                RaisePropertyChanged();
            }
        }
        private bool _ShowTable = false;

        public bool ShowTable {
            get { return _ShowTable; }

            set {
                _ShowTable = value;
                RaisePropertyChanged();
            }
        }
        private string _SearchText = string.Empty;

        public string SearchText {
            get { return _SearchText; }

            set {
                _SearchText = value;
                RaisePropertyChanged();

                // BeginSearch();
            }
        }

        private int _PageSize = 40;

        public int PageSize {
            get { return _PageSize; }

            set {
                _PageSize = value;
                RaisePropertyChanged();
            }
        }

        private long _TotalCount = 0;

        public long TotalCount {
            get { return _TotalCount; }

            set {
                _TotalCount = value;
                RaisePropertyChanged();
            }
        }


        private int _CurrentPage = 1;

        public int CurrentPage {
            get { return _CurrentPage; }

            set {
                _CurrentPage = value;

                RaisePropertyChanged();
            }
        }

        private List<ActorInfo> _Actors;

        public List<ActorInfo> Actors {
            get { return _Actors; }

            set {
                _Actors = value;
                RaisePropertyChanged();
            }
        }

        private ObservableCollection<ActorInfo> _CurrentList;

        public ObservableCollection<ActorInfo> CurrentList {
            get { return _CurrentList; }

            set {
                _CurrentList = value;
                RaisePropertyChanged();
            }
        }


        public int _CurrentCount = 0;

        public int CurrentCount {
            get { return _CurrentCount; }

            set {
                _CurrentCount = value;
                RaisePropertyChanged();
            }
        }

        private List<ActorInfo> _SelectedActors = new List<ActorInfo>();

        public List<ActorInfo> SelectedActors {
            get { return _SelectedActors; }

            set {
                _SelectedActors = value;
                RaisePropertyChanged();
            }
        }


        private int _RenderProgress;

        public int RenderProgress {
            get { return _RenderProgress; }

            set {
                _RenderProgress = value;
                RaisePropertyChanged();
            }
        }


        private bool _rendering;
        public bool Rendering {
            get { return _rendering; }
            set {
                _rendering = value;
                RaisePropertyChanged();
            }
        }

        #endregion


        #region "控件属性"

        public static readonly DependencyProperty ViewModeProperty = DependencyProperty.Register(
nameof(ViewMode), typeof(bool), typeof(ActorList), new PropertyMetadata(false));

        public bool ViewMode {
            get { return (bool)GetValue(ViewModeProperty); }
            set {
                SetValue(ViewModeProperty, value);
            }
        }

        #endregion


        public ActorList()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            this.DataContext = this;
            ConfigManager.VideoConfig.ActorEditMode = false;

            SetDataGrid();
        }

        static ActorList()
        {
            for (int i = 0; i < SortDictList.Count; i++) {
                SortDict.Add(i, SortDictList[i]);
            }
            SelectedFieldUnion[0] = "CASE WHEN count(ActorName) = 1 THEN 0 ELSE 1 END as Count";
        }


        private void ActorList_Loaded(object sender, RoutedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            if (sender is ActorList actorList && actorList.IsLoaded) {
                RefreshActorRenderToken();
                BindingEvent();
                Select();
            }
        }

        public void Refresh(int page = -1)
        {
            if (page > 0 && CurrentPage != page) {
                CurrentPage = page;
                pagination.CurrentPage = page;
            } else {
                Select();
            }
        }

        public void RefreshGrade(long actorID, float grade)
        {
            if (actorID <= 0)
                return;
            for (int i = 0; i < CurrentList.Count; i++) {
                if (CurrentList[i].ActorID == actorID) {
                    CurrentList[i].Grade = grade;
                    break;
                }
            }
        }

        public void BindingEvent()
        {
            // 设置演员排序类型
            int actorSortType = (int)ConfigManager.VideoConfig.ActorSortType;
            var SortMenuItems = SortBorder.ContextMenu.Items.OfType<MenuItem>().ToList();
            for (int i = 0; i < SortMenuItems.Count; i++) {
                SortMenuItems[i].Click += SortMenu_Click;
                SortMenuItems[i].IsCheckable = true;
                if (i == actorSortType)
                    SortMenuItems[i].IsChecked = true;
            }


            // 设置演员显示模式
            var arbs = ViewModeStackPanel.Children.OfType<PathRadioButton>().ToList();
            for (int i = 0; i < arbs.Count; i++) {
                arbs[i].Click += SetViewMode;
                if (i == ConfigManager.VideoConfig.ActorViewMode)
                    arbs[i].IsChecked = true;
            }


            this.PageChangedCompleted += (s, ev) => {
                //if (ConfigManager.VideoConfig.ActorEditMode)
                //    SetSelected();
                scrollViewer.ScrollToTop();
            };
        }

        public void SetViewMode(object sender, RoutedEventArgs e)
        {
            PathRadioButton radioButton = sender as PathRadioButton;
            if (radioButton == null)
                return;
            var rbs = ViewModeStackPanel.Children.OfType<PathRadioButton>().ToList();
            int idx = rbs.IndexOf(radioButton);
            ConfigManager.VideoConfig.ActorViewMode = idx;
            ConfigManager.VideoConfig.ActorEditMode = false;
            ConfigManager.VideoConfig.Save();
            SetDataGrid();
        }

        public void SetDataGrid()
        {
            if (ConfigManager.VideoConfig.ActorViewMode == 2) {
                ShowTable = true;
            } else {
                ShowTable = false;
            }
        }

        private void SortMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            ContextMenu contextMenu = menuItem.Parent as ContextMenu;
            for (int i = 0; i < contextMenu.Items.Count; i++) {
                MenuItem item = (MenuItem)contextMenu.Items[i];
                if (item == menuItem) {
                    item.IsChecked = true;
                    if (i == ConfigManager.VideoConfig.ActorSortType) {
                        ConfigManager.VideoConfig.ActorSortDescending = !ConfigManager.VideoConfig.ActorSortDescending;
                    }

                    ConfigManager.VideoConfig.ActorSortType = i;
                } else
                    item.IsChecked = false;
            }

            Select();
        }


        public void RefreshActorRenderToken()
        {
            RenderCTS = new CancellationTokenSource();
            RenderCTS.Token.Register(() => { Logger.Warn("cancel load actor page task"); });
            RenderCT = RenderCTS.Token;
        }

        public async void Select()
        {            if (ConfigManager.Main == null)
                return;

            // 判断当前获取的队列
            while (PageQueue.Count > 1) {
                int page = PageQueue.Dequeue();
            }

            // 当前有视频在渲染的时候，打断渲染，等待结束
            while (Rendering) {
                RenderCTS?.Cancel(); // 取消加载
                await Task.Delay(100);
            }

            SelectWrapper<ActorInfo> wrapper = new SelectWrapper<ActorInfo>();
            SetSortOrder(wrapper);

            bool search = !string.IsNullOrEmpty(SearchText);
            string searchText = SearchText.ToProperSql();

            string count_sql = "SELECT count(*) as Count " +
                         "from (SELECT actor_info.ActorID FROM actor_info join metadata_to_actor " +
                         "on metadata_to_actor.ActorID=actor_info.ActorID " +
                         "join metadata " +
                         "on metadata_to_actor.DataID=metadata.DataID " +
                         $"WHERE metadata.DBId={ConfigManager.Main.CurrentDBId} and metadata.DataType={0} " +
                         $"{(search ? $"and actor_info.ActorName like '%{searchText}%' " : string.Empty)} " +
                         "GROUP BY actor_info.ActorID " +
                         "UNION " +
                         "select actor_info.ActorID  " +
                         "FROM actor_info WHERE NOT EXISTS " +
                         "(SELECT 1 from metadata_to_actor where metadata_to_actor.ActorID=actor_info.ActorID ) " +
                         $"{(search ? $"and actor_info.ActorName like '%{searchText}%' " : string.Empty)} " +
                         "GROUP BY actor_info.ActorID)";


            TotalCount = actorMapper.SelectCount(count_sql);

            string filedSelect = new SelectWrapper<ActorInfo>().Select(SelectedField).ToSelect(false);
            string unionSelect = new SelectWrapper<ActorInfo>().Select(SelectedFieldUnion).ToSelect(false);

            string sql = $"{filedSelect} FROM actor_info " +
                $"join metadata_to_actor on metadata_to_actor.ActorID=actor_info.ActorID " +
                $"join metadata on metadata_to_actor.DataID=metadata.DataID " +
                $"WHERE metadata.DBId={ConfigManager.Main.CurrentDBId} and metadata.DataType={0} " +
                $"{(search ? $"and actor_info.ActorName like '%{searchText}%' " : string.Empty)} " +
                $"GROUP BY actor_info.ActorID " +
                "UNION " +
                // 显示只有演员名，没有数量的演员
                $"{unionSelect} FROM actor_info " +
                "WHERE NOT EXISTS(SELECT 1 from metadata_to_actor where metadata_to_actor.ActorID=actor_info.ActorID )" +
                $"{(search ? $"and actor_info.ActorName like '%{searchText}%' " : string.Empty)}  GROUP BY actor_info.ActorID " +
                wrapper.ToOrder() + ActorToLimit();

            onPageChange(TotalCount);
            Render(sql);
        }

        public void onPageChange(long total)
        {
            App.Current.Dispatcher.Invoke(() => pagination.Total = TotalCount);
        }



        public void SetSortOrder<T>(IWrapper<T> wrapper)
        {
            if (wrapper == null ||
                SortDict == null ||
                ConfigManager.VideoConfig == null ||
                ConfigManager.VideoConfig.ActorSortType >= SortDict.Count)
                return;
            string sortField = SortDict[(int)ConfigManager.VideoConfig.ActorSortType];
            if (ConfigManager.VideoConfig.ActorSortDescending)
                wrapper.Desc(sortField);
            else
                wrapper.Asc(sortField);
        }

        public string ActorToLimit()
        {
            int row_count = PageSize;
            long offset = PageSize * (CurrentPage - 1);
            return $" LIMIT {offset},{row_count}";
        }


        public void Render(string sql)
        {
            List<Dictionary<string, object>> list = actorMapper.Select(sql);
            List<ActorInfo> actors = actorMapper.ToEntity<ActorInfo>(list, typeof(ActorInfo).GetProperties(), false);
            Actors = new List<ActorInfo>();
            if (actors == null)
                actors = new List<ActorInfo>();
            Actors.AddRange(actors);


            CurrentCount = Actors.Count;

            RenderActor();
        }

        public async void RenderActor()
        {
            if (CurrentList == null) {
                CurrentList = new ObservableCollection<ActorInfo>();
                Nothing = true;
                CurrentList.CollectionChanged += (s, e) => {
                    Nothing = CurrentList.Count == 0;
                };
            }

            for (int i = 0; i < Actors.Count; i++) {
                try {
                    RenderCT.ThrowIfCancellationRequested();
                } catch (OperationCanceledException) {
                    RenderCTS?.Dispose();
                    break;
                }

                Rendering = true;
                ActorInfo actorInfo = Actors[i];
                ActorInfo.SetImage(ref actorInfo);
                // 编辑模式勾选状态在翻页/重渲染后保持(与 SelectedActors 同步)
                actorInfo.IsSelected = SelectedActors.Any(a => a.ActorID == actorInfo.ActorID);
                await App.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new LoadActorDelegate(LoadActor), actorInfo, i);
                RenderProgress = (int)(100 * (i + 1) / (float)Actors.Count);
            }

            // 清除
            for (int i = CurrentList.Count - 1; i > Actors.Count - 1; i--) {
                CurrentList.RemoveAt(i);
            }

            if (RenderCT.IsCancellationRequested)
                RefreshActorRenderToken();
            Rendering = false;

            // if (pageQueue.Count > 0) pageQueue.Dequeue();
            PageChangedCompleted?.Invoke(this, null);
        }

        private void LoadActor(ActorInfo actor, int idx)
        {
            if (RenderCT.IsCancellationRequested)
                return;
            if (CurrentList.Count < PageSize) {
                if (idx < CurrentList.Count) {
                    LoadActor(idx, actor);
                } else {
                    CurrentList.Add(actor);
                }
            } else {
                LoadActor(idx, actor);
            }
        }

        private void LoadActor(int idx, ActorInfo actorInfo)
        {
            if (CurrentList[idx].ActorID == actorInfo.ActorID) {
                // 不知为啥，如果 2 个对象相等，则不会触发 notify
                ActorInfo temp = CurrentList[idx];
                RefreshData(ref temp, actorInfo);
            } else {
                CurrentList[idx] = actorInfo;
            }
        }

        private void RefreshData(ref ActorInfo origin, ActorInfo target)
        {
            System.Reflection.PropertyInfo[] propertyInfos = target.GetType().GetProperties();
            foreach (var item in propertyInfos) {
                object v = item.GetValue(target);
                if (v != null) {
                    item.SetValue(origin, v);
                }
            }
            // 资料字段逐个反射写入,计算属性(信息卡片/资料串)不会自动感知,手动通知刷新,
            // 保证补齐结果刷新按钮后立即上屏
            origin.NotifyInfoDisplayChanged();
        }

        private void Page_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.Right) {
                // 末页
                //SetSelected();
            } else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.Left) {
                CurrentPage = 1;
                //SetSelected();

            } else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.A) {
                // Ctrl+A 全选本页演员；焦点在搜索框等文本框内时放行(Ctrl+A 应选中搜索文本)
                if (searchBox.IsKeyboardFocusWithin || Keyboard.FocusedElement is TextBox)
                    return;
                SelectAllCurrentPage();
                e.Handled = true;
            }
        }

        // 刷新按钮:重新从库加载演员信息(补齐结果立即上屏)
        public void RefreshActors(object sender, RoutedEventArgs e)
        {
            Select();
        }

        private void SetActorSelectMode(object sender, RoutedEventArgs e)
        {
            SelectedActors.Clear();
            // 关闭多选开关时清除所有蓝框
            if (CurrentList != null)
                foreach (ActorInfo actor in CurrentList)
                    if (actor != null)
                        actor.IsSelected = false;
        }

        /// <summary>
        /// 右键「全选」：一键勾选本页所有演员（与 Ctrl+A 共用 SelectAllCurrentPage）
        /// </summary>
        private void SelectAllActors(object sender, RoutedEventArgs e)
        {
            SelectAllCurrentPage();
        }

        /// <summary>
        /// 一键勾选本页所有演员。未打开多选开关时自动打开（开关经绑定联动），便于直接批量同步信息
        /// </summary>
        private void SelectAllCurrentPage()
        {
            if (!ConfigManager.VideoConfig.ActorEditMode)
                ConfigManager.VideoConfig.ActorEditMode = true;
            SelectedActors.Clear();
            if (CurrentList == null)
                return;
            foreach (ActorInfo actor in CurrentList) {
                if (actor == null || actor.ActorID <= 0)
                    continue;
                actor.IsSelected = true;
                if (!SelectedActors.Any(arg => arg.ActorID == actor.ActorID))
                    SelectedActors.Add(actor);
            }
        }

        private void CurrentActorPageChange(object sender, EventArgs e)
        {
            Pagination pagination = sender as Pagination;

            if (!pagination.IsLoaded)
                return;


            CurrentPage = pagination.CurrentPage;
            PageQueue.Enqueue(pagination.CurrentPage);
            Select();
        }

        private void ActorPageSizeChange(object sender, EventArgs e)
        {
            Pagination pagination = sender as Pagination;
            if (!pagination.IsLoaded)
                return;
            PageSize = pagination.PageSize;
            Select();
        }

        public void RefreshActor(long actorID)
        {
            if (CurrentList?.Count <= 0)
                return;
            for (int i = 0; i < CurrentList.Count; i++) {
                if (CurrentList[i]?.ActorID == actorID) {
                    long count = CurrentList[i].Count;
                    CurrentList[i] = new ActorInfo();
                    // 影片数量必须随对象一起上屏：条目替换引发行重绑时先读到 Count=0，
                    // 之后补赋值若不通知(Count 现已带 INPC)角标会一直显示 0
                    ActorInfo fresh = ActorInfo.GetById(actorID);
                    fresh.Count = count;
                    CurrentList[i] = fresh;
                    break;
                }
            }
        }

        /// <summary>
        /// 导出演员列表（hitchao/Jvedio#346/#212，支持 CSV/Excel/JSON）。
        /// 分两级：仅本页（当前页演员）/ 当前筛选条件下的整个库
        /// </summary>
        public void ExportActorListPage(object sender, RoutedEventArgs e)
        {
            List<long> ids = CurrentList?.Where(arg => arg != null && arg.ActorID > 0)
                .Select(arg => arg.ActorID).Distinct().ToList() ?? new List<long>();
            if (ids.Count == 0) {
                MessageNotify.Info("本页没有演员");
                return;
            }
            ExportActorListRange($" WHERE ActorID in ({string.Join(",", ids)})", $"Jvedio演员列表_本页_{DateTime.Now:yyyyMMdd_HHmmss}");
        }

        public void ExportActorListLibrary(object sender, RoutedEventArgs e)
        {
            ExportActorListRange(string.Empty, $"Jvedio演员列表_{DateTime.Now:yyyyMMdd_HHmmss}");
        }

        private async void ExportActorListRange(string outerFilter, string fileName)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "CSV 文件 (*.csv)|*.csv|Excel 文件 (*.xls)|*.xls|JSON 文件 (*.json)|*.json";
            dlg.FileName = fileName;
            if (dlg.ShowDialog() != true)
                return;
            string savePath = dlg.FileName;
            Jvedio.Core.Export.ExportHelper.ExportFormat format = GetExportFormat(dlg.FilterIndex);
            bool search = !string.IsNullOrEmpty(SearchText);
            string searchText = SearchText.ToProperSql();
            await Task.Run(() => {
                try {
                    string filedSelect = new SelectWrapper<ActorInfo>().Select(SelectedField).ToSelect(false);
                    string unionSelect = new SelectWrapper<ActorInfo>().Select(SelectedFieldUnion).ToSelect(false);
                    string sql = $"{filedSelect} FROM actor_info " +
                        $"join metadata_to_actor on metadata_to_actor.ActorID=actor_info.ActorID " +
                        $"join metadata on metadata_to_actor.DataID=metadata.DataID " +
                        $"WHERE metadata.DBId={ConfigManager.Main.CurrentDBId} and metadata.DataType={0} " +
                        $"{(search ? $"and actor_info.ActorName like '%{searchText}%' " : string.Empty)} " +
                        $"GROUP BY actor_info.ActorID " +
                        "UNION " +
                        $"{unionSelect} FROM actor_info " +
                        "WHERE NOT EXISTS(SELECT 1 from metadata_to_actor where metadata_to_actor.ActorID=actor_info.ActorID )" +
                        $"{(search ? $"and actor_info.ActorName like '%{searchText}%' " : string.Empty)}  GROUP BY actor_info.ActorID";
                    // 仅本页：整查询包一层，按当前页演员 ID 过滤（列结构与整库导出一致）
                    if (!string.IsNullOrEmpty(outerFilter))
                        sql = $"SELECT * FROM ({sql}){outerFilter}";
                    int count = Jvedio.Core.Export.ExportHelper.ExportActors(savePath, format, sql);
                    App.Current.Dispatcher.BeginInvoke(new Action(() => {
                        MessageNotify.Success($"{LangManager.GetValueByKey("ExportSuccess")} {count}");
                    }));
                } catch (Exception ex) {
                    Logger.Error(ex);
                    App.Current.Dispatcher.BeginInvoke(new Action(() => MessageNotify.Error(ex.Message)));
                }
            });
        }

        private static Jvedio.Core.Export.ExportHelper.ExportFormat GetExportFormat(int filterIndex)
        {
            switch (filterIndex) {
                case 2: return Jvedio.Core.Export.ExportHelper.ExportFormat.Excel;
                case 3: return Jvedio.Core.Export.ExportHelper.ExportFormat.Json;
                default: return Jvedio.Core.Export.ExportHelper.ExportFormat.Csv;
            }
        }

        private void DeleteActors(object sender, RoutedEventArgs e)
        {
            if (new MsgBox("即将删除演员信息，是否继续？").ShowDialog() == true) {
                MenuItem mnu = sender as MenuItem;
                ContextMenu contextMenu = mnu.Parent as ContextMenu;

                // FrameworkElement image = contextMenu.PlacementTarget as FrameworkElement;
                long.TryParse(contextMenu.Tag.ToString(), out long actorID);
                if (actorID <= 0)
                    return;

                if (!ConfigManager.VideoConfig.ActorEditMode)
                    SelectedActors.Clear();
                ActorInfo actor = CurrentList.Where(arg => arg.ActorID == actorID).FirstOrDefault();
                if (!SelectedActors.Where(arg => arg.ActorID == actorID).Any())
                    SelectedActors.Add(actor);

                foreach (ActorInfo actorInfo in SelectedActors) {
                    actorMapper.DeleteById(actorInfo.ActorID);
                    string sql = $"delete from metadata_to_actor where metadata_to_actor.ActorID='{actorInfo.ActorID}'";
                    actorMapper.ExecuteNonQuery(sql);
                }

                Select();
            }
        }

        /// <summary>
        /// 批量转换英文名：对 ActorNameEN 为空的演员，用假名→罗马字算法兜底转换（结果含汉字/假名残留则跳过）。
        /// 分三级：选中演员（编辑模式=勾选项，否则=右键这一位，与删除/同步信息语义一致）/ 仅本页 / 整个库
        /// </summary>
        private void ConvertActorNameENSelected(object sender, RoutedEventArgs e)
        {
            MenuItem mnu = sender as MenuItem;
            ContextMenu contextMenu = mnu?.Parent as ContextMenu;
            if (contextMenu == null || !long.TryParse(contextMenu.Tag?.ToString(), out long actorID) || actorID <= 0)
                return;
            List<long> ids = new List<long>();
            if (ConfigManager.VideoConfig.ActorEditMode) {
                foreach (ActorInfo selected in SelectedActors)
                    if (selected != null && selected.ActorID > 0 && !ids.Contains(selected.ActorID))
                        ids.Add(selected.ActorID);
            }
            // 一个都没勾选(或未开多选)时退回右键的这一位,与「删除/同步信息」语义一致
            if (!ids.Contains(actorID))
                ids.Add(actorID);
            ConvertActorNameEnRange($"and actor_info.ActorID in ({string.Join(",", ids)})", "选中演员");
        }

        private void ConvertActorNameENPage(object sender, RoutedEventArgs e)
        {
            List<long> ids = CurrentList?.Where(arg => arg != null && arg.ActorID > 0)
                .Select(arg => arg.ActorID).Distinct().ToList() ?? new List<long>();
            if (ids.Count == 0) {
                MessageNotify.Info("本页没有演员");
                return;
            }
            ConvertActorNameEnRange($"and actor_info.ActorID in ({string.Join(",", ids)})", "本页");
        }

        private void ConvertActorNameENLibrary(object sender, RoutedEventArgs e)
        {
            ConvertActorNameEnRange(string.Empty, "整个库");
        }

        private async void ConvertActorNameEnRange(string idFilter, string scopeLabel)
        {
            int converted = 0;
            await Task.Run(() => {
                List<Dictionary<string, object>> list = actorMapper.Select(
                    "select ActorID, ActorName from actor_info " +
                    $"where (ActorNameEN is null or ActorNameEN = '') {idFilter}");
                if (list == null)
                    return;
                foreach (Dictionary<string, object> row in list) {
                    if (!row.TryGetValue("ActorID", out object idObj) || !row.TryGetValue("ActorName", out object nameObj))
                        continue;
                    string actorName = nameObj?.ToString();
                    if (string.IsNullOrEmpty(actorName))
                        continue;
                    string en = RomajiConverter.Convert(actorName);
                    if (string.IsNullOrEmpty(en) || !RomajiConverter.IsPureAscii(en) || en.Equals(actorName))
                        continue;
                    actorMapper.UpdateFieldById("ActorNameEN", en, Convert.ToInt64(idObj));
                    converted++;
                }
            });
            Select();
            MessageNotify.Success($"英文名转换完成（{scopeLabel}）：成功 {converted} 个");
        }

        /// <summary>
        /// 右键「同步信息」：手动补齐演员资料（MinnanoAV）。
        /// 选择语义与「删除」一致：编辑模式下批量补齐已勾选的演员，否则仅右键的这一位。
        /// 显式操作不受「演员资料自动补齐」开关限制，但需先启用 MinnanoAV 源
        /// </summary>
        public void DownLoadSelectedActor(object sender, RoutedEventArgs e)
        {
            MenuItem mnu = sender as MenuItem;
            ContextMenu contextMenu = mnu?.Parent as ContextMenu;
            if (contextMenu == null || !long.TryParse(contextMenu.Tag?.ToString(), out long actorID) || actorID <= 0)
                return;

            List<ActorInfo> targets = new List<ActorInfo>();
            if (ConfigManager.VideoConfig.ActorEditMode) {
                foreach (ActorInfo selected in SelectedActors) {
                    if (selected != null && selected.ActorID > 0 && !targets.Any(arg => arg.ActorID == selected.ActorID))
                        targets.Add(selected);
                }
            }
            ActorInfo current = CurrentList?.Where(arg => arg != null && arg.ActorID == actorID).FirstOrDefault();
            if (current == null)
                current = ActorInfo.GetById(actorID);
            if (current == null)
                return;
            if (!targets.Any(arg => arg.ActorID == actorID))
                targets.Add(current);

            int result = Jvedio.Core.Tasks.ActorScrapeManager.EnqueueManual(targets);
            if (result < 0)
                MessageNotify.Warning("尚未启用演员资料源：请在「选项 → 同步信息」中启用 JAVDatabase(推荐)或 MinnanoAV 后重试");
            else if (result == 0)
                MessageNotify.Info("所选演员已在补齐队列中");
            else
                MessageNotify.Success($"已加入演员资料补齐队列：{result} 位（串行执行，进度见右下角任务页）");
        }

        /// <summary>
        /// 右键「同步信息（整个库）」：全库演员全量入队补齐。
        /// 显式手动操作不受「演员资料自动补齐」开关限制；是否覆盖已有字段由
        /// 「演员资料：刮削时强制覆写已有信息」开关决定
        /// </summary>
        public void DownLoadAllActors(object sender, RoutedEventArgs e)
        {
            int result = Jvedio.Core.Tasks.ActorScrapeManager.EnqueueAllLibrary();
            if (result < 0)
                MessageNotify.Warning("尚未启用演员资料源：请在「选项 → 同步信息」中启用 JAVDatabase(推荐)或 MinnanoAV 后重试");
            else if (result == 0)
                MessageNotify.Info("库内没有可入队的演员");
            else
                MessageNotify.Success($"已加入演员资料补齐队列：{result} 位（串行执行，每 10 个长歇 15 秒，进度见右下角任务页）");
        }

        private void OpenActorImagePath(object sender, RoutedEventArgs e)
        {
            MenuItem mnu = sender as MenuItem;
            ContextMenu contextMenu = mnu.Parent as ContextMenu;
            long.TryParse(contextMenu.Tag.ToString(), out long actorID);
            if (actorID <= 0)
                return;
            ActorInfo actorInfo = actorMapper.SelectById(new SelectWrapper<ActorInfo>().Eq("ActorID", actorID));
            string path = Path.GetFullPath(actorInfo.GetImagePath());
            FileHelper.TryOpenSelectPath(path);
        }


        private void SideActorRate_ValueChanged(object sender, EventArgs e)
        {
            Rate rate = sender as Rate;
            if (rate.Tag == null)
                return;
            long.TryParse(rate.Tag.ToString(), out long actorID);
            if (actorID <= 0)
                return;
            actorMapper.UpdateFieldById("Grade", rate.Value.ToString(), actorID);
        }
        private void EditActor(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            long.TryParse(button.Tag.ToString(), out long actorID);
            if (actorID <= 0)
                return;

            Window_EditActor window_EditActor = new Window_EditActor(actorID);
            window_EditActor.ShowDialog();
        }

        private void ShowSameActor(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            long.TryParse(button.Tag.ToString(), out long actorID);
            if (actorID <= 0)
                return;

            onShowSameActor?.Invoke(actorID);
        }

        private void NewActor(object sender, RoutedEventArgs e)
        {
            bool? success = new Window_EditActor(0, true).ShowDialog();
            if ((bool)success) {
                MessageNotify.Success(LangManager.GetValueByKey("AddSuccess"));
                onStatistic?.Invoke();
                Refresh();
            }
        }


        public void SetSearchFocus()
        {
            searchBox.SetFocus();
        }

        private void searchBox_Search(object sender, RoutedEventArgs e)
        {
            if (sender is SearchBox searchBox && searchBox.IsLoaded)
                Select();
        }

        /// <summary>
        /// 整卡点击：编辑模式=点击卡片任意位置(头像/姓名/信息)切换勾选；
        /// 普通模式=跳转该演员的影片页。星星(评分)与按钮等交互控件除外。
        /// 仅响应左键——右键必须放行给右键菜单(此前右键被当点击吞掉导致菜单弹不出)
        /// </summary>
        private void Card_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            // 命中星星/按钮时不触发选中或跳转(否则评分、编辑/查看按钮失效)
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null) {
                if (source is SuperControls.Style.Rate || source is System.Windows.Controls.Primitives.ButtonBase)
                    return;
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }

            if (!(sender is FrameworkElement ele) || ele.Tag == null ||
                !long.TryParse(ele.Tag.ToString(), out long actorID))
                return;

            // 编辑模式:点击切换勾选(蓝框),不导航;普通模式:跳转该演员的影片页
            if (ConfigManager.VideoConfig.ActorEditMode) {
                ActorInfo actor = CurrentList?.Where(arg => arg != null && arg.ActorID == actorID).FirstOrDefault();
                if (actor == null)
                    return;
                if (SelectedActors.Where(arg => arg.ActorID == actorID).Any()) {
                    SelectedActors.RemoveAll(arg => arg.ActorID == actorID);
                    actor.IsSelected = false;
                } else {
                    SelectedActors.Add(actor);
                    actor.IsSelected = true;
                }
                e.Handled = true;
                return;
            }
            if (ViewMode) {
                e.Handled = true;
                onShowSameActor?.Invoke(actorID);
            }
        }

        public void NextPage()
        {
            pagination.NextPage();
        }

        public void PreviousPage()
        {
            pagination.PrevPage();
        }

        /// <summary>去到顶部（Ctrl+Up 与右下角悬浮按钮共用）</summary>
        public void GoToTop()
        {
            scrollViewer?.ScrollToTop();
        }

        /// <summary>去到底部（Ctrl+Down 与右下角悬浮按钮共用）</summary>
        public void GoToBottom()
        {
            scrollViewer?.ScrollToBottom();
        }

        /// <summary>去到顶部/底部悬浮按钮（与影片信息界面同款 super:GoToTop）</summary>
        public void GotoTop(object sender, RoutedEventArgs e)
        {
            scrollViewer?.ScrollToTop();
        }

        public void GotoBottom(object sender, RoutedEventArgs e)
        {
            scrollViewer?.ScrollToBottom();
        }

        public void FirstPage()
        {
            pagination.FirstPage();
        }

        public void LastPage()
        {
            pagination.LastPage();
        }

        private void SideActorRate_ValueChanged(object sender, FunctionEventArgs<double> e)
        {
            if (sender is Rate rate && rate.Tag != null &&
                long.TryParse(rate.Tag.ToString(), out long actorID) &&
                actorID > 0) {
                actorMapper.UpdateFieldById("Grade", rate.Value.ToString(), actorID);
                onGradeChange?.Invoke(actorID, (float)rate.Value);
            }

        }

        private void Row_DoubleClick(object sender, MouseButtonEventArgs e)
        {

            if (CurrentList != null && tableData.SelectedIndex is int idx &&
                idx >= 0 && idx < CurrentList.Count &&
                CurrentList[idx] is ActorInfo info && info.ActorID is long id) {
                onShowSameActor?.Invoke(id);
            }
        }
    }
}
