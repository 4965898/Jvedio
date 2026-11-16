using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using static Jvedio.MapperManager;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// 观看时长统计：本软件用外部播放器（fire-and-forget 启动，拿不到进程句柄），
    /// 采用「进程名轮询」近似——播放时起会话，每 5 秒探测播放器进程是否存活并累计，
    /// 每 30 秒把增量落库（update metadata set PlayDuration=ifnull(PlayDuration,0)+增量）。
    /// 会话结束条件：播放器进程退出 / 开始播放另一部 / 单会话满 6 小时（防僵尸进程永久累计）。
    /// 局限（有意接受）：同播放器连续排播多部时，时长记到最后一次播放的影片；
    /// 未配置播放器路径（用系统默认打开）时无法识别进程，不统计。
    /// 只走裸 SQL，不进 MetaData 实体——避免陈旧实体 UpdateById 把累计时长清零
    /// </summary>
    public static class WatchTimeTracker
    {
        private const int POLL_MS = 5000;
        /// <summary>落库周期（POLL_MS 的倍数）：约 30 秒，崩溃最多丢 30 秒</summary>
        private const int FLUSH_EVERY_TICKS = 6;
        /// <summary>单会话上限：6 小时</summary>
        private const long MAX_SESSION_SECONDS = 6 * 3600;

        private static readonly object _Gate = new object();
        private static long _DataId;
        private static string _PlayerProcessName;
        private static long _LastTickUtc;
        private static long _PendingSeconds;
        private static bool _WorkerStarted;

        /// <summary>开始/切换播放会话（同一播放器连续播多部时以最后一次为准）</summary>
        public static void StartSession(long dataId, string playerPath)
        {
            if (dataId <= 0)
                return;
            string processName = string.IsNullOrWhiteSpace(playerPath)
                ? null : Path.GetFileNameWithoutExtension(playerPath);
            if (string.IsNullOrWhiteSpace(processName))
                return; // 系统默认播放器打开：无法识别进程，不统计
            lock (_Gate) {
                FlushLocked();
                _DataId = dataId;
                _PlayerProcessName = processName;
                _LastTickUtc = DateTime.UtcNow.Ticks;
                _PendingSeconds = 0;
                if (!_WorkerStarted) {
                    _WorkerStarted = true;
                    Task.Run(Loop);
                }
            }
        }

        /// <summary>应用退出时冲刷未落库的增量（ProcessExit 只给约 2 秒，一条 UPDATE 足够）</summary>
        public static void FlushOnExit()
        {
            lock (_Gate)
                FlushLocked();
        }

        private static async Task Loop()
        {
            int ticks = 0;
            while (true) {
                await Task.Delay(POLL_MS).ConfigureAwait(false);
                long flushId = 0;
                int flushSeconds = 0;
                lock (_Gate) {
                    if (_DataId <= 0)
                        continue; // 无活动会话，空转
                    bool alive = IsPlayerRunning();
                    long now = DateTime.UtcNow.Ticks;
                    long delta = (now - _LastTickUtc) / TimeSpan.TicksPerSecond;
                    if (delta > 0)
                        _PendingSeconds += delta;
                    _LastTickUtc = now;
                    if (!alive || _PendingSeconds >= MAX_SESSION_SECONDS) {
                        // 播放器退出 / 达到上限：结束会话并冲刷
                        flushId = _DataId;
                        flushSeconds = (int)Math.Min(_PendingSeconds, int.MaxValue);
                        _DataId = 0;
                        _PendingSeconds = 0;
                        _PlayerProcessName = null;
                    } else if (++ticks % FLUSH_EVERY_TICKS == 0) {
                        // 周期落库：把当前增量写库后清零，崩溃最多丢一个周期
                        flushId = _DataId;
                        flushSeconds = (int)Math.Min(_PendingSeconds, int.MaxValue);
                        _PendingSeconds = 0;
                    }
                }
                if (flushId > 0 && flushSeconds > 0)
                    WriteDelta(flushId, flushSeconds);
            }
        }

        private static bool IsPlayerRunning()
        {
            try {
                Process[] processes = Process.GetProcessesByName(_PlayerProcessName);
                bool alive = processes != null && processes.Length > 0;
                if (processes != null)
                    foreach (Process process in processes)
                        process.Dispose();
                return alive;
            } catch {
                return false;
            }
        }

        private static void FlushLocked()
        {
            if (_DataId <= 0 || _PendingSeconds <= 0)
                return;
            long flushId = _DataId;
            int flushSeconds = (int)Math.Min(_PendingSeconds, int.MaxValue);
            _DataId = 0;
            _PendingSeconds = 0;
            _PlayerProcessName = null;
            WriteDelta(flushId, flushSeconds);
        }

        /// <summary>增量落库：ifnull 防老库列为 NULL；失败仅记日志（下个周期继续累计）</summary>
        private static void WriteDelta(long dataId, int seconds)
        {
            try {
                videoMapper.ExecuteNonQuery(
                    $"update metadata set PlayDuration = ifnull(PlayDuration,0) + {seconds} where DataID={dataId}");
            } catch (Exception ex) {
                App.Logger?.Error($"观看时长落库失败（DataID={dataId}，+{seconds}s）：{ex.Message}");
            }
        }
    }
}
