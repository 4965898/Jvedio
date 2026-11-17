using Jvedio.Core.Crawler;
using Jvedio.Core.Media;
using Jvedio.Core.Utils;
using Jvedio.Entity;
using SuperControls.Style;
using SuperUtils.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Jvedio.MapperManager;

namespace Jvedio.Core.UserControls
{
    /// <summary>
    /// ActorInfoView.xaml 的交互逻辑
    /// </summary>
    public partial class ActorInfoView : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        #region "事件"
        public event Action Close;

        #endregion


        #region "属性"




        private ActorInfo _CurrentActorInfo;

        public ActorInfo CurrentActorInfo {
            get { return _CurrentActorInfo; }

            set {
                _CurrentActorInfo = value;
                RaisePropertyChanged();
                RefreshBirthdayUI();
                RefreshAge();
                LoadActorOnlineJumpButtons();
            }
        }

        /// <summary>
        /// 详情页展示的年龄：生日有效时按当前日期实时计算，否则用库内 Age 值
        /// </summary>
        private int _DisplayAge;

        public int DisplayAge {
            get { return _DisplayAge; }

            set {
                _DisplayAge = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        private void RefreshBirthdayUI()
        {
            if (BirthdayDatePicker == null)
                return;
            if (CurrentActorInfo != null && DateTime.TryParse(CurrentActorInfo.Birthday, out DateTime bd))
                BirthdayDatePicker.SelectedDate = bd;
            else
                BirthdayDatePicker.SelectedDate = null;
        }

        private void RefreshAge()
        {
            if (CurrentActorInfo == null)
                return;
            string bd = CurrentActorInfo.Birthday;
            DisplayAge = !string.IsNullOrEmpty(bd) && DateTime.TryParse(bd, out DateTime _)
                ? ActorInfo.CalculateAge(bd)
                : CurrentActorInfo.Age;
        }

        /// <summary>
        /// 在线搜索：按当前演员名生成各站搜索跳转按钮（位置：爱好下方，样式同影片详情页在线观看按钮）。
        /// 与影片详情页共用 OnlineSites.Sites 站点列表与「选项-网络」自定义网址（联动）。
        /// 停用的站点不出现；按钮带可达性小圆点（与影片详情页同款）。
        /// </summary>
        private void LoadActorOnlineJumpButtons()
        {
            if (actorOnlineJumpPanel == null)
                return;
            actorOnlineJumpPanel.Children.Clear();
            string name = CurrentActorInfo?.ActorName;
            if (string.IsNullOrEmpty(name))
                return;
            string encodedName = Uri.EscapeDataString(name);
            foreach (OnlineSite site in OnlineSites.Sites) {
                if (!site.Enabled)
                    continue;
                string url = site.GetSearchUrl(encodedName);
                Button button = new Button() {
                    Style = (Style)FindResource("OnlineJumpButton"),
                    Tag = site.Name,
                    ToolTip = url,
                };
                StackPanel content = new StackPanel() { Orientation = Orientation.Horizontal };
                content.Children.Add(new TextBlock() { Text = site.Name });
                Border dot = new Border() {
                    Name = "stateDot",
                    Width = 7,
                    Height = 7,
                    Margin = new Thickness(5, 0, 0, 0),
                    CornerRadius = new CornerRadius(3.5),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = new SolidColorBrush(GetOnlineStateColor(OnlineSiteStatus.Get(site.Name))),
                };
                content.Children.Add(dot);
                button.Content = content;
                button.Click += (s, e) => FileHelper.TryOpenUrl(url);
                actorOnlineJumpPanel.Children.Add(button);
            }
            OnlineSiteStatus.ProbeAll(OnlineSites.Sites);
        }

        private static System.Windows.Media.Color GetOnlineStateColor(OnlineSiteState state)
        {
            switch (state) {
                case OnlineSiteState.Ok: return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#67C23A");
                case OnlineSiteState.MaybeBlocked: return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E6A23C");
                case OnlineSiteState.Fail: return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F56C6C");
                default: return System.Windows.Media.Colors.Gray;
            }
        }

        private void BirthdayDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CurrentActorInfo == null)
                return;
            DatePicker dp = sender as DatePicker;
            if (dp.SelectedDate.HasValue) {
                string s = dp.SelectedDate.Value.ToString("yyyy-MM-dd");
                if (CurrentActorInfo.Birthday != s) {
                    CurrentActorInfo.Birthday = s;
                    // 生日变更 → 实时重算年龄
                    CurrentActorInfo.Age = ActorInfo.CalculateAge(s);
                }
            } else {
                CurrentActorInfo.Birthday = "";
            }
            RefreshAge();
        }


        public ActorInfoView()
        {
            InitializeComponent();
            this.DataContext = this;
        }


        private void OpenActorPath(object sender, RoutedEventArgs e)
        {
            if (CurrentActorInfo != null)
                FileHelper.TryOpenSelectPath(CurrentActorInfo.GetImagePath());
        }

        private void CopyActorName(object sender, MouseButtonEventArgs e)
        {
            if (CurrentActorInfo == null || string.IsNullOrEmpty(CurrentActorInfo.ActorName))
                return;
            ClipBoard.TrySetDataObject(CurrentActorInfo.ActorName);
            MessageNotify.Success($"{LangManager.GetValueByKey("Message_Copied")} {CurrentActorInfo.ActorName}");
        }

        private void CopyActorNameEN(object sender, MouseButtonEventArgs e)
        {
            if (CurrentActorInfo == null || string.IsNullOrEmpty(CurrentActorInfo.ActorNameEN))
                return;
            ClipBoard.TrySetDataObject(CurrentActorInfo.ActorNameEN);
            MessageNotify.Success($"{LangManager.GetValueByKey("Message_Copied")} {CurrentActorInfo.ActorNameEN}");
        }

