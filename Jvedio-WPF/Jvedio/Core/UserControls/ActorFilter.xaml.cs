using Jvedio.Core.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using static Jvedio.MapperManager;

namespace Jvedio.Core.UserControls
{
    /// <summary>
    /// 演员筛选器（仿影片筛选器 Filter 的侧栏结构）：组间 AND、组内多选 OR。
    /// 数值档位复用统计图分桶（LibraryStatisticsService，语义一致：0/非数字=未知）；
    /// 作品数量走 HAVING 子句（聚合列），由 ActorList.Select() 拼进双查询臂。
    /// </summary>
    public partial class ActorFilter : UserControl
    {
        /// <summary>作品数量分桶：首项=0（无影片演员臂），其余为区间</summary>
        private static readonly string[] CountLabels = { "0", "1~5", "6~10", "11~20", "21~50", ">50" };
        private static readonly int[][] CountRanges = {
            null,
            new[] { 1, 5 }, new[] { 6, 10 }, new[] { 11, 20 }, new[] { 21, 50 }, new[] { 51, int.MaxValue },
        };

        /// <summary>出生年代分桶（Birthday 文本列取前 4 位年，0/非数字=未知）</summary>
        private static readonly string[] DecadeLabels = { "StatUnknown", "≤1980", "1981~1990", "1991~1995", "1996~2000", "2001~2005", ">2005" };
        private static readonly string[] DecadeSql = {
            "cast(substr(ifnull(actor_info.Birthday,''),1,4) as integer)<=0",
            "cast(substr(ifnull(actor_info.Birthday,''),1,4) as integer) between 1 and 1980",
            "cast(substr(ifnull(actor_info.Birthday,''),1,4) as integer) between 1981 and 1990",
            "cast(substr(ifnull(actor_info.Birthday,''),1,4) as integer) between 1991 and 1995",
            "cast(substr(ifnull(actor_info.Birthday,''),1,4) as integer) between 1996 and 2000",
            "cast(substr(ifnull(actor_info.Birthday,''),1,4) as integer) between 2001 and 2005",
            "cast(substr(ifnull(actor_info.Birthday,''),1,4) as integer)>2005",
        };

        /// <summary>胸围/腰围/臀围分桶</summary>
        private static readonly string[] ChestLabels = { "StatUnknown", "<80", "80~89", "90~99", "≥100" };
        private static readonly string[] ChestSql = {
            "ifnull(actor_info.Chest,0)<=0",
            "actor_info.Chest>0 and actor_info.Chest<80",
            "actor_info.Chest between 80 and 89",
            "actor_info.Chest between 90 and 99",
            "actor_info.Chest>=100",
        };
        private static readonly string[] WaistLabels = { "StatUnknown", "<50", "50~59", "60~69", "≥70" };
        private static readonly string[] WaistSql = {
            "ifnull(actor_info.Waist,0)<=0",
            "actor_info.Waist>0 and actor_info.Waist<50",
            "actor_info.Waist between 50 and 59",
            "actor_info.Waist between 60 and 69",
            "actor_info.Waist>=70",
        };
        private static readonly string[] HiplineLabels = { "StatUnknown", "<85", "85~94", "95~104", "≥105" };
        private static readonly string[] HiplineSql = {
            "ifnull(actor_info.Hipline,0)<=0",
            "actor_info.Hipline>0 and actor_info.Hipline<85",
            "actor_info.Hipline between 85 and 94",
            "actor_info.Hipline between 95 and 104",
            "actor_info.Hipline>=105",
        };

        /// <summary>血型（站内解析值为 A/B/O/AB 单字母，精确匹配）</summary>
        private static readonly string[] BloodLabels = { "A", "B", "O", "AB", "StatUnknown" };
        private static readonly string[] BloodSql = {
            "actor_info.BloodType='A'", "actor_info.BloodType='B'",
            "actor_info.BloodType='O'", "actor_info.BloodType='AB'",
            "ifnull(actor_info.BloodType,'')=''",
        };

        /// <summary>应用筛选（ActorList 订阅后重置页码并 Select()）</summary>
        public event EventHandler ApplyRequested;
        /// <summary>关闭侧栏（ActorList 订阅后收起）</summary>
        public event EventHandler CloseRequested;

