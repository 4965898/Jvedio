using Jvedio.Core.Enums;
using Jvedio.Core.Crawler;
using Jvedio.Windows;
using Jvedio.Core.Exceptions;
using Jvedio.Core.Utils;
using Jvedio.Entity;
using SuperControls.Style;
using SuperUtils.Common;
using SuperUtils.CustomEventArgs;
using SuperUtils.Framework.ORM.Enums;
using SuperUtils.Framework.ORM.Wrapper;
using SuperUtils.Framework.Tasks;
using SuperUtils.IO;
using SuperUtils.NetWork.Entity;
using SuperUtils.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using static Jvedio.MapperManager;

namespace Jvedio.Core.Net
{
    public sealed class CapturedMediaOptions
    {
        public bool CoverImages { get; set; }
        public bool PreviewImages { get; set; }
        public bool PreviewVideos { get; set; }
        public string Referer { get; set; }
    }
    // todo 检视
    public class DownLoadTask : AbstractTask
    {
        private const String WRONG_STATUS_CODE = "-1";

        #region "事件"
        public static Action<DownLoadTask> onDownloadSuccess;
        public static Action<long, string, byte[]> onDownloadPreview;

        #endregion

        #region "属性"

        public bool DownloadPreview { get; set; } // 是否下载预览图
        public long DataID { get; set; }

        public DataType DataType { get; set; }

        public bool OverrideInfo { get; set; }// 强制下载覆盖信息
        public bool PreviewInfo { get; set; }
        public CapturedMediaOptions CapturedMedia { get; set; }
        private HashSet<string> _AllowedMetadataFields;

        #endregion


        private static class Delay
        {
            public static int INFO = 3000;
            public static int EXTRA_IMAGE = 500;
            public static int BIG_IMAGE = 50;
            public static int SMALL_IMAGE = 50;
        }

        public DownLoadTask(Video video, bool downloadPreview = false, bool overrideInfo = false) :
            this(video.toMetaData())
        {
            Title = string.IsNullOrEmpty(video.VID) ? video.Title : video.VID;
            DownloadPreview = downloadPreview;
            OverrideInfo = overrideInfo;
        }

        static DownLoadTask()
        {
            STATUS_TO_TEXT_DICT[TaskStatus.Running] = $"{LangManager.GetValueByKey("Downloading")}...";
        }

        public DownLoadTask(MetaData data) : base()
        {
            DataID = data.DataID;
            DataType = data.DataType;
        }

        static Dictionary<int, string> STATUS_DICT { get; set; } = new Dictionary<int, string>()
        {
            {200,"成功获取资源" },
            {301,"被永久重定向（301）：该网址已失效，请在「选项 → 同步信息」更换服务器网址" },
            {302,"被重定向（302）：站点认为请求过于频繁或需要登录，请降低并发、稍后重试，或在 Headers 中填入登录 Cookie" },
            {401,"站点要求登录（401）：请在「选项 → 同步信息」对应服务器的 Headers 中粘贴登录后的浏览器请求头（含 cookie）" },
            {403,"远程服务器拒绝了您的访问（IP 被限制或站点启用 Cloudflare 人机验证，请更新 Cookie/UA 或更换代理）" },
            {404,"远程服务器无该资源：番号可能不存在或已下架，也可能是网址已更换" },
            {500,"远程服务器错误" },
        };

        public static string StatusCodeToMessage(int status)
        {
            if (STATUS_DICT.ContainsKey(status)) {
                return STATUS_DICT[status];
            } else {
                return status.ToString();
            }
        }

        public static string StatusCodeToMessage(string status)
        {
            if (int.TryParse(status, out int _temp))
                return StatusCodeToMessage(_temp);
            return "";
        }

