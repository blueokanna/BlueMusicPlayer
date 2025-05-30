using Microsoft.Data.Sqlite;
using System;
using System.Collections.ObjectModel;

namespace BlueMusicPlayer.Models
{
    public class MusicRepository
    {
        private readonly string _dbPath;
        public MusicRepository(string dbPath)
        {
            _dbPath = dbPath;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Tracks (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              FilePath TEXT NOT NULL,
              Title    TEXT NOT NULL
            );";
            cmd.ExecuteNonQuery();
        }

        public ObservableCollection<Track> LoadAll()
        {
            var list = new ObservableCollection<Track>();
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, FilePath, Title FROM Tracks;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Track
                {
                    Id = reader.GetInt32(0),
                    FilePath = reader.GetString(1),
                    Title = reader.GetString(2)
                });
            }
            return list;
        }

        public void Add(Track track)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Tracks (FilePath, Title) 
                VALUES ($fp, $t);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$fp", track.FilePath);
            cmd.Parameters.AddWithValue("$t", track.Title);

            var result = cmd.ExecuteScalar();
            if (result != null && long.TryParse(result.ToString(), out var lastId))
            {
                track.Id = (int)lastId;
            }
            else
            {
                throw new Exception("无法获取新插入记录的 ID");
            }
        }


        public void Delete(Track track)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Tracks WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", track.Id);
            cmd.ExecuteNonQuery();
        }
    }
}
