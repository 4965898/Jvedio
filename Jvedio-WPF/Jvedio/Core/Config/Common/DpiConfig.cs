using Jvedio.Core.Global;
using Newtonsoft.Json;
using System;
using System.IO;

namespace Jvedio.Core.Config
{
    /// <summary>
    /// 显示缩放配置（独立 JSON：data\用户名\dpi.config.json，不随 exe 版本丢失）
    /// UseSystemDpiScale：跟随系统缩放比例（PerMonitorV2，进程级，重启生效）
    /// UiFontScale：界面字号缩放比例（0.8~1.5，即时生效）
    /// </summary>
    public static class DpiConfig
    {
        public static bool UseSystemDpiScale { get; set; } = true;

        public static double UiFontScale { get; set; } = 1.0;

        public static string PersistPath => Path.Combine(PathManager.CurrentUserFolder, "dpi.config.json");

        public static void Load()
        {
            try {
                if (File.Exists(PersistPath)) {
                    DpiConfigData data = JsonConvert.DeserializeObject<DpiConfigData>(File.ReadAllText(PersistPath));
                    if (data != null) {
                        UseSystemDpiScale = data.UseSystemDpiScale;
                        if (data.UiFontScale >= 0.5 && data.UiFontScale <= 2.0)
                            UiFontScale = data.UiFontScale;
                    }
                }
            } catch (Exception) { }
        }

        public static void Save()
        {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(PersistPath));
                File.WriteAllText(PersistPath, JsonConvert.SerializeObject(
                    new DpiConfigData { UseSystemDpiScale = UseSystemDpiScale, UiFontScale = UiFontScale },
                    Newtonsoft.Json.Formatting.Indented));
            } catch (Exception) { }
        }

        private class DpiConfigData
        {
            public bool UseSystemDpiScale { get; set; } = true;

            public double UiFontScale { get; set; } = 1.0;
        }
    }
}