        /// <summary>
        /// 把爬虫返回的原始错误翻译成「哪一环出错 + 怎么办」的中文提示。
        /// 返回空串表示该错误不构成失败（空值/默认占位）。
        /// </summary>
        public static string NormalizeCrawlerError(string error)
        {
            if (string.IsNullOrEmpty(error) || error.Equals(HttpResult.DEFAULT_ERROR_MSG))
                return "";
            string text = error.Trim();
            switch (text) {
                case "NotFound":
                    return "站点返回 404：未找到该番号（番号可能不存在、已下架，或网址已失效）";
                case "Forbidden":
                    return StatusCodeToMessage(403);
                case "Unauthorized":
                    return StatusCodeToMessage(401);
                case "no VID":
                case "必须指定 VID":
                case "传入的信息不合理":
                    return "缺少识别码（VID）等必要信息，无法刮削";
                case "failed":
                    return "站点返回了空页面（可能被拦截或网址已失效）";
                case "检索太频繁":
                    return StatusCodeToMessage(302);
                case "開通VIP":
                    return "该影片需要 JavDB VIP 账户才能查看，请换用其他源或跳过";
                case "此內容需要登入才能查看或操作":
                    return "需要登录：先在浏览器登录 JavDB，再在「选项 → 同步信息」该服务器的 Headers 中粘贴登录后的浏览器请求头（务必包含 cookie）";
            }
            string lower = text.ToLowerInvariant();
            if (lower.Contains("timeout") || lower.Contains("timed out") || text.Contains("超时"))
                return "网络请求超时：检查网址是否有效、代理与网络是否可用，或稍后重试（" + text + "）";
            if (lower.Contains("unable to connect") || lower.Contains("connection") || lower.Contains("name resolution")
                || lower.Contains("no such host") || lower.Contains("socket") || lower.Contains("ssl"))
                return "无法连接站点：检查网址是否有效、网络与代理是否可用（" + text + "）";
            return text;
        }

        /// <summary>在错误信息前标注出错的刮削器与服务器，让用户知道是哪一个源出错。</summary>
        public static string PrefixCrawlerSource(Dictionary<string, object> dict, string message)
        {
            if (dict == null || string.IsNullOrEmpty(message))
                return message;
            string pluginID = dict.TryGetValue("PluginID", out object p) ? p?.ToString() : "";
            string url = dict.TryGetValue("ServerUrl", out object u) ? u?.ToString() : "";
            if (string.IsNullOrEmpty(pluginID))
                return message;
            string host = url;
            try { if (!string.IsNullOrEmpty(url)) host = new Uri(url).Host; } catch { }
            return string.IsNullOrEmpty(host) ? $"[{pluginID}] {message}" : $"[{pluginID} · {host}] {message}";
        }

        public async Task<Dictionary<string, object>> GetDataInfo(Video video, VideoDownLoader downLoader, RequestHeader header, Action<RequestHeader> headerCallBack)
        {
            Dictionary<string, object> dict = null;
            if (video == null || video.DataID <= 0) {
                Message = $"不存在 DataID={DataID} 的资源";
                Logger.Error(Message);
                FinalizeWithCancel();
                throw new Exception(Message);
            }
            if (CapturedMedia != null) return null;

            // 判断是否需要下载，自动跳过已下载的信息
            if (OverrideInfo || video.ToDownload()) {
                if (!string.IsNullOrEmpty(video.VID)) {
                    // 有 VID 的
                    try {
                        dict = await downLoader.GetInfo((h) => { headerCallBack?.Invoke(h); });
                    } catch (CrawlerNotFoundException ex) {
                        // todo 显示到界面上
                        Message = ex.Message;
                        Logger.Error(Message);
                        FinalizeWithCancel();
                        throw ex;
                    } catch (DllLoadFailedException ex) {
                        Message = ex.Message;
                        Logger.Error(Message);
                        FinalizeWithCancel();
                        throw ex;
                    }
                } else {
                    // 无 VID 的
                    throw new Exception("同步信息需要 VID");
                }

                // 等待了很久都没成功
                Logger.Info($"暂停 {DateHelper.ToReadableTime(Delay.INFO)}");
                await Task.Delay(Delay.INFO);
                return dict;
            } else {
                Message = "该资源信息已同步，跳过信息下载";
                Logger.Info(Message);
                Logger.Info(LangManager.GetValueByKey("SkipDownLoadInfoAndDownloadImage"));
                return null;
            }
        }

