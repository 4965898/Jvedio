using Jvedio.Core.Translation;
using SuperUtils.Framework.Tasks;
using System;
using System.Linq;
using System.Threading.Tasks;

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
            Dispatcher.Enqueue(task as TranslateTask);
            Dispatcher.BeginWork();
        }

        public override void ClearDispatcher()
        {
            Dispatcher.ClearDoneList();
        }

        /// <summary>
        /// 重启全部失败（取消）的任务，逐个重启等待完成，避免并发翻译被限流
        /// </summary>
        public async void RestartAllFailed()
        {
            var failed = CurrentTasks.Where(t => t.Status == TaskStatus.Canceled).ToList();
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
            Start();
        }
    }
}