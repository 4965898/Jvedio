using Jvedio.Core.Global;
using Newtonsoft.Json.Linq;
using SuperControls.Style;
using SuperControls.Style.Windows;
using SuperUtils.IO;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using static Jvedio.App;

namespace Jvedio.Upgrade
{
    public static class UpgradeHelper
    {
        public const int AUTO_CHECK_UPGRADE_DELAY = 60 * 1000;

        private const string LatestReleaseApi = "https://api.github.com/repos/" +
            UrlManager.ReleaseRepository + "/releases/latest";
        private static Window ParentWindow { get; set; }
        private static bool Checking { get; set; }

        public static void Init(Window parent)
        {
            ParentWindow = parent;
            Logger.Info("init upgrade check ok");
        }

        public static async void OpenWindow()
        {
            if (Checking)
                return;

            Checking = true;
            try {
                var release = await GetUpgradeInfo();
                if (HasNewVersion(release.LatestVersion)) {
                    PromptToOpenRelease(release.LatestVersion, release.ReleaseDate);
                } else {
                    string message = string.Format(
                        LangManager.GetValueByKey("UpgradeUpToDate"),
                        App.GetLocalVersion(false), release.LatestVersion);
                    ShowMessage(message, MessageBoxImage.Information);
                }
            } catch (Exception ex) {
                Logger.Error(ex);
                string message = string.Format(
                    LangManager.GetValueByKey("UpgradeCheckFailed"), ex.Message);
                ShowMessage(message, MessageBoxImage.Warning);
            } finally {
                Checking = false;
            }
        }

        public static bool HasNewVersion(string latestVersion)
        {
            string tag = latestVersion?.Trim().TrimStart('v', 'V');
            if (!Version.TryParse(tag, out Version remote))
                throw new FormatException("Invalid release version: " + latestVersion);

            return remote.CompareTo(Version.Parse(App.GetLocalVersion(false))) > 0;
        }

        public static void PromptToOpenRelease(string latestVersion, string releaseDate)
        {
            string message = string.Format(
                LangManager.GetValueByKey("UpgradeAvailable"),
                latestVersion, App.GetLocalVersion(false), releaseDate);
            if (new MsgBox(message).ShowDialog(GetActiveWindow()) == true)
                FileHelper.TryOpenUrl(UrlManager.ReleaseUrl + "/latest");
        }

        private static Window GetActiveWindow()
        {
            if (ParentWindow?.IsVisible == true &&
                ParentWindow.WindowState != WindowState.Minimized)
                return ParentWindow;
            return Application.Current?.Windows.OfType<Window>().FirstOrDefault(window =>
                window.IsActive && window.IsVisible && window.WindowState != WindowState.Minimized);
        }

        private static void ShowMessage(string message, MessageBoxImage icon)
        {
            string title = LangManager.GetValueByKey("CheckUpgrade");
            Window owner = GetActiveWindow();
            if (owner == null)
                MessageBox.Show(message, title, MessageBoxButton.OK, icon);
            else
                MessageBox.Show(owner, message, title, MessageBoxButton.OK, icon);
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

                using (var client = new HttpClient(handler)) {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Jvedio-update-check");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github+json");

                    string response = await client.GetStringAsync(LatestReleaseApi);
                    JObject release = JObject.Parse(response);
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