        /// <summary>
        /// 转换当前演员英文名：假名→罗马字兜底（结果含汉字/假名残留则提示手动填写）
        /// </summary>
        private void ConvertCurrentActorNameEN(object sender, MouseButtonEventArgs e)
        {
            if (CurrentActorInfo == null || string.IsNullOrEmpty(CurrentActorInfo.ActorName))
                return;
            string en = RomajiConverter.Convert(CurrentActorInfo.ActorName);
            if (string.IsNullOrEmpty(en) || !RomajiConverter.IsPureAscii(en) || en.Equals(CurrentActorInfo.ActorName)) {
                MessageNotify.Warning("无法自动转换（含汉字读音），请在编辑页手动填写英文名");
                return;
            }
            CurrentActorInfo.ActorNameEN = en;
            actorMapper.UpdateFieldById("ActorNameEN", en, CurrentActorInfo.ActorID);
            MessageNotify.Success($"英文名：{en}");
        }

        /// <summary>手动下载头像进行中标记（防重复点击并发下载）</summary>
        private bool _DownloadingAvatar;

        /// <summary>
        /// 手动从 Gfriends 头像库下载当前演员头像：按演员名（英文名兜底）取图并保存到本地头像目录。
        /// 与自动刮削「已有头像不覆盖」不同，显式点击视为刷新，成功后覆盖本地头像并即时上屏
        /// </summary>
        private void BeginDownLoadActress(object sender, MouseButtonEventArgs e)
        {
            if (CurrentActorInfo == null || string.IsNullOrEmpty(CurrentActorInfo.ActorName) || _DownloadingAvatar)
                return;
            if (!Gfriends.GfriendsAvatar.Enabled) {
                MessageNotify.Warning("未启用 Gfriends 头像库：请在「选项 → 同步信息」中勾选");
                return;
            }
            ActorInfo actorInfo = CurrentActorInfo;
            long actorID = actorInfo.ActorID;
            string actorName = actorInfo.ActorName;
            string actorNameEN = actorInfo.ActorNameEN;
            _DownloadingAvatar = true;
            MessageNotify.Info($"正在从 Gfriends 头像库获取头像：{actorName}");
            Task.Run(async () => {
                try {
                    byte[] bytes = await Gfriends.GfriendsAvatar.TryGetAvatarBytesAsync(actorName, actorNameEN);
                    if (bytes == null || bytes.Length == 0) {
                        Dispatcher.BeginInvoke(new Action(() => MessageNotify.Warning($"Gfriends 头像库未收录：{actorName}")));
                        return;
                    }
                    // GetImagePath：已有头像返回其现有文件名（原地覆盖），没有则落到约定命名
                    string targetPath = Path.GetFullPath(actorInfo.GetImagePath());
                    string dir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllBytes(targetPath, bytes);
                    ImageCache.Remove(targetPath);
                    App.Logger.Info($"手动下载演员头像（Gfriends）：{actorName} → {targetPath}");
                    Dispatcher.Invoke((Action)delegate () {
                        // 期间用户已切换演员则只落盘，不刷新当前展示
                        if (CurrentActorInfo != null && CurrentActorInfo.ActorID == actorID) {
                            string imagePath = CurrentActorInfo.GetImagePath();
                            BitmapImage image = ImageCache.Get(imagePath);
                            CurrentActorInfo.SmallImage = image ?? MetaData.DefaultActorImage;
                        }
                        MessageNotify.Success($"头像已保存：{actorName}");
                    });
                } catch (Exception ex) {
                    App.Logger.Error($"手动下载演员头像失败 {actorName}：{ex.Message}");
                    Dispatcher.BeginInvoke(new Action(() => MessageNotify.Error($"头像下载失败：{ex.Message}")));
                } finally {
                    _DownloadingAvatar = false;
                }
            });
        }
        private void EditActress(object sender, MouseButtonEventArgs e)
        {
            if (CurrentActorInfo != null) {
                Window_EditActor window_EditActor = new Window_EditActor(CurrentActorInfo.ActorID);
                window_EditActor.ShowDialog();
            }
        }

        /// <summary>
        /// 手动补齐当前演员资料（同演员列表右键「同步信息」）：插队入队串行执行，
        /// 进度见右下角任务页；完成后经 ActorScrapeTask.ActorDataChanged 联动刷新弹窗字段
        /// </summary>
        private void SyncActorInfo(object sender, MouseButtonEventArgs e)
        {
            if (CurrentActorInfo == null || CurrentActorInfo.ActorID <= 0 || string.IsNullOrEmpty(CurrentActorInfo.ActorName))
                return;
            int result = Jvedio.Core.Tasks.ActorScrapeManager.EnqueueManual(new List<ActorInfo>() { CurrentActorInfo });
            if (result < 0)
                MessageNotify.Warning("尚未启用演员资料源：请在「选项 → 同步信息」中启用 JAVDatabase(推荐)或 MinnanoAV 后重试");
            else if (result == 0)
                MessageNotify.Info("该演员已在补齐队列中");
            else
                MessageNotify.Success($"已加入演员资料补齐队列：{CurrentActorInfo.ActorName}");
        }

        private void ActorRate_ValueChanged(object sender, EventArgs e)
        {
            Rate rate = (Rate)sender;
            if (CurrentActorInfo != null)
                actorMapper.UpdateFieldById("Grade", rate.Value.ToString(), CurrentActorInfo.ActorID);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void HideActressGrid(object sender, RoutedEventArgs e)
        {
            Close?.Invoke();
        }
    }
}
