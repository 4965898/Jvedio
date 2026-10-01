using Jvedio.Core.Config.Base;
using System.Collections.Generic;

namespace Jvedio.Core.Config.Data
{
    public class FilterConfig : AbstractConfig
    {
        private FilterConfig() : base("FilterConfig")
        {
            ExpandTag = true;

        }

        private bool _ExpandTag;
        public bool ExpandTag {
            get { return _ExpandTag; }
            set {
                _ExpandTag = value;
                RaisePropertyChanged();
            }
        }
        private bool _ExpandCommon;
        public bool ExpandCommon {
            get { return _ExpandCommon; }
            set {
                _ExpandCommon = value;
                RaisePropertyChanged();
            }
        }
        private bool _ExpandGenre;
        public bool ExpandGenre {
            get { return _ExpandGenre; }
            set {
                _ExpandGenre = value;
                RaisePropertyChanged();
            }
        }
        private bool _ExpandSeries;
        public bool ExpandSeries {
            get { return _ExpandSeries; }
            set {
                _ExpandSeries = value;
                RaisePropertyChanged();
            }
        }
        private bool _ExpandDirector;
        public bool ExpandDirector {
            get { return _ExpandDirector; }
            set {
                _ExpandDirector = value;
                RaisePropertyChanged();
            }
        }
        private bool _ExpandStudio;
        public bool ExpandStudio {
            get { return _ExpandStudio; }
            set {
                _ExpandStudio = value;
                RaisePropertyChanged();
            }
        }

        private static FilterConfig _instance = null;

        public static FilterConfig CreateInstance()
        {
            if (_instance == null)
                _instance = new FilterConfig();

            return _instance;
        }

        /// <summary>
        /// 保存的筛选器（智能收藏）：名称 + 筛选面板/搜索/排序状态的 JSON 快照
        /// </summary>
        public List<SavedFilter> SavedFilters { get; set; } = new List<SavedFilter>();
    }

    /// <summary>
    /// 一份命名筛选方案；State 为 Filter.xaml CaptureState() 的 JSON
    /// </summary>
    public class SavedFilter
    {
        public string Name { get; set; }
        public string State { get; set; }
    }

    /// <summary>
    /// 筛选面板完整状态快照（JSON 序列化进 SavedFilter.State）。
    /// 惰性加载的面板（年份/类别/系列/导演/制作商/标记）应用时按需展开加载后回填
    /// </summary>
    public class FilterState
    {
        public List<long> TagIds { get; set; } = new List<long>();
        public int PlayRadio { get; set; } = -1;
        public List<int> VideoTypes { get; set; } = new List<int>();
        public bool OnlySubsection { get; set; }
        public int PosterSel { get; set; } = -1;
        public int ThumbSel { get; set; } = -1;
        public int ActorSel { get; set; } = -1;
        public int SubSel { get; set; } = -1;
        public int TimeIndex { get; set; }
        public int SizeIndex { get; set; }
        public double RateMin { get; set; } = -1;
        public double RateMax { get; set; } = -1;
        public List<string> Years { get; set; } = new List<string>();
        public List<string> Genres { get; set; } = new List<string>();
        public List<string> SeriesList { get; set; } = new List<string>();
        public List<string> Directors { get; set; } = new List<string>();
        public List<string> Studios { get; set; } = new List<string>();
        public string GenreSearch { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public int SearchFieldIndex { get; set; }
        public int SortType { get; set; }
        public bool SortDescending { get; set; }
    }
}