        public async Task<bool> DownloadPoster(Video video, Dictionary<string, object> dict, VideoDownLoader downLoader, RequestHeader header)
        {
            if (!(CapturedMedia?.CoverImages ?? ConfigManager.DownloadConfig.DownloadPoster))
                return true;
            object o = GetInfoFromExist("BigImageUrl", video, dict);
            string imageUrl = o != null ? o.ToString() : string.Empty;
            if (!string.IsNullOrEmpty(imageUrl)) {
                Logger.Info($"url: {imageUrl}");
                string saveFileName = video.GetBigImage(Path.GetExtension(imageUrl), false);
                if (!File.Exists(saveFileName)) {
                    byte[] fileByte = await downLoader.DownloadImage(imageUrl, header, (error) => {
                        if (!string.IsNullOrEmpty(error))
                            Logger.Error($"{imageUrl} => {error}");
                    });
                    if (fileByte == null || fileByte.Length == 0) {
                        Logger.Warning($"海报图下载失败，正在重试...");
                        await Task.Delay(1000);
                        fileByte = await downLoader.DownloadImage(imageUrl, header, (error) => {
                            if (!string.IsNullOrEmpty(error))
                                Logger.Error($"重试: {imageUrl} => {error}");
                        });
                    }
                    if (fileByte != null && fileByte.Length > 0) {
                        FileHelper.ByteArrayToFile(fileByte, saveFileName);
                        StatusText = "3.1 同步海报图成功";
                        return true;
                    } else
                        Logger.Error($"同步失败，文件大小为空");
                    await Task.Delay(Delay.BIG_IMAGE);
                } else {
                    Logger.Info($"{LangManager.GetValueByKey("SkipDownloadImage")} {saveFileName}");
                }
            } else {
                Logger.Error("图片地址为空");
            }
            return false;
        }

        public async Task<bool> DownloadThumbnail(Video video, Dictionary<string, object> dict, VideoDownLoader downLoader, RequestHeader header)
        {
            if (!(CapturedMedia?.CoverImages ?? ConfigManager.DownloadConfig.DownloadThumbNail))
                return true;
            object o = GetInfoFromExist("SmallImageUrl", video, dict);
            string imageUrl = o != null ? o.ToString() : string.Empty;

            if (!string.IsNullOrEmpty(imageUrl)) {
                Logger.Info($"url: {imageUrl}");
                string saveFileName = video.GetSmallImage(Path.GetExtension(imageUrl), false);
                if (!File.Exists(saveFileName)) {
                    byte[] fileByte = await downLoader.DownloadImage(imageUrl, header, (error) => {
                        if (!string.IsNullOrEmpty(error))
                            Logger.Error($"{imageUrl} => {error}");
                    });
                    if (fileByte == null || fileByte.Length == 0) {
                        Logger.Warning($"缩略图下载失败，正在重试...");
                        await Task.Delay(1000);
                        fileByte = await downLoader.DownloadImage(imageUrl, header, (error) => {
                            if (!string.IsNullOrEmpty(error))
                                Logger.Error($"重试: {imageUrl} => {error}");
                        });
                    }
                    if (fileByte == null || fileByte.Length == 0) {
                        object posterUrlObj = GetInfoFromExist("BigImageUrl", video, dict);
                        if (posterUrlObj != null) {
                            string posterUrl = posterUrlObj.ToString();
                            if (posterUrl.EndsWith("_b.jpg", StringComparison.OrdinalIgnoreCase)) {
                                string fallbackUrl = posterUrl.Replace("_b.jpg", "_s.jpg");
                                if (!string.Equals(fallbackUrl, imageUrl, StringComparison.OrdinalIgnoreCase)) {
                                    Logger.Info($"尝试备用缩略图URL: {fallbackUrl}");
                                    fileByte = await downLoader.DownloadImage(fallbackUrl, header, (error) => {
                                        if (!string.IsNullOrEmpty(error))
                                            Logger.Error($"{fallbackUrl} => {error}");
                                    });
                                }
                            }
                        }
                    }
                    if (fileByte != null && fileByte.Length > 0) {
                        FileHelper.ByteArrayToFile(fileByte, saveFileName);
                        StatusText = "4.1 同步缩略图成功";
                        return true;
                    } else
                        Logger.Error($"sync thumbnail failed, file byte is empty");
                    await Task.Delay(Delay.SMALL_IMAGE);
                } else {
                    Logger.Info($"{LangManager.GetValueByKey("SkipDownloadImage")} {saveFileName}");
                }
            } else {
                Logger.Error("sync thumbnail image url is empty");
            }
            return false;
        }

