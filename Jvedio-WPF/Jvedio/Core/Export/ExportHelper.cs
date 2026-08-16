using Jvedio.Core.Enums;
using Jvedio.Entity;
using Jvedio.Mapper;
using Newtonsoft.Json;
using SuperUtils.Framework.ORM.Wrapper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using static Jvedio.App;
using static Jvedio.MapperManager;

namespace Jvedio.Core.Export
{
    /// <summary>
    /// 数据导出公共入口（hitchao/Jvedio#346/#212）：
    /// 支持 CSV / Excel(SpreadsheetML 2003) / JSON 三种格式，影片与演员共用。
    /// 所有导出方法均为纯后台逻辑，调用方负责 SaveFileDialog 与 Task.Run。
    /// </summary>
    public static class ExportHelper
    {
        public enum ExportFormat
        {
            Csv = 1,
            Excel = 2,
            Json = 3,
        }

        private static readonly string[] VIDEO_HEADERS = {
            "识别码", "标题", "中文标题", "演员", "评分", "发行日期",
            "大小(MB)", "类型", "系列", "导演", "厂牌", "路径"
        };

        private static readonly string[] VIDEO_KEYS = {
            "VID", "Title", "TitleCN", "ActorNames", "Grade", "ReleaseDate",
            "SizeMB", "Genre", "Series", "Director", "Studio", "Path"
        };

        private static readonly string[] ACTOR_HEADERS = {
            "ActorID", "演员名", "出演数", "国家", "出生地", "生日", "身高(CM)", "体重(KG)",
            "罩杯", "胸围", "腰围", "臀围", "鞋码", "评分", "WebUrl"
        };

        private static readonly string[] ACTOR_KEYS = {
            "ActorID", "ActorName", "Count", "Country", "BirthPlace", "Birthday", "Height", "Weight",
            "Cup", "Chest", "Waist", "Hipline", "ShoeSize", "Grade", "WebUrl"
        };

        /// <summary>
        /// 导出影片数据（sql 需自行组装，如全库或带筛选）
        /// </summary>
        public static int ExportVideos(string savePath, ExportFormat format, string sql)
        {
            List<Dictionary<string, object>> list = metaDataMapper.Select(sql);
            List<Dictionary<string, object>> rows = BuildVideoRows(list);
            WriteRows(savePath, format, VIDEO_HEADERS, VIDEO_KEYS, rows);
            return rows.Count;
        }

        /// <summary>
        /// 导出演员数据（sql 需自行组装，如全库或带搜索）
        /// </summary>
        public static int ExportActors(string savePath, ExportFormat format, string sql)
        {
            List<Dictionary<string, object>> list = actorMapper.Select(sql);
            List<Dictionary<string, object>> rows = BuildActorRows(list);
            WriteRows(savePath, format, ACTOR_HEADERS, ACTOR_KEYS, rows);
            return rows.Count;
        }

        /// <summary>
        /// 影片行：统一字段 + 大小换算 MB
        /// </summary>
        private static List<Dictionary<string, object>> BuildVideoRows(List<Dictionary<string, object>> list)
        {
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            if (list == null)
                return rows;
            foreach (var dict in list) {
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in VIDEO_KEYS) {
                    if (key == "SizeMB") {
                        object size = dict.TryGetValue("Size", out object s) ? s : null;
                        row[key] = size is long || size is int || size is double
                            ? (Convert.ToDouble(size) / 1024.0 / 1024.0).ToString("F1")
                            : "";
                    } else {
                        row[key] = dict.TryGetValue(key, out object v) ? v : null;
                    }
                }
                rows.Add(row);
            }
            return rows;
        }

