using Jvedio.Core.Config.Base;
using System.Collections.Generic;

namespace Jvedio.Core.Config
{
    /// <summary>
    /// 在线观看跳转站点的网址覆盖配置
    /// （站点域名变更 / 出现免翻墙镜像站时，由用户在「选项-网络」中自定义）
    /// </summary>
    public class OnlineConfig : AbstractConfig
    {
        private OnlineConfig() : base("OnlineSites")
        {
        }

        private static OnlineConfig instance = null;

        public static OnlineConfig CreateInstance()
        {
            if (instance == null)
                instance = new OnlineConfig();
            return instance;
        }

        /// <summary>
        /// 站点名 → 覆盖网址模板（含 {{code}} 占位符）；留空/删除 = 使用内置默认网址
        /// </summary>
        public Dictionary<string, string> UrlOverrides { get; set; } = new Dictionary<string, string>();
    }
}
