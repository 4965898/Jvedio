using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommonNet.Entity;
using HtmlAgilityPack;
using Jvedio.CommonNet.Crawler;
using SuperUtils.Common;
using SuperUtils.NetWork;
using SuperUtils.NetWork.Entity;
using SuperUtils.NetWork.Enums;
using SuperUtils.Values;

namespace Jvedio.Crawler
{
    /// <summary>
    /// JavDB（db）爬虫。此工程由原二进制 DBCrawler.dll 反编译还原为源码后重建，
    /// 保留原全部逻辑（搜索拿 DataCode、磁力解析、FC2 处理、图片/演员/类别等），
    /// 并补充「發行」（发行商/Publisher）字段解析（原二进制插件未解析该字段）。
    /// </summary>
    public class DBCrawler : AbstractCrawler
    {
        public const string WEB_TYPE = "db";

        public const string ACTOR_BASE_URL = "https://c0.jdbstatic.com/avatars/{0}/{1}.jpg";

        private bool FC2 = false;

        private string VID = "";

        private string DataCode = "";

        private string Host = "";

        private Dictionary<string, object> Info = new Dictionary<string, object>();

        public DBCrawler()
            : base("db")
        {
        }

        public override string IsPluginAvailable(Dictionary<string, object> dict)
        {
            DataInfo = dict;
            if (DataInfo == null || DataInfo.Keys.Count == 0)
                return "传入的信息不合理";
            if (DataInfo.TryGetValue("VID", out object value) && value is string value2 && !string.IsNullOrEmpty(value2) && DataInfo.TryGetValue("VideoType", out object value3) && Enum.TryParse<VideoType>(value3.ToString(), out VideoType _))
                return "";
            return "必须指定 VID";
        }

        private async Task<string> GetDataCode()
        {
            string url = BaseUrl + "search?q=" + VID + "&f=all";
            Header.AllowAutoRedirect = false;
            HttpResult result;
            try {
                result = await HttpClient.Get(url, Header, (HttpMode)0);
            } catch (Exception ex2) {
                Exception ex = ex2;
                result = new HttpResult();
                result.Error = ex.Message;
            }
            if (result != null && result.StatusCode == HttpStatusCode.Found) {
                if (!Info.ContainsKey("Error"))
                    Info.Add("Error", "检索太频繁");
                else
                    Info["Error"] = "检索太频繁";
            }
            if (result != null && !string.IsNullOrEmpty(result.SourceCode))
                return await GetCodeFromSearchResult(result.SourceCode);
            return "";
        }

