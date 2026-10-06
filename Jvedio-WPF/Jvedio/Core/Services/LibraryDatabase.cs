using Jvedio.Core.DataBase;
using System;
using System.Data.SQLite;
using System.IO;

namespace Jvedio.Core.Library
{
    internal static class LibraryDatabase
    {
        public static SQLiteConnection Open(bool readOnly = false)
        {
            if (!File.Exists(SqlManager.DEFAULT_SQLITE_PATH))
                throw new FileNotFoundException("Library database is unavailable", SqlManager.DEFAULT_SQLITE_PATH);
            var builder = new SQLiteConnectionStringBuilder {
                DataSource = SqlManager.DEFAULT_SQLITE_PATH, Version = 3, ReadOnly = readOnly, DefaultTimeout = 5
            };
            var connection = new SQLiteConnection(builder.ConnectionString);
            connection.Open();
            using (var command = new SQLiteCommand("PRAGMA busy_timeout=5000", connection))
                command.ExecuteNonQuery();
            return connection;
        }

        public static SQLiteCommand MakeCommand(SQLiteConnection connection, SQLiteTransaction transaction,
            string sql, params object[] parameters)
        {
            var command = new SQLiteCommand(sql, connection, transaction);
            for (int i = 0; i < parameters.Length; i += 2)
                command.Parameters.AddWithValue((string)parameters[i], parameters[i + 1] ?? DBNull.Value);
            return command;
        }

        public static int Execute(SQLiteConnection connection, SQLiteTransaction transaction,
            string sql, params object[] parameters)
        {
            using (var command = MakeCommand(connection, transaction, sql, parameters))
                return command.ExecuteNonQuery();
        }
    }
}
