using Jvedio.Core.Global;
using Jvedio.Core.Net;
using Jvedio.Entity;
using Newtonsoft.Json;
using SuperControls.Style;
using SuperUtils.Framework.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Jvedio.App;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// 刮削任务的持久化记录（JSON，仅存恢复所需的字段）
    /// </summary>
    public class DownLoadTaskRecord
    {
        public long DataID { get; set; }

        public string DataType { get; set; }

        public string Title { get; set; }

        public bool DownloadPreview { get; set; }

        public bool OverrideInfo { get; set; }

        public int Status { get; set; }

        public string CreateTime { get; set; }
    }

    public class DownloadManager : BaseManager
    {
        /// <summary>
        /// 每个任务的间隔 (ms)
        /// </summary>
        private const int TASK_DELAY = 3000;

        /// <summary>
        /// 任务的数目到达 LongTaskCount 时暂停的间隔 (ms)
        /// </summary>
        private const int LONG_TASK_DELAY = 10 * 1000;

        /// <summary>
        /// 是否开启长暂停
        /// </summary>
        private const bool ENABLE_LONG_TASK_DELAY = true;

        /// <summary>
        /// 可同时进行任务的数目
        /// </summary>
        private const int TASK_COUNT = 2;

        /// <summary>
        /// 进行长暂停的上限
        /// </summary>
        private const int LONG_TASK_COUNT = 5;


        /// <summary>
        /// 默认的任务配置
        /// </summary>
        private static TaskConfig DEFAULT_CONFIG { get; set; } = new TaskConfig() {
            TaskDelay = TASK_DELAY,
            TaskCount = TASK_COUNT,
            LongTaskCount = LONG_TASK_COUNT,
            LongTaskDelay = LONG_TASK_DELAY,
            EnableLongTaskDelay = ENABLE_LONG_TASK_DELAY,
        };



        #region "事件"


        public event EventHandler onLongDelay;

        #endregion


        private static TaskDispatcher<DownLoadTask> Dispatcher { get; set; }

        static DownloadManager()
        {
            Dispatcher = TaskDispatcher<DownLoadTask>.CreateInstance(DEFAULT_CONFIG);
            Dispatcher.onWorking += (s, e) => {
                App.Current.Dispatcher.Invoke(() => {
                    Instance.onRunning?.Invoke();
                    Instance.Progress = (int)Dispatcher.Progress;
                });
            };
            Dispatcher.onLongDelay += (s, e) => {
                Instance.onLongDelay?.Invoke(s, e);
            };
            Dispatcher.onComplete += (s, e) => {
                Instance.Progress = 100;
            };
            //start();
        }

        public void Start()
        {
            Dispatcher.BeginWork();
        }

        private DownloadManager() { }

        public new static DownloadManager Instance { get; set; }

        public new static DownloadManager CreateInstance()
        {
            if (Instance == null)
                Instance = new DownloadManager();
            return Instance;
        }


        public override void AddToDispatcher(AbstractTask task)
        {
            Dispatcher.Enqueue(task as DownLoadTask);
            Dispatcher.BeginWork();
        }

        public override void ClearDispatcher()
        {
            Dispatcher.ClearDoneList();
        }

        public async void RestartAllFailed()
        {
            var failed = CurrentTasks.Where(t => t.Status == System.Threading.Tasks.TaskStatus.Canceled).ToList();
            if (failed.Count == 0)
                return;
            int index = 0;
            while (index < failed.Count) {
                int batch = Math.Min(failed.Count - index, TASK_COUNT);
                for (int i = 0; i < batch; i++) {
                    failed[index + i].Restart();
                }
                bool completed = false;
                while (!completed) {
                    completed = true;
                    for (int i = 0; i < batch; i++) {
                        var t = failed[index + i];
                        if (t.Status == System.Threading.Tasks.TaskStatus.Running ||
                            t.Status == System.Threading.Tasks.TaskStatus.WaitingToRun) {
                            completed = false;
                            break;
                        }
                    }
                    if (!completed)
                        await Task.Delay(TASK_DELAY);
                }
                index += batch;
            }
            Start();
        }

        #region "任务持久化（崩溃/退出后恢复未完成的刮削任务）"

        /// <summary>
        /// 是否为退出流程（退出时仍保存快照，但取消操作不删除快照）
        /// </summary>
        public bool Exiting { get; set; }

        private readonly object _PersistLock = new object();

        private readonly Dictionary<long, DownLoadTaskRecord> _PendingRecords = new Dictionary<long, DownLoadTaskRecord>();

        private static string PersistPath => Path.Combine(PathManager.CurrentUserFolder, "download_tasks.json");

        public new void AddTask(AbstractTask task)
        {
            base.AddTask(task);
            task.onCompleted += OnTaskPersistCompleted;
            if (task is DownLoadTask downloadTask) {
                lock (_PersistLock) {
                    _PendingRecords[downloadTask.DataID] = ToRecord(downloadTask);
                }
            }
            SaveTasksToFile();
        }

        private void OnTaskPersistCompleted(object sender, EventArgs e)
        {
            if (sender is DownLoadTask downloadTask) {
                lock (_PersistLock) {
                    _PendingRecords.Remove(downloadTask.DataID);
                }
                SaveTasksToFile();
            }
        }

        public new void RemoveTask(System.Threading.Tasks.TaskStatus status)
        {
            List<AbstractTask> removed = new List<AbstractTask>();
            if (status == (TaskStatus.Canceled | TaskStatus.RanToCompletion)) {
                removed.AddRange(CurrentTasks);
            } else {
                removed.AddRange(CurrentTasks.Where(t => t.Status == status));
            }
            base.RemoveTask(status);
            lock (_PersistLock) {
                foreach (AbstractTask task in removed) {
                    if (task is DownLoadTask downloadTask)
                        _PendingRecords.Remove(downloadTask.DataID);
                }
            }
            SaveTasksToFile();
        }

        public new void CancelTask(string id)
        {
            base.CancelTask(id);
            if (Exiting)
                return;
            AbstractTask task = CurrentTasks.FirstOrDefault(arg => arg.ID.Equals(id));
            if (task is DownLoadTask downloadTask) {
                lock (_PersistLock) {
                    _PendingRecords.Remove(downloadTask.DataID);
                }
                SaveTasksToFile();
            }
        }

        public new void CancelAll()
        {
            base.CancelAll();
            if (Exiting)
                return;
            lock (_PersistLock) {
                _PendingRecords.Clear();
            }
            SaveTasksToFile();
        }

        private static DownLoadTaskRecord ToRecord(DownLoadTask task)
        {
            return new DownLoadTaskRecord {
                DataID = task.DataID,
                DataType = task.DataType.ToString(),
                Title = task.Title,
                DownloadPreview = task.DownloadPreview,
                OverrideInfo = task.OverrideInfo,
                Status = (int)task.Status,
                CreateTime = task.CreateTime,
            };
        }

        /// <summary>
        /// 把未完成任务快照写入本地文件（原子替换）。任何时机调用都安全，异常仅记日志。
        /// </summary>
        public void SaveTasksToFile()
        {
            try {
                string json;
                lock (_PersistLock) {
                    json = JsonConvert.SerializeObject(_PendingRecords.Values.ToList());
                }
                string dir = PathManager.CurrentUserFolder;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                string tmp = PersistPath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(PersistPath))
                    File.Delete(PersistPath);
                File.Move(tmp, PersistPath);
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        /// <summary>
        /// 启动时从本地文件恢复未完成的刮削任务。
        /// 所有恢复的任务只进入任务列表（状态显示为「上次未完成，可重启」），
        /// 是否继续刮削由用户决定：点「重启全部失败」或单个任务「重启」后才会开始。
        /// </summary>
        /// <returns>恢复的任务数量</returns>
        public int RestoreTasksFromFile()
        {
            List<DownLoadTaskRecord> records = new List<DownLoadTaskRecord>();
            try {
                if (File.Exists(PersistPath)) {
                    string json = File.ReadAllText(PersistPath);
                    records = JsonConvert.DeserializeObject<List<DownLoadTaskRecord>>(json) ?? new List<DownLoadTaskRecord>();
                }
            } catch (Exception ex) {
                Logger.Error(ex);
                records = new List<DownLoadTaskRecord>();
            }

            int restored = 0;
            foreach (DownLoadTaskRecord record in records) {
                if (record == null || record.DataID <= 0)
                    continue;
                try {
                    if (CurrentTasks.Any(t => t is DownLoadTask dt && dt.DataID == record.DataID))
                        continue;

                    MetaData metaData = new MetaData();
                    metaData.DataID = record.DataID;
                    Enum.TryParse(record.DataType, out Jvedio.Core.Enums.DataType dataType);
                    metaData.DataType = dataType;

                    DownLoadTask task = new DownLoadTask(metaData) {
                        Title = record.Title,
                        DownloadPreview = record.DownloadPreview,
                        OverrideInfo = record.OverrideInfo,
                        CreateTime = record.CreateTime,
                    };
                    task.onCompleted += OnTaskPersistCompleted;

                    // 只恢复到列表（保持「未完成」状态），不自动开始；用户点「重启全部失败」或单任务重启后继续
                    task.Status = TaskStatus.Canceled;
                    task.StatusText = LangManager.GetValueByKey("TaskInterrupted");
                    CurrentTasks.Add(task);
                    lock (_PersistLock) {
                        _PendingRecords[record.DataID] = record;
                    }
                    restored++;
                } catch (Exception ex) {
                    Logger.Error(ex);
                }
            }
            if (restored > 0) {
                Logger.Info($"restore {restored} download tasks (not started, waiting for user)");
                SaveTasksToFile();
            }
            return restored;
        }

        #endregion
    }
}