        public void SaveActorNames(object names, Video video, object enNames = null)
        {
            if (names is List<string> actorNames && actorNames.Count > 0) {
                int actorCount = actorNames.Count;
                for (int i = 0; i < actorCount; i++) {
                    string actorName = actorNames[i];
                    ActorInfo actorInfo =
                        actorMapper.SelectOne(new SelectWrapper<ActorInfo>().Eq("ActorName", actorName));
                    if (actorInfo == null || actorInfo.ActorID <= 0) {
                        actorInfo = new ActorInfo();
                        actorInfo.ActorName = actorName;
                        SetActorNameEN(actorInfo, enNames, i);
                        actorMapper.Insert(actorInfo);
                    } else {
                        SetActorNameEN(actorInfo, enNames, i);
                    }
                    // 保存信息
                    string sql = $"insert or ignore into metadata_to_actor (ActorID,DataID) values ({actorInfo.ActorID},{video.DataID})";
                    metaDataMapper.ExecuteNonQuery(sql);
                    StatusText = $"{i + 1}/{actorCount} 成功保存演员信息：{actorName}";
                }
            }
        }

        /// <summary>
        /// 从刮削返回的罗马字 slug 列表（与 ActorNames 一一对应）填充 ActorNameEN；已存在则不覆盖
        /// </summary>
        private void SetActorNameEN(ActorInfo actorInfo, object enNames, int index)
        {
            if (enNames is List<string> enList && index < enList.Count && !string.IsNullOrEmpty(enList[index])) {
                string en = RomajiConverter.SlugToDisplay(enList[index]);
                if (string.IsNullOrEmpty(actorInfo.ActorNameEN) && !string.IsNullOrEmpty(en)) {
                    actorInfo.ActorNameEN = en;
                    if (actorInfo.ActorID > 0)
                        actorMapper.UpdateFieldById("ActorNameEN", en, actorInfo.ActorID);
                }
            }
        }

