
using Jvedio.Core.Enums;
using Jvedio.Core.Global;
using Jvedio.Core.Media;
using Jvedio.Core.Scan;
using SuperUtils.Framework.ORM.Attributes;
using SuperUtils.Framework.ORM.Enums;
using SuperUtils.Framework.ORM.Wrapper;
using SuperUtils.IO;
using SuperUtils.Reflections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media.Imaging;

namespace Jvedio.Entity
{
    /// <summary>
    /// 演员卡片信息条目（不入库，仅供 UI 展示）：小标签 + 数值
    /// </summary>
    public class ActorInfoChip
    {
        public string Label { get; set; }
        public string Value { get; set; }
    }

    [Table(tableName: "actor_info")]
    public class ActorInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        #region "属性"

        [TableId(IdType.AUTO)]
        public long ActorID { get; set; }

        private string _ActorName = string.Empty;
        public string ActorName {
            get { return _ActorName; }
            set {
                _ActorName = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 英文名（罗马字），如 三上悠亜 → Mikami Yua；刮削时从 JavDB 演员 slug 提取，或假名转罗马字兜底
        /// </summary>
        private string _ActorNameEN = string.Empty;
        public string ActorNameEN {
            get { return _ActorNameEN; }
            set {
                _ActorNameEN = value;
                RaisePropertyChanged();
            }
        }

        private string _Country = string.Empty;
        public string Country {
            get { return _Country; }
            set {
                _Country = value;
                RaisePropertyChanged();
            }
        }
        private string _Nation = string.Empty;
        public string Nation {
            get { return _Nation; }
            set {
                _Nation = value;
                RaisePropertyChanged();
            }
        }
        private string _BirthPlace = string.Empty;
        public string BirthPlace {
            get { return _BirthPlace; }
            set {
                _BirthPlace = value;
                RaisePropertyChanged();
            }
        }
        private string _Birthday = string.Empty;

        public string Birthday {
            get { return _Birthday; }
            set {
                _Birthday = value;
                RaisePropertyChanged();
            }
        }

        private int _Age;

        public int Age {
            get { return _Age; }
            set {
                _Age = value;
                RaisePropertyChanged();
            }
        }

        private string _BloodType = string.Empty;
        public string BloodType {
            get { return _BloodType; }
            set {
                _BloodType = value;
                RaisePropertyChanged();
            }
        }


        private int _Height;

        public int Height {
            get { return _Height; }
            set {
                _Height = value;
                RaisePropertyChanged();
            }
        }

        private int _Weight;

        public int Weight {
            get { return _Weight; }
            set {
                _Weight = value;
                RaisePropertyChanged();
            }
        }

        public Gender _Gender = Gender.Girl;

        public Gender Gender {
            get { return _Gender; }
            set {
                _Gender = value;
                RaisePropertyChanged();
            }
        }

        private string _Hobby = string.Empty;

        public string Hobby {
            get { return _Hobby; }
            set {
                _Hobby = value;
                RaisePropertyChanged();
            }
        }

        private char _Cup = 'Z';

        public char Cup {
            get { return _Cup; }
            set {
                _Cup = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 鞋码（脚码），国内常见 38/40 数字（hitchao/Jvedio issues 优化）
        /// </summary>
        private string _ShoeSize = string.Empty;

        public string ShoeSize {
            get { return _ShoeSize; }
            set {
                _ShoeSize = value;
                RaisePropertyChanged();
            }
        }

        private int _Chest;

        public int Chest {
            get { return _Chest; }
            set {
                _Chest = value;
                RaisePropertyChanged();
            }
        }

        private int _Waist;

        public int Waist {
            get { return _Waist; }
            set {
                _Waist = value;
                RaisePropertyChanged();
            }
        }

        private int _Hipline;

        public int Hipline {
            get { return _Hipline; }
            set {
                _Hipline = value;
                RaisePropertyChanged();
            }
        }

        public string WebType { get; set; } = string.Empty;

        public string WebUrl { get; set; } = string.Empty;

        private float _Grade;

        public float Grade {
            get { return _Grade; }
            set {
                _Grade = value;
                RaisePropertyChanged();
            }
        }

        public string ExtraInfo { get; set; } = string.Empty;

        public string CreateDate { get; set; } = string.Empty;

        public string UpdateDate { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        [TableField(exist: false)]
        public long ImageID { get; set; }

        /// <summary>
        /// 出演的作品的数量（UI 角标绑定，需 INPC：条目替换后补赋值必须通知刷新，否则角标停在 0）
        /// </summary>
        private long _Count;
        [TableField(exist: false)]
        public long Count {
            get { return _Count; }
            set {
                _Count = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 编辑模式下的勾选状态(仅 UI 使用,不入库)
        /// </summary>
        private bool _IsSelected;
        [TableField(exist: false)]
        public bool IsSelected {
            get { return _IsSelected; }
            set {
                _IsSelected = value;
                RaisePropertyChanged();
            }
        }

        private BitmapSource _smallimage;

        [TableField(exist: false)]
        public BitmapSource SmallImage {
            get { return _smallimage; }

            set {
                _smallimage = value;
                RaisePropertyChanged();
            }
        }

        private List<ActorInfoChip> _InfoChips;

        /// <summary>
        /// 演员卡片信息条目（完整版，列表模式）：身高/体重/生日/三围/罩杯/鞋码/血型/出身/国家/民族/爱好，
        /// 空字段自动跳过，每条为「标签+数值」小卡片；标签经多语言资源解析（缺失时回退中文）
        /// </summary>
        [TableField(exist: false)]
        public List<ActorInfoChip> InfoChips {
            get { return BuildInfoChips(false); }
            set { _InfoChips = value; }// 兼容反射刷新（RefreshData 会写回），实际展示以 getter 计算为准
        }

        /// <summary>演员卡片信息条目（精简版，缩略图模式）：生日只显年龄，省略出身/国家/民族/爱好</summary>
        [TableField(exist: false)]
        public List<ActorInfoChip> InfoChipsCompact {
            get { return BuildInfoChips(true); }
            set { _InfoChips = value; }
        }

        private static string ChipLabel(string key, string fallback)
        {
            string value = SuperControls.Style.LangManager.GetValueByKey(key);
            if (string.IsNullOrEmpty(value) || value.Equals(key))
                return fallback;
            return value;
        }

        private static string ChipValue(string value)
        {
            // 超长值截断展示，完整内容走 ToolTip
            if (string.IsNullOrEmpty(value) || value.Length <= 24)
                return value;
            return value.Substring(0, 24) + "…";
        }

        private List<ActorInfoChip> BuildInfoChips(bool compact)
        {
            List<ActorInfoChip> chips = new List<ActorInfoChip>();
            if (Height > 0)
                chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Height", "身高"), Value = $"{Height}cm" });
            if (Weight > 0)
                chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Weight", "体重"), Value = $"{Weight}kg" });
            if (compact) {
                if (Age > 0)
                    chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Age", "年龄"), Value = $"{Age}岁" });
            } else if (!string.IsNullOrEmpty(Birthday)) {
                chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Birthday", "生日"), Value = ChipValue(Age > 0 ? $"{Birthday} ({Age}岁)" : Birthday) });
            } else if (Age > 0) {
                chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Age", "年龄"), Value = $"{Age}岁" });
            }
            if (Chest > 0 && Waist > 0 && Hipline > 0) {
                chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Measurements", "三围"), Value = $"{Chest}-{Waist}-{Hipline}" });
            } else {
                if (Chest > 0)
                    chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Chest", "胸围"), Value = Chest.ToString() });
                if (Waist > 0)
                    chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Waist", "腰围"), Value = Waist.ToString() });
                if (Hipline > 0)
                    chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Hipline", "臀围"), Value = Hipline.ToString() });
            }
            if (Cup >= 'A' && Cup < 'Z')
                chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Cup", "罩杯"), Value = Cup.ToString() });
            if (!string.IsNullOrEmpty(ShoeSize))
                chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_ShoeSize", "鞋码"), Value = ShoeSize });
            if (!string.IsNullOrEmpty(BloodType))
                chips.Add(new ActorInfoChip { Label = ChipLabel("BloodType", "血型"), Value = BloodType });
            if (!compact) {
                if (!string.IsNullOrEmpty(BirthPlace))
                    chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Birthplace", "出身"), Value = ChipValue(BirthPlace) });
                if (!string.IsNullOrEmpty(Country))
                    chips.Add(new ActorInfoChip { Label = ChipLabel("Country", "国家"), Value = ChipValue(Country) });
                if (!string.IsNullOrEmpty(Nation))
                    chips.Add(new ActorInfoChip { Label = ChipLabel("Nationality", "民族"), Value = ChipValue(Nation) });
                if (!string.IsNullOrEmpty(Hobby))
                    chips.Add(new ActorInfoChip { Label = ChipLabel("Actress_Hobby", "爱好"), Value = ChipValue(Hobby) });
            }
            return chips;
        }

        /// <summary>
        /// 通知资料展示相关计算属性已变化（列表刷新时由外部调用，避免 RefreshData 逐字段反射写入后卡片信息不更新）
        /// </summary>
        public void NotifyInfoDisplayChanged()
        {
            RaisePropertyChanged(nameof(InfoChips));
            RaisePropertyChanged(nameof(InfoChipsCompact));
            RaisePropertyChanged(nameof(InfoString));
        }

        private string _InfoString;

        [TableField(exist: false)]
        public string InfoString {
            get {

                StringBuilder builder = new StringBuilder();
                if (Height > 0)
                    builder.Append($"{Height}cm ");
                if (Weight > 0)
                    builder.Append($"{Weight}kg ");
                if (!string.IsNullOrEmpty(Birthday))
                    builder.Append($"{Birthday} ");
                if (Age > 0)
                    builder.Append($"({Age}岁) ");
                if (!string.IsNullOrEmpty(BirthPlace))
                    builder.Append($"{BirthPlace} ");
                if (Chest > 0)
                    builder.Append($"{Chest} ");
                if (Waist > 0)
                    builder.Append($"{Waist} ");
                if (Hipline > 0)
                    builder.Append($"{Hipline} ");

                builder.Append(Environment.NewLine);

                if (Cup >= 'A' && Cup < 'Z')
                    builder.Append($"{Cup} ");
                if (!string.IsNullOrEmpty(Country))
                    builder.Append($"{Country} ");
                if (!string.IsNullOrEmpty(Nation))
                    builder.Append($"{Nation} ");

                if (!string.IsNullOrEmpty(BloodType))
                    builder.Append($"{BloodType} ");
                if (!string.IsNullOrEmpty(Hobby))
                    builder.Append($"{Hobby} ");

                if (builder.Length == 0)
                    builder.Append("-");

                return builder.ToString();
            }

            set {
                _InfoString = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        public ActorInfo()
        {
            Cup = 'Z';
            Gender = Gender.Girl;
        }

        /// <summary>
        /// 按生日计算当前年龄（不足周岁向下取整；生日无效返回 0）
        /// </summary>
        public static int CalculateAge(string birthday, DateTime? today = null)
        {
            if (string.IsNullOrEmpty(birthday))
                return 0;
            if (!DateTime.TryParse(birthday, out DateTime bd))
                return 0;
            DateTime now = today ?? DateTime.Today;
            int age = now.Year - bd.Year;
            if (now.Month < bd.Month || (now.Month == bd.Month && now.Day < bd.Day))
                age--;
            return age < 0 ? 0 : age;
        }

        public static void SetImage(ref ActorInfo actorInfo)
        {
            // 加载图片
            PathType pathType = (PathType)ConfigManager.Settings.PicPathMode;
            BitmapImage smallimage = null;
            if (pathType != PathType.RelativeToData) {
                // 如果是相对于影片格式的，则不设置图片
                string smallImagePath = actorInfo.GetImagePath();
                smallimage = ImageCache.Get(smallImagePath);
            }

            if (smallimage == null)
                smallimage = MetaData.DefaultActorImage;
            actorInfo.SmallImage = smallimage;
        }

        public string GetImagePath(string dataPath = "", string ext = ".jpg", bool searchExt = true)
        {
            string result = string.Empty;
            PathType pathType = (PathType)ConfigManager.Settings.PicPathMode;
            string basePicPath = ConfigManager.Settings.PicPaths[pathType.ToString()].ToString();

            // 独立演员头像目录（hitchao/Jvedio#445/#338/#270）：
            // 图片路径为「相对影片」时也回退到软件数据目录的 Actresses（Gfriends 等平铺导出目录），
            // 保证演员列表页/详情页无需影片上下文也能显示头像；Actresses 目录不随图片路径模式变化。
            string actressDir = null;
            if (pathType == PathType.RelativeToData) {
                string appPath = ConfigManager.Settings.PicPaths[PathType.RelativeToApp.ToString()].ToString();
                actressDir = System.IO.Path.Combine(PathManager.CurrentUserFolder, appPath, "Actresses");
            } else {
                if (pathType == PathType.RelativeToApp)
                    basePicPath = System.IO.Path.Combine(PathManager.CurrentUserFolder, basePicPath);
                actressDir = System.IO.Path.Combine(basePicPath, "Actresses");
            }
            if (!Directory.Exists(actressDir))
                FileHelper.TryCreateDir(actressDir);

            // 优先使用 1_name.jpg 的方式
            result = System.IO.Path.Combine(actressDir, $"{ActorID}_{ActorName}{ext}");
            if (!File.Exists(result))
                result = System.IO.Path.Combine(actressDir, $"{ActorName}{ext}");
            if (File.Exists(result))
                return result;

            // 相对影片：影片目录的演员头像子目录（.actor / .actors，MDCx 等外部刮削器输出）
            if (pathType == PathType.RelativeToData && !string.IsNullOrEmpty(dataPath)) {
                string basePath = System.IO.Path.GetDirectoryName(dataPath);
                Dictionary<string, string> dict = (Dictionary<string, string>)ConfigManager.Settings.PicPaths[pathType.ToString()];
                if (dict != null && dict.ContainsKey("ActorImagePath")) {
                    string[] dirs = {
                        System.IO.Path.Combine(basePath, dict["ActorImagePath"]),
                        System.IO.Path.Combine(basePath, ".actors"),
                        System.IO.Path.Combine(basePath, ".actor"),
                    };
                    foreach (string dir in dirs) {
                        string[] arr = FileHelper.TryGetAllFiles(dir, "*.*");
                        if (arr == null || arr.Length == 0)
                            continue;
                        foreach (string item in arr) {
                            if (!ScanTask.PICTURE_EXTENSIONS_LIST.Contains(System.IO.Path.GetExtension(item).ToLower()))
                                continue;
                            if (System.IO.Path.GetFileNameWithoutExtension(item).ToLower().Equals(ActorName))
                                return item;
                        }
                    }
                }
            }

            // 替换成其他扩展名
            if (searchExt && !File.Exists(result))
                result = FileHelper.FindWithExt(result, ScanTask.PICTURE_EXTENSIONS_LIST);
            return result;
        }


        public static ActorInfo GetById(long actorID)
        {
            SelectWrapper<ActorInfo> wrapper = new SelectWrapper<ActorInfo>();
            wrapper.Eq("ActorID", actorID);
            ActorInfo actorInfo = MapperManager.actorMapper.SelectById(wrapper);
            if (actorInfo == null)
                return null;
            ActorInfo.SetImage(ref actorInfo);
            return actorInfo;
        }

        public override bool Equals(object obj)
        {
            ActorInfo actorInfo = obj as ActorInfo;
            if (actorInfo == null)
                return false;
            return this.ActorID == actorInfo.ActorID;
        }

        public override int GetHashCode()
        {
            return this.ActorID.GetHashCode();
        }


        public override string ToString()
        {
            return ClassUtils.ToString(this);
        }
    }
}
