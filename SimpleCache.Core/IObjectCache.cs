﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Amica.vNext.SimpleCache
{
    /// <summary>
    /// IObjectCache is the core interface on which SimpleCache is built, it is an
    /// interface describing an asynchronous persistent key-value store. 
    /// </summary>
    public interface IObjectCache : IDisposable
    {
        /// <summary>
        /// Retrieve an object of type T from the key-value cache. 
        /// </summary>
        /// <param name="key">The key to return asynchronously.</param>
        /// <returns>An object stored in the cache, or null if key was not found.</returns>
        Task<T> GetAsync<T>(string key);

        /// <summary>
        /// Retrieve all objects of type T from the key-value cache. 
        /// </summary>
        /// <returns>Objects of type T stored in the cache.</returns>
	Task<IEnumerable<T>> GetAllAsync<T>();

        /// <summary>
        /// Insert an object into the cache, with the specified key and expiration
        /// date. If the key exists the cache record is replaced.
        /// </summary>
        /// <param name="key">The key to use for the data.</param>
        /// <param name="value">The value to save in the cache.</param>
        /// <param name="absoluteExpiration">An optional expiration date.
        /// After the specified date, the key-value pair should be removed.</param>
	/// <returns> The number of rows inserted.</returns>
        Task<int> InsertAsync<T>(string key, T value, DateTimeOffset? absoluteExpiration = null);

        /// <summary>
        /// Remove an object from the cache. If the key doesn't exist, this method
        /// should do nothing and return (*not* throw KeyNotFoundException).
        /// </summary>
        /// <param name="key">The key to remove from the cache.</param>
	/// <returns>The number of objects deleted.</returns>
        Task<int> InvalidateAsync<T>(string key);

        /// <summary>
        /// Invalidate all entries in the cache (i.e. clear it). Note that
        /// this method is blocking and incurs a significant performance
        /// penalty if used while the cache is being used on other threads. 
        /// </summary>
	/// <returns>The number of objects deleted.</returns>
        Task<int> InvalidateAllAsync();

        /// <summary>
        /// Invalidate all objects T in the cache (i.e. clear it). Note that
        /// this method is blocking and incurs a significant performance
        /// penalty if used while the cache is being used on other threads. 
        /// </summary>
        Task InvalidateAllAsync<T>();

        /// <summary>
        /// This method eagerly removes all expired keys from the blob cache, as
        /// well as does any cleanup operations that makes sense (Hint: on SQLite3
        /// it does a Vacuum)
        /// </summary>
        Task VacuumAsync();

        /// <summary>
        /// This method guarantees that all in-flight inserts have completed
        /// and any indexes have been written to disk.
        /// </summary>
        Task FlushAsync();

        /// <summary>
        /// Return all keys in the cache. Note that this method is normally
        /// for diagnostic / testing purposes, and that it is not guaranteed
        /// to be accurate with respect to in-flight requests.
        /// </summary>
        /// <returns>A list of valid keys for the cache.</returns>
        Task<IEnumerable<string>> GetAllKeysAsync();

	/// <summary>
        /// Returns the time that the key was added to the cache, or returns 
        /// null if the key isn't in the cache.
        /// </summary>
        /// <param name="key">The key to return the date for.</param>
        /// <returns>The date the key was created on.</returns>
        Task<DateTimeOffset?> GetCreatedAtAsync<T>(string key);

        /// <summary>
        /// Returns the times that the keys were added to the cache, or returns 
        /// null if a key isn't in the cache.
        /// </summary>
        /// <param name="keys">The keys to return the date for.</param>
        /// <returns>The date the key was created on.</returns>
        Task<IDictionary<string, DateTimeOffset?>> GetCreatedAt(IEnumerable<string> keys);
    }

    public interface IObjectsCache : IObjectCache
    {
        /// <summary>
        /// Get several objects from the cache and deserialize it via the JSON
        /// serializer.
        /// </summary>
        /// <param name="keys">The key to look up in the cache.</param>
        /// <returns>The objects in the cache.</returns>
        Task<IDictionary<string, T>> GetAsync<T>(IEnumerable<string> keys);

        /// <summary>
        /// Insert several objects into the cache, via the JSON serializer. 
        /// Similarly to InsertAll, partial inserts should not happen.
        /// </summary>
        /// <param name="keyValuePairs">The data to insert into the cache</param>
        /// <param name="absoluteExpiration">An optional expiration date.</param>
	/// <returns>The number of objects deleted.</returns>
        Task<int> InsertAsync<T>(IDictionary<string, T> keyValuePairs, DateTimeOffset? absoluteExpiration = null);

        /// <summary>
        /// Invalidates several objects from the cache. It is important that the Type
        /// Parameter for this method be correct.
        /// </summary>
        /// <param name="keys">The key to invalidate.</param>
	/// <returns>The number of objects deleted.</returns>
        Task<int> InvalidateAsync<T>(IEnumerable<string> keys);

    }
}
