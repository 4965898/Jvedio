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
