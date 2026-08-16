using Jvedio.Core.CustomEventArgs;
using Jvedio.Core.Enums;
using Jvedio.Entity;
using Jvedio.Entity.CommonSQL;
using Jvedio.Mapper;
using SuperUtils.Framework.ORM.Utils;
using SuperUtils.Framework.ORM.Wrapper;
using SuperUtils.WPF.VieModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using static Jvedio.App;
using static Jvedio.MapperManager;

namespace Jvedio.Core.UserControls.ViewModels
{
    class VieModel_VideoList : ViewModelBase
    {
        private const int SEARCH_CANDIDATE_MAX_COUNT = 10;

        #region "浜嬩欢"

        //public Action<bool> onScroll;
        public Action<long> onPageChange;

        public static Action<bool> onSearchingChange;

        public event Action PageChangedStarted;

        public event EventHandler PageChangedCompleted;

        public event EventHandler RenderSqlChanged;

        #endregion

        #region "娓叉煋涓茶鍖?

        /// <summary>
        /// 鎵归噺鏇存柊 UI 鐨勬潯鏁?
        /// </summary>
        private const int UI_UPDATE_BATCH_SIZE = 12;

        /// <summary>
        /// 娓叉煋鐗堟湰鍙凤紝姣忔缈婚〉鑷锛屾棫娓叉煋寰幆妫€娴嬪埌鐗堟湰涓嶄竴鑷寸珛鍗抽€€鍑?
        /// </summary>
        private int _RenderVersion = 0;

        /// <summary>
        /// 褰撳墠娓叉煋浠诲姟锛屾柊鐨勭炕椤佃姹傚繀椤荤瓑寰呭畠鐪熸缁撴潫
        /// </summary>
        private Task _RenderTask = null;

        private class QueryResult
        {
            public long Total { get; set; }

            public List<Video> Videos { get; set; }

            public QueryResult(long total, List<Video> videos)
            {
                Total = total;
                Videos = videos;
            }
        }

        #endregion

        #region "闈欐€佸睘鎬?

        public static List<string> SortDict { get; set; } = new List<string>()
{
            "metadata_video.VID",
            "metadata.Grade",
            "metadata.Size",
            "metadata.LastScanDate",
            "metadata.FirstScanDate",
            "metadata.Title",
            "metadata.ViewCount",
            "metadata.ReleaseDate",
            "metadata.Rating",
            "metadata_video.Duration",
            "ACTOR_FIRST_NAME",
        };

        public static string[] SelectFields =
        {
            "DISTINCT metadata.DataID",
            "MVID",
            "VID",
            "metadata.Grade",
            "metadata.Title",
            "metadata.Path",
            "metadata.Hash",
            "metadata.Size",
            "metadata.ViewCount",
            "metadata.ViewDate",
            "metadata.CreateDate",
            "metadata.UpdateDate",
            "metadata_video.SubSection",
            "metadata_video.ImageUrls",
            "metadata.ReleaseDate",
            "metadata.LastScanDate",
            "metadata_video.Director",
            "metadata_video.Studio",
            "metadata_video.Duration",
            "metadata_video.WebUrl",
            "metadata_video.WebType",
            "(select group_concat(TagID,',') from metadata_to_tagstamp where metadata_to_tagstamp.DataID=metadata.DataID)  as TagIDs ",
        };


        #endregion

        #region "灞炴€?
        public Queue<int> PageQueue { get; set; } = new Queue<int>();


        /// <summary>
        /// 过滤器传进来的
        /// </summary>
        public SelectWrapper<Video> FilterWrapper { get; set; }

        /// <summary>
        /// 导出用：最近一次查询的 join 部分 SQL（FROM + JOIN，hitchao/Jvedio#346/#212）
        /// </summary>
        public string LastQuerySql { get; set; }

        /// <summary>
        /// 导出用：最近一次查询的 wrapper（where/order）
        /// </summary>
        public SelectWrapper<Video> LastWrapper { get; set; }

        /// <summary>
        /// 杩囨护鍣ㄤ紶杩涚殑 SQL
        /// </summary>
        public string FilterSQL { get; set; }

        /// <summary>
        /// 渚ц竟鏍忕偣鍑昏繘鍏ョ殑
        /// </summary>
        public SelectWrapper<Video> ExtraWrapper { get; set; }

