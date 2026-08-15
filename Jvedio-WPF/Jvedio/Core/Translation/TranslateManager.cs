using Jvedio.Core.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Jvedio.Core.Translation
{
    /// <summary>
    /// 翻译平台类别
    /// </summary>
    public enum TranslatePlatformClass
    {
        /// <summary>
        /// AI 平台（ChatGPT 兼容 Completion 格式：OpenAI / DeepSeek / Ollama / LM Studio 等）
        /// </summary>
        AI = 0,

        /// <summary>
        /// 传统机器翻译平台（百度/Google/DeepL 等）
        /// </summary>
        Machine = 1,
    }

    /// <summary>
    /// 翻译平台（参考 PotPlayer 字幕翻译插件各平台接口，只读参考
    /// A:\PotPlayer_20181126\PotPlayer\Extension\Subtitle\Translate）
    /// </summary>
    public enum TranslatePlatform
    {
        // ---------- AI 类（ChatGPT 兼容 Completion 格式） ----------
        OpenAI = 0,        // OpenAI 官方
        DeepSeek = 1,      // DeepSeek（ChatGPT 兼容）
        Ollama = 2,        // 本地 Ollama（ChatGPT 兼容）
        LMStudio = 3,      // 本地 LM Studio（ChatGPT 兼容）
        CustomChat = 4,    // 自定义 OpenAI 兼容端点（opencode 网关/中转站等）

        // ---------- 传统机器翻译类 ----------
        Baidu = 100,       // 百度翻译开放平台
        Aliyun = 101,      // 阿里翻译（走 server.cutil.top 代理）
        Tencent = 102,     // 腾讯翻译（走 server.cutil.top 代理）
        Calf = 103,        // 小牛翻译（走 server.cutil.top 代理）
        Human = 104,       // 火山翻译（走 server.cutil.top 代理）
        Google = 105,      // Google 翻译（免费接口，key 可空）
        DeepL = 106,       // DeepL
        Bing = 107,        // 微软翻译
        Libre = 108,       // LibreTranslate（支持本地部署地址）
        Yandex = 109,      // Yandex
        Papago = 110,      // Naver Papago NMT
    }

    public class TranslatePlatformDef
    {
        public TranslatePlatform Platform { get; set; }

        public TranslatePlatformClass Class { get; set; }

        public string Name { get; set; }

        public string DefaultUrl { get; set; }

        public string DefaultModel { get; set; }

        public string Field1Label { get; set; }

        public string Field2Label { get; set; }

        public string Field3Label { get; set; }

        public bool NeedModel { get; set; }
    }

    public static class TranslatePlatforms
    {
        public static List<TranslatePlatformDef> Defs { get; } = new List<TranslatePlatformDef>() {
            // ---------- AI 类（ChatGPT 兼容） ----------
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.OpenAI,
                Class = TranslatePlatformClass.AI,
                Name = "OpenAI (ChatGPT)",
                DefaultUrl = "https://api.openai.com/v1/chat/completions",
                DefaultModel = "gpt-4o-mini",
                Field1Label = "API Key",
                NeedModel = true,
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.DeepSeek,
                Class = TranslatePlatformClass.AI,
                Name = "DeepSeek",
                DefaultUrl = "https://api.deepseek.com/v1/chat/completions",
                DefaultModel = "deepseek-chat",
                Field1Label = "API Key",
                NeedModel = true,
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.Ollama,
                Class = TranslatePlatformClass.AI,
                Name = "Ollama (本地)",
                DefaultUrl = "http://127.0.0.1:11434/v1/chat/completions",
                DefaultModel = "qwen2.5:7b",
                Field1Label = "API Key (可空，本地无需)",
                NeedModel = true,
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.LMStudio,
                Class = TranslatePlatformClass.AI,
                Name = "LM Studio (本地)",
                DefaultUrl = "http://localhost:1234/v1/chat/completions",
                DefaultModel = "",
                Field1Label = "API Key (可空，本地无需)",
                NeedModel = false,
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.CustomChat,
                Class = TranslatePlatformClass.AI,
                Name = "自定义 (OpenAI 兼容)",
                DefaultUrl = "",
                DefaultModel = "",
                Field1Label = "API Key (可空)",
                NeedModel = true,
            },

            // ---------- 传统机器翻译类 ----------
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.Baidu,
                Class = TranslatePlatformClass.Machine,
                Name = "百度翻译",
                DefaultUrl = "http://api.fanyi.baidu.com/api/trans/vip/translate",
                Field1Label = "密钥",
                Field2Label = "App ID",
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.Aliyun,
                Class = TranslatePlatformClass.Machine,
                Name = "阿里翻译",
                DefaultUrl = "https://server.cutil.top/translate/aliyun",
                Field1Label = "AccessKey Secret",
                Field2Label = "AccessKey ID",
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.Tencent,
                Class = TranslatePlatformClass.Machine,
                Name = "腾讯翻译",
                DefaultUrl = "https://server.cutil.top/translate/tencent",
                Field1Label = "Secret Key",
                Field2Label = "Secret ID",
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.Calf,
                Class = TranslatePlatformClass.Machine,
                Name = "小牛翻译",
                DefaultUrl = "https://server.cutil.top/translate/calf",
                Field1Label = "api-key",
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.Human,
                Class = TranslatePlatformClass.Machine,
                Name = "火山翻译",
                DefaultUrl = "https://server.cutil.top/translate/human",
                Field1Label = "Secret Access",
                Field2Label = "Access Key",
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.Google,
                Class = TranslatePlatformClass.Machine,
                Name = "Google 翻译",
                DefaultUrl = "https://translate.googleapis.com/translate_a/single",
                Field1Label = "API Key (可空，免费接口无需)",
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.DeepL,
                Class = TranslatePlatformClass.Machine,
                Name = "DeepL",
                DefaultUrl = "https://api.deepl.com/v2/translate",
                Field1Label = "Auth Key",
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.Bing,
                Class = TranslatePlatformClass.Machine,
                Name = "微软翻译 (Bing)",
                DefaultUrl = "https://api-apc.cognitive.microsofttranslator.com/translate",
                Field1Label = "Subscription Key",
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.Libre,
                Class = TranslatePlatformClass.Machine,
                Name = "LibreTranslate",
                DefaultUrl = "https://libretranslate.com/translate",
                Field1Label = "API Key (可空；本地部署可填 http 地址)",
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.Yandex,
                Class = TranslatePlatformClass.Machine,
                Name = "Yandex",
                DefaultUrl = "https://translate.yandex.net/api/v1.5/tr.json/translate",
                Field1Label = "API Key",
            },
            new TranslatePlatformDef() {
                Platform = TranslatePlatform.Papago,
                Class = TranslatePlatformClass.Machine,
                Name = "Naver Papago",
                DefaultUrl = "https://naveropenapi.apigw.ntruss.com/nmt/v1/translation",
                Field2Label = "Client ID",
                Field3Label = "Client Secret",
            },
        };

        public static List<TranslatePlatformDef> GetByClass(TranslatePlatformClass cls)
        {
            return Defs.FindAll(arg => arg.Class == cls);
        }

        public static TranslatePlatformDef GetDef(TranslatePlatform platform)
        {
            return Defs.Find(arg => arg.Platform == platform);
        }
    }

    /// <summary>
    /// 标题翻译管理器：按配置的平台把文本翻译为目标语言
    /// </summary>
    public static class TranslateManager
    {
        private const string SYSTEM_PROMPT =
            "You are a professional translator. Translate the following text to the target language. " +
            "Keep it concise and faithful. Output ONLY the translated text, no explanations.";

        /// <summary>
        /// 最近一次翻译失败的具体原因（供设置页测试按钮显示；成功或未执行时为 null）
        /// </summary>
        public static string LastError { get; set; }

        public static string PlatformName(TranslatePlatform platform)
        {
            TranslatePlatformDef def = TranslatePlatforms.GetDef(platform);
            return def?.Name ?? platform.ToString();
        }

        /// <summary>
        /// 翻译文本；失败返回 null（具体原因见 LastError）
        /// </summary>
        public static async Task<string> Translate(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            TranslationConfig cfg = ConfigManager.TranslationConfig;
            if (cfg == null) {
                LastError = "翻译配置未初始化";
                return null;
            }
            TranslationPlatformSetting setting = cfg.GetSetting(cfg.Platform);
            try {
                TranslatePlatform platform = (TranslatePlatform)cfg.Platform;
                if (platform >= TranslatePlatform.OpenAI && platform <= TranslatePlatform.CustomChat)
                    return await ChatGPTCompat(text, cfg, setting, platform);
                switch (platform) {
                    case TranslatePlatform.Baidu:
                        return await Baidu(text, cfg, setting);
                    case TranslatePlatform.Aliyun:
                        return await CutilProxy(text, cfg, setting, "aliyun", $"ak_id={HttpUtility.UrlEncode(GetValue(setting.Field2))}&ak_secret={HttpUtility.UrlEncode(GetValue(setting.Field1))}");
                    case TranslatePlatform.Tencent:
                        return await CutilProxy(text, cfg, setting, "tencent", $"secret_id={HttpUtility.UrlEncode(GetValue(setting.Field2))}&secret_key={HttpUtility.UrlEncode(GetValue(setting.Field1))}");
                    case TranslatePlatform.Calf:
                        return await CutilProxy(text, cfg, setting, "calf", $"api_key={HttpUtility.UrlEncode(GetValue(setting.Field1))}");
                    case TranslatePlatform.Human:
                        return await CutilProxy(text, cfg, setting, "human", $"ak={HttpUtility.UrlEncode(GetValue(setting.Field2))}&sk={HttpUtility.UrlEncode(GetValue(setting.Field1))}");
                    case TranslatePlatform.Google:
                        return await Google(text, cfg, setting);
                    case TranslatePlatform.DeepL:
                        return await DeepL(text, cfg, setting);
                    case TranslatePlatform.Bing:
                        return await Bing(text, cfg, setting);
                    case TranslatePlatform.Libre:
                        return await Libre(text, cfg, setting);
                    case TranslatePlatform.Yandex:
                        return await Yandex(text, cfg, setting);
                    case TranslatePlatform.Papago:
                        return await Papago(text, cfg, setting);
                    default:
                        LastError = "未支持的平台";
                        return null;
                }
            } catch (Exception ex) {
                LastError = ex.Message;
                App.Logger.Error(ex);
                return null;
            }
        }

        private static string GetValue(string s)
        {
            return s ?? string.Empty;
        }

        // ---------- ChatGPT 兼容（AI 类平台统一实现） ----------

        private static async Task<string> ChatGPTCompat(string text, TranslationConfig cfg, TranslationPlatformSetting setting, TranslatePlatform platform)
        {
            TranslatePlatformDef def = TranslatePlatforms.GetDef(platform);
            string url = string.IsNullOrEmpty(setting.ApiUrl) ? (def?.DefaultUrl ?? "") : setting.ApiUrl;
            string model = string.IsNullOrEmpty(setting.Model) ? (def?.DefaultModel ?? "") : setting.Model;
            var body = new Dictionary<string, object> {
                { "model", model },
                { "messages", new object[] {
                    new Dictionary<string, object> { { "role", "system" }, { "content", SYSTEM_PROMPT } },
                    new Dictionary<string, object> { { "role", "user" }, { "content", BuildPrompt(text, cfg) } },
                } },
                { "temperature", 0.5 },
            };
            string json = await PostJson(url, body, GetValue(setting.Field1));
            if (string.IsNullOrEmpty(json)) {
                LastError = "无响应（检查 API 地址与网络/代理）";
                return null;
            }
            JObject root = JObject.Parse(json);
            string content = root["choices"]?[0]?["message"]?["content"]?.ToString();
            if (string.IsNullOrEmpty(content)) {
                LastError = root["error"]?["message"]?.ToString() ?? "响应缺少 choices[0].message.content（检查模型名/密钥）";
                return null;
            }
            LastError = null;
            return content;
        }

        // ---------- 百度 ----------

        private static async Task<string> Baidu(string text, TranslationConfig cfg, TranslationPlatformSetting setting)
        {
            string appid = GetValue(setting.Field2);
            string key = GetValue(setting.Field1);
            string salt = DateTime.Now.Ticks.ToString();
            string sign = MD5Hex(appid + text + salt + key);
            string src = MapLang(cfg.SourceLang, LangBaidu);
            string dst = MapLang(cfg.TargetLang, LangBaidu);
            string url = TranslatePlatforms.GetDef(TranslatePlatform.Baidu).DefaultUrl +
                $"?from={src}&to={dst}&appid={HttpUtility.UrlEncode(appid)}&salt={salt}&sign={sign}&q={HttpUtility.UrlEncode(text)}";
            string json = await GetString(url);
            if (string.IsNullOrEmpty(json))
                return null;
            JObject root = JObject.Parse(json);
            if (root["error_code"] != null)
                return $"error: {root["error_code"]} {root["error_msg"]}";
            JArray arr = root["trans_result"] as JArray;
            if (arr == null || arr.Count == 0)
                return null;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < arr.Count; i++) {
                if (i > 0)
                    sb.AppendLine();
                sb.Append(arr[i]["dst"]?.ToString());
            }
            return sb.ToString();
        }

        // ---------- server.cutil.top 代理（阿里/腾讯/小牛/火山） ----------

        private static async Task<string> CutilProxy(string text, TranslationConfig cfg, TranslationPlatformSetting setting, string module, string authParams)
        {
            string url = $"https://server.cutil.top/translate/{module}?{authParams}" +
                $"&source_text={HttpUtility.UrlEncode(text)}" +
                $"&source_language={HttpUtility.UrlEncode(MapLang(cfg.SourceLang, LangCutil))}" +
                $"&target_language={HttpUtility.UrlEncode(MapLang(cfg.TargetLang, LangCutil))}";
            string json = await GetString(url);
            if (string.IsNullOrEmpty(json))
                return null;
            JObject root = JObject.Parse(json);
            if (root["code"]?.ToString() != "200")
                return $"error: {root["msg"]}";
            JToken textToken = root["data"]?["text"];
            if (textToken is JArray arr) {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < arr.Count; i++) {
                    if (i > 0)
                        sb.AppendLine();
                    sb.Append(arr[i].ToString());
                }
                return sb.ToString();
            }
            return textToken?.ToString();
        }

        // ---------- Google ----------

        private static async Task<string> Google(string text, TranslationConfig cfg, TranslationPlatformSetting setting)
        {
            string src = string.IsNullOrEmpty(cfg.SourceLang) ? "auto" : cfg.SourceLang;
            string dst = cfg.TargetLang;
            string url = TranslatePlatforms.GetDef(TranslatePlatform.Google).DefaultUrl +
                $"?client=gtx&sl={HttpUtility.UrlEncode(src)}&tl={HttpUtility.UrlEncode(dst)}&dt=t&q={HttpUtility.UrlEncode(text)}";
            string json = await GetString(url);
            if (string.IsNullOrEmpty(json))
                return null;
            JArray root = JArray.Parse(json);
            if (root.Count == 0 || !(root[0] is JArray lines))
                return null;
            StringBuilder sb = new StringBuilder();
            foreach (JToken line in lines) {
                if (line is JArray arr && arr.Count > 0 && arr[0]?.Type == JTokenType.String)
                    sb.Append(arr[0].ToString());
            }
            return sb.ToString();
        }

        // ---------- DeepL ----------

        private static async Task<string> DeepL(string text, TranslationConfig cfg, TranslationPlatformSetting setting)
        {
            string baseUrl = string.IsNullOrEmpty(setting.ApiUrl) ? TranslatePlatforms.GetDef(TranslatePlatform.DeepL).DefaultUrl : setting.ApiUrl;
            string src = string.IsNullOrEmpty(cfg.SourceLang) ? "" : $"&source_lang={HttpUtility.UrlEncode(MapLang(cfg.SourceLang, LangDeepL))}";
            string url = $"{baseUrl}?text={HttpUtility.UrlEncode(text)}{src}&target_lang={HttpUtility.UrlEncode(MapLang(cfg.TargetLang, LangDeepL))}";
            string json = await GetString(url, $"DeepL-Auth-Key {GetValue(setting.Field1)}");
            if (string.IsNullOrEmpty(json))
                return null;
            JObject root = JObject.Parse(json);
            JArray arr = root["translations"] as JArray;
            if (arr == null || arr.Count == 0)
                return null;
            return arr[0]?["text"]?.ToString();
        }

        // ---------- 微软 ----------

        private static async Task<string> Bing(string text, TranslationConfig cfg, TranslationPlatformSetting setting)
        {
            string src = string.IsNullOrEmpty(cfg.SourceLang) ? "auto" : cfg.SourceLang;
            string url = TranslatePlatforms.GetDef(TranslatePlatform.Bing).DefaultUrl +
                $"?api-version=3.0&from={HttpUtility.UrlEncode(src)}&to={HttpUtility.UrlEncode(cfg.TargetLang)}";
            string json = await PostJson(url, new object[] { new Dictionary<string, string> { { "Text", text } } }, null, GetValue(setting.Field1));
            if (string.IsNullOrEmpty(json))
                return null;
            JArray root = JArray.Parse(json);
            return root[0]?["translations"]?[0]?["text"]?.ToString();
        }

        // ---------- Libre ----------

        private static async Task<string> Libre(string text, TranslationConfig cfg, TranslationPlatformSetting setting)
        {
            string url = string.IsNullOrEmpty(setting.ApiUrl) ? TranslatePlatforms.GetDef(TranslatePlatform.Libre).DefaultUrl : setting.ApiUrl;
            string key = GetValue(setting.Field1);
            if (key.StartsWith("http", StringComparison.OrdinalIgnoreCase)) {
                url = key;
                key = "";
            }
            var form = new Dictionary<string, string> {
                { "q", text },
                { "source", string.IsNullOrEmpty(cfg.SourceLang) ? "auto" : cfg.SourceLang },
                { "target", cfg.TargetLang },
                { "api_key", key },
                { "format", "text" },
            };
            string json = await PostForm(url, form);
            if (string.IsNullOrEmpty(json))
                return null;
            JObject root = JObject.Parse(json);
            if (root["error"] != null)
                return $"error: {root["error"]}";
            return root["translatedText"]?.ToString();
        }

        // ---------- Yandex ----------

        private static async Task<string> Yandex(string text, TranslationConfig cfg, TranslationPlatformSetting setting)
        {
            string src = string.IsNullOrEmpty(cfg.SourceLang) ? "auto" : cfg.SourceLang;
            string langs = $"{src}-{cfg.TargetLang}";
            string url = TranslatePlatforms.GetDef(TranslatePlatform.Yandex).DefaultUrl +
                $"?key={HttpUtility.UrlEncode(GetValue(setting.Field1))}&text={HttpUtility.UrlEncode(text)}&lang={HttpUtility.UrlEncode(langs)}";
            string json = await GetString(url);
            if (string.IsNullOrEmpty(json))
                return null;
            JObject root = JObject.Parse(json);
            JArray arr = root["text"] as JArray;
            if (arr == null || arr.Count == 0)
                return null;
            return arr[0]?.ToString();
        }

        // ---------- Papago ----------

        private static async Task<string> Papago(string text, TranslationConfig cfg, TranslationPlatformSetting setting)
        {
            string src = string.IsNullOrEmpty(cfg.SourceLang) ? "en" : cfg.SourceLang;
            var form = new Dictionary<string, string> {
                { "source", src },
                { "target", cfg.TargetLang },
                { "text", text },
            };
            string json = await PostForm(TranslatePlatforms.GetDef(TranslatePlatform.Papago).DefaultUrl, form,
                GetValue(setting.Field2), GetValue(setting.Field3));
            if (string.IsNullOrEmpty(json))
                return null;
            JObject root = JObject.Parse(json);
            if (root["errorMessage"] != null)
                return $"error: {root["errorMessage"]}";
            return root["message"]?["result"]?["translatedText"]?.ToString();
        }

        // ---------- 通用请求 ----------

        private static async Task<string> GetString(string url, string authHeader = null)
        {
            using (var client = CreateClient()) {
                if (!string.IsNullOrEmpty(authHeader))
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
                var resp = await client.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
        }

        private static async Task<string> PostJson(string url, object body, string bearerKey, string apiKey = null)
        {
            using (var client = CreateClient()) {
                if (!string.IsNullOrEmpty(bearerKey))
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {bearerKey}");
                if (!string.IsNullOrEmpty(apiKey))
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", apiKey);
                string json = JsonConvert.SerializeObject(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await client.PostAsync(url, content);
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
        }

        private static async Task<string> PostForm(string url, Dictionary<string, string> form, string clientId = null, string clientSecret = null)
        {
            using (var client = CreateClient()) {
                if (!string.IsNullOrEmpty(clientId))
                    client.DefaultRequestHeaders.TryAddWithoutValidation("X-NCP-APIGW-API-KEY-ID", clientId);
                if (!string.IsNullOrEmpty(clientSecret))
                    client.DefaultRequestHeaders.TryAddWithoutValidation("X-NCP-APIGW-API-KEY", clientSecret);
                var content = new FormUrlEncodedContent(form);
                var resp = await client.PostAsync(url, content);
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
        }

        private static HttpClient CreateClient()
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.AllowAutoRedirect = true;
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            if (ConfigManager.ProxyConfig != null) {
                var proxy = ConfigManager.ProxyConfig.GetWebProxy();
                if (proxy != null) {
                    handler.Proxy = proxy;
                    handler.UseProxy = true;
                }
            }
            HttpClient client = new HttpClient(handler, true);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return client;
        }

        // ---------- 语言映射 ----------

        private static string MapLang(string lang, Dictionary<string, string> map)
        {
            if (string.IsNullOrEmpty(lang))
                return "auto";
            if (map.TryGetValue(lang, out string v))
                return v;
            return lang;
        }

        private static readonly Dictionary<string, string> LangBaidu = new Dictionary<string, string>() {
            { "zh-CN", "zh" }, { "zh-TW", "cht" }, { "ja", "jp" }, { "ro", "rom" }, { "auto", "auto" },
        };

        private static readonly Dictionary<string, string> LangCutil = new Dictionary<string, string>() {
            { "zh-CN", "zh" }, { "zh-TW", "zh-Hant" }, { "auto", "auto" },
        };

        private static readonly Dictionary<string, string> LangDeepL = new Dictionary<string, string>() {
            { "zh-CN", "zh" }, { "zh-TW", "zh" }, { "auto", "" },
        };

        private static string BuildPrompt(string text, TranslationConfig cfg)
        {
            string src = string.IsNullOrEmpty(cfg.SourceLang) ? "" : $"from {cfg.SourceLang} ";
            return $"Translate the following text {src}to {cfg.TargetLang}, keep punctuation, give only the output without comments:\n\n{text}";
        }

        private static string MD5Hex(string input)
        {
            using (MD5 md5 = MD5.Create()) {
                byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                    sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
