using Jvedio.Core.Tasks;
using SuperUtils.Framework.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jvedio
{
    // The dispatcher test compiles its source without starting the WPF application.
    public static class App
    {
        public static TestLogger Logger { get; } = new TestLogger();
    }

    public sealed class TestLogger
    {
        public void Error(Exception error)
        {
            Console.Error.WriteLine(error);
        }
    }
}

namespace Jvedio.DispatcherStress
{
    internal sealed class QuickTask : AbstractTask
    {
        public int Starts;

        public override void DoWork()
        {
            Interlocked.Increment(ref Starts);
            Task.Run(async () => {
                await Task.Delay(1);
                Status = TaskStatus.RanToCompletion;
                OnCompleted(EventArgs.Empty);
            });
        }
    }

    internal static class Program
    {
        private static void Main()
        {
            var config = new TaskConfig { TaskCount = 8, TaskDelay = 0 };
            var dispatcher = ReliableTaskDispatcher<QuickTask>.CreateInstance(config);
            var tasks = new List<QuickTask>();

            // Concurrent enqueues and list clearing must never strand or double-start a task.
            var firstBatch = Enumerable.Range(0, 160).Select(_ => new QuickTask()).ToArray();
            tasks.AddRange(firstBatch);
            Parallel.ForEach(firstBatch, task => {
                dispatcher.Enqueue(task);
                dispatcher.ClearDoneList();
            });
            WaitFor(firstBatch);

            // Repeatedly enqueue as the previous worker drains and exits.
            for (int i = 0; i < 50; i++) {
                var task = new QuickTask();
                tasks.Add(task);
                dispatcher.Enqueue(task);
                WaitFor(new[] { task });
                dispatcher.ClearDoneList();
            }

            if (tasks.Any(task => task.Starts != 1))
                throw new Exception("A task started more than once.");
            Console.WriteLine($"Dispatcher stress passed: {tasks.Count} tasks, each started once.");
        }

        private static void WaitFor(IEnumerable<QuickTask> tasks)
        {
            if (!SpinWait.SpinUntil(
                () => tasks.All(task => task.Status == TaskStatus.RanToCompletion),
                TimeSpan.FromSeconds(20))) {
                throw new Exception("A queued task did not start within 20 seconds.");
            }
        }
    }
}
