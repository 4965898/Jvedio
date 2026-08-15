using Jvedio.Core.Enums;
using Jvedio.Entity;
using SuperControls.Style;
using SuperUtils.Framework.ORM.Utils;
using SuperUtils.Framework.ORM.Wrapper;
using SuperUtils.Time;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static Jvedio.App;
using static Jvedio.MapperManager;

namespace Jvedio.Core.UserControls
{
    /// <summary>
    /// VideoSideMenu.xaml 的交互逻辑
    /// </summary>
    public partial class VideoSideMenu : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #region "事件"
        public Action onStatistic { get; set; }
        public Action<object> onSideButtonCmd { get; set; }

        #endregion

        #region "属性-统计"


        private double _RecentWatchedCount = 0;

        public double RecentWatchedCount {
            get { return _RecentWatchedCount; }

            set {
                _RecentWatchedCount = value;
                RaisePropertyChanged();
            }
        }

        private long _AllVideoCount = 0;

        public long AllVideoCount {
            get { return _AllVideoCount; }

            set {
                _AllVideoCount = value;
                RaisePropertyChanged();
            }
        }

        private double _FavoriteVideoCount = 0;

        public double FavoriteVideoCount {
            get { return _FavoriteVideoCount; }

            set {
                _FavoriteVideoCount = value;
                RaisePropertyChanged();
            }
        }

        private long _RecentWatchCount = 0;

        public long RecentWatchCount {
            get { return _RecentWatchCount; }

            set {
                _RecentWatchCount = value;
                RaisePropertyChanged();
            }
        }

        private long _AllActorCount = 0;

        public long AllActorCount {
            get { return _AllActorCount; }

            set {
                _AllActorCount = value;
                RaisePropertyChanged();
            }
        }

        private long _AllLabelCount = 0;

        public long AllLabelCount {
            get { return _AllLabelCount; }

            set {
                _AllLabelCount = value;
                RaisePropertyChanged();
            }
        }

        private long _AllGenreCount = 0;

        public long AllGenreCount {
            get { return _AllGenreCount; }

            set {
                _AllGenreCount = value;
                RaisePropertyChanged();
            }
        }

        private long _AllSeriesCount = 0;

        public long AllSeriesCount {
            get { return _AllSeriesCount; }

            set {
                _AllSeriesCount = value;
                RaisePropertyChanged();
            }
        }
        private long _AllStudioCount = 0;

        public long AllStudioCount {
            get { return _AllStudioCount; }

            set {
                _AllStudioCount = value;
                RaisePropertyChanged();
            }
        }

        private long _AllDirectorCount = 0;

        public long AllDirectorCount {
            get { return _AllDirectorCount; }

            set {
                _AllDirectorCount = value;
                RaisePropertyChanged();
            }
        }

        #endregion


