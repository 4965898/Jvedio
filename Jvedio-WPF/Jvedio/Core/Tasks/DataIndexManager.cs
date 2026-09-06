using Jvedio.Entity;
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
    /// 资源存在性（可播放）索引自动维护：
    /// 扫描完成 / 文件删除 / 文件移动后，后台静默重建或增量更新 metadata.PathExist，
    /// 避免筛选「可播放 / 不可播放」时残留过期结果（此前只能手动点「建立资源存在索引」）。
    /// </summary>
    public static class DataIndexManager
    {
        private static readonly object LockObj = new object();

        private static bool _Rebuilding = false;

        private static bool _PendingRebuild = false;

        /// <summary>
        /// 全量静默重建 metadata.PathExist（与「选项-库」手动建立资源存在索引逻辑一致）。
        /// 单飞：重建进行中再次请求时标记 pending，完成后自动补一次，全程只有一条重建链路。
        /// </summary>
        public static void RebuildSilently()
        {
            lock (LockObj) {
                if (_Rebuilding) {
                    _PendingRebuild = true;
                    return;
                }
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
                    bool rerun = false;
                    lock (LockObj) {
                        if (_PendingRebuild) {
                            _PendingRebuild = false;
                            rerun = true;
                        }
                    }
                    if (rerun)
                        RebuildSilently();
                }
            });
        }

        private static void RebuildOnce()
        {
            // 只取 DataID + Path 两列原始数据，避免把整表映射成 MetaData 实体
            // （MetaData 的 Genre/Label 等 setter 会为每行构建 ObservableCollection，数万行映射非常重，
            //   且 metaDataMapper.SelectList 全列读取长时间占住 Mapper 锁，会与筛选查询撞车导致卡顿）
            List<Dictionary<string, object>> rows = metaDataMapper.Select("select DataID, Path from metadata");
            if (rows == null || rows.Count == 0)
                return;

            List<long> exist = new List<long>(rows.Count);
            List<long> missing = new List<long>();
            List<long> hasSub = new List<long>(rows.Count);
            List<long> noSub = new List<long>();

            // 字幕检测缓存：同一目录只扫一次 *.srt；
            // 集合内同时放入「文件名」与「去掉最后一个语言后缀的名」（如 abc.chs 与 abc），
            // 这样查询视频名是否命中只需一次 O(1) Contains，与 Video.FindSrtFile 的判断规则一致
            Dictionary<string, HashSet<string>> srtDirCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (Dictionary<string, object> row in rows) {
                if (row == null || !row.TryGetValue("DataID", out object idObj) || idObj == null)
                    continue;
                if (!long.TryParse(idObj.ToString(), out long id) || id <= 0)
                    continue;
                string path = row.TryGetValue("Path", out object pathObj) ? pathObj?.ToString() : null;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
                    missing.Add(id);
                    noSub.Add(id); // 视频文件不存在时字幕状态无意义，按无字幕处理
                } else {
                    exist.Add(id);
                    if (HasSubtitleCached(path, srtDirCache))
                        hasSub.Add(id);
                    else
                        noSub.Add(id);
                }
            }

            // 分块小事务更新：避免「update metadata set PathExist=1; 4万条 update;」巨型单事务
            // 长时间占住写锁，导致并发读/写出现 database is locked 与界面卡顿
            const int CHUNK = 500;
            UpdateChunked(exist, "PathExist", 1, CHUNK);
            UpdateChunked(missing, "PathExist", 0, CHUNK);
            UpdateChunked(hasSub, "SubtitleExist", 1, CHUNK);
            UpdateChunked(noSub, "SubtitleExist", 0, CHUNK);
            Logger.Info($"data index auto rebuilt silently, {rows.Count} rows");
        }

        /// <summary>
        /// 批量场景下的字幕存在判断（按目录缓存扫描结果）
        /// </summary>
        private static bool HasSubtitleCached(string videoPath, Dictionary<string, HashSet<string>> srtDirCache)
        {
            try {
                string dir = System.IO.Path.GetDirectoryName(videoPath);
                string name = System.IO.Path.GetFileNameWithoutExtension(videoPath);
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name))
                    return false;
                if (!srtDirCache.TryGetValue(dir, out HashSet<string> names)) {
                    names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string f in Directory.GetFiles(dir, "*.srt")) {
                        string fn = System.IO.Path.GetFileNameWithoutExtension(f);
                        names.Add(fn);
                        int dot = fn.LastIndexOf('.');
                        if (dot > 0)
                            names.Add(fn.Substring(0, dot));
                    }
                    srtDirCache[dir] = names;
                }
                return names.Contains(name);
            } catch (Exception ex) {
                Logger.Error(ex);
                return false;
            }
        }

        private static void UpdateChunked(List<long> ids, string field, int value, int chunkSize)
        {
            if (ids == null || ids.Count == 0)
                return;
            for (int i = 0; i < ids.Count; i += chunkSize) {
                int count = Math.Min(chunkSize, ids.Count - i);
                List<long> chunk = ids.GetRange(i, count);
                try {
                    videoMapper.ExecuteNonQuery($"update metadata set {field}={value} where DataID in ({string.Join(",", chunk)});");
                } catch (Exception ex) {
                    Logger.Error(ex);
                }
            }
        }

        /// <summary>
        /// 立即按当前磁盘状态重建资源存在性索引并等待完成（供筛选「可播放/不可播放」前调用）。
        /// 后台静默重建的索引只是当时的快照：会话期间文件被外部增删/移动、或建立索引时
        /// 移动硬盘/网络盘尚未就绪，都会让索引与实际相反（可播放筛出不可播放、反之亦然）。
        /// 现场重建保证筛选结果与点击那一刻的磁盘状态一致。
        /// </summary>
        public static Task<bool> RebuildAsync()
        {
            return Task.Run(() => {
                try {
                    RebuildOnce();
                    return true;
                } catch (Exception ex) {
                    Logger.Error(ex);
                    return false;
                }
            });
        }

        /// <summary>
        /// 文件删除后增量标记不可播放（PathExist=0），字幕状态同步清零。仅记日志，失败不抛异常。
        /// </summary>
        public static void MarkPathMissing(params long[] dataIds)
        {
            if (dataIds == null || dataIds.Length == 0)
                return;
            try {
                List<long> distinct = dataIds.Where(id => id > 0).Distinct().ToList();
                if (distinct.Count == 0)
                    return;
                string ids = string.Join(",", distinct);
                videoMapper.ExecuteNonQuery($"update metadata set PathExist=0 where DataID in ({ids});");
                videoMapper.ExecuteNonQuery($"update metadata set SubtitleExist=0 where DataID in ({ids});");
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        /// <summary>
        /// 文件移动/路径更新后增量标记可播放（PathExist=1），并按新路径同步字幕状态。仅记日志，失败不抛异常。
        /// </summary>
        public static void MarkPathExists(params long[] dataIds)
        {
            if (dataIds == null || dataIds.Length == 0)
                return;
            try {
                List<long> distinct = dataIds.Where(id => id > 0).Distinct().ToList();
                if (distinct.Count == 0)
                    return;
                string ids = string.Join(",", distinct);
                videoMapper.ExecuteNonQuery($"update metadata set PathExist=1 where DataID in ({ids});");

                // 移动后字幕文件通常一并移动，按新路径重新检测
                List<Dictionary<string, object>> rows = metaDataMapper.Select($"select DataID, Path from metadata where DataID in ({ids});");
                if (rows != null) {
                    List<long> hasSub = new List<long>();
                    List<long> noSub = new List<long>();
                    foreach (Dictionary<string, object> row in rows) {
                        if (row == null || !long.TryParse(row["DataID"]?.ToString(), out long id) || id <= 0)
                            continue;
                        string path = row.TryGetValue("Path", out object pathObj) ? pathObj?.ToString() : null;
                        if (!string.IsNullOrEmpty(path) && Video.FindSrtFile(path) != null)
                            hasSub.Add(id);
                        else
                            noSub.Add(id);
                    }
                    UpdateChunked(hasSub, "SubtitleExist", 1, 500);
                    UpdateChunked(noSub, "SubtitleExist", 0, 500);
                }
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }
    }
}