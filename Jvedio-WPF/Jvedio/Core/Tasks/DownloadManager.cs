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
            bool dispatcherIdle = !Dispatcher.Working;
            Dispatcher.Enqueue(task as DownLoadTask);
            Dispatcher.BeginWork();
            // BeginWork 与调度器「队列空即退出」的判断存在竞态：若在旧循环判定空队列与真正退出之间入队，
            // BeginWork 会因 Working 仍为 true 直接返回，随后旧循环退出，新任务永远停在 WaitingToRun。
            // 调度器空闲时入队后补一个迟到的兜底检查，确保新任务一定被调度器接管。
            if (dispatcherIdle)
                WatchForStuckStart(task);
        }

        /// <summary>
        /// 兜底：入队后若任务迟迟未被调度器开始（调度器工作循环可能因异常死亡、或与退出判断竞态被落下），
        /// 复位 Working 并重新拉起一个工作循环接管等待队列。
        /// </summary>
        private void WatchForStuckStart(AbstractTask task)
        {
            Task.Run(async () => {
                try {
                    await Task.Delay(4000);
                    if (task.Status != System.Threading.Tasks.TaskStatus.WaitingToRun)
                        return;
                    if (Dispatcher.Working)
                        Dispatcher.Working = false;
                    Dispatcher.BeginWork();
                } catch (Exception ex) {
                    Logger.Error(ex);
                }
            });
        }

        public override void ClearDispatcher()
        {
            Dispatcher.ClearDoneList();
        }

        /// <summary>
        /// 重启全部失败（取消）的任务，分批重启并等待每批完成，避免并发刮削超限。
        /// 用户在重启过程中点击「取消所有」/「清除列表」时会立即中止，不再自动重启后续批次。
        /// </summary>
        public async void RestartAllFailed()
        {
            if (RestartAllRunning)
                return;
            RestartAllRunning = true;
            RestartAllAborted = false;
            try {
                var failed = CurrentTasks.Where(t => t.Status == System.Threading.Tasks.TaskStatus.Canceled).ToList();
                if (failed.Count == 0)
                    return;
                int index = 0;
                while (index < failed.Count) {
                    // 用户已取消/清空列表 → 终止自动重启链
                    if (RestartAllAborted)
                        return;
                    int batch = Math.Min(failed.Count - index, TASK_COUNT);
                    List<AbstractTask> toRestart = new List<AbstractTask>();
                    for (int i = 0; i < batch; i++) {
                        AbstractTask t = failed[index + i];
                        // 仅重启仍在任务列表中的任务（清空列表后旧任务不再拉起），且仍处于失败（取消）状态
                        if (CurrentTasks.Contains(t) && t.Status == System.Threading.Tasks.TaskStatus.Canceled)
                            toRestart.Add(t);
                    }
                    if (toRestart.Count == 0) {
                        index += batch;
                        continue;
                    }
                    foreach (AbstractTask t in toRestart)
                        t.Restart();
                    bool completed = false;
                    while (!completed) {
                        // 重启过程中用户点了取消/清空列表：停掉本批次已重启的任务，不再重启后续批次
                        if (RestartAllAborted) {
                            foreach (AbstractTask t in toRestart)
                                t.Cancel();
                            return;
                        }
                        completed = true;
                        for (int i = 0; i < toRestart.Count; i++) {
                            var t = toRestart[i];
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
                if (!RestartAllAborted)
                    Start();
            } finally {
                RestartAllRunning = false;
            }
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
