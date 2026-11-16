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
    }
}
