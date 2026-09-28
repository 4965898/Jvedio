using SuperControls.Style.Upgrade;
using System.Collections.Generic;
using System.Linq;
using static Jvedio.App;

namespace Jvedio.Core.Global
{
    public static class UrlManager
    {
        #region "const"
        private const string DonateJsonBasePath = "SuperStudio-Donate";

        public const string ProjectUrl = "https://github.com/" + ReleaseRepository;
        private const string DocumentationUrl = ProjectUrl + "/blob/master/Jvedio-WPF/Document/Wiki/5.0/";
        public const string ServerHelpUrl = DocumentationUrl + "02-Beginning.md";
        public const string WebPage = ProjectUrl;
        public const string ReleaseRepository = "4965898/Jvedio";
        public const string ReleaseUrl = "https://github.com/" + ReleaseRepository + "/releases";
        public const string UpgradeSource = "https://hitchao.github.io/";
        public const string ServerUrl = "https://hitchao.github.io/hitchao/jvedio-server/jvedio-server.jar";


        public const string NoticeUrl = "https://hitchao.github.io/jvedioupdate/notice.json";
        public const string FeedBackUrl = ProjectUrl + "/issues";
        public const string WikiUrl = DocumentationUrl + "02-Beginning.md";
        public const string WebPageUrl = ProjectUrl;
        public const string ThemeDIY = "https://hitchao.github.io/JvedioWebPage/theme.html";
        public const string PLUGIN_LIST_URL = "https://hitchao.github.io/Jvedio-Plugin/pluginlist.json";
        public const string PLUGIN_LIST_BASE_URL = "https://hitchao.github.io/Jvedio-Plugin/";
        public const string FFMPEG_URL = "https://www.gyan.dev/ffmpeg/builds/";
        public const string PLUGIN_UPLOAD_HELP = DocumentationUrl + "08-Plugin.md";
        public const string HEADER_HELP = DocumentationUrl + "05-Headers.md";


        #endregion

        #region "属性"

        public static Dictionary<string, UpgradeSource> UpgradeSourceDict { get; set; } = new Dictionary<string, UpgradeSource>()
        {
            {"Github",new UpgradeSource(UpgradeSource,ReleaseUrl,"jvedioupdate") },
            {"Github加速",new UpgradeSource("https://cdn.jsdelivr.net/gh/hitchao/",ReleaseUrl,"jvedioupdate") },
            {"StormKit",new UpgradeSource("https://divealpine-ab8zhe--77466901127398.stormkit.dev/",ReleaseUrl,"") },
        };

        public static List<string> UpgradeSourceKeys { get; set; } = UpgradeSourceDict.Keys.ToList();

        /// <summary>
        /// 用户切换源的时候存储起来
        /// </summary>
        private static int RemoteIndex { get; set; } = (int)ConfigManager.Settings.RemoteIndex;

        #endregion

        public static int GetRemoteIndex()
        {
            return RemoteIndex;
        }
        public static void SetRemoteIndex(int idx)
        {
            RemoteIndex = idx;
            Logger.Info($"set remote index: {RemoteIndex}");
        }
        public static string GetRemoteBasePath()
        {
            if (RemoteIndex < 0 || RemoteIndex >= UpgradeSourceKeys.Count)
                RemoteIndex = 0;

            if (RemoteIndex >= UpgradeSourceKeys.Count)
                return "";

            return UpgradeSourceDict[UpgradeSourceKeys[RemoteIndex]].BaseUrl;
        }

        public static string GetDonateJsonUrl()
        {
            return $"{GetRemoteBasePath()}{DonateJsonBasePath}/config.json";
        }

        public static string GetPluginUrl()
        {
            return PLUGIN_LIST_BASE_URL;
        }
    }
}