        public async Task<string> GetCodeFromSearchResult(string content)
        {
            string code = "";
            if (string.IsNullOrEmpty(content))
                return "";
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(content);
            HtmlNodeCollection gridNodes = doc.DocumentNode.SelectNodes("//div[@class='item']");
            if (gridNodes != null) {
                foreach (HtmlNode gridNode in gridNodes) {
                    HtmlNode uidNode = gridNode.SelectSingleNode("//a/div[@class='video-title']/strong");
                    if (uidNode == null || !uidNode.InnerText.ToUpper().Equals(VID.ToUpper()))
                        continue;
                    HtmlAttribute obj = uidNode.ParentNode.ParentNode.Attributes["href"];
                    string dataCode = (obj != null) ? obj.Value : null;
                    if (!string.IsNullOrEmpty(dataCode))
                        code = dataCode.Replace("/v/", "");
                    string date = "";
                    string title = "";
                    string BigImageUrl = "";
                    HtmlNodeCollection nodes = uidNode.ParentNode.ParentNode.SelectNodes("div");
                    foreach (HtmlNode node in nodes) {
                        HtmlAttribute obj2 = node.Attributes["class"];
                        string className = (obj2 != null) ? obj2.Value : null;
                        if (string.IsNullOrEmpty(className))
                            continue;
                        if (className.Equals("cover") || className.Equals("cover ")) {
                            HtmlNode obj3 = node.SelectSingleNode("img");
                            object obj4 = (obj3 != null) ? obj3.Attributes["src"].Value : null;
                            BigImageUrl = (string)obj4;
                        } else if (className.Equals("video-title")) {
                            title = node.InnerText;
                            string vid = VID.ToUpper() + " ";
                            title = title.Replace(vid, "");
                        } else if (className.Equals("meta")) {
                            date = node.InnerText.Trim();
                        } else {
                            if (!className.Equals("score"))
                                continue;
                            HtmlNode span = node.SelectSingleNode("span");
                            if (span == null)
                                continue;
                            content = node.InnerText;
                            if (content == "N/A")
                                continue;
                            Match match = Regex.Match(content, "([0-9]|\\.)+分");
                            if (match != null) {
                                string rating = match.Value.Replace("分", "");
                                double.TryParse(rating, out double rate);
                                if (!Info.ContainsKey("Rating"))
                                    Info.Add("Rating", Math.Ceiling(rate * 20.0).ToString());
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(date) && !Info.ContainsKey("ReleaseDate"))
                        Info.Add("ReleaseDate", date);
                    if (!string.IsNullOrEmpty(title) && !Info.ContainsKey("Title"))
                        Info.Add("Title", title);
                    if (!string.IsNullOrEmpty(BigImageUrl)) {
                        if (!Info.ContainsKey("BigImageUrl"))
                            Info.Add("BigImageUrl", BigImageUrl);
                        if (!Info.ContainsKey("SmallImageUrl"))
                            Info.Add("SmallImageUrl", BigImageUrl.Replace("covers", "thumbs"));
                    }
                    break;
                }
            }
            await Task.Delay(1);
            return code;
        }

        public override void ParseDataInfo()
        {
            if (DataInfo == null)
                return;
            if (DataHelper.Get<string, object>(DataInfo, "VID", (object)"") is string text && DataHelper.Get<string, object>(DataInfo, "Url", (object)"") is string text2 && !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text2)) {
                BaseUrl = text2;
                VID = text.ToUpper();
                if (!BaseUrl.EndsWith("/"))
                    BaseUrl = BaseUrl + "/";
                Logs.Add("BaseUrl: " + BaseUrl);
                Host = new Uri(BaseUrl).GetLeftPart(UriPartial.Authority);
                FC2 = VID.IndexOf("FC2-") >= 0;
            }
            if (DataInfo.ContainsKey("Header") && DataInfo["Header"] is Dictionary<string, string> headers)
                Header.Headers = headers;
            if (DataInfo.ContainsKey("UrlCode") && DataInfo["UrlCode"] is string text3) {
                Dictionary<string, string> dictionary = JsonUtils.TryDeserializeObject<Dictionary<string, string>>(text3);
                if (dictionary != null && dictionary.TryGetValue("RemoteValue", out string value) && !string.IsNullOrEmpty(value))
                    DataCode = value;
            }
        }

        public override async Task<Dictionary<string, object>> GetInfo(RequestHeader header, Dictionary<string, object> dict)
        {
            Header = header;
            Header.Headers["Cache-Control"] = "no-cache";
            DataInfo = dict;
            ParseDataInfo();
            Logs.Add(string.Format("crawler recv vid: {0}", dict["VID"]));
            if (string.IsNullOrEmpty(DataCode))
                DataCode = await GetDataCode();
            if (!Info.ContainsKey("DataCode"))
                Info.Add("DataCode", DataCode);
            if (string.IsNullOrEmpty(DataCode) && !Info.ContainsKey("Error")) {
                Info.Add("Error", HttpStatusCode.NotFound);
                return Info;
            }
            string url = BaseUrl + "v/" + DataCode;
            Header.AllowAutoRedirect = true;
            try {
                Logs.Add("get: " + url);
                Logs.Add(string.Format("header: {0}", Header));
                httpResult = await HttpClient.Get(url, Header, (HttpMode)0);
            } catch (Exception ex) {
                httpResult = new HttpResult();
                httpResult.Error = ex.Message;
            }
            if (!Info.ContainsKey("StatusCode"))
                Info.Add("StatusCode", httpResult.StatusCode);
            if (httpResult.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(httpResult.SourceCode)) {
                HtmlText = httpResult.SourceCode;
                if (HtmlText.IndexOf("開通VIP") > 0) {
                    httpResult.Error = "開通VIP";
                } else if (HtmlText.IndexOf("此內容需要登入才能查看或操作") > 0) {
                    httpResult.Error = "此內容需要登入才能查看或操作";
                } else {
                    List<Dictionary<string, object>> magnets = ParseMagnet();
                    await Parse();
                    if (!Info.ContainsKey("Magnets"))
                        Info.Add("Magnets", magnets);
                }
                if (!Info.ContainsKey("WebUrl"))
                    Info.Add("WebUrl", url);
                if (!Info.ContainsKey("WebType"))
                    Info.Add("WebType", "db");
                Task.Delay(300).Wait();
            }
            if (!Info.ContainsKey("Error") && !string.IsNullOrEmpty(httpResult.Error))
                Info.Add("Error", httpResult.Error);
            if (!Info.ContainsKey("Error") && httpResult.StatusCode != HttpStatusCode.OK)
                Info.Add("Error", httpResult.StatusCode);
            Info.Add("Logs", Logs);
            return Info;
        }

        protected string GetCookies(string SetCookie)
        {
            return SetCookie;
        }

        private async Task<Dictionary<string, object>> Parse()
        {
            if (string.IsNullOrEmpty(HtmlText))
                return null;
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(HtmlText);
            HtmlNode titleNode = doc.DocumentNode.SelectSingleNode("//title");
            if (titleNode != null && !Info.ContainsKey("Title")) {
                string title = titleNode.InnerText.Replace(VID, "").Substring(1);
                Info.Add("Title", title.Substring(0, title.Length - " | JavDB 成人影片數據庫 ".Length));
            }
            HtmlNodeCollection infoNodes = doc.DocumentNode.SelectNodes("//nav[@class='panel movie-panel-info']/div");
            if (infoNodes != null) {
                foreach (HtmlNode infoNode in infoNodes) {
                    if (infoNode == null)
                        continue;
                    string headerText = infoNode.InnerText;
                    if (headerText.IndexOf("時間") >= 0 || headerText.IndexOf("日期") >= 0) {
                        HtmlNode node = infoNode.SelectSingleNode("span");
                        if (node != null) {
                            string content = node.InnerText;
                            if (content != "N/A" && !Info.ContainsKey("ReleaseDate"))
                                Info.Add("ReleaseDate", content);
                        }
                    } else if (infoNode.InnerText.IndexOf("時長") >= 0) {
                        HtmlNode node = infoNode.SelectSingleNode("span");
                        if (node == null)
                            continue;
                        string content = node.InnerText;
                        if (content != "N/A") {
                            Match match = Regex.Match(content, "[0-9]+");
                            if (match != null && !Info.ContainsKey("Duration"))
                                Info.Add("Duration", match.Value);
                        }
                    } else if (infoNode.InnerText.IndexOf("導演") >= 0) {
                        HtmlNode node = infoNode.SelectSingleNode("span/a");
                        if (node != null) {
                            string content = node.InnerText;
                            if (content != "N/A" && !Info.ContainsKey("Director"))
                                Info.Add("Director", content);
                        }
                    } else if (infoNode.InnerText.IndexOf("評分") >= 0) {
                        HtmlNode node = infoNode.SelectSingleNode("span");
                        if (node == null)
                            continue;
                        string content = node.InnerText;
                        if (content == "N/A")
                            continue;
                        Match match2 = Regex.Match(content, "([0-9]|\\.)+分");
                        if (match2 != null) {
                            string rating = match2.Value.Replace("分", "");
                            double.TryParse(rating, out double rate);
                            if (!Info.ContainsKey("Rating"))
                                Info.Add("Rating", Math.Ceiling(rate * 20.0).ToString());
                        }
                    } else if (infoNode.InnerText.IndexOf("類別") >= 0) {
                        HtmlNodeCollection genreNodes = infoNode.SelectNodes("span/a");
                        if (genreNodes == null || genreNodes.Count <= 0)
                            continue;
                        List<string> genres = new List<string>();
                        foreach (HtmlNode genreNode in genreNodes) {
                            if (genreNode != null)
                                genres.Add(genreNode.InnerText);
                        }
                        if (!Info.ContainsKey("Genre"))
                            Info.Add("Genre", string.Join(ConstValues.SeparatorString, genres));
                    } else if (infoNode.InnerText.IndexOf("片商") >= 0) {
                        HtmlNode node = infoNode.SelectSingleNode("span/a");
                        if (node != null) {
                            string content = node.InnerText;
                            if (content != "N/A" && !Info.ContainsKey("Studio"))
                                Info.Add("Studio", content);
                        }
                    } else if (infoNode.InnerText.IndexOf("發行") >= 0) {
                        // 发行商（JavDB 页面「發行」字段，链接 /publishers/）
                        HtmlNode node = infoNode.SelectSingleNode("span/a");
                        if (node != null) {
                            string content = node.InnerText;
                            if (content != "N/A" && !Info.ContainsKey("Publisher"))
                                Info.Add("Publisher", content);
                        }
                    } else if (infoNode.InnerText.IndexOf("系列") >= 0) {
                        HtmlNode node = infoNode.SelectSingleNode("span/a");
                        if (node != null) {
                            string content = node.InnerText;
                            if (content != "N/A" && !Info.ContainsKey("Series"))
                                Info.Add("Series", content);
                        }
                    } else {
                        if (infoNode.InnerText.IndexOf("演員") < 0)
                            continue;
                        HtmlNodeCollection actressNodes = infoNode.SelectNodes("span/a");
                        if (actressNodes == null)
                            continue;
                        List<string> actress = new List<string>();
                        List<string> actressId = new List<string>();
                        foreach (HtmlNode actressNode in actressNodes) {
                            if (actressNode != null) {
                                actress.Add(actressNode.InnerText);
                                HtmlAttribute obj = actressNode.Attributes["href"];
                                string id = (obj != null) ? obj.Value : null;
                                if (!string.IsNullOrEmpty(id) && id.IndexOf("/") >= 0)
                                    actressId.Add(id.Split('/').LastOrDefault());
                            }
                        }
                        if (!Info.ContainsKey("ActorNames"))
                            Info.Add("ActorNames", actress);
                        if (!Info.ContainsKey("ActressImageUrl"))
                            Info.Add("ActressImageUrl", ActorIdToUrl(actressId));
                    }
                }
            }
            HtmlNode bigimageNode = doc.DocumentNode.SelectSingleNode("//img[@class='video-cover']");
            if (bigimageNode != null) {
                string BigImageUrl = bigimageNode.Attributes["src"].Value;
                if (!Info.ContainsKey("BigImageUrl"))
                    Info.Add("BigImageUrl", BigImageUrl);
                if (VID.IndexOf("FC2-") < 0 && !Info.ContainsKey("SmallImageUrl"))
                    Info.Add("SmallImageUrl", BigImageUrl.Replace("covers", "thumbs"));
            }
            HtmlNodeCollection extrapicNodes = doc.DocumentNode.SelectNodes("//a[@class='tile-item']");
            if (extrapicNodes != null) {
                List<string> extraimage = new List<string>();
                foreach (HtmlNode extrapicNode in extrapicNodes) {
                    HtmlAttribute obj2 = extrapicNode.Attributes["href"];
                    string link = (obj2 != null) ? obj2.Value : null;
                    if (!string.IsNullOrEmpty(link) && link.IndexOf("/v/") < 0)
                        extraimage.Add(link);
                }
                if (!Info.ContainsKey("ExtraImageUrl"))
                    Info.Add("ExtraImageUrl", extraimage);
            }
            await Task.Delay(1);
            return Info;
        }

        public List<string> ActorIdToUrl(List<string> actorId)
        {
            List<string> list = new List<string>();
            if (actorId == null || actorId.Count == 0)
                return list;
            foreach (string item in actorId) {
                list.Add(string.Format("https://c0.jdbstatic.com/avatars/{0}/{1}.jpg", item.Substring(0, 2).ToLower(), item));
            }
            return list;
        }

        public List<Dictionary<string, object>> ParseMagnet()
        {
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            HtmlDocument val = new HtmlDocument();
            val.LoadHtml(HtmlText);
            HtmlNodeCollection val2 = val.DocumentNode.SelectNodes("//div[@id='magnets-content']/table/tr");
            if (val2 == null)
                return list;
            foreach (HtmlNode item in val2) {
                HtmlNodeCollection val3 = item.SelectNodes("td");
                if (val3 == null || val3.Count != 3)
                    continue;
                Dictionary<string, object> dictionary = new Dictionary<string, object>();
                HtmlNode val4 = val3[0].SelectSingleNode("a");
                HtmlAttribute obj = val4.Attributes["href"];
                string value = (obj != null) ? obj.Value : null;
                if (!string.IsNullOrEmpty(value))
                    dictionary.Add("MagnetLink", value);
                HtmlNodeCollection val5 = val4.SelectNodes("span");
                string innerText = val5[0].InnerText;
                if (!string.IsNullOrEmpty(innerText))
                    dictionary.Add("Title", innerText);
                List<string> list2 = new List<string>();
                for (int i = 1; i < val5.Count; i++)
                    list2.Add(val5[i].InnerText);
                List<string> list3 = new List<string>();
                long num = 0L;
                foreach (string item2 in list2) {
                    if (item2.IndexOf("GB") > 0) {
                        Regex regex = new Regex("\\d+\\.?\\d+GB");
                        Match match = regex.Match(item2);
                        if (match.Success && match.Value.Length > 0) {
                            double.TryParse(match.Value.Replace("GB", ""), out double result);
                            num = (long)(result * 1024.0 * 1024.0);
                        }
                    } else if (item2.IndexOf("MB") > 0) {
                        Regex regex2 = new Regex("\\d+\\.?\\d+MB");
                        Match match2 = regex2.Match(item2);
                        if (match2.Success && match2.Value.Length > 0) {
                            double.TryParse(match2.Value.Replace("MB", ""), out double result2);
                            num = (long)(result2 * 1024.0);
                        }
                    } else {
                        list3.Add(item2);
                    }
                }
                dictionary.Add("Size", num);
                dictionary.Add("Tags", list3);
                string innerText2 = val3[1].SelectSingleNode("span").InnerText;
                if (!string.IsNullOrEmpty(innerText2))
                    dictionary.Add("ReleaseDate", innerText2);
                list.Add(dictionary);
            }
            return list;
        }
    }
}