        public async Task<bool> DownloadActors(Video video, Dictionary<string, object> dict, VideoDownLoader downLoader, RequestHeader header)
        {
            if (CapturedMedia != null) return true;
            if (_AllowedMetadataFields != null && !_AllowedMetadataFields.Contains("ActorNames"))
                return true;
            object names = GetInfoFromExist("ActorNames", video, dict);
            object urls = GetInfoFromExist("ActressImageUrl", video, dict);
            object enNames = GetInfoFromExist("ActorNameEN", video, dict);

            if (names == null)
                return false;

            bool downloadActorImage = ConfigManager.DownloadConfig.DownloadActor;

            // 部分站点的演员头像图床有防盗链/风控，带上刮削源根域名作为 Referer 提高成功率
            if (header != null && dict != null && dict.ContainsKey("Url") && dict["Url"] != null) {
                string referer = dict["Url"].ToString();
                if (!string.IsNullOrEmpty(referer)) {
                    if (header.Headers == null)
                        header.Headers = new Dictionary<string, string>();
                    if (!header.Headers.ContainsKey("referer"))
                        header.Headers["referer"] = referer;
                }
            }

            if (urls == null || !downloadActorImage) {
                SaveActorNames(names, video, enNames);
                return true;
            }

            if (names != null &&
                urls != null &&
                names is List<string> actorNames &&
                urls is List<string> ActressImageUrl) {
                if (actorNames != null &&
                    ActressImageUrl != null &&
                    actorNames.Count == ActressImageUrl.Count) {
                    int actorCount = actorNames.Count;
                    for (int i = 0; i < actorCount; i++) {
                        string actorName = actorNames[i];
                        string url = ActressImageUrl[i];
                        Logger.Info($"{actorName}: {url}");
                        ActorInfo actorInfo = actorMapper.SelectOne(new SelectWrapper<ActorInfo>().Eq("ActorName", actorName));
                        if (actorInfo == null || actorInfo.ActorID <= 0) {
                            actorInfo = new ActorInfo();
                            actorInfo.ActorName = actorName;
                            actorInfo.ImageUrl = url;
                            SetActorNameEN(actorInfo, enNames, i);
                            actorMapper.Insert(actorInfo);
                        } else {
                            SetActorNameEN(actorInfo, enNames, i);
                        }

                        string sql = $"insert or ignore into metadata_to_actor (ActorID,DataID) values ({actorInfo.ActorID},{video.DataID})";
                        metaDataMapper.ExecuteNonQuery(sql);
                        StatusText = $"{i + 1}/{actorCount} 成功保存演员信息: {actorName}";

                        if (downloadActorImage) {
                            string saveFileName = actorInfo.GetImagePath(video.Path, Path.GetExtension(url), false);
                            if (!File.Exists(saveFileName)) {
                                byte[] fileByte = await downLoader.DownloadImage(url, header, (error) => {
                                    if (!string.IsNullOrEmpty(error))
                                        Logger.Error($"{url} => {error}");
                                });
                                // 头像图床多有风控（403），延迟重试一次
                                if (fileByte == null || fileByte.Length == 0) {
                                    Logger.Warning($"演员头像下载失败，正在重试... {url}");
                                    await Task.Delay(1000);
                                    fileByte = await downLoader.DownloadImage(url, header, (error) => {
                                        if (!string.IsNullOrEmpty(error))
                                            Logger.Error($"重试: {url} => {error}");
                                    });
                                }
                                if (fileByte != null && fileByte.Length > 0) {
                                    FileHelper.ByteArrayToFile(fileByte, saveFileName);
                                    StatusText = $"{i + 1}/{actorCount} 成功同步演员头像: {actorName}";
                                } else
                                    Logger.Error($"{i + 1}/{actorCount} sync actor（{actorName}）image failed, file byte is empty");
                            } else {
                                Logger.Info($"{LangManager.GetValueByKey("SkipDownloadImage")} {saveFileName}");
                            }
                        }
                    }
                    return true;
                } else {
                    Logger.Warning($"actorNames.Count({actorNames?.Count}) != ActressImageUrl.Count({ActressImageUrl?.Count}), fallback to SaveActorNames");
                    SaveActorNames(names, video, enNames);
                    return true;
                }
            } else {
                Logger.Error("empty: names,  urls");
            }
            return false;
        }

        public async Task<bool> DownloadPreviews(Video video, Dictionary<string, object> dict, VideoDownLoader downLoader, RequestHeader header)
        {
            if (!(CapturedMedia?.PreviewImages ?? ConfigManager.DownloadConfig.DownloadPreviewImage))
                return true;
            object urls = GetInfoFromExist("ExtraImageUrl", video, dict);
            if ((CapturedMedia?.PreviewImages ?? DownloadPreview) &&
                urls != null &&
                urls is List<string> imageUrls) {
                if (imageUrls != null && imageUrls.Count > 0) {
                    int imageCount = imageUrls.Count;
                    for (int i = 0; i < imageCount; i++) {
                        if (Canceled) {
                            FinalizeWithCancel();
                            return false;
                        }
                        string url = imageUrls[i];

                        // 下载图片
                        string saveDir = video.GetExtraImage();
                        DirHelper.TryCreateDirectory(saveDir);

                        string saveFileName = Path.Combine(saveDir, Path.GetFileName(url));
                        if (!File.Exists(saveFileName)) {
                            StatusText = $"{LangManager.GetValueByKey("Preview")} {i + 1}/{imageCount}";
                            byte[] fileByte = await downLoader.DownloadImage(url, header, (error) => {
                                if (!string.IsNullOrEmpty(error))
                                    Logger.Error($"{url} => {error}");
                            });
                            if (fileByte != null && fileByte.Length > 0) {
                                FileHelper.ByteArrayToFile(fileByte, saveFileName);
                                onDownloadPreview?.Invoke(DataID, saveFileName, fileByte);
                                StatusText = $"{i + 1}/{imageCount} 同步预览图成功";
                            } else {
                                Logger.Error($"{i + 1}/{imageCount} 下载图片失败，文件为空");
                            }

                            await Task.Delay(Delay.EXTRA_IMAGE);
                        } else {
                            Logger.Info($"{LangManager.GetValueByKey("SkipDownloadImage")} {saveFileName}");
                        }
                    }
                    return true;
                } else {
                    Logger.Warning("远程服务器无预览图");
                }
            } else if (!DownloadPreview) {
                StatusText = LangManager.GetValueByKey("NotSetPreviewDownload");
                Logger.Warning(LangManager.GetValueByKey("NotSetPreviewDownload"));
            } else {
                Logger.Warning("解析远程预览图失败");
            }
            return false;
        }

