using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using static Jvedio.Core.Library.LibraryDatabase;

namespace Jvedio.Core.Library
{
    public sealed class LibraryLabel
    {
        public string Name { get; set; }
        public long Count { get; set; }
    }

    public sealed class LabelVideo
    {
        public long DataID { get; set; }
        public string VID { get; set; }
        public string Title { get; set; }
        public bool Assigned { get; set; }
    }

    public sealed class LabelVideoPage
    {
        public long Total { get; set; }
        public List<LabelVideo> Videos { get; set; } = new List<LabelVideo>();
    }

    /// <summary>Library-scoped labels, including labels that have not been assigned yet.</summary>
    public static class LibraryLabelService
    {
        public static event Action<long> Changed;
        public const int PageSize = 100;

        public static string ValidateName(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name.Any(char.IsControl))
                throw new ArgumentException(SuperControls.Style.LangManager.GetValueByKey("LabelInvalidName"));
            return name;
        }

        public static List<LibraryLabel> List(long dbId)
        {
            var result = new List<LibraryLabel>();
            using (var connection = Open(true))
            using (var command = MakeCommand(connection, null,
                "with names as (select LabelName from metadata_label_catalog where DBId=@db " +
                "union select l.LabelName from metadata_to_label l join metadata m on m.DataID=l.DataID where m.DBId=@db), " +
                "counts as (select l.LabelName, count(distinct l.DataID) Cnt from metadata_to_label l " +
                "join metadata m on m.DataID=l.DataID where m.DBId=@db group by l.LabelName) " +
                "select n.LabelName, ifnull(c.Cnt,0) Cnt from names n left join counts c on c.LabelName=n.LabelName " +
                "where trim(ifnull(n.LabelName,''))<>'' order by Cnt desc,n.LabelName", "@db", dbId))
            using (var reader = command.ExecuteReader())
                while (reader.Read())
                    result.Add(new LibraryLabel { Name = reader.GetString(0), Count = reader.GetInt64(1) });
            return result;
        }

        public static List<string> Suggestions(long dbId, string search)
        {
            search = search?.Trim() ?? "";
            return List(dbId).Where(label => label.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(200).Select(label => label.Name + "(" + label.Count + ")").ToList();
        }

        public static long CountVideos(long dbId, bool withoutLabels = false)
        {
            using (var connection = Open(true))
            using (var command = MakeCommand(connection, null,
                "select count(*) from metadata m where m.DBId=@db" + (withoutLabels
                    ? " and not exists(select 1 from metadata_to_label l where l.DataID=m.DataID and trim(ifnull(l.LabelName,''))<>'')"
                    : ""), "@db", dbId))
                return Convert.ToInt64(command.ExecuteScalar());
        }

        public static bool Create(long dbId, string name)
        {
            name = ValidateName(name);
            if (List(dbId).Any(label => label.Name == name)) return false;
            int changed;
            using (var connection = Open())
                changed = Execute(connection, null,
                    "insert or ignore into metadata_label_catalog(DBId,LabelName) values(@db,@name)", "@db", dbId, "@name", name);
            if (changed > 0) Changed?.Invoke(dbId);
            return changed > 0;
        }

        public static void Rename(long dbId, string oldName, string newName)
        {
            newName = ValidateName(newName);
            if (oldName == newName) return;
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction()) {
                Execute(connection, transaction, "insert or ignore into metadata_label_catalog(DBId,LabelName) values(@db,@new)",
                    "@db", dbId, "@new", newName);
                Execute(connection, transaction, "insert or ignore into metadata_to_label(DataID,LabelName) " +
                    "select DataID,@new from metadata_to_label where LabelName=@old " +
                    "and DataID in(select DataID from metadata where DBId=@db)",
                    "@new", newName, "@old", oldName, "@db", dbId);
                Execute(connection, transaction, "delete from metadata_to_label where LabelName=@old " +
                    "and DataID in(select DataID from metadata where DBId=@db)", "@old", oldName, "@db", dbId);
                Execute(connection, transaction, "delete from metadata_label_catalog where DBId=@db and LabelName=@old",
                    "@db", dbId, "@old", oldName);
                transaction.Commit();
            }
            Changed?.Invoke(dbId);
        }