        /// <summary>
        /// 鎼滅储
        /// </summary>
        public SelectWrapper<Video> SearchWrapper { get; set; }

        private List<Video> _SelectedVideo { get; set; } = new List<Video>();

        public CancellationTokenSource RenderVideoCTS { get; set; }

        public CancellationToken RenderVideoCT { get; set; }

        public string ClickFilterType { get; set; }



        private int _SearchSelectedIndex;

        public int SearchSelectedIndex {
            get { return _SearchSelectedIndex; }

            set {
                _SearchSelectedIndex = value;
                RaisePropertyChanged();
            }
        }

        private bool _Nothing;

        public bool Nothing {
            get { return _Nothing; }

            set {
                _Nothing = value;
                RaisePropertyChanged();
            }
        }
        private bool _ShowTable = true;

        public bool ShowTable {
            get { return _ShowTable; }

            set {
                _ShowTable = value;
                RaisePropertyChanged();
            }
        }

        private bool _ShowAsso = true;

        public bool ShowAsso {
            get { return _ShowAsso; }

            set {
                _ShowAsso = value;
                RaisePropertyChanged();
            }
        }

        private string _SearchText = string.Empty;

        public string SearchText {
            get { return _SearchText; }

            set {
                _SearchText = value;
                RaisePropertyChanged();
            }
        }


        private string _UUID;

        public string UUID {
            get { return _UUID; }

            set {
                _UUID = value;
                RaisePropertyChanged();
            }
        }


        private int _GlobalImageHeight;

        public int GlobalImageHeight {
            get { return _GlobalImageHeight; }

            set {
                _GlobalImageHeight = value;
                RaisePropertyChanged();
            }
        }


        private int _GlobalImageWidth = (int)ConfigManager.VideoConfig.GlobalImageWidth;

        public int GlobalImageWidth {
            get { return _GlobalImageWidth; }

            set {
                _GlobalImageWidth = value;
                GlobalImageHeight = ViewVideo.GetImageHeight(ShowImageMode, value);
                ConfigManager.VideoConfig.GlobalImageWidth = value;

                RaisePropertyChanged();
            }
        }

        private int _ShowImageMode = (int)ConfigManager.VideoConfig.ImageMode;

        public int ShowImageMode {
            get { return _ShowImageMode; }

            set {
                _ShowImageMode = value;
                RaisePropertyChanged();
                ConfigManager.VideoConfig.ImageMode = value;
            }
        }

        private int _SortType = (int)ConfigManager.VideoConfig.SortType;

        public int SortType {
            get { return _SortType; }

            set {
                _SortType = value;
                RaisePropertyChanged();
                ConfigManager.VideoConfig.SortType = value;
            }
        }
        private bool _SortDescending = ConfigManager.VideoConfig.SortDescending;

        public bool SortDescending {
            get { return _SortDescending; }

            set {
                _SortDescending = value;
                RaisePropertyChanged();
                ConfigManager.VideoConfig.SortDescending = value;
            }
        }

        private bool _EditMode;

        public bool EditMode {
            get { return _EditMode; }

            set {
                _EditMode = value;
                RaisePropertyChanged();
            }
        }

        private ObservableCollection<TagStamp> _TagStamps = new ObservableCollection<TagStamp>();

