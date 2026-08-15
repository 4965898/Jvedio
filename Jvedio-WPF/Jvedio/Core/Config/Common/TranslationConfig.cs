using Jvedio.Core.Config.Base;
using System.Collections.Generic;

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
