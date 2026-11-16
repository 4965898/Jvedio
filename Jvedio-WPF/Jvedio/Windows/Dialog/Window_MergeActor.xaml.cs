using Jvedio.Core.Merge;
using Jvedio.Entity;
using SuperControls.Style;
using SuperControls.Style.Windows;
using SuperUtils.Framework.ORM.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using static Jvedio.MapperManager;

namespace Jvedio
{
    /// <summary>
    /// 重复演员合并对话框：从候选列表选出合并目标，把右键/勾选的演员并入。
    /// 候选搜索支持日文名/英文名（与演员列表搜索同语义）
    /// </summary>
    public partial class Window_MergeActor : BaseDialog, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>被合并的演员（右键这一位，或多选模式下勾选的全部）</summary>
        public List<ActorInfo> Sources { get; set; }

        private string _SourceSummary;
        public string SourceSummary {
            get { return _SourceSummary; }
            set {
                _SourceSummary = value;
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

        private ObservableCollection<ActorInfo> _Candidates = new ObservableCollection<ActorInfo>();
        public ObservableCollection<ActorInfo> Candidates {
            get { return _Candidates; }
            set {
                _Candidates = value;
                RaisePropertyChanged();
            }
        }

        private ActorInfo _SelectedTarget;
        public ActorInfo SelectedTarget {
            get { return _SelectedTarget; }
            set {
                _SelectedTarget = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasTarget));
                RaisePropertyChanged(nameof(TargetSummary));
            }
        }

        public bool HasTarget {
            get { return SelectedTarget != null; }
        }

        public string TargetSummary {
            get {
                if (SelectedTarget == null)
                    return "未选择目标演员";
                return $"合并到「{SelectedTarget.ActorName}」(ActorID={SelectedTarget.ActorID})";
            }
        }

        public Window_MergeActor(List<ActorInfo> sources)
        {
            InitializeComponent();
            Sources = sources ?? new List<ActorInfo>();
            this.DataContext = this;

            List<string> names = Sources.Where(arg => arg != null && !string.IsNullOrEmpty(arg.ActorName))
                .Select(arg => arg.ActorName).Distinct().ToList();
            SourceSummary = names.Count == 0 ? "未选中演员" :
                (names.Count == 1
                    ? $"被合并：{names[0]}（共 {Sources.Count} 位同名/重复演员）"
                    : $"被合并：{string.Join("、", names.Take(3))}{(names.Count > 3 ? " 等" : "")}（共 {Sources.Count} 位）");

            // 同名重复（最常见）：预填名字，候选列表直接列出同名条目
            if (names.Count == 1)
                SearchText = names[0];
        }

        private void BaseDialog_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCandidates();
        }

        private void TargetSearchBox_Search(object sender, RoutedEventArgs e)
        {
            LoadCandidates();
        }

        private void LoadCandidates()
        {
            List<long> excludeIds = Sources.Where(arg => arg != null && arg.ActorID > 0)
                .Select(arg => arg.ActorID).Distinct().ToList();
            string exclude = excludeIds.Count > 0 ? $"where ActorID not in ({string.Join(",", excludeIds)}) " : string.Empty;

            bool search = !string.IsNullOrEmpty(SearchText);
            string searchText = SearchText.ToProperSql();
            string searchCond = search
                ? $"and (ActorName like '%{searchText}%' or ActorNameEN like '%{searchText}%') " : string.Empty;

            string sql = "select actor_info.ActorID, ActorName, ActorNameEN, Country, Birthday, Height, Cup, Grade, " +
                "(select count(*) from metadata_to_actor where metadata_to_actor.ActorID=actor_info.ActorID) as Count " +
                $"from actor_info {exclude}{searchCond}" +
                "order by Count desc, ActorName asc limit 50";
            List<Dictionary<string, object>> list = actorMapper.Select(sql);
            List<ActorInfo> actors = list == null
                ? new List<ActorInfo>()
                : actorMapper.ToEntity<ActorInfo>(list, typeof(ActorInfo).GetProperties(), false);
            for (int i = 0; i < actors.Count; i++) {
                ActorInfo actor = actors[i];
                ActorInfo.SetImage(ref actor);
            }

            Candidates = new ObservableCollection<ActorInfo>(actors);
        }

        private void CandidateList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedTarget != null)
                DoMerge();
        }

        private void Merge_Click(object sender, RoutedEventArgs e)
        {
            DoMerge();
        }

        private void DoMerge()
        {
            if (SelectedTarget == null) {
                MessageNotify.Info("请先选择合并到的目标演员");
                return;
            }
            int sourceCount = Sources.Count(arg => arg != null && arg.ActorID > 0 && arg.ActorID != SelectedTarget.ActorID);
            if (sourceCount == 0) {
                MessageNotify.Info("没有可合并的演员");
                return;
            }

            string confirm = $"将把 {sourceCount} 位演员合并到「{SelectedTarget.ActorName}」名下：\n\n" +
                "· 影片关联合并到目标；\n" +
                "· 目标空缺的资料/评分/头像取自被合并者（不覆盖已填）；\n" +
                "· 被合并的演员条目随后删除，不可撤销。\n\n是否继续？";
            if (new MsgBox(confirm).ShowDialog() != true)
                return;

            List<long> sourceIds = Sources.Where(arg => arg != null && arg.ActorID > 0 && arg.ActorID != SelectedTarget.ActorID)
                .Select(arg => arg.ActorID).ToList();
            try {
                int merged = ActorMergeHelper.Merge(sourceIds, SelectedTarget.ActorID);
                MessageNotify.Success($"已合并 {merged} 位演员到「{SelectedTarget.ActorName}」");
                DialogResult = true;
            } catch (Exception ex) {
                App.Logger.Error($"演员合并失败：{ex.Message}");
                MessageNotify.Error($"合并失败：{ex.Message}");
            }
        }
    }
}
