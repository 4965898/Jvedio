using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommonNet.Entity;
using HtmlAgilityPack;
using Jvedio.CommonNet.Crawler;
using SuperUtils.Common;
using SuperUtils.NetWork;
using SuperUtils.NetWork.Entity;
using SuperUtils.NetWork.Enums;

namespace Jvedio.Crawler
{
    /// <summary>Restored from the bundled 1.4.0 plugin; retain its search and metadata parser.</summary>
    public class LibraryCrawler : AbstractCrawler
    {
        public const string WEB_TYPE = "library";

        private string DataCode = "";

        private Dictionary<string, object> Info = new Dictionary<string, object>();

        public LibraryCrawler()
            : base("library")
        {
        }

        public override string IsPluginAvailable(Dictionary<string, object> dict)
        {
            DataInfo = dict;
            if (DataInfo == null || DataInfo.Keys.Count == 0)
            {
                return "传入的信息不合理";
            }
            if (!DataInfo.TryGetValue("VID", out var value) || !(value is string vid) || string.IsNullOrWhiteSpace(vid))
            {
                return "必须指定 VID";
            }
            if (vid.Trim().StartsWith("fc2-", StringComparison.OrdinalIgnoreCase))
            {
                return "该刮削器不可刮削 FC2";
            }
            if (DataInfo.TryGetValue("VideoType", out var type) && type != null && !string.IsNullOrWhiteSpace(type.ToString()))
            {
                if (!Enum.TryParse<VideoType>(type.ToString(), out var videoType) || !Enum.IsDefined(typeof(VideoType), videoType))
                {
                    return "传入的信息不合理";
                }
                // New entries with only a VID default to Normal (unknown). Search the site
                // before metadata exists; do not force the saved movie type to Censored.
                if (videoType != VideoType.Normal && videoType != VideoType.Censored)
                {
                    return "该刮削器仅可刮削修正影片";
                }
            }
            return "";
        }

        private async Task<string> GetDataCode()
        {
            string url = BaseUrl + "vl_searchbyid.php?keyword=" + VID;
            Header.AllowAutoRedirect = false;
            HttpResult result;
            try
            {
                result = await HttpClient.Get(url, Header, HttpMode.String);
            }
            catch (Exception ex2)
            {
                Exception ex = ex2;
                result = new HttpResult
                {
                    Error = ex.Message
                };
            }
            if (result != null && result.StatusCode == HttpStatusCode.Found)
            {
                HttpResponseHeaders header = result.Headers;
                if (header != null && !string.IsNullOrEmpty(header.Location.ToString()))
                {
                    string Location = header.Location.ToString();
                    return Location.Replace("./?v=", "");
                }
            }
            else if (result != null && !string.IsNullOrEmpty(result.SourceCode))
            {
                return GetCodeFromSearchResult(result.SourceCode);
            }
            return "";
        }

