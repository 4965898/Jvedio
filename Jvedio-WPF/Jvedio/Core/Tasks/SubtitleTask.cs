using Jvedio.Core.Subtitle;
using SuperUtils.Framework.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Jvedio.MapperManager;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// 字幕下载任务：按影片番号从 SubtitleCat 查找用户偏好语言的字幕（按设置顺序优先，
    /// 如 zh-CN,zh-TW,en,ja），下载后经 SrtNormalizer 规范化再落盘。
    /// 保存位置：模式 0=影片目录（与视频同名 .srt）；模式 1=指定文件夹（番号.语言.srt，同番号共享）。
    /// 路径在任务执行时解析（入队后影片可能被重新刮削/改名）；已有字幕的影片跳过（不覆盖）。
    /// 所有终点路径必须触发 onCanceled（FinalizeWithCancel）或 onCompleted（OnCompleted）之一，
    /// 否则 BaseManager.RunningCount 泄漏，状态栏角标数字永远不消
    /// </summary>
    public class SubtitleTask : AbstractTask
    {
        public long DataID { get; set; }

        /// <summary>入队时的番号（执行时按 DataID 回读最新值，回读失败才用它兜底）</summary>
        private readonly string _Vid;

        /// <summary>入队时的影片路径（模式 0 的回读兜底）</summary>
        private readonly string _VideoPath;

        /// <summary>语言偏好（有序）</summary>
        private readonly string[] _Languages;

        /// <summary>保存模式：0=影片目录；1=指定文件夹</summary>
        private readonly int _SaveMode;

        /// <summary>模式 1 的目标文件夹</summary>
        private readonly string _Folder;

        /// <summary>入队序号（全局递增）：任务页按入队序显示，状态变化不重排（与演员任务同款）</summary>
        private static long _SeqCounter = 0;
        public long EnqueueSeq { get; private set; }

        public SubtitleTask(long dataID, string vid, string[] languages, int saveMode, string folder, string videoPath) : base()
        {
            EnqueueSeq = System.Threading.Interlocked.Increment(ref _SeqCounter);
            DataID = dataID;
            _Vid = vid;
            _Languages = languages != null && languages.Length > 0
                ? languages : new string[] { "zh-CN", "zh-TW" };
            _SaveMode = saveMode;
            _Folder = folder;
            _VideoPath = videoPath;
            Title = vid;
            StatusText = "等待下载字幕";
        }

        public override void DoWork()
        {
            Task.Run(async () => {
                try {
                    Progress = 0;
                    StartWatch();
                    Status = TaskStatus.Running;
                    StatusText = "正在读取影片信息...";
                    if (Token.IsCancellationRequested) {
                        FinalizeWithCancel();
                        return;
                    }

                    // 回读最新番号与路径（metadata 无 VID 列，必须 join metadata_video）
                    string vid = _Vid;
                    string videoPath = _VideoPath;
                    if (DataID > 0) {
                        try {
                            List<Dictionary<string, object>> rows = metaDataMapper.Select(
                                "select m.Path, v.VID from metadata m " +
                                "left join metadata_video v on v.DataID=m.DataID " +
                                $"where m.DataID={DataID}");
                            if (rows != null && rows.Count > 0) {
                                string freshVid = rows[0].TryGetValue("VID", out object v) ? v?.ToString() : null;
                                if (!string.IsNullOrWhiteSpace(freshVid))
                                    vid = freshVid.Trim();
                                string freshPath = rows[0].TryGetValue("Path", out object p) ? p?.ToString() : null;
                                if (!string.IsNullOrWhiteSpace(freshPath))
                                    videoPath = freshPath;
                            }
                        } catch (Exception ex) {
                            App.Logger.Error($"字幕任务回读影片信息失败：{ex.Message}");
                        }
                    }
                    if (string.IsNullOrWhiteSpace(vid)) {
                        StatusText = "影片无番号，跳过";
                        Logger.Info("影片无番号，无法查找字幕");
                        FinalizeWithCancel();
                        return;
                    }

                    // 模式 1 的目标文件夹（空回退用户数据目录 subtitles）
                    string folder = _Folder;
                    if (_SaveMode == 1 && string.IsNullOrWhiteSpace(folder))
                        folder = Path.Combine(Jvedio.Core.Global.PathManager.CurrentUserFolder, "subtitles");
                    string safeVid = string.Join("_", vid.Split(Path.GetInvalidFileNameChars(),
                        StringSplitOptions.RemoveEmptyEntries)).Trim();
                    if (_SaveMode == 1 && string.IsNullOrWhiteSpace(safeVid)) {
                        StatusText = "番号无法作为文件名，跳过";
                        FinalizeWithCancel();
                        return;
                    }

                    // 已有字幕不覆盖：模式 0 查同名 .srt；模式 1 查任一偏好语言的番号文件
                    if (_SaveMode == 0) {
                        if (!string.IsNullOrWhiteSpace(videoPath) && File.Exists(Path.ChangeExtension(videoPath, ".srt"))) {
                            CompleteSkip($"已存在：{Path.GetFileName(Path.ChangeExtension(videoPath, ".srt"))}");
                            return;
                        }
                    } else {
                        foreach (string lang in _Languages) {
                            string existing = Path.Combine(Path.GetFullPath(folder), $"{safeVid}.{lang}.srt");
                            if (File.Exists(existing)) {
                                CompleteSkip($"已存在：{Path.GetFileName(existing)}");
                                return;
                            }
                        }
                    }

                    // 站点结构化查询：搜索 → 候选 → 详情 → 轨道下载
                    Progress = 10;
                    StatusText = "正在搜索字幕...";
                    Logger.Info($"按番号 {vid} 搜索字幕（偏好语言：{string.Join("、", _Languages)}）");
                    List<SubtitleCandidate> candidates = await SubtitleCatProvider.SearchAsync(vid, Token);
                    if (candidates == null) {
                        StatusText = "SubtitleCat 暂不可用（站点改版或网络拦截）";
                        Logger.Info("SubtitleCat 搜索页不可用（无搜索表单），稍后重试");
                        App.Logger.Info($"字幕下载（{vid}）：SubtitleCat 暂不可用");
                        FinalizeWithCancel();
                        return;
                    }
                    if (candidates.Count == 0) {
                        string msg = "站内无该番号字幕";
                        StatusText = msg;
                        Logger.Info(msg);
                        App.Logger.Info($"字幕下载（{vid}）：{msg}");
                        FinalizeWithCancel();
                        return;
                    }
                    Logger.Info($"搜索到 {candidates.Count} 个候选，逐个查找偏好语言轨道");

                    int checkedCount = 0;
                    foreach (SubtitleCandidate candidate in candidates) {
                        if (Token.IsCancellationRequested || checkedCount >= 4)
                            break;
                        checkedCount++;
                        Progress = Math.Min(80, 10 + checkedCount * 15);
                        StatusText = $"正在查看候选 {checkedCount}/{Math.Min(4, candidates.Count)}：{candidate.Title}";
                        Dictionary<string, string> tracks = await SubtitleCatProvider.GetTracksAsync(candidate.DetailUrl, Token);
                        if (tracks == null)
                            continue; // 详情页失败（临时性），试下一个
                        // 番号一致性由搜索页的边界正则保证（标题允许带前缀/后缀，
                        // 如 "[SubtitleTools.com] IPZ-850"、"IPZ-850.zh"——整标题比对会误杀合法候选）
                        foreach (string lang in _Languages) {
                            if (!tracks.TryGetValue(lang, out string trackUrl))
                                continue;
                            StatusText = $"正在下载字幕（{lang}）：{candidate.Title}";
                            Progress = 85;
                            byte[] raw = await SubtitleCatProvider.DownloadTrackAsync(trackUrl, Token);
                            if (raw == null || !SrtNormalizer.TryNormalize(raw, out byte[] normalized)) {
                                Logger.Info($"候选「{candidate.Title}」的 {lang} 轨道下载或校验失败，换下一个候选");
                                break; // 该候选不可用，换下一个候选
                            }

                            // 落盘：模式 0=与视频同名；模式 1=番号.语言.srt
                            string savePath = _SaveMode == 0
                                ? Path.ChangeExtension(videoPath, ".srt")
                                : Path.Combine(Path.GetFullPath(folder), $"{safeVid}.{lang}.srt");
                            string dir = Path.GetDirectoryName(savePath);
                            if (string.IsNullOrEmpty(dir)) {
                                StatusText = "保存路径无效（影片无文件路径？），改用指定文件夹模式可解";
                                Logger.Info(StatusText);
                                FinalizeWithCancel();
                                return;
                            }
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);
                            File.WriteAllBytes(savePath, normalized);
                            Success = true;
                            Status = TaskStatus.RanToCompletion;
                            StatusText = $"已保存：{Path.GetFileName(savePath)}（{lang}）";
                            Progress = 100;
                            Logger.Info($"字幕已保存：{savePath}（来源 {candidate.Title}）");
                            App.Logger.Info($"字幕已保存：{vid} → {savePath}（{lang}，来源 {candidate.Title}）");
                            MarkSubtitleExist(_SaveMode, savePath);
                            OnCompleted(null);
                            return;
                        }
                    }

                    if (Token.IsCancellationRequested) {
                        FinalizeWithCancel();
                        return;
                    }
                    string missMsg = $"无可用字幕（偏好语言：{string.Join("、", _Languages)}）";
                    StatusText = missMsg;
                    Logger.Info(missMsg);
                    App.Logger.Info($"字幕下载（{vid}）：{missMsg}");
                    FinalizeWithCancel();
                    return; // FinalizeWithCancel 已触发 onCanceled，再落 OnCompleted 会双扣 RunningCount
                } catch (Exception ex) {
                    Message = ex.Message;
                    App.Logger.Error($"字幕下载失败（{_Vid}）：{ex.Message}");
                    try {
                        Logger.Error($"字幕下载异常：{ex.Message}");
                    } catch { }
                    FinalizeWithCancel();
                    StatusText = "字幕下载失败";
                    return; // 同上，防双扣
                }
            });
        }

        /// <summary>「已有字幕，跳过」终点：成功态 + 必须补 onCompleted（早退 return 不会走到方法尾）</summary>
        private void CompleteSkip(string text)
        {
            Success = true;
            Status = TaskStatus.RanToCompletion;
            StatusText = text;
            Progress = 100;
            Logger.Info(text);
            OnCompleted(null);
        }

        /// <summary>影片目录模式保存后刷新存在性索引，筛选「有字幕」立即生效</summary>
        private void MarkSubtitleExist(int saveMode, string savePath)
        {
            try {
                if (saveMode == 0 && DataID > 0)
                    videoMapper.ExecuteNonQuery($"update metadata set SubtitleExist=1 where DataID={DataID}");
            } catch (Exception ex) {
                App.Logger.Error(ex.Message);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is SubtitleTask other)
                return other.DataID == DataID;
            return false;
        }

        public override int GetHashCode()
        {
            return DataID.GetHashCode();
        }
    }
}
