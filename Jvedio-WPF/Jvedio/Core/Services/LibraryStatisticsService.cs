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
                scraped = 0, imported30 = 0, unknownYear = 0;
            double gradeTotal = 0;
            using (var connection = Open(true))
            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable, true)) {
                string sql = "select m.Size,m.ViewCount,m.Grade,m.Path,m.PathExist,m.SubtitleExist,m.Title,m.TitleCN," +
                    "m.Genre,m.Country,m.ReleaseDate,m.FirstScanDate,m.CreateDate," +
                    "v.Duration,v.FileDuration,v.VideoType,v.Studio,v.Series,v.Director,v.WebType,v.WebUrl " +
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
                result.Metrics = new Dictionary<string, double> {
                    ["Videos"] = total, ["Favorites"] = favorites, ["Size"] = sizeTotal, ["Duration"] = durationTotal,
                    ["Watched"] = watched, ["Unwatched"] = total - watched, ["PlayCount"] = playCount,
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
                AddChart(result, "StatByGenre", "Bar", "StatPreferences", Top(genres, 15));
                AddChart(result, "StatByStudio", "Bar", "StatPreferences", Top(studios, 10));
                AddChart(result, "StatBySeries", "Bar", "StatPreferences", Top(series, 10));
                AddChart(result, "StatByDirector", "Bar", "StatPreferences", Top(directors, 10));
                AddChart(result, "StatByCountry", "Bar", "StatPreferences", Top(countries, 10));
                AddChart(result, "StatByActor", "Bar", "StatPreferences", QueryBars(connection, transaction,
                    "select ifnull(nullif(a.ActorName,''),'#'||l.ActorID),count(distinct l.DataID) Cnt " +
                    "from metadata_to_actor l join metadata m on m.DataID=l.DataID left join actor_info a on a.ActorID=l.ActorID " +
                    "where " + scope + " group by l.ActorID order by Cnt desc limit 10", dbId));
                AddChart(result, "StatByLabels", "Bar", "StatPreferences", QueryBars(connection, transaction,
                    "select l.LabelName,count(distinct l.DataID) Cnt from metadata_to_label l join metadata m on m.DataID=l.DataID " +
                    "where " + scope + " and trim(ifnull(l.LabelName,''))<>'' group by l.LabelName order by Cnt desc limit 15", dbId));
                AddChart(result, "StatByMarkers", "Bar", "StatPreferences", QueryBars(connection, transaction,
                    "select t.TagName,count(distinct l.DataID) Cnt from metadata_to_tagstamp l join metadata m on m.DataID=l.DataID " +
                    "join common_tagstamp t on t.TagID=l.TagID where " + scope + " group by l.TagID order by Cnt desc limit 15", dbId));
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