        public VideoSideMenu()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;
            Init();
        }

        public void Init()
        {
            InitRecentWatched();
        }


        private void ClearRecentWatched(object sender, RoutedEventArgs e)
        {
            SelectWrapper<MetaData> wrapper = new SelectWrapper<MetaData>();
            wrapper.Eq("DBId", ConfigManager.Main.CurrentDBId).Eq("DataType", "0");
            metaDataMapper.UpdateField("ViewDate", string.Empty, wrapper);
            onStatistic?.Invoke();
        }


        private void ShowActorNotice(object sender, RoutedEventArgs e)
        {
            PathType pathType = (PathType)ConfigManager.Settings.PicPathMode;
            if (pathType.Equals(PathType.RelativeToData))
                MessageCard.Info(LangManager.GetValueByKey("ShowActorImageWarning"));
        }


        private static Dictionary<string, long> GetGenreDict(string SearchText)
        {
            Dictionary<string, long> genreDict = new Dictionary<string, long>();
            string sql = $"SELECT Genre from metadata " +
                $"where metadata.DBId={ConfigManager.Main.CurrentDBId} and metadata.DataType={0} AND Genre !=''";

            List<Dictionary<string, object>> lists = metaDataMapper.Select(sql);
            if (lists != null) {
                string searchText = string.IsNullOrEmpty(SearchText) ? string.Empty : SearchText;
                bool search = !string.IsNullOrEmpty(searchText);

                foreach (Dictionary<string, object> item in lists) {
                    if (!item.ContainsKey("Genre"))
                        continue;
                    string genre = item["Genre"].ToString();
                    if (string.IsNullOrEmpty(genre))
                        continue;
                    List<string> genres = genre.Split(new char[] { SuperUtils.Values.ConstValues.Separator }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    foreach (string g in genres) {
                        if (search && g.IndexOf(searchText) < 0)
                            continue;
                        if (genreDict.ContainsKey(g))
                            genreDict[g] = genreDict[g] + 1;
                        else
                            genreDict.Add(g, 1);
                    }
                }
            }
            return genreDict;
        }

        public static List<string> GetGenreList(string SearchText)
        {
            Dictionary<string, long> genreDict = GetGenreDict(SearchText);
            Dictionary<string, long> ordered = null;
            try {
                ordered = genreDict.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
            } catch (Exception ex) {
                Logger.Error(ex);
            }
            List<string> result = new List<string>();
            if (ordered != null) {
                foreach (var key in ordered.Keys) {
                    string v = $"{key}({ordered[key]})";
                    result.Add(v);
                }
            }
            return result;
        }

        public static List<string> GetListByField(string field, string SearchText)
        {
            string like_sql = string.Empty;
            if (!string.IsNullOrEmpty(SearchText))
                like_sql = $" and {field} like '%{SearchText.ToProperSql()}%' ";

            List<string> result = new List<string>();
            string sql = $"SELECT {field},Count({field}) as Count from metadata " +
                "JOIN metadata_video on metadata.DataID=metadata_video.DataID " +
                $"where metadata.DBId={ConfigManager.Main.CurrentDBId} and metadata.DataType={0} AND {field} !='' " +
                $"{(!string.IsNullOrEmpty(like_sql) ? like_sql : string.Empty)}" +
                $"GROUP BY {field} ORDER BY Count DESC";
            List<Dictionary<string, object>> list = metaDataMapper.Select(sql);
            if (list != null) {
                foreach (Dictionary<string, object> item in list) {
                    if (!item.ContainsKey(field))
                        continue;
                    string name = item[field].ToString();
                    long.TryParse(item["Count"].ToString(), out long count);
                    if (string.IsNullOrEmpty(name))
                        continue;
                    result.Add($"{name}({count})");
                }
            }

            return result;
        }


        /// <summary>
        /// 统计：加载时间 <70ms (15620个信息)
        /// 异步化：统计查询全部移到后台线程，避免与后台刮削写库撞锁时阻塞 UI（database is locked 假死）。
        /// </summary>
        private bool _StatisticBusy = false;

        private bool _StatisticPending = false;

        public void Statistic(string searchText)
        {
            if (_StatisticBusy) {
                _StatisticPending = true;
                return;
            }
            _StatisticBusy = true;
            string search = searchText ?? string.Empty;
            Task.Run(() => {
                try {
                    long dbid = ConfigManager.Main.CurrentDBId;
                    long allVideoCount = metaDataMapper.SelectCount(new SelectWrapper<MetaData>().Eq("DBId", dbid).Eq("DataType", 0));

                    long favoriteVideoCount = metaDataMapper.SelectCount(new SelectWrapper<MetaData>().Eq("DBId", dbid).Eq("DataType", 0).Gt("Grade", 0));

                    string actor_count_sql = "SELECT count(*) as Count " +
                             "from (SELECT actor_info.ActorID FROM actor_info join metadata_to_actor " +
                             "on metadata_to_actor.ActorID=actor_info.ActorID " +
                             "join metadata " +
                             "on metadata_to_actor.DataID=metadata.DataID " +
                             $"WHERE metadata.DBId={dbid} and metadata.DataType={0} " +
                             "GROUP BY actor_info.ActorID " +
                             "UNION " +
                             "select actor_info.ActorID  " +
                             "FROM actor_info WHERE NOT EXISTS " +
                             "(SELECT 1 from metadata_to_actor where metadata_to_actor.ActorID=actor_info.ActorID ) " +
                             "GROUP BY actor_info.ActorID)";

                    long allActorCount = actorMapper.SelectCount(actor_count_sql);

                    string label_count_sql = "SELECT COUNT(DISTINCT LabelName) as Count  from metadata_to_label " +
                                            "join metadata on metadata_to_label.DataID=metadata.DataID " +
                                             $"WHERE metadata.DBId={dbid} and metadata.DataType={0} ";

                    long allLabelCount = metaDataMapper.SelectCount(label_count_sql);
                    DateTime date1 = DateTime.Now.AddDays(-1 * Jvedio.ViewModel.VieModel_Main.RECENT_DAY);
                    DateTime date2 = DateTime.Now;
                    long recentWatchCount = metaDataMapper.SelectCount(new SelectWrapper<MetaData>().Eq("DBId", dbid).Eq("DataType", 0).Between("ViewDate", DateHelper.ToLocalDate(date1), DateHelper.ToLocalDate(date2)));

                    // 类别
                    int allGenreCount = GetGenreDict(search).Count;
                    int allSeriesCount = GetListByField(LabelType.Series.ToString(), search).Count;
                    int allStudioCount = GetListByField(LabelType.Studio.ToString(), search).Count;
                    int allDirectorCount = GetListByField(LabelType.Director.ToString(), search).Count;

                    App.Current.Dispatcher.BeginInvoke(new Action(() => {
                        AllVideoCount = allVideoCount;
                        FavoriteVideoCount = favoriteVideoCount;
                        AllActorCount = allActorCount;
                        AllLabelCount = allLabelCount;
                        RecentWatchCount = recentWatchCount;
                        AllGenreCount = allGenreCount;
                        AllSeriesCount = allSeriesCount;
                        AllStudioCount = allStudioCount;
                        AllDirectorCount = allDirectorCount;
                    }));

                    // 统计计数写回库：后台执行，失败仅记日志，绝不抛出阻塞 UI
                    try {
                        appDatabaseMapper.UpdateFieldById("Count", allVideoCount.ToString(), dbid);
                    } catch (Exception ex) {
                        Logger.Error(ex);
                    }
                } catch (Exception ex) {
                    Logger.Error(ex);
                } finally {
                    _StatisticBusy = false;
                    if (_StatisticPending) {
                        _StatisticPending = false;
                        App.Current.Dispatcher.BeginInvoke(new Action(() => Statistic(search)));
                    }
                }
            });
        }


        /// <summary>
        /// 显示最近播放
        /// </summary>
        private void InitRecentWatched()
        {
            SelectWrapper<MetaData> wrapper = new SelectWrapper<MetaData>();
            wrapper.Eq("DataType", (int)Main.CurrentDataType).NotEq("ViewDate", string.Empty);
            long count = metaDataMapper.SelectCount(wrapper);
            RecentWatchedCount = count;
        }

        private void HandleSideClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag != null &&
                element.Tag.ToString() is string tag) {
                onSideButtonCmd?.Invoke(tag);
            }
        }

        public void SetSelected(string text)
        {
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            foreach (var item in firstStackPanel.Children.OfType<PathRadioButton>()) {
                if (item.Tag.ToString() is string tag && !string.IsNullOrEmpty(tag) && text.Equals(tag)) {
                    item.IsChecked = true;
                    return;
                }
            }

            foreach (var item in secondStackPanel.Children.OfType<PathRadioButton>()) {
                if (item.Tag.ToString() is string tag && !string.IsNullOrEmpty(tag) && text.Equals(tag)) {
                    item.IsChecked = true;
                    return;
                }
            }
        }
    }
}
