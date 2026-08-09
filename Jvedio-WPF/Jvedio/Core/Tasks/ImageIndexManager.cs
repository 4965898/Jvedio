using Jvedio.Core.Config;
using Jvedio.Core.FFmpeg;
using Jvedio.Core.Net;
using Jvedio.Entity;
using Jvedio.Mapper;
using SuperUtils.Framework.ORM.Wrapper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Jvedio.App;
using static Jvedio.MapperManager;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// 图片存在性索引自动重建：
    /// 刮削 / 截图等操作使影片获得图片后，累计到阈值时在后台静默重建一次
    /// common_picture_exist 索引，避免筛选「无海报图 / 无缩略图」时残留过期结果。
    /// </summary>
    public static class ImageIndexManager
    {
        private static readonly object LockObj = new object();

        private static int _PendingCount = 0;

        private static bool _Rebuilding = false;

        /// <summary>
        /// 上一次已计数的下载任务（onDownloadSuccess 每个任务会触发两次，需去重）
        /// </summary>
        private static SuperUtils.Framework.Tasks.AbstractTask _LastCountedTask = null;

        static ImageIndexManager()
        {
            // 每成功刮削（同步信息/海报/缩略图）一条影片计数 +1（同一任务触发两次只计一次）
            DownLoadTask.onDownloadSuccess += (task) => {
                bool needCount = false;
                lock (LockObj) {
                    if (_LastCountedTask != task) {
                        _LastCountedTask = task;
                        needCount = true;
                    }
                }
                if (needCount)
                    Count(1);
            };
            // 截图成功也会影响「以截图作为海报/缩略图」的存在性判断
            ScreenShotTask.onScreenShotCompleted += (ok, dataID) => {
                if (ok)
                    Count(1);
            };
        }

        /// <summary>
        /// 空方法，仅用于触发类型初始化（静态构造函数完成事件订阅）
        /// </summary>
        public static void Init()
        {
        }

        /// <summary>
        /// 当前阈值（累计多少条变更后触发一次后台重建），0 表示关闭自动重建
        /// </summary>
        public static int Threshold {
            get {
                try {
                    return (int)ConfigManager.Settings.AutoRebuildImageIndexCount;
                } catch {
                    return 10;
                }
            }
        }

        public static void Count(int count = 1)
        {
            if (count <= 0 || Threshold <= 0)
                return;
            int total = Interlocked.Add(ref _PendingCount, count);
            if (total >= Threshold)
                TryRebuild();
        }

        private static void TryRebuild()
        {
            lock (LockObj) {
                if (_Rebuilding)
                    return;
                _Rebuilding = true;
            }
            Task.Run(() => {
                try {
                    RebuildOnce();
                } catch (Exception ex) {
                    Logger.Error(ex);
                } finally {
                    lock (LockObj)
                        _Rebuilding = false;
                    // 重建期间又累计到阈值，则继续重建
                    if (Volatile.Read(ref _PendingCount) >= Threshold)
                        TryRebuild();
                }
            });
        }

        /// <summary>
        /// 与「选项-库」中手动建立图片索引逻辑一致，但静默执行、不打扰 UI
        /// </summary>
        private static void RebuildOnce()
        {
            Interlocked.Exchange(ref _PendingCount, 0);
            string sql = VideoMapper.SQL_BASE;
            IWrapper<Video> wrapper = new SelectWrapper<Video>();
            wrapper.Select("metadata.DataID", "Path", "VID", "Hash");
            sql = wrapper.ToSelect(false) + sql;
            List<Dictionary<string, object>> temp = metaDataMapper.Select(sql);
            List<Video> videos = metaDataMapper.ToEntity<Video>(temp, typeof(Video).GetProperties(), true);
            if (videos == null || videos.Count <= 0)
                return;
            List<string> list = new List<string>();
            long pathType = ConfigManager.Settings.PicPathMode;
            for (int i = 0; i < videos.Count; i++) {
                Video video = videos[i];
                bool hasSmall = File.Exists(video.GetSmallImage());
                bool hasBig = File.Exists(video.GetBigImage());
                bool hasScreen = false;
                try {
                    string screenDir = video.GetScreenShot();
                    if (!string.IsNullOrEmpty(screenDir) && Directory.Exists(screenDir))
                        hasScreen = Directory.EnumerateFiles(screenDir, "*.*", System.IO.SearchOption.TopDirectoryOnly).Any();
                } catch { }
                // 将截图视为海报/缩略图存在
                hasSmall = hasSmall || hasScreen;
                hasBig = hasBig || hasScreen;
                list.Add($"({video.DataID},{pathType},0,{(hasSmall ? 1 : 0)})");
                list.Add($"({video.DataID},{pathType},1,{(hasBig ? 1 : 0)})");
            }
            string deleteSql = $"delete from common_picture_exist where PathType={pathType};";
            string insertSql = $"insert into common_picture_exist(DataID,PathType,ImageType,Exist) values {string.Join(",", list)};";
            videoMapper.ExecuteNonQuery($"begin;{deleteSql}{insertSql}commit;");
            Logger.Info($"image index auto rebuilt silently, {videos.Count} videos");
        }
    }
}