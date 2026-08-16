using Jvedio.Core.Config.Base;
using Jvedio.Core.Global;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static Jvedio.App;

namespace Jvedio.Core.Config
{
    /// <summary>
    /// 单个翻译平台的设置（每个平台独立保存，切换平台互不干扰）
    /// </summary>
    public class TranslationPlatformSetting
    {
        public string Model { get; set; } = string.Empty;

        public string ApiUrl { get; set; } = string.Empty;

        public string Field1 { get; set; } = string.Empty;

        public string Field2 { get; set; } = string.Empty;

        public string Field3 { get; set; } = string.Empty;
    }

    /// <summary>
    /// 翻译功能配置（选项-翻译）：平台、模型、URL、密钥等
    /// 参考 PotPlayer 字幕翻译插件（只读参考 A:\PotPlayer_20181126\PotPlayer\Extension\Subtitle\Translate）
    /// </summary>
    public class TranslationConfig : AbstractConfig
    {
        private TranslationConfig() : base("Translation")
        {
        }

        private static TranslationConfig instance = null;

        public static TranslationConfig CreateInstance()
        {
            if (instance == null)
                instance = new TranslationConfig();
            return instance;
        }

        /// <summary>
        /// 独立 JSON 配置文件（exe目录\data\用户名\translation.config.json）：
        /// 部署只拷 exe 不碰 data 目录，新版本 exe 直接读取，密钥与配置不丢失
        /// </summary>
        private static string PersistPath => Path.Combine(PathManager.CurrentUserFolder, "translation.config.json");

        /// <summary>
        /// 旧版位置（%APPDATA%\Jvedio），用于回退迁移
        /// </summary>
        private static string LegacyPersistPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Jvedio", "translation.config.json");

        public override void Read()
        {
            try {
                if (File.Exists(PersistPath)) {
                    string json = File.ReadAllText(PersistPath, Encoding.UTF8);
                    JsonConvert.PopulateObject(json, this);
                    Logger.Info("read translation config from file: " + PersistPath);
                    return;
                }
                if (File.Exists(LegacyPersistPath)) {
                    string json = File.ReadAllText(LegacyPersistPath, Encoding.UTF8);
                    JsonConvert.PopulateObject(json, this);
                    Logger.Info("read translation config from legacy file: " + LegacyPersistPath);
                    try {
                        Save();
                    } catch (Exception ex) {
                        Logger.Error(ex);
                    }
                    return;
                }
            } catch (Exception ex) {
                Logger.Error(ex);
            }
            base.Read();
            try {
                Save();
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        public override void Save()
        {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(PersistPath));
                string json = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(PersistPath, json, Encoding.UTF8);
                Directory.CreateDirectory(Path.GetDirectoryName(LegacyPersistPath));
                File.WriteAllText(LegacyPersistPath, json, Encoding.UTF8);
            } catch (Exception ex) {
                Logger.Error(ex);
            }
            base.Save();
        }

        /// <summary>
        /// 当前选中的翻译平台（见 Core.Translation.TranslatePlatform 枚举）
        /// </summary>
        public int Platform { get; set; } = 0;

        /// <summary>
        /// 源语言（空 = 自动检测；zh-CN/en/ja 等）
        /// </summary>
        public string SourceLang { get; set; } = string.Empty;

        /// <summary>
        /// 目标语言（默认 zh-CN）
        /// </summary>
        public string TargetLang { get; set; } = "zh-CN";

        /// <summary>
        /// 各平台的独立设置（平台枚举值 → 设置），切换平台互不覆盖
        /// </summary>
        public Dictionary<int, TranslationPlatformSetting> PlatformSettings { get; set; } =
            new Dictionary<int, TranslationPlatformSetting>();

        public TranslationPlatformSetting GetSetting(int platform)
        {
            if (PlatformSettings == null)
                PlatformSettings = new Dictionary<int, TranslationPlatformSetting>();
            if (!PlatformSettings.TryGetValue(platform, out TranslationPlatformSetting setting)) {
                setting = new TranslationPlatformSetting();
                PlatformSettings[platform] = setting;
            }
            return setting;
        }
    }
}
