using Jvedio.Core.Translation;
using SuperUtils.Framework.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Jvedio.App;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// 标题翻译任务管理器：串行执行翻译任务（防限流），支持取消全部/重启失败/清除列表
    /// </summary>
    public class TranslateTaskManager : BaseManager
    {
        /// <summary>
        /// 每个任务的间隔 (ms)，沿用原批量翻译的防限流间隔
        /// </summary>
        private const int TASK_DELAY = 500;

        /// <summary>
        /// 同时进行的任务数：翻译 API 对并发敏感，固定串行执行
        /// </summary>
        private const int TASK_COUNT = 1;

        /// <summary>
        /// 任务的数目到达 LongTaskCount 时暂停的间隔 (ms)
        /// </summary>
        private const int LONG_TASK_DELAY = 10 * 1000;

        /// <summary>
        /// 是否开启长暂停
        /// </summary>
        private const bool ENABLE_LONG_TASK_DELAY = false;

        /// <summary>
        /// 进行长暂停的上限
        /// </summary>
        private const int LONG_TASK_COUNT = 0;


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


        protected TranslateTaskManager() { }

        public new static TranslateTaskManager Instance { get; set; }

        public new static TranslateTaskManager CreateInstance()
        {
            if (Instance == null)
                Instance = new TranslateTaskManager();
            return Instance;
        }

        private static TaskDispatcher<TranslateTask> Dispatcher { get; set; }

        static TranslateTaskManager()
        {
            Dispatcher = TaskDispatcher<TranslateTask>.CreateInstance(DEFAULT_CONFIG);
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
            bool dispatcherIdle = !Dispatcher.Working;
            Dispatcher.Enqueue(task as TranslateTask);
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
                    if (task.Status != TaskStatus.WaitingToRun)
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
        /// 重启全部失败（取消）的任务，分批重启并等待每批完成，避免并发翻译被限流。
        /// 用户在重启过程中点击「取消所有」/「清除列表」时会立即中止，不再自动重启后续批次。
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
                    // 用户已取消/清空列表 → 终止自动重启链
                    if (RestartAllAborted)
                        return;
                    int batch = Math.Min(failed.Count - index, TASK_COUNT);
                    List<AbstractTask> toRestart = new List<AbstractTask>();
                    for (int i = 0; i < batch; i++) {
                        AbstractTask t = failed[index + i];
                        // 仅重启仍在任务列表中的任务（清空列表后旧任务不再拉起），且仍处于失败（取消）状态
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
                        // 重启过程中用户点了取消/清空列表：停掉本批次已重启的任务，不再重启后续批次
                        if (RestartAllAborted) {
                            foreach (AbstractTask t in toRestart)
                                t.Cancel();
                            return;
                        }
                        completed = true;
                        for (int i = 0; i < toRestart.Count; i++) {
                            var t = toRestart[i];
                            if (t.Status == TaskStatus.Running ||
                                t.Status == TaskStatus.WaitingToRun) {
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