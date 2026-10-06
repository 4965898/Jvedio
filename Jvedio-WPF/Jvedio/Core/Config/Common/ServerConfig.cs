using Jvedio.Core.Config.Base;
using Jvedio.Core.Crawler;
using Jvedio.Core.Global;
using Jvedio.Entity.CommonSQL;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SuperUtils.Common;
using SuperUtils.Framework.ORM.Enums;
using SuperUtils.Framework.ORM.Wrapper;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;

namespace Jvedio.Core.Config
{
    public class ServerConfig : AbstractConfig
    {
        private ServerConfig() : base("Servers")
        {
            //DownloadInfo = true;
            //DownloadThumbNail = true;
            //DownloadPoster = true;
            //DownloadPreviewImage = false;
            //DownloadActor = true;
        }

        //public bool DownloadInfo { get; set; }
        //public bool DownloadThumbNail { get; set; }
        //public bool DownloadPoster { get; set; }
        //public bool DownloadPreviewImage { get; set; }
        //public bool DownloadActor { get; set; }

        private static ServerConfig instance = null;

        public static ServerConfig CreateInstance()
        {
            if (instance == null)
                instance = new ServerConfig();
            return instance;
        }

        public List<CrawlerServer> CrawlerServers { get; set; }

        private bool ReadSuccessfully;

        public const string PendingRecoveryFileName = ".pending-crawler-sources.json";

        /// <summary>在新版本启动时合并从备份找回的源，保留当前网址及其最新设置。</summary>
        public static int ApplyPendingRecovery()
        {
            string pending = Path.Combine(PathManager.CurrentUserFolder, PendingRecoveryFileName);
            if (!File.Exists(pending)) return 0;
            var recovered = ParseRecoverySources(File.ReadAllText(pending));
            var wrapper = new SelectWrapper<AppConfig>().Eq("ConfigName", "Servers");
            AppConfig stored = MapperManager.appConfigMapper.SelectOne(wrapper);
            string original = stored?.ConfigValue ?? "[]";
            var current = ParseRecoverySources(original);
            int added = 0;
            foreach (CrawlerServer source in recovered) {
                if (source == null || string.IsNullOrWhiteSpace(source.PluginID) || string.IsNullOrWhiteSpace(source.Url))
                    continue;
                if (current.Any(item => item != null && item.PluginID == source.PluginID &&
                    string.Equals(item.Url?.TrimEnd('/'), source.Url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
                    continue;
                current.Add(source);
                added++;
            }
            if (added > 0) {
                string rollback = Path.Combine(PathManager.CurrentUserFolder,
                    "crawler-sources-before-recovery-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                File.WriteAllText(rollback, original);
                string merged = JsonConvert.SerializeObject(current);
                MapperManager.appConfigMapper.Insert(new AppConfig {
                    ConfigName = "Servers", ConfigValue = merged
                }, InsertMode.Replace);
                if (MapperManager.appConfigMapper.SelectOne(wrapper)?.ConfigValue != merged)
                    throw new IOException("恢复的刮削器配置未成功保存");
            }
            File.Delete(pending);
            return added;
        }

        private static List<CrawlerServer> ParseRecoverySources(string json)
        {
            var sources = new List<CrawlerServer>();
            foreach (JObject source in JArray.Parse(json).OfType<JObject>()) {
                // 旧备份保存过插件运行对象；它们没有 JSON 构造函数，且应由本次刮削重新创建。
                source.Remove(nameof(CrawlerServer.Invoker));
                source.Remove(nameof(CrawlerServer.UrlCode));
                sources.Add(source.ToObject<CrawlerServer>());
            }
            return sources;
        }

        public override void Read()
        {
            ReadSuccessfully = false;
            SelectWrapper<AppConfig> wrapper = new SelectWrapper<AppConfig>();
            wrapper.Eq("ConfigName", ConfigName);
            AppConfig appConfig = MapperManager.appConfigMapper.SelectOne(wrapper);
            if (appConfig == null || appConfig.ConfigId == 0) {
                CrawlerServers = new List<CrawlerServer>();
                ReadSuccessfully = true;
                return;
            }
            List<Dictionary<object, object>> dicts = JsonUtils.TryDeserializeObject<List<Dictionary<object, object>>>(appConfig.ConfigValue);

            if (dicts == null)
                return;
            // 配置先于插件加载；读取所有已保存源，不能把尚未加载/暂时缺失的插件当作已删除。
            List<CrawlerServer> servers = new List<CrawlerServer>();
            foreach (Dictionary<object, object> d in dicts) {
                CrawlerServer server = new CrawlerServer();
                //if (!server.HasAllKeys(d))
                //    continue;
                if (d.ContainsKey("PluginID"))
                    server.PluginID = d["PluginID"].ToString();
                if (string.IsNullOrEmpty(server.PluginID))
                    continue;
                if (d.ContainsKey("Url") && d["Url"] is string url)
                    server.Url = url;
                if (d.ContainsKey("Cookies") && d["Cookies"] is string Cookies)
                    server.Cookies = Cookies;
                if (d.ContainsKey("Enabled") && d["Enabled"] is bool enabled)
                    server.Enabled = enabled;
                if (d.ContainsKey("LastRefreshDate") && d["LastRefreshDate"] is string LastRefreshDate)
                    server.LastRefreshDate = LastRefreshDate;
                if (d.ContainsKey("Headers") && d["Headers"] is string Headers)
                    server.Headers = Headers;
                if (d.ContainsKey("Available") && int.TryParse(d["Available"].ToString(), out int available))
                    server.Available = available;
                servers.Add(server);
            }
            CrawlerServers = servers;
            ReadSuccessfully = true;
        }

        public override void Save()
        {
            if (ReadSuccessfully && CrawlerServers != null) {
                AppConfig appConfig = new AppConfig();
                appConfig.ConfigName = ConfigName;
                appConfig.ConfigValue = JsonConvert.SerializeObject(CrawlerServers);
                MapperManager.appConfigMapper.Insert(appConfig, InsertMode.Replace);
            }
        }
    }
}
