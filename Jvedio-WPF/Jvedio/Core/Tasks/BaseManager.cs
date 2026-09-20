using SuperUtils.Framework.Tasks;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Jvedio.Core.Tasks
{
    public class BaseManager : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        protected BaseManager() { }

        public static BaseManager Instance { get; set; }

        public static BaseManager CreateInstance()
        {
            if (Instance == null)
                Instance = new BaseManager();
            return Instance;
        }



        #region "事件"
        public Action onRunning;
        #endregion

        #region "重启全部失败：取消/清空列表时中止自动重启链"

        /// <summary>
        /// 「重启全部失败」进行中：用户点击取消/清空列表后置位，重启循环据此中止后续批次的自动重启，
        /// 避免已点取消的任务又被重启循环重新拉起（旧逻辑的「取消所有有时失效」根因）。
        /// </summary>
        protected volatile bool RestartAllAborted = false;

        /// <summary>
        /// 「重启全部失败」是否正在进行（防重复点击造成两条重启循环并发拉起同一批任务）。
        /// </summary>
        protected bool RestartAllRunning = false;

        #endregion


        #region "属性"

        private volatile int _Progress;

        public int Progress {
            get { return _Progress; }

            set {
                _Progress = value;
                RaisePropertyChanged();
            }
        }



        private bool? _Running = null;

        public bool? Running {
            get { return _Running; }

            set {
                _Running = value;
                RaisePropertyChanged();
            }
        }




        private volatile int _RunningCount = 0;

        public int RunningCount {
            get { return _RunningCount; }

            set {
                _RunningCount = value;
                RaisePropertyChanged();
                Running = value > 0;
                if (RunningCount == 0 && Progress > 0)
                    Progress = 100;

            }
        }


        private ObservableCollection<AbstractTask> _CurrentTasks = new ObservableCollection<AbstractTask>();

        public ObservableCollection<AbstractTask> CurrentTasks {
            get { return _CurrentTasks; }

            set {
                _CurrentTasks = value;
                RaisePropertyChanged();
            }
        }
        #endregion

        public void AddTask(AbstractTask task)
        {
            if (!CurrentTasks.Contains(task)) {

                AddToDispatcher(task);

                task.onCanceled += DecreaseRunCount;
                task.onCompleted += DecreaseRunCount;
                RunningCount++;

                CurrentTasks.Add(task);
            }
        }

        public bool Exists(AbstractTask task)
        {
            return CurrentTasks.Contains(task);
        }

        public virtual void AddToDispatcher(AbstractTask task)
        {
            throw new NotImplementedException();
        }

        public void DecreaseRunCount(object sender, EventArgs ev)
        {
            if (RunningCount > 0)
                RunningCount--;
        }

        public void RemoveTask(System.Threading.Tasks.TaskStatus status)
        {
            // 清空/移除任务即中止「重启全部失败」：不再自动拉起已被移出列表的旧任务
            RestartAllAborted = true;
            if (status == (TaskStatus.Canceled | TaskStatus.RanToCompletion)) {
                CurrentTasks.Clear();
            } else {
                for (int i = CurrentTasks.Count - 1; i >= 0; i--) {
                    if (CurrentTasks[i].Status == status) {
                        CurrentTasks.RemoveAt(i);
                    }
                }

            }


            ClearDispatcher();
        }

        public virtual void ClearDispatcher()
        {
            throw new NotImplementedException();
        }

        public void CancelAll()
        {
            // 中止进行中的「重启全部失败」自动重启链，随后取消当前所有任务
            RestartAllAborted = true;
            if (CurrentTasks.Count > 0) {
                foreach (AbstractTask task in CurrentTasks) {
                    task.Cancel();
                }
            }
        }

        public void CancelTask(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            AbstractTask task = CurrentTasks.FirstOrDefault(arg => arg.ID.Equals(id));
            task?.Cancel();
        }

        public void Restart(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            AbstractTask task = CurrentTasks.FirstOrDefault(arg => arg.ID.Equals(id));
            task?.Restart();
        }

        public string GetTaskLogs(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "";
            AbstractTask task = CurrentTasks.FirstOrDefault(arg => arg.ID.Equals(id));
            if (task == null)
                return "";
            return string.Join(Environment.NewLine, task.Logs);
        }
    }
}
