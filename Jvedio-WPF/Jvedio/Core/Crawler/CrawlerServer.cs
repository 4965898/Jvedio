
using Jvedio.Core.Interfaces;
using Jvedio.Entity;
using Newtonsoft.Json;
using SuperControls.Style.Plugin;
using SuperUtils.Common;
using SuperUtils.NetWork.Entity;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using static Jvedio.App;

namespace Jvedio.Core.Crawler
{
    /// <summary>
    /// 服务器源
    /// </summary>
    public class CrawlerServer : INotifyPropertyChanged, IDictKeys
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public CrawlerServer()
        {
            PluginID = string.Empty;
        }

        #region "属性"


        public UrlCode UrlCode { get; set; }
        public PluginInvoker Invoker { get; set; }

        public string PluginID { get; set; }

        private string _Url;

        public string Url {
            get {
                return _Url;
            }

            set {
                _Url = value;
                RaisePropertyChanged();
            }
        }

        private bool _Enabled;

        public bool Enabled {
            get {
                return _Enabled;
            }

            set {
                _Enabled = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// -1 不可用，1-可用，2-测试中
        /// </summary>
        private int _Available;

        public int Available {
            get {
                return _Available;
            }

            set {
                _Available = value;
                RaisePropertyChanged();
            }
        }

        public string LastRefreshDate { get; set; }

        private string _Cookies;

        public string Cookies {
            get {
                return _Cookies;
            }

            set {
                _Cookies = value;
                RaisePropertyChanged();
            }
        }

        private string _Headers { get; set; }

        public string Headers {
            get {
                return _Headers;
            }

            set {
                _Headers = value;
                RaisePropertyChanged();
            }
        }
        #endregion


        public bool IsHeaderProper()
        {
            if (string.IsNullOrEmpty(Headers))
                return true;
            try {
                Dictionary<string, string> dictionary =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(Headers);
                return true;
            } catch (Exception ex) {
                Logger.Error(ex);
                return false;
            }
        }

        // 返回独立拷贝：CrawlerHeader.Default 是静态共享实例，直接外发会被
        // 并发任务/爬虫插件修改其 Headers 字典（「集合已修改」异常的根源之一）
        private static RequestHeader CopyDefaultHeader()
        {
            RequestHeader copy = new RequestHeader();
            copy.WebProxy = ConfigManager.ProxyConfig.GetWebProxy();
            copy.TimeOut = ConfigManager.ProxyConfig.HttpTimeout * 1000; // 转为 ms
            if (CrawlerHeader.Default?.Headers != null)
                copy.Headers = new Dictionary<string, string>(CrawlerHeader.Default.Headers);
            return copy;
        }

        public static RequestHeader ParseHeader(CrawlerServer server)
        {
            if (server == null)
                return CopyDefaultHeader();
            RequestHeader result = new RequestHeader();
            result.WebProxy = ConfigManager.ProxyConfig.GetWebProxy();
            result.TimeOut = ConfigManager.ProxyConfig.HttpTimeout * 1000; // 转为 ms
            string header = server.Headers;
            if (string.IsNullOrEmpty(header))
                return CopyDefaultHeader();

            Logger.Info($"parse header: {header}");

            Dictionary<string, string> dict =
                JsonUtils.TryDeserializeObject<Dictionary<string, string>>(header);
            if (dict != null && dict.Count > 0) {
                if (!dict.ContainsKey("cookie") && !string.IsNullOrEmpty(server.Cookies))
                    dict.Add("cookie", server.Cookies);
                dict["Cache-Control"] = "no-cache";   // 不缓存
                result.Headers = dict;
            } else {
                Logger.Warn("header is not proper");
            }
            return result;
        }

        public bool HasAllKeys(Dictionary<object, object> dict)
        {
            if (dict == null || dict.Count == 0)
                return false;
            PropertyInfo[] propertyInfos = this.GetType().GetProperties();
            foreach (var item in propertyInfos) {
                if (!dict.ContainsKey(item.Name))
                    return false;
                if (dict[item.Name] == null)
                    return false;
            }

            return true;
        }

        public void AttachToDict(Dictionary<string, object> dict)
        {
            dict["Url"] = Url;
            dict["UrlCode"] = JsonUtils.TrySerializeObject(UrlCode);
            dict["Header"] = JsonUtils.TryDeserializeObject<Dictionary<string, string>>(Headers);
        }
    }
}
