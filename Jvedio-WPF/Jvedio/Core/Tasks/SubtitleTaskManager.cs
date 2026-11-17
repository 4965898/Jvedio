using Newtonsoft.Json;
using SuperControls.Style;
using SuperUtils.Framework.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Jvedio.App;
using static Jvedio.MapperManager;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// 字幕下载任务的持久化记录（JSON，仅存恢复所需的字段）
    /// </summary>
    public class SubtitleTaskRecord
    {
        public long DataID { get; set; }

        public string Vid { get; set; }

        public int Status { get; set; }

        public string CreateTime { get; set; }
    }
    /// <summary>
    /// 字幕下载任务管理器：串行执行 + 间隔限速（公共站点礼貌），任务页可看进度/取消/重启。
    /// 重启语义：基类 AbstractTask.Restart 对非 Canceled 状态只重置为 WaitingToRun 且不重新执行
    /// （实测：Restart 后 Status=WaitingToRun、StatusText='Waiting...'、Progress 保留——任务死在队列外，
    /// 表现为「进度 100% + Waiting... + 详情按钮灰」）。因此本管理器 new-shadow 重启实现：
    /// 删旧任务 + 新建同影片任务重新入队，任何状态都可正确重跑
    /// </summary>
    public class SubtitleTaskManager : BaseManager
    {
        /// <summary>每个任务之间的间隔 (ms)：搜索→详情→下载共 3+ 次请求，间隔放任务之间足够</summary>
        private const int TASK_DELAY = 1500;

        /// <summary>同时进行的任务数：SubtitleCat 是小型公共站点，固定串行</summary>
        private const int TASK_COUNT = 1;

        private static TaskConfig DEFAULT_CONFIG { get; set; } = new TaskConfig() {
            TaskDelay = TASK_DELAY,
            TaskCount = TASK_COUNT,
        };

        protected SubtitleTaskManager() { }

        public new static SubtitleTaskManager Instance { get; set; }

        public new static SubtitleTaskManager CreateInstance()
        {
            if (Instance == null)
                Instance = new SubtitleTaskManager();
            return Instance;
        }

        private static ReliableTaskDispatcher<SubtitleTask> Dispatcher { get; set; }

        static SubtitleTaskManager()
        {
            Dispatcher = ReliableTaskDispatcher<SubtitleTask>.CreateInstance(DEFAULT_CONFIG);
            Dispatcher.onWorking += (s, e) => {
                App.Current.Dispatcher.Invoke(() => {
                    Instance.onRunning?.Invoke();
                    Instance.Progress = (int)Dispatcher.Progress;
                });
            };
            Dispatcher.onComplete += (s, e) => {
                Instance.Progress = 100;
            };
        }

        public void Start()
        {
            Dispatcher.BeginWork();
        }

        public override void AddToDispatcher(AbstractTask task)
        {
            Dispatcher.Enqueue(task as SubtitleTask);
        }

        public override void AddToDispatcherFront(AbstractTask task)
        {
            Dispatcher.EnqueueFront(task as SubtitleTask);
        }

        public override void ClearDispatcher()
        {
            Dispatcher.ClearDoneList();
        }

        /// <summary>
        /// 语言偏好解析（设置里逗号分隔的语言码；空/非法回退简繁）。
        /// 统一规范化为站点轨道命名：zh-cn→zh-CN、zh-tw→zh-TW，其余小写——
        /// 规范码用于文件名（番号.zh-CN.srt）与日志显示，查找另有忽略大小写兜底
        /// </summary>
        public static string[] ParseLanguages()
        {
            string raw = ConfigManager.Settings?.SubtitleLanguages;
            if (string.IsNullOrWhiteSpace(raw))
                return new string[] { "zh-CN", "zh-TW" };
            string[] langs = raw.Split(new char[] { ',', ';', '，', '、', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant())
                .Where(s => Regex.IsMatch(s, "^[a-z]{2,3}(-[a-z0-9]{2,8})?$"))
                .Select(s => s == "zh-cn" ? "zh-CN" : s == "zh-tw" ? "zh-TW" : s)
                .Distinct()
                .ToArray();
            return langs.Length > 0 ? langs : new string[] { "zh-CN", "zh-TW" };
        }

        /// <summary>新建字幕任务（供入队与重启共用：同一部影片的任务参数一致）</summary>
        private static SubtitleTask CreateTask(Jvedio.Entity.Video video)
        {
            if (video == null || video.DataID <= 0 || string.IsNullOrWhiteSpace(video.VID))
                return null;
            int saveMode = ConfigManager.Settings?.SubtitleSaveMode ?? 0;
            // 模式 0 需要影片文件路径；无路径的纯库记录在模式 0 下无意义
            if (saveMode == 0 && string.IsNullOrWhiteSpace(video.Path))
                return null;
            return new SubtitleTask(video.DataID, video.VID, ParseLanguages(), saveMode,
                ConfigManager.Settings?.SubtitleSaveFolder, video.Path);
        }

        /// <summary>
        /// 手动下载（影片右键）：单选=当前影片，多选=勾选的全部。已有字幕跳过（不覆盖）。
        /// 返回入队数
        /// </summary>
        public static int EnqueueManual(IEnumerable<Jvedio.Entity.Video> videos)
        {
            if (Instance == null || videos == null)
                return 0;
            int added = 0;
            foreach (Jvedio.Entity.Video video in videos) {
                SubtitleTask task = CreateTask(video);
                if (task == null)
                    continue;
                // 清掉同影片的终态旧任务再入队，避免列表堆积（与演员任务同款）
                if (Instance.CurrentTasks != null) {
                    for (int i = Instance.CurrentTasks.Count - 1; i >= 0; i--) {
                        AbstractTask existing = Instance.CurrentTasks[i];
                        if (existing is SubtitleTask old && old.DataID == video.DataID &&
                            (old.Status == TaskStatus.RanToCompletion || old.Status == TaskStatus.Canceled ||
                             old.Status == TaskStatus.Faulted)) {
                            Instance.CurrentTasks.RemoveAt(i);
                        }
                    }
                    if (Instance.CurrentTasks.Contains(task))
                        continue; // 排队/进行中，不重复
                }
                Instance.AddTaskFront(task);
                added++;
            }
            if (added > 0) {
                Logger.Info($"字幕下载：插队入队 {added} 部（串行 + 间隔限速）");
                Instance.Start();
            }
            return added;
        }

        /// <summary>
        /// 刮削完成后自动下载单部影片的字幕（设置开启时）。
        /// 独立小队列入队，不阻塞刮削任务本身
        /// </summary>
        public static void EnqueueAuto(Jvedio.Entity.Video video)
        {
            if (Instance == null || video == null ||
                ConfigManager.Settings?.SubtitleAutoDownload != true)
                return;
            SubtitleTask task = CreateTask(video);
            if (task == null || Instance.Exists(task))
                return;
            Instance.AddTask(task);
            Instance.Start();
        }

        /// <summary>
        /// 重启单个任务：删除旧条目（任意状态）并新建同影片任务重新入队。
        /// 遮蔽 BaseManager.Restart —— 后者直接 task.Restart()，而基类 Restart 对非 Canceled
        /// 状态只重置不重跑（详见类注释），必须换新任务
        /// </summary>
        public new void Restart(string id)
        {
            if (string.IsNullOrEmpty(id) || Instance == null)
                return;
            SubtitleTask old = Instance.CurrentTasks?.FirstOrDefault(arg => arg.ID.Equals(id)) as SubtitleTask;
            if (old == null)
                return;
            Jvedio.Entity.Video video = videoMapper.SelectVideoByID(old.DataID);
            SubtitleTask fresh = video == null ? null : CreateTask(video);
            // 从列表移除旧任务（新任务 Equals 同 DataID，不移除会因 Contains 被跳过）
            Instance.CurrentTasks.Remove(old);
            if (fresh == null) {
                MessageNotify.Warning(SuperControls.Style.LangManager.GetValueByKey("SubtitleRestartNoPath"));
                return;
            }
            Instance.AddTaskFront(fresh);
            Instance.Start();
        }

        /// <summary>
        /// 重启全部失败（取消）的任务：同样走「删旧建新」语义，可被「取消所有」中止
        /// </summary>
        public void RestartAllFailed()
        {
            if (RestartAllRunning)
                return;
            RestartAllRunning = true;
            RestartAllAborted = false;
            try {
                List<SubtitleTask> failed = CurrentTasks
                    .Where(t => t is SubtitleTask && t.Status == TaskStatus.Canceled)
                    .Cast<SubtitleTask>().ToList();
                if (failed.Count == 0)
                    return;
                foreach (SubtitleTask old in failed) {
                    if (RestartAllAborted)
                        return;
                    if (!CurrentTasks.Contains(old) || old.Status != TaskStatus.Canceled)
                        continue;
                    Jvedio.Entity.Video video = videoMapper.SelectVideoByID(old.DataID);
                    SubtitleTask fresh = video == null ? null : CreateTask(video);
                    CurrentTasks.Remove(old);
                    if (fresh != null)
                        AddTaskFront(fresh);
                }
                if (!RestartAllAborted)
                    Start();
            } finally {
                RestartAllRunning = false;
            }
        }
        #region "任务持久化（崩溃/退出后恢复未完成的字幕下载任务，与刮削/演员任务同模式）"

        /// <summary>是否为退出流程（退出时仍保存快照，但取消操作不删除快照）</summary>
        public bool Exiting { get; set; }

        private readonly object _PersistLock = new object();

        private readonly Dictionary<long, SubtitleTaskRecord> _PendingRecords = new Dictionary<long, SubtitleTaskRecord>();

        private static string PersistPath => Path.Combine(Jvedio.Core.Global.PathManager.CurrentUserFolder, "subtitle_tasks.json");

        public new void AddTask(AbstractTask task)
        {
            if (CurrentTasks.Contains(task))
                return;

            task.onCompleted += OnTaskPersistCompleted;
            if (task is SubtitleTask subtitleTask) {
                lock (_PersistLock) {
                    _PendingRecords[subtitleTask.DataID] = ToRecord(subtitleTask);
                }
            }
            SaveTasksToFile();
            try {
                base.AddTask(task);
            } catch {
                task.onCompleted -= OnTaskPersistCompleted;
                if (task is SubtitleTask failedTask) {
                    lock (_PersistLock) {
                        _PendingRecords.Remove(failedTask.DataID);
                    }
                    SaveTasksToFile();
                }
                throw;
            }
        }

        public new void AddTaskFront(AbstractTask task)
        {
            if (CurrentTasks.Contains(task))
                return;

            task.onCompleted += OnTaskPersistCompleted;
            if (task is SubtitleTask subtitleTask) {
                lock (_PersistLock) {
                    _PendingRecords[subtitleTask.DataID] = ToRecord(subtitleTask);
                }
            }
            SaveTasksToFile();
            try {
                base.AddTaskFront(task);
            } catch {
                task.onCompleted -= OnTaskPersistCompleted;
                if (task is SubtitleTask failedTask) {
                    lock (_PersistLock) {
                        _PendingRecords.Remove(failedTask.DataID);
                    }
                    SaveTasksToFile();
                }
                throw;
            }
        }

        private void OnTaskPersistCompleted(object sender, EventArgs e)
        {
            if (sender is SubtitleTask subtitleTask) {
                lock (_PersistLock) {
                    _PendingRecords.Remove(subtitleTask.DataID);
                }
                SaveTasksToFile();
            }
        }

        public new void RemoveTask(System.Threading.Tasks.TaskStatus status)
        {
            List<AbstractTask> removed = new List<AbstractTask>();
            if (status == (System.Threading.Tasks.TaskStatus.Canceled | System.Threading.Tasks.TaskStatus.RanToCompletion)) {
                removed.AddRange(CurrentTasks);
            } else {
                removed.AddRange(CurrentTasks.Where(t => t.Status == status));
            }
            base.RemoveTask(status);
            lock (_PersistLock) {
                foreach (AbstractTask task in removed) {
                    if (task is SubtitleTask subtitleTask)
                        _PendingRecords.Remove(subtitleTask.DataID);
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
            if (task is SubtitleTask subtitleTask) {
                lock (_PersistLock) {
                    _PendingRecords.Remove(subtitleTask.DataID);
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

        private static SubtitleTaskRecord ToRecord(SubtitleTask task)
        {
            return new SubtitleTaskRecord {
                DataID = task.DataID,
                Vid = task.Title,
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
                string dir = Jvedio.Core.Global.PathManager.CurrentUserFolder;
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
        /// 启动时从本地文件恢复未完成的字幕下载任务。
        /// 按当前设置重建任务（语言/保存模式取当下配置），只进入任务列表（「上次未完成，可重启」），
        /// 由用户点「重启全部失败」或单任务重启后继续；影片已删除/无番号的快照直接丢弃。
        /// </summary>
        /// <returns>恢复的任务数量</returns>
        public int RestoreTasksFromFile()
        {
            List<SubtitleTaskRecord> records = new List<SubtitleTaskRecord>();
            try {
                if (File.Exists(PersistPath)) {
                    string json = File.ReadAllText(PersistPath);
                    records = JsonConvert.DeserializeObject<List<SubtitleTaskRecord>>(json) ?? new List<SubtitleTaskRecord>();
                }
            } catch (Exception ex) {
                Logger.Error(ex);
                records = new List<SubtitleTaskRecord>();
            }

            int restored = 0;
            foreach (SubtitleTaskRecord record in records) {
                if (record == null || record.DataID <= 0)
                    continue;
                try {
                    if (CurrentTasks.Any(t => t is SubtitleTask st && st.DataID == record.DataID))
                        continue;
                    // 影片已删除（或按当前设置不可下载：无番号/模式 0 无路径）则丢弃快照，避免僵尸任务
                    Jvedio.Entity.Video video = videoMapper.SelectVideoByID(record.DataID);
                    SubtitleTask task = video == null ? null : CreateTask(video);
                    if (task == null) {
                        Logger.Info($"video {record.DataID} no longer subtitle-eligible, drop snapshot");
                        continue;
                    }
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
                Logger.Info($"restore {restored} subtitle tasks (not started, waiting for user)");
                SaveTasksToFile();
            }
            return restored;
        }

        #endregion

    }
}