        public async Task<bool> CheckDataInfo(Video video, Dictionary<string, object> dict, VideoDownLoader downLoader, RequestHeader header)
        {
            if (CapturedMedia != null) return true;
            // 只有同步了信息才需要校验信息
            if (!(video.ToDownload() || OverrideInfo)) {
                return true;
            }
            bool success = false;
            if (dict != null && dict.ContainsKey("Error")) {
                string statusCode = dict.Get("StatusCode", WRONG_STATUS_CODE).ToString();
                Logger.Info($"响应码: {statusCode} ({StatusCodeToMessage(statusCode)})");
                string error = dict["Error"].ToString();
                string allSources = dict.TryGetValue("ErrorAll", out object allObj) ? allObj?.ToString() : "";
                string normalized = NormalizeCrawlerError(error);
                if (!string.IsNullOrEmpty(allSources)) {
                    // 多个源都失败：直接展示逐源汇总
                    Message = allSources;
                    Logger.Error(Message);
                } else if (!string.IsNullOrEmpty(normalized)) {
                    Message = PrefixCrawlerSource(dict, normalized);
                    Logger.Error(Message);
                }
                success = dict.ContainsKey("Title") && !string.IsNullOrEmpty(dict["Title"].ToString());
            }
            if (!success) {
                if (string.IsNullOrEmpty(Message)) {
                    // 区分「拿到了页面但没解析出信息」与「按状态码失败」两种情况
                    string statusCode = dict != null ? dict.Get("StatusCode", WRONG_STATUS_CODE).ToString() : WRONG_STATUS_CODE;
                    if (int.TryParse(statusCode, out int status)) {
                        Message = status == 200
                            ? "页面响应 200 但未解析出影片信息：可能被人机验证/防爬页拦截、需要登录或站点改版，请更新 Headers（Cookie/UA）或更换网址"
                            : StatusCodeToMessage(status);
                    } else {
                        Message = "未获得影片信息（" + statusCode + "）：请检查服务器网址与 Headers 配置";
                    }
                }
                if (int.TryParse(Message, out int status2)) {
                    Message = StatusCodeToMessage(status2);
                }
                Logger.Warning(Message);

                await Task.Delay(Delay.INFO);
                // 发生了错误，停止下载
                FinalizeWithCancel();
                // 但是已经请求了网址，所以视为完成，并加入到长时间等待队列
                Status = TaskStatus.Canceled;
                return false;
            } else {
                StatusText = "2.1 同步信息成功";
            }

            bool protect = ConfigManager.Settings.ProtectExistingScrapeFields;
            HashSet<string> selected = new HashSet<string>(ScrapeFieldPolicy.Available(dict)
                .Where(field => !protect || !ScrapeFieldPolicy.HasExistingValue(video, field)));
            if (PreviewInfo) {
                Func<HashSet<string>> showPreview = () => {
                    var window = new Window_ScrapePreview(video, dict, protect) {
                        Owner = Application.Current.MainWindow
                    };
                    return window.ShowDialog() == true ? window.SelectedFields : null;
                };
                selected = Application.Current.Dispatcher.Invoke(showPreview);
                if (selected == null) {
                    Message = "用户取消刮削结果预览";
                    FinalizeWithCancel();
                    return false;
                }
            }
            _AllowedMetadataFields = selected;
            Dictionary<string, object> merge = ScrapeFieldPolicy.MergeDictionary(dict, selected);
            if (merge.Count == 0) return true;
            bool downloadInfo = video.ParseDictInfo(merge); // 只写入用户选中的字段，保留本地识别码和路径
            if (downloadInfo) {
                StatusText = LangManager.GetValueByKey("SaveToLibrary");
                // 并发锁
                videoMapper.UpdateById(video);
                metaDataMapper.UpdateById(video.toMetaData());

                // 保存 dataCode
                if (dict.ContainsKey("DataCode") && dict.ContainsKey("WebType")) {
                    UrlCode urlCode = new UrlCode();
                    urlCode.LocalValue = video.VID;
                    urlCode.RemoteValue = dict["DataCode"].ToString();
                    urlCode.ValueType = "video";
                    urlCode.WebType = dict["WebType"].ToString();
                    urlCodeMapper.Insert(urlCode, InsertMode.Replace);
                    StatusText = "2.2 成功保存 DataCode";
                }

                // 保存 nfo
                video.SaveNfo();
                if (ConfigManager.Settings.SaveInfoToNFO)
                    StatusText = "2.3 成功保存 NFO";
                onDownloadSuccess?.Invoke(this);
                return true;
            } else {
                return false;
            }
        }

