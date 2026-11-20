using Jvedio.Core.Plugins.Crawler;
using Jvedio.Entity;
using Newtonsoft.Json;
using SuperUtils.Framework.ORM.Wrapper;
using SuperUtils.Framework.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Jvedio.App;
using static Jvedio.MapperManager;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// 演员资料补齐任务的持久化记录（JSON，仅存恢复所需的字段）
    /// </summary>
    public class ActorScrapeTaskRecord
    {
        public long ActorID { get; set; }

        public string ActorName { get; set; }

        public int Status { get; set; }

        public string CreateTime { get; set; }
    }

    /// <summary>
    /// 演员资料补齐任务管理器：默认串行（防限流），并发数由设置「演员资料补齐并发数」控制（1~5），
    /// 每 10 个长歇 15 秒防风控。参照 TranslateTaskManager 的队列模式；任务按 ActorID 去重。
    /// </summary>
    public class ActorScrapeManager : BaseManager
    {
        /// <summary>每个任务之间的间隔 (ms)</summary>
        private const int TASK_DELAY = 3000;

        /// <summary>
        /// 每次启动批量自动补齐的上限：大库全量入队会让队列膨胀到上万，
        /// 渐进补齐（剩余批次随下次启动继续），手动「同步信息」插队不受影响
        /// </summary>
        private const int AUTO_BATCH_LIMIT = 200;

        /// <summary>同时进行的任务数默认值：演员资料站点对并发敏感，实际值由设置 ActorScrapeConcurrency 应用</summary>
        private const int TASK_COUNT = 1;

        /// <summary>每完成 LONG_TASK_COUNT 个任务长歇一次</summary>
        private const int LONG_TASK_DELAY = 15 * 1000;
        private const bool ENABLE_LONG_TASK_DELAY = true;
        private const int LONG_TASK_COUNT = 10;

        private static TaskConfig DEFAULT_CONFIG { get; set; } = new TaskConfig() {
            TaskDelay = TASK_DELAY,
            TaskCount = TASK_COUNT,
            LongTaskCount = LONG_TASK_COUNT,
            LongTaskDelay = LONG_TASK_DELAY,
            EnableLongTaskDelay = ENABLE_LONG_TASK_DELAY,
        };

        protected ActorScrapeManager() { }

        public new static ActorScrapeManager Instance { get; set; }

        public new static ActorScrapeManager CreateInstance()
        {
            if (Instance == null)
                Instance = new ActorScrapeManager();
            return Instance;
        }

        private static ReliableTaskDispatcher<ActorScrapeTask> Dispatcher { get; set; }

        static ActorScrapeManager()
        {
            Dispatcher = ReliableTaskDispatcher<ActorScrapeTask>.CreateInstance(DEFAULT_CONFIG);
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
            // 每次启动重读设置：设置页改并发数后下一次批量/手动补齐立即生效
            try {
                ApplyConcurrency((int)ConfigManager.Settings.ActorScrapeConcurrency);
            } catch (Exception ex) {
                Logger.Error(ex);
            }
            Dispatcher.BeginWork();
        }

        /// <summary>
        /// 应用「演员资料补齐并发数」设置（1~5，立即生效）：默认串行 1 防风控，
        /// 并发越高越容易触发 JAVDatabase 的 Wordfence 404 限速——
        /// 插件已有连续 404 止损与拦截页检测兜底，但限频期的失败仍不可避免
        /// </summary>
        public static void ApplyConcurrency(int concurrency)
        {
            int value = Math.Max(1, Math.Min(5, concurrency));
            try {
                Dispatcher?.UpdateTaskCount(value);
                App.Logger.Info($"actor scrape concurrency applied: {value}");
            } catch (Exception ex) {
                App.Logger.Error(ex);
            }
        }

        public override void AddToDispatcher(AbstractTask task)
        {
            Dispatcher.Enqueue(task as ActorScrapeTask);
        }

        public override void AddToDispatcherFront(AbstractTask task)
        {
            Dispatcher.EnqueueFront(task as ActorScrapeTask);
        }

        public override void ClearDispatcher()
        {
            Dispatcher.ClearDoneList();
        }

        /// <summary>
        /// 把资料为空的演员全部入队（启动后台维护时调用）。
        /// 前提：设置开启 ActorAutoEnrich 且 MinnanoAV 源已启用，否则不产生任何请求。
        /// </summary>
        public int EnqueueAllIncomplete()
        {
            if (!ConfigManager.Settings.ActorAutoEnrich)
                return 0;
            if (!HasEnabledActorSource())
                return 0;
            // 一次性清理 2026-11-12 版误标：当天的网络失败（Cloudflare 拦截）也被记成了
            // 「查无此人」，会永久阻止自动补齐；该日期的标记一律作废，让演员重新可补
            try {
                List<Dictionary<string, object>> stale = actorMapper.Select(
                    "select ActorID from actor_info where ExtraInfo like '%minnanoav:notfound@2026-11-12%'");
                if (stale != null && stale.Count > 0) {
                    actorMapper.ExecuteNonQuery(
                        "update actor_info set ExtraInfo = replace(ExtraInfo, ';minnanoav:notfound@2026-11-12', '') " +
                        "where ExtraInfo like '%minnanoav:notfound@2026-11-12%'");
                    actorMapper.ExecuteNonQuery(
                        "update actor_info set ExtraInfo = replace(ExtraInfo, 'minnanoav:notfound@2026-11-12', '') " +
                        "where ExtraInfo like '%minnanoav:notfound@2026-11-12%'");
                    Logger.Info($"演员资料补齐：清理误标记 {stale.Count} 条（2026-11-12 网络失败被误记为查无此人，已作废可重试）");
                }
            } catch (Exception ex) {
                Logger.Error(ex);
            }
            // 一次性清理历史垃圾英文名：JavDB 部分演员页 URL 是随机 ID（含数字且无空格，
            // 如 QE4G/7e8V），曾被当作罗马字写入 ActorNameEN——这类一律清空，可重新补齐
            try {
                actorMapper.ExecuteNonQuery(
                    "update actor_info set ActorNameEN = '' where ActorNameEN <> '' " +
                    "and ActorNameEN GLOB '*[0-9]*' and ActorNameEN not GLOB '* *'");
            } catch (Exception ex) {
                Logger.Error(ex);
            }
            List<ActorInfo> actors;
            try {
                List<Dictionary<string, object>> list = actorMapper.Select("select * from actor_info");
                actors = list == null || list.Count == 0
                    ? new List<ActorInfo>()
                    : actorMapper.ToEntity<ActorInfo>(list, typeof(ActorInfo).GetProperties(), false);
            } catch (Exception ex) {
                Logger.Error(ex);
                return 0;
            }
            int count = 0;
            foreach (ActorInfo actor in actors) {
                if (!IsIncomplete(actor))
                    continue;
                ActorScrapeTask task = new ActorScrapeTask(actor);
                if (CurrentTasks != null && CurrentTasks.Contains(task))
                    continue;
                AddTask(task);
                count++;
                // 大库全量入队会让队列膨胀到上万个：每次启动渐进补齐一批即可，
                // 剩余的下批再补；手动「同步信息」走插队不受影响
                if (count >= AUTO_BATCH_LIMIT) {
                    Logger.Info($"演员资料补齐：本批达到上限 {AUTO_BATCH_LIMIT}，其余待下次启动继续");
                    break;
                }
            }
            if (count > 0) {
                Logger.Info($"演员资料补齐：已入队 {count} 名演员（串行 + 间隔防风控）");
                Start();
            }
            return count;
        }

        /// <summary>刮削新建演员后即时补齐（受开关约束，内部按 ActorID 去重）</summary>
        public static void EnqueueActorIfIncomplete(ActorInfo actorInfo)
        {
            try {
                if (Instance == null || actorInfo == null || actorInfo.ActorID <= 0)
                    return;
                if (!ConfigManager.Settings.ActorAutoEnrich)
                    return;
                if (!HasEnabledActorSource())
                    return;
                if (!IsIncomplete(actorInfo))
                    return;
                ActorScrapeTask task = new ActorScrapeTask(actorInfo);
                if (Instance.CurrentTasks != null && Instance.CurrentTasks.Contains(task))
                    return;
                Instance.AddTask(task);
                Instance.Start();
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        /// <summary>
        /// 手动补齐（演员列表右键「同步信息」）：显式意图，跳过自动开关与待补齐判定
        /// （已补齐/已标记查无此人的也可重试），仍要求 MinnanoAV 源已启用。
        /// 手动任务插队到队列最前（优先于批量后台任务）。
        /// 返回入队人数；源未启用返回 -1（调用方提示）。
        /// </summary>
        public static int EnqueueManual(IEnumerable<ActorInfo> actors)
        {
            if (Instance == null || actors == null)
                return 0;
            if (!HasEnabledActorSource())
                return -1;
            int added = 0;
            foreach (ActorInfo actor in actors) {
                if (actor == null || actor.ActorID <= 0)
                    continue;
                ActorScrapeTask task = new ActorScrapeTask(actor);
                // 清掉同 ActorID 的已完成/已取消旧任务再入队，避免列表里同名堆积
                if (Instance.CurrentTasks != null) {
                    for (int i = Instance.CurrentTasks.Count - 1; i >= 0; i--) {
                        AbstractTask existing = Instance.CurrentTasks[i];
                        if (existing is ActorScrapeTask old && old.ActorID == actor.ActorID &&
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
                Logger.Info($"演员资料手动补齐：插队入队 {added} 位（串行 + 间隔防风控）");
                Instance.Start();
            }
            return added;
        }

        /// <summary>
        /// 整库手动补齐（演员列表右键「同步信息（整个库）」）：全库演员全量入队（按库内顺序尾插）。
        /// 显式意图，跳过自动开关、待补齐判定与批量上限（是否覆盖已有字段由覆写开关决定），
        /// 仍要求演员资料源已启用。返回入队人数；源未启用返回 -1（调用方提示）。
        /// </summary>
        public static int EnqueueAllLibrary()
        {
            if (Instance == null)
                return 0;
            if (!HasEnabledActorSource())
                return -1;
            List<ActorInfo> actors;
            try {
                List<Dictionary<string, object>> list = actorMapper.Select(
                    "select * from actor_info where ActorName is not null and ActorName <> ''");
                actors = list == null || list.Count == 0
                    ? new List<ActorInfo>()
                    : actorMapper.ToEntity<ActorInfo>(list, typeof(ActorInfo).GetProperties(), false);
            } catch (Exception ex) {
                Logger.Error(ex);
                return 0;
            }
            // 先一次性清掉历史已完成/已取消任务并收集在队 ActorID（整库量大，逐个清理是 O(n²)）
            if (Instance.CurrentTasks != null) {
                for (int i = Instance.CurrentTasks.Count - 1; i >= 0; i--) {
                    if (Instance.CurrentTasks[i] is ActorScrapeTask old &&
                        (old.Status == TaskStatus.RanToCompletion || old.Status == TaskStatus.Canceled ||
                         old.Status == TaskStatus.Faulted)) {
                        Instance.CurrentTasks.RemoveAt(i);
                    }
                }
            }
            HashSet<long> queuedIds = new HashSet<long>(
                Instance.CurrentTasks?.OfType<ActorScrapeTask>().Select(t => t.ActorID) ?? Enumerable.Empty<long>());
            int added = 0;
            foreach (ActorInfo actor in actors) {
                if (actor == null || actor.ActorID <= 0 || queuedIds.Contains(actor.ActorID))
                    continue;
                Instance.AddTask(new ActorScrapeTask(actor));
                queuedIds.Add(actor.ActorID);
                added++;
            }
            if (added > 0) {
                Logger.Info($"演员资料整库补齐：入队 {added} 位（串行 + 间隔防风控）");
                Instance.Start();
            }
            return added;
        }

        #region "任务持久化（崩溃/退出后恢复未完成的演员补齐任务，与刮削任务同模式）"

        /// <summary>是否为退出流程（退出时仍保存快照，但取消操作不删除快照）</summary>
        public bool Exiting { get; set; }

        private readonly object _PersistLock = new object();

        private readonly Dictionary<long, ActorScrapeTaskRecord> _PendingRecords = new Dictionary<long, ActorScrapeTaskRecord>();

        private static string PersistPath => Path.Combine(Jvedio.Core.Global.PathManager.CurrentUserFolder, "actor_scrape_tasks.json");

        public new void AddTask(AbstractTask task)
        {
            if (CurrentTasks.Contains(task))
                return;

            task.onCompleted += OnTaskPersistCompleted;
            if (task is ActorScrapeTask actorTask) {
                lock (_PersistLock) {
                    _PendingRecords[actorTask.ActorID] = ToRecord(actorTask);
                }
            }
            SaveTasksToFile();
            try {
                base.AddTask(task);
            } catch {
                task.onCompleted -= OnTaskPersistCompleted;
                if (task is ActorScrapeTask failedTask) {
                    lock (_PersistLock) {
                        _PendingRecords.Remove(failedTask.ActorID);
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
            if (task is ActorScrapeTask actorTask) {
                lock (_PersistLock) {
                    _PendingRecords[actorTask.ActorID] = ToRecord(actorTask);
                }
            }
            SaveTasksToFile();
            try {
                base.AddTaskFront(task);
            } catch {
                task.onCompleted -= OnTaskPersistCompleted;
                if (task is ActorScrapeTask failedTask) {
                    lock (_PersistLock) {
                        _PendingRecords.Remove(failedTask.ActorID);
                    }
                    SaveTasksToFile();
                }
                throw;
            }
        }

        private void OnTaskPersistCompleted(object sender, EventArgs e)
        {
            if (sender is ActorScrapeTask actorTask) {
                lock (_PersistLock) {
                    _PendingRecords.Remove(actorTask.ActorID);
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
                    if (task is ActorScrapeTask actorTask)
                        _PendingRecords.Remove(actorTask.ActorID);
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
            if (task is ActorScrapeTask actorTask) {
                lock (_PersistLock) {
                    _PendingRecords.Remove(actorTask.ActorID);
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

        private static ActorScrapeTaskRecord ToRecord(ActorScrapeTask task)
        {
            return new ActorScrapeTaskRecord {
                ActorID = task.ActorID,
                ActorName = task.Title,
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
        /// 启动时从本地文件恢复未完成的演员补齐任务。
        /// 所有恢复的任务只进入任务列表（状态显示为「上次未完成，可重启」），
        /// 是否继续由用户决定：点「重启全部失败」或单个任务「重启」后才会开始。
        /// 演员已被删除的快照直接丢弃（避免僵尸任务）。
        /// </summary>
        /// <returns>恢复的任务数量</returns>
        public int RestoreTasksFromFile()
        {
            List<ActorScrapeTaskRecord> records = new List<ActorScrapeTaskRecord>();
            try {
                if (File.Exists(PersistPath)) {
                    string json = File.ReadAllText(PersistPath);
                    records = JsonConvert.DeserializeObject<List<ActorScrapeTaskRecord>>(json) ?? new List<ActorScrapeTaskRecord>();
                }
            } catch (Exception ex) {
                Logger.Error(ex);
                records = new List<ActorScrapeTaskRecord>();
            }

            int restored = 0;
            foreach (ActorScrapeTaskRecord record in records) {
                if (record == null || record.ActorID <= 0)
                    continue;
                try {
                    if (CurrentTasks.Any(t => t is ActorScrapeTask at && at.ActorID == record.ActorID))
                        continue;

                    // 演员已被删除则丢弃快照（避免「不存在 ActorID」的僵尸任务）
                    ActorInfo actor = actorMapper.SelectOne(new SelectWrapper<ActorInfo>().Eq("ActorID", record.ActorID));
                    if (actor == null) {
                        Logger.Info($"actor {record.ActorID} no longer exists, drop snapshot");
                        continue;
                    }

                    ActorScrapeTask task = new ActorScrapeTask(actor) { CreateTime = record.CreateTime };
                    task.onCompleted += OnTaskPersistCompleted;

                    // 只恢复到列表（保持「未完成」状态），不自动开始；用户点「重启全部失败」或单任务重启后继续
                    task.Status = System.Threading.Tasks.TaskStatus.Canceled;
                    task.StatusText = SuperControls.Style.LangManager.GetValueByKey("TaskInterrupted");
                    CurrentTasks.Add(task);
                    lock (_PersistLock) {
                        _PendingRecords[record.ActorID] = record;
                    }
                    restored++;
                } catch (Exception ex) {
                    Logger.Error(ex);
                }
            }
            if (restored > 0) {
                Logger.Info($"restore {restored} actor scrape tasks (not started, waiting for user)");
                SaveTasksToFile();
            }
            return restored;
        }

        #endregion

        // 支持任意一个演员资料源(JAVDatabase 优先,MinnanoAV 兜底)
        private static bool HasEnabledActorSource()
        {
            if (CrawlerManager.PluginMetaDatas == null || ConfigManager.ServerConfig?.CrawlerServers == null)
                return false;
            foreach (string id in new[] { "crawler/javdatabase", "crawler/minnanoav" }) {
                bool pluginOn = CrawlerManager.PluginMetaDatas.Any(arg => arg != null && arg.Enabled &&
                    arg.PluginID != null && arg.PluginID.Equals(id, StringComparison.OrdinalIgnoreCase));
                bool sourceOn = ConfigManager.ServerConfig.CrawlerServers.Any(arg => arg != null && arg.Enabled &&
                    arg.Available == 1 && !string.IsNullOrEmpty(arg.Url) &&
                    arg.PluginID != null && arg.PluginID.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (pluginOn && sourceOn)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 待补齐判定：从未补齐（WebUrl 空）、未被确认查无（ExtraInfo 无 notfound 标记）、
        /// 且核心资料缺失（无身高且无生日）
        /// </summary>
        private static bool IsIncomplete(ActorInfo actor)
        {
            if (actor == null || string.IsNullOrEmpty(actor.ActorName) || actor.ActorID <= 0)
                return false;
            if (!string.IsNullOrEmpty(actor.WebUrl))
                return false;
            if (!string.IsNullOrEmpty(actor.ExtraInfo) && actor.ExtraInfo.Contains("minnanoav:notfound"))
                return false;
            return actor.Height <= 0 && string.IsNullOrEmpty(actor.Birthday);
        }

        /// <summary>
        /// 重启全部失败（取消）的任务，分批重启并等待每批完成（防限流），可被「取消所有」中止
        /// </summary>
        public async void RestartAllFailed()
        {
            if (RestartAllRunning)
                return;
            RestartAllRunning = true;
            RestartAllAborted = false;
            try {
                var failed = CurrentTasks.Where(t => t.Status == TaskStatus.Canceled).ToList();
                if (failed.Count == 0)
                    return;
                int index = 0;
                while (index < failed.Count) {
                    if (RestartAllAborted)
                        return;
                    int batch = Math.Min(failed.Count - index, TASK_COUNT);
                    List<AbstractTask> toRestart = new List<AbstractTask>();
                    for (int i = 0; i < batch; i++) {
                        AbstractTask t = failed[index + i];
                        if (CurrentTasks.Contains(t) && t.Status == TaskStatus.Canceled)
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
                        if (RestartAllAborted) {
                            foreach (AbstractTask t in toRestart)
                                t.Cancel();
                            return;
                        }
                        completed = true;
                        for (int i = 0; i < toRestart.Count; i++) {
                            var t = toRestart[i];
                            if (t.Status == TaskStatus.Running || t.Status == TaskStatus.WaitingToRun) {
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
    }
}
