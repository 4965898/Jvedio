
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
        /// 出演的作品的数量
        /// </summary>
        [TableField(exist: false)]
        public long Count { get; set; }

        private BitmapSource _smallimage;

        [TableField(exist: false)]
        public BitmapSource SmallImage {
            get { return _smallimage; }

            set {
                _smallimage = value;
                RaisePropertyChanged();
            }
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
