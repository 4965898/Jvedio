using SuperUtils.Framework.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Jvedio.App;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// 重命名任务管理器：一部影片一个任务，本地文件操作可少量并发。
    /// 支持任务页的取消全部 / 重启失败 / 清除列表（与下载、翻译模块同构）。
    /// </summary>
    public class RenameTaskManager : BaseManager
    {
        /// <summary>每个任务的间隔 (ms)：本地 IO 无需防限流，仅留极小间隔</summary>
        private const int TASK_DELAY = 50;

        /// <summary>同时进行的任务数：文件移动为本地 IO，少量并发</summary>
        private const int TASK_COUNT = 4;

        private const int LONG_TASK_DELAY = 10 * 1000;
        private const bool ENABLE_LONG_TASK_DELAY = false;
        private const int LONG_TASK_COUNT = 0;

        private static TaskConfig DEFAULT_CONFIG { get; set; } = new TaskConfig() {
            TaskDelay = TASK_DELAY,
            TaskCount = TASK_COUNT,
            LongTaskCount = LONG_TASK_COUNT,
            LongTaskDelay = LONG_TASK_DELAY,
            EnableLongTaskDelay = ENABLE_LONG_TASK_DELAY,
        };

        protected RenameTaskManager() { }

        public new static RenameTaskManager Instance { get; set; }

        public new static RenameTaskManager CreateInstance()
        {
            if (Instance == null)
                Instance = new RenameTaskManager();
            return Instance;
        }

        private static ReliableTaskDispatcher<RenameTask> Dispatcher { get; set; }

        static RenameTaskManager()
        {
            Dispatcher = ReliableTaskDispatcher<RenameTask>.CreateInstance(DEFAULT_CONFIG);
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

        public override void AddToDispatcher(AbstractTask task)
        {
            Dispatcher.Enqueue(task as RenameTask);
        }

        public override void ClearDispatcher()
        {
            Dispatcher.ClearDoneList();
        }

        /// <summary>
        /// 重启全部失败（取消）的任务，分批重启并等待每批完成。
        /// 用户在重启过程中点击「取消所有」/「清除列表」时会立即中止。
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
            } finally {
                RestartAllRunning = false;
            }
        }
    }
}