        private string GetCodeFromSearchResult(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return "";
            }
            HtmlDocument htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);
            HtmlNodeCollection htmlNodeCollection = htmlDocument.DocumentNode.SelectNodes("//div[@class='id']");
            if (htmlNodeCollection != null)
            {
                HtmlNode htmlNode = null;
                string text = "";
                foreach (HtmlNode item in (IEnumerable<HtmlNode>)htmlNodeCollection)
                {
                    if (item == null)
                    {
                        continue;
                    }
                    text = item.InnerText;
                    if (string.IsNullOrEmpty(text) || !(text.ToUpper() == VID.ToUpper()))
                    {
                        continue;
                    }
                    htmlNode = item.ParentNode;
                    if (htmlNode != null)
                    {
                        string text2 = htmlNode.Attributes["href"]?.Value;
                        if (text2.IndexOf("=") > 0)
                        {
                            string text3 = text2.Split('=').Last();
                            if (text3.IndexOf("zh-cn") < 0)
                            {
                                return text3;
                            }
                            break;
                        }
                        break;
                    }
                    break;
                }
            }
            return "";
        }

        protected string GetCookies(string SetCookie)
        {
            if (string.IsNullOrEmpty(SetCookie))
            {
                return "";
            }
            List<string> list = new List<string>();
            List<string> list2 = SetCookie.Split(',', ';').ToList();
            foreach (string item in list2)
            {
                if (item.IndexOf('=') >= 0)
                {
                    string text = item.Split('=')[0];
                    string text2 = item.Split('=')[1];
                    if (text == "__cfduid" || text == "__qca")
                    {
                        list.Add(text + "=" + text2);
                    }
                }
            }
            list.Add("over18=18");
            return string.Join(";", list);
        }

        public override void ParseDataInfo()
        {
            if (DataInfo == null)
            {
                return;
            }
            if (DataInfo.Get("VID", "") is string text && DataInfo.Get("Url", "") is string text2 && !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text2))
            {
                BaseUrl = text2;
                VID = text.ToUpper();
                if (!BaseUrl.EndsWith("/"))
                {
                    BaseUrl += "/";
                }
                Logs.Add("BaseUrl: " + BaseUrl);
            }
            if (DataInfo.ContainsKey("Header") && DataInfo["Header"] is Dictionary<string, string> headers)
            {
                Header.Headers = headers;
            }
            if (DataInfo.ContainsKey("UrlCode") && DataInfo["UrlCode"] is string value)
            {
                Dictionary<string, string> dictionary = JsonUtils.TryDeserializeObject<Dictionary<string, string>>(value);
                if (dictionary != null && dictionary.TryGetValue("RemoteValue", out var value2) && !string.IsNullOrEmpty(value2))
                {
                    DataCode = value2;
                }
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
            {
                DataCode = await GetDataCode();
            }
            if (!Info.ContainsKey("DataCode"))
            {
                Info.Add("DataCode", DataCode);
            }
            if (string.IsNullOrEmpty(DataCode))
            {
                Info.Add("Error", HttpStatusCode.NotFound);
                return Info;
            }
            string url = BaseUrl + "?v=" + DataCode;
            if (!Info.ContainsKey("DataCode"))
            {
                Info.Add("DataCode", DataCode);
            }
            try
            {
                Logs.Add("get: " + url);
                Logs.Add($"header: {Header}");
                httpResult = await HttpClient.Get(url, Header, HttpMode.String);
            }
            catch (Exception ex)
            {
                httpResult = new HttpResult();
                httpResult.Error = ex.Message;
            }
            if (httpResult.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(httpResult.SourceCode))
            {
                HtmlText = httpResult.SourceCode;
                Parse();
                Info.Add("WebUrl", url);
                Info.Add("WebType", WebType);
                Task.Delay(300).Wait();
            }
            if (!Info.ContainsKey("StatusCode"))
            {
                Info.Add("StatusCode", httpResult.StatusCode);
            }
            if (!Info.ContainsKey("Error") && !string.IsNullOrEmpty(httpResult.Error))
            {
                Info.Add("Error", httpResult.Error);
            }
            if (!Info.ContainsKey("Error") && httpResult.StatusCode != HttpStatusCode.OK)
            {
                Info.Add("Error", httpResult.StatusCode);
            }
            Info.Add("Logs", Logs);
            return Info;
        }

        public void Parse()
        {
            if (string.IsNullOrEmpty(HtmlText))
            {
                return;
            }
            HtmlDocument htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(HtmlText);
            string text = "";
            HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("//h3[@class='post-title text']/a");
            if (htmlNode != null)
            {
                text = htmlNode.InnerText.Split(' ')[0].ToUpper();
                Info.Add("Title", htmlNode.InnerText.ToUpper().Replace(text, "").Substring(1));
            }
            HtmlNodeCollection htmlNodeCollection = htmlDocument.DocumentNode.SelectNodes("//div[@id='video_info']/div/table/tr");
            if (htmlNodeCollection != null)
            {
                foreach (HtmlNode item in (IEnumerable<HtmlNode>)htmlNodeCollection)
                {
                    if (item == null)
                    {
                        continue;
                    }
                    string innerText = item.InnerText;
                    string text2 = "";
                    HtmlNode htmlNode2 = null;
                    HtmlNodeCollection htmlNodeCollection2 = null;
                    if (innerText.IndexOf("发行日期") >= 0)
                    {
                        htmlNodeCollection2 = item.SelectNodes("td");
                        if (htmlNodeCollection2 != null && htmlNodeCollection2.Count != 0)
                        {
                            text2 = htmlNodeCollection2[1].InnerText;
                            Info.Add("ReleaseDate", text2);
                        }
                    }
                    else if (innerText.IndexOf("长度") >= 0)
                    {
                        htmlNode2 = item.SelectSingleNode("td/span");
                        if (htmlNode2 != null)
                        {
                            text2 = htmlNode2.InnerText;
                            Info.Add("Duration", text2);
                        }
                    }
                    else if (innerText.IndexOf("导演") >= 0)
                    {
                        htmlNode2 = item.SelectSingleNode("td/span/a");
                        if (htmlNode2 != null)
                        {
                            text2 = htmlNode2.InnerText;
                            Info.Add("Director", text2);
                        }
                    }
                    else if (innerText.IndexOf("发行商") >= 0)
                    {
                        htmlNode2 = item.SelectSingleNode("td/span/a");
                        if (htmlNode2 != null)
                        {
                            text2 = htmlNode2.InnerText;
                            Info.Add("Studio", text2);
                        }
                    }
                    else if (innerText.IndexOf("使用者评价:") >= 0)
                    {
                        htmlNode2 = htmlDocument.DocumentNode.SelectSingleNode("//span[@class='score']");
                        if (htmlNode2 != null)
                        {
                            text2 = htmlNode2.InnerText;
                            Match match = Regex.Match(text2, "([0-9]|\\.)+");
                            if (match != null)
                            {
                                double.TryParse(match.Value, out var result);
                                Info.Add("Rating", Math.Ceiling(result * 10.0).ToString());
                            }
                        }
                    }
                    else if (innerText.IndexOf("类别") >= 0)
                    {
                        HtmlNodeCollection htmlNodeCollection3 = item.SelectNodes("td/span/a");
                        if (htmlNodeCollection3 == null)
                        {
                            continue;
                        }
                        List<string> list = new List<string>();
                        foreach (HtmlNode item2 in (IEnumerable<HtmlNode>)htmlNodeCollection3)
                        {
                            list.Add(item2.InnerText);
                        }
                        Info.Add("Genre", list);
                    }
                    else
                    {
                        if (innerText.IndexOf("演员") < 0)
                        {
                            continue;
                        }
                        HtmlNodeCollection htmlNodeCollection4 = item.SelectNodes("td/span/span/a");
                        if (htmlNodeCollection4 == null)
                        {
                            continue;
                        }
                        List<string> list2 = new List<string>();
                        foreach (HtmlNode item3 in (IEnumerable<HtmlNode>)htmlNodeCollection4)
                        {
                            list2.Add(item3.InnerText);
                        }
                        Info.Add("ActorNames", list2);
                    }
                }
            }
            HtmlNode htmlNode3 = htmlDocument.DocumentNode.SelectSingleNode("//img[@id='video_jacket_img']");
            if (htmlNode3 != null)
            {
                string text3 = "http:" + htmlNode3.Attributes["src"].Value;
                Info.Add("BigImageUrl", text3);
                Info.Add("SmallImageUrl", text3.Replace("pl.jpg", "ps.jpg"));
            }
            HtmlNodeCollection htmlNodeCollection5 = htmlDocument.DocumentNode.SelectNodes("//div[@class='previewthumbs']/img");
            if (htmlNodeCollection5 == null)
            {
                return;
            }
            List<string> list3 = new List<string>();
            foreach (HtmlNode item4 in (IEnumerable<HtmlNode>)htmlNodeCollection5)
            {
                if (item4 == null)
                {
                    continue;
                }
                string text4 = item4.Attributes["src"].Value;
                if (!text4.StartsWith("https"))
                {
                    text4 = "https" + text4;
                }
                if (!text4.IsProperUrl())
                {
                    continue;
                }
                string fileName = Path.GetFileName(new Uri(text4).LocalPath);
                string text5 = text4.Replace(fileName, "");
                string[] array = fileName.Split('-');
                if (array.Length >= 2)
                {
                    if (array.First().EndsWith("jp"))
                    {
                        list3.Add(text4);
                        continue;
                    }
                    array[0] += "jp";
                    list3.Add(text5 + string.Join("-", array));
                }
            }
            Info.Add("ExtraImageUrl", list3);
        }
    }
}