        /// <summary>惰性面板加载守卫：面板名 → 已加载</summary>
        private readonly HashSet<string> LoadedPanels = new HashSet<string>();

        public ActorFilter()
        {
            InitializeComponent();
            BuildStaticToggles();
        }

        #region "构建静态档位"

        private void BuildStaticToggles()
        {
            BuildBucketToggles(countWrapPanel, CountLabels);
            BuildBucketToggles(ageWrapPanel, LibraryStatisticsService.AgeLabels);
            BuildBucketToggles(decadeWrapPanel, DecadeLabels);
            BuildBucketToggles(bloodWrapPanel, BloodLabels);
            BuildBucketToggles(heightWrapPanel, LibraryStatisticsService.HeightLabels);
            BuildBucketToggles(weightWrapPanel, LibraryStatisticsService.WeightLabels);
            BuildBucketToggles(chestWrapPanel, ChestLabels);
            BuildBucketToggles(waistWrapPanel, WaistLabels);
            BuildBucketToggles(hiplineWrapPanel, HiplineLabels);
            BuildBucketToggles(shoeWrapPanel, LibraryStatisticsService.ShoeLabels);
        }

        /// <summary>按分桶标签生成多选档位按钮（首项为语言键的翻译成文案），Tag=桶索引</summary>
        private void BuildBucketToggles(Panel panel, string[] labels)
        {
            for (int i = 0; i < labels.Length; i++) {
                string label = labels[i];
                if (label == "StatUnknown")
                    label = SuperControls.Style.LangManager.GetValueByKey("StatUnknown");
                ToggleButton button = new ToggleButton {
                    Content = label,
                    IsChecked = false,
                    Style = (Style)this.Resources["FilterToggleButton"],
                    Tag = i,
                };
                panel.Children.Add(button);
            }
        }

        #endregion

        #region "惰性加载 DISTINCT 面板"

        private void Cup_Expand(object sender, EventArgs e)
        {
            if (LoadedPanels.Contains("Cup"))
                return;
            LoadedPanels.Add("Cup");
            LoadDistinctValues(cupWrapPanel, "select distinct Cup from actor_info where Cup is not null and Cup <> '' and Cup <> 'Z' order by Cup", "Cup");
        }

        private void Country_Expand(object sender, EventArgs e)
        {
            if (LoadedPanels.Contains("Country"))
                return;
            LoadedPanels.Add("Country");
            LoadDistinctValues(countryWrapPanel, "select distinct Country from actor_info where Country is not null and Country <> '' order by Country", "Country");
        }

        private void Nation_Expand(object sender, EventArgs e)
        {
            if (LoadedPanels.Contains("Nation"))
                return;
            LoadedPanels.Add("Nation");
            LoadDistinctValues(nationWrapPanel, "select distinct Nation from actor_info where Nation is not null and Nation <> '' order by Nation", "Nation");
        }

        /// <summary>DISTINCT 值 → 多选按钮（Tag=值本身），未知按钮固定在最前（Tag=-1）</summary>
        private void LoadDistinctValues(Panel panel, string sql, string column)
        {
            try {
                ToggleButton unknown = new ToggleButton {
                    Content = SuperControls.Style.LangManager.GetValueByKey("StatUnknown"),
                    IsChecked = false,
                    Style = (Style)this.Resources["FilterToggleButton"],
                    Tag = -1,
                };
                panel.Children.Add(unknown);
                List<Dictionary<string, object>> list = actorMapper.Select(sql);
                List<string> values = (list ?? new List<Dictionary<string, object>>())
                    .Select(arg => arg.ContainsKey(column) && arg[column] != null ? arg[column].ToString().Trim() : "")
                    .Where(arg => arg != "").Distinct().OrderBy(arg => arg).ToList();
                foreach (string value in values)
                    panel.Children.Add(new ToggleButton {
                        Content = value,
                        IsChecked = false,
                        Style = (Style)this.Resources["FilterToggleButton"],
                        Tag = value,
                    });
            } catch (Exception ex) {
                App.Logger.Error($"筛选器加载 {column} 失败：{ex.Message}");
            }
        }

        #endregion

        #region "条件生成"

