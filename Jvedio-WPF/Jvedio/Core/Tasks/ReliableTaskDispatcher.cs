using SuperUtils.CustomEventArgs;
using SuperUtils.Framework.Tasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// Dispatches tasks with the queue and worker lifetime guarded by the same lock.
    /// Enqueue cannot miss the moment when an idle worker exits.
    /// </summary>
    internal sealed class ReliableTaskDispatcher<T> where T : AbstractTask
    {
        private readonly object _gate = new object();
        private readonly Queue<T> _waiting = new Queue<T>();
        private readonly List<T> _working = new List<T>();
        private readonly List<T> _done = new List<T>();
        private readonly List<T> _canceled = new List<T>();
        private readonly TaskConfig _config;
        private bool _workerRunning;
        private bool _completionNotified;
        private int _beforeTaskCount;
        private volatile float _progress;

        public float Progress => _progress;
        public bool Working {
            get {
                lock (_gate)
                    return _workerRunning;
            }
        }

        public event EventHandler onWorking;
        public event EventHandler onComplete;
        public event EventHandler onLongDelay;

        private ReliableTaskDispatcher(TaskConfig config)
        {
            _config = config ?? TaskConfig.DEFAULT;
        }

        public static ReliableTaskDispatcher<T> CreateInstance(TaskConfig config)
        {
            return new ReliableTaskDispatcher<T>(config);
        }

        /// <summary>
        /// 运行中更新并发任务数（工作循环每次取任务时读取，立即生效）；值限制在 1~10。
        /// </summary>
        public void UpdateTaskCount(int taskCount)
        {
            lock (_gate)
                _config.TaskCount = (uint)Math.Max(1, Math.Min(10, taskCount));
        }

        public void Enqueue(T task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            lock (_gate) {
                if (!_waiting.Contains(task) && !_working.Contains(task)) {
                    _waiting.Enqueue(task);
                    _completionNotified = false;
                }
                StartWorkerLocked();
            }
        }

        public void BeginWork()
        {
            lock (_gate) {
                if (_waiting.Count > 0 || _working.Count > 0)
                    StartWorkerLocked();
            }
        }

        public void ClearDoneList()
        {
            lock (_gate) {
                _done.Clear();
                _canceled.Clear();
                // Canceled tasks still in the queue belong to the cleared list.
                int count = _waiting.Count;
                for (int i = 0; i < count; i++) {
                    T task = _waiting.Dequeue();
                    if (task.Status == TaskStatus.WaitingToRun)
                        _waiting.Enqueue(task);
                }
            }
        }

        private void StartWorkerLocked()
        {
            if (_workerRunning)
                return;
            _workerRunning = true;
            Task.Run(WorkLoopAsync);
        }

        private async Task WorkLoopAsync()
        {
            try {
                while (true) {
                    List<T> toStart = new List<T>();
                    bool finished;
                    bool notifyComplete;
                    int delay;
                    bool longDelay;

                    lock (_gate) {
                        ReconcileLocked();
                        int limit = Math.Max(1, (int)_config.TaskCount);
                        while (_working.Count < limit && _waiting.Count > 0) {
                            T task = _waiting.Dequeue();
                            if (task.Status == TaskStatus.WaitingToRun) {
                                _working.Add(task);
                                toStart.Add(task);
                            } else if (task.Status == TaskStatus.Canceled) {
                                _canceled.Add(task);
                            } else if (task.Status == TaskStatus.RanToCompletion) {
                                _done.Add(task);
                            }
                        }

                        int total = _waiting.Count + _working.Count + _done.Count + _canceled.Count;
                        _progress = total == 0 ? 100f :
                            (float)Math.Round(100.0 * (_done.Count + _canceled.Count) / total, 2);
                        finished = _waiting.Count == 0 && _working.Count == 0;
                        notifyComplete = finished && !_completionNotified;
                        if (notifyComplete)
                            _completionNotified = true;

                        // Both the empty decision and the worker handoff happen under _gate.
                        // An Enqueue after this point starts a new worker immediately.
                        if (finished)
                            _workerRunning = false;

                        longDelay = !finished && _config.EnableLongTaskDelay &&
                            _config.LongTaskCount > 0 && _done.Count > 0 &&
                            _done.Count != _beforeTaskCount &&
                            _done.Count % _config.LongTaskCount == 0;
                        if (longDelay)
                            _beforeTaskCount = _done.Count;
                        delay = longDelay ? (int)_config.LongTaskDelay :
                            _working.Count > 0 ? (int)_config.TaskDelay : 0;
                    }

                    foreach (T task in toStart) {
                        try {
                            task.Start();
                        } catch (Exception ex) {
                            App.Logger?.Error(ex);
                            task.Cancel();
                        }
                    }

                    try {
                        onWorking?.Invoke(this, EventArgs.Empty);
                        if (notifyComplete && !Working)
                            onComplete?.Invoke(this, EventArgs.Empty);
                        if (longDelay)
                            onLongDelay?.Invoke(this, new MessageCallBackEventArgs(delay.ToString()));
                    } catch (Exception ex) {
                        App.Logger?.Error(ex);
                    }

                    if (finished)
                        return;

                    await Task.Delay(Math.Max(0, delay) + 200);
                    if (longDelay) {
                        try {
                            onLongDelay?.Invoke(this, new MessageCallBackEventArgs("0"));
                        } catch (Exception ex) {
                            App.Logger?.Error(ex);
                        }
                    }
                }
            } catch (Exception ex) {
                App.Logger?.Error(ex);
                lock (_gate) {
                    _workerRunning = false;
                    if (_waiting.Count > 0 || _working.Count > 0)
                        StartWorkerLocked();
                }
            }
        }

        private void ReconcileLocked()
        {
            for (int i = _working.Count - 1; i >= 0; i--) {
                T task = _working[i];
                if (task.Status == TaskStatus.RanToCompletion) {
                    _done.Add(task);
                    _working.RemoveAt(i);
                } else if (task.Status == TaskStatus.Canceled) {
                    _canceled.Add(task);
                    _working.RemoveAt(i);
                }
            }

            RequeueRestartedLocked(_done);
            RequeueRestartedLocked(_canceled);
        }

        private void RequeueRestartedLocked(List<T> tasks)
        {
            for (int i = tasks.Count - 1; i >= 0; i--) {
                T task = tasks[i];
                if (task.Status == TaskStatus.WaitingToRun) {
                    if (!_waiting.Contains(task) && !_working.Contains(task))
                        _waiting.Enqueue(task);
                    tasks.RemoveAt(i);
                }
            }
        }
    }
}
