using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using static Jvedio.Core.Library.LibraryDatabase;

namespace Jvedio.Core.Library
{
    public sealed class StatisticItem
    {
        public string Label { get; set; }
        public long Count { get; set; }
        public bool Localized { get; set; }

        /// <summary>点击跳转类型：影片维度=LabelType 枚举名（Genre/Studio/Series/Director/Country/LabelName）、Tag=标记、Actor=按 RefId 跳演员、ActorXxx=演员信息分布；空=不可跳转</summary>
        public string FilterType { get; set; }

        /// <summary>演员信息分布的跳转 SQL 条件（actor_info 列名前缀，注入演员列表查询）</summary>
        public string FilterSql { get; set; }

        /// <summary>跳转引用 ID（Actor 类型=ActorID）</summary>
        public long RefId { get; set; }
    }

    public sealed class StatisticChart
    {
        public string TitleKey { get; set; }
        public string Kind { get; set; }
        public string GroupKey { get; set; }
        public List<StatisticItem> Items { get; set; } = new List<StatisticItem>();
    }

    public sealed class LibraryStatistics
    {
        public Dictionary<string, double> Metrics { get; set; } = new Dictionary<string, double>();
        public List<StatisticChart> Charts { get; set; } = new List<StatisticChart>();
        public DateTime CollectedAt { get; set; }
    }