        public override void DoWork()
        {
            Task.Run(async () => {
                Progress = 0;
                TimeWatch.Start();
                if (DataType == DataType.Video) {
                    Video video = videoMapper.SelectVideoByID(DataID);
                    if (video == null) {
                        Message = $"不存在 DataID={DataID} 的资源";
                        Logger.Error(Message);
                        FinalizeWithCancel();
                        return;
                    }
                    RequestHeader header = null;
                    Dictionary<string, object> dict = null;
                    VideoDownLoader downLoader = new VideoDownLoader(video, Token, Logger);
                    StatusText = $"1. 开始同步信息: {video.VID}";
                    try {
                        //dict = new Dictionary<string, object>();
                        dict = await GetDataInfo(video, downLoader, header, (h) => { header = h; });
                    } catch (Exception ex) {
                        // 记录完整堆栈，便于定位「集合已修改」这类并发异常的准确抛出点
                        Logger.Error(ex.ToString());
                        FinalizeWithCancel();
                        return;
                    }
                    if (dict != null && dict.ContainsKey("PluginID") && dict["PluginID"] is string PluginID)
                        Logger?.Info($"使用刮削器：{PluginID}");

                    bool success = true;
                    Progress = 10f;
                    StatusText = "2. 校验信息";
                    success = await CheckDataInfo(video, dict, downLoader, header);
                    //success = true;
                    if (!success) {
                        dict = null;
                        StatusText = "2.1. 校验信息不通过";
                        FinalizeWithCancel();
                        return;
                    }

                    if (Canceled) {
                        FinalizeWithCancel();
                        return;
                    }

                    // 可能刮削了信息，但是没刮削图片
                    if (header == null) {
                        header = new RequestHeader();
                        header.WebProxy = ConfigManager.ProxyConfig.GetWebProxy();
                    }
                    header.TimeOut = ConfigManager.ProxyConfig.HttpTimeout * 1000;
                    if (CapturedMedia != null && !string.IsNullOrEmpty(CapturedMedia.Referer)) {
                        if (header.Headers == null) header.Headers = new Dictionary<string, string>();
                        header.Headers["Referer"] = CapturedMedia.Referer;
                    }
                    Message = "";
                    StatusText = "3. 开始同步海报图";
                    success = await DownloadPoster(video, dict, downLoader, header);
                    Progress = 66f;
                    if (Canceled) {
                        FinalizeWithCancel();
                        return;
                    }
                    StatusText = "4. 开始同步缩略图";
                    success = await DownloadThumbnail(video, dict, downLoader, header);
                    Progress = 77f;
                    if (Canceled) {
                        FinalizeWithCancel();
                        return;
                    }

                    onDownloadSuccess?.Invoke(this);
                    StatusText = "5. 开始同步演员头像";
                    success = await DownloadActors(video, dict, downLoader, header);

                    Progress = 88f;
                    if (Canceled) {
                        FinalizeWithCancel();
                        return;
                    }

                    StatusText = "6. 开始同步预览图";
                    success = await DownloadPreviews(video, dict, downLoader, header);
                    if (CapturedMedia?.PreviewVideos == true) {
                        StatusText = LangManager.GetValueByKey("ClipperDownloadingVideo");
                        if (GetInfoFromExist("PreviewVideoUrl", video, dict) is List<string> videoUrls) {
                            for (int i = 0; i < videoUrls.Count; i++) {
                                string extension = Path.GetExtension(new Uri(videoUrls[i]).AbsolutePath).ToLowerInvariant();
                                if (extension != ".webm" && extension != ".m4v") extension = ".mp4";
                                string directory = video.GetExtraImage();
                                Directory.CreateDirectory(directory);
                                string target = Path.Combine(directory, "preview-video-" + (i + 1) + extension);
                                bool downloaded = await downLoader.DownloadPreviewVideo(videoUrls[i], target, header, error => Message = error);
                                if (!downloaded) { FinalizeWithCancel(); return; }
                            }
                        }
                    }
                    Status = TaskStatus.RanToCompletion;
                }
                StatusText = "7. 同步所有内容完成";
                Running = false;
                Progress = 100.00f;
                TimeWatch.Stop();
                ElapsedMilliseconds = TimeWatch.ElapsedMilliseconds;
                Logger.Info($"{LangManager.GetValueByKey("TotalCost")} {DateHelper.ToReadableTime(ElapsedMilliseconds)}");

                // 任务完成后暂停 3s 随机
                await Task.Delay(new Random().Next(0, 3000));

                OnCompleted(null);
            });
        }

