using Jvedio.Entity;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Jvedio.App;
using static Jvedio.MapperManager;
using ConfigManager = Jvedio.ConfigManager;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// 视频真实时长索引（metadata_video.FileDuration，秒，0=未知）：
    /// 惰性——打开详情页时顺手写入（Video.UpdateFileDurationIndex）；
    /// 手动——选项-库「建立视频时长索引」触发全量后台重建（本类）。
    /// 分段视频取各段之和；文件不存在/读不到记 0（未知），「视频时长」排序时恒排末尾。
    /// </summary>
    public static class DurationIndexManager
    {
        /// <summary>
        /// 单飞标志：1=重建进行中
        /// </summary>
        private static int _Running = 0;

        /// <summary>
        /// 后台全量重建当前库的视频时长索引。
        /// 返回 false 表示已有重建在运行（本次请求被忽略）。
        /// </summary>
        public static Task<bool> RebuildAsync()
        {
            if (Interlocked.CompareExchange(ref _Running, 1, 0) != 0) {
                Logger.Info("file duration index rebuild is already running, skip");
                return Task.FromResult(false);
            }
            return Task.Run(() => {
                try {
                    RebuildOnce();
                    return true;
                } catch (Exception ex) {
                    Logger.Error(ex);
                    return false;
                } finally {
                    Interlocked.Exchange(ref _Running, 0);
                }
            });
        }

        private static void RebuildOnce()
        {
            long dbid = ConfigManager.Main.CurrentDBId;
            string sql = "SELECT metadata.DataID, metadata.Path, metadata_video.SubSection FROM metadata " +
                "JOIN metadata_video ON metadata.DataID=metadata_video.DataID " +
                $"WHERE metadata.DBId={dbid} AND metadata.DataType=0";
            List<Dictionary<string, object>> rows = metaDataMapper.Select(sql);
            if (rows == null || rows.Count == 0)
                return;

            StringBuilder builder = new StringBuilder();
            int pending = 0;
            int done = 0;
            foreach (Dictionary<string, object> row in rows) {
                long dataID = 0;
                long.TryParse(row["DataID"]?.ToString(), out dataID);
                if (dataID <= 0)
                    continue;
                string path = row["Path"]?.ToString();
                string subSection = row.ContainsKey("SubSection") ? row["SubSection"]?.ToString() : null;
                long seconds = SumDuration(path, subSection);
                builder.Append($"update metadata_video set FileDuration={seconds} where DataID={dataID};");
                pending++;
                done++;
                // 分块小事务，避免巨型事务锁库（与 3.39 DataIndexManager 的教训一致）
                if (pending >= 500) {
                    ExecuteBatch(builder);
                    builder.Clear();
                    pending = 0;
                    Logger.Info($"file duration index rebuilding: {done}/{rows.Count}");
                }
            }
            if (pending > 0)
                ExecuteBatch(builder);
            Logger.Info($"file duration index rebuild done: {done}");
        }

        /// <summary>
        /// 计算一个条目的真实时长（秒）：分段取各段之和；单文件直接读；文件不存在/读不到为 0
        /// </summary>
        private static long SumDuration(string path, string subSection)
        {
            if (!string.IsNullOrEmpty(subSection)) {
                string[] parts = subSection.Split(
                    new char[] { SuperUtils.Values.ConstValues.Separator }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0) {
                    long sum = 0;
                    foreach (string p in parts)
                        sum += Video.GetFileDurationSeconds(p);
                    return sum;
                }
            }
            return Video.GetFileDurationSeconds(path);
        }

        private static void ExecuteBatch(StringBuilder builder)
        {
            try {
                metaDataMapper.ExecuteNonQuery("begin;" + builder.ToString() + "commit;");
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }
    }
}
