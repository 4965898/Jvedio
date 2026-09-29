using Jvedio.Core.Global;
using Newtonsoft.Json.Linq;
using SuperControls.Style.Upgrade;
using SuperUtils.NetWork;
using SuperUtils.NetWork.Crawler;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Jvedio.App;

namespace Jvedio.Upgrade
{
    public static class UpgradeHelper
    {
        public const int AUTO_CHECK_UPGRADE_DELAY = 60 * 1000;

        private const string LatestReleaseApi = "https://api.github.com/repos/" +
            UrlManager.ReleaseRepository + "/releases/latest";
        private const string UpdateFeed = "https://raw.githubusercontent.com/" +
            UrlManager.ReleaseRepository + "/update-feed/";

        private static bool WindowClosed { get; set; }
        private static SuperUpgrader Upgrader { get; set; }
        private static Dialog_Upgrade Dialog { get; set; }

        public static void Init(Window parent)
        {
            Upgrader = new SuperUpgrader {
                UpgradeSourceDict = new Dictionary<string, UpgradeSource> {
                    { "Github", new UpgradeSource(UpdateFeed, UrlManager.ReleaseUrl + "/latest", "jvedioupdate") }
                },
                UpgradeSourceIndex = 0,
                Language = "zh-CN",
                Header = new CrawlerHeader(SuperWebProxy.SystemWebProxy).Default,
                BeforeUpdateDelay = 5,
                AfterUpdateDelay = 1,
                UpDateFileDir = "TEMP",
                AppName = "Jvedio.exe"
            };
            WindowClosed = true;
            Logger.Info("init upgrade dialog ok");
        }

        public static void OpenWindow()
        {
            if (WindowClosed) {
                Dialog = new Dialog_Upgrade(Upgrader) {
                    LocalVersion = App.GetLocalVersion(false)
                };
                Dialog.Closed += (sender, args) => WindowClosed = true;
                Dialog.ContentRendered += (sender, args) => WrapReleaseNote(Dialog);
                Dialog.PropertyChanged += (sender, args) => {
                    if (args.PropertyName == nameof(Dialog.LatestVersion)) {
                        bool newer = false;
                        if (!string.IsNullOrWhiteSpace(Dialog.LatestVersion)) {
                            try {
                                newer = HasNewVersion(Dialog.LatestVersion);
                            } catch (FormatException ex) {
                                Logger.Error(ex);
                            }
                        }
                        Dialog.CanUpgrade = newer;
                    }
                };
                Dialog.OnExitApp += () => Application.Current.Shutdown();
                WindowClosed = false;
            }

            Dialog?.ShowDialog();
        }

        private static void WrapReleaseNote(DependencyObject root)
        {
            if (root is TextBox textBox && textBox.ActualHeight > 100) {
                textBox.TextWrapping = TextWrapping.Wrap;
                textBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                return;
            }

            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                WrapReleaseNote(VisualTreeHelper.GetChild(root, index));
        }

        public static bool HasNewVersion(string latestVersion)
        {
            string tag = latestVersion?.Trim().TrimStart('v', 'V');
            if (!Version.TryParse(tag, out Version remote))
                throw new FormatException("Invalid release version: " + latestVersion);

            return remote.CompareTo(Version.Parse(App.GetLocalVersion(false))) > 0;
        }

        public static async Task<(string LatestVersion, string ReleaseDate, string ReleaseNote)> GetUpgradeInfo()
        {
            using (var handler = new HttpClientHandler()) {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                var proxy = ConfigManager.ProxyConfig?.GetWebProxy();
                if (proxy != null) {
                    handler.Proxy = proxy;
                    handler.UseProxy = true;
                }

                using (var client = new System.Net.Http.HttpClient(handler)) {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Jvedio-update-check");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github+json");

                    JObject release = JObject.Parse(await client.GetStringAsync(LatestReleaseApi));
                    string latestVersion = release.Value<string>("tag_name");
                    if (string.IsNullOrWhiteSpace(latestVersion) ||
                        !Version.TryParse(latestVersion.Trim().TrimStart('v', 'V'), out _))
                        throw new FormatException("GitHub release has no valid version tag.");

                    string publishedAt = release.Value<string>("published_at");
                    string releaseDate = DateTimeOffset.TryParse(publishedAt,
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                        out DateTimeOffset date)
                        ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : string.Empty;

                    return (latestVersion, releaseDate, release.Value<string>("body") ?? string.Empty);
                }
            }
        }
    }
}