    /// <summary>One deferred read snapshot; stream only the fields needed by the charts. No media I/O.</summary>
    public static class LibraryStatisticsService
    {
        public static LibraryStatistics Collect(long? dbId, DateTime? today = null)
        {
            var result = new LibraryStatistics { CollectedAt = DateTime.Now };
            DateTime now = (today ?? DateTime.Today).Date;
            DateTime firstMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-11);
            string scope = "m.DataType=0" + (dbId.HasValue ? " and m.DBId=@db" : "");
            var genres = new Dictionary<string, long>(StringComparer.Ordinal);
            var studios = new Dictionary<string, long>(StringComparer.Ordinal);
            var series = new Dictionary<string, long>(StringComparer.Ordinal);
            var directors = new Dictionary<string, long>(StringComparer.Ordinal);
            var countries = new Dictionary<string, long>(StringComparer.Ordinal);
            var years = new SortedDictionary<string, long>(StringComparer.Ordinal);
            var imports = new SortedDictionary<string, long>(StringComparer.Ordinal);
            for (int i = 0; i < 12; i++) imports[firstMonth.AddMonths(i).ToString("yyyy-MM", CultureInfo.InvariantCulture)] = 0;
            long[] grades = new long[6], sizes = new long[7], durations = new long[7], types = new long[3], plays = new long[5];
            long total = 0, favorites = 0, watched = 0, playCount = 0, sizeTotal = 0, durationTotal = 0,
                durationKnown = 0, sizeKnown = 0, playable = 0, subtitles = 0, translated = 0,
                titleKnown = 0, genreKnown = 0, studioKnown = 0, seriesKnown = 0, directorKnown = 0,
                scraped = 0, imported30 = 0, unknownYear = 0, watchTimeTotal = 0;
            double gradeTotal = 0;
            using (var connection = Open(true))
            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable, true)) {
                string sql = "select m.Size,m.ViewCount,m.Grade,m.Path,m.PathExist,m.SubtitleExist,m.Title,m.TitleCN," +
                    "m.Genre,m.Country,m.ReleaseDate,m.FirstScanDate,m.CreateDate," +
                    "v.Duration,v.FileDuration,v.VideoType,v.Studio,v.Series,v.Director,v.WebType,v.WebUrl,m.PlayDuration " +
                    "from metadata m left join metadata_video v on v.MVID=" +
                    "(select max(MVID) from metadata_video where DataID=m.DataID) where " + scope;
                using (var command = MakeCommand(connection, transaction, sql, "@db", dbId ?? 0))
                using (var reader = command.ExecuteReader()) {
                    while (reader.Read()) {
                        total++;
                        long size = Math.Max(0, Number(reader[0]));
                        long views = Math.Max(0, Number(reader[1]));
                        double grade = Double(reader[2]);
                        sizeTotal += size; playCount += views;
                        if (grade > 0) { favorites++; gradeTotal += grade; }
                        grades[grade <= 0 ? 0 : Math.Max(1, Math.Min(5, (int)Math.Ceiling(grade)))]++;
                        if (views > 0) watched++;
                        plays[views == 0 ? 0 : views == 1 ? 1 : views <= 3 ? 2 : views <= 10 ? 3 : 4]++;
                        if (size > 0) sizeKnown++;
                        double gb = size / 1073741824d;
                        sizes[size == 0 ? 0 : gb < 1 ? 1 : gb < 3 ? 2 : gb < 5 ? 3 : gb < 10 ? 4 : gb < 20 ? 5 : 6]++;
                        long seconds = Math.Max(0, Number(reader[14]));
                        if (seconds == 0) seconds = Math.Max(0, Number(reader[13])) * 60;
                        if (seconds > 0) durationKnown++;
                        durationTotal += seconds;
                        double minutes = seconds / 60d;
                        durations[seconds == 0 ? 0 : minutes < 30 ? 1 : minutes < 60 ? 2 : minutes < 120 ? 3 : minutes < 180 ? 4 : minutes < 240 ? 5 : 6]++;
                        if (!string.IsNullOrWhiteSpace(Text(reader[3])) && Number(reader[4]) > 0) playable++;
                        if (Number(reader[5]) > 0) subtitles++;
                        if (!string.IsNullOrWhiteSpace(Text(reader[6]))) titleKnown++;
                        if (!string.IsNullOrWhiteSpace(Text(reader[7]))) translated++;
                        if (AddValues(genres, Text(reader[8]))) genreKnown++;
                        AddValues(countries, Text(reader[9]));
                        string release = Text(reader[10]);
                        if (release.Length >= 4 && int.TryParse(release.Substring(0, 4), out int year) && year > 1900 && year <= now.Year + 2)
                            Increment(years, year.ToString(CultureInfo.InvariantCulture));
                        else unknownYear++;
                        if (!DateTime.TryParse(Text(reader[11]), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime imported))
                            DateTime.TryParse(Text(reader[12]), CultureInfo.InvariantCulture, DateTimeStyles.None, out imported);
                        if (imported >= now.AddDays(-29) && imported < now.AddDays(1)) imported30++;
                        string month = imported.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                        if (imports.ContainsKey(month)) imports[month]++;
                        int type = (int)Number(reader[15]);
                        types[type < 0 || type > 2 ? 0 : type]++;
                        if (AddValues(studios, Text(reader[16]))) studioKnown++;
                        if (AddValues(series, Text(reader[17]))) seriesKnown++;
                        if (AddValues(directors, Text(reader[18]))) directorKnown++;
                        if (!string.IsNullOrWhiteSpace(Text(reader[19])) || !string.IsNullOrWhiteSpace(Text(reader[20]))) scraped++;
                        watchTimeTotal += Math.Max(0, Number(reader[21]));
                    }
                }
                long actorCount = Scalar(connection, transaction,
                    "select count(distinct a.ActorID) from metadata_to_actor a join metadata m on m.DataID=a.DataID where " + scope, dbId);
                long withActors = Scalar(connection, transaction,
                    "select count(distinct a.DataID) from metadata_to_actor a join metadata m on m.DataID=a.DataID where " + scope, dbId);
                long labeled = Scalar(connection, transaction,
                    "select count(distinct l.DataID) from metadata_to_label l join metadata m on m.DataID=l.DataID where " + scope +
                    " and trim(ifnull(l.LabelName,''))<>''", dbId);
                long labelCount = Scalar(connection, transaction,
                    "select count(*) from (select c.DBId,c.LabelName from metadata_label_catalog c " +
                    "join app_databases b on b.DBId=c.DBId where b.DataType=0" +
                    (dbId.HasValue ? " and c.DBId=@db" : "") +
                    " union select m.DBId,l.LabelName from metadata_to_label l join metadata m on m.DataID=l.DataID where " + scope +
                    " and trim(ifnull(l.LabelName,''))<>'')", dbId);

                // 演员信息统计：范围内影片出现过的演员（与 StatByActor 同一连接与范围），逐项分桶
                long[] actorAges = new long[8], actorHeights = new long[6], actorWeights = new long[6], actorShoes = new long[7];
                var birthplaces = new Dictionary<string, long>(StringComparer.Ordinal);
                long unknownBirthplace = 0;
                using (var command = MakeCommand(connection, transaction,
                    "select actor_info.Age,actor_info.Height,actor_info.Weight,actor_info.BirthPlace,actor_info.ShoeSize " +
                    "from actor_info join metadata_to_actor on metadata_to_actor.ActorID=actor_info.ActorID " +
                    "join metadata m on m.DataID=metadata_to_actor.DataID where " + scope + " group by actor_info.ActorID", "@db", dbId ?? 0))
                using (var reader = command.ExecuteReader()) {
                    while (reader.Read()) {
                        long age = Number(reader[0]);
                        actorAges[age <= 0 ? 0 : age <= 20 ? 1 : age <= 25 ? 2 : age <= 30 ? 3 : age <= 35 ? 4 : age <= 40 ? 5 : age <= 50 ? 6 : 7]++;
                        long height = Number(reader[1]);
                        actorHeights[height <= 0 ? 0 : height < 150 ? 1 : height < 160 ? 2 : height < 170 ? 3 : height < 180 ? 4 : 5]++;
                        long weight = Number(reader[2]);
                        actorWeights[weight <= 0 ? 0 : weight < 40 ? 1 : weight < 50 ? 2 : weight < 60 ? 3 : weight < 70 ? 4 : 5]++;
                        string birthplace = Text(reader[3]).Trim();
                        if (birthplace.Length == 0) unknownBirthplace++; else Increment(birthplaces, birthplace);
                        double.TryParse(Text(reader[4]), NumberStyles.Any, CultureInfo.InvariantCulture, out double shoe);
                        actorShoes[shoe <= 0 ? 0 : shoe <= 34 ? 1 : shoe <= 36 ? 2 : shoe <= 38 ? 3 : shoe <= 40 ? 4 : shoe <= 42 ? 5 : 6]++;
                    }
                }

                result.Metrics = new Dictionary<string, double> {
                    ["Videos"] = total, ["Favorites"] = favorites, ["Size"] = sizeTotal, ["Duration"] = durationTotal,
                    ["Watched"] = watched, ["Unwatched"] = total - watched, ["PlayCount"] = playCount,
                    ["WatchTime"] = watchTimeTotal,
                    ["Playable"] = playable, ["Subtitles"] = subtitles, ["Translated"] = translated,
                    ["Actors"] = actorCount, ["Labels"] = labelCount, ["Labeled"] = labeled,
                    ["Imported30"] = imported30, ["AvgGrade"] = favorites == 0 ? 0 : gradeTotal / favorites,
                    ["AvgSize"] = sizeKnown == 0 ? 0 : sizeTotal / (double)sizeKnown,
                    ["AvgDuration"] = durationKnown == 0 ? 0 : durationTotal / (double)durationKnown,
                    ["DurationKnown"] = durationKnown
                };
                AddChart(result, "StatWatchShare", "Donut", "StatOverview", Partition(total, watched, "StatWatched", "StatUnwatched"));
                AddChart(result, "StatAvailability", "Donut", "StatOverview", Partition(total, playable, "StatIndexedAvailable", "StatIndexedUnavailable"));
                AddChart(result, "StatSubtitleShare", "Donut", "StatOverview", Partition(total, subtitles, "StatWithSubtitles", "StatWithoutSubtitles"));
                AddChart(result, "StatVideoTypes", "Donut", "StatOverview", Buckets(types, "StatTypeOther", "StatTypeCensored", "StatTypeUncensored"));
                var coverage = new List<StatisticItem> {
                    Item("StatHasTitle", titleKnown, true), Item("StatHasGenre", genreKnown, true),
                    Item("StatHasStudio", studioKnown, true), Item("StatHasSeries", seriesKnown, true),
                    Item("StatHasDirector", directorKnown, true), Item("StatHasActors", withActors, true),
                    Item("StatHasLabels", labeled, true), Item("StatHasTranslation", translated, true),
                    Item("StatKnownDuration", durationKnown, true), Item("StatScraped", scraped, true)
                };
                AddChart(result, "StatCoverage", "Coverage", "StatOverview", coverage);
                AddChart(result, "StatImportTrend", "Line", "StatHistory", imports.Select(p => Item(p.Key, p.Value)).ToList());
                var yearItems = years.Select(p => Item(p.Key, p.Value)).ToList();
                if (yearItems.Count > 30) {
                    int older = yearItems.Count - 30;
                    long oldCount = yearItems.Take(older).Sum(i => i.Count);
                    yearItems = yearItems.Skip(older).ToList();
                    yearItems.Insert(0, Item("StatEarlierYears", oldCount, true));
                }
                yearItems.Add(Item("StatUnknown", unknownYear, true));
                AddChart(result, "StatReleaseYears", "Column", "StatHistory", yearItems);
                AddChart(result, "StatByRating", "Column", "StatHistory", Buckets(grades,
                    "StatUnrated", "StatGrade1", "StatGrade2", "StatGrade3", "StatGrade4", "StatGrade5"));
                AddChart(result, "StatPlayDistribution", "Column", "StatHistory", Buckets(plays,
                    "StatNeverPlayed", "StatPlayedOnce", "StatPlayed2To3", "StatPlayed4To10", "StatPlayedMore"));
                AddChart(result, "StatSizeDistribution", "Bar", "StatFiles", Buckets(sizes,
                    "StatUnknown", "StatSize1", "StatSize3", "StatSize5", "StatSize10", "StatSize20", "StatSizeMore"));
                AddChart(result, "StatDurationDistribution", "Column", "StatFiles", Buckets(durations,
                    "StatUnknown", "StatDuration30", "StatDuration60", "StatDuration120", "StatDuration180", "StatDuration240", "StatDurationMore"));
                var genreItems = Top(genres, 15);
                Attach(genreItems, "Genre");
                AddChart(result, "StatByGenre", "Bar", "StatPreferences", genreItems);
                var studioItems = Top(studios, 10);
                Attach(studioItems, "Studio");
                AddChart(result, "StatByStudio", "Bar", "StatPreferences", studioItems);
                var seriesItems = Top(series, 10);
                Attach(seriesItems, "Series");
                AddChart(result, "StatBySeries", "Bar", "StatPreferences", seriesItems);
                var directorItems = Top(directors, 10);
                Attach(directorItems, "Director");
                AddChart(result, "StatByDirector", "Bar", "StatPreferences", directorItems);
                var countryItems = Top(countries, 10);
                Attach(countryItems, "Country");
                AddChart(result, "StatByCountry", "Bar", "StatPreferences", countryItems);
                var actorItems = new List<StatisticItem>();
                using (var command = MakeCommand(connection, transaction,
                    "select a.ActorID,ifnull(nullif(a.ActorName,''),'#'||l.ActorID),count(distinct l.DataID) Cnt " +
                    "from metadata_to_actor l join metadata m on m.DataID=l.DataID left join actor_info a on a.ActorID=l.ActorID " +
                    "where " + scope + " group by l.ActorID order by Cnt desc limit 10", "@db", dbId ?? 0))
                using (var reader = command.ExecuteReader()) {
                    while (reader.Read()) {
                        var item = Item(Text(reader[1]), Number(reader[2]));
                        item.FilterType = "Actor";
                        item.RefId = Number(reader[0]);
                        actorItems.Add(item);
                    }
                }
                AddChart(result, "StatByActor", "Bar", "StatPreferences", actorItems);
                var labelItems = QueryBars(connection, transaction,
                    "select l.LabelName,count(distinct l.DataID) Cnt from metadata_to_label l join metadata m on m.DataID=l.DataID " +
                    "where " + scope + " and trim(ifnull(l.LabelName,''))<>'' group by l.LabelName order by Cnt desc limit 15", dbId);
                Attach(labelItems, "LabelName");
                AddChart(result, "StatByLabels", "Bar", "StatPreferences", labelItems);
                var markerItems = QueryBars(connection, transaction,
                    "select t.TagName,count(distinct l.DataID) Cnt from metadata_to_tagstamp l join metadata m on m.DataID=l.DataID " +
                    "join common_tagstamp t on t.TagID=l.TagID where " + scope + " group by l.TagID order by Cnt desc limit 15", dbId);
                Attach(markerItems, "Tag");
                AddChart(result, "StatByMarkers", "Bar", "StatPreferences", markerItems);

                // 演员信息分布（点击条目 → 打开按桶筛选的演员列表；空数据归「未知」）
                AddChart(result, "StatActorAge", "Bar", "StatGroupActors", ActorBuckets(actorAges, "ActorAge", AgeLabels, AgeSql));
                AddChart(result, "StatActorHeight", "Bar", "StatGroupActors", ActorBuckets(actorHeights, "ActorHeight", HeightLabels, HeightSql));
                AddChart(result, "StatActorWeight", "Bar", "StatGroupActors", ActorBuckets(actorWeights, "ActorWeight", WeightLabels, WeightSql));
                AddChart(result, "StatActorShoeSize", "Bar", "StatGroupActors", ActorBuckets(actorShoes, "ActorShoeSize", ShoeLabels, ShoeSql));
                var birthItems = Top(birthplaces, 10);
                Attach(birthItems, "ActorBirthplace");
                foreach (var item in birthItems)
                    item.FilterSql = "actor_info.BirthPlace='" + item.Label.Replace("'", "''") + "'";
                var unknownPlace = Item("StatUnknown", unknownBirthplace, true);
                unknownPlace.FilterType = "ActorBirthplace";
                unknownPlace.FilterSql = "ifnull(actor_info.BirthPlace,'')=''";
                birthItems.Add(unknownPlace);
                AddChart(result, "StatActorBirthplace", "Bar", "StatGroupActors", birthItems);
                transaction.Commit();
            }
            return result;
        }

        private static long Scalar(SQLiteConnection connection, SQLiteTransaction transaction, string sql, long? dbId)
        {
            using (var command = MakeCommand(connection, transaction, sql, "@db", dbId ?? 0))
                return Number(command.ExecuteScalar());
        }

        private static List<StatisticItem> QueryBars(SQLiteConnection connection, SQLiteTransaction transaction, string sql, long? dbId)
        {
            var result = new List<StatisticItem>();
            using (var command = MakeCommand(connection, transaction, sql, "@db", dbId ?? 0))
            using (var reader = command.ExecuteReader())
                while (reader.Read()) result.Add(Item(Text(reader[0]), Number(reader[1])));
            return result;
        }

        private static List<StatisticItem> Buckets(long[] counts, params string[] labels) =>
            counts.Select((count, i) => Item(labels[i], count, true)).ToList();

        /// <summary>演员信息分布分桶标签（首项=未知，走 StatUnknown 语言键；其余为语言无关的数值区间）</summary>
        private static readonly string[] AgeLabels = { "StatUnknown", "≤20", "21~25", "26~30", "31~35", "36~40", "41~50", ">50" };
        private static readonly string[] HeightLabels = { "StatUnknown", "<150", "150~159", "160~169", "170~179", "≥180" };
        private static readonly string[] WeightLabels = { "StatUnknown", "<40", "40~49", "50~59", "60~69", "≥70" };
        private static readonly string[] ShoeLabels = { "StatUnknown", "≤34", "35~36", "37~38", "39~40", "41~42", "≥43" };

        /// <summary>与分桶一一对应的演员列表筛选 SQL（actor_info 列名前缀；数值语义与内存分桶一致：0/非数字=未知）</summary>
        private static readonly string[] AgeSql = {
            "ifnull(actor_info.Age,0)<=0",
            "actor_info.Age>0 and actor_info.Age<=20",
            "actor_info.Age between 21 and 25", "actor_info.Age between 26 and 30",
            "actor_info.Age between 31 and 35", "actor_info.Age between 36 and 40",
            "actor_info.Age between 41 and 50", "actor_info.Age>=51",
        };
        private static readonly string[] HeightSql = {
            "ifnull(actor_info.Height,0)<=0",
            "actor_info.Height>0 and actor_info.Height<150",
            "actor_info.Height between 150 and 159", "actor_info.Height between 160 and 169",
            "actor_info.Height between 170 and 179", "actor_info.Height>=180",
        };
        private static readonly string[] WeightSql = {
            "ifnull(actor_info.Weight,0)<=0",
            "actor_info.Weight>0 and actor_info.Weight<40",
            "actor_info.Weight between 40 and 49", "actor_info.Weight between 50 and 59",
            "actor_info.Weight between 60 and 69", "actor_info.Weight>=70",
        };
        private static readonly string[] ShoeSql = {
            "ifnull(actor_info.ShoeSize,'')='' or cast(ifnull(actor_info.ShoeSize,'0') as real)<=0",
            "cast(actor_info.ShoeSize as real)>0 and cast(actor_info.ShoeSize as real)<=34",
            "cast(actor_info.ShoeSize as real)>34 and cast(actor_info.ShoeSize as real)<=36",
            "cast(actor_info.ShoeSize as real)>36 and cast(actor_info.ShoeSize as real)<=38",
            "cast(actor_info.ShoeSize as real)>38 and cast(actor_info.ShoeSize as real)<=40",
            "cast(actor_info.ShoeSize as real)>40 and cast(actor_info.ShoeSize as real)<=42",
            "cast(actor_info.ShoeSize as real)>=43",
        };

        /// <summary>把计数数组组装为可跳转的分桶条目：未知桶走语言键，其余区间标签按原文显示</summary>
        private static List<StatisticItem> ActorBuckets(long[] counts, string filterType, string[] labels, string[] sql)
        {
            var list = new List<StatisticItem>();
            for (int i = 0; i < counts.Length; i++) {
                var item = Item(labels[i], counts[i], labels[i] == "StatUnknown");
                item.FilterType = filterType;
                item.FilterSql = sql[i];
                list.Add(item);
            }
            return list;
        }

        /// <summary>为统计条目批量标注点击跳转类型</summary>
        private static void Attach(List<StatisticItem> items, string filterType)
        {
            foreach (var item in items) item.FilterType = filterType;
        }

        private static List<StatisticItem> Partition(long total, long yes, string yesKey, string noKey) =>
            new List<StatisticItem> { Item(yesKey, yes, true), Item(noKey, total - yes, true) };
        private static List<StatisticItem> Top(Dictionary<string, long> counts, int take) =>
            counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.CurrentCulture)
                .Take(take).Select(p => Item(p.Key, p.Value)).ToList();
        private static StatisticItem Item(string label, long count, bool localized = false) =>
            new StatisticItem { Label = label, Count = count, Localized = localized };
        private static void AddChart(LibraryStatistics stats, string title, string kind, string group, List<StatisticItem> items) =>
            stats.Charts.Add(new StatisticChart { TitleKey = title, Kind = kind, GroupKey = group, Items = items });
        private static void Increment(IDictionary<string, long> counts, string key)
        {
            counts[key] = counts.TryGetValue(key, out long value) ? value + 1 : 1;
        }
        private static bool AddValues(Dictionary<string, long> counts, string value)
        {
            var values = value.Split(SuperUtils.Values.ConstValues.Separator).Select(s => s.Trim())
                .Where(s => s.Length > 0).Distinct(StringComparer.Ordinal).ToList();
            foreach (string item in values) Increment(counts, item);
            return values.Count > 0;
        }
        private static string Text(object value) => value == null || value == DBNull.Value ? "" : value.ToString();
        private static long Number(object value) => value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value);
        private static double Double(object value) => value == null || value == DBNull.Value ? 0 : Convert.ToDouble(value);
    }
}
