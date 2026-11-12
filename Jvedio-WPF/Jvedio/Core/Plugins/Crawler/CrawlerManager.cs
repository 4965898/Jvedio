using SuperControls.Style;
using Jvedio.Core.Crawler;
using SuperControls.Style.Plugin;
using SuperUtils.Common;
using SuperUtils.IO;
using SuperUtils.Reflections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static Jvedio.App;

namespace Jvedio.Core.Plugins.Crawler
{
    /// <summary>
    /// 加载爬虫插件
    /// <para>DLL 文件</para>
    /// </summary>
    public class CrawlerManager
    {

        #region "属性"


        public static List<PluginMetaData> PluginMetaDatas { get; set; }

        public static string BaseDir { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "crawlers");

        #endregion

        // todo DLL 签名验证
        public static void Init(bool moveFile)
        {
            // 扫描
            List<string> list = DirHelper.TryGetDirList(BaseDir).ToList();
            PluginMetaDatas = new List<PluginMetaData>();
            foreach (string crawler_dir in list) {
                // 移动并删除 temp
                if (moveFile) {
                    string tempPath = Path.Combine(crawler_dir, "temp");
                    if (Directory.Exists(tempPath))
                        DirHelper.TryMoveDir(tempPath, crawler_dir, true);
                }
                string[] arr = FileHelper.TryGetAllFiles(crawler_dir, "*.dll");
                if (arr == null || arr.Length <= 0) {
                    Logger.Warn($"dir does not have dll: {crawler_dir}");
                    continue;
                }

                string dllPath = arr[0];

                // 校验
                PluginMetaData data = GetPluginData(dllPath);
                if (data == null) {
                    Logger.Warn($"parse plugin meta data failed: {dllPath}");
                    continue;
                }
                data.SetPluginID(PluginType.Crawler, Path.GetFileName(crawler_dir));
                CrawlerInfo info = new CrawlerInfo();
                info.Path = dllPath;
                PluginMetaDatas.Add(data);

                // 校验并复制
                bool copy = NeedToCopy(dllPath);
                string target = Path.Combine(BaseDir, Path.GetFileName(dllPath));
                if (copy)
                    FileHelper.TryCopyFile(dllPath, target, true);
            }

            ConfigManager.ServerConfig.Read(); // 必须在加载所有爬虫插件后在初始化
            SeedDefaultSources();
            PluginMetaData.BaseDir = BaseDir;
        }

