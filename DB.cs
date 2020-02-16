using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace PollBot {

    internal class DB : IDisposable {
        private readonly SQLiteConnection connection;

        public DB(string conn) {
            connection = new SQLiteConnection(conn);
            connection.Open();
            using var trans = connection.BeginTransaction();
            using var cmd = new SQLiteCommand {
                Connection = connection,
                CommandText = "CREATE TABLE IF NOT EXISTS stat(id INTEGER PRIMARY KEY, time DATETIME DEFAULT CURRENT_TIMESTAMP, user INTEGER, content TEXT, msgid INTEGER)"
            };
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS namemap(user INTEGER PRIMARY KEY, username TEXT, first_name TEXT, last_name TEXT)";
            cmd.ExecuteNonQuery();
            trans.Commit();
        }

        public void AddLog(long userid, string username, string first_name, string last_name, string content, long msgid) {
            using var trans = connection.BeginTransaction();
            using var main_insert = new SQLiteCommand {
                Connection = connection,
                CommandText = "INSERT INTO stat(user, content, msgid) VALUES (@user, @content, @msgid)"
            };
            main_insert.Parameters.AddWithValue("@user", userid);
            main_insert.Parameters.AddWithValue("@content", content);
            main_insert.Parameters.AddWithValue("@msgid", msgid);
            main_insert.Prepare();
            main_insert.ExecuteNonQuery();
            using var namemap_insert = new SQLiteCommand {
                Connection = connection,
                CommandText = "INSERT OR REPLACE INTO namemap VALUES (@user, @username, @first_name, @last_name)"
            };
            namemap_insert.Parameters.AddWithValue("@user", userid);
            namemap_insert.Parameters.AddWithValue("@username", username);
            namemap_insert.Parameters.AddWithValue("@first_name", first_name);
            namemap_insert.Parameters.AddWithValue("@last_name", last_name);
            namemap_insert.Prepare();
            namemap_insert.ExecuteNonQuery();
            trans.Commit();
        }

        public struct Entry {
            public long UserId;
            public string Username;
            public string FirstName;
            public string LastName;
            public int Count;

            public string GetName() {
                if (Username.Length == 0) {
                    return $"{FirstName} {LastName}";
                }
                return $"{Username}";
            }
        }

        public IEnumerable<Entry> StatLog() {
            using var query = new SQLiteCommand {
                Connection = connection,
                CommandText = "SELECT namemap.*, count(stat.user) as count FROM stat JOIN namemap WHERE stat.time > datetime('now', '-7 days') GROUP BY stat.user ORDER BY count(stat.user) DESC LIMIT 10;"
            };
            using var reader = query.ExecuteReader();
            while (reader.Read()) {
                yield return new Entry {
                    UserId = reader.GetInt64(0),
                    Username = reader.GetString(1),
                    FirstName = reader.GetString(2),
                    LastName = reader.GetString(3),
                    Count = reader.GetInt32(4)
                };
            }
        }

        public void Dispose() => connection.Dispose();
    }
}