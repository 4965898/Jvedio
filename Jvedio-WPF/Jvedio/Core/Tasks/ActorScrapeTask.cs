using Jvedio.Core.Crawler;
using Jvedio.Core.Plugins.Crawler;
using Jvedio.Entity;
using SuperControls.Style;
using SuperControls.Style.Plugin;
using SuperUtils.Framework.Tasks;
using SuperUtils.NetWork.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static Jvedio.MapperManager;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// 单个演员的资料补齐任务：调用演员资料插件（MinnanoAV）按名字查询，
    /// 只填充空字段（身高/三围/罩杯/生日/罗马字），不覆盖手填资料；
    /// 成功写 WebUrl/WebType 作为「已补齐」标记，确认查无此人则记入 ExtraInfo
    /// （minnanoav:notfound@日期）防止每轮重复请求。
    /// </summary>
    public class ActorScrapeTask : AbstractTask
    {
        public long ActorID { get; set; }

        private readonly string _ActorName;

        /// <summary>入队序号（全局递增）：任务页显示按此从旧到新排序，状态变化不重排</summary>
        private static long _SeqCounter = 0;
        public long EnqueueSeq { get; private set; }

        public ActorScrapeTask(ActorInfo actorInfo) : base()
        {
            EnqueueSeq = System.Threading.Interlocked.Increment(ref _SeqCounter);
            ActorID = actorInfo.ActorID;
            _ActorName = actorInfo.ActorName;
            Title = actorInfo.ActorName;
            StatusText = "等待补齐演员资料";
        }

        public override void DoWork()
        {
            Task.Run(async () => {
                try {
                    Progress = 0;
                    StartWatch();
                    Status = TaskStatus.Running;
                    StatusText = "正在补齐演员资料...";
                    if (Token.IsCancellationRequested) {
                        FinalizeWithCancel();
                        StatusText = LangManager.GetValueByKey("Cancel");
                        return;
                    }

                    ActorInfo actorInfo = ActorInfo.GetById(ActorID);
                    if (actorInfo == null || string.IsNullOrEmpty(actorInfo.ActorName)) {
                        FinalizeWithCancel();
                        return;
                    }

                    (PluginMetaData metaData, CrawlerServer server) = FindActorPlugin();
                    if (metaData == null || server == null) {
                        Message = "未找到可用的演员资料源：请在「选项 → 同步信息」启用 JAVDatabase 或 MinnanoAV 源";
                        App.Logger.Error(Message);
                        FinalizeWithCancel();
                        StatusText = Message;
                        return;
                    }

                    RequestHeader header = CrawlerServer.ParseHeader(server);
                    Dictionary<string, object> dataInfo = new Dictionary<string, object>();
                    dataInfo["ActorName"] = actorInfo.ActorName;
                    dataInfo["ActorNameEN"] = actorInfo.ActorNameEN ?? "";
                    dataInfo["Vid"] = GetFirstLinkedVid(actorInfo.ActorID);
                    dataInfo["Vids"] = GetLinkedVids(actorInfo.ActorID, 10);
                    dataInfo["Url"] = server.Url;

                    PluginInvoker invoker = new PluginInvoker(metaData.GetDllPath());
                    object result = await invoker.SetMethod("GetActorInfo").InvokeAsync(new object[] { header, dataInfo });
                    if (!(result is Dictionary<string, object> dict)) {
                        Message = "演员资料插件调用失败";
                        App.Logger.Error(Message);
                        FinalizeWithCancel();
                        StatusText = Message;
                        return;
                    }
                    if (dict.TryGetValue("Logs", out object logsObj) && logsObj is List<string> logs) {
                        foreach (string log in logs)
                            App.Logger.Info($"[{metaData.PluginID}] {log}");
                    }

                    string error = dict.TryGetValue("Error", out object errObj) ? errObj?.ToString() : "";
                    if (!string.IsNullOrEmpty(error)) {
                        Message = error;
                        // 只有「搜索结果中无精确同名」才算确定的查无此人（记入 ExtraInfo
                        // 防止下一轮重复请求）；网络失败/Cloudflare 拦截不记录，下次可重试
                        if (error.Contains("无精确同名"))
                            MarkAttempted(actorInfo);
                        App.Logger.Info($"演员资料补齐（{actorInfo.ActorName}）：{error}");
                        FinalizeWithCancel();
                        StatusText = error.Length > 60 ? error.Substring(0, 60) + "…" : error;
                        return;
                    }

                    bool changed = false;
                    // 覆写开关：默认关=只补空字段（保留已有资料）；开=用站点数据强制覆写已有信息
                    //（日文原名 ActorName 永不覆盖，英文名 ActorNameEN 视为可刮削字段）
                    bool overwrite = ConfigManager.Settings != null && ConfigManager.Settings.ActorOverwriteScraped;
                    if (TryGetStr(dict, "Height", out string height) && (overwrite || actorInfo.Height <= 0) && int.TryParse(height, out int h) && h > 0) {
                        if (overwrite || actorInfo.Height != h) {
                            actorInfo.Height = h;
                            changed = true;
                        }
                    }
                    if (TryGetStr(dict, "Chest", out string chest) && (overwrite || actorInfo.Chest <= 0) && int.TryParse(chest, out int b) && b > 0) {
                        if (overwrite || actorInfo.Chest != b) {
                            actorInfo.Chest = b;
                            changed = true;
                        }
                    }
                    if (TryGetStr(dict, "Waist", out string waist) && (overwrite || actorInfo.Waist <= 0) && int.TryParse(waist, out int w) && w > 0) {
                        if (overwrite || actorInfo.Waist != w) {
                            actorInfo.Waist = w;
                            changed = true;
                        }
                    }
                    if (TryGetStr(dict, "Hipline", out string hipline) && (overwrite || actorInfo.Hipline <= 0) && int.TryParse(hipline, out int hip) && hip > 0) {
                        if (overwrite || actorInfo.Hipline != hip) {
                            actorInfo.Hipline = hip;
                            changed = true;
                        }
                    }
                    if (TryGetStr(dict, "Cup", out string cup) && !string.IsNullOrEmpty(cup) && (overwrite || actorInfo.Cup == 'Z' || actorInfo.Cup == '\0')) {
                        if (overwrite || actorInfo.Cup != cup[0]) {
                            actorInfo.Cup = cup[0];
                            changed = true;
                        }
                    }
                    if (TryGetStr(dict, "BirthPlace", out string bp) && (overwrite || string.IsNullOrEmpty(actorInfo.BirthPlace))) {
                        if (overwrite || actorInfo.BirthPlace != bp) {
                            actorInfo.BirthPlace = bp;
                            changed = true;
                        }
                    }
                    if (TryGetStr(dict, "BloodType", out string bt) && (overwrite || string.IsNullOrEmpty(actorInfo.BloodType))) {
                        if (overwrite || actorInfo.BloodType != bt) {
                            actorInfo.BloodType = bt;
                            changed = true;
                        }
                    }
                    if (TryGetStr(dict, "ShoeSize", out string shoeSize) && (overwrite || string.IsNullOrEmpty(actorInfo.ShoeSize))) {
                        if (overwrite || actorInfo.ShoeSize != shoeSize) {
                            actorInfo.ShoeSize = shoeSize;
                            changed = true;
                        }
                    }
                    if (TryGetStr(dict, "Birthday", out string birthday) && (overwrite || string.IsNullOrEmpty(actorInfo.Birthday))) {
                        if (overwrite || actorInfo.Birthday != birthday) {
                            actorInfo.Birthday = birthday;
                            changed = true;
                        }
                    }
                    if (TryGetStr(dict, "ActorNameEN", out string en) && (overwrite || string.IsNullOrEmpty(actorInfo.ActorNameEN))) {
                        if (overwrite || actorInfo.ActorNameEN != en) {
                            actorInfo.ActorNameEN = en;
                            changed = true;
                        }
                    }
                    // 页面直接提供年龄时纳入(生日残缺时尤为有用)
                    if (TryGetStr(dict, "Age", out string pageAge) && (overwrite || actorInfo.Age <= 0) && int.TryParse(pageAge, out int pageAgeNum) && pageAgeNum > 0 && pageAgeNum < 120) {
                        if (overwrite || actorInfo.Age != pageAgeNum) {
                            actorInfo.Age = pageAgeNum;
                            changed = true;
                        }
                    }
                    if (!string.IsNullOrEmpty(actorInfo.Birthday)) {
                        int age = ActorInfo.CalculateAge(actorInfo.Birthday);
                        if (age > 0 && actorInfo.Age != age) {
                            actorInfo.Age = age;
                            changed = true;
                        }
                    }

                    // 头像下载（设置开启且站点提供了头像时）：与文字资料相互独立
                    bool imageSaved = false;
                    if (ConfigManager.Settings?.ActorScrapeSaveImage == true &&
                        TryGetStr(dict, "ImageUrl", out string imageUrl))
                        imageSaved = await SaveActorImageAsync(actorInfo, imageUrl, header);

                    if (changed || imageSaved) {
                        if (changed) {
                            actorInfo.WebType = TryGetStr(dict, "WebType", out string webType) ? webType : "minnanoav";
                            if (TryGetStr(dict, "WebUrl", out string webUrl))
                                actorInfo.WebUrl = webUrl;
                            actorInfo.UpdateDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            actorMapper.UpdateById(actorInfo);
                        }
                        Success = true;
                        Status = TaskStatus.RanToCompletion;
                        StatusText = imageSaved
                            ? (changed ? $"{actorInfo.ActorName} 资料与头像已补齐" : $"{actorInfo.ActorName} 头像已保存")
                            : $"{actorInfo.ActorName} 资料已补齐";
                        Progress = 100;
                        App.Logger.Info($"演员资料已补齐：{actorInfo.ActorName}（{StatusText}）");
                    } else {
                        // 站点有页面但没补到任何字段：同样标记已处理，避免反复请求
                        MarkAttempted(actorInfo);
                        Success = true;
                        Status = TaskStatus.RanToCompletion;
                        StatusText = $"{actorInfo.ActorName} 站内无可补字段";
                        Progress = 100;
                    }
                    StopWatch();
                } catch (Exception ex) {
                    Message = ex.Message;
                    App.Logger.Error(ex.Message);
                    FinalizeWithCancel();
                    StatusText = "演员资料补齐失败";
                }
                OnCompleted(null);
            });
        }

        /// <summary>探测结果缓存（App 生命周期一次）：插件 DLL 路径 → 是否提供 GetActorInfo</summary>
        private static readonly Dictionary<string, bool> ActorPluginProbeCache = new Dictionary<string, bool>();
        private static PluginMetaData _ActorPluginMeta;
        private static CrawlerServer _ActorPluginServer;

        /// <summary>
        /// 找提供 GetActorInfo 方法的插件及其已启用源。
        /// 探测用原生反射 + 容错 GetTypes：PluginInvoker 构造会对 DLL 做完整 GetTypes，
        /// 演员插件探测顺序会先扫到依赖解析失败的插件（如 db 的基类 CommonNet 在部分
        /// 时机解析不到 → ReflectionTypeLoadException →「DLL加载失败」）而误判全盘失败；
        /// ReflectionTypeLoadException 的 Types 里非空项仍可用，MinnanoavCrawler 自足可解析。
        /// 调用仍走 PluginInvoker（与 bus 静态插件同模式）。
        /// </summary>
        private (PluginMetaData, CrawlerServer) FindActorPlugin()
        {
            if (_ActorPluginMeta != null)
                return (_ActorPluginMeta, _ActorPluginServer);
            if (CrawlerManager.PluginMetaDatas == null)
                return (null, null);
            foreach (PluginMetaData metaData in CrawlerManager.PluginMetaDatas.Where(arg => arg.Enabled)) {
                if (string.IsNullOrEmpty(metaData.PluginID))
                    continue;
                CrawlerServer server = ConfigManager.ServerConfig?.CrawlerServers?.FirstOrDefault(arg =>
                    arg != null && arg.Enabled && arg.Available == 1 && !string.IsNullOrEmpty(arg.Url) &&
                    arg.PluginID != null && arg.PluginID.Equals(metaData.PluginID, StringComparison.OrdinalIgnoreCase));
                if (server == null)
                    continue;
                string dllPath = metaData.GetDllPath();
                bool hasActorInfo;
                try {
                    if (!ActorPluginProbeCache.TryGetValue(dllPath, out hasActorInfo)) {
                        System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(dllPath);
                        Type[] types;
                        try {
                            types = assembly.GetTypes();
                        } catch (System.Reflection.ReflectionTypeLoadException ex) {
                            // 依赖缺失的类型为 null，跳过；自足的类型仍可探测
                            types = ex.Types;
                        }
                        hasActorInfo = false;
                        foreach (Type type in types) {
                            if (type == null || !type.IsPublic)
                                continue;
                            if (type.GetMethod("GetActorInfo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static) != null) {
                                hasActorInfo = true;
                                break;
                            }
                        }
                        ActorPluginProbeCache[dllPath] = hasActorInfo;
                    }
                } catch (Exception ex) {
                    App.Logger.Error($"演员插件探测失败 {dllPath}：{ex.Message}");
                    ActorPluginProbeCache[dllPath] = false;
                    continue;
                }
                if (!hasActorInfo)
                    continue;
                _ActorPluginMeta = metaData;
                _ActorPluginServer = server;
                return (_ActorPluginMeta, _ActorPluginServer);
            }
            return (null, null);
        }

        private static void MarkAttempted(ActorInfo actorInfo)
        {
            try {
                string stamp = $"minnanoav:notfound@{DateTime.Now:yyyy-MM-dd}";
                string extra = string.IsNullOrEmpty(actorInfo.ExtraInfo) ? stamp : actorInfo.ExtraInfo.TrimEnd(';') + ";" + stamp;
                actorMapper.UpdateFieldById("ExtraInfo", extra, actorInfo.ActorID);
            } catch (Exception ex) {
                Jvedio.App.Logger.Error(ex.Message);
            }
        }

        /// <summary>取该演员关联的第一部影片番号（供按番号定位演员页的数据源用）</summary>
        private static string GetFirstLinkedVid(long actorID)
        {
            List<string> vids = GetLinkedVids(actorID, 1);
            return vids.Count > 0 ? vids[0] : "";
        }

        /// <summary>
        /// 下载并保存演员头像到本地头像目录（GetImagePath 约定的文件名）。
        /// 已有本地头像的演员直接跳过（不覆盖 Gfriends/MDCx 等来源的图）；失败仅记日志不影响任务
        /// </summary>
        private static async Task<bool> SaveActorImageAsync(ActorInfo actorInfo, string imageUrl, RequestHeader header)
        {
            try {
                if (string.IsNullOrEmpty(imageUrl) || !imageUrl.StartsWith("http"))
                    return false;
                string targetPath = Path.GetFullPath(actorInfo.GetImagePath());
                if (File.Exists(targetPath))
                    return false;
                string dir = Path.GetDirectoryName(targetPath);
                if (string.IsNullOrEmpty(dir))
                    return false;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (HttpClient http = new HttpClient()) {
                    http.Timeout = TimeSpan.FromSeconds(30);
                    // 沿用源配置的 UA/Cookie（双 UA 头教训：已有 UA 不再追加默认 UA）
                    if (header?.Headers != null) {
                        foreach (KeyValuePair<string, string> kv in header.Headers) {
                            if (kv.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)) {
                                if (http.DefaultRequestHeaders.UserAgent.Count == 0 && !string.IsNullOrEmpty(kv.Value))
                                    http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", kv.Value);
                            } else if (kv.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(kv.Value)) {
                                if (!http.DefaultRequestHeaders.Contains("Cookie"))
                                    http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", kv.Value);
                            }
                        }
                    }
                    if (http.DefaultRequestHeaders.UserAgent.Count == 0)
                        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                    byte[] bytes = await http.GetByteArrayAsync(imageUrl);
                    if (bytes == null || bytes.Length < 1024) {
                        App.Logger.Info($"演员头像下载内容异常（{bytes?.Length ?? 0} 字节），跳过：{actorInfo.ActorName}");
                        return false;
                    }
                    File.WriteAllBytes(targetPath, bytes);
                    App.Logger.Info($"演员头像已保存：{actorInfo.ActorName} → {targetPath}");
                    return true;
                }
            } catch (Exception ex) {
                App.Logger.Error($"演员头像保存失败 {actorInfo.ActorName}：{ex.Message}");
                return false;
            }
        }

        /// <summary>取该演员关联的全部影片番号（最多 limit 个）：404 时逐个换下一个重试</summary>
        private static List<string> GetLinkedVids(long actorID, int limit)
        {
            List<string> vids = new List<string>();
            try {
                List<Dictionary<string, object>> list = metaDataMapper.Select(
                    "select v.VID from metadata_to_actor ma join metadata_video v on v.DataID = ma.DataID " +
                    "where ma.ActorID = '" + actorID + "' and v.VID is not null and v.VID <> '' " +
                    "order by v.MVID limit " + limit);
                if (list != null)
                    foreach (Dictionary<string, object> row in list)
                        if (row.TryGetValue("VID", out object vid) && vid != null && !string.IsNullOrWhiteSpace(vid.ToString()))
                            vids.Add(vid.ToString().Trim());
            } catch (Exception ex) {
                App.Logger.Error(ex.Message);
            }
            return vids;
        }

        private static bool TryGetStr(Dictionary<string, object> dict, string key, out string value)
        {
            value = null;
            if (dict.TryGetValue(key, out object obj) && obj != null) {
                value = obj.ToString();
                return !string.IsNullOrWhiteSpace(value) && value != "N/A";
            }
            return false;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj is ActorScrapeTask other)
                return other.ActorID.Equals(ActorID);
            return false;
        }

        public override int GetHashCode()
        {
            return ActorID.GetHashCode();
        }
    }
}