        private static List<Dictionary<string, object>> BuildActorRows(List<Dictionary<string, object>> list)
        {
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            if (list == null)
                return rows;
            foreach (var dict in list) {
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in ACTOR_KEYS)
                    row[key] = dict.TryGetValue(key, out object v) ? v : null;
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>
        /// 按格式写文件
        /// </summary>
        private static void WriteRows(string savePath, ExportFormat format, string[] headers, string[] keys, List<Dictionary<string, object>> rows)
        {
            switch (format) {
                case ExportFormat.Csv:
                    WriteCsv(savePath, headers, keys, rows);
                    break;
                case ExportFormat.Excel:
                    WriteExcel(savePath, headers, keys, rows);
                    break;
                case ExportFormat.Json:
                    WriteJson(savePath, headers, keys, rows);
                    break;
            }
        }

        private static void WriteCsv(string savePath, string[] headers, string[] keys, List<Dictionary<string, object>> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(CsvEscape)));
            foreach (var row in rows) {
                sb.AppendLine(string.Join(",", keys.Select(k => CsvEscape(row[k]))));
            }
            // UTF-8 BOM，Excel 直接打开不乱码
            File.WriteAllText(savePath, sb.ToString(), new UTF8Encoding(true));
        }

        /// <summary>
        /// Excel 使用 SpreadsheetML 2003（纯 XML，无第三方依赖，Excel 原生打开无警告）
        /// </summary>
        private static void WriteExcel(string savePath, string[] headers, string[] keys, List<Dictionary<string, object>> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" " +
                "xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            sb.AppendLine("<Worksheet ss:Name=\"Sheet1\"><Table>");
            // 表头
            sb.Append("<Row>");
            foreach (var h in headers)
                sb.Append($"<Cell><Data ss:Type=\"String\">{XmlEscape(h)}</Data></Cell>");
            sb.AppendLine("</Row>");
            // 数据
            foreach (var row in rows) {
                sb.Append("<Row>");
                foreach (var k in keys)
                    sb.Append($"<Cell><Data ss:Type=\"String\">{XmlEscape(ToString(row[k]))}</Data></Cell>");
                sb.AppendLine("</Row>");
            }
            sb.AppendLine("</Table></Worksheet></Workbook>");
            File.WriteAllText(savePath, sb.ToString(), new UTF8Encoding(false));
        }

        private static void WriteJson(string savePath, string[] headers, string[] keys, List<Dictionary<string, object>> rows)
        {
            // 中文表头作为 JSON key，便于阅读
            List<Dictionary<string, object>> jsonRows = new List<Dictionary<string, object>>();
            foreach (var row in rows) {
                var dict = new Dictionary<string, object>();
                for (int i = 0; i < keys.Length; i++)
                    dict[headers[i]] = row[keys[i]];
                jsonRows.Add(dict);
            }
            string json = JsonConvert.SerializeObject(jsonRows, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(savePath, json, new UTF8Encoding(false));
        }

        private static string ToString(object value)
        {
            return value == null ? "" : value.ToString();
        }

        private static string CsvEscape(object value)
        {
            string s = ToString(value);
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static string XmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        #region "NFO 导出（参考 sqlite2nfo.py，Kodi/Jellyfin/Emby 标准）"

        /// <summary>
        /// 导出影片为 NFO 文件（每部一个 .nfo，输出到指定目录）。
        /// 字段与空标签策略对齐脚本：source/plot/director/rating/criticrating/year/mpaa/customrating/
        /// countrycode 强制输出（空标签展开），premiered/release/runtime/country/studio/id/num/genre/tag/
        /// thumb/fanart/actor(name+thumb)。
        /// </summary>
        /// <param name="outputDir">输出目录（不存在自动创建）</param>
        /// <param name="sql">影片查询（需含 VideoMapper.SelectFields 全部字段）</param>
        /// <param name="actorSql">演员查询（需含 DataID, ActorName, ImageUrl）</param>
        /// <param name="dbId">当前库 ID（用于 fallback actor 映射）</param>
        public static int ExportVideosToNfo(string outputDir, string sql, long dbId)
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            List<Video> videos = ToVideoList(sql);
            Dictionary<long, List<KeyValuePair<string, string>>> actorMap = LoadActorMap(dbId);

            int count = 0;
            foreach (Video video in videos) {
                try {
                    WriteOneNfo(outputDir, video, actorMap);
                    count++;
                } catch (Exception ex) {
                    Logger.Error($"导出 NFO 失败 DataID={video.DataID}: {ex.Message}");
                }
            }
            return count;
        }

        private static List<Video> ToVideoList(string sql)
        {
            List<Dictionary<string, object>> list = metaDataMapper.Select(sql);
            return metaDataMapper.ToEntity<Video>(list, typeof(Video).GetProperties(), false) ?? new List<Video>();
        }

        /// <summary>
        /// 一次性加载当前库全部 影片-演员 映射（DataID → [名字, 头像URL]）
        /// </summary>
        private static Dictionary<long, List<KeyValuePair<string, string>>> LoadActorMap(long dbId)
        {
            Dictionary<long, List<KeyValuePair<string, string>>> map = new Dictionary<long, List<KeyValuePair<string, string>>>();
            string sql = "SELECT metadata_to_actor.DataID, actor_info.ActorName, actor_info.ImageUrl " +
                "FROM metadata_to_actor JOIN actor_info ON metadata_to_actor.ActorID=actor_info.ActorID " +
                $"JOIN metadata ON metadata.DataID=metadata_to_actor.DataID WHERE metadata.DBId={dbId} and metadata.DataType=0";
            try {
                List<Dictionary<string, object>> list = actorMapper.Select(sql);
                if (list != null) {
                    foreach (var dict in list) {
                        if (!long.TryParse(ToString(dict.TryGetValue("DataID", out object d) ? d : null), out long dataId))
                            continue;
                        string name = ToString(dict.TryGetValue("ActorName", out object n) ? n : null);
                        string thumb = ToString(dict.TryGetValue("ImageUrl", out object t) ? t : null);
                        if (!map.TryGetValue(dataId, out List<KeyValuePair<string, string>> arr)) {
                            arr = new List<KeyValuePair<string, string>>();
                            map[dataId] = arr;
                        }
                        arr.Add(new KeyValuePair<string, string>(name, thumb));
                    }
                }
            } catch (Exception ex) {
                Logger.Error(ex);
            }
            return map;
        }

        private static void WriteOneNfo(string outputDir, Video video, Dictionary<long, List<KeyValuePair<string, string>>> actorMap)
        {
            string nfoId = string.IsNullOrEmpty(video.VID) ? video.DataID.ToString() : video.VID;
            string releaseDate = FormatToYmd(video.ReleaseDate);
            string year = video.ReleaseYear.ToString();
            if (string.IsNullOrEmpty(year) || year == "0")
                year = releaseDate.Length >= 4 ? releaseDate.Substring(0, 4) : "";

            Dictionary<string, object> urls = new Dictionary<string, object>();
            try {
                if (!string.IsNullOrEmpty(video.ImageUrls))
                    urls = JsonConvert.DeserializeObject<Dictionary<string, object>>(video.ImageUrls) ?? urls;
            } catch { }

            // 缩略图：本地优先，无本地回退在线链接（参考脚本 thumb_candidates 顺序）
            string localBig = LocalImageOrNull(video, ImageType.Big);
            string localSmall = LocalImageOrNull(video, ImageType.Small);
            List<string> thumbs = new List<string>();
            if (!string.IsNullOrEmpty(localBig))
                thumbs.Add(localBig);
            else if (urls.TryGetValue("BigImageUrl", out object big))
                thumbs.Add(ToString(big));
            if (!string.IsNullOrEmpty(localSmall) && !thumbs.Contains(localSmall))
                thumbs.Add(localSmall);
            else if (!thumbs.Any() && urls.TryGetValue("SmallImageUrl", out object small))
                thumbs.Add(ToString(small));
            if (thumbs.Count == 0 && urls.TryGetValue("BigImageUrl", out object big2))
                thumbs.Add(ToString(big2));

            // fanart
            List<string> fanarts = new List<string>();
            try {
                if (urls.TryGetValue("ExtraImageUrl", out object extra)) {
                    List<string> list = JsonConvert.DeserializeObject<List<string>>(ToString(extra));
                    if (list != null)
                        fanarts = list;
                }
            } catch { }

            // 演员：库映射优先，空则回退 ImageUrls JSON 的 ActorNames/ActressImageUrl（参考脚本）
            List<KeyValuePair<string, string>> actors = new List<KeyValuePair<string, string>>();
            if (actorMap.TryGetValue(video.DataID, out List<KeyValuePair<string, string>> mapped))
                actors.AddRange(mapped);
            if (actors.Count == 0) {
                List<string> names = null, imgs = null;
                try {
                    if (urls.TryGetValue("ActorNames", out object n))
                        names = JsonConvert.DeserializeObject<List<string>>(ToString(n));
                    if (urls.TryGetValue("ActressImageUrl", out object i))
                        imgs = JsonConvert.DeserializeObject<List<string>>(ToString(i));
                } catch { }
                if (names != null) {
                    for (int i = 0; i < names.Count; i++) {
                        string thumb = imgs != null && i < imgs.Count ? imgs[i] : null;
                        actors.Add(new KeyValuePair<string, string>(names[i], thumb));
                    }
                }
            }
            // 去重（参考脚本 seen 逻辑）
            List<KeyValuePair<string, string>> filtered = new List<KeyValuePair<string, string>>();
            HashSet<string> seen = new HashSet<string>();
            foreach (var kv in actors) {
                string key = (kv.Key ?? "").Trim();
                if (key.Length == 0 || seen.Contains(key))
                    continue;
                seen.Add(key);
                filtered.Add(kv);
            }

            // 文件名：视频文件名（去扩展名），无则 VID/DataID
            string baseName = string.Empty;
            if (!string.IsNullOrEmpty(video.Path))
                baseName = System.IO.Path.GetFileNameWithoutExtension(video.Path);
            if (string.IsNullOrEmpty(baseName))
                baseName = nfoId;
            baseName = SafeNfoFileName(baseName);
            string nfoPath = System.IO.Path.Combine(outputDir, $"{baseName}.nfo");

            string separator = SuperUtils.Values.ConstValues.SeparatorString;
            XmlWriterSettings settings = new XmlWriterSettings {
                Indent = true,
                IndentChars = "  ",
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = false,
            };
            using (XmlWriter w = XmlWriter.Create(nfoPath, settings)) {
                w.WriteStartDocument();
                w.WriteStartElement("movie");

                WriteNfoText(w, "source", video.WebUrl, true);
                WriteNfoText(w, "plot", string.IsNullOrEmpty(video.Plot) ? video.Outline : video.Plot, true);
                WriteNfoText(w, "title", video.Title, false);
                WriteNfoText(w, "director", video.Director, true);
                WriteNfoText(w, "rating", ToString(video.Rating), true, "0");
                WriteNfoText(w, "criticrating", "", true);
                WriteNfoText(w, "year", year, true, "0");
                WriteNfoText(w, "mpaa", "", true);
                WriteNfoText(w, "customrating", "", true);
                WriteNfoText(w, "countrycode", "", true);
                WriteNfoText(w, "premiered", releaseDate, false);
                WriteNfoText(w, "release", releaseDate, false);
                WriteNfoText(w, "runtime", ToString(video.Duration), false);
                WriteNfoText(w, "country", video.Country, true);
                WriteNfoText(w, "studio", video.Studio, false);
                WriteNfoText(w, "id", nfoId, false);
                WriteNfoText(w, "num", nfoId, false);

                // genre（分隔符 → 逗号拆分，参考脚本）
                if (!string.IsNullOrEmpty(video.Genre)) {
                    foreach (string gi in video.Genre.Replace(separator, ",").Split(',')) {
                        string t = gi.Trim();
                        if (t.Length > 0)
                            WriteNfoText(w, "genre", t, false);
                    }
                }
                // tag（系列）
                if (!string.IsNullOrEmpty(video.Series))
                    WriteNfoText(w, "tag", video.Series, false);

                // thumb
                foreach (string t in thumbs) {
                    if (!string.IsNullOrEmpty(t))
                        WriteNfoText(w, "thumb", t, false);
                }

                // fanart
                if (fanarts.Count > 0) {
                    w.WriteStartElement("fanart");
                    foreach (string f in fanarts) {
                        if (string.IsNullOrEmpty(f))
                            continue;
                        w.WriteStartElement("thumb");
                        w.WriteAttributeString("preview", f);
                        w.WriteString(f);
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }

                // actor
                foreach (var kv in filtered) {
                    w.WriteStartElement("actor");
                    WriteNfoText(w, "name", kv.Key, false);
                    if (!string.IsNullOrEmpty(kv.Value))
                        WriteNfoText(w, "thumb", kv.Value, false);
                    w.WriteEndElement();
                }

                w.WriteEndElement();
                w.WriteEndDocument();
            }
        }

        private static string LocalImageOrNull(Video video, ImageType imageType)
        {
            try {
                string path = imageType == ImageType.Big ? video.GetBigImage(searchExt: false) : video.GetSmallImage(searchExt: false);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
            } catch { }
            return null;
        }

        private static void WriteNfoText(XmlWriter w, string tag, string value, bool force, string defaultValue = "")
        {
            string val = value ?? "";
            if (val.Length == 0 && !force)
                return;
            if (val.Length == 0 && force)
                val = defaultValue;
            w.WriteStartElement(tag);
            w.WriteString(val);
            w.WriteEndElement();
        }

        /// <summary>
        /// 任意格式日期 → YYYY-MM-DD（参考脚本 format_to_yyyy_mm_dd：年月日 → 年月 → 年 逐级兜底）
        /// </summary>
        private static string FormatToYmd(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return "";
            dateStr = dateStr.Trim();
            Match m = Regex.Match(dateStr, @"(\d{4})[^\d]*(\d{1,2})[^\d]*(\d{1,2})");
            if (m.Success)
                return $"{int.Parse(m.Groups[1].Value):0000}-{int.Parse(m.Groups[2].Value):00}-{int.Parse(m.Groups[3].Value):00}";
            m = Regex.Match(dateStr, @"(\d{4})[^\d]*(\d{1,2})");
            if (m.Success)
                return $"{int.Parse(m.Groups[1].Value):0000}-{int.Parse(m.Groups[2].Value):00}-01";
            m = Regex.Match(dateStr, @"(\d{4})");
            if (m.Success)
                return $"{int.Parse(m.Groups[1].Value):0000}-01-01";
            return dateStr;
        }

        /// <summary>
        /// 文件名安全清洗（参考脚本 safe_filename：字母数字 + -_.()[] {}）
        /// </summary>
        private static string SafeNfoFileName(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "unknown";
            const string keep = "-_.()[] {}";
            StringBuilder sb = new StringBuilder();
            foreach (char c in s) {
                if (char.IsLetterOrDigit(c) || keep.IndexOf(c) >= 0)
                    sb.Append(c);
            }
            string r = sb.ToString().Trim();
            if (r.Length > 200)
                r = r.Substring(0, 200);
            return r.Length == 0 ? "unknown" : r;
        }

        #endregion
    }
}
