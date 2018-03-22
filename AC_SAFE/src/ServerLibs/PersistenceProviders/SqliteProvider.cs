using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Proto.Persistence;

namespace PersistenceProviders
{
    public class TypedSerializer<T>
    {
        public Func<object, string> Serialize { get; }
        public Func<string, object> Deserialize { get; }

        public TypedSerializer(Func<object, string> serialize, Func<string, object> deserialize)
        {
            Serialize = serialize;
            Deserialize = deserialize;
        }
    }

    public class SqliteProvider<TEvent, TSnapshot> : IProvider
    {
        private readonly SqliteConnectionStringBuilder _connectionStringBuilder;
        private readonly TypedSerializer<TEvent> _eventSerializer;
        private readonly TypedSerializer<TSnapshot> _snapshotSerializer;
        private string ConnectionString => $"{_connectionStringBuilder}";

        public SqliteProvider(SqliteConnectionStringBuilder connectionStringBuilder, TypedSerializer<TEvent> eventSerializer, TypedSerializer<TSnapshot> snapshotSerializer)
        {
            _connectionStringBuilder = connectionStringBuilder;
            _eventSerializer = eventSerializer;
            _snapshotSerializer = snapshotSerializer;

            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                var initEventsCommand = connection.CreateCommand();
                initEventsCommand.CommandText = "CREATE TABLE IF NOT EXISTS Events (Id TEXT, ActorName TEXT, EventIndex REAL, EventData TEXT)";
                initEventsCommand.ExecuteNonQuery();

                var initSnapshotsCommand = connection.CreateCommand();
                initSnapshotsCommand.CommandText = "CREATE TABLE IF NOT EXISTS Snapshots (Id TEXT, ActorName TEXT, SnapshotIndex REAL, SnapshotData TEXT)";
                initSnapshotsCommand.ExecuteNonQuery();
            }
        }

        public Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = "DELETE FROM Events WHERE ActorName = $actorName AND EventIndex <= $inclusiveToIndex";
                deleteCommand.Parameters.AddWithValue("$actorName", actorName);
                deleteCommand.Parameters.AddWithValue("$inclusiveToIndex", inclusiveToIndex);
                deleteCommand.ExecuteNonQuery();
            }

            return Task.FromResult(0);
        }

        public Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = "DELETE FROM Snapshots WHERE ActorName = $actorName AND SnapshotIndex <= $inclusiveToIndex";
                deleteCommand.Parameters.AddWithValue("$actorName", actorName);
                deleteCommand.Parameters.AddWithValue("$inclusiveToIndex", inclusiveToIndex);
                deleteCommand.ExecuteNonQuery();
            }

            return Task.FromResult(0);
        }

        public Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                    
                var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = "SELECT EventIndex, EventData FROM Events WHERE ActorName = $ActorName AND EventIndex >= $IndexStart AND EventIndex <= $IndexEnd ORDER BY EventIndex ASC";
                selectCommand.Parameters.AddWithValue("$ActorName", actorName);
                selectCommand.Parameters.AddWithValue("$IndexStart", indexStart);
                selectCommand.Parameters.AddWithValue("$IndexEnd", indexEnd);

                var indexes = new List<long>();

                using (var reader = selectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        indexes.Add(Convert.ToInt64(reader["EventIndex"]));

                        callback(_eventSerializer.Deserialize(reader["EventData"].ToString()));
                    }
                }

                var result = indexes.Any() ? indexes.LastOrDefault() : -1;
                    
                return Task.FromResult(result);
            }
        }

        public Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            object snapshot = null;
            long index = 0;

            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = "SELECT SnapshotIndex, SnapshotData FROM Snapshots WHERE ActorName = $ActorName ORDER BY SnapshotIndex DESC LIMIT 1";
                selectCommand.Parameters.AddWithValue("$ActorName", actorName);

                using (var reader = selectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        snapshot = _snapshotSerializer.Deserialize(reader["SnapshotData"].ToString());
                        index = Convert.ToInt64(reader["SnapshotIndex"]);
                    }
                }
            }

            return Task.FromResult((snapshot, index));
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            var item = new Event(actorName, index, _eventSerializer.Serialize(@event));

            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = "INSERT INTO Events (Id, ActorName, EventIndex, EventData) VALUES ($Id, $ActorName, $EventIndex, $EventData)";
                insertCommand.Parameters.AddWithValue("$Id", item.Id);
                insertCommand.Parameters.AddWithValue("$ActorName", item.ActorName);
                insertCommand.Parameters.AddWithValue("$EventIndex", item.EventIndex);
                insertCommand.Parameters.AddWithValue("$EventData", item.EventData);
                await insertCommand.ExecuteNonQueryAsync();
            }

            return index + 1;
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            var item = new Snapshot(actorName, index, _snapshotSerializer.Serialize(snapshot));

            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = "INSERT INTO Snapshots (Id, ActorName, SnapshotIndex, SnapshotData) VALUES ($Id, $ActorName, $SnapshotIndex, $SnapshotData)";
                insertCommand.Parameters.AddWithValue("$Id", item.Id);
                insertCommand.Parameters.AddWithValue("$ActorName", item.ActorName);
                insertCommand.Parameters.AddWithValue("$SnapshotIndex", item.SnapshotIndex);
                insertCommand.Parameters.AddWithValue("$SnapshotData", item.SnapshotData);
                await insertCommand.ExecuteNonQueryAsync();
            }
        }
    }
}