        private object GetInfoFromExist(string type, Video video, Dictionary<string, object> dict)
        {
            if (dict != null && dict.Count > 0) {
                if (dict.ContainsKey(type)) {
                    if (dict[type].GetType() == typeof(Newtonsoft.Json.Linq.JArray)) {
                        Newtonsoft.Json.Linq.JArray jArray = Newtonsoft.Json.Linq.JArray.Parse(dict[type].ToString());
                        return jArray.Select(x => x.ToString()).ToList();
                    }

                    return dict[type];
                }

                return null;
            } else if (video != null) {
                string imageUrls = video.ImageUrls;
                if (!string.IsNullOrEmpty(imageUrls)) {
                    Dictionary<string, object> dic = JsonUtils.TryDeserializeObject<Dictionary<string, object>>(imageUrls);
                    if (dic == null)
                        return null;
                    return GetInfoFromExist(type, null, dic); // 递归调用
                }
            }

            return null;
        }

        #region "对外静态方法"
        public static void DownloadVideo(Video video, bool previewInfo = false)
        {
            DownLoadTask downloadTask = new DownLoadTask(video, ConfigManager.DownloadConfig.DownloadPreviewImage,
                previewInfo || ConfigManager.DownloadConfig.OverrideInfo) { PreviewInfo = previewInfo };

            if (App.DownloadManager.Exists(downloadTask)) {
                MessageNotify.Warning(LangManager.GetValueByKey("TaskExists"));
                return;
            }
            downloadTask.onError += (s, ev) => MessageCard.Error((ev as MessageCallBackEventArgs).Message);
            App.DownloadManager.AddTask(downloadTask);
        }

        #endregion



        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj is DownLoadTask other)
                return other.DataID.Equals(DataID);
            return false;
        }

        public override int GetHashCode()
        {
            return DataID.GetHashCode();
        }
    }
}
