﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using SQLite;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tests")]

namespace SimpleObjectCache
{

    public class SqliteObjectCache : IBulkObjectCache
    {
        private static SQLiteAsyncConnection _connection;
        private string _databasePath;

        /// <summary>
        /// Database path, filename included.
        /// </summary>
        public string DatabasePath
        {
            get
            {
                if (_databasePath == null)
                    throw new SimpleObjectCacheDatabasePathNullException();

                return _databasePath;
            }
            set { _databasePath = value; }
        }


        private SQLiteAsyncConnection GetConnection()
        {
            if (_connection != null) return _connection;

            _connection = PlatformConnection();
			_connection.CreateTableAsync<CacheElement>().ConfigureAwait(false);

            return _connection;
        }

        /// <summary>
        /// Returns the appropriate platform connection.
        /// </summary>
        /// <returns>The platform connection.</returns>
        protected SQLiteAsyncConnection PlatformConnection()
        {
            return new SQLiteAsyncConnection(DatabasePath);
        }

        /// <summary>
        /// Deserializes a byte array into an object.
        /// </summary>
        /// <typeparam name="T">Object type.</typeparam>
        /// <param name="data">Input array.</param>
        /// <returns>The object resulting from the deseralization of the input array.</returns>
        private static T DeserializeObject<T>(byte[] data)
        {
            var serializer = JsonSerializer.Create();
            var reader = new BsonReader(new MemoryStream(data));

	    return serializer.Deserialize<T>(reader);
        }

	/// <summary>
	/// Serializes an object into a byte array.
	/// </summary>
	/// <typeparam name="T">Object type.</typeparam>
	/// <param name="value">Object instance.</param>
	/// <returns>A byte array representing the serialization of the input object.</returns>
	private static byte [] SerializeObject<T>(T value)
	{
	    var serializer = JsonSerializer.Create();
	    var ms = new MemoryStream();
	    var writer = new BsonWriter(ms);
	    serializer.Serialize(writer, value);
	    return ms.ToArray();
	}

        /// <summary>
        /// This method is called immediately before writing any data to disk.
        /// Override this in encrypting data stores in order to encrypt the
        /// data.
        /// </summary>
        /// <param name="data">The byte data about to be written to disk.</param>
        /// <returns>A result representing the encrypted data</returns>
        protected virtual byte[] BeforeWriteToDiskFilter(byte[] data)
        {
            return data;
        }

        /// <summary>
        /// This method is called immediately after reading any data to disk.
        /// Override this in encrypting data stores in order to decrypt the
        /// data.
        /// </summary>
        /// <param name="data">The byte data that has just been read from
        /// disk.</param>
        /// <returns>A result representing the decrypted data</returns>
        protected virtual byte[] AfterReadFromDiskFilter(byte[] data)
        {
            return data;
        }

        public async Task<T> Get<T>(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var element = await GetConnection().FindAsync<CacheElement>(key).ConfigureAwait(false);
            if (element == null)
                throw new KeyNotFoundException(nameof(key));

            return DeserializeObject<T>(AfterReadFromDiskFilter(element.Value));
        }

        public async Task<DateTimeOffset?> GetCreatedAt(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var element = await GetConnection().FindAsync<CacheElement>(key);

            return element?.CreatedAt;
        }

        public async Task<IEnumerable<T>> GetAll<T>()
        {
            var query = GetConnection().Table<CacheElement>().Where(v => v.TypeName == typeof (T).FullName);

            var elements = new List<T>();
            await query.ToListAsync().ContinueWith(t =>
            {
                elements.AddRange(t.Result.Select(element => DeserializeObject<T>(AfterReadFromDiskFilter(element.Value))));
            }
	    );
            return elements.AsEnumerable();
        }

        public async Task<int> Insert<T>(string key, T value, DateTimeOffset? absoluteExpiration = null)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var data = BeforeWriteToDiskFilter(SerializeObject(value));
            var exp = (absoluteExpiration ?? DateTimeOffset.MaxValue).UtcDateTime;
            var createdAt = DateTimeOffset.Now.UtcDateTime;

            return await GetConnection().InsertOrReplaceAsync(new CacheElement()
            {
                Key = key,
                TypeName = typeof (T).FullName,
                Value = data,
                CreatedAt = createdAt,
                Expiration = exp
            }).ConfigureAwait(false);
        }

        public async Task<int> Invalidate<T>(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var element = await GetConnection().FindAsync<CacheElement>(key).ConfigureAwait(false);
            if (element == null)
                throw new KeyNotFoundException(nameof(key));

            var typeName = typeof (T).FullName;
            if (element.TypeName != typeName)
                throw new SimpleObjectCacheTypeMismatchException();

            return await GetConnection().DeleteAsync(element).ConfigureAwait(false);
        }

        public async Task<int> InvalidateAll<T>()
        {
            var typeName = typeof (T).FullName;
            return await GetConnection().ExecuteAsync($"DELETE FROM CacheElement WHERE TypeName = '{typeName}'");
        }

        public async Task<int> Vacuum()
        {
            var challenge = DateTime.UtcNow.Ticks;
            var deleted = await GetConnection().ExecuteAsync($"DELETE FROM CacheElement WHERE Expiration < {challenge}");

            await GetConnection().ExecuteAsync("VACUUM");

            return deleted;
        }

        public void Dispose()
        {
                _connection = null;
        }

        public async Task<IDictionary<string, T>> Get<T>(IEnumerable<string> keys)
        {
            var results = new Dictionary<string, T>();
            foreach (var key in keys)
            {
                try
                {
                    var result = await Get<T>(key);
                    results.Add(key, result);
                }
		catch (KeyNotFoundException) { }
            }
            return results;
        }

        public async Task<int> Insert<T>(IDictionary<string, T> keyValuePairs, DateTimeOffset? absoluteExpiration = null)
        {
            var inserted = 0;
            foreach (var keyValuePair in keyValuePairs)
            {
                inserted += await Insert(keyValuePair.Key, keyValuePair.Value, absoluteExpiration);
            }
            return inserted;
        }

        public async Task<int> Invalidate<T>(IEnumerable<string> keys)
        {
            var invalidated = 0;
            foreach (var key in keys)
            {
                try
                {
                    invalidated += await Invalidate<T>(key);
                }
                catch (KeyNotFoundException) { }
            }
            return invalidated;
        }

        public async Task<IDictionary<string, DateTimeOffset?>> GetCreatedAt(IEnumerable<string> keys)
        {
            var results = new Dictionary<string, DateTimeOffset?>();
            foreach (var key in keys)
            {
                results.Add(key, await GetCreatedAt(key));
            }
            return results;
        }
    }
}
