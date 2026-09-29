using Jvedio.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Jvedio.Core.Crawler
{
    /// <summary>Only these network metadata fields may be merged into an existing video.</summary>
    public static class ScrapeFieldPolicy
    {
        public static readonly string[] Fields = {
            "Title", "TitleCN", "ReleaseDate", "Duration", "Director", "Studio", "Publisher",
            "Series", "Genre", "Country", "Rating", "RatingCount", "Plot", "Outline",
            "ActorNames", "Label", "WebUrl"
        };

        private static readonly HashSet<string> ImageKeys = new HashSet<string> {
            "SmallImageUrl", "BigImageUrl", "ExtraImageUrl", "ActressImageUrl"
        };

        public static IEnumerable<string> Available(Dictionary<string, object> scraped)
        {
            return Fields.Where(name => scraped != null && scraped.ContainsKey(name) && scraped[name] != null);
        }

        public static string Current(Video video, string name)
        {
            PropertyInfo property = typeof(Video).GetProperty(name);
            return property?.GetValue(video)?.ToString() ?? string.Empty;
        }

        public static string Proposed(Dictionary<string, object> scraped, string name)
        {
            if (scraped == null || !scraped.TryGetValue(name, out object value) || value == null)
                return string.Empty;
            return value is List<string> list ? string.Join(", ", list) : value.ToString();
        }

        public static bool HasExistingValue(Video video, string name)
        {
            string value = Current(video, name);
            return !string.IsNullOrWhiteSpace(value) && value != "0";
        }

        public static Dictionary<string, object> MergeDictionary(Dictionary<string, object> scraped,
            IEnumerable<string> chosen)
        {
            var selected = new HashSet<string>(chosen ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var result = new Dictionary<string, object>();
            if (scraped == null) return result;
            foreach (string name in selected) {
                if (Fields.Contains(name) && scraped.TryGetValue(name, out object value))
                    result[name] = value;
            }
            foreach (string key in ImageKeys) {
                if (scraped.TryGetValue(key, out object value)) result[key] = value;
            }
            return result;
        }
    }
}
