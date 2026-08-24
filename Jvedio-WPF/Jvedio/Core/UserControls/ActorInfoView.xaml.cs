using Jvedio.Core.Crawler;
using Jvedio.Core.Utils;
using Jvedio.Entity;
using SuperControls.Style;
using SuperUtils.IO;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
                string url = site.GetSearchUrl(encodedName);
                Button button = new Button() {
                    Content = site.Name,
                    Style = (Style)FindResource("OnlineJumpButton"),
                    ToolTip = url,
                };
                button.Click += (s, e) => FileHelper.TryOpenUrl(url);
                actorOnlineJumpPanel.Children.Add(button);
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

        // todo 演员信息下载
        private void BeginDownLoadActress(object sender, MouseButtonEventArgs e)
        {
            MessageNotify.Info("开发中");
            // List<Actress> actresses = new List<Actress>();
            // actresses.Add(vieModel.Actress);
            // DownLoadActress downLoadActress = new DownLoadActress(actresses);
            // downLoadActress.BeginDownLoad();
            // downLoadActress.InfoUpdate += (s, ev) =>
            // {
            //    ActressUpdateEventArgs actressUpdateEventArgs = ev as ActressUpdateEventArgs;
            //    try
            //    {
            //        Dispatcher.Invoke((Action)delegate ()
            //        {
            //            vieModel.Actress = null;
            //            vieModel.Actress = actressUpdateEventArgs.Actress;
            //            downLoadActress.State = DownLoadState.Completed;
            //        });
            //    }
            //    catch (TaskCanceledException ex) { Logger.LogE(ex); }

            // };

            // downLoadActress.MessageCallBack += (s, ev) =>
            // {
            //    MessageCallBackEventArgs actressUpdateEventArgs = ev as MessageCallBackEventArgs;
            //    msgCard.Info(actressUpdateEventArgs.Message);

            // };
        }
        private void EditActress(object sender, MouseButtonEventArgs e)
        {
            if (CurrentActorInfo != null) {
                Window_EditActor window_EditActor = new Window_EditActor(CurrentActorInfo.ActorID);
                window_EditActor.ShowDialog();
            }
        }

        private void LoadActorOtherMovie(object sender, MouseButtonEventArgs e)
        {
            MessageNotify.Info("开发中");
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