        /// <summary>勾选的 ToggleButton 桶索引（Tag 为 int）</summary>
        private List<int> CheckedIndexes(Panel panel)
        {
            return panel.Children.OfType<ToggleButton>()
                .Where(arg => arg.IsChecked == true && arg.Tag is int)
                .Select(arg => (int)arg.Tag).ToList();
        }

        /// <summary>按 (标签组, SQL 组) 生成组内 OR 子句；sql 与标签一一对应</summary>
        private string OrGroup(string[] sqlList, List<int> checkedIndexes)
        {
            if (checkedIndexes == null || checkedIndexes.Count == 0)
                return null;
            List<string> parts = checkedIndexes
                .Where(arg => arg >= 0 && arg < sqlList.Length)
                .Select(arg => sqlList[arg]).Distinct().ToList();
            return parts.Count == 0 ? null : CombineOr(parts);
        }

        /// <summary>值组（罩杯/国家/民族）：Tag=-1 为未知，其余为字面值</summary>
        private string OrValueGroup(Panel panel, string column, string unknownSql)
        {
            List<ToggleButton> checkedButtons = panel.Children.OfType<ToggleButton>()
                .Where(arg => arg.IsChecked == true).ToList();
            if (checkedButtons.Count == 0)
                return null;
            List<string> parts = new List<string>();
            foreach (ToggleButton button in checkedButtons) {
                if (button.Tag is int idx && idx == -1) {
                    parts.Add(unknownSql);
                    continue;
                }
                string value = button.Tag?.ToString();
                if (!string.IsNullOrEmpty(value))
                    parts.Add($"actor_info.{column}='{value.Replace("'", "''")}'");
            }
            return parts.Count == 0 ? null : CombineOr(parts);
        }

        private static string CombineOr(List<string> parts)
        {
            return parts.Count == 1 ? parts[0] : "(" + string.Join(" or ", parts) + ")";
        }

        /// <summary>
        /// 生成 WHERE 条件列表（组间 AND）：性别/年龄/出生年代/血型/身高/体重/胸围/腰围/臀围/鞋码/评分/罩杯/国家/民族/爱好
        /// </summary>
        public List<string> BuildWhereConditions()
        {
            List<string> conditions = new List<string>();

            // 性别：RadioButton 组（0=全部不筛，1=女,2=男,3=未知）
            List<RadioButton> genders = genderWrapPanel.Children.OfType<RadioButton>().ToList();
            for (int i = 0; i < genders.Count; i++) {
                if (genders[i].IsChecked != true || i == 0)
                    continue;
                string sql = i == 1 ? "actor_info.Gender=1" : i == 2 ? "actor_info.Gender=2" : "ifnull(actor_info.Gender,0)=0";
                conditions.Add(sql);
            }

            string age = OrGroup(LibraryStatisticsService.AgeSql, CheckedIndexes(ageWrapPanel));
            if (age != null) conditions.Add(age);
            string decade = OrGroup(DecadeSql, CheckedIndexes(decadeWrapPanel));
            if (decade != null) conditions.Add(decade);
            string blood = OrGroup(BloodSql, CheckedIndexes(bloodWrapPanel));
            if (blood != null) conditions.Add(blood);
            string height = OrGroup(LibraryStatisticsService.HeightSql, CheckedIndexes(heightWrapPanel));
            if (height != null) conditions.Add(height);
            string weight = OrGroup(LibraryStatisticsService.WeightSql, CheckedIndexes(weightWrapPanel));
            if (weight != null) conditions.Add(weight);
            string chest = OrGroup(ChestSql, CheckedIndexes(chestWrapPanel));
            if (chest != null) conditions.Add(chest);
            string waist = OrGroup(WaistSql, CheckedIndexes(waistWrapPanel));
            if (waist != null) conditions.Add(waist);
            string hipline = OrGroup(HiplineSql, CheckedIndexes(hiplineWrapPanel));
            if (hipline != null) conditions.Add(hipline);
            string shoe = OrGroup(LibraryStatisticsService.ShoeSql, CheckedIndexes(shoeWrapPanel));
            if (shoe != null) conditions.Add(shoe);

            string cup = OrValueGroup(cupWrapPanel, "Cup", "ifnull(actor_info.Cup,'') in ('','Z')");
            if (cup != null) conditions.Add(cup);
            string country = OrValueGroup(countryWrapPanel, "Country", "ifnull(actor_info.Country,'')=''");
            if (country != null) conditions.Add(country);
            string nation = OrValueGroup(nationWrapPanel, "Nation", "ifnull(actor_info.Nation,'')=''");
            if (nation != null) conditions.Add(nation);

            // 评分（双滑块范围；0= unrated，min==max 时精确匹配）
            double minRate = rateSlider.MinValue;
            double maxRate = rateSlider.MaxValue;
            if (minRate != rateSlider.Minimum || maxRate != rateSlider.Maximum) {
                conditions.Add(minRate == maxRate
                    ? $"actor_info.Grade={minRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                    : $"actor_info.Grade>={minRate.ToString(System.Globalization.CultureInfo.InvariantCulture)} and actor_info.Grade<={maxRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            // 爱好：空格分隔关键词，组内 OR 包含匹配
            string hobby = BuildHobbyCondition();
            if (hobby != null) conditions.Add(hobby);

            return conditions;
        }

        /// <summary>爱好关键词 → actor_info.Hobby like '%kw%'（多词 OR）</summary>
        private string BuildHobbyCondition()
        {
            string text = (hobbyBox.Text ?? "").Trim();
            if (text == "")
                return null;
            string[] keywords = text.Split(new[] { ' ', '\u3000', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> parts = keywords.Select(arg => $"actor_info.Hobby like '%{arg.Replace("'", "''")}%'").Distinct().ToList();
            return CombineOr(parts);
        }

        /// <summary>
        /// 作品数量档位 → HAVING 子句（聚合列 count(*)，拼进双查询臂 GROUP BY 之后）；
        /// includeZeroCount=true 表示勾了 0 作品档（无影片演员臂保留）
        /// </summary>
        public string BuildWorksCountHaving(out bool includeZeroCount)
        {
            includeZeroCount = false;
            List<int> checkedIndexes = CheckedIndexes(countWrapPanel);
            if (checkedIndexes.Count == 0)
                return null;
            List<string> parts = new List<string>();
            foreach (int index in checkedIndexes) {
                if (index == 0) {
                    includeZeroCount = true;
                    parts.Add("count(*) = 0");
                } else if (index >= 1 && index < CountRanges.Length) {
                    int lo = CountRanges[index][0];
                    int hi = CountRanges[index][1];
                    parts.Add(hi == int.MaxValue ? $"count(*) >= {lo}" : $"count(*) between {lo} and {hi}");
                }
            }
            return parts.Count == 0 ? null : CombineOr(parts);
        }

        #endregion

        #region "事件"

        private void SetGender(object sender, RoutedEventArgs e) { }

        private void SetAvatar(object sender, RoutedEventArgs e) { }

        /// <summary>头像筛选选中项：0=全部（不筛）、1=有头像、2=无头像。有/无由 ActorList 扫描头像目录生成集合条件</summary>
        public int GetAvatarSelection()
        {
            List<RadioButton> radios = avatarWrapPanel.Children.OfType<RadioButton>().ToList();
            for (int i = 0; i < radios.Count; i++)
                if (radios[i].IsChecked == true)
                    return i;
            return 0;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            WrapPanel[] panels = {
                countWrapPanel, ageWrapPanel, decadeWrapPanel, bloodWrapPanel, heightWrapPanel,
                weightWrapPanel, chestWrapPanel, waistWrapPanel, hiplineWrapPanel, shoeWrapPanel,
                cupWrapPanel, countryWrapPanel, nationWrapPanel,
            };
            foreach (Panel panel in panels)
                foreach (ToggleButton button in panel.Children.OfType<ToggleButton>())
                    button.IsChecked = false;
            List<RadioButton> genders = genderWrapPanel.Children.OfType<RadioButton>().ToList();
            for (int i = 0; i < genders.Count; i++)
                genders[i].IsChecked = i == 0;
            List<RadioButton> avatars = avatarWrapPanel.Children.OfType<RadioButton>().ToList();
            for (int i = 0; i < avatars.Count; i++)
                avatars[i].IsChecked = i == 0;
            rateSlider.MinValue = rateSlider.Minimum;
            rateSlider.MaxValue = rateSlider.Maximum;
            hobbyBox.Text = "";
            ApplyRequested?.Invoke(this, EventArgs.Empty);
        }

        private void HobbyBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) {
                ApplyRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        private void HideGrid(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