        public static void Delete(long dbId, string name)
        {
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction()) {
                Execute(connection, transaction, "delete from metadata_to_label where LabelName=@name " +
                    "and DataID in(select DataID from metadata where DBId=@db)", "@name", name, "@db", dbId);
                Execute(connection, transaction, "delete from metadata_label_catalog where DBId=@db and LabelName=@name",
                    "@db", dbId, "@name", name);
                transaction.Commit();
            }
            Changed?.Invoke(dbId);
        }

        public static void Assign(long dbId, string name, IDictionary<long, bool> changes)
        {
            name = ValidateName(name);
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction()) {
                Execute(connection, transaction, "insert or ignore into metadata_label_catalog(DBId,LabelName) values(@db,@name)",
                    "@db", dbId, "@name", name);
                foreach (var change in changes) {
                    string sql = change.Value
                        ? "insert or ignore into metadata_to_label(DataID,LabelName) " +
                          "select DataID,@name from metadata where DataID=@id and DBId=@db"
                        : "delete from metadata_to_label where DataID=@id and LabelName=@name " +
                          "and DataID in(select DataID from metadata where DBId=@db)";
                    Execute(connection, transaction, sql, "@name", name, "@id", change.Key, "@db", dbId);
                }
                transaction.Commit();
            }
            Changed?.Invoke(dbId);
        }

        public static void SetLabels(long dataId, IEnumerable<string> names)
        {
            var labels = (names ?? Enumerable.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(ValidateName).Distinct(StringComparer.Ordinal).ToList();
            long dbId;
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction()) {
                using (var command = MakeCommand(connection, transaction, "select DBId from metadata where DataID=@id", "@id", dataId)) {
                    object value = command.ExecuteScalar();
                    if (value == null || value == DBNull.Value) return;
                    dbId = Convert.ToInt64(value);
                }
                // Keep existing names in the catalog even when their last assignment is removed.
                Execute(connection, transaction, "insert or ignore into metadata_label_catalog(DBId,LabelName) " +
                    "select @db,LabelName from metadata_to_label where DataID=@id", "@db", dbId, "@id", dataId);
                Execute(connection, transaction, "delete from metadata_to_label where DataID=@id", "@id", dataId);
                foreach (string name in labels) {
                    Execute(connection, transaction, "insert or ignore into metadata_label_catalog(DBId,LabelName) values(@db,@name)",
                        "@db", dbId, "@name", name);
                    Execute(connection, transaction, "insert or ignore into metadata_to_label(DataID,LabelName) values(@id,@name)",
                        "@id", dataId, "@name", name);
                }
                transaction.Commit();
            }
            Changed?.Invoke(dbId);
        }

        public static LabelVideoPage QueryVideos(long dbId, string name, string search, bool assignedOnly, int page)
        {
            var result = new LabelVideoPage();
            string assigned = "exists(select 1 from metadata_to_label l where l.DataID=m.DataID and l.LabelName=@name)";
            string where = "m.DBId=@db";
            if (!string.IsNullOrWhiteSpace(search))
                where += " and instr(lower(ifnull(v.VID,'')||' '||ifnull(m.Title,'')||' '||ifnull(m.TitleCN,'')),lower(@search))>0";
            if (assignedOnly) where += " and " + assigned;
            string from = " from metadata m left join metadata_video v on v.MVID=" +
                "(select max(MVID) from metadata_video where DataID=m.DataID) where " + where;
            object[] parameters = { "@db", dbId, "@name", name, "@search", search?.Trim() ?? "",
                "@offset", Math.Max(0, page - 1) * PageSize, "@limit", PageSize };
            using (var connection = Open(true)) {
                using (var command = MakeCommand(connection, null, "select count(*)" + from, parameters))
                    result.Total = Convert.ToInt64(command.ExecuteScalar());
                using (var command = MakeCommand(connection, null,
                    "select m.DataID,ifnull(v.VID,''),ifnull(m.Title,'')," + assigned + from +
                    " order by m.DataID desc limit @limit offset @offset", parameters))
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        result.Videos.Add(new LabelVideo {
                            DataID = reader.GetInt64(0), VID = reader.GetString(1), Title = reader.GetString(2),
                            Assigned = Convert.ToInt64(reader.GetValue(3)) > 0
                        });
            }
            return result;
        }
    }
}