        /// <summary>
        /// 新增插件（avmoo/javmenu/minnanoav）首次出现且没有任何源时，写入一条默认源：
        /// Enabled=false（用户在设置页手动开启）、Available=1（免测即可用，运行期失败由逐源回退兜底）。
        /// 幂等：已有任意源（含用户自建镜像）则跳过。
        /// PluginID 真实格式为「crawler/<目录名>」（SetPluginID = 枚举名小写 + '/'，用户库 Servers 配置实测确认）
        /// </summary>
        private static void SeedDefaultSources()
        {
            try {
                if (PluginMetaDatas == null || ConfigManager.ServerConfig?.CrawlerServers == null)
                    return;
                string prefix = PluginType.Crawler.ToString().ToLowerInvariant() + "/";
                Dictionary<string, string> defaultUrls = new Dictionary<string, string>() {
                    { "avmoo", "https://avmoo.shop" },
                    { "javmenu", "https://javmenu.com" },
                    { "minnanoav", "https://www.minnano-av.com" },
                    { "javdatabase", "https://www.javdatabase.com" },
                };
                bool changed = false;
                // 清理 6.0.0.5 首个构建用错格式（Crawler.avmoo 点号写法）播种出的死行：
                // 该格式匹配不到任何插件、设置页不可见，只可能由本程序播种产生
                for (int i = ConfigManager.ServerConfig.CrawlerServers.Count - 1; i >= 0; i--) {
                    CrawlerServer server = ConfigManager.ServerConfig.CrawlerServers[i];
                    if (server?.PluginID != null &&
                        defaultUrls.Keys.Any(name => server.PluginID.Equals("Crawler." + name, StringComparison.Ordinal))) {
                        ConfigManager.ServerConfig.CrawlerServers.RemoveAt(i);
                        changed = true;
                        Logger.Info($"removed stale seed row with wrong PluginID: {server.PluginID}");
                    }
                }
                foreach (var pair in defaultUrls) {
                    string pluginID = prefix + pair.Key;
                    if (ConfigManager.ServerConfig.CrawlerServers.Any(arg => arg != null &&
                        !string.IsNullOrEmpty(arg.PluginID) &&
                        arg.PluginID.Equals(pluginID, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    if (!PluginMetaDatas.Any(arg => arg != null &&
                        arg.PluginID != null && arg.PluginID.Equals(pluginID, StringComparison.OrdinalIgnoreCase)))
                        continue; // 插件未安装则不播种
                    ConfigManager.ServerConfig.CrawlerServers.Add(new CrawlerServer() {
                        PluginID = pluginID,
                        Url = pair.Value,
                        Enabled = false,
                        Available = 1,
                    });
                    changed = true;
                    Logger.Info($"seed default source for {pluginID}: {pair.Value} (disabled, enable in settings)");
                }
                if (changed)
                    ConfigManager.ServerConfig.Save();
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        public static bool NeedToCopy(string dllPath)
        {
            string target = Path.Combine(BaseDir, Path.GetFileName(dllPath));
            if (!File.Exists(target))
                return true;

            // 检查 Md5
            string m1 = SuperUtils.Security.Encrypt.TryGetFileMD5(dllPath);
            string m2 = SuperUtils.Security.Encrypt.TryGetFileMD5(target);
            return !m1.Equals(m2);
        }

        private static PluginMetaData GetPluginData(string dllPath)
        {
            string basePath = Path.GetDirectoryName(dllPath);
            string json_path = Path.Combine(basePath, "main.json");
            PluginMetaData data = null;
            string jsonPath = GetCrawlerJsonPath(json_path);
            if (!File.Exists(jsonPath)) {
                Logger.Warn($"file not exists: {jsonPath}");
                return null;
            }
            data = PluginMetaData.ParseByPath(jsonPath);
            if (data == null) {
                Logger.Warn($"parse failed: {jsonPath}");
                return null;
            }

            // 必须加载
            Assembly dll = ReflectionHelper.TryLoadAssembly(dllPath);
            if (dll == null)
                return null;
            Type classType = getPublicType(dll);
            if (classType == null)
                return null;

            data.Installed = true;
            // 读取配置
            string configPath = Path.Combine(basePath, "config.json");
            if (File.Exists(configPath) &&
                FileHelper.TryReadFile(configPath) is string configString &&
                !string.IsNullOrEmpty(configString)) {
                Dictionary<string, object> dict =
                    JsonUtils.TryDeserializeObject<Dictionary<string, object>>(configString);
                if (dict != null) {
                    dict.TryGetValue("enabled", out object enabledString);
                    if (enabledString is bool enabled)
                        data.Enabled = enabled;
                }
            } else {
                Logger.Warn($"read plugin config failed: {configPath}");
            }
            return data;
        }

        private static string GetCrawlerJsonPath(string dllPath)
        {
            string dir = Path.GetDirectoryName(dllPath);
            string name = Path.GetFileNameWithoutExtension(dllPath);
            return Path.Combine(dir, name + ".json");
        }

        private static Type getPublicType(Assembly dll)
        {
            Type[] types = null;
            try {
                types = dll.GetTypes();
            } catch (Exception ex) {
                MessageCard.Error(ex.Message);
                return null;
            }

            if (types == null || types.Length == 0)
                return null;
            foreach (Type type in types) {
                if (type.IsPublic)
                    return type;
            }

            return null;
        }
    }
}