        public ObservableCollection<TagStamp> TagStamps {
            get { return _TagStamps; }

            set {
                _TagStamps = value;
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

        private bool _Searching = false;

        public bool Searching {
            get { return _Searching; }

            set {
                _Searching = value;
                onSearchingChange?.Invoke(value);
                RaisePropertyChanged();
            }
        }

        private bool _ShowActorGrid;

        public bool ShowActorGrid {
            get { return _ShowActorGrid; }

            set {
                _ShowActorGrid = value;
                RaisePropertyChanged();
            }
        }
        private bool _ShowActorToggle;

        public bool ShowActorToggle {
            get { return _ShowActorToggle; }

            set {
                _ShowActorToggle = value;
                RaisePropertyChanged();
            }
        }


        private bool _EnableEditActress = false;

        public bool EnableEditActress {
            get { return _EnableEditActress; }

            set {
                _EnableEditActress = value;
                RaisePropertyChanged();
            }
        }

        private int _CurrentCount = 0;

        public int CurrentCount {
            get { return _CurrentCount; }

            set {
                _CurrentCount = value;
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

        private int _TotalPage = 1;

        public int TotalPage {
            get { return _TotalPage; }

            set {
                _TotalPage = value;
                RaisePropertyChanged();
            }
        }



        private List<Video> _VideoList;

        public List<Video> VideoList {
            get { return _VideoList; }

            set {
                _VideoList = value;
                RaisePropertyChanged();
            }
        }


        private ObservableCollection<Video> _CurrentVideoList;

        public ObservableCollection<Video> CurrentVideoList {
            get { return _CurrentVideoList; }

            set {
                _CurrentVideoList = value;
                RaisePropertyChanged();
            }
        }


        public List<Video> SelectedVideo {
            get { return _SelectedVideo; }

            set {
                _SelectedVideo = value;
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

        private int _PageSize = (int)ConfigManager.VideoConfig.PageSize;

        public int PageSize {
            get { return _PageSize; }

            set {
                _PageSize = value;
                RaisePropertyChanged();
                ConfigManager.VideoConfig.PageSize = value;
            }
        }

        private ObservableCollection<Video> _ViewAssociationDatas;

        public ObservableCollection<Video> ViewAssociationDatas {
            get { return _ViewAssociationDatas; }

            set {
                _ViewAssociationDatas = value;
                RaisePropertyChanged();
            }
        }

        // 褰辩墖鍏宠仈
        private ObservableCollection<Video> _AssociationDatas;

        public ObservableCollection<Video> AssociationDatas {
            get { return _AssociationDatas; }

            set {
                _AssociationDatas = value;
                RaisePropertyChanged();
            }
        }

        #endregion


        #region "绛涢€?



        private bool _ShowFilter = ConfigManager.VideoConfig.ShowFilter;

        public bool ShowFilter {
            get { return _ShowFilter; }

            set {
                _ShowFilter = value;
                RaisePropertyChanged();
                ConfigManager.VideoConfig.ShowFilter = value;
            }
        }

        #endregion


        public VieModel_VideoList()
        {
            RefreshVideoRenderToken();
            Init();
        }

        public override void Init()
        {
            GlobalImageHeight = ViewVideo.GetImageHeight(ShowImageMode, GlobalImageWidth);
        }

        public void LoadData()
        {
            Select();
        }

        public void RandomDisplay()
        {
            Select(true);
        }

        public void Refresh() => Select();


        public void RefreshVideoRenderToken()
        {
            CancellationTokenSource old = RenderVideoCTS;
            RenderVideoCTS = new CancellationTokenSource();
            RenderVideoCTS.Token.Register(() => { Logger.Warn("cancel load video page task"); });
            RenderVideoCT = RenderVideoCTS.Token;
            try {
                old?.Cancel();
                old?.Dispose();
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }



        public SelectWrapper<Video> GetSearchWrapper(SearchField searchType)
        {
            SelectWrapper<Video> wrapper = new SelectWrapper<Video>();
            if (string.IsNullOrEmpty(SearchText))
                return null;
            string formatSearch = SearchText.ToProperSql().Trim();
            if (string.IsNullOrEmpty(formatSearch))
                return null;
            string searchContent = formatSearch;

            switch (searchType) {
                case SearchField.VID:

                    string vid = JvedioLib.Security.Identify.GetVID(formatSearch);
                    if (string.IsNullOrEmpty(vid))
                        searchContent = formatSearch;
                    else
                        searchContent = vid;
                    wrapper.Like("VID", searchContent);
                    break;
                default:
                    wrapper.Like(searchType.ToString(), searchContent);
                    break;
            }

            return wrapper;
        }


        public bool Query(SearchField searchType = SearchField.VID)
        {
            SearchWrapper = GetSearchWrapper(searchType);
            Select();
            return true;
        }

        public void ToLimit<T>(IWrapper<T> wrapper)
        {
            int row_count = PageSize;
            long offset = PageSize * (CurrentPage - 1);
            wrapper.Limit(offset, row_count);
        }

        public async void Select(bool random = false)
        {
            Logger.Info("0.Select");

            // 鍒ゆ柇褰撳墠鑾峰彇鐨勯槦鍒?
            while (PageQueue.Count > 1) {
                int page = PageQueue.Dequeue();
                Logger.Info($"skip page: {page}");
            }

            // 姣忔缈婚〉鐗堟湰鍙疯嚜澧烇紝璁╂鍦ㄨ繍琛岀殑鏃ф覆鏌撳敖蹇€€鍑?
            int version = Interlocked.Increment(ref _RenderVersion);

            // 鍗曢锛氱瓑寰呬笂涓€娆℃覆鏌撲换鍔＄湡姝ｇ粨鏉燂紝閬垮厤骞跺彂娓叉煋瀵艰嚧闂€€
            Task prevTask = _RenderTask;
            if (prevTask != null && !prevTask.IsCompleted) {
                try {
                    await prevTask;
                } catch (Exception ex) {
                    Logger.Error(ex);
                }
            }
            // 绛夊緟鏈熼棿宸茬粡鏈夋洿鏂扮殑璇锋眰杩涙潵浜嗭紝鏀惧純鏈鏌ヨ
            if (version != _RenderVersion)
                return;

            SelectWrapper<Video> wrapper = Video.InitWrapper();

            SetSortOrder(wrapper, random);

            ToLimit(wrapper);
            wrapper.Select(SelectFields);

            string sql = VideoMapper.SQL_BASE;


            if (ExtraWrapper != null) {
                wrapper.Join(ExtraWrapper);
                if (!string.IsNullOrEmpty(ExtraWrapper.ExtraSql))
                    sql += ExtraWrapper.ExtraSql;
            }

            if (SearchWrapper != null) {
                wrapper.Join(SearchWrapper);
                if (!string.IsNullOrEmpty(SearchWrapper.ExtraSql))
                    sql += SearchWrapper.ExtraSql;
            }

            if (FilterWrapper != null) {
                wrapper.Join(FilterWrapper);
                if (!string.IsNullOrEmpty(FilterSQL))
                    sql += FilterSQL;
            }

            // todo 濡傛灉鎼滅储妗嗛€変腑浜嗘爣绛撅紝鎼滅储鍑烘潵鐨勭粨鏋滀笉涓€鑷?
            SearchField searchType = (SearchField)SearchSelectedIndex;
            if (Searching) {
                if (searchType == SearchField.ActorName)
                    sql += VideoMapper.SQL_JOIN_ACTOR;
                else if (searchType == SearchField.LabelName)
                    sql += VideoMapper.SQL_JOIN_LABEL;
            } else if (!string.IsNullOrEmpty(ClickFilterType)) {
                if (ClickFilterType == "Label") {
                    sql += VideoMapper.SQL_JOIN_LABEL;
                } else if (ClickFilterType == "Actor") {
                    sql += VideoMapper.SQL_JOIN_ACTOR;
                } else {
                }
            }

            string count_sql = "select count(DISTINCT metadata.DataID) " + sql + wrapper.ToWhere(false);
            string select_sql = wrapper.ToSelect(false) + sql + wrapper.ToWhere(false) + wrapper.ToOrder() + wrapper.ToLimit();

            // 鏁版嵁搴撴煡璇㈢Щ鍒板悗鍙扮嚎绋嬶紝閬垮厤闃诲 UI
            QueryResult result = null;
            try {
                result = await Task.Run(() => {
                    long total = metaDataMapper.SelectCount(count_sql);
                    List<Dictionary<string, object>> list = metaDataMapper.Select(select_sql);
                    List<Video> videos = list == null
                        ? new List<Video>()
                        : metaDataMapper.ToEntity<Video>(list, typeof(Video).GetProperties(), false);
                    return new QueryResult(total, videos);
                });
            } catch (Exception ex) {
                Logger.Error(ex);
                return;
            }
            // 鏌ヨ鏈熼棿鍙堟湁鏂拌姹傦紝涓㈠純鏈缁撴灉
            if (version != _RenderVersion)
                return;

            TotalCount = result.Total;
            VideoList = result.Videos;
            CurrentCount = VideoList.Count;

            WrapperEventArg<Video> arg = new WrapperEventArg<Video>();
            arg.Wrapper = wrapper;
            arg.SQL = sql;
            RenderSqlChanged?.Invoke(null, arg);

            // 导出用：保存本次查询的 join 部分与 where 条件（hitchao/Jvedio#346/#212）
            LastQuerySql = sql;
            LastWrapper = wrapper;

            onPageChange?.Invoke(TotalCount);
            _RenderTask = RenderAsync(version);
        }

        public void SetSortOrder<T>(IWrapper<T> wrapper, bool random = false)
        {
            if (wrapper == null)
                return;
            int sortIndex = SortType;
            if (sortIndex < 0 || sortIndex >= VieModel_VideoList.SortDict.Count)
                sortIndex = 0;
            string sortField = VieModel_VideoList.SortDict[sortIndex];
            if (random)
                wrapper.Asc("RANDOM()");
            else {
                if (sortField == "ACTOR_FIRST_NAME") {
                    // 演员名排序：空/无演员(NULL)强制排末尾，且必须合并为单个表达式
                    // （Asc/Desc 是覆盖语义，后调覆盖先调，两次调用会丢掉空值处理）
                    // 注意：ORDER BY a, b ASC/DESC 中方向只作用于最后一个键，CASE 键恒 ASC → 空值恒排末尾，升降序均正确（实测）
                    string sub = "(select MIN(actor_info.ActorName) from metadata_to_actor join actor_info on metadata_to_actor.ActorID=actor_info.ActorID where metadata_to_actor.DataID=metadata.DataID)";
                    string merged = $"CASE WHEN {sub} IS NULL OR {sub}='' THEN 1 ELSE 0 END, {sub} COLLATE NOCASE";
                    if (SortDescending)
                        wrapper.Desc(merged);
                    else
                        wrapper.Asc(merged);
                } else if (sortField.IndexOf("VID", StringComparison.OrdinalIgnoreCase) >= 0) {
                    // 识别码排序：按「字母前缀 + 数字后缀」两段排，避免 LUXU-119 → LUXU-1190 → LUXU-120 的字符串序
                    // 注意：wrapper.Asc/Desc 是覆盖语义（后调覆盖先调），多字段排序必须合并为单个表达式
                    // 前缀：第一个 '-' 之前；数字：最后一个 '-' 之后（兼容 FC2-PPV-123456 双连字符），数字零填充 15 位再拼接
                    string prefix = $"CASE WHEN {sortField} LIKE '%-%' THEN SUBSTR({sortField},1,INSTR({sortField},'-')-1) ELSE {sortField} END";
                    string number = "CASE " +
                        $"WHEN {sortField} LIKE '%-%-%' THEN CAST(SUBSTR({sortField}, INSTR({sortField},'-') + INSTR(SUBSTR({sortField}, INSTR({sortField},'-')+1), '-') + 1) AS INTEGER) " +
                        $"WHEN {sortField} LIKE '%-%' THEN CAST(SUBSTR({sortField}, INSTR({sortField},'-')+1) AS INTEGER) " +
                        "ELSE 0 END";
                    string merged = $"(({prefix}) || printf('%015d', ({number}))) COLLATE NOCASE";
                    if (SortDescending)
                        wrapper.Desc(merged);
                    else
                        wrapper.Asc(merged);
                } else if (sortField == "metadata.Title") {
                    // 按名称排序：空/未同步标题(NULL)强制排末尾，避免「新加入未同步影片」混排到最前、
                    // 「最近播放(已同步)」被挤到末尾（hitchao/Jvedio#362/#437）
                    // 注意：wrapper.Asc/Desc 是覆盖语义（后调覆盖先调），多键必须合并为单个表达式（逗号分隔）
                    string merged = $"CASE WHEN {sortField} IS NULL OR {sortField}='' THEN 1 ELSE 0 END, {sortField} COLLATE NOCASE";
                    if (SortDescending)
                        wrapper.Desc(merged);
                    else
                        wrapper.Asc(merged);
                } else {
                    if (SortDescending)
                        wrapper.Desc(sortField);
                    else
                        wrapper.Asc(sortField);
                }
            }
        }



        public Task RenderAsync(int version)
        {
            Logger.Info("1.Render");
            if (CurrentVideoList == null) {
                CurrentVideoList = new ObservableCollection<Video>();
                Nothing = true;
                CurrentVideoList.CollectionChanged += (s, e) => {
                    Nothing = CurrentVideoList.Count == 0;
                };
            }

            PageChangedStarted?.Invoke();

            // 鍙栨秷骞舵浛鎹㈡棫鐨勫彇娑堜护鐗?
            RefreshVideoRenderToken();
            Rendering = true;
            RenderProgress = 0;

            List<Video> videos = VideoList;
            CancellationToken token = RenderVideoCT;
            return Task.Run(async () => {
                try {
                    int from = 0;
                    for (int i = 0; i < videos.Count; i++) {
                        if (version != _RenderVersion || token.IsCancellationRequested)
                            break;
                        Video video = videos[i];
                        if (video == null)
                            continue;
                        // 浠ヤ笅鑰楁椂鎿嶄綔锛堝浘鐗囪В鐮併€佹暟鎹簱鏌ヨ锛夊叏閮ㄥ湪鍚庡彴绾跨▼鎵ц
                        Video.SetImage(ref video, ShowImageMode);
                        Video.SetTagStamps(ref video); // 璁剧疆鏍囩鎴?
                        Video.SetTitleAndDate(ref video); // 璁剧疆鏍囬鍜屽彂琛屾棩鏈?
                        Video.SetAsso(ref video);
                        RenderProgress = (int)(100 * (i + 1) / (float)videos.Count);
                        if (i % UI_UPDATE_BATCH_SIZE == UI_UPDATE_BATCH_SIZE - 1) {
                            if (version != _RenderVersion || token.IsCancellationRequested)
                                break;
                            int to = i;
                            await App.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                                new Action(() => ApplyRenderBatch(videos, from, to)));
                            from = i + 1;
                        }
                    }

                    // 鏀跺熬锛氭覆鏌撴湡闂村張鏈夋柊璇锋眰鍒欐斁寮冿紝閬垮厤鏃х粨鏋滆鐩栨柊椤甸潰
                    if (version == _RenderVersion && !token.IsCancellationRequested) {
                        int from2 = from;
                        await App.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                            new Action(() => {
                                if (videos.Count > 0)
                                    ApplyRenderBatch(videos, from2, videos.Count - 1);
                                // 娓呴櫎澶氫綑鐨勯」锛堝惈缁撴灉涓虹┖鏃舵竻绌烘暣椤碉級
                                for (int j = CurrentVideoList.Count - 1; j >= videos.Count; j--)
                                    CurrentVideoList.RemoveAt(j);
                            }));
                    }
                } catch (Exception ex) {
                    Logger.Error(ex);
                } finally {
                    try {
                        System.Windows.Application app = App.Current;
                        if (app != null && app.Dispatcher != null) {
                            await app.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                                new Action(() => {
                                    try {
                                        if (RenderVideoCT.IsCancellationRequested)
                                            RefreshVideoRenderToken();
                                    } finally {
                                        Rendering = false;
                                        PageChangedCompleted?.Invoke(this, null);
                                    }
                                }));
                        }
                    } catch (Exception ex) {
                        Logger.Error(ex);
                    }
                }
            });
        }

        /// <summary>
        /// 鍦?UI 绾跨▼鎵归噺鏇存柊闆嗗悎锛堟浛鎹?鏂板锛夛紝閬垮厤閫愭潯 BeginInvoke
        /// </summary>
        private void ApplyRenderBatch(List<Video> videos, int from, int to)
        {
            if (videos == null || CurrentVideoList == null)
                return;
            for (int i = from; i <= to && i < videos.Count; i++) {
                Video video = videos[i];
                if (video == null)
                    continue;
                if (i < CurrentVideoList.Count) {
                    Video temp = CurrentVideoList[i];
                    if (temp == null || temp.DataID != video.DataID)
                        CurrentVideoList[i] = video;
                    else
                        RefreshData(ref temp, video);
                } else {
                    CurrentVideoList.Add(video);
                }
            }
        }

        /// <summary>
        /// 鎼滅储
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> GetSearchCandidate()
        {
            return await Task.Run(() => {
                SearchField searchType = (SearchField)SearchSelectedIndex;
                string field = searchType.ToString();

                List<string> result = new List<string>();
                if (string.IsNullOrEmpty(SearchText))
                    return result;
                SelectWrapper<Video> wrapper = new SelectWrapper<Video>();
                SetSortOrder(wrapper); // 鎸夌収褰撳墠鎺掑簭
                wrapper.Eq("metadata.DBId", ConfigManager.Main.CurrentDBId).Eq("metadata.DataType", 0);
                SelectWrapper<Video> selectWrapper = GetSearchWrapper(searchType);
                if (selectWrapper != null)
                    wrapper.Join(selectWrapper);


                string sql = $"SELECT DISTINCT {field} FROM metadata_video " +
                            "JOIN metadata " +
                            "on metadata.DataID=metadata_video.DataID ";

                if (ExtraWrapper != null) {
                    wrapper.Join(ExtraWrapper);
                    if (!string.IsNullOrEmpty(ExtraWrapper.ExtraSql))
                        sql += ExtraWrapper.ExtraSql;
                }

                if (FilterWrapper != null) {
                    wrapper.Join(FilterWrapper);
                    if (!string.IsNullOrEmpty(FilterSQL))
                        sql += FilterSQL;
                }

                if (searchType == SearchField.ActorName)
                    sql += ActorMapper.SQL_JOIN_ACTOR;
                else if (searchType == SearchField.LabelName)
                    sql += VideoMapper.SQL_JOIN_LABEL;


                string condition_sql = wrapper.ToWhere(false) + wrapper.ToOrder()
                            + $" LIMIT 0,{SEARCH_CANDIDATE_MAX_COUNT}";

                if (searchType == SearchField.Genre) {
                    // 绫诲埆鐗规畩澶勭悊
                    string genre_sql = $"SELECT {field} FROM metadata_video " +
                            "JOIN metadata " +
                            "on metadata.DataID=metadata_video.DataID ";
                    List<Dictionary<string, object>> list = metaDataMapper.Select(genre_sql);
                    if (list != null && list.Count > 0)
                        SetGenreCandidate(field, list, ref result);
                } else {
                    List<Dictionary<string, object>> list = metaDataMapper.Select(sql + condition_sql);
                    if (list != null && list.Count > 0) {
                        foreach (Dictionary<string, object> dict in list) {
                            if (!dict.ContainsKey(field))
                                continue;
                            string value = dict[field].ToString();
                            if (string.IsNullOrEmpty(value))
                                continue;
                            result.Add(value);
                        }
                    }
                }

                return result;
            });
        }

        public void RefreshData(long dataID)
        {
            if (CurrentVideoList == null || CurrentVideoList.Count == 0)
                return;
            int idx = -1;
            for (int i = 0; i < VideoList.Count; i++) {
                if (VideoList[i].DataID == dataID) {
                    idx = i;
                    break;
                }
            }
            if (idx < 0 || idx >= CurrentVideoList.Count)
                return;
            Video video = Video.GetById(dataID);
            Video temp = VideoList[idx];
            RefreshData(ref temp, video);
            temp = CurrentVideoList[idx];
            RefreshData(ref temp, video);
        }

        public void RefreshTagStamp(long dataID)
        {
            for (int i = 0; i < VideoList.Count; i++) {
                if (VideoList[i].DataID == dataID) {
                    Video video = VideoList[i];
                    Video.SetTagStamps(ref video);
                    break;
                }
            }
            for (int i = 0; i < CurrentVideoList.Count; i++) {
                if (CurrentVideoList[i].DataID == dataID) {
                    Video video = CurrentVideoList[i];
                    Video.SetTagStamps(ref video);
                    break;
                }
            }
        }

        private void RefreshData(ref Video origin, Video target)
        {
            System.Reflection.PropertyInfo[] propertyInfos = target.GetType().GetProperties();
            foreach (var item in propertyInfos) {
                object v = item.GetValue(target);
                if (v != null) {
                    item.SetValue(origin, v);
                }
            }
        }

        private void SetGenreCandidate(string field, List<Dictionary<string, object>> list, ref List<string> result)
        {
            string search = SearchText.ToProperSql().ToLower();
            HashSet<string> set = new HashSet<string>();
            foreach (Dictionary<string, object> dict in list) {
                if (!dict.ContainsKey(field))
                    continue;
                string value = dict[field].ToString();
                if (string.IsNullOrEmpty(value))
                    continue;
                string[] arr = value.Split(new char[] { SuperUtils.Values.ConstValues.Separator }, StringSplitOptions.RemoveEmptyEntries);
                if (arr != null && arr.Length > 0) {
                    foreach (var item in arr) {
                        if (string.IsNullOrEmpty(item))
                            continue;
                        set.Add(item);
                    }
                }
            }

            result = set.Where(arg => arg.ToLower().IndexOf(search) >= 0).ToList()
                .Take(SEARCH_CANDIDATE_MAX_COUNT).ToList();
        }

    }
}
