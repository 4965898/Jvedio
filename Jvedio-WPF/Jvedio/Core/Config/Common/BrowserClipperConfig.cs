using Jvedio.Core.Config.Base;

namespace Jvedio.Core.Config
{
    public sealed class BrowserClipperConfig : AbstractConfig
    {
        private BrowserClipperConfig() : base("BrowserClipper") { }
        private static readonly BrowserClipperConfig Instance = new BrowserClipperConfig();
        public static BrowserClipperConfig CreateInstance() => Instance;
        public bool Enabled { get; set; }
        public long Port { get; set; } = 18888;
        public bool SaveImages { get; set; } = true;
        public bool SaveCoverImages { get; set; } = true;
        public bool SavePreviewImages { get; set; }
        public bool SaveActorImages { get; set; } = true;
        public bool SavePreviewVideos { get; set; }
        // DPAPI protects the receiver credential for the current Windows user.
        public string ProtectedToken { get; set; } = "";
    }
}
